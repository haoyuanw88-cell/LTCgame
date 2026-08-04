package analytics

import "time"

type GuestSignInRequest struct {
	InstallationUID string `json:"installationUid"`
	DisplayName     string `json:"displayName"`
}

type PlayerSessionResponse struct {
	PlayerID     int64  `json:"playerId"`
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
	PlayerID       int64  `json:"playerId"`
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
	GameCode         string          `json:"gameCode"`
	StartedAtUTC     time.Time       `json:"startedAtUtc"`
	EndedAtUTC       time.Time       `json:"endedAtUtc"`
	CompletionStatus string          `json:"completionStatus"`
	Trials           []TrialRequest  `json:"trials"`
	Metrics          []MetricRequest `json:"metrics"`
}

type AssessmentResponse struct {
	SessionID   string `json:"sessionId"`
	Stored      bool   `json:"stored"`
	Created     bool   `json:"created"`
	TrialCount  int    `json:"trialCount"`
	MetricCount int    `json:"metricCount"`
}

type HeartbeatResponse struct {
	PlayerID  int64  `json:"playerId"`
	SeenAtUTC string `json:"seenAtUtc"`
}

type DomainAverage struct {
	Domain       string  `json:"domain"`
	AverageScore float64 `json:"averageScore"`
	RecordCount  int     `json:"recordCount"`
}

type DashboardOverview struct {
	OnlineUsers       int             `json:"onlineUsers"`
	TotalPlayers      int             `json:"totalPlayers"`
	CompletedSessions int             `json:"completedSessions"`
	GeneratedAtUTC    string          `json:"generatedAtUtc"`
	CognitiveAverages []DomainAverage `json:"cognitiveAverages"`
}
