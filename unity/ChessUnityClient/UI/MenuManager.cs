// ChessUnityClient/UI/MenuManager.cs
// Creates and manages all UGUI menus procedurally: main menu, lobby, and in-game HUD.
// If prefabs are available at the expected Addressable paths, they are used instead.

using System;
using UnityEngine;
using UnityEngine.UI;

namespace ChessUnityClient
{
    /// <summary>
    /// Creates and manages the UI flow for the chess game.
    /// All UI is generated in code — no prefabs required.
    ///
    /// To replace with your own UI, place prefabs at:
    ///   Assets/ChessUnityClient/Prefabs/UI/MainMenu.prefab
    ///   Assets/ChessUnityClient/Prefabs/UI/LobbyMenu.prefab
    ///   Assets/ChessUnityClient/Prefabs/UI/GameHUD.prefab
    /// </summary>
    public class MenuManager : MonoBehaviour
    {
        // Panels
        private GameObject _mainMenuPanel;
        private GameObject _lobbyPanel;
        private GameObject _gameHUDPanel;
        private Canvas _canvas;

        // Main menu elements
        private InputField _serverUrlInput;
        private InputField _playerNameInput;
        private Text _statusText;

        // Lobby elements
        private InputField _sessionIdInput;
        private Text _lobbyStatusText;
        private Button _createSessionBtn;
        private Button _joinSessionBtn;

        // Game HUD elements
        private Text _turnText;
        private Text _statusGameText;
        private Text _moveHistoryText;
        private Button _resignBtn;
        private Text _resultText;
        private GameObject _resultPanel;

        // Callbacks
        public Action<string, string> OnConnect;      // serverUrl, playerName
        public Action<string> OnCreateSession;         // sessionId
        public Action<string> OnJoinSession;           // sessionId
        public Action OnResign;

        /// <summary>
        /// Initialize the menu system. Creates a Canvas and all menu panels.
        /// </summary>
        public void Initialize()
        {
            CreateCanvas();
            CreateMainMenu();
            CreateLobbyMenu();
            CreateGameHUD();
            ShowMainMenu();
        }

        // ───────────────────────── Panel Switching ─────────────────────────

        public void ShowMainMenu()
        {
            _mainMenuPanel.SetActive(true);
            _lobbyPanel.SetActive(false);
            _gameHUDPanel.SetActive(false);
        }

        public void ShowLobby()
        {
            _mainMenuPanel.SetActive(false);
            _lobbyPanel.SetActive(true);
            _gameHUDPanel.SetActive(false);
        }

        public void ShowGameHUD()
        {
            _mainMenuPanel.SetActive(false);
            _lobbyPanel.SetActive(false);
            _gameHUDPanel.SetActive(true);
            if (_resultPanel != null) _resultPanel.SetActive(false);
        }

        // ───────────────────────── Status Updates ─────────────────────────

        public void SetMainMenuStatus(string text)
        {
            if (_statusText != null) _statusText.text = text;
        }

        public void SetLobbyStatus(string text)
        {
            if (_lobbyStatusText != null) _lobbyStatusText.text = text;
        }

        public void UpdateGameHUD(ChessBoard board)
        {
            if (_turnText != null)
            {
                string turnDisplay = board.Turn == "white" ? "White's Turn" : "Black's Turn";
                _turnText.text = turnDisplay;
            }

            if (_statusGameText != null)
            {
                string statusDisplay = board.Status;
                if (board.Status == "check") statusDisplay = "CHECK!";
                else if (board.Status == "checkmate") statusDisplay = "CHECKMATE!";
                else if (board.Status == "stalemate") statusDisplay = "STALEMATE";
                else if (board.Status == "resigned") statusDisplay = "RESIGNED";
                else statusDisplay = $"Move #{board.MoveCount}";
                _statusGameText.text = statusDisplay;
            }

            if (_moveHistoryText != null)
            {
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < board.MoveHistory.Count; i++)
                {
                    if (i % 2 == 0) sb.Append($"{(i / 2) + 1}. ");
                    sb.Append(board.MoveHistory[i]);
                    sb.Append(i % 2 == 0 ? " " : "\n");
                }
                _moveHistoryText.text = sb.ToString();
            }

            // Show resign button only during active game
            if (_resignBtn != null)
                _resignBtn.gameObject.SetActive(board.IsGameActive);

            // Show result if game is over
            if (!board.IsGameActive && board.MoveCount > 0)
            {
                ShowResult(board.Status);
            }
        }

        public void ShowResult(string result)
        {
            if (_resultPanel != null)
            {
                _resultPanel.SetActive(true);
                if (_resultText != null)
                {
                    string display = result;
                    if (result == "checkmate") display = "Checkmate!";
                    else if (result == "stalemate") display = "Stalemate — Draw";
                    else if (result == "resigned") display = "Player Resigned";
                    else if (result == "draw") display = "Draw";
                    _resultText.text = display;
                }
            }
        }

        // ───────────────────────── Canvas ─────────────────────────

