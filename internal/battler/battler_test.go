package battler

import (
	"bytes"
	"encoding/json"
	"net/http"
	"net/http/httptest"
	"strings"
	"testing"
	"time"

	"github.com/gorilla/mux"
	"github.com/gorilla/websocket"
)

// ─────────────────────── Unit Type Tests ───────────────────────

func TestDefaultUnitTypes(t *testing.T) {
	types := DefaultUnitTypes()
	if len(types) != 6 {
		t.Fatalf("expected 6 unit types, got %d", len(types))
	}

	names := map[string]bool{}
	for _, ut := range types {
		names[ut.Name] = true
		if ut.Health <= 0 {
			t.Errorf("unit %s has invalid health: %d", ut.Name, ut.Health)
		}
		if ut.Cost <= 0 {
			t.Errorf("unit %s has invalid cost: %d", ut.Name, ut.Cost)
		}
	}

	expected := []string{"crawler", "archer", "tank", "artillery", "scout", "healer"}
	for _, name := range expected {
		if !names[name] {
			t.Errorf("missing unit type: %s", name)
		}
	}
}

func TestGetUnitType(t *testing.T) {
	t.Run("existing type", func(t *testing.T) {
		ut := GetUnitType("crawler")
		if ut == nil {
			t.Fatal("expected crawler, got nil")
		}
		if ut.Health != 50 {
			t.Errorf("expected health 50, got %d", ut.Health)
		}
	})

	t.Run("non-existing type", func(t *testing.T) {
		ut := GetUnitType("nonexistent")
		if ut != nil {
			t.Errorf("expected nil for nonexistent type, got %+v", ut)
		}
	})
}

func TestDistance(t *testing.T) {
	tests := []struct {
		name string
		x1, y1, x2, y2 float64
		want float64
	}{
		{"same point", 0, 0, 0, 0, 0},
		{"horizontal", 0, 0, 3, 0, 3},
		{"vertical", 0, 0, 0, 4, 4},
		{"diagonal 3-4-5", 0, 0, 3, 4, 5},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			got := Distance(tt.x1, tt.y1, tt.x2, tt.y2)
			if got != tt.want {
				t.Errorf("Distance(%v,%v,%v,%v) = %v, want %v",
					tt.x1, tt.y1, tt.x2, tt.y2, got, tt.want)
			}
		})
	}
}

// ─────────────────────── Simulation Tests ───────────────────────

func TestNewSimulation(t *testing.T) {
	army1 := []ArmyPlacement{{TypeName: "crawler", X: 0, Y: 0}}
	army2 := []ArmyPlacement{{TypeName: "crawler", X: 10, Y: 0}}

	sim, err := NewSimulation(1, 2, army1, army2)
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if len(sim.Units) != 2 {
		t.Errorf("expected 2 units, got %d", len(sim.Units))
	}
	if sim.Status != "running" {
		t.Errorf("expected status running, got %s", sim.Status)
	}
}

func TestNewSimulationInvalidUnit(t *testing.T) {
	army1 := []ArmyPlacement{{TypeName: "nonexistent", X: 0, Y: 0}}
	army2 := []ArmyPlacement{{TypeName: "crawler", X: 10, Y: 0}}

	_, err := NewSimulation(1, 2, army1, army2)
	if err == nil {
		t.Error("expected error for unknown unit type")
	}
}

func TestSimulationCrawlerVsCrawler(t *testing.T) {
	// Two crawlers facing each other — they should move toward each other and fight
	army1 := []ArmyPlacement{{TypeName: "crawler", X: 0, Y: 0}}
	army2 := []ArmyPlacement{{TypeName: "crawler", X: 5, Y: 0}}

	sim, _ := NewSimulation(1, 2, army1, army2)

	// Run for a few hundred ticks — battle should finish
	for i := 0; i < 500; i++ {
		sim.Step()
		if sim.Status == "finished" {
			break
		}
	}

	if sim.Status != "finished" {
		t.Error("battle should have finished after 500 ticks")
	}
	if sim.WinnerID == 0 {
		t.Error("expected a winner")
	}
}

