package analytics

import "testing"

func TestAllowedGoogleClientIDsIncludeUnityDesktop(t *testing.T) {
	const desktopClientID = "969364101892-hc9laqgknt463hrnad8tf0jkbi5qniij.apps.googleusercontent.com"
	if _, ok := allowedGoogleClientIDs[desktopClientID]; !ok {
		t.Fatalf("Unity desktop OAuth client ID is not allow-listed")
	}
}

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
