// BattlerUnityClient/Rendering/BattlefieldRenderer.cs
// Renders the battlefield terrain, unit GameObjects, health bars, and projectiles.
// Updates every frame based on the latest server state, with interpolation.

using System.Collections.Generic;
using UnityEngine;

namespace BattlerUnityClient
{
    /// <summary>
    /// Renders the battle: terrain, units, health bars, projectiles.
    /// Updates from BattleStateData received via WebSocket.
    /// </summary>
    public class BattlefieldRenderer : MonoBehaviour
    {
        [Header("Battlefield Size")]
        [Tooltip("Width of the battlefield (X axis)")]
        public float FieldWidth = 100f;

        [Tooltip("Depth of the battlefield (Z axis)")]
        public float FieldDepth = 30f;

        [Header("Visual Settings")]
        public Color GroundColor = new Color(0.3f, 0.5f, 0.2f);
        public float HealthBarHeight = 1.5f;
        public float HealthBarWidth = 0.8f;
        public float UnitInterpolationSpeed = 10f;

        private Transform _terrainParent;
        private Transform _unitsParent;
        private Transform _projectilesParent;
        private Transform _healthBarsParent;

        private UnitFactory _unitFactory;
        private int _player1Id;

        // Tracked objects
        private readonly Dictionary<string, GameObject> _unitObjects = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, Vector3> _unitTargetPositions = new Dictionary<string, Vector3>();
        private readonly Dictionary<string, GameObject> _healthBarObjects = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, GameObject> _projectileObjects = new Dictionary<string, GameObject>();

        /// <summary>
        /// Initialize the battlefield renderer.
        /// </summary>
        public void Initialize(int player1Id)
        {
            _player1Id = player1Id;

            _terrainParent = new GameObject("Terrain").transform;
            _terrainParent.SetParent(transform);

            _unitsParent = new GameObject("Units").transform;
            _unitsParent.SetParent(transform);

            _projectilesParent = new GameObject("Projectiles").transform;
            _projectilesParent.SetParent(transform);

            _healthBarsParent = new GameObject("HealthBars").transform;
            _healthBarsParent.SetParent(transform);

            _unitFactory = new UnitFactory(_unitsParent, _projectilesParent);
            _unitFactory.TryLoadAddressablePrefabs();

            CreateTerrain();
        }

        /// <summary>
        /// Register a custom unit prefab.
        /// </summary>
        public void RegisterUnitPrefab(string typeName, GameObject prefab)
        {
            _unitFactory?.RegisterPrefab(typeName, prefab);
        }

        /// <summary>
        /// Register a custom projectile prefab.
        /// </summary>
        public void RegisterProjectilePrefab(GameObject prefab)
        {
            _unitFactory?.RegisterProjectilePrefab(prefab);
        }

        /// <summary>
        /// Updates all visuals from the latest battle state.
        /// </summary>
        public void UpdateFromState(BattleStateData state)
        {
            if (state == null) return;
            UpdateUnits(state);
            UpdateProjectiles(state);
        }

        void Update()
        {
            // Interpolate unit positions
            foreach (var kvp in _unitTargetPositions)
            {
                if (_unitObjects.TryGetValue(kvp.Key, out var unitObj) && unitObj != null)
                {
                    unitObj.transform.position = Vector3.Lerp(
                        unitObj.transform.position,
                        kvp.Value,
                        Time.deltaTime * UnitInterpolationSpeed);

                    // Face movement direction
                    Vector3 dir = kvp.Value - unitObj.transform.position;
                    if (dir.sqrMagnitude > 0.001f)
                    {
                        unitObj.transform.rotation = Quaternion.Slerp(
                            unitObj.transform.rotation,
                            Quaternion.LookRotation(dir),
                            Time.deltaTime * 5f);
                    }
                }
            }
        }

        // ───────────────────────── Terrain ─────────────────────────

        private void CreateTerrain()
        {
            // Ground plane
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.transform.SetParent(_terrainParent);
            ground.transform.position = new Vector3(FieldWidth / 2f, -0.05f, FieldDepth / 2f);
            ground.transform.localScale = new Vector3(FieldWidth + 2f, 0.1f, FieldDepth + 2f);
            var groundRenderer = ground.GetComponent<Renderer>();
            groundRenderer.material = new Material(Shader.Find("Standard"));
            groundRenderer.material.color = GroundColor;

            // Grid lines for visual reference
            CreateGridLines();

            // Deployment zone markers
            CreateDeploymentZones();

            // Center line
            var centerLine = GameObject.CreatePrimitive(PrimitiveType.Cube);
            centerLine.name = "CenterLine";
            centerLine.transform.SetParent(_terrainParent);
            centerLine.transform.position = new Vector3(FieldWidth / 2f, 0.01f, FieldDepth / 2f);
            centerLine.transform.localScale = new Vector3(0.05f, 0.01f, FieldDepth);
            var lineRenderer = centerLine.GetComponent<Renderer>();
            lineRenderer.material = new Material(Shader.Find("Standard"));
            lineRenderer.material.color = new Color(1, 1, 1, 0.3f);
            var col = centerLine.GetComponent<Collider>();
            if (col != null) Destroy(col);
        }

