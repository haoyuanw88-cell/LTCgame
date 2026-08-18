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
	PlayerID string
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
	return encoreauth.UID(playerID), &AuthData{PlayerID: playerID}, nil
}

func issueAccessToken(playerID string, expiresAt time.Time) string {
	payload := fmt.Sprintf("v2.%s.%d", playerID, expiresAt.Unix())
	signature := signToken(payload)
	return payload + "." + signature
}

func verifyAccessToken(token string) (string, error) {
	parts := strings.Split(token, ".")
	if len(parts) != 4 || (parts[0] != "v1" && parts[0] != "v2") {
		return "", fmt.Errorf("invalid token format")
	}
	payload := strings.Join(parts[:3], ".")
	expected, err := base64.RawURLEncoding.DecodeString(signToken(payload))
	if err != nil {
		return "", err
	}
	actual, err := base64.RawURLEncoding.DecodeString(parts[3])
	if err != nil || !hmac.Equal(expected, actual) {
		return "", fmt.Errorf("invalid token signature")
	}
	expiresUnix, err := strconv.ParseInt(parts[2], 10, 64)
	if err != nil || time.Now().Unix() >= expiresUnix {
		return "", fmt.Errorf("expired token")
	}
	playerID := parts[1]
	if parts[0] == "v1" {
		legacyID, parseErr := strconv.ParseInt(parts[1], 10, 64)
		if parseErr != nil || legacyID <= 0 {
			return "", fmt.Errorf("invalid legacy player id")
		}
		playerID = fmt.Sprintf("P%06d", legacyID)
	}
	if !validPlayerID(playerID) {
		return "", fmt.Errorf("invalid player id")
	}
	return playerID, nil
}

func signToken(payload string) string {
	mac := hmac.New(sha256.New, []byte(secrets.AccessTokenKey))
	_, _ = mac.Write([]byte(payload))
	return base64.RawURLEncoding.EncodeToString(mac.Sum(nil))
}

func issueAssessmentToken(playerID, sessionID, gameCode string, expiresAt time.Time) string {
	payload := fmt.Sprintf("s1|%s|%s|%s|%d", playerID, sessionID, gameCode, expiresAt.Unix())
	encoded := base64.RawURLEncoding.EncodeToString([]byte(payload))
	return encoded + "." + signToken(payload)
}

func verifyAssessmentToken(token, playerID, sessionID, gameCode string) error {
	parts := strings.Split(token, ".")
	if len(parts) != 2 {
		return fmt.Errorf("invalid assessment token format")
	}
	payloadBytes, err := base64.RawURLEncoding.DecodeString(parts[0])
	if err != nil {
		return fmt.Errorf("invalid assessment token payload")
	}
	payload := string(payloadBytes)
	expected, err := base64.RawURLEncoding.DecodeString(signToken(payload))
	if err != nil {
		return err
	}
	actual, err := base64.RawURLEncoding.DecodeString(parts[1])
	if err != nil || !hmac.Equal(expected, actual) {
		return fmt.Errorf("invalid assessment token signature")
	}
	fields := strings.Split(payload, "|")
	if len(fields) != 5 || fields[0] != "s1" || fields[1] != playerID || fields[2] != sessionID || fields[3] != gameCode {
		return fmt.Errorf("assessment token does not match request")
	}
	expiresUnix, err := strconv.ParseInt(fields[4], 10, 64)
	if err != nil || time.Now().Unix() >= expiresUnix {
		return fmt.Errorf("assessment token expired")
	}
	return nil
}

func currentPlayerID() (string, error) {
	data, ok := encoreauth.Data().(*AuthData)
	if !ok || data == nil || !validPlayerID(data.PlayerID) {
		return "", unauthenticated("player identity is unavailable")
	}
	return data.PlayerID, nil
}

func validPlayerID(value string) bool {
	if len(value) != 7 || value[0] != 'P' {
		return false
	}
	number, err := strconv.Atoi(value[1:])
	return err == nil && number > 0
}

func unauthenticated(message string) error {
	return &errs.Error{Code: errs.Unauthenticated, Message: message}
}

func invalidArgument(message string) error {
	return &errs.Error{Code: errs.InvalidArgument, Message: message}
}
