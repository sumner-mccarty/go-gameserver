// ChessUnityClient/Rendering/BoardRenderer.cs
// Renders the chess board and pieces in 3D space.
// Creates a procedural 8x8 board with alternating colors and manages piece GameObjects.

using System.Collections.Generic;
using UnityEngine;

namespace ChessUnityClient
{
    /// <summary>
    /// Renders the chess board and pieces. Creates all visuals procedurally
    /// if no prefabs are provided.
    ///
    /// Board occupies X=[0..8], Z=[0..8] in world space.
    /// Row 0 (white's side) is at Z=0, Row 7 is at Z=7.
    /// </summary>
    public class BoardRenderer : MonoBehaviour
    {
        [Header("Customization")]
        [Tooltip("Override the light square color")]
        public Color LightSquareColor = new Color(0.93f, 0.87f, 0.73f);

        [Tooltip("Override the dark square color")]
        public Color DarkSquareColor = new Color(0.47f, 0.30f, 0.17f);

        [Tooltip("Color for selected square highlight")]
        public Color SelectedColor = new Color(0.2f, 0.8f, 0.2f, 0.7f);

        [Tooltip("Color for valid move target highlight")]
        public Color MoveTargetColor = new Color(0.2f, 0.5f, 0.9f, 0.5f);

        [Tooltip("Height of the board squares")]
        public float SquareHeight = 0.1f;

        private GameObject[,] _boardSquares = new GameObject[8, 8];
        private Dictionary<string, GameObject> _pieceObjects = new Dictionary<string, GameObject>();
        private PieceFactory _pieceFactory;
        private Transform _boardParent;
        private Transform _piecesParent;
        private GameObject _selectedHighlight;

        /// <summary>
        /// Initialize the board renderer.
        /// </summary>
        public void Initialize()
        {
            _boardParent = new GameObject("ChessBoard").transform;
            _boardParent.SetParent(transform);
            _boardParent.localPosition = Vector3.zero;

            _piecesParent = new GameObject("Pieces").transform;
            _piecesParent.SetParent(transform);
            _piecesParent.localPosition = Vector3.zero;

            _pieceFactory = new PieceFactory(_piecesParent);
            _pieceFactory.TryLoadAddressablePrefabs();

            CreateBoard();
        }

        /// <summary>
        /// Register a custom prefab for a specific piece type.
        /// Call before UpdatePieces to use your own 3D models.
        /// </summary>
        /// <param name="pieceName">e.g. "WhiteKing", "BlackPawn"</param>
        /// <param name="prefab">Your custom prefab</param>
        public void RegisterPiecePrefab(string pieceName, GameObject prefab)
        {
            _pieceFactory?.RegisterPrefab(pieceName, prefab);
        }

        /// <summary>
        /// Updates piece positions and creates/destroys pieces to match the board state.
        /// </summary>
        public void UpdatePieces(ChessBoard board)
        {
            // Track which piece keys are still alive
            var activeKeys = new HashSet<string>();

            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    string piece = board.GetPieceAt(row, col);
                    string key = $"{row}_{col}";
                    Vector3 worldPos = SquareToWorld(row, col);

                    if (string.IsNullOrEmpty(piece))
                    {
                        // No piece here — remove if one exists
                        if (_pieceObjects.TryGetValue(key, out var existing))
                        {
                            Destroy(existing);
                            _pieceObjects.Remove(key);
                        }
                        continue;
                    }

                    activeKeys.Add(key);

                    if (_pieceObjects.TryGetValue(key, out var obj))
                    {
                        // Check if the piece type changed (capture + new piece)
                        string expectedName = GetExpectedPieceName(piece);
                        if (obj.name != expectedName)
                        {
                            Destroy(obj);
                            _pieceObjects[key] = _pieceFactory.CreatePiece(piece, worldPos);
                        }
                        else
                        {
                            // Smoothly move to position (frame-rate independent)
                            obj.transform.position = Vector3.Lerp(obj.transform.position, worldPos, Time.deltaTime * 8f);
                        }
                    }
                    else
                    {
                        // Create new piece
                        _pieceObjects[key] = _pieceFactory.CreatePiece(piece, worldPos);
                    }
                }
            }

