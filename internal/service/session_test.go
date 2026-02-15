package service

import (
	"testing"
)

// --- Session Service Tests ---

func TestSessionService_Create(t *testing.T) {
	db := newTestDB(t)
	svc := NewSessionService(db)

	t.Run("valid session", func(t *testing.T) {
		s, err := svc.Create("sess-1", 4)
		if err != nil {
			t.Fatalf("unexpected error: %v", err)
		}
		if s.SessionID != "sess-1" {
			t.Errorf("expected SessionID sess-1, got %s", s.SessionID)
		}
		if s.Status != "waiting" {
			t.Errorf("expected Status waiting, got %s", s.Status)
		}
		if s.MaxPlayers != 4 {
			t.Errorf("expected MaxPlayers 4, got %d", s.MaxPlayers)
		}
	})

	t.Run("default max players", func(t *testing.T) {
		s, err := svc.Create("sess-default", 0)
		if err != nil {
			t.Fatalf("unexpected error: %v", err)
		}
		if s.MaxPlayers != 4 {
			t.Errorf("expected default MaxPlayers 4, got %d", s.MaxPlayers)
		}
	})

	t.Run("empty session_id", func(t *testing.T) {
		_, err := svc.Create("", 4)
		if err == nil {
			t.Error("expected error for empty session_id")
		}
	})

	t.Run("duplicate session_id", func(t *testing.T) {
		_, err := svc.Create("sess-1", 4)
		if err == nil {
			t.Error("expected error for duplicate session_id")
		}
	})
}

func TestSessionService_GetBySessionID(t *testing.T) {
	db := newTestDB(t)
	svc := NewSessionService(db)

	svc.Create("sess-get", 4)

	t.Run("existing session", func(t *testing.T) {
		s, err := svc.GetBySessionID("sess-get")
		if err != nil {
			t.Fatalf("unexpected error: %v", err)
		}
		if s == nil {
			t.Fatal("expected session, got nil")
		}
		if s.SessionID != "sess-get" {
			t.Errorf("expected SessionID sess-get, got %s", s.SessionID)
		}
	})

	t.Run("non-existing session", func(t *testing.T) {
		s, err := svc.GetBySessionID("nonexistent")
		if err != nil {
			t.Fatalf("unexpected error: %v", err)
		}
		if s != nil {
			t.Errorf("expected nil for non-existing session, got %+v", s)
		}
	})
}

func TestSessionService_List(t *testing.T) {
	db := newTestDB(t)
	svc := NewSessionService(db)

	t.Run("empty list", func(t *testing.T) {
		sessions, err := svc.List()
		if err != nil {
			t.Fatalf("unexpected error: %v", err)
		}
		if len(sessions) != 0 {
			t.Errorf("expected 0 sessions, got %d", len(sessions))
		}
	})

	svc.Create("sess-list-1", 2)
	svc.Create("sess-list-2", 4)

	t.Run("with sessions", func(t *testing.T) {
		sessions, err := svc.List()
		if err != nil {
			t.Fatalf("unexpected error: %v", err)
		}
		if len(sessions) != 2 {
			t.Errorf("expected 2 sessions, got %d", len(sessions))
		}
	})
}

