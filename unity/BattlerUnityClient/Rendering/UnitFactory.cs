// BattlerUnityClient/Rendering/UnitFactory.cs
// Creates unit GameObjects procedurally or from Addressable prefabs.
// Each unit type gets a distinct visual appearance.

using System.Collections.Generic;
using UnityEngine;

namespace BattlerUnityClient
{
    /// <summary>
    /// Creates unit and projectile GameObjects. Falls back to procedural meshes
    /// if no Addressable prefabs are found.
    ///
    /// Addressable paths (place your prefabs here to override):
    ///   Assets/BattlerUnityClient/Prefabs/Units/Crawler.prefab
    ///   Assets/BattlerUnityClient/Prefabs/Units/Archer.prefab
    ///   Assets/BattlerUnityClient/Prefabs/Units/Tank.prefab
    ///   Assets/BattlerUnityClient/Prefabs/Units/Artillery.prefab
    ///   Assets/BattlerUnityClient/Prefabs/Units/Scout.prefab
    ///   Assets/BattlerUnityClient/Prefabs/Units/Healer.prefab
    ///   Assets/BattlerUnityClient/Prefabs/Projectile.prefab
    /// </summary>
    public class UnitFactory
    {
        private readonly Dictionary<string, GameObject> _prefabCache = new Dictionary<string, GameObject>();
        private readonly Transform _unitParent;
        private readonly Transform _projectileParent;

        // Team colors
        public static readonly Color Team1Color = new Color(0.2f, 0.5f, 0.9f);  // Blue
        public static readonly Color Team2Color = new Color(0.9f, 0.3f, 0.2f);  // Red

        public UnitFactory(Transform unitParent, Transform projectileParent)
        {
            _unitParent = unitParent;
            _projectileParent = projectileParent;
        }

        /// <summary>
        /// Register a custom prefab for a unit type.
        /// </summary>
        public void RegisterPrefab(string unitTypeName, GameObject prefab)
        {
            _prefabCache[unitTypeName] = prefab;
        }

        /// <summary>
        /// Register a custom projectile prefab.
        /// </summary>
        public void RegisterProjectilePrefab(GameObject prefab)
        {
            _prefabCache["__projectile__"] = prefab;
        }

        /// <summary>
        /// Try to load prefabs from Addressables.
        /// </summary>
        public void TryLoadAddressablePrefabs()
        {
            Debug.Log("[BattlerUnityClient] No Addressable prefabs found; using procedural meshes. " +
                      "Place prefabs at Assets/BattlerUnityClient/Prefabs/Units/ to override.");
        }

        /// <summary>
        /// Creates a unit GameObject for the given unit data.
        /// </summary>
        public GameObject CreateUnit(BattleUnitData unitData, int player1Id)
        {
            bool isTeam1 = unitData.owner_id == player1Id;
            Color teamColor = isTeam1 ? Team1Color : Team2Color;

            // Try cached prefab
            if (_prefabCache.TryGetValue(unitData.type_name, out var prefab) && prefab != null)
            {
                var instance = (GameObject)Object.Instantiate(prefab,
                    new Vector3(unitData.x, 0, unitData.y),
                    Quaternion.identity, _unitParent);
                instance.name = unitData.id;
                TintObject(instance, teamColor);
                return instance;
            }

            // Generate procedurally
            return CreateProceduralUnit(unitData, teamColor);
        }

        /// <summary>
        /// Creates a projectile GameObject.
        /// </summary>
        public GameObject CreateProjectile(ProjectileData data, int player1Id)
        {
            bool isTeam1 = data.owner_id == player1Id;
            Color color = isTeam1 ? new Color(0.4f, 0.7f, 1f) : new Color(1f, 0.5f, 0.3f);

            if (_prefabCache.TryGetValue("__projectile__", out var prefab) && prefab != null)
            {
                var instance = (GameObject)Object.Instantiate(prefab,
                    new Vector3(data.x, 1f, data.y),
                    Quaternion.identity, _projectileParent);
                instance.name = data.id;
                return instance;
            }

            // Procedural projectile: small glowing sphere
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = data.id;
            go.transform.SetParent(_projectileParent);
            go.transform.position = new Vector3(data.x, 1f, data.y);
            go.transform.localScale = Vector3.one * (data.splash > 0 ? 0.4f : 0.2f);

            var renderer = go.GetComponent<Renderer>();
            var mat = new Material(Shader.Find("Standard"));
            mat.color = color;
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(color.r * 2f, color.g * 2f, color.b * 2f));
            renderer.material = mat;

            // Remove collider
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);

