package analytics

import (
	"context"
	"crypto/sha256"
	"encoding/hex"
	"strings"
	"time"
)

const accessTokenLifetime = 30 * 24 * time.Hour

// GuestSignIn creates or restores one player from this installation.
//
//encore:api public method=POST path=/api/v2/auth/guest
func GuestSignIn(ctx context.Context, p *GuestSignInRequest) (*PlayerSessionResponse, error) {
	if p == nil {
		return nil, invalidArgument("request body is required")
	}
	installationUID := strings.TrimSpace(p.InstallationUID)
	if len(installationUID) < 16 || len(installationUID) > 128 {
		return nil, invalidArgument("installationUid must contain 16 to 128 characters")
	}
	displayName := cleanText(p.DisplayName, 40)
	authUID := hashIdentifier(installationUID)

	var playerID string
	var storedName string
	var isNew bool
	err := db.QueryRow(ctx, `
		INSERT INTO player (auth_uid, p_name, last_seen_ts)
		VALUES ($1, NULLIF($2, ''), date_trunc('minute', NOW()))
		ON CONFLICT (auth_uid) DO UPDATE SET
			p_name = COALESCE(NULLIF(EXCLUDED.p_name, ''), player.p_name),
			last_seen_ts = date_trunc('minute', NOW())
		RETURNING p_id, COALESCE(p_name, ''), (xmax = 0)
	`, authUID, displayName).Scan(&playerID, &storedName, &isNew)
	if err != nil {
		return nil, err
	}
	if _, err = db.Exec(ctx, `INSERT INTO wallet (p_id) VALUES ($1) ON CONFLICT (p_id) DO NOTHING`, playerID); err != nil {
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

// Heartbeat marks the authenticated player online. Unity sends this once per minute.
//
//encore:api auth method=POST path=/api/v1/presence/heartbeat
func Heartbeat(ctx context.Context) (*HeartbeatResponse, error) {
	playerID, err := currentPlayerID()
	if err != nil {
		return nil, err
	}
	seenAt := time.Now().UTC().Truncate(time.Minute)
	if _, err = db.Exec(ctx, `UPDATE player SET last_seen_ts = $2 WHERE p_id = $1`, playerID, seenAt); err != nil {
		return nil, err
	}
	return &HeartbeatResponse{PlayerID: playerID, SeenAtUTC: seenAt.Format(time.RFC3339)}, nil
}

// UpdateProfile stores the onboarding fields already collected by Unity.
//
//encore:api auth method=PUT path=/api/v1/profile
func UpdateProfile(ctx context.Context, p *UpdateProfileParams) (*PlayerProfile, error) {
	playerID, err := currentPlayerID()
	if err != nil {
		return nil, err
	}
	if p == nil {
		return nil, invalidArgument("request body is required")
	}
	birthDate, err := time.Parse("2006-01-02", strings.TrimSpace(p.BirthDate))
	if err != nil || birthDate.After(time.Now()) || birthDate.Year() < 1900 {
		return nil, invalidArgument("birthDate must be a valid YYYY-MM-DD date")
	}
	sexCode := compactSexCode(p.SexCode)
	if sexCode == "" {
		return nil, invalidArgument("sexCode is invalid")
	}
	if p.EducationYears < 0 || p.EducationYears > 30 {
		return nil, invalidArgument("educationYears must be between 0 and 30")
	}
	displayName := cleanText(p.DisplayName, 40)
	if _, err = db.Exec(ctx, `
		UPDATE player SET p_name = NULLIF($2, ''), birth_dt = $3, sex_cd = $4, edu_yrs = $5,
			last_seen_ts = date_trunc('minute', NOW())
		WHERE p_id = $1
	`, playerID, displayName, birthDate, sexCode, p.EducationYears); err != nil {
		return nil, err
	}
	return &PlayerProfile{
		PlayerID:       playerID,
		PlayerCode:     playerCode(playerID),
		DisplayName:    displayName,
		BirthDate:      birthDate.Format("2006-01-02"),
		SexCode:        sexCode,
		EducationYears: p.EducationYears,
	}, nil
}

func hashIdentifier(value string) string {
	sum := sha256.Sum256([]byte(value))
	return hex.EncodeToString(sum[:])
}

func playerCode(playerID string) string {
	return strings.TrimSpace(playerID)
}

func compactSexCode(value string) string {
	switch strings.ToLower(strings.TrimSpace(value)) {
	case "m", "male":
		return "M"
	case "f", "female":
		return "F"
	case "x", "other":
		return "X"
	case "n", "prefer_not_to_say":
		return "N"
	default:
		return ""
	}
}

func cleanText(value string, max int) string {
	value = strings.TrimSpace(value)
	runes := []rune(value)
	if len(runes) > max {
		value = string(runes[:max])
	}
	return value
}
