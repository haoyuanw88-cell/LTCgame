package analytics

import (
	"context"
	"math"
	"strings"
)

const (
	maxTrialsPerAssessment  = 1000
	maxMetricsPerAssessment = 100
)

// SubmitAssessment stores one completed Unity assessment. The client-generated
// session id is the idempotency key, so offline retries do not duplicate data.
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
	if !strings.EqualFold(strings.TrimSpace(p.CompletionStatus), "completed") {
		return &AssessmentResponse{SessionID: strings.TrimSpace(p.SessionID), Stored: false}, nil
	}

	sessionID := strings.TrimSpace(p.SessionID)
	gameName := cleanText(p.GameCode, 64)
	durationMS := p.EndedAtUTC.Sub(p.StartedAtUTC).Milliseconds()
	doneDate := p.EndedAtUTC.UTC().Format("2006-01-02")

	tx, err := db.Begin(ctx)
	if err != nil {
		return nil, err
	}
	defer tx.Rollback()

	result, err := tx.Exec(ctx, `
		INSERT INTO game_session (s_id, p_id, game_name, done_dt, dur_ms)
		VALUES ($1, $2, $3, $4, $5)
		ON CONFLICT (s_id) DO NOTHING
	`, sessionID, playerID, gameName, doneDate, durationMS)
	if err != nil {
		return nil, err
	}
	created := result.RowsAffected() == 1
	if !created {
		var ownerID int64
		if err = tx.QueryRow(ctx, `SELECT p_id FROM game_session WHERE s_id = $1`, sessionID).Scan(&ownerID); err != nil {
			return nil, err
		}
		if ownerID != playerID {
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
		if _, err = tx.Exec(ctx, `
			INSERT INTO trial_test (s_id, q_no, cond_cd, target, answer, rt_ms)
			VALUES ($1, $2, NULLIF($3, ''), NULLIF($4, ''), NULLIF($5, ''), $6)
		`, sessionID, questionNumber, cleanText(trial.TrialType, 64),
			cleanText(trial.ExpectedResponse, 500), cleanText(trial.ActualResponse, 500),
			maxInt(0, trial.ReactionTimeMS)); err != nil {
			return nil, err
		}
		trialCount++
	}

	metricCount := 0
	for _, metric := range p.Metrics {
		metricName := cleanText(metric.MetricCode, 64)
		domainName := cleanText(metric.DomainCode, 64)
		if metricName == "" || domainName == "" || math.IsNaN(metric.Value) || math.IsInf(metric.Value, 0) {
			continue
		}
		valid := strings.EqualFold(strings.TrimSpace(metric.QualityFlag), "valid")
		if _, err = tx.Exec(ctx, `
			INSERT INTO metric (s_id, dmn_name, metric_name, m_val, valid_yn)
			VALUES ($1, $2, $3, $4, $5)
			ON CONFLICT (s_id, dmn_name, metric_name) DO UPDATE SET
				m_val = EXCLUDED.m_val,
				valid_yn = EXCLUDED.valid_yn
		`, sessionID, domainName, metricName, metric.Value, valid); err != nil {
			return nil, err
		}
		metricCount++
	}
	if _, err = tx.Exec(ctx, `UPDATE player SET last_seen_ts = NOW() WHERE p_id = $1`, playerID); err != nil {
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
	if id := strings.TrimSpace(p.SessionID); len(id) < 8 || len(id) > 40 {
		return invalidArgument("sessionId must contain 8 to 40 characters")
	}
	if name := strings.TrimSpace(p.GameCode); name == "" || len([]rune(name)) > 64 {
		return invalidArgument("gameCode is required and must not exceed 64 characters")
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

func maxInt(a, b int) int {
	if a > b {
		return a
	}
	return b
}
