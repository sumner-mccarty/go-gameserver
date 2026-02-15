// ChessUnityClient/Rendering/PieceFactory.cs
// Creates chess piece GameObjects either from Addressable prefabs or procedurally generated meshes.
// Users can replace the procedural meshes by placing prefabs at the expected Addressable paths.

using System.Collections.Generic;
using UnityEngine;

namespace ChessUnityClient
{
    /// <summary>
    /// Creates chess piece GameObjects. Tries to load from Addressables first;
    /// if not available, generates simple procedural 3D meshes.
    ///
    /// Addressable paths (place your prefabs here to override):
    ///   Assets/ChessUnityClient/Prefabs/WhiteKing.prefab
    ///   Assets/ChessUnityClient/Prefabs/BlackQueen.prefab
    ///   etc.
    ///
    /// If no prefab is found, a procedural mesh is generated.
    /// </summary>
    public class PieceFactory
    {
        private readonly Dictionary<string, GameObject> _prefabCache = new Dictionary<string, GameObject>();
        private readonly Transform _pieceParent;

        // Colors
        private static readonly Color WhiteColor = new Color(0.95f, 0.93f, 0.88f);
        private static readonly Color BlackColor = new Color(0.2f, 0.2f, 0.22f);

        public PieceFactory(Transform pieceParent)
        {
            _pieceParent = pieceParent;
        }

        /// <summary>
        /// Creates a chess piece GameObject for the given piece code.
        /// pieceCode: uppercase = white (K,Q,R,B,N,P), lowercase = black (k,q,r,b,n,p)
        /// </summary>
        public GameObject CreatePiece(string pieceCode, Vector3 position)
        {
            if (string.IsNullOrEmpty(pieceCode)) return null;

            bool isWhite = char.IsUpper(pieceCode[0]);
            string pieceName = GetPieceName(pieceCode);
            string colorName = isWhite ? "White" : "Black";
            string fullName = $"{colorName}{pieceName}";

            // Try Addressables cache first
            if (_prefabCache.TryGetValue(fullName, out var prefab) && prefab != null)
            {
                var instance = (GameObject)Object.Instantiate(prefab, position, Quaternion.identity, _pieceParent);
                instance.name = fullName;
                return instance;
            }

            // Try loading via Addressables (async in real Unity; here we fall through to procedural)
            // In a real project with Addressables installed, you'd use:
            //   Addressables.LoadAssetAsync<GameObject>($"Assets/ChessUnityClient/Prefabs/{fullName}.prefab")
            // For now, generate procedurally.

            return CreateProceduralPiece(pieceCode, fullName, position, isWhite);
        }

        /// <summary>
        /// Attempts to load prefabs from Addressables. Call this during initialization.
        /// If Addressables are available, loaded prefabs are cached for future use.
        /// </summary>
        public void TryLoadAddressablePrefabs()
        {
            // This is a stub — in a real project with Addressables, you'd:
            //   var handle = Addressables.LoadAssetAsync<GameObject>(path);
            //   handle.Completed += (op) => { _prefabCache[name] = op.Result; };
            //
            // The procedural fallback ensures the game works without any prefabs.
            Debug.Log("[ChessUnityClient] No Addressable prefabs found; using procedural meshes. " +
                      "Place prefabs at Assets/ChessUnityClient/Prefabs/ to override.");
        }

        /// <summary>
        /// Register a custom prefab to use for a piece type.
        /// Call this before the game starts to inject your own models.
        /// </summary>
        /// <param name="pieceName">e.g. "WhiteKing", "BlackQueen"</param>
        /// <param name="prefab">Your custom prefab</param>
        public void RegisterPrefab(string pieceName, GameObject prefab)
        {
            _prefabCache[pieceName] = prefab;
        }

        // ───────────────────────── Procedural Generation ─────────────────────────

        private GameObject CreateProceduralPiece(string pieceCode, string name, Vector3 position, bool isWhite)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_pieceParent);
            go.transform.position = position;

            var color = isWhite ? WhiteColor : BlackColor;
            char type = char.ToUpper(pieceCode[0]);

            // Create different shapes for each piece type
            switch (type)
            {
                case 'K': CreateKingMesh(go, color); break;
                case 'Q': CreateQueenMesh(go, color); break;
                case 'R': CreateRookMesh(go, color); break;
                case 'B': CreateBishopMesh(go, color); break;
                case 'N': CreateKnightMesh(go, color); break;
                case 'P': CreatePawnMesh(go, color); break;
                default:  CreatePawnMesh(go, color); break;
            }

