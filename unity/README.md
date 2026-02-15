# Unity Game Client Libraries

Two drop-in Unity client libraries that work with the `go-gameserver` backend. Each is a self-contained folder you drag into your Unity project — one script on one GameObject and you have a fully working game.

## Quick Start (Both Games)

1. **Start the go-gameserver**:
   ```bash
   cd go-gameserver
   go run ./cmd/gameserver
   ```

2. **Create a Unity project** (2021.3+ recommended)

3. **Copy the client folder** into your Unity project:
   - For Chess: copy `unity/ChessUnityClient/` into `Assets/ChessUnityClient/`
   - For Battler: copy `unity/BattlerUnityClient/` into `Assets/BattlerUnityClient/`

4. **Create an empty GameObject** in your scene

5. **Drag `GameSetup.cs`** onto the GameObject

6. **Hit Play** — that's it!

---

## ChessUnityClient

A complete chess game client with:
- **Main Menu** → enter server URL and player name
- **Lobby** → create or join a chess session
- **3D Chess Board** → procedurally generated board with 3D pieces
- **Click-to-Move** → click a piece, then click the destination
- **Polling** → automatically polls the server for opponent moves
- **NPC Mode** → toggle `PlayVsNPC` in the Inspector to play against a random AI
- **Resign** → button in the HUD to resign the game

### Inspector Settings

| Field | Default | Description |
|-------|---------|-------------|
| `ServerURL` | `http://localhost:3000` | URL of the go-gameserver |
| `DefaultPlayerName` | `Player` | Your display name |
| `PollInterval` | `2.0` | Seconds between server state polls |
| `PlayVsNPC` | `false` | Play against a random computer opponent |

### File Structure

```
ChessUnityClient/
├── GameSetup.cs               ← Drag this onto a GameObject
├── Networking/
│   ├── ApiTypes.cs            — Serializable types matching server JSON
│   └── ServerClient.cs        — HTTP client (UnityWebRequest)
├── GameState/
│   └── ChessBoard.cs          — Board state parser and helpers
├── Rendering/
│   ├── BoardRenderer.cs       — 3D board creation and piece management
│   └── PieceFactory.cs        — Procedural piece meshes (or Addressable prefabs)
└── UI/
    └── MenuManager.cs          — All UGUI menus (Main Menu, Lobby, Game HUD)
```

### Replacing Piece Models

**Option A — Register prefabs in code:**
```csharp
// In your own script's Start():
var setup = FindObjectOfType<ChessUnityClient.GameSetup>();
var board = setup.GetComponentInChildren<ChessUnityClient.BoardRenderer>();
board.RegisterPiecePrefab("WhiteKing", myWhiteKingPrefab);
board.RegisterPiecePrefab("BlackQueen", myBlackQueenPrefab);
// Names: White/Black + King/Queen/Rook/Bishop/Knight/Pawn
```

**Option B — Addressables (automatic):**
Place prefabs at:
```
Assets/ChessUnityClient/Prefabs/WhiteKing.prefab
Assets/ChessUnityClient/Prefabs/BlackQueen.prefab
Assets/ChessUnityClient/Prefabs/WhitePawn.prefab
... (12 total: 6 piece types × 2 colors)
```

---

## BattlerUnityClient

A complete Mechabellum-style auto-battler client with:
- **Main Menu** → connect to server
- **Army Builder** → browse unit catalog, add units to your army within budget
- **Real-Time Battle** → 3D battlefield with units fighting automatically
- **WebSocket Streaming** → receives 20 state updates/second from the server
- **Health Bars** → floating health bars above each unit
- **Projectiles** → visible projectile travel with splash effects
- **NPC Mode** → toggle `PlayVsNPC` for instant single-player matches
- **Result Screen** → Victory/Defeat/Draw overlay when battle ends

### Unit Types

| Unit | Cost | HP | Speed | Damage | Range | Special |
|------|------|----|-------|--------|-------|---------|
| Crawler | 100 | 50 | 3.0 | 10 | 1.5 | Melee, fast |
| Archer | 150 | 30 | 2.0 | 15 | 8.0 | Ranged, fragile |
| Tank | 300 | 200 | 1.0 | 40 | 5.0 | Heavy armor |
| Artillery | 400 | 60 | 0.5 | 80 | 15.0 | Splash damage |
| Scout | 50 | 20 | 5.0 | 5 | 2.0 | Ultra-fast |
| Healer | 250 | 40 | 2.0 | -10 | 6.0 | Heals allies |

### Inspector Settings