func TestSessionService_JoinSession(t *testing.T) {
	db := newTestDB(t)
	sessionSvc := NewSessionService(db)
	playerSvc := NewPlayerService(db)

	sessionSvc.Create("sess-join", 2)
	p1, _ := playerSvc.Create("p1", "Player1")
	p2, _ := playerSvc.Create("p2", "Player2")
	p3, _ := playerSvc.Create("p3", "Player3")

	t.Run("join first player", func(t *testing.T) {
		s, err := sessionSvc.JoinSession("sess-join", p1.ID)
		if err != nil {
			t.Fatalf("unexpected error: %v", err)
		}
		if len(s.Players) != 1 {
			t.Errorf("expected 1 player in session, got %d", len(s.Players))
		}
	})

	t.Run("join second player", func(t *testing.T) {
		s, err := sessionSvc.JoinSession("sess-join", p2.ID)
		if err != nil {
			t.Fatalf("unexpected error: %v", err)
		}
		if len(s.Players) != 2 {
			t.Errorf("expected 2 players in session, got %d", len(s.Players))
		}
	})

	t.Run("session full", func(t *testing.T) {
		_, err := sessionSvc.JoinSession("sess-join", p3.ID)
		if err == nil {
			t.Error("expected error for full session")
		}
	})

	t.Run("player already in session", func(t *testing.T) {
		// Create a new session with room
		sessionSvc.Create("sess-join-dup", 4)
		sessionSvc.JoinSession("sess-join-dup", p1.ID)

		_, err := sessionSvc.JoinSession("sess-join-dup", p1.ID)
		if err == nil {
			t.Error("expected error for duplicate player in session")
		}
	})

	t.Run("non-existing session", func(t *testing.T) {
		_, err := sessionSvc.JoinSession("nonexistent", p1.ID)
		if err == nil {
			t.Error("expected error for non-existing session")
		}
	})

	t.Run("non-existing player", func(t *testing.T) {
		sessionSvc.Create("sess-join-noplayer", 4)
		_, err := sessionSvc.JoinSession("sess-join-noplayer", 99999)
		if err == nil {
			t.Error("expected error for non-existing player")
		}
	})

	t.Run("session not waiting", func(t *testing.T) {
		sessionSvc.Create("sess-join-active", 4)
		sessionSvc.UpdateStatus("sess-join-active", "active")
		_, err := sessionSvc.JoinSession("sess-join-active", p1.ID)
		if err == nil {
			t.Error("expected error for non-waiting session")
		}
	})

	t.Run("player session count incremented", func(t *testing.T) {
		p, err := playerSvc.GetByID(p1.ID)
		if err != nil {
			t.Fatalf("unexpected error: %v", err)
		}
		if p.NumSessions < 1 {
			t.Errorf("expected NumSessions >= 1 after joining, got %d", p.NumSessions)
		}
	})
}

func TestSessionService_UpdateStatus(t *testing.T) {
	db := newTestDB(t)
	svc := NewSessionService(db)

	svc.Create("sess-status", 4)

	t.Run("to active", func(t *testing.T) {
		s, err := svc.UpdateStatus("sess-status", "active")
		if err != nil {
			t.Fatalf("unexpected error: %v", err)
		}
		if s.Status != "active" {
			t.Errorf("expected Status active, got %s", s.Status)
		}
	})

	t.Run("to finished", func(t *testing.T) {
		s, err := svc.UpdateStatus("sess-status", "finished")
		if err != nil {
			t.Fatalf("unexpected error: %v", err)
		}
		if s.Status != "finished" {
			t.Errorf("expected Status finished, got %s", s.Status)
		}
	})

	t.Run("invalid status", func(t *testing.T) {
		_, err := svc.UpdateStatus("sess-status", "invalid")
		if err == nil {
			t.Error("expected error for invalid status")
		}
	})

	t.Run("non-existing session", func(t *testing.T) {
		_, err := svc.UpdateStatus("nonexistent", "active")
		if err == nil {
			t.Error("expected error for non-existing session")
		}
	})
}

func TestSessionService_Delete(t *testing.T) {
	db := newTestDB(t)
	svc := NewSessionService(db)

	svc.Create("sess-delete", 4)

	t.Run("existing session", func(t *testing.T) {
		err := svc.Delete("sess-delete")
		if err != nil {
			t.Fatalf("unexpected error: %v", err)
		}

		s, err := svc.GetBySessionID("sess-delete")
		if err != nil {
			t.Fatalf("unexpected error: %v", err)
		}
		if s != nil {
			t.Error("expected session to be soft-deleted")
		}
	})

	t.Run("non-existing session", func(t *testing.T) {
		err := svc.Delete("nonexistent")
		if err == nil {
			t.Error("expected error for non-existing session")
		}
	})
}
