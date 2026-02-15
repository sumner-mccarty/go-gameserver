package handler

import (
	"bytes"
	"encoding/json"
	"net/http"
	"net/http/httptest"
	"testing"

	"github.com/gorilla/mux"
	"gorm.io/driver/sqlite"
	"gorm.io/gorm"
	"gorm.io/gorm/logger"

	"github.com/sumner-mccarty/go-gameserver/internal/model"
	"github.com/sumner-mccarty/go-gameserver/internal/service"
)

// newTestRouter creates a test router with in-memory SQLite and all routes registered.
func newTestRouter(t *testing.T) (*mux.Router, *gorm.DB) {
	t.Helper()
	db, err := gorm.Open(sqlite.Open(":memory:"), &gorm.Config{
		Logger: logger.Discard,
	})
	if err != nil {
		t.Fatalf("failed to open test database: %v", err)
	}
	if err := db.AutoMigrate(&model.Player{}, &model.GameSession{}); err != nil {
		t.Fatalf("failed to migrate test database: %v", err)
	}

	h := New(service.NewPlayerService(db), service.NewSessionService(db))
	r := mux.NewRouter()
	RegisterRoutes(r, h)
	return r, db
}

func doRequest(r *mux.Router, method, path string, body interface{}) *httptest.ResponseRecorder {
	var reqBody *bytes.Buffer
	if body != nil {
		b, _ := json.Marshal(body)
		reqBody = bytes.NewBuffer(b)
	} else {
		reqBody = bytes.NewBuffer(nil)
	}
	req := httptest.NewRequest(method, path, reqBody)
	req.Header.Set("Content-Type", "application/json")
	rr := httptest.NewRecorder()
	r.ServeHTTP(rr, req)
	return rr
}

func decodeJSON(t *testing.T, rr *httptest.ResponseRecorder, v interface{}) {
	t.Helper()
	if err := json.NewDecoder(rr.Body).Decode(v); err != nil {
		t.Fatalf("failed to decode response: %v (body: %s)", err, rr.Body.String())
	}
}

// --- Health Check ---

func TestHealthCheck(t *testing.T) {
	r, _ := newTestRouter(t)
	rr := doRequest(r, "GET", "/api/health", nil)

	if rr.Code != http.StatusOK {
		t.Errorf("expected status 200, got %d", rr.Code)
	}
	var resp map[string]string
	decodeJSON(t, rr, &resp)
	if resp["status"] != "ok" {
		t.Errorf("expected status ok, got %s", resp["status"])
	}
}

// --- Player Endpoints ---

func TestPlayerCRUD(t *testing.T) {
	r, _ := newTestRouter(t)

	// Create player
	t.Run("create player", func(t *testing.T) {
		rr := doRequest(r, "POST", "/api/players", map[string]string{
			"uid": "test-uid", "name": "TestPlayer",
		})
		if rr.Code != http.StatusCreated {
			t.Errorf("expected 201, got %d: %s", rr.Code, rr.Body.String())
		}
		var player model.Player
		decodeJSON(t, rr, &player)
		if player.UID != "test-uid" {
			t.Errorf("expected UID test-uid, got %s", player.UID)
		}
		if player.Name != "TestPlayer" {
			t.Errorf("expected Name TestPlayer, got %s", player.Name)
		}
	})

	// Create second player
	doRequest(r, "POST", "/api/players", map[string]string{
		"uid": "test-uid-2", "name": "TestPlayer2",
	})

	// List players
	t.Run("list players", func(t *testing.T) {
		rr := doRequest(r, "GET", "/api/players", nil)
		if rr.Code != http.StatusOK {
			t.Errorf("expected 200, got %d", rr.Code)
		}
		var players []model.Player
		decodeJSON(t, rr, &players)
		if len(players) != 2 {
			t.Errorf("expected 2 players, got %d", len(players))
		}
	})

	// Get player by ID
	t.Run("get player by id", func(t *testing.T) {
		rr := doRequest(r, "GET", "/api/players/1", nil)
		if rr.Code != http.StatusOK {
			t.Errorf("expected 200, got %d", rr.Code)
		}
		var player model.Player
		decodeJSON(t, rr, &player)
		if player.UID != "test-uid" {
			t.Errorf("expected UID test-uid, got %s", player.UID)
		}
	})

	// Get non-existing player
	t.Run("get non-existing player", func(t *testing.T) {
		rr := doRequest(r, "GET", "/api/players/999", nil)
		if rr.Code != http.StatusNotFound {
			t.Errorf("expected 404, got %d", rr.Code)
		}
	})

	// Update player
	t.Run("update player", func(t *testing.T) {
		rr := doRequest(r, "PUT", "/api/players/1", map[string]string{
			"name": "UpdatedName",
		})
		if rr.Code != http.StatusOK {
			t.Errorf("expected 200, got %d: %s", rr.Code, rr.Body.String())
		}
		var player model.Player
		decodeJSON(t, rr, &player)
		if player.Name != "UpdatedName" {
			t.Errorf("expected Name UpdatedName, got %s", player.Name)
		}
	})

	// Delete player
	t.Run("delete player", func(t *testing.T) {
		rr := doRequest(r, "DELETE", "/api/players/2", nil)
		if rr.Code != http.StatusOK {
			t.Errorf("expected 200, got %d: %s", rr.Code, rr.Body.String())
		}

		// Verify it's gone
		rr = doRequest(r, "GET", "/api/players/2", nil)
		if rr.Code != http.StatusNotFound {
			t.Errorf("expected 404 after delete, got %d", rr.Code)
		}
	})

	// Invalid body
	t.Run("create player invalid body", func(t *testing.T) {
		req := httptest.NewRequest("POST", "/api/players", bytes.NewBufferString("not json"))
		req.Header.Set("Content-Type", "application/json")
		rr := httptest.NewRecorder()
		r.ServeHTTP(rr, req)
		if rr.Code != http.StatusBadRequest {
			t.Errorf("expected 400, got %d", rr.Code)
		}
	})

	// Create player missing required fields
	t.Run("create player missing uid", func(t *testing.T) {
		rr := doRequest(r, "POST", "/api/players", map[string]string{
			"name": "NoUID",
		})
		if rr.Code != http.StatusBadRequest {
			t.Errorf("expected 400, got %d", rr.Code)
		}
	})
}

