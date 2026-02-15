// BattlerUnityClient/Networking/ApiTypes.cs
// Serializable data types matching the go-gameserver REST API and WebSocket JSON.
// Covers players, sessions, battle state, and unit types.

using System;

namespace BattlerUnityClient
{
    // ───────────────────────── Server Responses ─────────────────────────

    [Serializable]
    public class PlayerData
    {
        public int id;
        public string uid;
        public string name;
        public int num_sessions;
    }

    [Serializable]
    public class SessionData
    {
        public int id;
        public string session_id;
        public string status;
        public int max_players;
        public PlayerData[] players;
    }

    [Serializable]
    public class BattleStartResponse
    {
        public string message;
        public int session_id;
        public int unit_count;
    }

    [Serializable]
    public class ErrorResponse
    {
        public string error;
    }

    [Serializable]
    public class HealthResponse
    {
        public string status;
    }

    // ───────────────────────── WebSocket State ─────────────────────────

    /// <summary>
    /// Received from the WebSocket every tick (20 times/sec).
    /// </summary>
    [Serializable]
    public class BattleStateData
    {
        public int tick;
        public string status;       // "running" or "finished"
        public BattleUnitData[] units;
        public ProjectileData[] projectiles;
        public int winner_id;
    }

    [Serializable]
    public class BattleUnitData
    {
        public string id;
        public string type_name;
        public int owner_id;
        public float x;
        public float y;
        public int health;
        public int max_health;
        public string target_id;
        public bool alive;
    }

    [Serializable]
    public class ProjectileData
    {
        public string id;
        public int owner_id;
        public float x;
        public float y;
        public float target_x;
        public float target_y;
        public float speed;
        public int damage;
        public float splash;
    }

    // ───────────────────────── Request Bodies ─────────────────────────

    [Serializable]
    public class CreatePlayerRequest
    {
        public string uid;
        public string name;
    }

    [Serializable]
    public class CreateSessionRequest
    {
        public string session_id;
        public int max_players;
    }

    [Serializable]
    public class JoinSessionRequest
    {
        public int player_id;
    }

    [Serializable]
    public class UpdateStatusRequest
    {
        public string status;
    }

    [Serializable]
    public class ArmyUnitPlacement
    {
        public string type_name;
        public float x;
        public float y;
    }

    /// <summary>
    /// Request to start a battle. Both armies are submitted.
    /// </summary>
    [Serializable]
    public class StartBattleRequest
    {
        public int session_id;
        public int player1_id;
        public int player2_id;
        public ArmyUnitPlacement[] army1;
        public ArmyUnitPlacement[] army2;
    }

    // ───────────────────────── Unit Type Definitions ─────────────────────────

    /// <summary>
    /// Client-side unit type definition matching the server's unit catalog.
    /// Used for the army builder UI and unit rendering.
    /// </summary>
    [Serializable]
    public class UnitTypeInfo
    {
        public string name;
        public string displayName;
        public int health;
        public float speed;
        public int attackDamage;
        public float attackRange;
        public float attackCooldown;
        public float projectileSpeed;
        public float splashRadius;
        public int cost;
        public string description;
    }
}
