package analytics

import (
	"context"
	"crypto/hmac"
	"crypto/sha256"
	"encoding/base64"
	"fmt"
	"strconv"
	"strings"
	"time"

	encoreauth "encore.dev/beta/auth"
	"encore.dev/beta/errs"
)

var secrets struct {
	AccessTokenKey string
}

type AuthParams struct {
	Authorization string `header:"Authorization"`
}

type AuthData struct {
	PlayerID int64
}

// AuthHandler validates the short-lived bearer token returned by guest sign-in.
// Google/Firebase can replace token issuance later without changing game APIs.
//
//encore:authhandler
func AuthHandler(_ context.Context, p *AuthParams) (encoreauth.UID, *AuthData, error) {
	if p == nil || !strings.HasPrefix(p.Authorization, "Bearer ") {
		return "", nil, unauthenticated("missing bearer token")
	}
	playerID, err := verifyAccessToken(strings.TrimSpace(strings.TrimPrefix(p.Authorization, "Bearer ")))
	if err != nil {
		return "", nil, unauthenticated("invalid or expired bearer token")
	}
	return encoreauth.UID(strconv.FormatInt(playerID, 10)), &AuthData{PlayerID: playerID}, nil
}

func issueAccessToken(playerID int64, expiresAt time.Time) string {
	payload := fmt.Sprintf("v1.%d.%d", playerID, expiresAt.Unix())
	signature := signToken(payload)
	return payload + "." + signature
}

func verifyAccessToken(token string) (int64, error) {
	parts := strings.Split(token, ".")
	if len(parts) != 4 || parts[0] != "v1" {
		return 0, fmt.Errorf("invalid token format")
	}
	payload := strings.Join(parts[:3], ".")
	expected, err := base64.RawURLEncoding.DecodeString(signToken(payload))
	if err != nil {
		return 0, err
	}
	actual, err := base64.RawURLEncoding.DecodeString(parts[3])
	if err != nil || !hmac.Equal(expected, actual) {
		return 0, fmt.Errorf("invalid token signature")
	}
	playerID, err := strconv.ParseInt(parts[1], 10, 64)
	if err != nil || playerID <= 0 {
		return 0, fmt.Errorf("invalid player id")
	}
	expiresUnix, err := strconv.ParseInt(parts[2], 10, 64)
	if err != nil || time.Now().Unix() >= expiresUnix {
		return 0, fmt.Errorf("expired token")
	}
	return playerID, nil
}

func signToken(payload string) string {
	mac := hmac.New(sha256.New, []byte(secrets.AccessTokenKey))
	_, _ = mac.Write([]byte(payload))
	return base64.RawURLEncoding.EncodeToString(mac.Sum(nil))
}

func currentPlayerID() (int64, error) {
	data, ok := encoreauth.Data().(*AuthData)
	if !ok || data == nil || data.PlayerID <= 0 {
		return 0, unauthenticated("player identity is unavailable")
	}
	return data.PlayerID, nil
}

func unauthenticated(message string) error {
	return &errs.Error{Code: errs.Unauthenticated, Message: message}
}

func invalidArgument(message string) error {
	return &errs.Error{Code: errs.InvalidArgument, Message: message}
}
