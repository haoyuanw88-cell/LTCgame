package analytics

import "time"

// Domain identifies the cognitive ability measured by a game.
type Domain string

const (
	DomainOverall           Domain = "overall"
	DomainAttention         Domain = "attention"
	DomainProcessingSpeed   Domain = "processing_speed"
	DomainExecutiveFunction Domain = "executive_function"
)

// SubmitSessionParams contains raw measurements captured by Unity.
// The API computes the 0-100 score so every client uses the same rules.
type SubmitSessionParams struct {
	SessionID          string    `json:"session_id"`
	PlayerID           string    `json:"player_id"`
	GameID             string    `json:"game_id"`
	Domain             Domain    `json:"domain"`
	Difficulty         int       `json:"difficulty"`
	CorrectCount       int       `json:"correct_count"`
	WrongCount         int       `json:"wrong_count"`
	OmittedCount       int       `json:"omitted_count"`
	AverageReactionMS  int       `json:"average_reaction_ms"`
	DurationSeconds    int       `json:"duration_seconds"`
	PlayedAt           time.Time `json:"played_at"`
	TimezoneOffsetMins int       `json:"timezone_offset_minutes"`
}

// SessionResult is returned after Encore validates and scores a session.
type SessionResult struct {
	SessionID  string  `json:"session_id"`
	PlayerID   string  `json:"player_id"`
	GameID     string  `json:"game_id"`
	Domain     Domain  `json:"domain"`
	Score      int     `json:"score"`
	Accuracy   float64 `json:"accuracy"`
	Speed      float64 `json:"speed"`
	Completion float64 `json:"completion"`
	Created    bool    `json:"created"`
}

type GetCognitiveSummaryParams struct {
	RangeDays          int `query:"range_days"`
	TimezoneOffsetMins int `query:"timezone_offset_minutes"`
}

type CognitiveSummary struct {
	PlayerID               string `json:"player_id"`
	RangeDays              int    `json:"range_days"`
	EffectiveDays          int    `json:"effective_days"`
	RecordCount            int    `json:"record_count"`
	AttentionScore         int    `json:"attention_score"`
	ProcessingSpeedScore   int    `json:"processing_speed_score"`
	ExecutiveFunctionScore int    `json:"executive_function_score"`
	OverallScore           int    `json:"overall_score"`
}

type GetCognitiveTrendsParams struct {
	RangeDays          int    `query:"range_days"`
	Domain             string `query:"domain"`
	TimezoneOffsetMins int    `query:"timezone_offset_minutes"`
}

type TrendPoint struct {
	Date         string `json:"date"`
	Score        int    `json:"score"`
	SessionCount int    `json:"session_count"`
}

type CognitiveTrends struct {
	PlayerID      string            `json:"player_id"`
	RangeDays     int               `json:"range_days"`
	Domain        Domain            `json:"domain"`
	EffectiveDays int               `json:"effective_days"`
	Summary       *CognitiveSummary `json:"summary"`
	Points        []*TrendPoint     `json:"points"`
}
