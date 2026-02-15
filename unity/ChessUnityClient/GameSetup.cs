// ChessUnityClient/GameSetup.cs
//
// ╔══════════════════════════════════════════════════════════════════════╗
// ║  CHESS GAME SETUP — Drag this script onto any GameObject and Play  ║
// ║                                                                    ║
// ║  This single script wires up the entire chess game:                ║
// ║  • Main Menu → connect to server, enter player name               ║
// ║  • Lobby → create or join a chess session                         ║
// ║  • Game → 3D chess board with click-to-move, turn-based play      ║
// ║  • All UI and 3D assets are generated procedurally in code        ║
// ║  • Replace any visuals by registering your own prefabs            ║
// ╚══════════════════════════════════════════════════════════════════════╝
//
// SETUP:
//   1. Create an empty GameObject in your scene
//   2. Drag this script onto it
//   3. Hit Play
//   4. (Optional) Set ServerURL in the Inspector or Main Menu UI
//
// CUSTOMIZATION:
//   • To replace chess piece models: call boardRenderer.RegisterPiecePrefab()
//     in your own script, or place prefabs at Assets/ChessUnityClient/Prefabs/
//     with names like "WhiteKing", "BlackPawn", etc.
//   • To replace UI: place UGUI prefabs at Assets/ChessUnityClient/Prefabs/UI/
//
// REQUIREMENTS:
//   • A running go-gameserver instance (default: http://localhost:3000)
//   • Unity 2021.3+ with TextMeshPro (optional) and legacy UI

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ChessUnityClient
{
    /// <summary>
    /// Main entry point for the chess game. Drag onto a GameObject and hit Play.
    /// Everything is created automatically — board, pieces, UI, networking.
    /// </summary>
    public class GameSetup : MonoBehaviour
    {
        [Header("Server Configuration")]
        [Tooltip("URL of the go-gameserver instance")]
        public string ServerURL = "http://localhost:3000";

        [Header("Player Configuration")]
        [Tooltip("Default player name (can be changed in the Main Menu)")]
        public string DefaultPlayerName = "Player";

        [Header("Game Settings")]
        [Tooltip("Seconds between polling the server for game state updates")]
        public float PollInterval = 2.0f;

        [Tooltip("Play against AI/NPC (server must support it) or wait for human opponent")]
        public bool PlayVsNPC = false;

        // ───────────────────────── Internal State ─────────────────────────

        private ServerClient _client;
        private ChessBoard _board;
        private BoardRenderer _boardRenderer;
        private MenuManager _menuManager;

        private int _myPlayerId;
        private string _myPlayerName;
        private string _currentSessionId;
        private int _currentGameId;
        private string _myColor; // "white" or "black"
        private bool _isPolling;
        private Camera _gameCamera;

        // ───────────────────────── Lifecycle ─────────────────────────

        void Start()
        {
            // Ensure EventSystem exists (required for UI)
            if (FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }

            _board = new ChessBoard();
            _client = new ServerClient(ServerURL);

            // Create board renderer
            var boardGO = new GameObject("BoardRenderer");
            boardGO.transform.SetParent(transform);
            _boardRenderer = boardGO.AddComponent<BoardRenderer>();
            _boardRenderer.Initialize();
            boardGO.SetActive(false); // Hidden until game starts

            // Create menu manager
            var menuGO = new GameObject("MenuManager");
            menuGO.transform.SetParent(transform);
            _menuManager = menuGO.AddComponent<MenuManager>();
            _menuManager.Initialize();

            // Wire up menu callbacks
            _menuManager.OnConnect = HandleConnect;
            _menuManager.OnCreateSession = HandleCreateSession;
            _menuManager.OnJoinSession = HandleJoinSession;
            _menuManager.OnResign = HandleResign;

            // Create camera for the chess board view
            SetupCamera();

            Debug.Log("[ChessUnityClient] Game setup complete. Use the Main Menu to connect to the server.");
        }

        void Update()
        {
            // Handle mouse click for piece selection and movement
            if (_currentGameId > 0 && _board.IsGameActive && _board.Turn == _myColor)
            {
                HandleMouseInput();
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
            // 1. Check server health
            bool healthy = false;
            yield return _client.CheckHealth(h => healthy = h);

            if (!healthy)
            {
                _menuManager.SetMainMenuStatus("Cannot reach server. Check the URL and try again.");
                yield break;
            }

            // 2. Create player (or reuse if uid exists — server returns existing)
            string uid = _myPlayerName.ToLower().Replace(" ", "-") + "-" + UnityEngine.Random.Range(1000, 9999);
            PlayerData player = null;
            string error = null;

            yield return _client.CreatePlayer(uid, _myPlayerName, (p, err) =>
            {
                player = p;
                error = err;
            });

            if (error != null)
            {
                _menuManager.SetMainMenuStatus($"Failed to register: {error}");
                yield break;
            }

            _myPlayerId = player.id;
            _menuManager.SetMainMenuStatus($"Connected as {player.name} (ID: {player.id})");

            // Move to lobby after brief delay
            yield return new WaitForSeconds(0.5f);
            _menuManager.ShowLobby();
        }

        private void HandleCreateSession(string sessionId)
        {
            StartCoroutine(CreateAndJoinSession(sessionId));
        }

        private IEnumerator CreateAndJoinSession(string sessionId)
        {
            _menuManager.SetLobbyStatus("Creating session...");

            // Create session
            SessionData session = null;
            string error = null;

            yield return _client.CreateSession(sessionId, 2, (s, err) =>
            {
                session = s;
                error = err;
            });

            if (error != null)
            {
                _menuManager.SetLobbyStatus($"Failed to create session: {error}");
                yield break;
            }

            // Join the session
            yield return _client.JoinSession(sessionId, _myPlayerId, (s, err) =>
            {
                session = s;
                error = err;
            });

            if (error != null)
            {
                _menuManager.SetLobbyStatus($"Failed to join: {error}");
                yield break;
            }

            _currentSessionId = sessionId;
            _menuManager.SetLobbyStatus("Waiting for opponent...");

            if (PlayVsNPC)
            {
                // Create NPC player and start game immediately
                yield return CreateNPCAndStartGame(session);
            }
            else
            {
                // Wait for another player to join
                StartCoroutine(WaitForOpponent(sessionId));
            }
        }

        private void HandleJoinSession(string sessionId)
        {
            StartCoroutine(JoinExistingSession(sessionId));
        }

        private IEnumerator JoinExistingSession(string sessionId)
        {
            _menuManager.SetLobbyStatus("Joining session...");

            string error = null;
            SessionData session = null;

            yield return _client.JoinSession(sessionId, _myPlayerId, (s, err) =>
            {
                session = s;
                error = err;
            });

            if (error != null)
            {
                _menuManager.SetLobbyStatus($"Failed to join: {error}");
                yield break;
            }

            _currentSessionId = sessionId;

            // If session has 2 players, start the game
            if (session.players != null && session.players.Length >= 2)
            {
                yield return StartChessGame(session);
            }
            else
            {
                _menuManager.SetLobbyStatus("Joined! Waiting for opponent...");
                StartCoroutine(WaitForOpponent(sessionId));
            }
        }

        private IEnumerator WaitForOpponent(string sessionId)
        {
            while (true)
            {
                yield return new WaitForSeconds(2f);

                SessionData session = null;
                yield return _client.GetSession(sessionId, (s, err) =>
                {
                    session = s;
                });

                if (session != null && session.players != null && session.players.Length >= 2)
                {
                    yield return StartChessGame(session);
                    yield break;
                }

                _menuManager.SetLobbyStatus("Waiting for opponent to join...");
            }
        }

        private IEnumerator CreateNPCAndStartGame(SessionData session)
        {
            // Create an NPC player
            string npcUid = "npc-" + UnityEngine.Random.Range(10000, 99999);
            PlayerData npc = null;
            string error = null;

            yield return _client.CreatePlayer(npcUid, "Computer", (p, err) =>
            {
                npc = p;
                error = err;
            });

            if (error != null || npc == null)
            {
                _menuManager.SetLobbyStatus($"Failed to create NPC: {error}");
                yield break;
            }

            // NPC joins the session
            yield return _client.JoinSession(_currentSessionId, npc.id, (s, err) =>
            {
                session = s;
                error = err;
            });

            if (error != null)
            {
                _menuManager.SetLobbyStatus($"NPC failed to join: {error}");
                yield break;
            }

            yield return StartChessGame(session);
        }

        // ───────────────────────── Game Flow ─────────────────────────

        private IEnumerator StartChessGame(SessionData session)
        {
            _menuManager.SetLobbyStatus("Starting chess game...");

            // Determine who is white and who is black
            int whiteId = session.players[0].id;
            int blackId = session.players[1].id;
            _myColor = (_myPlayerId == whiteId) ? "white" : "black";

            // Update session status to active
            yield return _client.UpdateSessionStatus(_currentSessionId, "active", (s, err) => { });

            // Create chess game on server
            ChessGameData gameData = null;
            string error = null;

            yield return _client.CreateChessGame(session.id, whiteId, blackId, (g, err) =>
            {
                gameData = g;
                error = err;
            });

            if (error != null)
            {
                _menuManager.SetLobbyStatus($"Failed to start game: {error}");
                yield break;
            }

            _currentGameId = gameData.id;
            _board.UpdateFromServer(gameData);

            // Show the game
            _menuManager.ShowGameHUD();
            _boardRenderer.gameObject.SetActive(true);
            _boardRenderer.UpdatePieces(_board);
            _menuManager.UpdateGameHUD(_board);

            // Position camera based on color
            PositionCamera(_myColor);

            // Start polling for game state
            _isPolling = true;
            StartCoroutine(PollGameState());

            Debug.Log($"[ChessUnityClient] Game started! You are {_myColor}.");
        }

        private IEnumerator PollGameState()
        {
            while (_isPolling && _board.IsGameActive)
            {
                yield return new WaitForSeconds(PollInterval);

                ChessGameData gameData = null;
                yield return _client.GetChessGame(_currentGameId, (g, err) =>
                {
                    gameData = g;
                });

                if (gameData != null)
                {
                    _board.UpdateFromServer(gameData);
                    _boardRenderer.UpdatePieces(_board);
                    _menuManager.UpdateGameHUD(_board);

                    // If it's NPC's turn and we're in NPC mode, make a random move
                    if (PlayVsNPC && _board.Turn != _myColor && _board.IsGameActive)
                    {
                        yield return MakeNPCMove();
                    }
                }
            }
        }

        // ───────────────────────── Mouse Input ─────────────────────────

        private void HandleMouseInput()
        {
            if (Input.GetMouseButtonDown(0))
            {
                Ray ray = _gameCamera.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    var (row, col) = _boardRenderer.WorldToSquare(hit.point);
                    if (row < 0) return;

                    string clickedSquare = ChessBoard.IndexToAlgebraic(row, col);

                    if (_board.SelectedSquare == null)
                    {
                        // First click — select a piece
                        if (_board.IsPieceOwnedBy(row, col, _myColor))
                        {
                            _board.SelectedSquare = clickedSquare;
                            _boardRenderer.HighlightSquare(row, col, true);
                        }
                    }
                    else
                    {
                        // Second click — attempt move
                        string from = _board.SelectedSquare;
                        string to = clickedSquare;

                        _board.SelectedSquare = null;
                        _boardRenderer.ClearHighlight();

                        if (from != to)
                        {
                            StartCoroutine(SendMove(from, to));
                        }
                    }
                }
            }

            // Right click to deselect
            if (Input.GetMouseButtonDown(1))
            {
                _board.SelectedSquare = null;
                _boardRenderer.ClearHighlight();
            }
        }

        private IEnumerator SendMove(string from, string to)
        {
            ChessGameData result = null;
            string error = null;

            yield return _client.MakeChessMove(_currentGameId, _myPlayerId, from, to, (g, err) =>
            {
                result = g;
                error = err;
            });

            if (error != null)
            {
                Debug.LogWarning($"[ChessUnityClient] Move rejected: {error}");
                // Could show a toast/notification here
                yield break;
            }

            _board.UpdateFromServer(result);
            _boardRenderer.UpdatePieces(_board);
            _menuManager.UpdateGameHUD(_board);
        }

        // ───────────────────────── NPC ─────────────────────────

        private IEnumerator MakeNPCMove()
        {
            yield return new WaitForSeconds(0.5f);

            // Find a valid move for the NPC (simple: pick first piece with a valid target)
            string npcColor = (_myColor == "white") ? "black" : "white";
            int npcId = (npcColor == "white") ? _board.WhiteID : _board.BlackID;

            // Try random moves until one works
            for (int attempt = 0; attempt < 100; attempt++)
            {
                int fromRow = UnityEngine.Random.Range(0, 8);
                int fromCol = UnityEngine.Random.Range(0, 8);

                if (!_board.IsPieceOwnedBy(fromRow, fromCol, npcColor))
                    continue;

                int toRow = UnityEngine.Random.Range(0, 8);
                int toCol = UnityEngine.Random.Range(0, 8);

                string from = ChessBoard.IndexToAlgebraic(fromRow, fromCol);
                string to = ChessBoard.IndexToAlgebraic(toRow, toCol);

                if (from == to) continue;

                bool moveOk = false;
                yield return _client.MakeChessMove(_currentGameId, npcId, from, to, (g, err) =>
                {
                    if (err == null && g != null)
                    {
                        _board.UpdateFromServer(g);
                        _boardRenderer.UpdatePieces(_board);
                        _menuManager.UpdateGameHUD(_board);
                        moveOk = true;
                    }
                });

                if (moveOk) yield break;
            }
        }

        // ───────────────────────── Resign ─────────────────────────

        private void HandleResign()
        {
            if (_currentGameId <= 0) return;
            StartCoroutine(DoResign());
        }

        private IEnumerator DoResign()
        {
            yield return _client.ResignChessGame(_currentGameId, _myPlayerId, (g, err) =>
            {
                if (g != null)
                {
                    _board.UpdateFromServer(g);
                    _boardRenderer.UpdatePieces(_board);
                    _menuManager.UpdateGameHUD(_board);
                }
            });
            _isPolling = false;
        }

        // ───────────────────────── Camera Setup ─────────────────────────

        private void SetupCamera()
        {
            // Check if main camera exists, otherwise create one
            _gameCamera = Camera.main;
            if (_gameCamera == null)
            {
                var camGO = new GameObject("ChessCamera");
                _gameCamera = camGO.AddComponent<Camera>();
                _gameCamera.tag = "MainCamera";
            }

            _gameCamera.clearFlags = CameraClearFlags.SolidColor;
            _gameCamera.backgroundColor = new Color(0.15f, 0.15f, 0.2f);

            PositionCamera("white");
        }

        private void PositionCamera(string color)
        {
            if (_gameCamera == null) return;

            if (color == "black")
            {
                // View from black's side
                _gameCamera.transform.position = new Vector3(4f, 8f, 12f);
                _gameCamera.transform.LookAt(new Vector3(4f, 0, 4f));
            }
            else
            {
                // View from white's side
                _gameCamera.transform.position = new Vector3(4f, 8f, -4f);
                _gameCamera.transform.LookAt(new Vector3(4f, 0, 4f));
            }
        }
    }
}