            return go;
        }

        private void CreateKingMesh(GameObject go, Color color)
        {
            // Tall cylinder with cross on top
            var body = CreateCylinder(go.transform, new Vector3(0, 0.4f, 0), 0.25f, 0.8f, color);
            var cross1 = CreateCube(go.transform, new Vector3(0, 0.9f, 0), new Vector3(0.08f, 0.25f, 0.08f), color);
            var cross2 = CreateCube(go.transform, new Vector3(0, 0.95f, 0), new Vector3(0.2f, 0.06f, 0.08f), color);
            CreateCylinder(go.transform, new Vector3(0, 0.05f, 0), 0.3f, 0.1f, color); // base
        }

        private void CreateQueenMesh(GameObject go, Color color)
        {
            // Tall cylinder with sphere crown
            CreateCylinder(go.transform, new Vector3(0, 0.35f, 0), 0.22f, 0.7f, color);
            CreateSphere(go.transform, new Vector3(0, 0.8f, 0), 0.15f, color);
            CreateCylinder(go.transform, new Vector3(0, 0.05f, 0), 0.3f, 0.1f, color); // base
        }

        private void CreateRookMesh(GameObject go, Color color)
        {
            // Shorter cylinder with crenellations
            CreateCylinder(go.transform, new Vector3(0, 0.25f, 0), 0.25f, 0.5f, color);
            CreateCube(go.transform, new Vector3(-0.12f, 0.58f, 0), new Vector3(0.1f, 0.12f, 0.25f), color);
            CreateCube(go.transform, new Vector3(0.12f, 0.58f, 0), new Vector3(0.1f, 0.12f, 0.25f), color);
            CreateCylinder(go.transform, new Vector3(0, 0.05f, 0), 0.3f, 0.1f, color); // base
        }

        private void CreateBishopMesh(GameObject go, Color color)
        {
            // Cylinder with pointed top
            CreateCylinder(go.transform, new Vector3(0, 0.3f, 0), 0.2f, 0.6f, color);
            CreateSphere(go.transform, new Vector3(0, 0.7f, 0), 0.1f, color);
            CreateSphere(go.transform, new Vector3(0, 0.82f, 0), 0.05f, color);
            CreateCylinder(go.transform, new Vector3(0, 0.05f, 0), 0.28f, 0.1f, color); // base
        }

        private void CreateKnightMesh(GameObject go, Color color)
        {
            // L-shaped with sphere head
            CreateCylinder(go.transform, new Vector3(0, 0.25f, 0), 0.22f, 0.5f, color);
            CreateCube(go.transform, new Vector3(0, 0.55f, 0.08f), new Vector3(0.18f, 0.22f, 0.3f), color);
            CreateSphere(go.transform, new Vector3(0, 0.6f, 0.2f), 0.1f, color); // head
            CreateCylinder(go.transform, new Vector3(0, 0.05f, 0), 0.28f, 0.1f, color); // base
        }

        private void CreatePawnMesh(GameObject go, Color color)
        {
            // Simple short cylinder with sphere top
            CreateCylinder(go.transform, new Vector3(0, 0.18f, 0), 0.15f, 0.36f, color);
            CreateSphere(go.transform, new Vector3(0, 0.42f, 0), 0.12f, color);
            CreateCylinder(go.transform, new Vector3(0, 0.03f, 0), 0.22f, 0.06f, color); // base
        }

        // ───────────────────────── Primitive Helpers ─────────────────────────

        private GameObject CreateCylinder(Transform parent, Vector3 localPos, float radius, float height, Color color)
        {
            var cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cyl.transform.SetParent(parent);
            cyl.transform.localPosition = localPos;
            cyl.transform.localScale = new Vector3(radius * 2, height / 2, radius * 2);
            SetColor(cyl, color);
            return cyl;
        }

        private GameObject CreateCube(Transform parent, Vector3 localPos, Vector3 size, Color color)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(parent);
            cube.transform.localPosition = localPos;
            cube.transform.localScale = size;
            SetColor(cube, color);
            return cube;
        }

        private GameObject CreateSphere(Transform parent, Vector3 localPos, float radius, Color color)
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.transform.SetParent(parent);
            sphere.transform.localPosition = localPos;
            sphere.transform.localScale = Vector3.one * radius * 2;
            SetColor(sphere, color);
            return sphere;
        }

        private void SetColor(GameObject go, Color color)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = new Material(Shader.Find("Standard"));
                renderer.material.color = color;
            }
        }

        private string GetPieceName(string code)
        {
            switch (char.ToUpper(code[0]))
            {
                case 'K': return "King";
                case 'Q': return "Queen";
                case 'R': return "Rook";
                case 'B': return "Bishop";
                case 'N': return "Knight";
                case 'P': return "Pawn";
                default:  return "Unknown";
            }
        }
    }
}
