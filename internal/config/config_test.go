package config

import (
	"os"
	"testing"
)

func TestLoadDefaults(t *testing.T) {
	// Clear any env vars that might be set
	os.Unsetenv("SERVER_HOST")
	os.Unsetenv("SERVER_PORT")
	os.Unsetenv("DB_DRIVER")
	os.Unsetenv("DB_DSN")

	cfg := Load()

	if cfg.Server.Host != "0.0.0.0" {
		t.Errorf("expected default host 0.0.0.0, got %s", cfg.Server.Host)
	}
	if cfg.Server.Port != 3000 {
		t.Errorf("expected default port 3000, got %d", cfg.Server.Port)
	}
	if cfg.Database.Driver != "sqlite" {
		t.Errorf("expected default driver sqlite, got %s", cfg.Database.Driver)
	}
	if cfg.Database.DSN != "gameserver.db" {
		t.Errorf("expected default DSN gameserver.db, got %s", cfg.Database.DSN)
	}
}

func TestLoadFromEnv(t *testing.T) {
	os.Setenv("SERVER_HOST", "127.0.0.1")
	os.Setenv("SERVER_PORT", "8080")
	os.Setenv("DB_DRIVER", "postgres")
	os.Setenv("DB_DSN", "host=localhost dbname=test")
	defer func() {
		os.Unsetenv("SERVER_HOST")
		os.Unsetenv("SERVER_PORT")
		os.Unsetenv("DB_DRIVER")
		os.Unsetenv("DB_DSN")
	}()

	cfg := Load()

	if cfg.Server.Host != "127.0.0.1" {
		t.Errorf("expected host 127.0.0.1, got %s", cfg.Server.Host)
	}
	if cfg.Server.Port != 8080 {
		t.Errorf("expected port 8080, got %d", cfg.Server.Port)
	}
	if cfg.Database.Driver != "postgres" {
		t.Errorf("expected driver postgres, got %s", cfg.Database.Driver)
	}
	if cfg.Database.DSN != "host=localhost dbname=test" {
		t.Errorf("expected DSN 'host=localhost dbname=test', got %s", cfg.Database.DSN)
	}
}

func TestLoadInvalidPort(t *testing.T) {
	os.Setenv("SERVER_PORT", "not_a_number")
	defer os.Unsetenv("SERVER_PORT")

	cfg := Load()

	if cfg.Server.Port != 3000 {
		t.Errorf("expected fallback port 3000 for invalid env, got %d", cfg.Server.Port)
	}
}

func TestServerConfigAddr(t *testing.T) {
	tests := []struct {
		name string
		host string
		port int
		want string
	}{
		{"default", "0.0.0.0", 3000, "0.0.0.0:3000"},
		{"localhost", "127.0.0.1", 8080, "127.0.0.1:8080"},
		{"custom", "192.168.1.1", 9090, "192.168.1.1:9090"},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			s := ServerConfig{Host: tt.host, Port: tt.port}
			got := s.Addr()
			if got != tt.want {
				t.Errorf("Addr() = %s, want %s", got, tt.want)
			}
		})
	}
}