        private void CreateCanvas()
        {
            var canvasGO = new GameObject("ChessUI_Canvas");
            canvasGO.transform.SetParent(transform);
            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;

            canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGO.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
            canvasGO.AddComponent<GraphicRaycaster>();
        }

        // ───────────────────────── Main Menu ─────────────────────────

        private void CreateMainMenu()
        {
            _mainMenuPanel = CreatePanel("MainMenu", _canvas.transform);

            // Title
            CreateText(_mainMenuPanel.transform, "Chess Online", 48, new Vector2(0, 200),
                new Vector2(600, 80), TextAnchor.MiddleCenter);

            // Server URL input
            CreateText(_mainMenuPanel.transform, "Server URL:", 20, new Vector2(-150, 80),
                new Vector2(200, 30), TextAnchor.MiddleRight);
            _serverUrlInput = CreateInputField(_mainMenuPanel.transform, "http://localhost:3000",
                new Vector2(100, 80), new Vector2(300, 40));

            // Player name input
            CreateText(_mainMenuPanel.transform, "Your Name:", 20, new Vector2(-150, 20),
                new Vector2(200, 30), TextAnchor.MiddleRight);
            _playerNameInput = CreateInputField(_mainMenuPanel.transform, "Player",
                new Vector2(100, 20), new Vector2(300, 40));

            // Connect button
            var connectBtn = CreateButton(_mainMenuPanel.transform, "Connect", new Vector2(0, -50),
                new Vector2(200, 50), new Color(0.2f, 0.6f, 0.3f));
            connectBtn.onClick.AddListener(() =>
            {
                OnConnect?.Invoke(_serverUrlInput.text, _playerNameInput.text);
            });

            // Status text
            _statusText = CreateText(_mainMenuPanel.transform, "", 18, new Vector2(0, -120),
                new Vector2(500, 40), TextAnchor.MiddleCenter);
            _statusText.color = Color.yellow;
        }

        // ───────────────────────── Lobby ─────────────────────────

        private void CreateLobbyMenu()
        {
            _lobbyPanel = CreatePanel("Lobby", _canvas.transform);

            // Title
            CreateText(_lobbyPanel.transform, "Game Lobby", 36, new Vector2(0, 200),
                new Vector2(400, 60), TextAnchor.MiddleCenter);

            // Session ID input
            CreateText(_lobbyPanel.transform, "Session ID:", 20, new Vector2(-150, 80),
                new Vector2(200, 30), TextAnchor.MiddleRight);
            _sessionIdInput = CreateInputField(_lobbyPanel.transform, "chess-match-1",
                new Vector2(100, 80), new Vector2(300, 40));

            // Create Session button
            _createSessionBtn = CreateButton(_lobbyPanel.transform, "Create Game", new Vector2(-110, 10),
                new Vector2(200, 50), new Color(0.2f, 0.5f, 0.7f));
            _createSessionBtn.onClick.AddListener(() =>
            {
                OnCreateSession?.Invoke(_sessionIdInput.text);
            });

            // Join Session button
            _joinSessionBtn = CreateButton(_lobbyPanel.transform, "Join Game", new Vector2(110, 10),
                new Vector2(200, 50), new Color(0.6f, 0.4f, 0.2f));
            _joinSessionBtn.onClick.AddListener(() =>
            {
                OnJoinSession?.Invoke(_sessionIdInput.text);
            });

            // Status
            _lobbyStatusText = CreateText(_lobbyPanel.transform, "Enter a session ID to create or join a game.",
                18, new Vector2(0, -70), new Vector2(500, 60), TextAnchor.MiddleCenter);
            _lobbyStatusText.color = Color.yellow;

            // Back button
            var backBtn = CreateButton(_lobbyPanel.transform, "Back", new Vector2(0, -150),
                new Vector2(150, 40), new Color(0.5f, 0.5f, 0.5f));
            backBtn.onClick.AddListener(ShowMainMenu);
        }

        // ───────────────────────── Game HUD ─────────────────────────