func TestSimulationTankVsScout(t *testing.T) {
	// Tank (200hp, 40dmg) vs Scout (20hp, 5dmg) — tank should win
	army1 := []ArmyPlacement{{TypeName: "tank", X: 0, Y: 0}}
	army2 := []ArmyPlacement{{TypeName: "scout", X: 5, Y: 0}}

	sim, _ := NewSimulation(1, 2, army1, army2)

	for i := 0; i < 500; i++ {
		sim.Step()
		if sim.Status == "finished" {
			break
		}
	}

	if sim.WinnerID != 1 {
		t.Errorf("expected player 1 (tank) to win, got winner %d", sim.WinnerID)
	}
}

func TestSimulationArcherShoots(t *testing.T) {
	// Archer (ranged) should spawn projectiles when in range
	army1 := []ArmyPlacement{{TypeName: "archer", X: 0, Y: 0}}
	army2 := []ArmyPlacement{{TypeName: "tank", X: 7, Y: 0}} // Within archer range (8)

	sim, _ := NewSimulation(1, 2, army1, army2)

	// Run a few ticks to let the archer fire
	foundProjectile := false
	for i := 0; i < 100; i++ {
		sim.Step()
		if len(sim.Projectiles) > 0 {
			foundProjectile = true
			break
		}
	}

	if !foundProjectile {
		t.Error("archer should have spawned a projectile")
	}
}

func TestSimulationArtillerySplash(t *testing.T) {
	// Artillery has splash damage — should damage multiple close units
	army1 := []ArmyPlacement{{TypeName: "artillery", X: 0, Y: 0}}
	army2 := []ArmyPlacement{
		{TypeName: "crawler", X: 14, Y: 0},
		{TypeName: "crawler", X: 14.5, Y: 0.5}, // Close to first crawler, within splash radius (3.0)
	}

	sim, _ := NewSimulation(1, 2, army1, army2)

	for i := 0; i < 500; i++ {
		sim.Step()
		if sim.Status == "finished" {
			break
		}
	}

	if sim.Status != "finished" {
		t.Error("battle should have finished")
	}
}

func TestSimulationHealerHeals(t *testing.T) {
	// Healer should heal damaged friendly units
	army1 := []ArmyPlacement{
		{TypeName: "tank", X: 0, Y: 0},
		{TypeName: "healer", X: 1, Y: 0},
	}
	army2 := []ArmyPlacement{{TypeName: "scout", X: 50, Y: 50}} // Far away, won't interfere

	sim, _ := NewSimulation(1, 2, army1, army2)

	// Damage the tank
	sim.Units[0].Health = 100 // Tank has max 200

	// Run enough ticks for healer to activate (cooldown 2s = 40 ticks at 20fps)
	for i := 0; i < 200; i++ {
		sim.Step()
	}

	if sim.Units[0].Health <= 100 {
		t.Errorf("healer should have healed tank above 100, health is %d", sim.Units[0].Health)
	}
}

func TestSimulationGetState(t *testing.T) {
	army1 := []ArmyPlacement{{TypeName: "crawler", X: 0, Y: 0}}
	army2 := []ArmyPlacement{{TypeName: "crawler", X: 5, Y: 0}}

	sim, _ := NewSimulation(1, 2, army1, army2)
	sim.Step()

	state := sim.GetState()
	if state.Tick != 1 {
		t.Errorf("expected tick 1, got %d", state.Tick)
	}
	if len(state.Units) != 2 {
		t.Errorf("expected 2 units in state, got %d", len(state.Units))
	}
	if state.Status != "running" {
		t.Errorf("expected status running, got %s", state.Status)
	}
}

func TestSimulationAliveCount(t *testing.T) {
	army1 := []ArmyPlacement{
		{TypeName: "crawler", X: 0, Y: 0},
		{TypeName: "crawler", X: 1, Y: 0},
	}
	army2 := []ArmyPlacement{{TypeName: "crawler", X: 5, Y: 0}}

	sim, _ := NewSimulation(1, 2, army1, army2)

	if sim.AliveCount(1) != 2 {
		t.Errorf("expected 2 alive for player 1, got %d", sim.AliveCount(1))
	}
	if sim.AliveCount(2) != 1 {
		t.Errorf("expected 1 alive for player 2, got %d", sim.AliveCount(2))
	}
}

