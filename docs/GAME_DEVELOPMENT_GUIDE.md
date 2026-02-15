# Game Development Guide

How to use `go-gameserver` as a foundation to build authoritative game servers for any type of game — from turn-based chess to real-time strategy games like Mechabellum — with Unity (or any engine) as a thin visual client.

---

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [How This Repo Is Structured](#how-this-repo-is-structured)
3. [Extension Points](#extension-points)
4. [Example 1: Chess Game Server](#example-1-chess-game-server)
5. [Example 2: Mechabellum-Style RTS Server](#example-2-mechabellum-style-rts-server)
6. [Connecting a Unity Client](#connecting-a-unity-client)
7. [Going to Production](#going-to-production)

---

## Architecture Overview

### Why Server-Authoritative?

In a server-authoritative architecture, **all gameplay logic runs on the server**. The client (Unity, Unreal, etc.) is a "dumb terminal" — it sends player inputs to the server and renders the state it receives back. This approach:

- **Prevents cheating** — the client never decides what happens, only the server does
- **Ensures consistency** — all players see the same game state
- **Enables replays** — every action is recorded server-side
- **Simplifies clients** — Unity only handles rendering, input, and audio

```
┌─────────────┐         HTTP / WebSocket         ┌─────────────────┐
│ Unity Client├────────────────────────────────►  │  Go Game Server │
│  (Renderer) │  ◄────────────────────────────────┤  (Game Logic)   │
│             │        Game State Updates         │                 │
│  • Input    │                                   │  • Rules        │
│  • Graphics │                                   │  • Physics      │
│  • Audio    │                                   │  • State        │
│  • UI       │                                   │  • Persistence  │
└─────────────┘                                   └────────┬────────┘
                                                           │
                                                  ┌────────▼────────┐
                                                  │    Database     │
                                                  │  (SQLite / PG)  │
                                                  └─────────────────┘
```

### Communication Patterns

| Pattern | Best For | Protocol |
|---------|----------|----------|
| **Request/Response** | Turn-based games (chess, card games) | HTTP REST |
| **Real-time Streaming** | Action/RTS games (Mechabellum, FPS) | WebSocket |
| **Hybrid** | Games with lobbies + real-time gameplay | HTTP for lobby, WebSocket for gameplay |

This repo provides the HTTP REST foundation. The examples below show how to extend it for both patterns.

---

## How This Repo Is Structured

The existing codebase gives you a production-ready starting point:

```
go-gameserver/
├── cmd/gameserver/main.go        # Server entry point
├── internal/
│   ├── config/config.go          # Environment-based configuration
│   ├── model/model.go            # Data models (Player, GameSession)
│   ├── service/
│   │   ├── player.go             # Player business logic (CRUD)
│   │   └── session.go            # Session business logic (create, join, status)
│   ├── handler/
│   │   ├── handler.go            # HTTP handlers (REST endpoints)
│   │   └── routes.go             # Route registration
│   ├── middleware/middleware.go   # Logging, JSON content-type
│   └── store/store.go            # Database connection (SQLite/PostgreSQL/MySQL)
├── Dockerfile                    # Production container
├── docker-compose.yml            # Local development
└── deploy/aws/main.tf            # Cloud deployment
```

**What you already get out of the box:**

- Player registration and management (`POST /api/players`, `GET /api/players/{id}`)
- Game session creation and lifecycle (`POST /api/sessions`, join, status updates)
- Configurable database (SQLite locally, PostgreSQL in production)
- Docker and cloud deployment configs
- 47 passing tests

**What you add for your specific game:**

- Game-specific models (board state, units, etc.)
- Game logic service (rules, validation, state transitions)
- Game-specific endpoints or WebSocket handlers

---

## Extension Points

When building a game-specific server, you extend three layers:

### 1. Models (`internal/model/`)

Add new structs for your game's data. They integrate with the existing GORM setup and auto-migrate on startup.

```go
// internal/model/model.go — add your game models alongside Player and GameSession

type ChessGame struct {
    ID        uint           `json:"id" gorm:"primarykey"`
    CreatedAt time.Time      `json:"created_at"`
    UpdatedAt time.Time      `json:"updated_at"`
    DeletedAt gorm.DeletedAt `json:"-" gorm:"index"`

    SessionID uint        `json:"session_id"`
    Session   GameSession `json:"-" gorm:"foreignKey:SessionID"`
    Board     string      `json:"board" gorm:"type:text"` // JSON-encoded board state
    Turn      string      `json:"turn" gorm:"size:8"`     // "white" or "black"
    Status    string      `json:"status" gorm:"size:32"`  // "playing", "checkmate", "stalemate", "draw"
    MoveCount int         `json:"move_count"`
}
```

### 2. Services (`internal/service/`)

Add a new service file for your game logic. Services contain all business rules and are independent of HTTP.

```go
// internal/service/chess.go — new file

type ChessService struct {
    db *gorm.DB
}

func NewChessService(db *gorm.DB) *ChessService {
    return &ChessService{db: db}
}

func (s *ChessService) MakeMove(gameID uint, from, to string) (*model.ChessGame, error) {
    // 1. Load game from database
    // 2. Validate move against chess rules
    // 3. Update board state
    // 4. Check for checkmate/stalemate
    // 5. Save and return updated state
}
```

### 3. Handlers and Routes (`internal/handler/`)

Add new endpoints and wire them into the existing router.

```go
// internal/handler/routes.go — add to RegisterRoutes()

// Chess-specific routes
api.HandleFunc("/chess/{session_id}", h.GetChessGame).Methods("GET")
api.HandleFunc("/chess/{session_id}/move", h.MakeChessMove).Methods("POST")
```

### 4. Store Migrations (`internal/store/store.go`)

Add your new models to the auto-migration list:

```go
// internal/store/store.go — add to the AutoMigrate call

if err := db.AutoMigrate(&model.Player{}, &model.GameSession{}, &model.ChessGame{}); err != nil {
    return nil, fmt.Errorf("failed to migrate database: %w", err)
}
```

---

## Example 1: Chess Game Server

Chess is an ideal first example: it's turn-based, has well-defined rules, 2 players, and the entire game state fits in a simple data structure. HTTP REST is the perfect communication pattern.

### Step 1: Define the Chess Model

Add to `internal/model/model.go`:

```go
// ChessGame represents a chess match linked to a GameSession.
type ChessGame struct {
    ID        uint           `json:"id" gorm:"primarykey"`
    CreatedAt time.Time      `json:"created_at"`
    UpdatedAt time.Time      `json:"updated_at"`
    DeletedAt gorm.DeletedAt `json:"-" gorm:"index"`

    SessionID  uint        `json:"session_id"`
    Session    GameSession `json:"-" gorm:"foreignKey:SessionID"`
    WhiteID    uint        `json:"white_id"`    // Player ID for white
    BlackID    uint        `json:"black_id"`    // Player ID for black
    Board      string      `json:"board" gorm:"type:text"`       // JSON-serialized 8x8 board
    Turn       string      `json:"turn" gorm:"size:8;default:'white'"`
    Status     string      `json:"status" gorm:"size:32;default:'playing'"` // playing, check, checkmate, stalemate, draw, resigned
    MoveCount  int         `json:"move_count" gorm:"default:0"`
    MoveHistory string     `json:"move_history" gorm:"type:text"` // JSON array of moves
}
```

### Step 2: Define Board Representation

Create `internal/service/chess.go`:

```go
package service

import (
    "encoding/json"
    "errors"
    "fmt"

    "gorm.io/gorm"

    "github.com/sumner-mccarty/go-gameserver/internal/model"
)

// Piece represents a chess piece.
// Convention: uppercase = white, lowercase = black
// K=King, Q=Queen, R=Rook, B=Bishop, N=Knight, P=Pawn
// Empty square = ""
type Board [8][8]string

// InitialBoard returns the standard chess starting position.
func InitialBoard() Board {
    return Board{
        {"R", "N", "B", "Q", "K", "B", "N", "R"}, // rank 1 (white)
        {"P", "P", "P", "P", "P", "P", "P", "P"}, // rank 2
        {"", "", "", "", "", "", "", ""},             // rank 3
        {"", "", "", "", "", "", "", ""},             // rank 4
        {"", "", "", "", "", "", "", ""},             // rank 5
        {"", "", "", "", "", "", "", ""},             // rank 6
        {"p", "p", "p", "p", "p", "p", "p", "p"}, // rank 7
        {"r", "n", "b", "q", "k", "b", "n", "r"}, // rank 8 (black)
    }
}

// ChessService handles chess game logic.
type ChessService struct {
    db *gorm.DB
}

// NewChessService creates a new ChessService.
func NewChessService(db *gorm.DB) *ChessService {
    return &ChessService{db: db}
}

// CreateGame starts a new chess game for a session with two players.
func (s *ChessService) CreateGame(sessionID, whiteID, blackID uint) (*model.ChessGame, error) {
    board := InitialBoard()
    boardJSON, err := json.Marshal(board)
    if err != nil {
        return nil, fmt.Errorf("failed to serialize board: %w", err)
    }

    movesJSON, _ := json.Marshal([]string{}) // safe: []string{} always marshals successfully

    game := &model.ChessGame{
        SessionID:   sessionID,
        WhiteID:     whiteID,
        BlackID:     blackID,
        Board:       string(boardJSON),
        Turn:        "white",
        Status:      "playing",
        MoveHistory: string(movesJSON),
    }

    if err := s.db.Create(game).Error; err != nil {
        return nil, fmt.Errorf("failed to create chess game: %w", err)
    }
    return game, nil
}

// MakeMove validates and applies a chess move.
// from/to are in algebraic notation, e.g. "e2", "e4"
func (s *ChessService) MakeMove(gameID uint, playerID uint, from, to string) (*model.ChessGame, error) {
    var game model.ChessGame
    if err := s.db.First(&game, gameID).Error; err != nil {
        if errors.Is(err, gorm.ErrRecordNotFound) {
            return nil, fmt.Errorf("game not found")
        }
        return nil, fmt.Errorf("failed to get game: %w", err)
    }

    if game.Status != "playing" && game.Status != "check" {
        return nil, fmt.Errorf("game is over (status: %s)", game.Status)
    }

    // Verify it's this player's turn
    if game.Turn == "white" && playerID != game.WhiteID {
        return nil, fmt.Errorf("it is not your turn")
    }
    if game.Turn == "black" && playerID != game.BlackID {
        return nil, fmt.Errorf("it is not your turn")
    }

    // Parse board
    var board Board
    if err := json.Unmarshal([]byte(game.Board), &board); err != nil {
        return nil, fmt.Errorf("failed to parse board: %w", err)
    }

    // Convert algebraic notation to array indices
    fromRow, fromCol, err := algebraicToIndex(from)
    if err != nil {
        return nil, fmt.Errorf("invalid 'from' square: %w", err)
    }
    toRow, toCol, err := algebraicToIndex(to)
    if err != nil {
        return nil, fmt.Errorf("invalid 'to' square: %w", err)
    }

    // Validate the piece belongs to the current player
    piece := board[fromRow][fromCol]
    if piece == "" {
        return nil, fmt.Errorf("no piece at %s", from)
    }
    if game.Turn == "white" && !isUppercase(piece) {
        return nil, fmt.Errorf("that is not your piece")
    }
    if game.Turn == "black" && isUppercase(piece) {
        return nil, fmt.Errorf("that is not your piece")
    }

    // -------------------------------------------------------
    // GAME RULE VALIDATION GOES HERE
    // In a production implementation, you would validate:
    // - The piece can legally move to the target square
    // - The move doesn't leave your own king in check
    // - Special moves: castling, en passant, pawn promotion
    //
    // For brevity, this example applies the move without
    // full rule validation. See "Implementing Full Chess Rules"
    // below for guidance on adding complete validation.
    // -------------------------------------------------------

    // Apply the move
    board[toRow][toCol] = board[fromRow][fromCol]
    board[fromRow][fromCol] = ""

    // Record the move (MoveHistory is always valid JSON written by the server)
    var moves []string
    _ = json.Unmarshal([]byte(game.MoveHistory), &moves)
    moves = append(moves, fmt.Sprintf("%s-%s", from, to))

    // Switch turns
    nextTurn := "black"
    if game.Turn == "black" {
        nextTurn = "white"
    }

    // Serialize updated state (Board and moves are simple types that always marshal successfully)
    boardJSON, _ := json.Marshal(board)
    movesJSON, _ := json.Marshal(moves)

    // Save to database
    updates := map[string]interface{}{
        "board":        string(boardJSON),
        "turn":         nextTurn,
        "move_count":   game.MoveCount + 1,
        "move_history": string(movesJSON),
    }
    if err := s.db.Model(&game).Updates(updates).Error; err != nil {
        return nil, fmt.Errorf("failed to save move: %w", err)
    }

    // Reload
    s.db.First(&game, gameID)
    return &game, nil
}

// GetGame returns the current state of a chess game.
func (s *ChessService) GetGame(gameID uint) (*model.ChessGame, error) {
    var game model.ChessGame
    if err := s.db.First(&game, gameID).Error; err != nil {
        if errors.Is(err, gorm.ErrRecordNotFound) {
            return nil, nil
        }
        return nil, fmt.Errorf("failed to get game: %w", err)
    }
    return &game, nil
}

// Resign allows a player to resign the game.
func (s *ChessService) Resign(gameID, playerID uint) (*model.ChessGame, error) {
    var game model.ChessGame
    if err := s.db.First(&game, gameID).Error; err != nil {
        return nil, fmt.Errorf("game not found")
    }
    if playerID != game.WhiteID && playerID != game.BlackID {
        return nil, fmt.Errorf("player is not in this game")
    }
    s.db.Model(&game).Update("status", "resigned")
    s.db.First(&game, gameID)
    return &game, nil
}

// --- Helpers ---

func algebraicToIndex(square string) (int, int, error) {
    if len(square) != 2 {
        return 0, 0, fmt.Errorf("invalid square: %s", square)
    }
    col := int(square[0] - 'a')
    row := int(square[1] - '1')
    if col < 0 || col > 7 || row < 0 || row > 7 {
        return 0, 0, fmt.Errorf("square out of range: %s", square)
    }
    return row, col, nil
}

func isUppercase(s string) bool {
    return len(s) > 0 && s[0] >= 'A' && s[0] <= 'Z'
}
```

### Step 3: Add HTTP Handlers

Add to `internal/handler/handler.go`:

```go
// --- Chess Handlers ---

// CreateChessGame handles POST /api/chess
func (h *Handler) CreateChessGame(w http.ResponseWriter, r *http.Request) {
    var req struct {
        SessionID uint `json:"session_id"`
        WhiteID   uint `json:"white_id"`
        BlackID   uint `json:"black_id"`
    }
    if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
        respondError(w, http.StatusBadRequest, "invalid request body")
        return
    }

    game, err := h.Chess.CreateGame(req.SessionID, req.WhiteID, req.BlackID)
    if err != nil {
        respondError(w, http.StatusBadRequest, err.Error())
        return
    }
    respondJSON(w, http.StatusCreated, game)
}

// GetChessGame handles GET /api/chess/{id}
func (h *Handler) GetChessGame(w http.ResponseWriter, r *http.Request) {
    id, err := parseIDParam(r)
    if err != nil {
        respondError(w, http.StatusBadRequest, "invalid game id")
        return
    }
    game, err := h.Chess.GetGame(id)
    if err != nil {
        respondError(w, http.StatusInternalServerError, err.Error())
        return
    }
    if game == nil {
        respondError(w, http.StatusNotFound, "game not found")
        return
    }
    respondJSON(w, http.StatusOK, game)
}

// MakeChessMove handles POST /api/chess/{id}/move
func (h *Handler) MakeChessMove(w http.ResponseWriter, r *http.Request) {
    id, err := parseIDParam(r)
    if err != nil {
        respondError(w, http.StatusBadRequest, "invalid game id")
        return
    }

    var req struct {
        PlayerID uint   `json:"player_id"`
        From     string `json:"from"`
        To       string `json:"to"`
    }
    if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
        respondError(w, http.StatusBadRequest, "invalid request body")
        return
    }

    game, err := h.Chess.MakeMove(id, req.PlayerID, req.From, req.To)
    if err != nil {
        respondError(w, http.StatusBadRequest, err.Error())
        return
    }
    respondJSON(w, http.StatusOK, game)
}

// ResignChessGame handles POST /api/chess/{id}/resign
func (h *Handler) ResignChessGame(w http.ResponseWriter, r *http.Request) {
    id, err := parseIDParam(r)
    if err != nil {
        respondError(w, http.StatusBadRequest, "invalid game id")
        return
    }

    var req struct {
        PlayerID uint `json:"player_id"`
    }
    if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
        respondError(w, http.StatusBadRequest, "invalid request body")
        return
    }

    game, err := h.Chess.Resign(id, req.PlayerID)
    if err != nil {
        respondError(w, http.StatusBadRequest, err.Error())
        return
    }
    respondJSON(w, http.StatusOK, game)
}
```

### Step 4: Register Routes

Add to `internal/handler/routes.go` inside `RegisterRoutes()`:

```go
// Chess
api.HandleFunc("/chess", h.CreateChessGame).Methods("POST")
api.HandleFunc("/chess/{id:[0-9]+}", h.GetChessGame).Methods("GET")
api.HandleFunc("/chess/{id:[0-9]+}/move", h.MakeChessMove).Methods("POST")
api.HandleFunc("/chess/{id:[0-9]+}/resign", h.ResignChessGame).Methods("POST")
```

### Step 5: Wire It Up

Update `internal/handler/handler.go`:

```go
type Handler struct {
    Players  *service.PlayerService
    Sessions *service.SessionService
    Chess    *service.ChessService    // ← add this
}
```

Update `cmd/gameserver/main.go`:

```go
chessService := service.NewChessService(db)
h := handler.New(playerService, sessionService)
h.Chess = chessService  // attach the chess service
```

Update `internal/store/store.go` to auto-migrate the new model:

```go
if err := db.AutoMigrate(&model.Player{}, &model.GameSession{}, &model.ChessGame{}); err != nil {
```

### Step 6: Play a Game via curl

```bash
# 1. Start the server
go run ./cmd/gameserver

# 2. Create two players
curl -s -X POST http://localhost:3000/api/players \
  -H 'Content-Type: application/json' \
  -d '{"uid": "alice", "name": "Alice"}'

curl -s -X POST http://localhost:3000/api/players \
  -H 'Content-Type: application/json' \
  -d '{"uid": "bob", "name": "Bob"}'

# 3. Create a session
curl -s -X POST http://localhost:3000/api/sessions \
  -H 'Content-Type: application/json' \
  -d '{"session_id": "chess-match-1", "max_players": 2}'

# 4. Both players join
curl -s -X POST http://localhost:3000/api/sessions/chess-match-1/join \
  -H 'Content-Type: application/json' \
  -d '{"player_id": 1}'

curl -s -X POST http://localhost:3000/api/sessions/chess-match-1/join \
  -H 'Content-Type: application/json' \
  -d '{"player_id": 2}'

# 5. Start the chess game
curl -s -X POST http://localhost:3000/api/chess \
  -H 'Content-Type: application/json' \
  -d '{"session_id": 1, "white_id": 1, "black_id": 2}'

# 6. White opens with e2→e4
curl -s -X POST http://localhost:3000/api/chess/1/move \
  -H 'Content-Type: application/json' \
  -d '{"player_id": 1, "from": "e2", "to": "e4"}'

# 7. Black responds with e7→e5
curl -s -X POST http://localhost:3000/api/chess/1/move \
  -H 'Content-Type: application/json' \
  -d '{"player_id": 2, "from": "e7", "to": "e5"}'

# 8. Check the board state at any time
curl -s http://localhost:3000/api/chess/1 | python3 -m json.tool
```

### Implementing Full Chess Rules

For a production chess server, you would add full move validation in `ChessService.MakeMove()`. The recommended approach:

1. **Use a chess library** — import a Go chess package (e.g., `github.com/notnil/chess`) for move validation, check/checkmate detection, PGN export, and FEN parsing
2. **Or implement from scratch** — validate each piece type's movement pattern, check detection, castling rights, en passant, pawn promotion, and the 50-move rule

Either way, the server architecture remains the same — the game logic lives in the service layer, the HTTP handler is thin, and the database stores the authoritative state.

---

## Example 2: Mechabellum-Style RTS Server

A Mechabellum-style game has fundamentally different requirements from chess:

- **Many unit types** with different stats (speed, health, damage, range)
- **Real-time simulation** — the server ticks forward at a fixed rate
- **Projectile physics** — weapons have trajectories, travel time, splash damage
- **Simultaneous players** — both sides act at the same time
- **Large state** — potentially hundreds of units with position, velocity, status

This requires a **game loop with a tick rate** and **WebSocket connections** for real-time state streaming.

### Architecture for Real-Time Games

```
                  WebSocket
Unity Client ◄──────────────► Go Game Server
                                    │
                              ┌─────▼──────┐
                              │  Game Loop  │  ← runs at 20 ticks/sec
                              │  (goroutine)│
                              └─────┬──────┘
                                    │
                            ┌───────▼────────┐
                            │  Game State    │
                            │  • Units[]     │
                            │  • Projectiles│
                            │  • Terrain    │
                            └───────┬────────┘
                                    │
                              ┌─────▼──────┐
                              │  Database  │  ← save results, replays
                              └────────────┘
```

### Step 1: Define Unit Types and Game State Models

Add to `internal/model/model.go`:

```go
// UnitType defines a class of unit with its base stats.
type UnitType struct {
    ID        uint           `json:"id" gorm:"primarykey"`
    CreatedAt time.Time      `json:"created_at"`
    UpdatedAt time.Time      `json:"updated_at"`
    DeletedAt gorm.DeletedAt `json:"-" gorm:"index"`

    Name           string  `json:"name" gorm:"uniqueIndex;size:64;not null"`
    Health         int     `json:"health"`
    Speed          float64 `json:"speed"`           // units per second
    AttackDamage   int     `json:"attack_damage"`
    AttackRange    float64 `json:"attack_range"`    // distance units
    AttackCooldown float64 `json:"attack_cooldown"` // seconds between attacks
    ProjectileSpeed float64 `json:"projectile_speed"` // 0 = hitscan (instant)
    SplashRadius   float64 `json:"splash_radius"`   // 0 = single target
    Cost           int     `json:"cost"`
    Description    string  `json:"description" gorm:"size:256"`
}

// BattleUnit represents a single unit instance in a battle.
// This is NOT stored in the database during gameplay — it lives in memory
// for performance. Only the final result is persisted.
type BattleUnit struct {
    ID         string  `json:"id"`          // unique within the battle
    TypeName   string  `json:"type_name"`   // references UnitType.Name
    OwnerID    uint    `json:"owner_id"`    // player who owns this unit
    X          float64 `json:"x"`           // position
    Y          float64 `json:"y"`
    Health     int     `json:"health"`      // current health
    MaxHealth  int     `json:"max_health"`
    TargetID   string  `json:"target_id"`   // ID of unit being attacked
    CooldownLeft float64 `json:"cooldown_left"` // seconds until next attack
    Alive      bool    `json:"alive"`
}

// Projectile represents a projectile in flight.
type Projectile struct {
    ID        string  `json:"id"`
    OwnerID   uint    `json:"owner_id"`
    X         float64 `json:"x"`
    Y         float64 `json:"y"`
    TargetX   float64 `json:"target_x"`
    TargetY   float64 `json:"target_y"`
    Speed     float64 `json:"speed"`
    Damage    int     `json:"damage"`
    Splash    float64 `json:"splash"`
}

// BattleResult is persisted to the database after a match ends.
type BattleResult struct {
    ID         uint           `json:"id" gorm:"primarykey"`
    CreatedAt  time.Time      `json:"created_at"`
    DeletedAt  gorm.DeletedAt `json:"-" gorm:"index"`

    SessionID  uint   `json:"session_id"`
    WinnerID   uint   `json:"winner_id"`
    LoserID    uint   `json:"loser_id"`
    TotalTicks int    `json:"total_ticks"`
    ReplayData string `json:"replay_data" gorm:"type:text"` // JSON-encoded tick snapshots
}
```

### Step 2: Define Unit Catalog

Create `internal/service/units.go`:

```go
package service

// DefaultUnitTypes returns the built-in unit catalog.
// In production, you'd load these from the database or a config file.
func DefaultUnitTypes() map[string]UnitStats {
    return map[string]UnitStats{
        "crawler": {
            Health: 50, Speed: 3.0,
            AttackDamage: 10, AttackRange: 1.5, AttackCooldown: 0.8,
            ProjectileSpeed: 0, SplashRadius: 0, Cost: 100,
        },
        "archer": {
            Health: 30, Speed: 2.0,
            AttackDamage: 15, AttackRange: 8.0, AttackCooldown: 1.5,
            ProjectileSpeed: 15.0, SplashRadius: 0, Cost: 150,
        },
        "tank": {
            Health: 200, Speed: 1.0,
            AttackDamage: 40, AttackRange: 5.0, AttackCooldown: 2.0,
            ProjectileSpeed: 20.0, SplashRadius: 0, Cost: 300,
        },
        "artillery": {
            Health: 60, Speed: 0.5,
            AttackDamage: 80, AttackRange: 15.0, AttackCooldown: 4.0,
            ProjectileSpeed: 10.0, SplashRadius: 3.0, Cost: 400,
        },
        "scout": {
            Health: 20, Speed: 5.0,
            AttackDamage: 5, AttackRange: 2.0, AttackCooldown: 0.5,
            ProjectileSpeed: 0, SplashRadius: 0, Cost: 50,
        },
        "healer": {
            Health: 40, Speed: 2.0,
            AttackDamage: -10, AttackRange: 6.0, AttackCooldown: 2.0, // negative = healing
            ProjectileSpeed: 0, SplashRadius: 2.0, Cost: 250,
        },
    }
}

// UnitStats holds the base statistics for a unit type.
type UnitStats struct {
    Health          int
    Speed           float64
    AttackDamage    int
    AttackRange     float64
    AttackCooldown  float64
    ProjectileSpeed float64
    SplashRadius    float64
    Cost            int
}
```

### Step 3: Build the Game Loop

Create `internal/service/battle.go`:

```go
package service

import (
    "encoding/json"
    "fmt"
    "math"
    "sync"
    "time"

    "gorm.io/gorm"

    "github.com/sumner-mccarty/go-gameserver/internal/model"
)

const (
    TickRate    = 20              // ticks per second
    TickDelta   = 1.0 / TickRate // seconds per tick (0.05)
    MaxTicks    = 6000           // 5-minute max game (20 * 60 * 5)
)

// BattleState holds the full in-memory state of a running battle.
type BattleState struct {
    mu          sync.RWMutex
    SessionID   uint
    Player1ID   uint
    Player2ID   uint
    Units       []model.BattleUnit
    Projectiles []model.Projectile
    Tick        int
    Status      string // "running", "finished"
    WinnerID    uint
    TickHistory [][]byte // snapshot per tick for replay
}

// BattleService manages real-time battles.
type BattleService struct {
    db       *gorm.DB
    units    map[string]UnitStats
    battles  map[uint]*BattleState // sessionID → state
    mu       sync.RWMutex
}

// NewBattleService creates a new BattleService.
func NewBattleService(db *gorm.DB) *BattleService {
    return &BattleService{
        db:      db,
        units:   DefaultUnitTypes(),
        battles: make(map[uint]*BattleState),
    }
}

// StartBattle begins a new real-time battle simulation.
func (s *BattleService) StartBattle(sessionID, player1ID, player2ID uint, army1, army2 []ArmyUnit) (*BattleState, error) {
    state := &BattleState{
        SessionID: sessionID,
        Player1ID: player1ID,
        Player2ID: player2ID,
        Status:    "running",
    }

    // Spawn player 1's army on the left side
    for i, au := range army1 {
        stats, ok := s.units[au.TypeName]
        if !ok {
            return nil, fmt.Errorf("unknown unit type: %s", au.TypeName)
        }
        state.Units = append(state.Units, model.BattleUnit{
            ID:        fmt.Sprintf("p1-%s-%d", au.TypeName, i),
            TypeName:  au.TypeName,
            OwnerID:   player1ID,
            X:         au.X,
            Y:         au.Y,
            Health:    stats.Health,
            MaxHealth: stats.Health,
            Alive:     true,
        })
    }

    // Spawn player 2's army on the right side
    for i, au := range army2 {
        stats, ok := s.units[au.TypeName]
        if !ok {
            return nil, fmt.Errorf("unknown unit type: %s", au.TypeName)
        }
        state.Units = append(state.Units, model.BattleUnit{
            ID:        fmt.Sprintf("p2-%s-%d", au.TypeName, i),
            TypeName:  au.TypeName,
            OwnerID:   player2ID,
            X:         au.X,
            Y:         au.Y,
            Health:    stats.Health,
            MaxHealth: stats.Health,
            Alive:     true,
        })
    }

    s.mu.Lock()
    s.battles[sessionID] = state
    s.mu.Unlock()

    // Start the game loop in a goroutine
    go s.runGameLoop(state)

    return state, nil
}

// runGameLoop is the server-authoritative simulation loop.
func (s *BattleService) runGameLoop(state *BattleState) {
    ticker := time.NewTicker(time.Second / TickRate)
    defer ticker.Stop()

    for range ticker.C {
        state.mu.Lock()

        if state.Status != "running" {
            state.mu.Unlock()
            break
        }

        state.Tick++

        // 1. Move units toward their targets
        s.tickMovement(state)

        // 2. Units acquire targets and attack
        s.tickCombat(state)

        // 3. Move projectiles and check hits
        s.tickProjectiles(state)

        // 4. Remove dead units
        s.tickCleanup(state)

        // 5. Check win condition
        s.tickWinCheck(state)

        // 6. Save tick snapshot for replay
        snapshot, err := json.Marshal(struct {
            Tick        int                  `json:"tick"`
            Units       []model.BattleUnit   `json:"units"`
            Projectiles []model.Projectile   `json:"projectiles"`
        }{state.Tick, state.Units, state.Projectiles})
        if err != nil {
            log.Printf("warning: failed to marshal tick %d snapshot: %v", state.Tick, err)
        } else {
            state.TickHistory = append(state.TickHistory, snapshot)
        }

        // 7. Time limit
        if state.Tick >= MaxTicks {
            state.Status = "finished"
        }

        state.mu.Unlock()

        if state.Status == "finished" {
            s.saveBattleResult(state)
            break
        }
    }
}

// tickMovement moves each unit toward its target.
func (s *BattleService) tickMovement(state *BattleState) {
    for i := range state.Units {
        u := &state.Units[i]
        if !u.Alive || u.TargetID == "" {
            continue
        }

        target := s.findUnit(state, u.TargetID)
        if target == nil || !target.Alive {
            u.TargetID = ""
            continue
        }

        stats := s.units[u.TypeName]
        dist := distance(u.X, u.Y, target.X, target.Y)

        // Only move if out of attack range
        if dist > stats.AttackRange {
            dx := target.X - u.X
            dy := target.Y - u.Y
            moveAmount := stats.Speed * TickDelta
            u.X += (dx / dist) * moveAmount
            u.Y += (dy / dist) * moveAmount
        }
    }
}

// tickCombat handles target acquisition and attacking.
func (s *BattleService) tickCombat(state *BattleState) {
    for i := range state.Units {
        u := &state.Units[i]
        if !u.Alive {
            continue
        }

        stats := s.units[u.TypeName]

        // Reduce cooldown
        u.CooldownLeft -= TickDelta
        if u.CooldownLeft < 0 {
            u.CooldownLeft = 0
        }

        // Find closest enemy if no target
        if u.TargetID == "" {
            u.TargetID = s.findClosestEnemy(state, u)
        }

        // Attack if in range and cooldown ready
        if u.TargetID != "" && u.CooldownLeft <= 0 {
            target := s.findUnit(state, u.TargetID)
            if target != nil && target.Alive {
                dist := distance(u.X, u.Y, target.X, target.Y)
                if dist <= stats.AttackRange {
                    if stats.ProjectileSpeed > 0 {
                        // Ranged attack — spawn projectile
                        state.Projectiles = append(state.Projectiles, model.Projectile{
                            ID:      fmt.Sprintf("proj-%d-%d", state.Tick, i),
                            OwnerID: u.OwnerID,
                            X:       u.X, Y: u.Y,
                            TargetX: target.X, TargetY: target.Y,
                            Speed:   stats.ProjectileSpeed,
                            Damage:  stats.AttackDamage,
                            Splash:  stats.SplashRadius,
                        })
                    } else {
                        // Melee attack — instant damage
                        target.Health -= stats.AttackDamage
                        if target.Health <= 0 {
                            target.Alive = false
                        }
                    }
                    u.CooldownLeft = stats.AttackCooldown
                }
            }
        }
    }
}

// tickProjectiles moves projectiles and checks for impact.
func (s *BattleService) tickProjectiles(state *BattleState) {
    alive := state.Projectiles[:0]
    for _, p := range state.Projectiles {
        dist := distance(p.X, p.Y, p.TargetX, p.TargetY)
        moveAmount := p.Speed * TickDelta

        if dist <= moveAmount {
            // Projectile hit — apply damage
            if p.Splash > 0 {
                // Splash damage to all enemies in radius
                for j := range state.Units {
                    eu := &state.Units[j]
                    if eu.Alive && eu.OwnerID != p.OwnerID {
                        if distance(eu.X, eu.Y, p.TargetX, p.TargetY) <= p.Splash {
                            eu.Health -= p.Damage
                            if eu.Health <= 0 {
                                eu.Alive = false
                            }
                        }
                    }
                }
            } else {
                // Single target — find unit closest to impact point
                for j := range state.Units {
                    eu := &state.Units[j]
                    if eu.Alive && eu.OwnerID != p.OwnerID {
                        if distance(eu.X, eu.Y, p.TargetX, p.TargetY) < 0.5 {
                            eu.Health -= p.Damage
                            if eu.Health <= 0 {
                                eu.Alive = false
                            }
                            break
                        }
                    }
                }
            }
            // Projectile consumed — don't add to alive list
        } else {
            // Move projectile toward target
            dx := p.TargetX - p.X
            dy := p.TargetY - p.Y
            p.X += (dx / dist) * moveAmount
            p.Y += (dy / dist) * moveAmount
            alive = append(alive, p)
        }
    }
    state.Projectiles = alive
}

// tickCleanup marks dead units.
func (s *BattleService) tickCleanup(state *BattleState) {
    for i := range state.Units {
        if state.Units[i].Health <= 0 {
            state.Units[i].Alive = false
        }
    }
}

// tickWinCheck determines if one side has won.
func (s *BattleService) tickWinCheck(state *BattleState) {
    p1Alive, p2Alive := false, false
    for _, u := range state.Units {
        if u.Alive && u.OwnerID == state.Player1ID {
            p1Alive = true
        }
        if u.Alive && u.OwnerID == state.Player2ID {
            p2Alive = true
        }
    }
    if !p1Alive && !p2Alive {
        state.Status = "finished" // draw
    } else if !p1Alive {
        state.Status = "finished"
        state.WinnerID = state.Player2ID
    } else if !p2Alive {
        state.Status = "finished"
        state.WinnerID = state.Player1ID
    }
}

// saveBattleResult persists the final result to the database.
func (s *BattleService) saveBattleResult(state *BattleState) {
    replayJSON, err := json.Marshal(state.TickHistory)
    if err != nil {
        log.Printf("warning: failed to marshal replay data: %v", err)
        replayJSON = []byte("[]")
    }
    loserID := state.Player1ID
    if state.WinnerID == state.Player1ID {
        loserID = state.Player2ID
    }
    result := &model.BattleResult{
        SessionID:  state.SessionID,
        WinnerID:   state.WinnerID,
        LoserID:    loserID,
        TotalTicks: state.Tick,
        ReplayData: string(replayJSON),
    }
    s.db.Create(result)
}

// GetState returns the current battle state (thread-safe).
func (s *BattleService) GetState(sessionID uint) (*BattleState, error) {
    s.mu.RLock()
    state, ok := s.battles[sessionID]
    s.mu.RUnlock()
    if !ok {
        return nil, fmt.Errorf("no active battle for session %d", sessionID)
    }
    return state, nil
}

// --- Helpers ---

type ArmyUnit struct {
    TypeName string  `json:"type_name"`
    X        float64 `json:"x"`
    Y        float64 `json:"y"`
}

func (s *BattleService) findUnit(state *BattleState, id string) *model.BattleUnit {
    for i := range state.Units {
        if state.Units[i].ID == id {
            return &state.Units[i]
        }
    }
    return nil
}

func (s *BattleService) findClosestEnemy(state *BattleState, unit *model.BattleUnit) string {
    minDist := math.MaxFloat64
    closestID := ""
    for _, other := range state.Units {
        if !other.Alive || other.OwnerID == unit.OwnerID {
            continue
        }
        d := distance(unit.X, unit.Y, other.X, other.Y)
        if d < minDist {
            minDist = d
            closestID = other.ID
        }
    }
    return closestID
}

func distance(x1, y1, x2, y2 float64) float64 {
    dx := x2 - x1
    dy := y2 - y1
    return math.Sqrt(dx*dx + dy*dy)
}
```

### Step 4: Add WebSocket Support for Real-Time Updates

For real-time games, you need WebSocket connections so the server can push state updates to clients every tick. Add the `gorilla/websocket` package:

```bash
go get github.com/gorilla/websocket
```

Create `internal/handler/websocket.go`:

```go
package handler

import (
    "encoding/json"
    "log"
    "net/http"
    "strconv"
    "time"

    "github.com/gorilla/mux"
    "github.com/gorilla/websocket"
)

var upgrader = websocket.Upgrader{
    ReadBufferSize:  1024,
    WriteBufferSize: 1024,
    CheckOrigin: func(r *http.Request) bool {
        return true // In production, validate the origin
    },
}

// BattleWebSocket handles GET /ws/battle/{session_id}
// Unity connects here to receive real-time game state updates.
func (h *Handler) BattleWebSocket(w http.ResponseWriter, r *http.Request) {
    sessionIDStr := mux.Vars(r)["session_id"]
    sessionID, err := strconv.ParseUint(sessionIDStr, 10, 32)
    if err != nil {
        http.Error(w, "invalid session_id", http.StatusBadRequest)
        return
    }

    conn, err := upgrader.Upgrade(w, r, nil)
    if err != nil {
        log.Printf("websocket upgrade failed: %v", err)
        return
    }
    defer conn.Close()

    // Stream battle state at the tick rate
    ticker := time.NewTicker(50 * time.Millisecond) // 20 Hz
    defer ticker.Stop()

    for range ticker.C {
        state, err := h.Battle.GetState(uint(sessionID))
        if err != nil {
            conn.WriteMessage(websocket.TextMessage, []byte(`{"error": "no active battle"}`))
            return
        }

        state.mu.RLock()
        data, _ := json.Marshal(map[string]interface{}{
            "tick":        state.Tick,
            "status":      state.Status,
            "units":       state.Units,
            "projectiles": state.Projectiles,
            "winner_id":   state.WinnerID,
        })
        state.mu.RUnlock()

        if err := conn.WriteMessage(websocket.TextMessage, data); err != nil {
            break // Client disconnected
        }

        if state.Status == "finished" {
            break
        }
    }
}
```

Register the WebSocket route in `routes.go`:

```go
// WebSocket (outside /api prefix — no JSON middleware)
r.HandleFunc("/ws/battle/{session_id:[0-9]+}", h.BattleWebSocket)
```

### Step 5: Start a Battle via the API

Add an HTTP endpoint to start a battle (players submit their armies, then the simulation begins):

```go
// StartBattle handles POST /api/battle/start
func (h *Handler) StartBattle(w http.ResponseWriter, r *http.Request) {
    var req struct {
        SessionID uint              `json:"session_id"`
        Player1ID uint              `json:"player1_id"`
        Player2ID uint              `json:"player2_id"`
        Army1     []service.ArmyUnit `json:"army1"`
        Army2     []service.ArmyUnit `json:"army2"`
    }
    if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
        respondError(w, http.StatusBadRequest, "invalid request body")
        return
    }

    state, err := h.Battle.StartBattle(req.SessionID, req.Player1ID, req.Player2ID, req.Army1, req.Army2)
    if err != nil {
        respondError(w, http.StatusBadRequest, err.Error())
        return
    }
    respondJSON(w, http.StatusOK, map[string]interface{}{
        "message":    "battle started",
        "session_id": state.SessionID,
        "unit_count": len(state.Units),
    })
}
```

### Step 6: Example — Start a Mechabellum Battle via curl

```bash
# 1. Create players and session (same as chess example)

# 2. Start a battle with armies
curl -s -X POST http://localhost:3000/api/battle/start \
  -H 'Content-Type: application/json' \
  -d '{
    "session_id": 1,
    "player1_id": 1,
    "player2_id": 2,
    "army1": [
      {"type_name": "tank",    "x": 5,  "y": 10},
      {"type_name": "tank",    "x": 5,  "y": 15},
      {"type_name": "archer",  "x": 3,  "y": 10},
      {"type_name": "archer",  "x": 3,  "y": 15},
      {"type_name": "crawler", "x": 8,  "y": 12},
      {"type_name": "crawler", "x": 8,  "y": 13},
      {"type_name": "crawler", "x": 8,  "y": 14}
    ],
    "army2": [
      {"type_name": "artillery", "x": 95, "y": 10},
      {"type_name": "artillery", "x": 95, "y": 15},
      {"type_name": "scout",     "x": 90, "y": 10},
      {"type_name": "scout",     "x": 90, "y": 12},
      {"type_name": "scout",     "x": 90, "y": 14},
      {"type_name": "healer",    "x": 93, "y": 12}
    ]
  }'

# 3. Connect via WebSocket to watch the battle (use websocat or wscat)
# Install: cargo install websocat  OR  npm install -g wscat
websocat ws://localhost:3000/ws/battle/1
```

### Adding More Unit Types

To add a new unit type, you just add an entry to the `DefaultUnitTypes()` map:

```go
"flamethrower": {
    Health: 80, Speed: 1.5,
    AttackDamage: 25, AttackRange: 3.0, AttackCooldown: 0.3,
    ProjectileSpeed: 0, SplashRadius: 2.0, Cost: 350,
},
```

To make unit types configurable at runtime, load them from the database using the `UnitType` model and expose CRUD endpoints:

```
POST   /api/unit-types          — create a new unit type
GET    /api/unit-types           — list all unit types
PUT    /api/unit-types/{id}     — update stats
DELETE /api/unit-types/{id}     — remove a unit type
```

---

## Connecting a Unity Client

### For Turn-Based Games (Chess)

Unity polls the server or uses long-polling:

```csharp
// Unity C# — ChessClient.cs
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

public class ChessClient : MonoBehaviour
{
    private string serverURL = "http://localhost:3000";
    private int gameID = 1;
    private int playerID = 1;

    // Poll the game state every 2 seconds
    IEnumerator Start()
    {
        while (true)
        {
            yield return GetGameState();
            yield return new WaitForSeconds(2f);
        }
    }

    IEnumerator GetGameState()
    {
        using (var req = UnityWebRequest.Get($"{serverURL}/api/chess/{gameID}"))
        {
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success)
            {
                var game = JsonUtility.FromJson<ChessGameState>(req.downloadHandler.text);
                UpdateBoard(game);
            }
        }
    }

    public void MakeMove(string from, string to)
    {
        StartCoroutine(SendMove(from, to));
    }

    IEnumerator SendMove(string from, string to)
    {
        var body = JsonUtility.ToJson(new MoveRequest {
            player_id = playerID, from = from, to = to
        });
        var bodyBytes = System.Text.Encoding.UTF8.GetBytes(body);
        using (var req = new UnityWebRequest(
            $"{serverURL}/api/chess/{gameID}/move", "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(bodyBytes);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success)
            {
                var game = JsonUtility.FromJson<ChessGameState>(req.downloadHandler.text);
                UpdateBoard(game);
            }
            else
            {
                Debug.LogError($"Move failed: {req.downloadHandler.text}");
            }
        }
    }

    void UpdateBoard(ChessGameState state)
    {
        // Update your Unity chess board visuals here
        // state.board is an 8x8 array of piece strings
        // state.turn tells you whose move it is
        Debug.Log($"Turn: {state.turn}, Move #{state.move_count}");
    }

    [System.Serializable]
    public class ChessGameState
    {
        public int id;
        public string[][] board;
        public string turn;
        public string status;
        public int move_count;
    }

    [System.Serializable]
    public class MoveRequest
    {
        public int player_id;
        public string from;
        public string to;
    }
}
```

### For Real-Time Games (Mechabellum)

Unity connects via WebSocket and renders every server tick:

```csharp
// Unity C# — BattleClient.cs
using UnityEngine;
using NativeWebSocket; // install: https://github.com/endel/NativeWebSocket

public class BattleClient : MonoBehaviour
{
    WebSocket ws;
    BattleState latestState;

    async void Start()
    {
        ws = new WebSocket("ws://localhost:3000/ws/battle/1");

        ws.OnMessage += (bytes) =>
        {
            string json = System.Text.Encoding.UTF8.GetString(bytes);
            latestState = JsonUtility.FromJson<BattleState>(json);
        };

        ws.OnClose += (code) => Debug.Log("Battle ended");

        await ws.Connect();
    }

    void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        ws?.DispatchMessageQueue();
#endif

        if (latestState != null)
        {
            RenderBattle(latestState);
        }
    }

    void RenderBattle(BattleState state)
    {
        // For each unit in state.units:
        //   - Find or create a GameObject
        //   - Lerp its position to (unit.x, unit.y)
        //   - Update health bar
        //   - Play attack/death animations based on state changes

        // For each projectile in state.projectiles:
        //   - Spawn or move a projectile VFX

        foreach (var unit in state.units)
        {
            if (!unit.alive) continue;
            // Move unit GameObjects to new positions
            // Unity handles interpolation and animation
        }
    }

    async void OnDestroy()
    {
        if (ws != null) await ws.Close();
    }

    [System.Serializable]
    public class BattleState
    {
        public int tick;
        public string status;
        public BattleUnit[] units;
        public Projectile[] projectiles;
        public int winner_id;
    }

    [System.Serializable]
    public class BattleUnit
    {
        public string id;
        public string type_name;
        public int owner_id;
        public float x, y;
        public int health, max_health;
        public bool alive;
    }

    [System.Serializable]
    public class Projectile
    {
        public string id;
        public float x, y, target_x, target_y;
    }
}
```

### Key Unity Architecture Pattern

```
Unity Project/
├── Scripts/
│   ├── Networking/
│   │   ├── GameServerClient.cs   — HTTP client for REST API
│   │   └── BattleSocket.cs       — WebSocket client for real-time
│   ├── GameState/
│   │   ├── ChessState.cs         — Chess-specific state management
│   │   └── BattleState.cs        — RTS-specific state management
│   ├── Rendering/
│   │   ├── BoardRenderer.cs      — Chess board visualization
│   │   └── BattleRenderer.cs     — RTS unit/projectile rendering
│   └── UI/
│       ├── LobbyUI.cs            — Session browser, join game
│       └── GameUI.cs             — In-game HUD
└── Prefabs/
    ├── ChessPieces/              — 3D models for each piece
    └── Units/                    — Mechabellum unit prefabs
        ├── Crawler.prefab
        ├── Archer.prefab
        ├── Tank.prefab
        └── Artillery.prefab
```

---

## Going to Production

### Scaling Considerations

| Game Type | Server Load | Scaling Strategy |
|-----------|------------|------------------|
| Chess | Low (1 request per move) | Horizontal scaling behind ALB; many games per server |
| Mechabellum | High (20 ticks/sec per game) | Dedicated goroutine per battle; scale by instance count |

### Database Strategy

| Data | Storage | Why |
|------|---------|-----|
| Player accounts | PostgreSQL | Persistent, queryable, relational |
| Game sessions | PostgreSQL | Need to list/search active games |
| Chess board state | PostgreSQL | Small, changes infrequently |
| RTS battle state | **In-memory** | Changes 20x/sec — too fast for DB writes |
| Battle results | PostgreSQL | Persisted when battle ends |
| Replay data | PostgreSQL or S3 | Can be large; archive to object storage |

### Deployment Checklist

1. **Build the Docker image** with your game-specific code
2. **Choose your database** — SQLite for dev, PostgreSQL for production (already configured)
3. **Deploy to AWS** using the included Terraform configs (`deploy/aws/main.tf`)
4. **Set environment variables** for production database and host
5. **Configure auto-scaling** based on your game's load profile
6. **Add monitoring** — the `/api/health` endpoint works with ALB health checks out of the box
7. **Enable HTTPS** — add an ACM certificate and HTTPS listener to the ALB

See the main [README.md](../README.md) for detailed deployment instructions for AWS, GCP, Azure, and DigitalOcean.

### Extending Further

Ideas for additional features you can build on this foundation:

- **Matchmaking** — add an ELO rating system and match players of similar skill
- **Spectator mode** — allow WebSocket connections that only receive state (read-only)
- **Chat** — add a `/ws/chat/{session_id}` WebSocket endpoint
- **Leaderboards** — query player stats from PostgreSQL
- **Replay playback** — serve recorded tick data through a REST endpoint
- **Multiple game types** — add a `game_type` field to `GameSession` and route to the appropriate service
- **Anti-cheat** — since all logic is server-side, the server is already the authority; add rate limiting and input validation
- **Tournaments** — bracket management using session relationships

---

## Summary

| What You Want | What You Do |
|--------------|-------------|
| Turn-based game (chess, cards) | Add models + service + HTTP handlers. Poll for state or use long-polling. |
| Real-time game (RTS, action) | Add models + service with game loop + WebSocket handler. Unity renders the state stream. |
| New unit/piece types | Add entries to your type catalog (code or database) |
| Persist results | Write to the database when the game ends (already set up) |
| Deploy to cloud | Use the included Dockerfile + Terraform (see README) |
| Connect Unity | HTTP for lobbies and turn-based; WebSocket for real-time gameplay |

The entire game logic runs on the Go server. Unity is a renderer. The server is the single source of truth.
