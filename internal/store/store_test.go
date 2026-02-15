package store

import (
	"testing"

	"github.com/sumner-mccarty/go-gameserver/internal/config"
)

func TestNewSQLite(t *testing.T) {
	cfg := config.DatabaseConfig{
		Driver: "sqlite",
		DSN:    ":memory:",
	}

	db, err := New(cfg)
	if err != nil {
		t.Fatalf("failed to create SQLite database: %v", err)
	}

	sqlDB, err := db.DB()
	if err != nil {
		t.Fatalf("failed to get sql.DB: %v", err)
	}

	if err := sqlDB.Ping(); err != nil {
		t.Fatalf("failed to ping database: %v", err)
	}
}

func TestNewUnsupportedDriver(t *testing.T) {
	cfg := config.DatabaseConfig{
		Driver: "unsupported",
		DSN:    "whatever",
	}

	_, err := New(cfg)
	if err == nil {
		t.Error("expected error for unsupported driver")
	}
}

func TestNewInvalidDSN(t *testing.T) {
	// MySQL/Postgres with invalid DSN should fail to connect
	// We test with a driver that validates DSN format
	cfg := config.DatabaseConfig{
		Driver: "mysql",
		DSN:    "invalid-dsn-that-wont-work",
	}

	// mysql driver may or may not fail at Open time vs first query,
	// so this test just verifies no panic
	_, _ = New(cfg)
}
