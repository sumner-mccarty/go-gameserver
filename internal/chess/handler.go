package chess

import (
	"encoding/json"
	"net/http"
	"strconv"

	"github.com/gorilla/mux"
)

// Handler holds chess HTTP handlers.
type Handler struct {
	service *Service
}

// NewHandler creates a new chess handler.
func NewHandler(service *Service) *Handler {
	return &Handler{service: service}
}

// CreateGame handles POST /api/chess
func (h *Handler) CreateGame(w http.ResponseWriter, r *http.Request) {
	var req struct {
		SessionID int `json:"session_id"`
		WhiteID   int `json:"white_id"`
		BlackID   int `json:"black_id"`
	}
	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		respondError(w, http.StatusBadRequest, "invalid request body")
		return
	}

	game, err := h.service.CreateGame(req.SessionID, req.WhiteID, req.BlackID)
	if err != nil {
		respondError(w, http.StatusBadRequest, err.Error())
		return
	}

	respondJSON(w, http.StatusCreated, game.ToResponse())
}

// GetGame handles GET /api/chess/{id}
func (h *Handler) GetGame(w http.ResponseWriter, r *http.Request) {
	id, err := strconv.Atoi(mux.Vars(r)["id"])
	if err != nil {
		respondError(w, http.StatusBadRequest, "invalid game id")
		return
	}

	game, err := h.service.GetGame(id)
	if err != nil {
		respondError(w, http.StatusInternalServerError, err.Error())
		return
	}
	if game == nil {
		respondError(w, http.StatusNotFound, "game not found")
		return
	}

	respondJSON(w, http.StatusOK, game.ToResponse())
}

// MakeMove handles POST /api/chess/{id}/move
func (h *Handler) MakeMove(w http.ResponseWriter, r *http.Request) {
	id, err := strconv.Atoi(mux.Vars(r)["id"])
	if err != nil {
		respondError(w, http.StatusBadRequest, "invalid game id")
		return
	}

	var req struct {
		PlayerID int    `json:"player_id"`
		From     string `json:"from"`
		To       string `json:"to"`
	}
	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		respondError(w, http.StatusBadRequest, "invalid request body")
		return
	}

	game, err := h.service.MakeMove(id, req.PlayerID, req.From, req.To)
	if err != nil {
		respondError(w, http.StatusBadRequest, err.Error())
		return
	}

	respondJSON(w, http.StatusOK, game.ToResponse())
}

// Resign handles POST /api/chess/{id}/resign
func (h *Handler) Resign(w http.ResponseWriter, r *http.Request) {
	id, err := strconv.Atoi(mux.Vars(r)["id"])
	if err != nil {
		respondError(w, http.StatusBadRequest, "invalid game id")
		return
	}

	var req struct {
		PlayerID int `json:"player_id"`
	}
	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		respondError(w, http.StatusBadRequest, "invalid request body")
		return
	}

	game, err := h.service.Resign(id, req.PlayerID)
	if err != nil {
		respondError(w, http.StatusBadRequest, err.Error())
		return
	}

	respondJSON(w, http.StatusOK, game.ToResponse())
}

// RegisterRoutes adds chess routes to the router.
func (h *Handler) RegisterRoutes(r *mux.Router) {
	r.HandleFunc("/api/chess", h.CreateGame).Methods("POST")
	r.HandleFunc("/api/chess/{id:[0-9]+}", h.GetGame).Methods("GET")
	r.HandleFunc("/api/chess/{id:[0-9]+}/move", h.MakeMove).Methods("POST")
	r.HandleFunc("/api/chess/{id:[0-9]+}/resign", h.Resign).Methods("POST")
}

// --- JSON helpers (local to chess package to avoid circular imports) ---

func respondJSON(w http.ResponseWriter, status int, data interface{}) {
	w.Header().Set("Content-Type", "application/json")
	w.WriteHeader(status)
	json.NewEncoder(w).Encode(data)
}

func respondError(w http.ResponseWriter, status int, message string) {
	respondJSON(w, status, map[string]string{"error": message})
}
