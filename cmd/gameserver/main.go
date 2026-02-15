package main

import (
	"log"
	"net/http"

	"github.com/gorilla/mux"

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

	addr := cfg.Server.Addr()
	log.Printf("starting gameserver on %s (db: %s)", addr, cfg.Database.Driver)
	if err := http.ListenAndServe(addr, r); err != nil {
		log.Fatalf("server error: %v", err)
	}
}
