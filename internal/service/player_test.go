package service

import (
	"testing"

	"gorm.io/driver/sqlite"
	"gorm.io/gorm"
	"gorm.io/gorm/logger"

	"github.com/sumner-mccarty/go-gameserver/internal/model"
)

// newTestDB creates an in-memory SQLite database for testing.
func newTestDB(t *testing.T) *gorm.DB {
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
	return db
}

// --- Player Service Tests ---

func TestPlayerService_Create(t *testing.T) {
	db := newTestDB(t)
	svc := NewPlayerService(db)

	t.Run("valid player", func(t *testing.T) {
		p, err := svc.Create("uid-1", "Alice")
		if err != nil {
			t.Fatalf("unexpected error: %v", err)
		}
		if p.UID != "uid-1" {
			t.Errorf("expected UID uid-1, got %s", p.UID)
		}
		if p.Name != "Alice" {
			t.Errorf("expected Name Alice, got %s", p.Name)
		}
		if p.ID == 0 {
			t.Error("expected non-zero ID after creation")
		}
	})

	t.Run("empty uid", func(t *testing.T) {
		_, err := svc.Create("", "Alice")
		if err == nil {
			t.Error("expected error for empty uid")
		}
	})

	t.Run("empty name", func(t *testing.T) {
		_, err := svc.Create("uid-2", "")
		if err == nil {
			t.Error("expected error for empty name")
		}
	})

	t.Run("duplicate uid", func(t *testing.T) {
		_, err := svc.Create("uid-1", "Bob")
		if err == nil {
			t.Error("expected error for duplicate uid")
		}
	})
}

func TestPlayerService_GetByUID(t *testing.T) {
	db := newTestDB(t)
	svc := NewPlayerService(db)

	svc.Create("uid-get", "GetTest")

	t.Run("existing player", func(t *testing.T) {
		p, err := svc.GetByUID("uid-get")
		if err != nil {
			t.Fatalf("unexpected error: %v", err)
		}
		if p == nil {
			t.Fatal("expected player, got nil")
		}
		if p.Name != "GetTest" {
			t.Errorf("expected Name GetTest, got %s", p.Name)
		}
	})

	t.Run("non-existing player", func(t *testing.T) {
		p, err := svc.GetByUID("nonexistent")
		if err != nil {
			t.Fatalf("unexpected error: %v", err)
		}
		if p != nil {
			t.Errorf("expected nil for non-existing player, got %+v", p)
		}
	})
}

func TestPlayerService_GetByID(t *testing.T) {
	db := newTestDB(t)
	svc := NewPlayerService(db)

	created, _ := svc.Create("uid-id", "IDTest")

	t.Run("existing player", func(t *testing.T) {
		p, err := svc.GetByID(created.ID)
		if err != nil {
			t.Fatalf("unexpected error: %v", err)
		}
		if p == nil {
			t.Fatal("expected player, got nil")
		}
		if p.UID != "uid-id" {
			t.Errorf("expected UID uid-id, got %s", p.UID)
		}
	})

	t.Run("non-existing id", func(t *testing.T) {
		p, err := svc.GetByID(99999)
		if err != nil {
			t.Fatalf("unexpected error: %v", err)
		}
		if p != nil {
			t.Errorf("expected nil for non-existing ID, got %+v", p)
		}
	})
}

func TestPlayerService_List(t *testing.T) {
	db := newTestDB(t)
	svc := NewPlayerService(db)

	t.Run("empty list", func(t *testing.T) {
		players, err := svc.List()
		if err != nil {
			t.Fatalf("unexpected error: %v", err)
		}
		if len(players) != 0 {
			t.Errorf("expected 0 players, got %d", len(players))
		}
	})

	svc.Create("uid-list-1", "Player1")
	svc.Create("uid-list-2", "Player2")
	svc.Create("uid-list-3", "Player3")

	t.Run("with players", func(t *testing.T) {
		players, err := svc.List()
		if err != nil {
			t.Fatalf("unexpected error: %v", err)
		}
		if len(players) != 3 {
			t.Errorf("expected 3 players, got %d", len(players))
		}
	})
}

func TestPlayerService_Update(t *testing.T) {
	db := newTestDB(t)
	svc := NewPlayerService(db)

	created, _ := svc.Create("uid-update", "Original")

	t.Run("update name", func(t *testing.T) {
		p, err := svc.Update(created.ID, "Updated", nil)
		if err != nil {
			t.Fatalf("unexpected error: %v", err)
		}
		if p.Name != "Updated" {
			t.Errorf("expected Name Updated, got %s", p.Name)
		}
	})

	t.Run("update num_sessions", func(t *testing.T) {
		sessions := uint(5)
		p, err := svc.Update(created.ID, "", &sessions)
		if err != nil {
			t.Fatalf("unexpected error: %v", err)
		}
		if p.NumSessions != 5 {
			t.Errorf("expected NumSessions 5, got %d", p.NumSessions)
		}
	})

	t.Run("non-existing player", func(t *testing.T) {
		p, err := svc.Update(99999, "Nope", nil)
		if err != nil {
			t.Fatalf("unexpected error: %v", err)
		}
		if p != nil {
			t.Errorf("expected nil for non-existing player, got %+v", p)
		}
	})
}

func TestPlayerService_Delete(t *testing.T) {
	db := newTestDB(t)
	svc := NewPlayerService(db)

	created, _ := svc.Create("uid-delete", "DeleteMe")

	t.Run("existing player", func(t *testing.T) {
		err := svc.Delete(created.ID)
		if err != nil {
			t.Fatalf("unexpected error: %v", err)
		}

		// Verify soft-deleted
		p, err := svc.GetByID(created.ID)
		if err != nil {
			t.Fatalf("unexpected error: %v", err)
		}
		if p != nil {
			t.Error("expected player to be soft-deleted")
		}
	})

	t.Run("non-existing player", func(t *testing.T) {
		err := svc.Delete(99999)
		if err == nil {
			t.Error("expected error for non-existing player")
		}
	})
}
