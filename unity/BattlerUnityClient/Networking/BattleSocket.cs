// BattlerUnityClient/Networking/BattleSocket.cs
// WebSocket client for receiving real-time battle state from the server.
// Uses Unity's built-in networking or a simple TCP-based WebSocket implementation.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace BattlerUnityClient
{
    /// <summary>
    /// WebSocket client that connects to the battle state stream.
    /// Receives BattleStateData at 20 Hz from the go-gameserver.
    ///
    /// Usage:
    ///   var socket = gameObject.AddComponent&lt;BattleSocket&gt;();
    ///   socket.OnStateReceived += (state) => { /* render */ };
    ///   socket.Connect("ws://localhost:3000/ws/battle/1");
    /// </summary>
    public class BattleSocket : MonoBehaviour
    {
        /// <summary>Fired every time a new battle state is received from the server.</summary>
        public event Action<BattleStateData> OnStateReceived;

        /// <summary>Fired when the WebSocket connection is established.</summary>
        public event Action OnConnected;

        /// <summary>Fired when the connection is closed.</summary>
        public event Action<string> OnDisconnected;

        /// <summary>Fired on connection error.</summary>
        public event Action<string> OnError;

        /// <summary>The latest state received from the server.</summary>
        public BattleStateData LatestState { get; private set; }

        /// <summary>Whether the socket is currently connected.</summary>
        public bool IsConnected { get; private set; }

        private ClientWebSocket _ws;
        private CancellationTokenSource _cts;
        private readonly Queue<string> _messageQueue = new Queue<string>();
        private readonly object _queueLock = new object();

        /// <summary>
        /// Connect to the battle WebSocket endpoint.
        /// </summary>
        public void Connect(string wsUrl)
        {
            StartCoroutine(ConnectAsync(wsUrl));
        }

        /// <summary>
        /// Disconnect from the WebSocket.
        /// </summary>
        public void Disconnect()
        {
            _cts?.Cancel();
            IsConnected = false;
        }

        private IEnumerator ConnectAsync(string wsUrl)
        {
            _ws = new ClientWebSocket();
            _cts = new CancellationTokenSource();

            // Connect on background thread
            Task connectTask = null;
            try
            {
                connectTask = _ws.ConnectAsync(new Uri(wsUrl), _cts.Token);
            }
            catch (Exception e)
            {
                OnError?.Invoke($"WebSocket connection failed: {e.Message}");
                yield break;
            }

            // Wait for connection
            while (!connectTask.IsCompleted)
                yield return null;

            if (connectTask.IsFaulted)
            {
                OnError?.Invoke($"WebSocket connection failed: {connectTask.Exception?.Message}");
                yield break;
            }

            IsConnected = true;
            OnConnected?.Invoke();
            Debug.Log($"[BattlerUnityClient] WebSocket connected to {wsUrl}");

            // Start receiving on background thread
            StartReceiveLoop();
        }

        private void StartReceiveLoop()
        {
            Task.Run(async () =>
            {
                var buffer = new byte[8192];
                try
                {
                    while (_ws.State == WebSocketState.Open && !_cts.Token.IsCancellationRequested)
                    {
                        var result = await _ws.ReceiveAsync(
                            new ArraySegment<byte>(buffer), _cts.Token);

                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            lock (_queueLock) { _messageQueue.Enqueue("__CLOSED__"); }
                            break;
                        }

                        if (result.MessageType == WebSocketMessageType.Text)
                        {
                            string json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                            lock (_queueLock) { _messageQueue.Enqueue(json); }
                        }
                    }
                }
                catch (Exception e)
                {
                    lock (_queueLock) { _messageQueue.Enqueue("__ERROR__" + e.Message); }
                }
            });
        }

        void Update()
        {
            // Process messages on the main thread
            lock (_queueLock)
            {
                while (_messageQueue.Count > 0)
                {
                    string msg = _messageQueue.Dequeue();

                    if (msg == "__CLOSED__")
                    {
                        IsConnected = false;
                        OnDisconnected?.Invoke("Connection closed by server");
                        continue;
                    }

                    if (msg.StartsWith("__ERROR__"))
                    {
                        IsConnected = false;
                        OnError?.Invoke(msg.Substring(9));
                        continue;
                    }

                    try
                    {
                        var state = JsonUtility.FromJson<BattleStateData>(msg);
                        LatestState = state;
                        OnStateReceived?.Invoke(state);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[BattlerUnityClient] Failed to parse state: {e.Message}");
                    }
                }
            }
        }

        void OnDestroy()
        {
            Disconnect();
            if (_ws != null)
            {
                try
                {
                    if (_ws.State == WebSocketState.Open)
                        _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closing",
                            CancellationToken.None).Wait(1000);
                }
                catch { }
                _ws.Dispose();
            }
        }
    }
}
