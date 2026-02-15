// BattlerUnityClient/UI/MenuManager.cs
// Creates and manages all UGUI menus procedurally:
// Main Menu, Army Builder, Lobby/Waiting, and Battle HUD with result screen.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BattlerUnityClient
{
    /// <summary>
    /// Creates and manages the entire UI flow for the battler game.
    /// All UI is generated in code — no prefabs required.
    ///
    /// To replace, place prefabs at:
    ///   Assets/BattlerUnityClient/Prefabs/UI/MainMenu.prefab
    ///   Assets/BattlerUnityClient/Prefabs/UI/ArmyBuilder.prefab
    ///   Assets/BattlerUnityClient/Prefabs/UI/BattleHUD.prefab
    /// </summary>
    public class MenuManager : MonoBehaviour
    {
        // Panels
        private GameObject _mainMenuPanel;
        private GameObject _armyBuilderPanel;
        private GameObject _waitingPanel;
        private GameObject _battleHUDPanel;
        private Canvas _canvas;

        // Main menu elements
        private InputField _serverUrlInput;
        private InputField _playerNameInput;
        private Text _statusText;

        // Army builder elements
        private Text _budgetText;
        private Text _armyListText;
        private ScrollRect _unitCatalogScroll;
        private Transform _unitCatalogContent;

        // Waiting elements
        private Text _waitingText;

        // Battle HUD elements
        private Text _tickText;
        private Text _team1CountText;
        private Text _team2CountText;
        private Text _battleStatusText;
        private GameObject _resultPanel;
        private Text _resultText;

        // Callbacks
        public Action<string, string> OnConnect;       // serverUrl, playerName
        public Action<string> OnCreateSession;          // sessionId
        public Action<string> OnJoinSession;            // sessionId
        public Action<string, float, float> OnAddUnit;  // typeName, x, y
        public Action OnRemoveLastUnit;
        public Action OnClearArmy;
        public Action OnStartBattle;

        // ───────────────────────── Initialization ─────────────────────────

        public void Initialize()
        {
            CreateCanvas();
            CreateMainMenu();
            CreateArmyBuilder();
            CreateWaitingScreen();
            CreateBattleHUD();
            ShowMainMenu();
        }

        // ───────────────────────── Panel Switching ─────────────────────────

        public void ShowMainMenu()
        {
            SetPanels(main: true);
        }

        public void ShowArmyBuilder(BattleManager battleManager)
        {
            SetPanels(army: true);
            PopulateUnitCatalog(battleManager);
            UpdateArmyBuilderUI(battleManager);
        }

        public void ShowWaiting(string message)
        {
            SetPanels(waiting: true);
            if (_waitingText != null) _waitingText.text = message;
        }

        public void ShowBattleHUD()
        {
            SetPanels(battle: true);
            if (_resultPanel != null) _resultPanel.SetActive(false);
        }

        private void SetPanels(bool main = false, bool army = false, bool waiting = false, bool battle = false)
        {
            _mainMenuPanel.SetActive(main);
            _armyBuilderPanel.SetActive(army);
            _waitingPanel.SetActive(waiting);
            _battleHUDPanel.SetActive(battle);
        }

        // ───────────────────────── Status Updates ─────────────────────────

        public void SetMainMenuStatus(string text)
        {
            if (_statusText != null) _statusText.text = text;
        }

        public void SetWaitingText(string text)
        {
            if (_waitingText != null) _waitingText.text = text;
        }

        public void UpdateArmyBuilderUI(BattleManager bm)
        {
            if (_budgetText != null)
                _budgetText.text = $"Budget: {bm.ArmySpent} / {bm.ArmyBudget}";

            if (_armyListText != null)
            {
                var sb = new System.Text.StringBuilder();
                var counts = new Dictionary<string, int>();
                foreach (var u in bm.Army1)
                {
                    if (!counts.ContainsKey(u.type_name)) counts[u.type_name] = 0;
                    counts[u.type_name]++;
                }
                foreach (var kvp in counts)
                    sb.AppendLine($"{kvp.Key}: x{kvp.Value}");
                _armyListText.text = sb.ToString();
            }
        }

        public void UpdateBattleHUD(BattleStateData state, int player1Id)
        {
            if (state == null) return;

            if (_tickText != null)
                _tickText.text = $"Tick: {state.tick}";

            int t1 = 0, t2 = 0;
            if (state.units != null)
            {
                foreach (var u in state.units)
                {
                    if (!u.alive) continue;
                    if (u.owner_id == player1Id) t1++;
                    else t2++;
                }
            }

            if (_team1CountText != null) _team1CountText.text = $"Team 1: {t1}";
            if (_team2CountText != null) _team2CountText.text = $"Team 2: {t2}";

            if (_battleStatusText != null)
                _battleStatusText.text = state.status == "running" ? "BATTLE IN PROGRESS" : "BATTLE ENDED";

            if (state.status == "finished")
            {
                ShowResult(state, player1Id);
            }
        }

        public void ShowResult(BattleStateData state, int player1Id)
        {
            if (_resultPanel == null) return;
            _resultPanel.SetActive(true);

            string result;
            if (state.winner_id == 0)
                result = "DRAW!";
            else if (state.winner_id == player1Id)
                result = "VICTORY!";
            else
                result = "DEFEAT";

            if (_resultText != null)
                _resultText.text = result;
        }

        // ───────────────────────── Canvas ─────────────────────────

        private void CreateCanvas()
        {
            var canvasGO = new GameObject("BattlerUI_Canvas");
            canvasGO.transform.SetParent(transform);
            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGO.AddComponent<GraphicRaycaster>();
        }

        // ───────────────────────── Main Menu ─────────────────────────

        private void CreateMainMenu()
        {
            _mainMenuPanel = CreatePanel("MainMenu", _canvas.transform);

            CreateText(_mainMenuPanel.transform, "MECHA BATTLER", 56, new Vector2(0, 220),
                new Vector2(700, 80), TextAnchor.MiddleCenter, new Color(0.9f, 0.6f, 0.2f));

            CreateText(_mainMenuPanel.transform, "Server URL:", 20, new Vector2(-150, 80),
                new Vector2(200, 30), TextAnchor.MiddleRight);
            _serverUrlInput = CreateInputField(_mainMenuPanel.transform, "http://localhost:3000",
                new Vector2(100, 80), new Vector2(300, 40));

            CreateText(_mainMenuPanel.transform, "Your Name:", 20, new Vector2(-150, 20),
                new Vector2(200, 30), TextAnchor.MiddleRight);
            _playerNameInput = CreateInputField(_mainMenuPanel.transform, "Commander",
                new Vector2(100, 20), new Vector2(300, 40));

            var connectBtn = CreateButton(_mainMenuPanel.transform, "Deploy", new Vector2(0, -50),
                new Vector2(200, 55), new Color(0.8f, 0.5f, 0.1f));
            connectBtn.onClick.AddListener(() =>
            {
                OnConnect?.Invoke(_serverUrlInput.text, _playerNameInput.text);
            });

            _statusText = CreateText(_mainMenuPanel.transform, "", 18, new Vector2(0, -130),
                new Vector2(500, 40), TextAnchor.MiddleCenter);
            _statusText.color = Color.yellow;
        }

        // ───────────────────────── Army Builder ─────────────────────────

        private void CreateArmyBuilder()
        {
            _armyBuilderPanel = CreatePanel("ArmyBuilder", _canvas.transform);

            CreateText(_armyBuilderPanel.transform, "BUILD YOUR ARMY", 36, new Vector2(0, 250),
                new Vector2(500, 60), TextAnchor.MiddleCenter, new Color(0.9f, 0.6f, 0.2f));

            _budgetText = CreateText(_armyBuilderPanel.transform, "Budget: 0 / 2000", 24, new Vector2(0, 200),
                new Vector2(300, 40), TextAnchor.MiddleCenter);

            // Unit catalog (left side)
            CreateText(_armyBuilderPanel.transform, "Unit Types:", 22, new Vector2(-350, 160),
                new Vector2(250, 30), TextAnchor.MiddleLeft);

            // Scroll view for unit catalog
            var scrollGO = new GameObject("UnitCatalogScroll");
            scrollGO.transform.SetParent(_armyBuilderPanel.transform, false);
            var scrollRect = scrollGO.AddComponent<RectTransform>();
            scrollRect.anchoredPosition = new Vector2(-350, -30);
            scrollRect.sizeDelta = new Vector2(350, 400);
            scrollGO.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.2f);
            _unitCatalogScroll = scrollGO.AddComponent<ScrollRect>();

            var contentGO = new GameObject("Content");
            contentGO.transform.SetParent(scrollGO.transform, false);
            var contentRect = contentGO.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.sizeDelta = new Vector2(0, 0);
            var vlg = contentGO.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 5;
            vlg.padding = new RectOffset(5, 5, 5, 5);
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            var csf = contentGO.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _unitCatalogContent = contentGO.transform;
            _unitCatalogScroll.content = contentRect;

            // Army summary (right side)
            CreateText(_armyBuilderPanel.transform, "Your Army:", 22, new Vector2(200, 160),
                new Vector2(250, 30), TextAnchor.MiddleLeft);
            _armyListText = CreateText(_armyBuilderPanel.transform, "", 18, new Vector2(200, -30),
                new Vector2(300, 400), TextAnchor.UpperLeft);

            // Buttons
            var undoBtn = CreateButton(_armyBuilderPanel.transform, "Undo", new Vector2(200, -240),
                new Vector2(120, 40), new Color(0.6f, 0.4f, 0.2f));
            undoBtn.onClick.AddListener(() => OnRemoveLastUnit?.Invoke());

            var clearBtn = CreateButton(_armyBuilderPanel.transform, "Clear", new Vector2(340, -240),
                new Vector2(120, 40), new Color(0.5f, 0.2f, 0.2f));
            clearBtn.onClick.AddListener(() => OnClearArmy?.Invoke());

            var startBtn = CreateButton(_armyBuilderPanel.transform, "START BATTLE", new Vector2(0, -300),
                new Vector2(280, 60), new Color(0.8f, 0.3f, 0.1f));
            startBtn.onClick.AddListener(() => OnStartBattle?.Invoke());

            // Session ID input
            var sessionInput = CreateInputField(_armyBuilderPanel.transform, "battle-1",
                new Vector2(200, 200), new Vector2(200, 35));
            var sessionLabel = CreateText(_armyBuilderPanel.transform, "Session:", 16,
                new Vector2(80, 200), new Vector2(100, 35), TextAnchor.MiddleRight);

            // Wire session actions
            var createBtn = CreateButton(_armyBuilderPanel.transform, "Create", new Vector2(340, 200),
                new Vector2(80, 35), new Color(0.3f, 0.5f, 0.7f));
            createBtn.onClick.AddListener(() => OnCreateSession?.Invoke(sessionInput.text));

            var joinBtn = CreateButton(_armyBuilderPanel.transform, "Join", new Vector2(430, 200),
                new Vector2(80, 35), new Color(0.5f, 0.4f, 0.3f));
            joinBtn.onClick.AddListener(() => OnJoinSession?.Invoke(sessionInput.text));
        }

        private void PopulateUnitCatalog(BattleManager bm)
        {
            // Clear existing
            foreach (Transform child in _unitCatalogContent)
                Destroy(child.gameObject);

            foreach (var unit in bm.UnitCatalog)
            {
                var entryGO = new GameObject($"Unit_{unit.name}");
                entryGO.transform.SetParent(_unitCatalogContent, false);

                var entryRect = entryGO.AddComponent<RectTransform>();
                entryRect.sizeDelta = new Vector2(0, 80);
                var entryLayout = entryGO.AddComponent<LayoutElement>();
                entryLayout.preferredHeight = 80;

                var bg = entryGO.AddComponent<Image>();
                bg.color = new Color(0.2f, 0.2f, 0.25f);

                // Name and cost
                var nameText = CreateText(entryGO.transform, $"{unit.displayName} ({unit.cost}g)",
                    18, new Vector2(10, 20), new Vector2(200, 25), TextAnchor.MiddleLeft);

                // Stats
                var statsText = CreateText(entryGO.transform,
                    $"HP:{unit.health} SPD:{unit.speed:F1} DMG:{unit.attackDamage} RNG:{unit.attackRange:F0}",
                    14, new Vector2(10, -5), new Vector2(300, 20), TextAnchor.MiddleLeft);
                statsText.color = new Color(0.7f, 0.7f, 0.7f);

                // Description
                var descText = CreateText(entryGO.transform, unit.description,
                    12, new Vector2(10, -25), new Vector2(250, 20), TextAnchor.MiddleLeft);
                descText.color = new Color(0.5f, 0.5f, 0.5f);

                // Add button
                var addBtn = CreateButton(entryGO.transform, "+", new Vector2(150, 0),
                    new Vector2(40, 40), new Color(0.3f, 0.6f, 0.3f));
                var unitName = unit.name; // capture for closure
                addBtn.onClick.AddListener(() =>
                {
                    float x = UnityEngine.Random.Range(2f, 18f);
                    float y = UnityEngine.Random.Range(2f, 28f);
                    OnAddUnit?.Invoke(unitName, x, y);
                });
            }
        }

        // ───────────────────────── Waiting Screen ─────────────────────────

        private void CreateWaitingScreen()
        {
            _waitingPanel = CreatePanel("Waiting", _canvas.transform);

            _waitingText = CreateText(_waitingPanel.transform, "Waiting for opponent...",
                32, new Vector2(0, 0), new Vector2(600, 60), TextAnchor.MiddleCenter);

            var backBtn = CreateButton(_waitingPanel.transform, "Cancel", new Vector2(0, -80),
                new Vector2(150, 45), new Color(0.5f, 0.3f, 0.3f));
            backBtn.onClick.AddListener(() => ShowMainMenu());
        }

        // ───────────────────────── Battle HUD ─────────────────────────

        private void CreateBattleHUD()
        {
            _battleHUDPanel = CreatePanel("BattleHUD", _canvas.transform);
            _battleHUDPanel.GetComponent<Image>().color = new Color(0, 0, 0, 0); // transparent

            // Top bar
            var topBar = CreatePanel("TopBar", _battleHUDPanel.transform, new Color(0, 0, 0, 0.7f));
            var topRect = topBar.GetComponent<RectTransform>();
            topRect.anchorMin = new Vector2(0, 1);
            topRect.anchorMax = new Vector2(1, 1);
            topRect.pivot = new Vector2(0.5f, 1);
            topRect.sizeDelta = new Vector2(0, 50);
            topRect.anchoredPosition = Vector2.zero;

            _team1CountText = CreateText(topBar.transform, "Team 1: 0", 22,
                new Vector2(-300, 0), new Vector2(200, 40), TextAnchor.MiddleCenter);
            _team1CountText.color = UnitFactory.Team1Color;

            _battleStatusText = CreateText(topBar.transform, "BATTLE", 26,
                new Vector2(0, 0), new Vector2(300, 40), TextAnchor.MiddleCenter);
            _battleStatusText.color = new Color(0.9f, 0.6f, 0.2f);

            _team2CountText = CreateText(topBar.transform, "Team 2: 0", 22,
                new Vector2(300, 0), new Vector2(200, 40), TextAnchor.MiddleCenter);
            _team2CountText.color = UnitFactory.Team2Color;

            // Bottom bar: tick counter
            var botBar = CreatePanel("BotBar", _battleHUDPanel.transform, new Color(0, 0, 0, 0.5f));
            var botRect = botBar.GetComponent<RectTransform>();
            botRect.anchorMin = new Vector2(0, 0);
            botRect.anchorMax = new Vector2(1, 0);
            botRect.pivot = new Vector2(0.5f, 0);
            botRect.sizeDelta = new Vector2(0, 35);
            botRect.anchoredPosition = Vector2.zero;

            _tickText = CreateText(botBar.transform, "Tick: 0", 16,
                new Vector2(0, 0), new Vector2(200, 30), TextAnchor.MiddleCenter);

            // Result overlay
            _resultPanel = CreatePanel("ResultPanel", _battleHUDPanel.transform, new Color(0, 0, 0, 0.85f));
            _resultText = CreateText(_resultPanel.transform, "", 64, new Vector2(0, 40),
                new Vector2(600, 100), TextAnchor.MiddleCenter);
            _resultText.color = new Color(0.9f, 0.7f, 0.2f);

            var returnBtn = CreateButton(_resultPanel.transform, "Return to Menu", new Vector2(0, -60),
                new Vector2(250, 55), new Color(0.4f, 0.5f, 0.7f));
            returnBtn.onClick.AddListener(ShowMainMenu);
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
            image.color = bgColor ?? new Color(0.08f, 0.08f, 0.12f, 0.97f);
            return panel;
        }

        private Text CreateText(Transform parent, string text, int fontSize, Vector2 position,
            Vector2 size, TextAnchor alignment, Color? color = null)
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
            txt.color = color ?? Color.white;
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
            go.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.25f);

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

            var phGO = new GameObject("Placeholder");
            phGO.transform.SetParent(go.transform, false);
            var phRect = phGO.AddComponent<RectTransform>();
            phRect.anchorMin = Vector2.zero;
            phRect.anchorMax = Vector2.one;
            phRect.sizeDelta = new Vector2(-10, 0);
            var phText = phGO.AddComponent<Text>();
            phText.font = text.font;
            phText.fontSize = 18;
            phText.color = new Color(0.5f, 0.5f, 0.5f);
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
            text.fontSize = 18;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (text.font == null)
                text.font = Font.CreateDynamicFontFromOSFont("Arial", 18);
            return button;
        }
    }
}