func TestSimulationMutualDestruction(t *testing.T) {
	// Two identical units very close — could result in mutual destruction
	army1 := []ArmyPlacement{{TypeName: "crawler", X: 0, Y: 0}}
	army2 := []ArmyPlacement{{TypeName: "crawler", X: 0.1, Y: 0}}

	sim, _ := NewSimulation(1, 2, army1, army2)

	for i := 0; i < 500; i++ {
		sim.Step()
		if sim.Status == "finished" {
			break
		}
	}

	if sim.Status != "finished" {
		t.Error("battle should have finished")
	}
}

func TestSimulationManyUnits(t *testing.T) {
	// Larger army should generally win
	army1 := []ArmyPlacement{
		{TypeName: "crawler", X: 0, Y: 0},
		{TypeName: "crawler", X: 1, Y: 0},
		{TypeName: "crawler", X: 0, Y: 1},
		{TypeName: "archer", X: -2, Y: 0},
	}
	army2 := []ArmyPlacement{
		{TypeName: "crawler", X: 10, Y: 0},
	}

	sim, _ := NewSimulation(1, 2, army1, army2)

	for i := 0; i < 500; i++ {
		sim.Step()
		if sim.Status == "finished" {
			break
		}
	}

	if sim.WinnerID != 1 {
		t.Errorf("expected player 1 (larger army) to win, got %d", sim.WinnerID)
	}
}

// ─────────────────────── Handler Tests ───────────────────────

func newTestBattlerRouter() *mux.Router {
	mgr := NewManager()
	h := NewHandler(mgr)
	r := mux.NewRouter()
	h.RegisterRoutes(r)
	return r
}

func doBattlerRequest(r *mux.Router, method, path string, body interface{}) *httptest.ResponseRecorder {
	var reqBody *bytes.Buffer
	if body != nil {
		b, _ := json.Marshal(body)
		reqBody = bytes.NewBuffer(b)
	} else {
		reqBody = bytes.NewBuffer(nil)
	}
	req := httptest.NewRequest(method, path, reqBody)
	req.Header.Set("Content-Type", "application/json")
	rr := httptest.NewRecorder()
	r.ServeHTTP(rr, req)
	return rr
}

func TestBattlerHandlerGetTypes(t *testing.T) {
	r := newTestBattlerRouter()
	rr := doBattlerRequest(r, "GET", "/api/battle/types", nil)

	if rr.Code != http.StatusOK {
		t.Errorf("expected 200, got %d: %s", rr.Code, rr.Body.String())
	}

	var types []UnitType
	json.NewDecoder(rr.Body).Decode(&types)
	if len(types) != 6 {
		t.Errorf("expected 6 unit types, got %d", len(types))
	}
}

func TestBattlerHandlerStartBattle(t *testing.T) {
	r := newTestBattlerRouter()

	req := StartBattleRequest{
		SessionID: 1,
		Player1ID: 1,
		Player2ID: 2,
		Army1:     []ArmyPlacement{{TypeName: "crawler", X: 0, Y: 0}},
		Army2:     []ArmyPlacement{{TypeName: "crawler", X: 10, Y: 0}},
	}

	rr := doBattlerRequest(r, "POST", "/api/battle/start", req)
	if rr.Code != http.StatusCreated {
		t.Errorf("expected 201, got %d: %s", rr.Code, rr.Body.String())
	}

	var resp map[string]interface{}
	json.NewDecoder(rr.Body).Decode(&resp)
	if resp["message"] != "battle started" {
		t.Errorf("expected 'battle started', got %v", resp["message"])
	}
}

func TestBattlerHandlerStartBattleMissingPlayers(t *testing.T) {
	r := newTestBattlerRouter()

	rr := doBattlerRequest(r, "POST", "/api/battle/start", map[string]interface{}{
		"army1": []map[string]interface{}{{"type_name": "crawler", "x": 0, "y": 0}},
		"army2": []map[string]interface{}{{"type_name": "crawler", "x": 10, "y": 0}},
	})
	if rr.Code != http.StatusBadRequest {
		t.Errorf("expected 400, got %d", rr.Code)
	}
}

