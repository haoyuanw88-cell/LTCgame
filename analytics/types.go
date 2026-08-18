package analytics

import "time"

type GuestSignInRequest struct {
	InstallationUID string `json:"installationUid"`
	DisplayName     string `json:"displayName"`
}

// GoogleSignInRequest contains the Google ID token produced by the Unity
// desktop login flow. The installation id is used only to convert an existing
// guest player into the Google player on first sign-in, preserving game data.
type GoogleSignInRequest struct {
	IDToken         string `json:"idToken"`
	Nonce           string `json:"nonce"`
	InstallationUID string `json:"installationUid"`
	DisplayName     string `json:"displayName"`
}

type PlayerSessionResponse struct {
	PlayerID     string `json:"playerId"`
	PlayerCode   string `json:"playerCode"`
	DisplayName  string `json:"displayName"`
	AccessToken  string `json:"accessToken"`
	ExpiresAtUTC string `json:"expiresAtUtc"`
	IsNewPlayer  bool   `json:"isNewPlayer"`
}

type UpdateProfileParams struct {
	DisplayName    string `json:"displayName"`
	BirthDate      string `json:"birthDate"`
	SexCode        string `json:"sexCode"`
	EducationYears int    `json:"educationYears"`
}

type PlayerProfile struct {
	PlayerID       string `json:"playerId"`
	PlayerCode     string `json:"playerCode"`
	DisplayName    string `json:"displayName"`
	BirthDate      string `json:"birthDate"`
	SexCode        string `json:"sexCode"`
	EducationYears int    `json:"educationYears"`
}

type TrialRequest struct {
	TrialIndex       int    `json:"trialIndex"`
	TrialType        string `json:"trialType"`
	ExpectedResponse string `json:"expectedResponse"`
	ActualResponse   string `json:"actualResponse"`
	ReactionTimeMS   int    `json:"reactionTimeMs"`
}

type MetricRequest struct {
	MetricCode  string  `json:"metricCode"`
	Value       float64 `json:"value"`
	DomainCode  string  `json:"domainCode"`
	QualityFlag string  `json:"qualityFlag"`
}

type AssessmentRequest struct {
	SessionID        string          `json:"sessionId"`
	SessionToken     string          `json:"sessionToken"`
	GameCode         string          `json:"gameCode"`
	StartedAtUTC     time.Time       `json:"startedAtUtc"`
	EndedAtUTC       time.Time       `json:"endedAtUtc"`
	CompletionStatus string          `json:"completionStatus"`
	Trials           []TrialRequest  `json:"trials"`
	Metrics          []MetricRequest `json:"metrics"`
}

type StartAssessmentRequest struct {
	GameCode string `json:"gameCode"`
}

type StartAssessmentResponse struct {
	SessionID    string `json:"sessionId"`
	SessionToken string `json:"sessionToken"`
	ExpiresAtUTC string `json:"expiresAtUtc"`
}

type AssessmentResponse struct {
	SessionID   string `json:"sessionId"`
	Stored      bool   `json:"stored"`
	Created     bool   `json:"created"`
	TrialCount  int    `json:"trialCount"`
	MetricCount int    `json:"metricCount"`
}

type HeartbeatResponse struct {
	PlayerID  string `json:"playerId"`
	SeenAtUTC string `json:"seenAtUtc"`
}

type DomainAverage struct {
	Domain       string  `json:"domain"`
	Label        string  `json:"label"`
	AverageScore float64 `json:"averageScore"`
	RecordCount  int     `json:"recordCount"`
	Trend        string  `json:"trend"`
	Status       string  `json:"status"`
	HasInvalid   bool    `json:"hasInvalid"`
}

type DashboardTrendPoint struct {
	Date     string `json:"date"`
	Sessions int    `json:"sessions"`
	Score    int    `json:"score"`
}

type DashboardTask struct {
	ID       string `json:"id"`
	Title    string `json:"title"`
	Owner    string `json:"owner"`
	Priority string `json:"priority"`
	Due      string `json:"due"`
	Status   string `json:"status"`
}

type DashboardGameStatus struct {
	ID             string `json:"id"`
	Name           string `json:"name"`
	Domain         string `json:"domain"`
	Sessions       int    `json:"sessions"`
	CompletionRate int    `json:"completionRate"`
	Status         string `json:"status"`
	NextAction     string `json:"nextAction"`
}

type DashboardRecentSession struct {
	PlayerID string `json:"playerId"`
	Game     string `json:"game"`
	Score    int    `json:"score"`
	Duration string `json:"duration"`
	PlayedAt string `json:"playedAt"`
	Status   string `json:"status"`
}

type DashboardOverview struct {
	OnlineUsers       int                      `json:"onlineUsers"`
	TotalPlayers      int                      `json:"totalPlayers"`
	CompletedSessions int                      `json:"completedSessions"`
	TodaySessions     int                      `json:"todaySessions"`
	ActiveAlerts      int                      `json:"activeAlerts"`
	AverageScore      int                      `json:"averageScore"`
	CompletionRate    float64                  `json:"completionRate"`
	GeneratedAtUTC    string                   `json:"generatedAtUtc"`
	CognitiveAverages []DomainAverage          `json:"cognitiveAverages"`
	WeeklyTrend       []DashboardTrendPoint    `json:"weeklyTrend"`
	Tasks             []DashboardTask          `json:"tasks"`
	Games             []DashboardGameStatus    `json:"games"`
	RecentSessions    []DashboardRecentSession `json:"recentSessions"`
}