        private void CreateGridLines()
        {
            float gridSpacing = 10f;
            Color gridColor = new Color(0.25f, 0.45f, 0.18f);

            for (float x = 0; x <= FieldWidth; x += gridSpacing)
            {
                var line = GameObject.CreatePrimitive(PrimitiveType.Cube);
                line.name = "GridX";
                line.transform.SetParent(_terrainParent);
                line.transform.position = new Vector3(x, 0.005f, FieldDepth / 2f);
                line.transform.localScale = new Vector3(0.02f, 0.01f, FieldDepth);
                line.GetComponent<Renderer>().material = new Material(Shader.Find("Standard")) { color = gridColor };
                var c = line.GetComponent<Collider>();
                if (c != null) Destroy(c);
            }

            for (float z = 0; z <= FieldDepth; z += gridSpacing)
            {
                var line = GameObject.CreatePrimitive(PrimitiveType.Cube);
                line.name = "GridZ";
                line.transform.SetParent(_terrainParent);
                line.transform.position = new Vector3(FieldWidth / 2f, 0.005f, z);
                line.transform.localScale = new Vector3(FieldWidth, 0.01f, 0.02f);
                line.GetComponent<Renderer>().material = new Material(Shader.Find("Standard")) { color = gridColor };
                var c = line.GetComponent<Collider>();
                if (c != null) Destroy(c);
            }
        }

        private void CreateDeploymentZones()
        {
            // Player 1 zone (left)
            var zone1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            zone1.name = "DeployZone_P1";
            zone1.transform.SetParent(_terrainParent);
            zone1.transform.position = new Vector3(10f, 0.008f, FieldDepth / 2f);
            zone1.transform.localScale = new Vector3(20f, 0.01f, FieldDepth);
            var r1 = zone1.GetComponent<Renderer>();
            r1.material = new Material(Shader.Find("Standard"));
            r1.material.color = new Color(0.2f, 0.4f, 0.8f, 0.15f);
            var c1 = zone1.GetComponent<Collider>();
            if (c1 != null) Destroy(c1);

            // Player 2 zone (right)
            var zone2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            zone2.name = "DeployZone_P2";
            zone2.transform.SetParent(_terrainParent);
            zone2.transform.position = new Vector3(FieldWidth - 10f, 0.008f, FieldDepth / 2f);
            zone2.transform.localScale = new Vector3(20f, 0.01f, FieldDepth);
            var r2 = zone2.GetComponent<Renderer>();
            r2.material = new Material(Shader.Find("Standard"));
            r2.material.color = new Color(0.8f, 0.3f, 0.2f, 0.15f);
            var c2 = zone2.GetComponent<Collider>();
            if (c2 != null) Destroy(c2);
        }

        // ───────────────────────── Unit Management ─────────────────────────

        private void UpdateUnits(BattleStateData state)
        {
            var activeIds = new HashSet<string>();

            if (state.units != null)
            {
                foreach (var unit in state.units)
                {
                    if (!unit.alive)
                    {
                        // Unit died — destroy with effect
                        if (_unitObjects.TryGetValue(unit.id, out var deadObj))
                        {
                            DestroyUnitVisual(unit.id);
                        }
                        continue;
                    }

                    activeIds.Add(unit.id);
                    Vector3 targetPos = new Vector3(unit.x, 0, unit.y);
                    _unitTargetPositions[unit.id] = targetPos;

                    if (!_unitObjects.ContainsKey(unit.id))
                    {
                        // Create new unit visual
                        var unitObj = _unitFactory.CreateUnit(unit, _player1Id);
                        _unitObjects[unit.id] = unitObj;

                        // Create health bar
                        CreateHealthBar(unit.id, unitObj.transform);
                    }

                    // Update health bar
                    UpdateHealthBar(unit.id, unit.health, unit.max_health);
                }
            }

            // Remove units no longer in state
            var toRemove = new List<string>();
            foreach (var kvp in _unitObjects)
            {
                if (!activeIds.Contains(kvp.Key))
                    toRemove.Add(kvp.Key);
            }
            foreach (var id in toRemove)
                DestroyUnitVisual(id);
        }

