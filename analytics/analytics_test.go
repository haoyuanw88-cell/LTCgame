package analytics

import (
	"context"
	"errors"
	"testing"
	"time"

	"encore.dev/beta/errs"
)

func TestCalculateScoreUsesDomainProfile(t *testing.T) {
	params := &SubmitSessionParams{
		Domain:            DomainAttention,
		Difficulty:        2,
		CorrectCount:      8,
		WrongCount:        2,
		OmittedCount:      0,
		AverageReactionMS: 1500,
	}

	got := calculateScore(params)

	if got.score != 87 {
		t.Fatalf("score = %d, want 87", got.score)
	}
	if got.accuracy != 0.8 {
		t.Errorf("accuracy = %v, want 0.8", got.accuracy)
	}
	if got.speed != 1 {
		t.Errorf("speed = %v, want 1", got.speed)
	}
	if got.completion != 1 {
		t.Errorf("completion = %v, want 1", got.completion)
	}
}

func TestSubmitSessionIsIdempotent(t *testing.T) {
	resetTestState(t)
	ctx := context.Background()
	params := validSession("session-1", DomainAttention, testNow().Add(-time.Hour))

	first, err := SubmitSession(ctx, params)
	if err != nil {
		t.Fatalf("first SubmitSession() error = %v", err)
	}
	second, err := SubmitSession(ctx, params)
	if err != nil {
		t.Fatalf("second SubmitSession() error = %v", err)
	}

	if !first.Created {
		t.Fatal("first submission should be created")
	}
	if second.Created {
		t.Fatal("retry should return the stored session without creating another record")
	}
	if first.Score != second.Score {
		t.Errorf("retry score = %d, want %d", second.Score, first.Score)
	}
}

func TestSubmitSessionRejectsConflictingSessionID(t *testing.T) {
	resetTestState(t)
	ctx := context.Background()
	first := validSession("session-1", DomainAttention, testNow().Add(-time.Hour))
	if _, err := SubmitSession(ctx, first); err != nil {
		t.Fatalf("first SubmitSession() error = %v", err)
	}

	conflict := *first
	conflict.CorrectCount = 6
	conflict.WrongCount = 4
	_, err := SubmitSession(ctx, &conflict)
	if err == nil {
		t.Fatal("conflicting submission should fail")
	}
	var encoreErr *errs.Error
	if !errors.As(err, &encoreErr) || encoreErr.Code != errs.AlreadyExists {
		t.Fatalf("error = %v, want AlreadyExists", err)
	}
}

func TestSummaryAndTrendsAggregateByDayAndDomain(t *testing.T) {
	resetTestState(t)
	ctx := context.Background()

	sessions := []*SubmitSessionParams{
		validSession("attention-day-1", DomainAttention, time.Date(2026, 7, 23, 2, 0, 0, 0, time.UTC)),
		validSession("processing-day-1", DomainProcessingSpeed, time.Date(2026, 7, 23, 3, 0, 0, 0, time.UTC)),
		validSession("attention-day-2", DomainAttention, time.Date(2026, 7, 24, 4, 0, 0, 0, time.UTC)),
	}
	sessions[2].CorrectCount = 10
	sessions[2].WrongCount = 0

	for _, session := range sessions {
		if _, err := SubmitSession(ctx, session); err != nil {
			t.Fatalf("SubmitSession(%s) error = %v", session.SessionID, err)
		}
	}

	summary, err := GetCognitiveSummary(ctx, "player-1", &GetCognitiveSummaryParams{
		RangeDays:          30,
		TimezoneOffsetMins: 480,
	})
	if err != nil {
		t.Fatalf("GetCognitiveSummary() error = %v", err)
	}
	if summary.RecordCount != 3 {
		t.Errorf("record_count = %d, want 3", summary.RecordCount)
	}
	if summary.EffectiveDays != 2 {
		t.Errorf("effective_days = %d, want 2", summary.EffectiveDays)
	}
	if summary.AttentionScore != 94 {
		t.Errorf("attention_score = %d, want 94", summary.AttentionScore)
	}
	if summary.ProcessingSpeedScore != 82 {
		t.Errorf("processing_speed_score = %d, want 82", summary.ProcessingSpeedScore)
	}
	if summary.ExecutiveFunctionScore != 0 {
		t.Errorf("executive_function_score = %d, want 0", summary.ExecutiveFunctionScore)
	}
	if summary.OverallScore != 88 {
		t.Errorf("overall_score = %d, want 88", summary.OverallScore)
	}

	trends, err := GetCognitiveTrends(ctx, "player-1", &GetCognitiveTrendsParams{
		RangeDays:          30,
		Domain:             string(DomainAttention),
		TimezoneOffsetMins: 480,
	})
	if err != nil {
		t.Fatalf("GetCognitiveTrends() error = %v", err)
	}
	if trends.EffectiveDays != 2 || len(trends.Points) != 2 {
		t.Fatalf("trend days = %d, points = %d, want 2 and 2", trends.EffectiveDays, len(trends.Points))
	}
	if trends.Points[0].Date != "2026-07-23" || trends.Points[1].Date != "2026-07-24" {
		t.Errorf("trend dates = %q, %q", trends.Points[0].Date, trends.Points[1].Date)
	}
	if trends.Points[0].Score != 87 || trends.Points[1].Score != 100 {
		t.Errorf("trend scores = %d, %d, want 87 and 100", trends.Points[0].Score, trends.Points[1].Score)
	}
}

func TestTrendsValidateRangeAndDomain(t *testing.T) {
	resetTestState(t)
	ctx := context.Background()

	_, err := GetCognitiveTrends(ctx, "player-1", &GetCognitiveTrendsParams{
		RangeDays: 14,
		Domain:    string(DomainOverall),
	})
	assertInvalidArgument(t, err)

	_, err = GetCognitiveTrends(ctx, "player-1", &GetCognitiveTrendsParams{
		RangeDays: 30,
		Domain:    "memory",
	})
	assertInvalidArgument(t, err)
}

func TestSubmitSessionRequiresPlayedAt(t *testing.T) {
	resetTestState(t)
	params := validSession("session-1", DomainAttention, time.Time{})

	_, err := SubmitSession(context.Background(), params)

	assertInvalidArgument(t, err)
}

func resetTestState(t *testing.T) {
	t.Helper()
	oldRepository := repository
	oldNow := nowFunc
	repository = newMemorySessionRepository()
	nowFunc = testNow
	t.Cleanup(func() {
		repository = oldRepository
		nowFunc = oldNow
	})
}

func validSession(sessionID string, domain Domain, playedAt time.Time) *SubmitSessionParams {
	return &SubmitSessionParams{
		SessionID:          sessionID,
		PlayerID:           "player-1",
		GameID:             "demo-game",
		Domain:             domain,
		Difficulty:         2,
		CorrectCount:       8,
		WrongCount:         2,
		OmittedCount:       0,
		AverageReactionMS:  1500,
		DurationSeconds:    90,
		PlayedAt:           playedAt,
		TimezoneOffsetMins: 480,
	}
}

func testNow() time.Time {
	return time.Date(2026, 7, 25, 4, 0, 0, 0, time.UTC)
}

func assertInvalidArgument(t *testing.T, err error) {
	t.Helper()
	if err == nil {
		t.Fatal("expected InvalidArgument error")
	}
	var encoreErr *errs.Error
	if !errors.As(err, &encoreErr) || encoreErr.Code != errs.InvalidArgument {
		t.Fatalf("error = %v, want InvalidArgument", err)
	}
}
