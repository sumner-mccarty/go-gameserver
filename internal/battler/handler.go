package battler

import (
	"encoding/json"
	"log"
	"net/http"
	"os"
	"strconv"
	"strings"

	"github.com/gorilla/mux"
	"github.com/gorilla/websocket"
)

// Handler holds battler HTTP and WebSocket handlers.
type Handler struct {
	manager  *Manager
	upgrader websocket.Upgrader
}

// NewHandler creates a new battler handler.
// Set the WS_ALLOWED_ORIGINS environment variable to a comma-separated list of
// allowed origins (e.g. "http://localhost:3000,https://mygame.com").
// If not set, all origins are allowed (suitable for development only).
func NewHandler(manager *Manager) *Handler {
	allowedOrigins := os.Getenv("WS_ALLOWED_ORIGINS")
	return &Handler{
		manager: manager,
		upgrader: websocket.Upgrader{
			CheckOrigin: makeOriginChecker(allowedOrigins),
		},
	}
}

// makeOriginChecker returns a function that validates WebSocket origins.
func makeOriginChecker(allowedOrigins string) func(r *http.Request) bool {
	if allowedOrigins == "" {
		return func(r *http.Request) bool { return true }
	}
	origins := map[string]bool{}
	for _, o := range strings.Split(allowedOrigins, ",") {
		origins[strings.TrimSpace(o)] = true
	}
	return func(r *http.Request) bool {
		origin := r.Header.Get("Origin")
		return origins[origin]
	}
}

// StartBattleRequest is the JSON body for POST /api/battle/start.
type StartBattleRequest struct {
	SessionID int             `json:"session_id"`
	Player1ID int             `json:"player1_id"`
	Player2ID int             `json:"player2_id"`
	Army1     []ArmyPlacement `json:"army1"`
	Army2     []ArmyPlacement `json:"army2"`
}

// StartBattle handles POST /api/battle/start.
func (h *Handler) StartBattle(w http.ResponseWriter, r *http.Request) {
	var req StartBattleRequest
	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		respondError(w, http.StatusBadRequest, "invalid request body")
		return
	}

	if req.Player1ID == 0 || req.Player2ID == 0 {
		respondError(w, http.StatusBadRequest, "player1_id and player2_id are required")
		return
	}
	if len(req.Army1) == 0 || len(req.Army2) == 0 {
		respondError(w, http.StatusBadRequest, "both armies must have at least one unit")
		return
	}

	battle, err := h.manager.StartBattle(req.SessionID, req.Player1ID, req.Player2ID, req.Army1, req.Army2)
	if err != nil {
		respondError(w, http.StatusBadRequest, err.Error())
		return
	}

	totalUnits := len(req.Army1) + len(req.Army2)
	respondJSON(w, http.StatusCreated, map[string]interface{}{
		"message":    "battle started",
		"session_id": battle.ID,
		"unit_count": totalUnits,
	})
}

// GetUnitTypes handles GET /api/battle/types.
func (h *Handler) GetUnitTypes(w http.ResponseWriter, r *http.Request) {
	respondJSON(w, http.StatusOK, DefaultUnitTypes())
}

// BattleWebSocket handles GET /ws/battle/{id} — upgrades to WebSocket for state streaming.
func (h *Handler) BattleWebSocket(w http.ResponseWriter, r *http.Request) {
	idStr := mux.Vars(r)["id"]
	id, err := strconv.Atoi(idStr)
	if err != nil {
		http.Error(w, "invalid battle id", http.StatusBadRequest)
		return
	}

	battle := h.manager.GetBattle(id)
	if battle == nil {
		http.Error(w, "battle not found", http.StatusNotFound)
		return
	}

	conn, err := h.upgrader.Upgrade(w, r, nil)
	if err != nil {
		log.Printf("[battler] websocket upgrade error: %v", err)
		return
	}

	battle.AddClient(conn)
	log.Printf("[battle %d] websocket client connected", id)

	// Keep the connection alive — read loop to detect disconnects
	go func() {
		defer func() {
			battle.RemoveClient(conn)
			conn.Close()
			log.Printf("[battle %d] websocket client disconnected", id)
		}()
		for {
			_, _, err := conn.ReadMessage()
			if err != nil {
				return
			}
		}
	}()
}

// RegisterRoutes adds battler routes to the router.
func (h *Handler) RegisterRoutes(r *mux.Router) {
	r.HandleFunc("/api/battle/start", h.StartBattle).Methods("POST")
	r.HandleFunc("/api/battle/types", h.GetUnitTypes).Methods("GET")
	r.HandleFunc("/ws/battle/{id:[0-9]+}", h.BattleWebSocket).Methods("GET")
}

// --- JSON helpers ---

func respondJSON(w http.ResponseWriter, status int, data interface{}) {
	w.Header().Set("Content-Type", "application/json")
	w.WriteHeader(status)
	json.NewEncoder(w).Encode(data)
}

func respondError(w http.ResponseWriter, status int, message string) {
	respondJSON(w, status, map[string]string{"error": message})
}
