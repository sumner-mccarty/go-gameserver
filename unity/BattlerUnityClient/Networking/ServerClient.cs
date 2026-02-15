// BattlerUnityClient/Networking/ServerClient.cs
// HTTP client for the go-gameserver REST API.
// Handles player, session, and battle management via HTTP.

using System;
using System.Text;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace BattlerUnityClient
{
    /// <summary>
    /// HTTP client for the go-gameserver REST API.
    /// All calls are coroutine-based with callback results.
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

        // ───────────────────────── Battle ─────────────────────────

        /// <summary>
        /// Starts a battle. The server begins the real-time simulation.
        /// After this call, connect via WebSocket to receive state updates.
        /// </summary>
        public IEnumerator StartBattle(StartBattleRequest request, Action<BattleStartResponse, string> callback)
        {
            var body = JsonUtility.ToJson(request);
            yield return Post<BattleStartResponse>("/api/battle/start", body, callback);
        }

        /// <summary>
        /// Returns the WebSocket URL for battle state streaming.
        /// </summary>
        public string GetBattleWebSocketURL(int sessionId)
        {
            string wsBase = BaseURL.Replace("http://", "ws://").Replace("https://", "wss://");
            return $"{wsBase}/ws/battle/{sessionId}";
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
