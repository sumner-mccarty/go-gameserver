package main

import (
	"log"
	"net/http"

	"github.com/gorilla/mux"

	"github.com/sumner-mccarty/go-gameserver/internal/battler"
	"github.com/sumner-mccarty/go-gameserver/internal/chess"
	"github.com/sumner-mccarty/go-gameserver/internal/config"
	"github.com/sumner-mccarty/go-gameserver/internal/handler"
	"github.com/sumner-mccarty/go-gameserver/internal/middleware"
	"github.com/sumner-mccarty/go-gameserver/internal/service"
	"github.com/sumner-mccarty/go-gameserver/internal/store"
)

func main() {
	cfg := config.Load()

	db, err := store.New(cfg.Database)
	if err != nil {
		log.Fatalf("failed to initialize database: %v", err)
	}

	playerService := service.NewPlayerService(db)
	sessionService := service.NewSessionService(db)

	h := handler.New(playerService, sessionService)

	r := mux.NewRouter()
	r.Use(middleware.Logger)
	handler.RegisterRoutes(r, h)

	// Chess game server
	chessService := chess.NewService()
	chessHandler := chess.NewHandler(chessService)
	chessHandler.RegisterRoutes(r)

	// Battler game server (real-time simulation with WebSocket)
	battleManager := battler.NewManager()
	battleHandler := battler.NewHandler(battleManager)
	battleHandler.RegisterRoutes(r)

	addr := cfg.Server.Addr()
	log.Printf("starting gameserver on %s (db: %s)", addr, cfg.Database.Driver)
	log.Printf("  chess:   POST /api/chess, GET /api/chess/{id}, POST /api/chess/{id}/move, POST /api/chess/{id}/resign")
	log.Printf("  battler: POST /api/battle/start, GET /api/battle/types, WS /ws/battle/{id}")
	if err := http.ListenAndServe(addr, r); err != nil {
		log.Fatalf("server error: %v", err)
	}
}
