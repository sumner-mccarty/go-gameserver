// BattlerUnityClient/GameSetup.cs
//
// ╔══════════════════════════════════════════════════════════════════════════╗
// ║  BATTLER GAME SETUP — Drag this script onto any GameObject and Play   ║
// ║                                                                        ║
// ║  This single script wires up the entire Mechabellum-style battler:    ║
// ║  • Main Menu → connect to server, enter player name                   ║
// ║  • Army Builder → pick unit types, set budget, compose your army      ║
// ║  • Battle → real-time 3D battlefield, units fight automatically       ║
// ║  • All UI and 3D assets are generated procedurally in code            ║
// ║  • Replace any visuals by registering your own prefabs                ║
// ╚══════════════════════════════════════════════════════════════════════════╝
//
// SETUP:
//   1. Create an empty GameObject in your scene
//   2. Drag this script onto it
//   3. Hit Play
//   4. (Optional) Set ServerURL in the Inspector or Main Menu UI
//
// CUSTOMIZATION:
//   • To replace unit models: call battlefieldRenderer.RegisterUnitPrefab()
//     in your own script, or place prefabs at
//     Assets/BattlerUnityClient/Prefabs/Units/[Crawler|Archer|Tank|...].prefab
//   • To replace projectile: RegisterProjectilePrefab()
//   • To replace UI: place UGUI prefabs at Assets/BattlerUnityClient/Prefabs/UI/
//
// REQUIREMENTS:
//   • A running go-gameserver instance with battle endpoints (default: http://localhost:3000)
//   • Unity 2021.3+

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BattlerUnityClient
{
    /// <summary>
    /// Main entry point for the Mechabellum-style battler game.
    /// Drag onto a GameObject and hit Play. Everything is created automatically.
    /// </summary>
    public class GameSetup : MonoBehaviour
    {
        [Header("Server Configuration")]
        [Tooltip("URL of the go-gameserver instance")]
        public string ServerURL = "http://localhost:3000";

        [Header("Player Configuration")]
        [Tooltip("Default player name")]
        public string DefaultPlayerName = "Commander";

        [Header("Game Settings")]
        [Tooltip("Budget for army building")]
        public int ArmyBudget = 2000;

        [Tooltip("Play against NPC (generates a random opponent army)")]
        public bool PlayVsNPC = true;

        [Header("Battlefield")]
        [Tooltip("Battlefield width (X axis)")]
        public float FieldWidth = 100f;

        [Tooltip("Battlefield depth (Z axis)")]
        public float FieldDepth = 30f;

        // ───────────────────────── Internal State ─────────────────────────

        private ServerClient _client;
        private BattleManager _battleManager;
        private BattlefieldRenderer _battlefieldRenderer;
        private BattleSocket _battleSocket;
        private MenuManager _menuManager;

        private int _myPlayerId;
        private string _myPlayerName;
        private int _currentSessionDbId; // database ID (integer)
        private string _currentSessionId; // string session_id
        private int _opponentPlayerId;
        private Camera _gameCamera;
        private bool _battleActive;

        // ───────────────────────── Lifecycle ─────────────────────────

        void Start()
        {
            // Ensure EventSystem exists
            if (FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }

            _client = new ServerClient(ServerURL);
            _battleManager = new BattleManager();
            _battleManager.ArmyBudget = ArmyBudget;

            // Create battlefield renderer (hidden initially)
            var bfGO = new GameObject("BattlefieldRenderer");
            bfGO.transform.SetParent(transform);
            _battlefieldRenderer = bfGO.AddComponent<BattlefieldRenderer>();
            _battlefieldRenderer.FieldWidth = FieldWidth;
            _battlefieldRenderer.FieldDepth = FieldDepth;
            bfGO.SetActive(false);

            // Create WebSocket component
            _battleSocket = gameObject.AddComponent<BattleSocket>();
            _battleSocket.OnStateReceived += OnBattleStateReceived;
            _battleSocket.OnConnected += () => Debug.Log("[BattlerUnityClient] WebSocket connected");
            _battleSocket.OnDisconnected += (msg) =>
            {
                Debug.Log($"[BattlerUnityClient] WebSocket disconnected: {msg}");
                _battleActive = false;
            };
            _battleSocket.OnError += (msg) => Debug.LogError($"[BattlerUnityClient] WebSocket error: {msg}");

            // Create menu manager
            var menuGO = new GameObject("MenuManager");
            menuGO.transform.SetParent(transform);
            _menuManager = menuGO.AddComponent<MenuManager>();
            _menuManager.Initialize();

            // Wire up menu callbacks
            _menuManager.OnConnect = HandleConnect;
            _menuManager.OnCreateSession = HandleCreateSession;
            _menuManager.OnJoinSession = HandleJoinSession;
            _menuManager.OnAddUnit = HandleAddUnit;
            _menuManager.OnRemoveLastUnit = HandleRemoveLastUnit;
            _menuManager.OnClearArmy = HandleClearArmy;
            _menuManager.OnStartBattle = HandleStartBattle;

            // Camera
            SetupCamera();

            Debug.Log("[BattlerUnityClient] Game setup complete. Use the Main Menu to connect.");
        }

        void Update()
        {
            // Update battle HUD while battle is active
            if (_battleActive && _battleSocket.LatestState != null)
            {
                _menuManager.UpdateBattleHUD(_battleSocket.LatestState, _myPlayerId);
            }
        }

        // ───────────────────────── Menu Callbacks ─────────────────────────

        private void HandleConnect(string serverUrl, string playerName)
        {
            ServerURL = serverUrl;
            _client = new ServerClient(serverUrl);
            _myPlayerName = playerName;
            _menuManager.SetMainMenuStatus("Connecting...");
            StartCoroutine(ConnectToServer());
        }

        private IEnumerator ConnectToServer()
        {
            // Check health
            bool healthy = false;
            yield return _client.CheckHealth(h => healthy = h);

            if (!healthy)
            {
                _menuManager.SetMainMenuStatus("Cannot reach server. Check the URL.");
                yield break;
            }

            // Create player
            string uid = _myPlayerName.ToLower().Replace(" ", "-") + "-" + Random.Range(1000, 9999);
            PlayerData player = null;
            string error = null;

            yield return _client.CreatePlayer(uid, _myPlayerName, (p, err) =>
            {
                player = p;
                error = err;
            });

            if (error != null)
            {
                _menuManager.SetMainMenuStatus($"Failed: {error}");
                yield break;
            }

            _myPlayerId = player.id;
            _menuManager.SetMainMenuStatus($"Connected as {player.name} (ID: {player.id})");

            yield return new WaitForSeconds(0.5f);
            _menuManager.ShowArmyBuilder(_battleManager);
        }

        private void HandleCreateSession(string sessionId)
        {
            StartCoroutine(CreateSession(sessionId));
        }

        private IEnumerator CreateSession(string sessionId)
        {
            _menuManager.SetWaitingText("Creating session...");

            SessionData session = null;
            string error = null;

            yield return _client.CreateSession(sessionId, 2, (s, err) =>
            {
                session = s;
                error = err;
            });

            if (error != null)
            {
                _menuManager.SetMainMenuStatus($"Failed: {error}");
                yield break;
            }

            yield return _client.JoinSession(sessionId, _myPlayerId, (s, err) =>
            {
                session = s;
                error = err;
            });

            if (error != null)
            {
                _menuManager.SetMainMenuStatus($"Join failed: {error}");
                yield break;
            }

            _currentSessionId = sessionId;
            _currentSessionDbId = session.id;

            _menuManager.ShowArmyBuilder(_battleManager);
            _menuManager.UpdateArmyBuilderUI(_battleManager);
        }

        private void HandleJoinSession(string sessionId)
        {
            StartCoroutine(JoinSession(sessionId));
        }

        private IEnumerator JoinSession(string sessionId)
        {
            SessionData session = null;
            string error = null;

            yield return _client.JoinSession(sessionId, _myPlayerId, (s, err) =>
            {
                session = s;
                error = err;
            });

            if (error != null)
            {
                _menuManager.SetMainMenuStatus($"Join failed: {error}");
                yield break;
            }

            _currentSessionId = sessionId;
            _currentSessionDbId = session.id;

            _menuManager.ShowArmyBuilder(_battleManager);
        }

        private void HandleAddUnit(string typeName, float x, float y)
        {
            if (_battleManager.AddUnitToArmy(typeName, x, y))
            {
                _menuManager.UpdateArmyBuilderUI(_battleManager);
            }
        }

        private void HandleRemoveLastUnit()
        {
            _battleManager.RemoveLastUnit();
            _menuManager.UpdateArmyBuilderUI(_battleManager);
        }

        private void HandleClearArmy()
        {
            _battleManager.ClearArmy();
            _menuManager.UpdateArmyBuilderUI(_battleManager);
        }

        private void HandleStartBattle()
        {
            if (_battleManager.Army1.Count == 0)
            {
                _menuManager.SetMainMenuStatus("Add at least one unit to your army!");
                return;
            }
            StartCoroutine(StartBattle());
        }

        // ───────────────────────── Battle Flow ─────────────────────────

        private IEnumerator StartBattle()
        {
            _menuManager.ShowWaiting("Preparing battle...");

            // Ensure we have a session
            if (string.IsNullOrEmpty(_currentSessionId))
            {
                string sessionId = "battle-" + Random.Range(10000, 99999);
                yield return CreateSession(sessionId);
                if (string.IsNullOrEmpty(_currentSessionId)) yield break;
            }

            // Create NPC opponent if needed
            if (PlayVsNPC)
            {
                yield return CreateNPCOpponent();
                if (_opponentPlayerId == 0) yield break;
            }
            else
            {
                // Wait for opponent to join
                yield return WaitForOpponent();
                if (_opponentPlayerId == 0) yield break;
            }

            // Update session to active
            yield return _client.UpdateSessionStatus(_currentSessionId, "active", (s, err) => { });

            // Generate opponent army — deploy in right 20% of the field
            float deployMargin = 2f;
            float enemyXMin = FieldWidth * 0.8f;
            float enemyXMax = FieldWidth - deployMargin;
            List<ArmyUnitPlacement> opponentArmy;
            if (PlayVsNPC)
            {
                opponentArmy = _battleManager.GenerateRandomArmy(enemyXMin, enemyXMax, deployMargin, FieldDepth - deployMargin, ArmyBudget);
            }
            else
            {
                // In PvP, the other player would submit their army too
                // For now, generate a random one as placeholder
                opponentArmy = _battleManager.GenerateRandomArmy(enemyXMin, enemyXMax, deployMargin, FieldDepth - deployMargin, ArmyBudget);
            }

            // Send start battle request
            var request = new StartBattleRequest
            {
                session_id = _currentSessionDbId,
                player1_id = _myPlayerId,
                player2_id = _opponentPlayerId,
                army1 = _battleManager.Army1.ToArray(),
                army2 = opponentArmy.ToArray()
            };

            BattleStartResponse startResp = null;
            string error = null;

            yield return _client.StartBattle(request, (r, err) =>
            {
                startResp = r;
                error = err;
            });

            if (error != null)
            {
                _menuManager.ShowWaiting($"Failed to start battle: {error}");
                yield return new WaitForSeconds(2f);
                _menuManager.ShowArmyBuilder(_battleManager);
                yield break;
            }

            // Initialize battlefield renderer
            _battlefieldRenderer.gameObject.SetActive(true);
            _battlefieldRenderer.Initialize(_myPlayerId);
            _battlefieldRenderer.ClearAll();

            // Show battle HUD
            _menuManager.ShowBattleHUD();

            // Position camera for battle view
            PositionBattleCamera();

            // Connect WebSocket for real-time updates
            string wsUrl = _client.GetBattleWebSocketURL(_currentSessionDbId);
            _battleSocket.Connect(wsUrl);
            _battleActive = true;

            Debug.Log($"[BattlerUnityClient] Battle started! {startResp?.unit_count} units deployed.");
        }

        private IEnumerator CreateNPCOpponent()
        {
            string npcUid = "npc-" + Random.Range(10000, 99999);
            PlayerData npc = null;
            string error = null;

            yield return _client.CreatePlayer(npcUid, "Enemy AI", (p, err) =>
            {
                npc = p;
                error = err;
            });

            if (error != null)
            {
                _menuManager.ShowWaiting($"Failed to create opponent: {error}");
                yield break;
            }

            yield return _client.JoinSession(_currentSessionId, npc.id, (s, err) =>
            {
                error = err;
            });

            if (error != null)
            {
                _menuManager.ShowWaiting($"Opponent failed to join: {error}");
                yield break;
            }

            _opponentPlayerId = npc.id;
        }

        private IEnumerator WaitForOpponent()
        {
            _menuManager.ShowWaiting("Waiting for opponent...");

            for (int i = 0; i < 300; i++) // 10-minute timeout
            {
                yield return new WaitForSeconds(2f);

                SessionData session = null;
                yield return _client.GetSession(_currentSessionId, (s, err) => { session = s; });

                if (session?.players != null && session.players.Length >= 2)
                {
                    foreach (var p in session.players)
                    {
                        if (p.id != _myPlayerId)
                        {
                            _opponentPlayerId = p.id;
                            yield break;
                        }
                    }
                }
            }
            _menuManager.ShowWaiting("Timed out waiting for opponent.");
        }

        // ───────────────────────── WebSocket State Handler ─────────────────────────

        private void OnBattleStateReceived(BattleStateData state)
        {
            _battleManager.CurrentState = state;
            _battlefieldRenderer.UpdateFromState(state);

            if (state.status == "finished")
            {
                _battleActive = false;
                _battleSocket.Disconnect();
                Debug.Log($"[BattlerUnityClient] Battle finished! Winner: {state.winner_id}");
            }
        }

        // ───────────────────────── Camera ─────────────────────────

        private void SetupCamera()
        {
            _gameCamera = Camera.main;
            if (_gameCamera == null)
            {
                var camGO = new GameObject("BattleCamera");
                _gameCamera = camGO.AddComponent<Camera>();
                _gameCamera.tag = "MainCamera";
            }

            _gameCamera.clearFlags = CameraClearFlags.SolidColor;
            _gameCamera.backgroundColor = new Color(0.1f, 0.1f, 0.15f);
            _gameCamera.transform.position = new Vector3(FieldWidth / 2f, 40f, -20f);
            _gameCamera.transform.LookAt(new Vector3(FieldWidth / 2f, 0, FieldDepth / 2f));
        }

        private void PositionBattleCamera()
        {
            if (_gameCamera == null) return;
            _gameCamera.transform.position = new Vector3(FieldWidth / 2f, 35f, -15f);
            _gameCamera.transform.LookAt(new Vector3(FieldWidth / 2f, 0, FieldDepth / 2f));
        }
    }
}
