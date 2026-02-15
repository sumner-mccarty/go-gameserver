package chess

import (
	"encoding/json"
	"fmt"
	"strings"
	"sync"
)

// Game represents a single chess game instance.
type Game struct {
	ID          int      `json:"id"`
	SessionID   int      `json:"session_id"`
	WhiteID     int      `json:"white_id"`
	BlackID     int      `json:"black_id"`
	Board       Board    `json:"-"`
	BoardJSON   string   `json:"board"`
	Turn        string   `json:"turn"`   // "white" or "black"
	Status      string   `json:"status"` // "playing", "check", "checkmate", "stalemate", "draw", "resigned"
	MoveCount   int      `json:"move_count"`
	MoveHistory []string `json:"move_history"`
}

// Service manages chess games in memory.
type Service struct {
	mu     sync.RWMutex
	games  map[int]*Game
	nextID int
}

// NewService creates a new chess service.
func NewService() *Service {
	return &Service{
		games:  make(map[int]*Game),
		nextID: 1,
	}
}

// CreateGame creates a new chess game with the standard starting position.
func (s *Service) CreateGame(sessionID, whiteID, blackID int) (*Game, error) {
	if whiteID == 0 || blackID == 0 {
		return nil, fmt.Errorf("both white_id and black_id are required")
	}
	if whiteID == blackID {
		return nil, fmt.Errorf("white and black must be different players")
	}

	board := NewBoard()
	game := &Game{
		SessionID:   sessionID,
		WhiteID:     whiteID,
		BlackID:     blackID,
		Board:       board,
		BoardJSON:   board.ToJSON(),
		Turn:        "white",
		Status:      "playing",
		MoveCount:   0,
		MoveHistory: []string{},
	}

	s.mu.Lock()
	game.ID = s.nextID
	s.nextID++
	s.games[game.ID] = game
	s.mu.Unlock()

	return game, nil
}

// GetGame returns a chess game by ID.
func (s *Service) GetGame(id int) (*Game, error) {
	s.mu.RLock()
	defer s.mu.RUnlock()

	game, ok := s.games[id]
	if !ok {
		return nil, nil
	}
	return game, nil
}

// MakeMove executes a chess move.
func (s *Service) MakeMove(gameID, playerID int, fromStr, toStr string) (*Game, error) {
	s.mu.Lock()
	defer s.mu.Unlock()

	game, ok := s.games[gameID]
	if !ok {
		return nil, fmt.Errorf("game not found")
	}

	if game.Status != "playing" && game.Status != "check" {
		return nil, fmt.Errorf("game is not in progress (status: %s)", game.Status)
	}

	// Verify it's this player's turn
	expectedPlayer := game.WhiteID
	if game.Turn == "black" {
		expectedPlayer = game.BlackID
	}
	if playerID != expectedPlayer {
		return nil, fmt.Errorf("not your turn")
	}

	from, err := ParseSquare(fromStr)
	if err != nil {
		return nil, fmt.Errorf("invalid from square: %w", err)
	}
	to, err := ParseSquare(toStr)
	if err != nil {
		return nil, fmt.Errorf("invalid to square: %w", err)
	}

	// Validate the move
	if err := ValidateMoveForColor(&game.Board, from, to, game.Turn); err != nil {
		return nil, fmt.Errorf("illegal move: %w", err)
	}

	// Execute the move
	piece := game.Board.Get(from)
	game.Board.Set(to, piece)
	game.Board.Set(from, Empty)

	// Handle pawn promotion (auto-promote to queen)
	upper := strings.ToUpper(piece)
	if upper == "P" {
		if (IsWhite(piece) && to.Row == 0) || (IsBlack(piece) && to.Row == 7) {
			if IsWhite(piece) {
				game.Board.Set(to, WhiteQueen)
			} else {
				game.Board.Set(to, BlackQueen)
			}
		}
	}

	// Record the move
	moveNotation := fmt.Sprintf("%s-%s", fromStr, toStr)
	game.MoveHistory = append(game.MoveHistory, moveNotation)
	game.MoveCount++

	// Switch turns
	if game.Turn == "white" {
		game.Turn = "black"
	} else {
		game.Turn = "white"
	}

	// Check game status
	game.Status = GameStatus(&game.Board, game.Turn)
	game.BoardJSON = game.Board.ToJSON()

	return game, nil
}

// Resign allows a player to resign the game.
func (s *Service) Resign(gameID, playerID int) (*Game, error) {
	s.mu.Lock()
	defer s.mu.Unlock()

	game, ok := s.games[gameID]
	if !ok {
		return nil, fmt.Errorf("game not found")
	}

	if game.Status != "playing" && game.Status != "check" {
		return nil, fmt.Errorf("game is already over (status: %s)", game.Status)
	}

	if playerID != game.WhiteID && playerID != game.BlackID {
		return nil, fmt.Errorf("player is not in this game")
	}

	game.Status = "resigned"
	return game, nil
}

// GameResponse returns a JSON-friendly representation with move_history as a JSON string.
type GameResponse struct {
	ID              int    `json:"id"`
	SessionID       int    `json:"session_id"`
	WhiteID         int    `json:"white_id"`
	BlackID         int    `json:"black_id"`
	Board           string `json:"board"`
	Turn            string `json:"turn"`
	Status          string `json:"status"`
	MoveCount       int    `json:"move_count"`
	MoveHistoryJSON string `json:"move_history"`
}

// ToResponse converts a Game to a GameResponse (for JSON serialization).
func (g *Game) ToResponse() GameResponse {
	historyJSON, _ := json.Marshal(g.MoveHistory)
	return GameResponse{
		ID:              g.ID,
		SessionID:       g.SessionID,
		WhiteID:         g.WhiteID,
		BlackID:         g.BlackID,
		Board:           g.BoardJSON,
		Turn:            g.Turn,
		Status:          g.Status,
		MoveCount:       g.MoveCount,
		MoveHistoryJSON: string(historyJSON),
	}
}
