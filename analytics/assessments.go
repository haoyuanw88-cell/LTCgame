package analytics

import (
	"context"
	"math"
	"strings"
	"time"
)

const (
	maxTrialsPerAssessment  = 1000
	maxMetricsPerAssessment = 100
)

// StartAssessment reserves a short, server-issued session identifier. The
// signed token binds it to one player and game without another database field.
//
//encore:api auth method=POST path=/api/v1/assessments/start
func StartAssessment(ctx context.Context, p *StartAssessmentRequest) (*StartAssessmentResponse, error) {
	playerID, err := currentPlayerID()
	if err != nil {
		return nil, err
	}
	gameCode := ""
	if p != nil {
		gameCode = compactGameCode(p.GameCode)
	}
	if gameCode == "" {
		return nil, invalidArgument("gameCode is invalid")
	}

	var sessionID string
	if err = db.QueryRow(ctx, `
		SELECT 'S' || LPAD(nextval('session_code_seq')::text, 8, '0')
	`).Scan(&sessionID); err != nil {
		return nil, err
	}
	sessionID = strings.TrimSpace(sessionID)
	expiresAt := time.Now().UTC().Add(4 * time.Hour)
	return &StartAssessmentResponse{
		SessionID:    sessionID,
		SessionToken: issueAssessmentToken(playerID, sessionID, gameCode, expiresAt),
		ExpiresAtUTC: expiresAt.Format(time.RFC3339),
	}, nil
}

