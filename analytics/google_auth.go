package analytics

import (
	"context"
	"fmt"
	"strings"
	"time"

	"google.golang.org/api/idtoken"
)

// OAuth client IDs are public identifiers, not secrets. Add the Android client
// ID here when the native Android Google sign-in integration is introduced.
var allowedGoogleClientIDs = map[string]struct{}{
	// Existing web client retained for backward compatibility and server flows.
	"969364101892-1nvsfsd5immh713adnn04l87ss9vfo60.apps.googleusercontent.com": {},
	// Unity Editor and Windows desktop Authorization Code + PKCE flow.
	"969364101892-hc9laqgknt463hrnad8tf0jkbi5qniij.apps.googleusercontent.com": {},
}

// GoogleSignIn validates Google's signed ID token on the server and returns the
// same LTC bearer-token format used by the rest of the game APIs.
//
//encore:api public method=POST path=/api/v2/auth/google
func GoogleSignIn(ctx context.Context, p *GoogleSignInRequest) (*PlayerSessionResponse, error) {
	if p == nil {
		return nil, invalidArgument("request body is required")
	}
	idToken := strings.TrimSpace(p.IDToken)
	if idToken == "" || len(idToken) > 8192 {
		return nil, invalidArgument("idToken is required")
	}
	nonce := strings.TrimSpace(p.Nonce)
	if len(nonce) < 16 || len(nonce) > 256 {
		return nil, invalidArgument("nonce is invalid")
	}
	installationUID := strings.TrimSpace(p.InstallationUID)
	if installationUID != "" && (len(installationUID) < 16 || len(installationUID) > 128) {
		return nil, invalidArgument("installationUid must contain 16 to 128 characters")
	}

	// Parsing is used only to select an allow-listed audience. Validate then
	// verifies the Google signature, issuer, audience, and expiration.
	unverified, err := idtoken.ParsePayload(idToken)
	if err != nil {
		return nil, unauthenticated("Google identity token is invalid")
	}
	if _, allowed := allowedGoogleClientIDs[unverified.Audience]; !allowed {
		return nil, unauthenticated("Google identity token has an unexpected audience")
	}
	verified, err := idtoken.Validate(ctx, idToken, unverified.Audience)
	if err != nil || verified.Subject == "" {
		return nil, unauthenticated("Google identity token could not be verified")
	}
	verifiedNonce, _ := verified.Claims["nonce"].(string)
	if verifiedNonce == "" || verifiedNonce != nonce {
		return nil, unauthenticated("Google identity token nonce does not match")
	}

	displayName := googleDisplayName(verified.Claims, p.DisplayName)
	googleAuthUID := hashIdentifier("google:" + verified.Subject)
	guestAuthUID := ""
	if installationUID != "" {
		guestAuthUID = hashIdentifier(installationUID)
	}

	// If this installation already owns a guest player and the Google account is
	// new, convert that row in-place so existing sessions, wallet, and inventory
	// remain attached to the same p_id. Otherwise restore the Google player.
	tx, err := db.Begin(ctx)
	if err != nil {
		return nil, err
	}
	defer tx.Rollback()

	var playerID string
	var storedName string
	var isNew bool
	err = tx.QueryRow(ctx, `
		WITH converted AS (
			UPDATE player
			SET auth_uid = $1,
				p_name = COALESCE(NULLIF($3, ''), p_name),
				last_seen_ts = date_trunc('minute', NOW())
			WHERE $2 <> ''
			  AND auth_uid = $2
			  AND NOT EXISTS (SELECT 1 FROM player WHERE auth_uid = $1)
			RETURNING p_id, COALESCE(p_name, '') AS p_name, FALSE AS is_new
		), upserted AS (
			INSERT INTO player (auth_uid, p_name, last_seen_ts)
			SELECT $1, NULLIF($3, ''), date_trunc('minute', NOW())
			WHERE NOT EXISTS (SELECT 1 FROM converted)
			ON CONFLICT (auth_uid) DO UPDATE SET
				p_name = COALESCE(NULLIF(EXCLUDED.p_name, ''), player.p_name),
				last_seen_ts = date_trunc('minute', NOW())
			RETURNING p_id, COALESCE(p_name, '') AS p_name, (xmax = 0) AS is_new
		)
		SELECT p_id, p_name, is_new FROM converted
		UNION ALL
		SELECT p_id, p_name, is_new FROM upserted
		LIMIT 1
	`, googleAuthUID, guestAuthUID, displayName).Scan(&playerID, &storedName, &isNew)
	if err != nil {
		return nil, fmt.Errorf("store Google player: %w", err)
	}
	playerID = strings.TrimSpace(playerID)
	if _, err = tx.Exec(ctx, `INSERT INTO wallet (p_id) VALUES ($1) ON CONFLICT (p_id) DO NOTHING`, playerID); err != nil {
		return nil, err
	}

	// When both a guest row and a Google row already exist, merge every record
	// owned by this installation into the Google player before deleting the guest.
	if guestAuthUID != "" {
		if _, err = tx.Exec(ctx, `
			UPDATE player target
			SET birth_dt = COALESCE(target.birth_dt, guest.birth_dt),
				sex_cd = COALESCE(target.sex_cd, guest.sex_cd),
				edu_yrs = COALESCE(target.edu_yrs, guest.edu_yrs)
			FROM player guest
			WHERE target.p_id = $1 AND guest.auth_uid = $2 AND guest.p_id <> $1
		`, playerID, guestAuthUID); err != nil {
			return nil, err
		}
		if _, err = tx.Exec(ctx, `
			INSERT INTO inventory (p_id, i_id, qty)
			SELECT $1, source.i_id, source.qty
			FROM inventory source
			JOIN player guest ON guest.p_id = source.p_id
			WHERE guest.auth_uid = $2 AND guest.p_id <> $1
			ON CONFLICT (p_id, i_id) DO UPDATE SET qty = inventory.qty + EXCLUDED.qty
		`, playerID, guestAuthUID); err != nil {
			return nil, err
		}
		if _, err = tx.Exec(ctx, `
			UPDATE game_session SET p_id = $1
			WHERE p_id IN (SELECT p_id FROM player WHERE auth_uid = $2 AND p_id <> $1)
		`, playerID, guestAuthUID); err != nil {
			return nil, err
		}
		if _, err = tx.Exec(ctx, `
			UPDATE coin_tx SET p_id = $1
			WHERE p_id IN (SELECT p_id FROM player WHERE auth_uid = $2 AND p_id <> $1)
		`, playerID, guestAuthUID); err != nil {
			return nil, err
		}
		if _, err = tx.Exec(ctx, `
			UPDATE wallet target SET bal = target.bal + source.bal
			FROM wallet source
			JOIN player guest ON guest.p_id = source.p_id
			WHERE target.p_id = $1 AND guest.auth_uid = $2 AND guest.p_id <> $1
		`, playerID, guestAuthUID); err != nil {
			return nil, err
		}
		if _, err = tx.Exec(ctx, `
			DELETE FROM inventory
			WHERE p_id IN (SELECT p_id FROM player WHERE auth_uid = $2 AND p_id <> $1)
		`, playerID, guestAuthUID); err != nil {
			return nil, err
		}
		if _, err = tx.Exec(ctx, `
			DELETE FROM wallet
			WHERE p_id IN (SELECT p_id FROM player WHERE auth_uid = $2 AND p_id <> $1)
		`, playerID, guestAuthUID); err != nil {
			return nil, err
		}
		if _, err = tx.Exec(ctx, `DELETE FROM player WHERE auth_uid = $2 AND p_id <> $1`, playerID, guestAuthUID); err != nil {
			return nil, err
		}
	}
	if err = tx.Commit(); err != nil {
		return nil, err
	}

	expiresAt := time.Now().UTC().Add(accessTokenLifetime)
	return &PlayerSessionResponse{
		PlayerID:     playerID,
		PlayerCode:   playerCode(playerID),
		DisplayName:  storedName,
		AccessToken:  issueAccessToken(playerID, expiresAt),
		ExpiresAtUTC: expiresAt.Format(time.RFC3339),
		IsNewPlayer:  isNew,
	}, nil
}

func googleDisplayName(claims map[string]interface{}, fallback string) string {
	if name, ok := claims["name"].(string); ok {
		if cleaned := cleanText(name, 40); cleaned != "" {
			return cleaned
		}
	}
	return cleanText(fallback, 40)
}
