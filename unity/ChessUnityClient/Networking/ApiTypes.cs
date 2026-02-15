// ChessUnityClient/Networking/ApiTypes.cs
// Serializable data types matching the go-gameserver REST API JSON responses.
// These types are used by Unity's JsonUtility for serialization/deserialization.

using System;

namespace ChessUnityClient
{
    // ───────────────────────── Server Responses ─────────────────────────

    [Serializable]
    public class PlayerData
    {
        public int id;
        public string uid;
        public string name;
        public int num_sessions;
        public string created_at;
        public string updated_at;
    }

    [Serializable]
    public class SessionData
    {
        public int id;
        public string session_id;
        public string status;
        public int max_players;
        public PlayerData[] players;
        public string created_at;
        public string updated_at;
    }

    [Serializable]
    public class ChessGameData
    {
        public int id;
        public int session_id;
        public int white_id;
        public int black_id;
        public string board;    // JSON-encoded 8x8 string array
        public string turn;     // "white" or "black"
        public string status;   // "playing", "check", "checkmate", "stalemate", "draw", "resigned"
        public int move_count;
        public string move_history; // JSON array of "e2-e4" strings
        public string created_at;
        public string updated_at;
    }

    [Serializable]
    public class ErrorResponse
    {
        public string error;
    }

    [Serializable]
    public class MessageResponse
    {
        public string message;
    }

    [Serializable]
    public class HealthResponse
    {
        public string status;
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
    public class CreateChessGameRequest
    {
        public int session_id;
        public int white_id;
        public int black_id;
    }

    [Serializable]
    public class ChessMoveRequest
    {
        public int player_id;
        public string from;
        public string to;
    }

    [Serializable]
    public class ResignRequest
    {
        public int player_id;
    }

    [Serializable]
    public class UpdateStatusRequest
    {
        public string status;
    }

    // ───────────────────────── Board Parsing ─────────────────────────

    /// <summary>
    /// Helper to parse the board JSON string into a 2D array.
    /// The server stores the board as a JSON-encoded [8][8]string.
    /// </summary>
    [Serializable]
    public class BoardRow
    {
        public string[] cells;
    }
}
