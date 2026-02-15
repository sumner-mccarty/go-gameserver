package handler

import (
	"github.com/gorilla/mux"
	"github.com/sumner-mccarty/go-gameserver/internal/middleware"
)

// RegisterRoutes sets up all API routes on the given router.
func RegisterRoutes(r *mux.Router, h *Handler) {
	api := r.PathPrefix("/api").Subrouter()
	api.Use(middleware.JSON)

	// Health
	api.HandleFunc("/health", h.HealthCheck).Methods("GET")

	// Players
	api.HandleFunc("/players", h.ListPlayers).Methods("GET")
	api.HandleFunc("/players", h.CreatePlayer).Methods("POST")
	api.HandleFunc("/players/{id:[0-9]+}", h.GetPlayer).Methods("GET")
	api.HandleFunc("/players/{id:[0-9]+}", h.UpdatePlayer).Methods("PUT")
	api.HandleFunc("/players/{id:[0-9]+}", h.DeletePlayer).Methods("DELETE")

	// Sessions
	api.HandleFunc("/sessions", h.ListSessions).Methods("GET")
	api.HandleFunc("/sessions", h.CreateSession).Methods("POST")
	api.HandleFunc("/sessions/{session_id}", h.GetSession).Methods("GET")
	api.HandleFunc("/sessions/{session_id}/join", h.JoinSession).Methods("POST")
	api.HandleFunc("/sessions/{session_id}/status", h.UpdateSessionStatus).Methods("PUT")
	api.HandleFunc("/sessions/{session_id}", h.DeleteSession).Methods("DELETE")
}
