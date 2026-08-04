package analytics

import (
	"testing"
	"time"
)

func TestValidateAssessment(t *testing.T) {
	start := time.Now().UTC().Add(-time.Minute)
	valid := &AssessmentRequest{
		SessionID:        "1234567890abcdef",
		GameCode:         "stroop_color_match",
		StartedAtUTC:     start,
		EndedAtUTC:       start.Add(time.Minute),
		CompletionStatus: "completed",
	}
	if err := validateAssessment(valid); err != nil {
		t.Fatalf("valid assessment rejected: %v", err)
	}

	invalid := *valid
	invalid.EndedAtUTC = invalid.StartedAtUTC
	if err := validateAssessment(&invalid); err == nil {
		t.Fatal("non-positive duration should be rejected")
	}
}

func TestCleanTextUsesRuneLimit(t *testing.T) {
	if got := cleanText("  測試玩家名稱  ", 4); got != "測試玩家" {
		t.Fatalf("cleanText() = %q, want %q", got, "測試玩家")
	}
}
