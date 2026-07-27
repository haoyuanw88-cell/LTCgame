package analytics

import (
	"context"
	"errors"
	"sync"
	"time"
)

var errSessionConflict = errors.New("session_id already exists with different data")

type gameSession struct {
	SessionID          string
	PlayerID           string
	GameID             string
	Domain             Domain
	Difficulty         int
	CorrectCount       int
	WrongCount         int
	OmittedCount       int
	AverageReactionMS  int
	DurationSeconds    int
	PlayedAt           time.Time
	TimezoneOffsetMins int
	Score              int
	Accuracy           float64
	Speed              float64
	Completion         float64
}

// sessionRepository isolates the API from the eventual database schema.
// A PostgreSQL implementation can replace memorySessionRepository later.
type sessionRepository interface {
	Save(ctx context.Context, session gameSession) (stored gameSession, created bool, err error)
	List(ctx context.Context, playerID string, from time.Time) ([]gameSession, error)
}

type memorySessionRepository struct {
	mu       sync.RWMutex
	sessions map[string]gameSession
}

func newMemorySessionRepository() *memorySessionRepository {
	return &memorySessionRepository{sessions: make(map[string]gameSession)}
}

func (r *memorySessionRepository) Save(_ context.Context, session gameSession) (gameSession, bool, error) {
	r.mu.Lock()
	defer r.mu.Unlock()

	if existing, ok := r.sessions[session.SessionID]; ok {
		if existing != session {
			return existing, false, errSessionConflict
		}
		return existing, false, nil
	}
	r.sessions[session.SessionID] = session
	return session, true, nil
}

func (r *memorySessionRepository) List(_ context.Context, playerID string, from time.Time) ([]gameSession, error) {
	r.mu.RLock()
	defer r.mu.RUnlock()

	result := make([]gameSession, 0)
	for _, session := range r.sessions {
		if session.PlayerID == playerID && !session.PlayedAt.Before(from) {
			result = append(result, session)
		}
	}
	return result, nil
}
