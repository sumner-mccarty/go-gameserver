package service

import (
	"errors"
	"fmt"

	"gorm.io/gorm"

	"github.com/sumner-mccarty/go-gameserver/internal/model"
)

// PlayerService handles player-related business logic.
type PlayerService struct {
	db *gorm.DB
}

// NewPlayerService creates a new PlayerService.
func NewPlayerService(db *gorm.DB) *PlayerService {
	return &PlayerService{db: db}
}

// Create creates a new player.
func (s *PlayerService) Create(uid, name string) (*model.Player, error) {
	if uid == "" {
		return nil, fmt.Errorf("uid is required")
	}
	if name == "" {
		return nil, fmt.Errorf("name is required")
	}

	player := &model.Player{
		UID:  uid,
		Name: name,
	}

	if err := s.db.Create(player).Error; err != nil {
		return nil, fmt.Errorf("failed to create player: %w", err)
	}
	return player, nil
}

// GetByUID retrieves a player by their UID.
func (s *PlayerService) GetByUID(uid string) (*model.Player, error) {
	var player model.Player
	if err := s.db.Where("uid = ?", uid).First(&player).Error; err != nil {
		if errors.Is(err, gorm.ErrRecordNotFound) {
			return nil, nil
		}
		return nil, fmt.Errorf("failed to get player: %w", err)
	}
	return &player, nil
}

// GetByID retrieves a player by their database ID.
func (s *PlayerService) GetByID(id uint) (*model.Player, error) {
	var player model.Player
	if err := s.db.First(&player, id).Error; err != nil {
		if errors.Is(err, gorm.ErrRecordNotFound) {
			return nil, nil
		}
		return nil, fmt.Errorf("failed to get player: %w", err)
	}
	return &player, nil
}

// List returns all players.
func (s *PlayerService) List() ([]model.Player, error) {
	var players []model.Player
	if err := s.db.Find(&players).Error; err != nil {
		return nil, fmt.Errorf("failed to list players: %w", err)
	}
	return players, nil
}

// Update updates a player's name and/or session count.
func (s *PlayerService) Update(id uint, name string, numSessions *uint) (*model.Player, error) {
	var player model.Player
	if err := s.db.First(&player, id).Error; err != nil {
		if errors.Is(err, gorm.ErrRecordNotFound) {
			return nil, nil
		}
		return nil, fmt.Errorf("failed to find player: %w", err)
	}

	updates := map[string]interface{}{}
	if name != "" {
		updates["name"] = name
	}
	if numSessions != nil {
		updates["num_sessions"] = *numSessions
	}

	if len(updates) > 0 {
		if err := s.db.Model(&player).Updates(updates).Error; err != nil {
			return nil, fmt.Errorf("failed to update player: %w", err)
		}
	}

	// Reload to get updated values
	if err := s.db.First(&player, id).Error; err != nil {
		return nil, fmt.Errorf("failed to reload player: %w", err)
	}
	return &player, nil
}

// Delete soft-deletes a player by ID.
func (s *PlayerService) Delete(id uint) error {
	result := s.db.Delete(&model.Player{}, id)
	if result.Error != nil {
		return fmt.Errorf("failed to delete player: %w", result.Error)
	}
	if result.RowsAffected == 0 {
		return fmt.Errorf("player not found")
	}
	return nil
}
