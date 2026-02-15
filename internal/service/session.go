package service

import (
	"errors"
	"fmt"

	"gorm.io/gorm"

	"github.com/sumner-mccarty/go-gameserver/internal/model"
)

// SessionService handles game session business logic.
type SessionService struct {
	db *gorm.DB
}

// NewSessionService creates a new SessionService.
func NewSessionService(db *gorm.DB) *SessionService {
	return &SessionService{db: db}
}

// Create creates a new game session.
func (s *SessionService) Create(sessionID string, maxPlayers int) (*model.GameSession, error) {
	if sessionID == "" {
		return nil, fmt.Errorf("session_id is required")
	}
	if maxPlayers <= 0 {
		maxPlayers = 4
	}

	session := &model.GameSession{
		SessionID:  sessionID,
		Status:     "waiting",
		MaxPlayers: maxPlayers,
	}

	if err := s.db.Create(session).Error; err != nil {
		return nil, fmt.Errorf("failed to create session: %w", err)
	}
	return session, nil
}

// GetBySessionID retrieves a game session by its session ID.
func (s *SessionService) GetBySessionID(sessionID string) (*model.GameSession, error) {
	var session model.GameSession
	if err := s.db.Preload("Players").Where("session_id = ?", sessionID).First(&session).Error; err != nil {
		if errors.Is(err, gorm.ErrRecordNotFound) {
			return nil, nil
		}
		return nil, fmt.Errorf("failed to get session: %w", err)
	}
	return &session, nil
}

// List returns all game sessions.
func (s *SessionService) List() ([]model.GameSession, error) {
	var sessions []model.GameSession
	if err := s.db.Preload("Players").Find(&sessions).Error; err != nil {
		return nil, fmt.Errorf("failed to list sessions: %w", err)
	}
	return sessions, nil
}

// JoinSession adds a player to a game session.
func (s *SessionService) JoinSession(sessionID string, playerID uint) (*model.GameSession, error) {
	var session model.GameSession
	if err := s.db.Preload("Players").Where("session_id = ?", sessionID).First(&session).Error; err != nil {
		if errors.Is(err, gorm.ErrRecordNotFound) {
			return nil, fmt.Errorf("session not found")
		}
		return nil, fmt.Errorf("failed to get session: %w", err)
	}

	if session.Status != "waiting" {
		return nil, fmt.Errorf("session is not accepting players (status: %s)", session.Status)
	}

	if len(session.Players) >= session.MaxPlayers {
		return nil, fmt.Errorf("session is full")
	}

	var player model.Player
	if err := s.db.First(&player, playerID).Error; err != nil {
		if errors.Is(err, gorm.ErrRecordNotFound) {
			return nil, fmt.Errorf("player not found")
		}
		return nil, fmt.Errorf("failed to get player: %w", err)
	}

	// Check if player is already in session
	for _, p := range session.Players {
		if p.ID == playerID {
			return nil, fmt.Errorf("player already in session")
		}
	}

	if err := s.db.Model(&session).Association("Players").Append(&player); err != nil {
		return nil, fmt.Errorf("failed to join session: %w", err)
	}

	// Increment player's session count
	s.db.Model(&player).Update("num_sessions", gorm.Expr("num_sessions + 1"))

	// Reload session with players
	if err := s.db.Preload("Players").First(&session, session.ID).Error; err != nil {
		return nil, fmt.Errorf("failed to reload session: %w", err)
	}
	return &session, nil
}

// UpdateStatus changes the status of a game session.
func (s *SessionService) UpdateStatus(sessionID, status string) (*model.GameSession, error) {
	validStatuses := map[string]bool{"waiting": true, "active": true, "finished": true}
	if !validStatuses[status] {
		return nil, fmt.Errorf("invalid status: %s (must be waiting, active, or finished)", status)
	}

	var session model.GameSession
	if err := s.db.Where("session_id = ?", sessionID).First(&session).Error; err != nil {
		if errors.Is(err, gorm.ErrRecordNotFound) {
			return nil, fmt.Errorf("session not found")
		}
		return nil, fmt.Errorf("failed to get session: %w", err)
	}

	if err := s.db.Model(&session).Update("status", status).Error; err != nil {
		return nil, fmt.Errorf("failed to update session status: %w", err)
	}

	if err := s.db.Preload("Players").First(&session, session.ID).Error; err != nil {
		return nil, fmt.Errorf("failed to reload session: %w", err)
	}
	return &session, nil
}

// Delete soft-deletes a game session by session ID.
func (s *SessionService) Delete(sessionID string) error {
	result := s.db.Where("session_id = ?", sessionID).Delete(&model.GameSession{})
	if result.Error != nil {
		return fmt.Errorf("failed to delete session: %w", result.Error)
	}
	if result.RowsAffected == 0 {
		return fmt.Errorf("session not found")
	}
	return nil
}