        private void DestroyUnitVisual(string unitId)
        {
            if (_unitObjects.TryGetValue(unitId, out var obj))
            {
                // Simple death effect: scale down
                Destroy(obj, 0.1f);
                _unitObjects.Remove(unitId);
            }
            if (_healthBarObjects.TryGetValue(unitId, out var hb))
            {
                Destroy(hb);
                _healthBarObjects.Remove(unitId);
            }
            _unitTargetPositions.Remove(unitId);
        }

        // ───────────────────────── Health Bars ─────────────────────────

        private void CreateHealthBar(string unitId, Transform unitTransform)
        {
            var hbGO = new GameObject($"HealthBar_{unitId}");
            hbGO.transform.SetParent(_healthBarsParent);

            // Background (red)
            var bg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bg.name = "HB_BG";
            bg.transform.SetParent(hbGO.transform);
            bg.transform.localPosition = new Vector3(0, HealthBarHeight, 0);
            bg.transform.localScale = new Vector3(HealthBarWidth, 0.08f, 0.08f);
            bg.GetComponent<Renderer>().material = new Material(Shader.Find("Standard")) { color = Color.red };
            var c1 = bg.GetComponent<Collider>();
            if (c1 != null) Destroy(c1);

            // Foreground (green)
            var fg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fg.name = "HB_FG";
            fg.transform.SetParent(hbGO.transform);
            fg.transform.localPosition = new Vector3(0, HealthBarHeight, -0.01f);
            fg.transform.localScale = new Vector3(HealthBarWidth, 0.08f, 0.08f);
            fg.GetComponent<Renderer>().material = new Material(Shader.Find("Standard")) { color = Color.green };
            var c2 = fg.GetComponent<Collider>();
            if (c2 != null) Destroy(c2);

            _healthBarObjects[unitId] = hbGO;
        }

        private void UpdateHealthBar(string unitId, int health, int maxHealth)
        {
            if (!_healthBarObjects.TryGetValue(unitId, out var hbGO)) return;
            if (!_unitObjects.TryGetValue(unitId, out var unitObj)) return;

            // Follow unit position
            hbGO.transform.position = unitObj.transform.position;

            // Billboard toward camera
            if (Camera.main != null)
            {
                hbGO.transform.LookAt(Camera.main.transform);
            }

            // Scale foreground bar
            float ratio = maxHealth > 0 ? Mathf.Clamp01((float)health / maxHealth) : 0;
            var fg = hbGO.transform.Find("HB_FG");
            if (fg != null)
            {
                var scale = fg.localScale;
                scale.x = HealthBarWidth * ratio;
                fg.localScale = scale;

                // Offset to align left
                float offset = (HealthBarWidth - scale.x) / 2f;
                var pos = fg.localPosition;
                pos.x = -offset;
                fg.localPosition = pos;

                // Color gradient: green → yellow → red
                var renderer = fg.GetComponent<Renderer>();
                if (renderer != null)
                {
                    Color barColor = ratio > 0.5f
                        ? Color.Lerp(Color.yellow, Color.green, (ratio - 0.5f) * 2f)
                        : Color.Lerp(Color.red, Color.yellow, ratio * 2f);
                    renderer.material.color = barColor;
                }
            }
        }

        // ───────────────────────── Projectiles ─────────────────────────

        private void UpdateProjectiles(BattleStateData state)
        {
            var activeIds = new HashSet<string>();

            if (state.projectiles != null)
            {
                foreach (var proj in state.projectiles)
                {
                    activeIds.Add(proj.id);
                    Vector3 pos = new Vector3(proj.x, 1f, proj.y);

                    if (!_projectileObjects.ContainsKey(proj.id))
                    {
                        var projObj = _unitFactory.CreateProjectile(proj, _player1Id);
                        _projectileObjects[proj.id] = projObj;
                    }
                    else
                    {
                        _projectileObjects[proj.id].transform.position = pos;
                    }
                }
            }

            // Remove projectiles no longer in state (they hit)
            var toRemove = new List<string>();
            foreach (var kvp in _projectileObjects)
            {
                if (!activeIds.Contains(kvp.Key))
                {
                    // Impact effect (could spawn particles here)
                    Destroy(kvp.Value);
                    toRemove.Add(kvp.Key);
                }
            }
            foreach (var id in toRemove)
                _projectileObjects.Remove(id);
        }

        /// <summary>
        /// Clears all rendered objects for a fresh start.
        /// </summary>
        public void ClearAll()
        {
            foreach (var kvp in _unitObjects) if (kvp.Value != null) Destroy(kvp.Value);
            foreach (var kvp in _healthBarObjects) if (kvp.Value != null) Destroy(kvp.Value);
            foreach (var kvp in _projectileObjects) if (kvp.Value != null) Destroy(kvp.Value);

            _unitObjects.Clear();
            _unitTargetPositions.Clear();
            _healthBarObjects.Clear();
            _projectileObjects.Clear();
        }
    }
}
