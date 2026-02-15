package handler

import (
	"encoding/json"
	"net/http"
	"strconv"

	"github.com/gorilla/mux"
	"github.com/sumner-mccarty/go-gameserver/internal/service"
)

// Handler holds all HTTP handlers and their dependencies.
type Handler struct {
	Players  *service.PlayerService
	Sessions *service.SessionService
}

// New creates a new Handler with the given services.
func New(players *service.PlayerService, sessions *service.SessionService) *Handler {
	return &Handler{
		Players:  players,
		Sessions: sessions,
	}
}

// HealthCheck returns a simple health status.
func (h *Handler) HealthCheck(w http.ResponseWriter, r *http.Request) {
	respondJSON(w, http.StatusOK, map[string]string{"status": "ok"})
}

// --- Player Handlers ---

// CreatePlayer handles POST /api/players
func (h *Handler) CreatePlayer(w http.ResponseWriter, r *http.Request) {
	var req struct {
		UID  string `json:"uid"`
		Name string `json:"name"`
	}
	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		respondError(w, http.StatusBadRequest, "invalid request body")
		return
	}

	player, err := h.Players.Create(req.UID, req.Name)
	if err != nil {
		respondError(w, http.StatusBadRequest, err.Error())
		return
	}
	respondJSON(w, http.StatusCreated, player)
}

// GetPlayer handles GET /api/players/{id}
func (h *Handler) GetPlayer(w http.ResponseWriter, r *http.Request) {
	id, err := parseIDParam(r)
	if err != nil {
		respondError(w, http.StatusBadRequest, "invalid player id")
		return
	}

	player, err := h.Players.GetByID(id)
	if err != nil {
		respondError(w, http.StatusInternalServerError, err.Error())
		return
	}
	if player == nil {
		respondError(w, http.StatusNotFound, "player not found")
		return
	}
	respondJSON(w, http.StatusOK, player)
}

// ListPlayers handles GET /api/players
func (h *Handler) ListPlayers(w http.ResponseWriter, r *http.Request) {
	players, err := h.Players.List()
	if err != nil {
		respondError(w, http.StatusInternalServerError, err.Error())
		return
	}
	respondJSON(w, http.StatusOK, players)
}

// UpdatePlayer handles PUT /api/players/{id}
func (h *Handler) UpdatePlayer(w http.ResponseWriter, r *http.Request) {
	id, err := parseIDParam(r)
	if err != nil {
		respondError(w, http.StatusBadRequest, "invalid player id")
		return
	}

	var req struct {
		Name        string `json:"name"`
		NumSessions *uint  `json:"num_sessions"`
	}
	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		respondError(w, http.StatusBadRequest, "invalid request body")
		return
	}

	player, err := h.Players.Update(id, req.Name, req.NumSessions)
	if err != nil {
		respondError(w, http.StatusInternalServerError, err.Error())
		return
	}
	if player == nil {
		respondError(w, http.StatusNotFound, "player not found")
		return
	}
	respondJSON(w, http.StatusOK, player)
}

// DeletePlayer handles DELETE /api/players/{id}
func (h *Handler) DeletePlayer(w http.ResponseWriter, r *http.Request) {
	id, err := parseIDParam(r)
	if err != nil {
		respondError(w, http.StatusBadRequest, "invalid player id")
		return
	}

	if err := h.Players.Delete(id); err != nil {
		respondError(w, http.StatusNotFound, err.Error())
		return
	}
	respondJSON(w, http.StatusOK, map[string]string{"message": "player deleted"})
}

// --- Session Handlers ---

// CreateSession handles POST /api/sessions
func (h *Handler) CreateSession(w http.ResponseWriter, r *http.Request) {
	var req struct {
		SessionID  string `json:"session_id"`
		MaxPlayers int    `json:"max_players"`
	}
	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		respondError(w, http.StatusBadRequest, "invalid request body")
		return
	}

	session, err := h.Sessions.Create(req.SessionID, req.MaxPlayers)
	if err != nil {
		respondError(w, http.StatusBadRequest, err.Error())
		return
	}
	respondJSON(w, http.StatusCreated, session)
}

// GetSession handles GET /api/sessions/{session_id}
func (h *Handler) GetSession(w http.ResponseWriter, r *http.Request) {
	sessionID := mux.Vars(r)["session_id"]

	session, err := h.Sessions.GetBySessionID(sessionID)
	if err != nil {
		respondError(w, http.StatusInternalServerError, err.Error())
		return
	}
	if session == nil {
		respondError(w, http.StatusNotFound, "session not found")
		return
	}
	respondJSON(w, http.StatusOK, session)
}

// ListSessions handles GET /api/sessions
func (h *Handler) ListSessions(w http.ResponseWriter, r *http.Request) {
	sessions, err := h.Sessions.List()
	if err != nil {
		respondError(w, http.StatusInternalServerError, err.Error())
		return
	}
	respondJSON(w, http.StatusOK, sessions)
}

// JoinSession handles POST /api/sessions/{session_id}/join
func (h *Handler) JoinSession(w http.ResponseWriter, r *http.Request) {
	sessionID := mux.Vars(r)["session_id"]

	var req struct {
		PlayerID uint `json:"player_id"`
	}
	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		respondError(w, http.StatusBadRequest, "invalid request body")
		return
	}

	session, err := h.Sessions.JoinSession(sessionID, req.PlayerID)
	if err != nil {
		respondError(w, http.StatusBadRequest, err.Error())
		return
	}
	respondJSON(w, http.StatusOK, session)
}

// UpdateSessionStatus handles PUT /api/sessions/{session_id}/status
func (h *Handler) UpdateSessionStatus(w http.ResponseWriter, r *http.Request) {
	sessionID := mux.Vars(r)["session_id"]

	var req struct {
		Status string `json:"status"`
	}
	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		respondError(w, http.StatusBadRequest, "invalid request body")
		return
	}

	session, err := h.Sessions.UpdateStatus(sessionID, req.Status)
	if err != nil {
		respondError(w, http.StatusBadRequest, err.Error())
		return
	}
	respondJSON(w, http.StatusOK, session)
}

// DeleteSession handles DELETE /api/sessions/{session_id}
func (h *Handler) DeleteSession(w http.ResponseWriter, r *http.Request) {
	sessionID := mux.Vars(r)["session_id"]

	if err := h.Sessions.Delete(sessionID); err != nil {
		respondError(w, http.StatusNotFound, err.Error())
		return
	}
	respondJSON(w, http.StatusOK, map[string]string{"message": "session deleted"})
}

// --- Helpers ---

func parseIDParam(r *http.Request) (uint, error) {
	idStr := mux.Vars(r)["id"]
	id, err := strconv.ParseUint(idStr, 10, 32)
	if err != nil {
		return 0, err
	}
	return uint(id), nil
}

func respondJSON(w http.ResponseWriter, status int, data interface{}) {
	w.WriteHeader(status)
	if err := json.NewEncoder(w).Encode(data); err != nil {
		http.Error(w, err.Error(), http.StatusInternalServerError)
	}
}

func respondError(w http.ResponseWriter, status int, message string) {
	respondJSON(w, status, map[string]string{"error": message})
}
