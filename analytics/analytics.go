// Service analytics receives raw Unity game results and exposes cognitive
// summaries and trend data for the statistics screen.
package analytics

import (
	"context"
	"errors"
	"strings"
	"time"

	"encore.dev/beta/errs"
)

var (
	repository sessionRepository = newMemorySessionRepository()
	nowFunc                      = time.Now
)

// SubmitSession validates and scores one completed Unity game session.
// SessionID acts as an idempotency key, so an offline client can safely retry.
//
// TODO: Change public to auth after the project authentication service exists.
//
//encore:api public method=POST path=/v1/game-sessions
func SubmitSession(ctx context.Context, params *SubmitSessionParams) (*SessionResult, error) {
	if err := validateSession(params); err != nil {
		return nil, err
	}

	playedAt := params.PlayedAt.UTC()

	breakdown := calculateScore(params)
	stored, created, err := repository.Save(ctx, gameSession{
		SessionID:          strings.TrimSpace(params.SessionID),
		PlayerID:           strings.TrimSpace(params.PlayerID),
		GameID:             strings.TrimSpace(params.GameID),
		Domain:             params.Domain,
		Difficulty:         params.Difficulty,
		CorrectCount:       params.CorrectCount,
		WrongCount:         params.WrongCount,
		OmittedCount:       params.OmittedCount,
		AverageReactionMS:  params.AverageReactionMS,
		DurationSeconds:    params.DurationSeconds,
		PlayedAt:           playedAt,
		TimezoneOffsetMins: normalizeTimezoneOffset(params.TimezoneOffsetMins),
		Score:              breakdown.score,
		Accuracy:           breakdown.accuracy,
		Speed:              breakdown.speed,
		Completion:         breakdown.completion,
	})
	if err != nil {
		if errors.Is(err, errSessionConflict) {
			return nil, &errs.Error{
				Code:    errs.AlreadyExists,
				Message: "session_id already exists with different data",
			}
		}
		return nil, err
	}

	return sessionResult(stored, created), nil
}

// GetCognitiveSummary returns the three score cards shown above the chart.
//
//encore:api public method=GET path=/v1/players/:playerID/cognitive-summary
func GetCognitiveSummary(ctx context.Context, playerID string, params *GetCognitiveSummaryParams) (*CognitiveSummary, error) {
	playerID = strings.TrimSpace(playerID)
	if playerID == "" {
		return nil, invalidArgument("player_id is required")
	}
	if params == nil {
		params = &GetCognitiveSummaryParams{}
	}

	rangeDays, err := normalizeRangeDays(params.RangeDays)
	if err != nil {
		return nil, err
	}
	location, err := timezoneLocation(params.TimezoneOffsetMins)
	if err != nil {
		return nil, err
	}

	sessions, err := repository.List(ctx, playerID, rangeStart(nowFunc(), rangeDays, location))
	if err != nil {
		return nil, err
	}
	return buildSummary(playerID, rangeDays, sessions, location), nil
}

// GetCognitiveTrends returns the selected 7, 30, or 90-day chart series.
//
//encore:api public method=GET path=/v1/players/:playerID/cognitive-trends
func GetCognitiveTrends(ctx context.Context, playerID string, params *GetCognitiveTrendsParams) (*CognitiveTrends, error) {
	playerID = strings.TrimSpace(playerID)
	if playerID == "" {
		return nil, invalidArgument("player_id is required")
	}
	if params == nil {
		params = &GetCognitiveTrendsParams{}
	}

	rangeDays, err := normalizeRangeDays(params.RangeDays)
	if err != nil {
		return nil, err
	}
	domain := Domain(params.Domain)
	if domain == "" {
		domain = DomainOverall
	}
	if !isTrendDomain(domain) {
		return nil, invalidArgument("domain must be overall, attention, processing_speed, or executive_function")
	}
	location, err := timezoneLocation(params.TimezoneOffsetMins)
	if err != nil {
		return nil, err
	}

	sessions, err := repository.List(ctx, playerID, rangeStart(nowFunc(), rangeDays, location))
	if err != nil {
		return nil, err
	}
	points, effectiveDays := buildTrendPoints(sessions, domain, location)

	return &CognitiveTrends{
		PlayerID:      playerID,
		RangeDays:     rangeDays,
		Domain:        domain,
		EffectiveDays: effectiveDays,
		Summary:       buildSummary(playerID, rangeDays, sessions, location),
		Points:        points,
	}, nil
}

func validateSession(params *SubmitSessionParams) error {
	if params == nil {
		return invalidArgument("request body is required")
	}
	if strings.TrimSpace(params.SessionID) == "" {
		return invalidArgument("session_id is required")
	}
	if strings.TrimSpace(params.PlayerID) == "" {
		return invalidArgument("player_id is required")
	}
	if strings.TrimSpace(params.GameID) == "" {
		return invalidArgument("game_id is required")
	}
	if !isSessionDomain(params.Domain) {
		return invalidArgument("domain must be attention, processing_speed, or executive_function")
	}
	if params.Difficulty < 1 || params.Difficulty > 3 {
		return invalidArgument("difficulty must be between 1 and 3")
	}
	if params.CorrectCount < 0 || params.WrongCount < 0 || params.OmittedCount < 0 {
		return invalidArgument("answer counts cannot be negative")
	}
	if params.CorrectCount+params.WrongCount == 0 {
		return invalidArgument("at least one answered item is required")
	}
	if params.AverageReactionMS <= 0 {
		return invalidArgument("average_reaction_ms must be greater than zero")
	}
	if params.DurationSeconds <= 0 {
		return invalidArgument("duration_seconds must be greater than zero")
	}
	if params.PlayedAt.IsZero() {
		return invalidArgument("played_at is required")
	}
	if _, err := timezoneLocation(params.TimezoneOffsetMins); err != nil {
		return err
	}
	return nil
}

func sessionResult(session gameSession, created bool) *SessionResult {
	return &SessionResult{
		SessionID:  session.SessionID,
		PlayerID:   session.PlayerID,
		GameID:     session.GameID,
		Domain:     session.Domain,
		Score:      session.Score,
		Accuracy:   session.Accuracy,
		Speed:      session.Speed,
		Completion: session.Completion,
		Created:    created,
	}
}

func normalizeRangeDays(value int) (int, error) {
	if value == 0 {
		return defaultRangeDays, nil
	}
	switch value {
	case 7, 30, 90:
		return value, nil
	default:
		return 0, invalidArgument("range_days must be 7, 30, or 90")
	}
}

func normalizeTimezoneOffset(value int) int {
	if value == 0 {
		return defaultTimezoneOffsetMins
	}
	return value
}

func timezoneLocation(offsetMins int) (*time.Location, error) {
	offsetMins = normalizeTimezoneOffset(offsetMins)
	if offsetMins < -12*60 || offsetMins > 14*60 {
		return nil, invalidArgument("timezone_offset_minutes must be between -720 and 840")
	}
	return time.FixedZone("player", offsetMins*60), nil
}

func isSessionDomain(domain Domain) bool {
	return domain == DomainAttention ||
		domain == DomainProcessingSpeed ||
		domain == DomainExecutiveFunction
}

func isTrendDomain(domain Domain) bool {
	return domain == DomainOverall || isSessionDomain(domain)
}

func invalidArgument(message string) error {
	return &errs.Error{Code: errs.InvalidArgument, Message: message}
}