func TestBattlerHandlerStartBattleEmptyArmy(t *testing.T) {
	r := newTestBattlerRouter()

	rr := doBattlerRequest(r, "POST", "/api/battle/start", map[string]interface{}{
		"player1_id": 1,
		"player2_id": 2,
		"army1":      []map[string]interface{}{},
		"army2":      []map[string]interface{}{{"type_name": "crawler", "x": 10, "y": 0}},
	})
	if rr.Code != http.StatusBadRequest {
		t.Errorf("expected 400, got %d", rr.Code)
	}
}

func TestBattlerHandlerInvalidBody(t *testing.T) {
	r := newTestBattlerRouter()
	req := httptest.NewRequest("POST", "/api/battle/start", strings.NewReader("not json"))
	req.Header.Set("Content-Type", "application/json")
	rr := httptest.NewRecorder()
	r.ServeHTTP(rr, req)
	if rr.Code != http.StatusBadRequest {
		t.Errorf("expected 400, got %d", rr.Code)
	}
}

func TestBattlerHandlerInvalidUnitType(t *testing.T) {
	r := newTestBattlerRouter()

	rr := doBattlerRequest(r, "POST", "/api/battle/start", map[string]interface{}{
		"player1_id": 1,
		"player2_id": 2,
		"army1":      []map[string]interface{}{{"type_name": "nonexistent", "x": 0, "y": 0}},
		"army2":      []map[string]interface{}{{"type_name": "crawler", "x": 10, "y": 0}},
	})
	if rr.Code != http.StatusBadRequest {
		t.Errorf("expected 400, got %d", rr.Code)
	}
}

// ─────────────────────── WebSocket Integration Test ───────────────────────

func TestBattlerWebSocketIntegration(t *testing.T) {
	mgr := NewManager()
	h := NewHandler(mgr)
	r := mux.NewRouter()
	h.RegisterRoutes(r)

	// Start a test HTTP server
	server := httptest.NewServer(r)
	defer server.Close()

	// Start a battle via HTTP
	body, _ := json.Marshal(StartBattleRequest{
		SessionID: 1,
		Player1ID: 1,
		Player2ID: 2,
		Army1:     []ArmyPlacement{{TypeName: "crawler", X: 0, Y: 0}},
		Army2:     []ArmyPlacement{{TypeName: "crawler", X: 3, Y: 0}},
	})

	resp, err := http.Post(server.URL+"/api/battle/start", "application/json", bytes.NewBuffer(body))
	if err != nil {
		t.Fatalf("failed to start battle: %v", err)
	}
	if resp.StatusCode != http.StatusCreated {
		t.Fatalf("expected 201, got %d", resp.StatusCode)
	}
	resp.Body.Close()

	// Connect via WebSocket
	wsURL := "ws" + strings.TrimPrefix(server.URL, "http") + "/ws/battle/1"
	ws, _, err := websocket.DefaultDialer.Dial(wsURL, nil)
	if err != nil {
		t.Fatalf("failed to connect websocket: %v", err)
	}
	defer ws.Close()

	// Read a few state messages
	stateReceived := 0
	var lastState BattleState

	for i := 0; i < 50; i++ {
		ws.SetReadDeadline(time.Now().Add(2 * time.Second))
		_, msg, err := ws.ReadMessage()
		if err != nil {
			break
		}

		var state BattleState
		if err := json.Unmarshal(msg, &state); err != nil {
			t.Fatalf("failed to parse state: %v", err)
		}
		stateReceived++
		lastState = state

		if state.Status == "finished" {
			break
		}
	}

	if stateReceived < 2 {
		t.Errorf("expected at least 2 state updates, got %d", stateReceived)
	}
	if lastState.Tick < 1 {
		t.Errorf("expected tick >= 1, got %d", lastState.Tick)
	}
	if len(lastState.Units) != 2 {
		t.Errorf("expected 2 units in state, got %d", len(lastState.Units))
	}

	t.Logf("received %d state updates, last tick: %d, status: %s, winner: %d",
		stateReceived, lastState.Tick, lastState.Status, lastState.WinnerID)
}