// SubmitAssessment stores one completed Unity assessment. The short session ID
// and its signed token make retries idempotent without persisting a long GUID.
//
//encore:api auth method=POST path=/api/v1/assessments
func SubmitAssessment(ctx context.Context, p *AssessmentRequest) (*AssessmentResponse, error) {
	playerID, err := currentPlayerID()
	if err != nil {
		return nil, err
	}
	if err = validateAssessment(p); err != nil {
		return nil, err
	}

	sessionID := strings.TrimSpace(p.SessionID)
	gameCode := compactGameCode(p.GameCode)
	if err = verifyAssessmentToken(strings.TrimSpace(p.SessionToken), playerID, sessionID, gameCode); err != nil {
		return nil, invalidArgument("sessionToken is invalid, expired, or does not match this assessment")
	}
	if !strings.EqualFold(strings.TrimSpace(p.CompletionStatus), "completed") {
		return &AssessmentResponse{SessionID: sessionID, Stored: false}, nil
	}

	durationMS := p.EndedAtUTC.Sub(p.StartedAtUTC).Milliseconds()
	doneDate := p.EndedAtUTC.UTC().Format("2006-01-02")

	tx, err := db.Begin(ctx)
	if err != nil {
		return nil, err
	}
	defer tx.Rollback()

	result, err := tx.Exec(ctx, `
		INSERT INTO game_session (s_id, p_id, game_cd, done_dt, dur_ms)
		VALUES ($1, $2, $3, $4, $5)
		ON CONFLICT (s_id) DO NOTHING
	`, sessionID, playerID, gameCode, doneDate, durationMS)
	if err != nil {
		return nil, err
	}
	created := result.RowsAffected() == 1
	if !created {
		var ownerID string
		if err = tx.QueryRow(ctx, `SELECT p_id FROM game_session WHERE s_id = $1`, sessionID).Scan(&ownerID); err != nil {
			return nil, err
		}
		if strings.TrimSpace(ownerID) != playerID {
			return nil, invalidArgument("sessionId belongs to another player")
		}
		var trialCount, metricCount int
		if err = tx.QueryRow(ctx, `SELECT COUNT(*) FROM trial_test WHERE s_id = $1`, sessionID).Scan(&trialCount); err != nil {
			return nil, err
		}
		if err = tx.QueryRow(ctx, `SELECT COUNT(*) FROM metric WHERE s_id = $1`, sessionID).Scan(&metricCount); err != nil {
			return nil, err
		}
		if err = tx.Commit(); err != nil {
			return nil, err
		}
		return &AssessmentResponse{
			SessionID: sessionID, Stored: true, Created: false,
			TrialCount: trialCount, MetricCount: metricCount,
		}, nil
	}

	seenQuestionNumbers := make(map[int]struct{}, len(p.Trials))
	trialCount := 0
	for index, trial := range p.Trials {
		questionNumber := trial.TrialIndex
		if questionNumber <= 0 {
			questionNumber = index + 1
		}
		for {
			if _, exists := seenQuestionNumbers[questionNumber]; !exists {
				break
			}
			questionNumber++
		}
		seenQuestionNumbers[questionNumber] = struct{}{}
		conditionCode := compactConditionCode(trial.TrialType)
		if conditionCode == "" {
			conditionCode = "UNK"
		}
		if _, err = tx.Exec(ctx, `
			INSERT INTO trial_test (s_id, q_no, cond_cd, target, answer, rt_ms)
			VALUES ($1, $2, $3, NULLIF($4, ''), NULLIF($5, ''), $6)
		`, sessionID, questionNumber, conditionCode,
			cleanText(trial.ExpectedResponse, 500), cleanText(trial.ActualResponse, 500),
			maxInt(0, trial.ReactionTimeMS)); err != nil {
			return nil, err
		}
		trialCount++
	}

	metricCount := 0
	for _, metric := range p.Metrics {
		metricCode := compactMetricCode(metric.MetricCode)
		domainCode := compactDomainCode(metric.DomainCode)
		if metricCode == "" || domainCode == "" || math.IsNaN(metric.Value) || math.IsInf(metric.Value, 0) {
			continue
		}
		valid := strings.EqualFold(strings.TrimSpace(metric.QualityFlag), "valid")
		if _, err = tx.Exec(ctx, `
			INSERT INTO metric (s_id, dmn_cd, metric_cd, m_val, valid_yn)
			VALUES ($1, $2, $3, $4, $5)
			ON CONFLICT (s_id, dmn_cd, metric_cd) DO UPDATE SET
				m_val = EXCLUDED.m_val,
				valid_yn = EXCLUDED.valid_yn
		`, sessionID, domainCode, metricCode, metric.Value, valid); err != nil {
			return nil, err
		}
		metricCount++
	}
	if _, err = tx.Exec(ctx, `
		UPDATE player SET last_seen_ts = date_trunc('minute', NOW()) WHERE p_id = $1
	`, playerID); err != nil {
		return nil, err
	}
	if err = tx.Commit(); err != nil {
		return nil, err
	}

	return &AssessmentResponse{
		SessionID: sessionID, Stored: true, Created: true,
		TrialCount: trialCount, MetricCount: metricCount,
	}, nil
}

func validateAssessment(p *AssessmentRequest) error {
	if p == nil {
		return invalidArgument("request body is required")
	}
	if !validSessionID(strings.TrimSpace(p.SessionID)) {
		return invalidArgument("sessionId must use the S00000000 format")
	}
	if strings.TrimSpace(p.SessionToken) == "" {
		return invalidArgument("sessionToken is required")
	}
	if compactGameCode(p.GameCode) == "" {
		return invalidArgument("gameCode is invalid")
	}
	if p.StartedAtUTC.IsZero() || p.EndedAtUTC.IsZero() || !p.EndedAtUTC.After(p.StartedAtUTC) {
		return invalidArgument("startedAtUtc and endedAtUtc must form a positive duration")
	}
	if len(p.Trials) > maxTrialsPerAssessment {
		return invalidArgument("too many trials")
	}
	if len(p.Metrics) > maxMetricsPerAssessment {
		return invalidArgument("too many metrics")
	}
	return nil
}

func validSessionID(value string) bool {
	if len(value) != 9 || value[0] != 'S' {
		return false
	}
	for _, ch := range value[1:] {
		if ch < '0' || ch > '9' {
			return false
		}
	}
	return true
}

func maxInt(a, b int) int {
	if a > b {
		return a
	}
	return b
}