            // Clean up pieces that are no longer on the board
            var toRemove = new List<string>();
            foreach (var kvp in _pieceObjects)
            {
                if (!activeKeys.Contains(kvp.Key))
                {
                    Destroy(kvp.Value);
                    toRemove.Add(kvp.Key);
                }
            }
            foreach (var key in toRemove)
                _pieceObjects.Remove(key);
        }

        /// <summary>
        /// Highlights a square (e.g. when selected).
        /// </summary>
        public void HighlightSquare(int row, int col, bool selected)
        {
            if (row < 0 || row > 7 || col < 0 || col > 7) return;

            if (_selectedHighlight == null)
            {
                _selectedHighlight = GameObject.CreatePrimitive(PrimitiveType.Quad);
                _selectedHighlight.name = "SquareHighlight";
                _selectedHighlight.transform.SetParent(_boardParent);
                _selectedHighlight.transform.rotation = Quaternion.Euler(90, 0, 0);
                var renderer = _selectedHighlight.GetComponent<Renderer>();
                renderer.material = new Material(Shader.Find("Standard"));
                renderer.material.color = SelectedColor;
                // Remove collider so it doesn't interfere with clicks
                var collider = _selectedHighlight.GetComponent<Collider>();
                if (collider != null) Destroy(collider);
            }

            if (selected)
            {
                _selectedHighlight.SetActive(true);
                _selectedHighlight.transform.position = SquareToWorld(row, col) + Vector3.up * 0.01f;
            }
            else
            {
                _selectedHighlight.SetActive(false);
            }
        }

        /// <summary>
        /// Clears the selection highlight.
        /// </summary>
        public void ClearHighlight()
        {
            if (_selectedHighlight != null)
                _selectedHighlight.SetActive(false);
        }

        /// <summary>
        /// Converts a square (row, col) to world position.
        /// </summary>
        public Vector3 SquareToWorld(int row, int col)
        {
            return new Vector3(col + 0.5f, SquareHeight + 0.01f, row + 0.5f);
        }

        /// <summary>
        /// Converts a world position (from raycast) to (row, col).
        /// Returns (-1, -1) if outside the board.
        /// </summary>
        public (int row, int col) WorldToSquare(Vector3 worldPos)
        {
            int col = Mathf.FloorToInt(worldPos.x);
            int row = Mathf.FloorToInt(worldPos.z);
            if (row < 0 || row > 7 || col < 0 || col > 7)
                return (-1, -1);
            return (row, col);
        }

        // ───────────────────────── Board Creation ─────────────────────────

        private void CreateBoard()
        {
            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    bool isLight = (row + col) % 2 == 0;
                    var square = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    square.name = $"Square_{ChessBoard.IndexToAlgebraic(row, col)}";
                    square.transform.SetParent(_boardParent);
                    square.transform.position = new Vector3(col + 0.5f, SquareHeight / 2f, row + 0.5f);
                    square.transform.localScale = new Vector3(1f, SquareHeight, 1f);

                    var renderer = square.GetComponent<Renderer>();
                    renderer.material = new Material(Shader.Find("Standard"));
                    renderer.material.color = isLight ? LightSquareColor : DarkSquareColor;

                    _boardSquares[row, col] = square;
                }
            }

            // Add file/rank labels
            CreateLabels();

            // Add border frame
            CreateBorder();
        }

        private void CreateLabels()
        {
            // File labels (a-h) along the front
            for (int col = 0; col < 8; col++)
            {
                CreateTextLabel($"{(char)('a' + col)}", new Vector3(col + 0.5f, 0.01f, -0.3f));
            }
            // Rank labels (1-8) along the left
            for (int row = 0; row < 8; row++)
            {
                CreateTextLabel($"{row + 1}", new Vector3(-0.3f, 0.01f, row + 0.5f));
            }
        }

        private void CreateTextLabel(string text, Vector3 position)
        {
            var go = new GameObject($"Label_{text}");
            go.transform.SetParent(_boardParent);
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(90, 0, 0);

            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.characterSize = 0.15f;
            tm.fontSize = 48;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = Color.white;
        }

        private void CreateBorder()
        {
            var border = new GameObject("BoardBorder");
            border.transform.SetParent(_boardParent);
            border.transform.localPosition = Vector3.zero;

            float thickness = 0.05f;
            Color borderColor = new Color(0.3f, 0.18f, 0.1f);

            // Four edges
            CreateBorderEdge(border.transform, new Vector3(4f, SquareHeight / 2f, -thickness / 2f), new Vector3(8.2f, SquareHeight, thickness), borderColor);
            CreateBorderEdge(border.transform, new Vector3(4f, SquareHeight / 2f, 8f + thickness / 2f), new Vector3(8.2f, SquareHeight, thickness), borderColor);
            CreateBorderEdge(border.transform, new Vector3(-thickness / 2f, SquareHeight / 2f, 4f), new Vector3(thickness, SquareHeight, 8.2f), borderColor);
            CreateBorderEdge(border.transform, new Vector3(8f + thickness / 2f, SquareHeight / 2f, 4f), new Vector3(thickness, SquareHeight, 8.2f), borderColor);
        }

        private void CreateBorderEdge(Transform parent, Vector3 pos, Vector3 scale, Color color)
        {
            var edge = GameObject.CreatePrimitive(PrimitiveType.Cube);
            edge.name = "BorderEdge";
            edge.transform.SetParent(parent);
            edge.transform.position = pos;
            edge.transform.localScale = scale;
            var renderer = edge.GetComponent<Renderer>();
            renderer.material = new Material(Shader.Find("Standard"));
            renderer.material.color = color;
            // Remove collider
            var collider = edge.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
        }

        private string GetExpectedPieceName(string pieceCode)
        {
            bool isWhite = char.IsUpper(pieceCode[0]);
            string colorName = isWhite ? "White" : "Black";
            string pieceName;
            switch (char.ToUpper(pieceCode[0]))
            {
                case 'K': pieceName = "King"; break;
                case 'Q': pieceName = "Queen"; break;
                case 'R': pieceName = "Rook"; break;
                case 'B': pieceName = "Bishop"; break;
                case 'N': pieceName = "Knight"; break;
                case 'P': pieceName = "Pawn"; break;
                default:  pieceName = "Unknown"; break;
            }
            return $"{colorName}{pieceName}";
        }
    }
}
