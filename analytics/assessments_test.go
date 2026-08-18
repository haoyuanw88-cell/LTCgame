package analytics

import (
	"testing"
	"time"
)

func TestValidateAssessment(t *testing.T) {
	start := time.Now().UTC().Add(-time.Minute)
	valid := &AssessmentRequest{
		SessionID:        "S00000001",
		SessionToken:     "signed-test-token",
		GameCode:         "STP",
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

func TestCalculateGameRewardMatchesUnityRules(t *testing.T) {
	tests := []struct {
		name     string
		gameCode string
		trials   []TrialRequest
		want     int
	}{
		{
			name: "stroop reward",
			gameCode: "STP",
			trials: []TrialRequest{
				{EventCode: "RSP", OutcomeCode: "COR"},
				{EventCode: "RSP", OutcomeCode: "COR"},
				{EventCode: "RSP", OutcomeCode: "INC"},
			},
			want: 2, // two correct + floor((10+10-5)/20)
		},
		{
			name: "number order reward",
			gameCode: "ORD",
			trials: []TrialRequest{
				{EventCode: "RSP", OutcomeCode: "COR"},
				{EventCode: "RSP", OutcomeCode: "COR"},
				{EventCode: "RND", OutcomeCode: "COR"},
			},
			want: 4,
		},
		{
			name: "number sum reward",
			gameCode: "SUM",
			trials: []TrialRequest{
				{EventCode: "RND", OutcomeCode: "COR"},
				{EventCode: "SEL", OutcomeCode: "INC"},
			},
			want: 4, // three for the round + floor((20-5)/10)
		},
	}
	for _, test := range tests {
		t.Run(test.name, func(t *testing.T) {
			if got := calculateGameReward(test.gameCode, test.trials); got != test.want {
				t.Fatalf("calculateGameReward() = %d, want %d", got, test.want)
			}
		})
	}
}
