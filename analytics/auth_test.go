package analytics

import (
	"testing"
	"time"
)

func TestAccessTokenRoundTrip(t *testing.T) {
	oldKey := secrets.AccessTokenKey
	secrets.AccessTokenKey = "unit-test-key-that-is-not-used-outside-tests"
	t.Cleanup(func() { secrets.AccessTokenKey = oldKey })

	token := issueAccessToken(42, time.Now().Add(time.Hour))
	playerID, err := verifyAccessToken(token)
	if err != nil {
		t.Fatalf("verifyAccessToken() error = %v", err)
	}
	if playerID != 42 {
		t.Fatalf("player id = %d, want 42", playerID)
	}
}

func TestAccessTokenRejectsTamperingAndExpiry(t *testing.T) {
	oldKey := secrets.AccessTokenKey
	secrets.AccessTokenKey = "unit-test-key-that-is-not-used-outside-tests"
	t.Cleanup(func() { secrets.AccessTokenKey = oldKey })

	token := issueAccessToken(7, time.Now().Add(time.Hour))
	if _, err := verifyAccessToken(token + "x"); err == nil {
		t.Fatal("tampered token should be rejected")
	}
	if _, err := verifyAccessToken(issueAccessToken(7, time.Now().Add(-time.Second))); err == nil {
		t.Fatal("expired token should be rejected")
	}
}