// --- Session Endpoints ---

func TestSessionCRUD(t *testing.T) {
	r, _ := newTestRouter(t)

	// Create session
	t.Run("create session", func(t *testing.T) {
		rr := doRequest(r, "POST", "/api/sessions", map[string]interface{}{
			"session_id": "game-1", "max_players": 4,
		})
		if rr.Code != http.StatusCreated {
			t.Errorf("expected 201, got %d: %s", rr.Code, rr.Body.String())
		}
		var session model.GameSession
		decodeJSON(t, rr, &session)
		if session.SessionID != "game-1" {
			t.Errorf("expected SessionID game-1, got %s", session.SessionID)
		}
		if session.Status != "waiting" {
			t.Errorf("expected Status waiting, got %s", session.Status)
		}
	})

	// List sessions
	t.Run("list sessions", func(t *testing.T) {
		rr := doRequest(r, "GET", "/api/sessions", nil)
		if rr.Code != http.StatusOK {
			t.Errorf("expected 200, got %d", rr.Code)
		}
		var sessions []model.GameSession
		decodeJSON(t, rr, &sessions)
		if len(sessions) != 1 {
			t.Errorf("expected 1 session, got %d", len(sessions))
		}
	})

	// Get session
	t.Run("get session", func(t *testing.T) {
		rr := doRequest(r, "GET", "/api/sessions/game-1", nil)
		if rr.Code != http.StatusOK {
			t.Errorf("expected 200, got %d", rr.Code)
		}
	})

	// Get non-existing session
	t.Run("get non-existing session", func(t *testing.T) {
		rr := doRequest(r, "GET", "/api/sessions/nonexistent", nil)
		if rr.Code != http.StatusNotFound {
			t.Errorf("expected 404, got %d", rr.Code)
		}
	})

	// Create player and join session
	doRequest(r, "POST", "/api/players", map[string]string{
		"uid": "player-1", "name": "Player1",
	})

	t.Run("join session", func(t *testing.T) {
		rr := doRequest(r, "POST", "/api/sessions/game-1/join", map[string]interface{}{
			"player_id": 1,
		})
		if rr.Code != http.StatusOK {
			t.Errorf("expected 200, got %d: %s", rr.Code, rr.Body.String())
		}
		var session model.GameSession
		decodeJSON(t, rr, &session)
		if len(session.Players) != 1 {
			t.Errorf("expected 1 player in session, got %d", len(session.Players))
		}
	})

	// Update session status
	t.Run("update session status", func(t *testing.T) {
		rr := doRequest(r, "PUT", "/api/sessions/game-1/status", map[string]string{
			"status": "active",
		})
		if rr.Code != http.StatusOK {
			t.Errorf("expected 200, got %d: %s", rr.Code, rr.Body.String())
		}
		var session model.GameSession
		decodeJSON(t, rr, &session)
		if session.Status != "active" {
			t.Errorf("expected Status active, got %s", session.Status)
		}
	})

	// Delete session
	t.Run("delete session", func(t *testing.T) {
		rr := doRequest(r, "DELETE", "/api/sessions/game-1", nil)
		if rr.Code != http.StatusOK {
			t.Errorf("expected 200, got %d", rr.Code)
		}

		rr = doRequest(r, "GET", "/api/sessions/game-1", nil)
		if rr.Code != http.StatusNotFound {
			t.Errorf("expected 404 after delete, got %d", rr.Code)
		}
	})

	// Create session missing session_id
	t.Run("create session missing id", func(t *testing.T) {
		rr := doRequest(r, "POST", "/api/sessions", map[string]interface{}{
			"max_players": 4,
		})
		if rr.Code != http.StatusBadRequest {
			t.Errorf("expected 400, got %d", rr.Code)
		}
	})
}

// --- Response format tests ---

func TestContentTypeJSON(t *testing.T) {
	r, _ := newTestRouter(t)
	rr := doRequest(r, "GET", "/api/health", nil)

	ct := rr.Header().Get("Content-Type")
	if ct != "application/json" {
		t.Errorf("expected Content-Type application/json, got %s", ct)
	}
}

func TestErrorResponseFormat(t *testing.T) {
	r, _ := newTestRouter(t)
	rr := doRequest(r, "GET", "/api/players/999", nil)

	var resp map[string]string
	decodeJSON(t, rr, &resp)
	if _, ok := resp["error"]; !ok {
		t.Error("expected error field in error response")
	}
}