            return go;
        }

        // ───────────────────────── Procedural Unit Creation ─────────────────────────

        private GameObject CreateProceduralUnit(BattleUnitData data, Color teamColor)
        {
            var go = new GameObject(data.id);
            go.transform.SetParent(_unitParent);
            go.transform.position = new Vector3(data.x, 0, data.y);

            switch (data.type_name)
            {
                case "crawler":  CreateCrawlerMesh(go, teamColor); break;
                case "archer":   CreateArcherMesh(go, teamColor); break;
                case "tank":     CreateTankMesh(go, teamColor); break;
                case "artillery":CreateArtilleryMesh(go, teamColor); break;
                case "scout":    CreateScoutMesh(go, teamColor); break;
                case "healer":   CreateHealerMesh(go, teamColor); break;
                default:         CreateCrawlerMesh(go, teamColor); break;
            }

            return go;
        }

        private void CreateCrawlerMesh(GameObject go, Color color)
        {
            // Small, low, fast-looking: flat capsule
            var body = CreatePrimitive(go.transform, PrimitiveType.Capsule,
                new Vector3(0, 0.2f, 0), new Vector3(0.3f, 0.2f, 0.3f), color);
            // Claws
            CreatePrimitive(go.transform, PrimitiveType.Cube,
                new Vector3(0.2f, 0.15f, 0.15f), new Vector3(0.15f, 0.1f, 0.05f), color);
            CreatePrimitive(go.transform, PrimitiveType.Cube,
                new Vector3(-0.2f, 0.15f, 0.15f), new Vector3(0.15f, 0.1f, 0.05f), color);
        }

        private void CreateArcherMesh(GameObject go, Color color)
        {
            // Slim upright figure with a "bow"
            CreatePrimitive(go.transform, PrimitiveType.Capsule,
                new Vector3(0, 0.4f, 0), new Vector3(0.2f, 0.4f, 0.2f), color);
            // Bow (thin cube arc)
            CreatePrimitive(go.transform, PrimitiveType.Cube,
                new Vector3(0.25f, 0.5f, 0), new Vector3(0.05f, 0.5f, 0.05f),
                Color.Lerp(color, Color.white, 0.3f));
        }

        private void CreateTankMesh(GameObject go, Color color)
        {
            // Large blocky body
            CreatePrimitive(go.transform, PrimitiveType.Cube,
                new Vector3(0, 0.3f, 0), new Vector3(0.7f, 0.4f, 0.9f), color);
            // Turret
            CreatePrimitive(go.transform, PrimitiveType.Cylinder,
                new Vector3(0, 0.6f, 0), new Vector3(0.35f, 0.15f, 0.35f), color);
            // Barrel
            CreatePrimitive(go.transform, PrimitiveType.Cylinder,
                new Vector3(0, 0.6f, 0.4f), new Vector3(0.08f, 0.3f, 0.08f),
                Color.Lerp(color, Color.black, 0.3f));
            var barrel = go.transform.GetChild(go.transform.childCount - 1);
            barrel.localRotation = Quaternion.Euler(90, 0, 0);
        }

        private void CreateArtilleryMesh(GameObject go, Color color)
        {
            // Wide platform base
            CreatePrimitive(go.transform, PrimitiveType.Cube,
                new Vector3(0, 0.15f, 0), new Vector3(0.8f, 0.3f, 0.8f), color);
            // Cannon barrel (angled up)
            var barrel = CreatePrimitive(go.transform, PrimitiveType.Cylinder,
                new Vector3(0, 0.5f, 0.2f), new Vector3(0.12f, 0.5f, 0.12f),
                Color.Lerp(color, Color.black, 0.3f));
            barrel.transform.localRotation = Quaternion.Euler(45, 0, 0);
            // Stabilizers
            CreatePrimitive(go.transform, PrimitiveType.Cube,
                new Vector3(0.35f, 0.1f, -0.3f), new Vector3(0.1f, 0.1f, 0.4f), color);
            CreatePrimitive(go.transform, PrimitiveType.Cube,
                new Vector3(-0.35f, 0.1f, -0.3f), new Vector3(0.1f, 0.1f, 0.4f), color);
        }

        private void CreateScoutMesh(GameObject go, Color color)
        {
            // Tiny, sleek
            CreatePrimitive(go.transform, PrimitiveType.Sphere,
                new Vector3(0, 0.15f, 0), new Vector3(0.2f, 0.15f, 0.25f), color);
            // "Wings" for speed appearance
            CreatePrimitive(go.transform, PrimitiveType.Cube,
                new Vector3(0.2f, 0.15f, -0.05f), new Vector3(0.2f, 0.02f, 0.12f),
                Color.Lerp(color, Color.white, 0.5f));
            CreatePrimitive(go.transform, PrimitiveType.Cube,
                new Vector3(-0.2f, 0.15f, -0.05f), new Vector3(0.2f, 0.02f, 0.12f),
                Color.Lerp(color, Color.white, 0.5f));
        }

        private void CreateHealerMesh(GameObject go, Color color)
        {
            // Sphere body with a cross on top
            CreatePrimitive(go.transform, PrimitiveType.Sphere,
                new Vector3(0, 0.3f, 0), new Vector3(0.35f, 0.35f, 0.35f), color);
            // Green cross
            Color healColor = new Color(0.1f, 0.9f, 0.3f);
            CreatePrimitive(go.transform, PrimitiveType.Cube,
                new Vector3(0, 0.6f, 0), new Vector3(0.06f, 0.2f, 0.06f), healColor);
            CreatePrimitive(go.transform, PrimitiveType.Cube,
                new Vector3(0, 0.6f, 0), new Vector3(0.2f, 0.06f, 0.06f), healColor);
            // Aura ring
            CreatePrimitive(go.transform, PrimitiveType.Cylinder,
                new Vector3(0, 0.05f, 0), new Vector3(0.6f, 0.02f, 0.6f),
                new Color(0.1f, 0.9f, 0.3f, 0.3f));
        }

        // ───────────────────────── Helpers ─────────────────────────

        private GameObject CreatePrimitive(Transform parent, PrimitiveType type,
            Vector3 localPos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.transform.SetParent(parent);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;

            var renderer = go.GetComponent<Renderer>();
            var mat = new Material(Shader.Find("Standard"));
            mat.color = color;
            renderer.material = mat;

            // Remove colliders to avoid physics interference
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);

            return go;
        }

        private void TintObject(GameObject go, Color color)
        {
            foreach (var renderer in go.GetComponentsInChildren<Renderer>())
            {
                var mat = new Material(renderer.material);
                mat.color = Color.Lerp(mat.color, color, 0.5f);
                renderer.material = mat;
            }
        }
    }
}