| Field | Default | Description |
|-------|---------|-------------|
| `ServerURL` | `http://localhost:3000` | URL of the go-gameserver |
| `DefaultPlayerName` | `Commander` | Your display name |
| `ArmyBudget` | `2000` | Budget for army building |
| `PlayVsNPC` | `true` | Auto-generate opponent army |
| `FieldWidth` | `100` | Battlefield X size |
| `FieldDepth` | `30` | Battlefield Z size |

### File Structure

```
BattlerUnityClient/
├── GameSetup.cs               ← Drag this onto a GameObject
├── Networking/
│   ├── ApiTypes.cs            — Serializable types matching server JSON
│   ├── ServerClient.cs        — HTTP client for REST API
│   └── BattleSocket.cs        — WebSocket client for real-time state
├── GameState/
│   └── BattleManager.cs       — Unit catalog, army composition, state tracking
├── Rendering/
│   ├── BattlefieldRenderer.cs — Terrain, unit management, health bars, projectiles
│   └── UnitFactory.cs         — Procedural unit meshes (or Addressable prefabs)
└── UI/
    └── MenuManager.cs          — All UGUI menus (Main Menu, Army Builder, Battle HUD)
```

### Replacing Unit Models

**Option A — Register prefabs in code:**
```csharp
var setup = FindObjectOfType<BattlerUnityClient.GameSetup>();
var bf = setup.GetComponentInChildren<BattlerUnityClient.BattlefieldRenderer>();
bf.RegisterUnitPrefab("crawler", myCrawlerPrefab);
bf.RegisterUnitPrefab("tank", myTankPrefab);
bf.RegisterProjectilePrefab(myProjectilePrefab);
```

**Option B — Addressables (automatic):**
Place prefabs at:
```
Assets/BattlerUnityClient/Prefabs/Units/Crawler.prefab
Assets/BattlerUnityClient/Prefabs/Units/Archer.prefab
Assets/BattlerUnityClient/Prefabs/Units/Tank.prefab
Assets/BattlerUnityClient/Prefabs/Units/Artillery.prefab
Assets/BattlerUnityClient/Prefabs/Units/Scout.prefab
Assets/BattlerUnityClient/Prefabs/Units/Healer.prefab
Assets/BattlerUnityClient/Prefabs/Projectile.prefab
```

---

## How It Works

Both clients follow the same architecture:

```
┌─────────────────────────────────────────────────────┐
│ GameSetup.cs (MonoBehaviour — the single entry)     │
│   ├── Creates: Camera, EventSystem, UI, Renderers   │
│   ├── Manages: Game flow (menu → lobby → game)      │
│   └── Coordinates: Networking ↔ Rendering ↔ UI      │
├─────────────────────────────────────────────────────┤
│ Networking/ (ServerClient + optional BattleSocket)   │
│   └── Talks to go-gameserver via HTTP / WebSocket    │
├─────────────────────────────────────────────────────┤
│ GameState/ (ChessBoard or BattleManager)             │
│   └── Parses server state into client-side models    │
├─────────────────────────────────────────────────────┤
│ Rendering/ (BoardRenderer or BattlefieldRenderer)    │
│   └── Creates/updates 3D visuals from game state     │
├─────────────────────────────────────────────────────┤
│ UI/ (MenuManager)                                    │
│   └── All UGUI menus, generated in code              │
└─────────────────────────────────────────────────────┘
```

The server is the **single source of truth**. The Unity client is a renderer:
- **Chess**: Polls the server every N seconds for state updates
- **Battler**: Receives state via WebSocket at 20 Hz, interpolates visually

All game logic (move validation, combat, win conditions) runs on the Go server. The client cannot cheat.

---

## Server Endpoints Used

### Chess Client
| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/health` | GET | Health check |
| `/api/players` | POST | Register player |
| `/api/sessions` | POST | Create game session |
| `/api/sessions/{id}/join` | POST | Join session |
| `/api/sessions/{id}/status` | PUT | Update session status |
| `/api/sessions/{id}` | GET | Get session (poll for opponent) |
| `/api/chess` | POST | Create chess game |
| `/api/chess/{id}` | GET | Get game state (polling) |
| `/api/chess/{id}/move` | POST | Make a move |
| `/api/chess/{id}/resign` | POST | Resign |

### Battler Client
| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/health` | GET | Health check |
| `/api/players` | POST | Register player |
| `/api/sessions` | POST | Create session |
| `/api/sessions/{id}/join` | POST | Join session |
| `/api/sessions/{id}/status` | PUT | Update status |
| `/api/sessions/{id}` | GET | Get session |
| `/api/battle/start` | POST | Start battle (submit armies) |
| `/ws/battle/{id}` | WebSocket | Real-time state stream |
