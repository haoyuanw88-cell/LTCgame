package analytics

import (
	"testing"
	"time"
)

func TestAccessTokenRoundTrip(t *testing.T) {
	oldKey := secrets.AccessTokenKey
	secrets.AccessTokenKey = "unit-test-key-that-is-not-used-outside-tests"
	t.Cleanup(func() { secrets.AccessTokenKey = oldKey })

	token := issueAccessToken("P000042", time.Now().Add(time.Hour))
	playerID, err := verifyAccessToken(token)
	if err != nil {
		t.Fatalf("verifyAccessToken() error = %v", err)
	}
	if playerID != "P000042" {
		t.Fatalf("player id = %s, want P000042", playerID)
	}
}

func TestAccessTokenRejectsTamperingAndExpiry(t *testing.T) {
	oldKey := secrets.AccessTokenKey
	secrets.AccessTokenKey = "unit-test-key-that-is-not-used-outside-tests"
	t.Cleanup(func() { secrets.AccessTokenKey = oldKey })

	token := issueAccessToken("P000007", time.Now().Add(time.Hour))
	if _, err := verifyAccessToken(token + "x"); err == nil {
		t.Fatal("tampered token should be rejected")
	}
	if _, err := verifyAccessToken(issueAccessToken("P000007", time.Now().Add(-time.Second))); err == nil {
		t.Fatal("expired token should be rejected")
	}
}

func TestAssessmentTokenIsBoundToPlayerSessionAndGame(t *testing.T) {
	oldKey := secrets.AccessTokenKey
	secrets.AccessTokenKey = "unit-test-key-that-is-not-used-outside-tests"
	t.Cleanup(func() { secrets.AccessTokenKey = oldKey })

	token := issueAssessmentToken("P000007", "S00000012", "STP", time.Now().Add(time.Hour))
	if err := verifyAssessmentToken(token, "P000007", "S00000012", "STP"); err != nil {
		t.Fatalf("verifyAssessmentToken() error = %v", err)
	}
	if err := verifyAssessmentToken(token, "P000008", "S00000012", "STP"); err == nil {
		t.Fatal("assessment token should reject another player")
	}
	if err := verifyAssessmentToken(token, "P000007", "S00000013", "STP"); err == nil {
		t.Fatal("assessment token should reject another session")
	}
}
