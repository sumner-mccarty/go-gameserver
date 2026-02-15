package battler

import (
	"encoding/json"
	"log"
	"sync"
	"time"

	"github.com/gorilla/websocket"
)

// Battle is a running battle instance managed by the Manager.
type Battle struct {
	ID         int
	Simulation *Simulation
	Player1ID  int
	Player2ID  int
	clients    map[*websocket.Conn]bool
	mu         sync.Mutex
	done       chan struct{}
}

// Manager manages all active battles.
type Manager struct {
	mu       sync.RWMutex
	battles  map[int]*Battle
	nextID   int
}

// NewManager creates a new battle manager.
func NewManager() *Manager {
	return &Manager{
		battles: make(map[int]*Battle),
		nextID:  1,
	}
}

// StartBattle creates and starts a new battle simulation.
func (m *Manager) StartBattle(sessionID, player1ID, player2ID int, army1, army2 []ArmyPlacement) (*Battle, error) {
	sim, err := NewSimulation(player1ID, player2ID, army1, army2)
	if err != nil {
		return nil, err
	}

	m.mu.Lock()
	battle := &Battle{
		ID:         m.nextID,
		Simulation: sim,
		Player1ID:  player1ID,
		Player2ID:  player2ID,
		clients:    make(map[*websocket.Conn]bool),
		done:       make(chan struct{}),
	}
	m.nextID++
	m.battles[battle.ID] = battle
	m.mu.Unlock()

	// Start the tick loop in a goroutine
	go battle.runLoop()

	return battle, nil
}

// GetBattle returns a battle by ID.
func (m *Manager) GetBattle(id int) *Battle {
	m.mu.RLock()
	defer m.mu.RUnlock()
	return m.battles[id]
}

// AddClient adds a WebSocket connection to receive state updates.
func (b *Battle) AddClient(conn *websocket.Conn) {
	b.mu.Lock()
	b.clients[conn] = true
	b.mu.Unlock()
}

// RemoveClient removes a WebSocket connection.
func (b *Battle) RemoveClient(conn *websocket.Conn) {
	b.mu.Lock()
	delete(b.clients, conn)
	b.mu.Unlock()
}

// runLoop is the main 20-tick-per-second game loop.
func (b *Battle) runLoop() {
	ticker := time.NewTicker(time.Second / TickRate)
	defer ticker.Stop()

	log.Printf("[battle %d] simulation started (player %d vs %d, %d units)",
		b.ID, b.Player1ID, b.Player2ID, len(b.Simulation.Units))

	for {
		select {
		case <-ticker.C:
			b.Simulation.Step()
			state := b.Simulation.GetState()
			b.broadcast(state)

			if state.Status == "finished" {
				log.Printf("[battle %d] finished — winner: player %d (tick %d)",
					b.ID, state.WinnerID, state.Tick)
				close(b.done)
				return
			}
		case <-b.done:
			return
		}
	}
}

// broadcast sends the current state to all connected WebSocket clients.
func (b *Battle) broadcast(state BattleState) {
	data, err := json.Marshal(state)
	if err != nil {
		log.Printf("[battle %d] failed to marshal state: %v", b.ID, err)
		return
	}

	b.mu.Lock()
	defer b.mu.Unlock()

	for conn := range b.clients {
		if err := conn.WriteMessage(websocket.TextMessage, data); err != nil {
			log.Printf("[battle %d] client write error: %v", b.ID, err)
			conn.Close()
			delete(b.clients, conn)
		}
	}
}

// Done returns a channel that is closed when the battle finishes.
func (b *Battle) Done() <-chan struct{} {
	return b.done
}
