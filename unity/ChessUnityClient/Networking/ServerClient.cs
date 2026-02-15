// ChessUnityClient/Networking/ServerClient.cs
// HTTP client for communicating with the go-gameserver REST API.
// Handles all player, session, and chess game API calls.

using System;
using System.Text;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace ChessUnityClient
{
    /// <summary>
    /// Communicates with the go-gameserver REST API over HTTP.
    /// All calls are coroutine-based and return results via callbacks.
    /// </summary>
    public class ServerClient
    {
        public string BaseURL { get; set; }

        public ServerClient(string baseURL)
        {
            BaseURL = baseURL.TrimEnd('/');
        }

        // ───────────────────────── Health ─────────────────────────

        public IEnumerator CheckHealth(Action<bool> callback)
        {
            yield return Get<HealthResponse>("/api/health", (resp, err) =>
            {
                callback(err == null && resp != null && resp.status == "ok");
            });
        }

        // ───────────────────────── Players ─────────────────────────

        public IEnumerator CreatePlayer(string uid, string name, Action<PlayerData, string> callback)
        {
            var body = JsonUtility.ToJson(new CreatePlayerRequest { uid = uid, name = name });
            yield return Post<PlayerData>("/api/players", body, callback);
        }

        public IEnumerator GetPlayer(int id, Action<PlayerData, string> callback)
        {
            yield return Get<PlayerData>($"/api/players/{id}", callback);
        }

        // ───────────────────────── Sessions ─────────────────────────

        public IEnumerator CreateSession(string sessionId, int maxPlayers, Action<SessionData, string> callback)
        {
            var body = JsonUtility.ToJson(new CreateSessionRequest { session_id = sessionId, max_players = maxPlayers });
            yield return Post<SessionData>("/api/sessions", body, callback);
        }

        public IEnumerator JoinSession(string sessionId, int playerId, Action<SessionData, string> callback)
        {
            var body = JsonUtility.ToJson(new JoinSessionRequest { player_id = playerId });
            yield return Post<SessionData>($"/api/sessions/{sessionId}/join", body, callback);
        }

        public IEnumerator GetSession(string sessionId, Action<SessionData, string> callback)
        {
            yield return Get<SessionData>($"/api/sessions/{sessionId}", callback);
        }

        public IEnumerator UpdateSessionStatus(string sessionId, string status, Action<SessionData, string> callback)
        {
            var body = JsonUtility.ToJson(new UpdateStatusRequest { status = status });
            yield return Put<SessionData>($"/api/sessions/{sessionId}/status", body, callback);
        }

        // ───────────────────────── Chess ─────────────────────────

        public IEnumerator CreateChessGame(int sessionId, int whiteId, int blackId, Action<ChessGameData, string> callback)
        {
            var body = JsonUtility.ToJson(new CreateChessGameRequest
            {
                session_id = sessionId,
                white_id = whiteId,
                black_id = blackId
            });
            yield return Post<ChessGameData>("/api/chess", body, callback);
        }

        public IEnumerator GetChessGame(int gameId, Action<ChessGameData, string> callback)
        {
            yield return Get<ChessGameData>($"/api/chess/{gameId}", callback);
        }

        public IEnumerator MakeChessMove(int gameId, int playerId, string from, string to, Action<ChessGameData, string> callback)
        {
            var body = JsonUtility.ToJson(new ChessMoveRequest
            {
                player_id = playerId,
                from = from,
                to = to
            });
            yield return Post<ChessGameData>($"/api/chess/{gameId}/move", body, callback);
        }

        public IEnumerator ResignChessGame(int gameId, int playerId, Action<ChessGameData, string> callback)
        {
            var body = JsonUtility.ToJson(new ResignRequest { player_id = playerId });
            yield return Post<ChessGameData>($"/api/chess/{gameId}/resign", body, callback);
        }

        // ───────────────────────── HTTP Helpers ─────────────────────────

        private IEnumerator Get<T>(string path, Action<T, string> callback)
        {
            using (var req = UnityWebRequest.Get(BaseURL + path))
            {
                yield return req.SendWebRequest();
                HandleResponse(req, callback);
            }
        }

        private IEnumerator Post<T>(string path, string jsonBody, Action<T, string> callback)
        {
            var bodyBytes = Encoding.UTF8.GetBytes(jsonBody);
            using (var req = new UnityWebRequest(BaseURL + path, "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(bodyBytes);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                yield return req.SendWebRequest();
                HandleResponse(req, callback);
            }
        }

        private IEnumerator Put<T>(string path, string jsonBody, Action<T, string> callback)
        {
            var bodyBytes = Encoding.UTF8.GetBytes(jsonBody);
            using (var req = new UnityWebRequest(BaseURL + path, "PUT"))
            {
                req.uploadHandler = new UploadHandlerRaw(bodyBytes);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                yield return req.SendWebRequest();
                HandleResponse(req, callback);
            }
        }

        private void HandleResponse<T>(UnityWebRequest req, Action<T, string> callback)
        {
            if (req.result != UnityWebRequest.Result.Success)
            {
                string errorMsg = req.downloadHandler?.text ?? req.error;
                try
                {
                    var errResp = JsonUtility.FromJson<ErrorResponse>(errorMsg);
                    if (!string.IsNullOrEmpty(errResp?.error))
                        errorMsg = errResp.error;
                }
                catch { }
                callback(default(T), errorMsg);
                return;
            }
            try
            {
                var data = JsonUtility.FromJson<T>(req.downloadHandler.text);
                callback(data, null);
            }
            catch (Exception e)
            {
                callback(default(T), $"JSON parse error: {e.Message}");
            }
        }
    }
}