        private void CreateGameHUD()
        {
            _gameHUDPanel = CreatePanel("GameHUD", _canvas.transform);

            // Remove background image for HUD (should be transparent)
            var img = _gameHUDPanel.GetComponent<Image>();
            if (img != null) img.color = new Color(0, 0, 0, 0);

            // Top bar: turn indicator and status
            var topBar = CreatePanel("TopBar", _gameHUDPanel.transform, new Color(0, 0, 0, 0.6f));
            var topRect = topBar.GetComponent<RectTransform>();
            topRect.anchorMin = new Vector2(0, 1);
            topRect.anchorMax = new Vector2(1, 1);
            topRect.pivot = new Vector2(0.5f, 1);
            topRect.sizeDelta = new Vector2(0, 60);
            topRect.anchoredPosition = Vector2.zero;

            _turnText = CreateText(topBar.transform, "White's Turn", 28, Vector2.zero,
                new Vector2(300, 50), TextAnchor.MiddleCenter);

            _statusGameText = CreateText(topBar.transform, "", 20, new Vector2(300, 0),
                new Vector2(300, 50), TextAnchor.MiddleCenter);

            // Right panel: move history
            var historyPanel = CreatePanel("HistoryPanel", _gameHUDPanel.transform, new Color(0, 0, 0, 0.5f));
            var histRect = historyPanel.GetComponent<RectTransform>();
            histRect.anchorMin = new Vector2(1, 0);
            histRect.anchorMax = new Vector2(1, 1);
            histRect.pivot = new Vector2(1, 0.5f);
            histRect.sizeDelta = new Vector2(250, 0);
            histRect.anchoredPosition = Vector2.zero;

            CreateText(historyPanel.transform, "Moves", 22, new Vector2(0, -20),
                new Vector2(230, 30), TextAnchor.MiddleCenter);

            _moveHistoryText = CreateText(historyPanel.transform, "", 16, new Vector2(0, -200),
                new Vector2(220, 350), TextAnchor.UpperLeft);

            // Resign button (bottom center)
            _resignBtn = CreateButton(_gameHUDPanel.transform, "Resign", new Vector2(0, 50),
                new Vector2(150, 40), new Color(0.7f, 0.2f, 0.2f));
            var resignRect = _resignBtn.GetComponent<RectTransform>();
            resignRect.anchorMin = new Vector2(0.5f, 0);
            resignRect.anchorMax = new Vector2(0.5f, 0);
            resignRect.pivot = new Vector2(0.5f, 0);
            _resignBtn.onClick.AddListener(() => OnResign?.Invoke());

            // Result overlay (hidden initially)
            _resultPanel = CreatePanel("ResultPanel", _gameHUDPanel.transform, new Color(0, 0, 0, 0.8f));
            _resultText = CreateText(_resultPanel.transform, "", 48, new Vector2(0, 30),
                new Vector2(500, 80), TextAnchor.MiddleCenter);
            var returnBtn = CreateButton(_resultPanel.transform, "Return to Lobby", new Vector2(0, -60),
                new Vector2(250, 50), new Color(0.3f, 0.5f, 0.7f));
            returnBtn.onClick.AddListener(ShowLobby);
            _resultPanel.SetActive(false);
        }

        // ───────────────────────── UI Factory Helpers ─────────────────────────

        private GameObject CreatePanel(string name, Transform parent, Color? bgColor = null)
        {
            var panel = new GameObject(name);
            panel.transform.SetParent(parent, false);

            var rect = panel.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;

            var image = panel.AddComponent<Image>();
            image.color = bgColor ?? new Color(0.1f, 0.1f, 0.15f, 0.95f);

            return panel;
        }

        private Text CreateText(Transform parent, string text, int fontSize, Vector2 position,
            Vector2 size, TextAnchor alignment)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var txt = go.AddComponent<Text>();
            txt.text = text;
            txt.fontSize = fontSize;
            txt.alignment = alignment;
            txt.color = Color.white;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (txt.font == null)
                txt.font = Font.CreateDynamicFontFromOSFont("Arial", fontSize);

            return txt;
        }

        private InputField CreateInputField(Transform parent, string placeholder, Vector2 position, Vector2 size)
        {
            var go = new GameObject("InputField");
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var image = go.AddComponent<Image>();
            image.color = new Color(0.2f, 0.2f, 0.25f);

            // Text child
            var textGO = new GameObject("Text");
            textGO.transform.SetParent(go.transform, false);
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = new Vector2(-10, 0);
            var text = textGO.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (text.font == null)
                text.font = Font.CreateDynamicFontFromOSFont("Arial", 18);
            text.fontSize = 18;
            text.color = Color.white;
            text.supportRichText = false;

            // Placeholder child
            var placeholderGO = new GameObject("Placeholder");
            placeholderGO.transform.SetParent(go.transform, false);
            var phRect = placeholderGO.AddComponent<RectTransform>();
            phRect.anchorMin = Vector2.zero;
            phRect.anchorMax = Vector2.one;
            phRect.sizeDelta = new Vector2(-10, 0);
            var phText = placeholderGO.AddComponent<Text>();
            phText.font = text.font;
            phText.fontSize = 18;
            phText.color = new Color(0.6f, 0.6f, 0.6f);
            phText.text = placeholder;
            phText.fontStyle = FontStyle.Italic;

            var input = go.AddComponent<InputField>();
            input.textComponent = text;
            input.placeholder = phText;
            input.text = placeholder;

            return input;
        }

        private Button CreateButton(Transform parent, string label, Vector2 position, Vector2 size, Color color)
        {
            var go = new GameObject("Button_" + label);
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var image = go.AddComponent<Image>();
            image.color = color;

            var button = go.AddComponent<Button>();
            button.targetGraphic = image;

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(go.transform, false);
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            var text = textGO.AddComponent<Text>();
            text.text = label;
            text.fontSize = 20;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (text.font == null)
                text.font = Font.CreateDynamicFontFromOSFont("Arial", 20);

            return button;
        }
    }
}
