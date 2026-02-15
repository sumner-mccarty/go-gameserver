package model

import (
	"time"

	"gorm.io/gorm"
)

// Player represents a game player.
type Player struct {
	ID        uint           `json:"id" gorm:"primarykey"`
	CreatedAt time.Time      `json:"created_at"`
	UpdatedAt time.Time      `json:"updated_at"`
	DeletedAt gorm.DeletedAt `json:"-" gorm:"index"`

	UID         string `json:"uid" gorm:"uniqueIndex;size:64;not null"`
	Name        string `json:"name" gorm:"size:128;not null"`
	NumSessions uint   `json:"num_sessions" gorm:"default:0"`
}

// GameSession represents an active or completed game session.
type GameSession struct {
	ID        uint           `json:"id" gorm:"primarykey"`
	CreatedAt time.Time      `json:"created_at"`
	UpdatedAt time.Time      `json:"updated_at"`
	DeletedAt gorm.DeletedAt `json:"-" gorm:"index"`

	SessionID string `json:"session_id" gorm:"uniqueIndex;size:64;not null"`
	Status    string `json:"status" gorm:"size:32;default:'waiting'"` // waiting, active, finished
	MaxPlayers int   `json:"max_players" gorm:"default:4"`

	Players []Player `json:"players,omitempty" gorm:"many2many:session_players;"`
}
