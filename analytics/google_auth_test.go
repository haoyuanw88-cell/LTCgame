package analytics

import "testing"

func TestGoogleDisplayNamePrefersVerifiedClaim(t *testing.T) {
	claims := map[string]interface{}{"name": "  Google 玩家  "}
	if got := googleDisplayName(claims, "本機名稱"); got != "Google 玩家" {
		t.Fatalf("googleDisplayName() = %q", got)
	}
}

func TestGoogleDisplayNameFallsBackToLocalName(t *testing.T) {
	if got := googleDisplayName(nil, "  本機名稱  "); got != "本機名稱" {
		t.Fatalf("googleDisplayName() = %q", got)
	}
}
