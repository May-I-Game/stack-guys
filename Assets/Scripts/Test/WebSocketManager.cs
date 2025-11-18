using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using WebSocketSharp;
using WebSocketSharp.Server;

public class WebSocketManager : MonoBehaviour
{
    public static WebSocketManager Instance;
    public int ConnectedBotCount => connectedBots.Count;

    public GameObject playerPref;
    private WebSocketServer ws;

    // 메인 스레드에서 처리하기 위한 큐
    private static ConcurrentQueue<string> messageQueue = new ConcurrentQueue<string>();

    private Dictionary<string, ConsoleBotController> connectedBots = new Dictionary<string, ConsoleBotController>();
    private enum EventType { Connect, Disconnect, Message }

    private static ConcurrentQueue<ServerEvent> eventQueue = new ConcurrentQueue<ServerEvent>();

    private struct ServerEvent
    {
        public EventType type;
        public string sessionId;
        public string message;
    }

    private struct RemoteCommand
    {
        public string functionName; // 실행할 함수 이름
        public string parameter;    // 매개변수 (필요시 int 등으로 변경 가능)
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        if (!NetworkManager.Singleton || !NetworkManager.Singleton.IsServer) return;

        // 웹소켓 서버 생성 (서버 포트 + 1000)
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        ushort port = (ushort)(transport.ConnectionData.Port + 1000);
        ws = new WebSocketServer(port);

        // 클라이언트가 서버로 접속하면 GameBehavior가 작동하도록 설정
        ws.AddWebSocketService<GameBehavior>("/");

        // 서버 시작
        ws.Start();
        Debug.Log($"웹소켓 서버가 포트 {port}에서 시작되었습니다.");
    }

    void Update()
    {
        if (!NetworkManager.Singleton || !NetworkManager.Singleton.IsServer) return;

        while (eventQueue.TryDequeue(out ServerEvent evt))
        {
            switch (evt.type)
            {
                case EventType.Connect:
                    SpawnBot(evt.sessionId);
                    break;

                case EventType.Disconnect:
                    DestroyBot(evt.sessionId);
                    break;

                case EventType.Message:
                    HandleMessage(evt.sessionId, evt.message);
                    break;
            }
        }
    }

    void OnApplicationQuit()
    {
        if (ws != null)
        {
            ws.Stop();
        }
    }

    // 웹소켓 행동 정의 (내부 클래스)
    public class GameBehavior : WebSocketBehavior
    {
        protected override void OnOpen()
        {
            // 🔥 최대 인원 체크 (100명 제한) - WebSocket 연결 단계에서 먼저 차단
            const int MAX_PLAYERS = 100;
            int currentPlayers = 0;

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                currentPlayers = NetworkManager.Singleton.ConnectedClients.Count + Instance.ConnectedBotCount;
            }

            if (currentPlayers >= MAX_PLAYERS)
            {
                Debug.LogWarning($"⚠️ [WebSocket] 서버 정원 초과! 현재: {currentPlayers}명 / 최대: {MAX_PLAYERS}명 → WebSocket 연결 거부");
                Context.WebSocket.Close(CloseStatusCode.Normal, "Server is full");
                return;
            }

            // ★ 중요: 큐에 넣어야 메인 스레드가 봇을 만듭니다.
            eventQueue.Enqueue(new ServerEvent
            {
                type = EventType.Connect,
                sessionId = this.ID
            });

            Debug.Log($"✅ [WebSocket] Bot connected ({currentPlayers + 1}/{MAX_PLAYERS})");
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            eventQueue.Enqueue(new ServerEvent
            {
                type = EventType.Message,
                sessionId = this.ID,
                message = e.Data
            });
        }

        protected override void OnClose(CloseEventArgs e)
        {
            eventQueue.Enqueue(new ServerEvent
            {
                type = EventType.Disconnect,
                sessionId = this.ID
            });
        }
    }

    void HandleMessage(string sessionId, string jsonMessage)
    {
        try
        {
            // JSON 파싱
            RemoteCommand cmd = JsonUtility.FromJson<RemoteCommand>(jsonMessage);
            ExecuteGameFunction(sessionId, cmd);
        }
        catch (Exception e)
        {
            Debug.LogError($"JSON 파싱 에러: {e.Message}");
        }
    }

    void ExecuteGameFunction(string sessionId, RemoteCommand cmd)
    {
        switch (cmd.functionName)
        {
            case "MovePlayer":
                Vector2 dir = ParseVector2(cmd.parameter);
                MovePlayer(sessionId, dir);
                break;

            case "JumpPlayer":
                JumpPlayer(sessionId);
                break;

            default:
                Debug.LogWarning($"알 수 없는 함수: {cmd.functionName}");
                break;
        }
    }

    private void SpawnBot(string sessionId)
    {
        if (connectedBots.ContainsKey(sessionId)) return;

        // 🔥 최대 인원 체크 (100명 제한) - 봇 스폰 직전 최종 확인
        const int MAX_PLAYERS = 100;
        int currentPlayers = NetworkManager.Singleton.ConnectedClients.Count + ConnectedBotCount;

        if (currentPlayers >= MAX_PLAYERS)
        {
            Debug.LogWarning($"⚠️ [WebSocket] 봇 스폰 거부! 현재: {currentPlayers}명 / 최대: {MAX_PLAYERS}명");
            return;
        }

        Vector3 spawnPos = new Vector3(UnityEngine.Random.Range(-545f, -535f), 1f, UnityEngine.Random.Range(10f, 15f));
        GameObject bot = Instantiate(playerPref, spawnPos, Quaternion.identity);
        bot.GetComponent<NetworkObject>().Spawn();
        PlayerNameSync nameSync = bot.GetComponent<PlayerNameSync>();
        if (nameSync != null)
        {
            nameSync.SetPlayerName("ConsoleBot");
        }

        connectedBots.Add(sessionId, bot.GetComponent<ConsoleBotController>());
        Debug.Log($"✅ [WebSocket] Bot spawned ({currentPlayers + 1}/{MAX_PLAYERS})");
    }

    private void DestroyBot(string sessionId)
    {
        if (connectedBots.TryGetValue(sessionId, out ConsoleBotController bot))
        {
            // 1. 오브젝트 파괴
            bot.GetComponent<NetworkObject>().Despawn();

            // 2. 딕셔너리에서 제거
            connectedBots.Remove(sessionId);
            Debug.Log($"플레이어 퇴장 및 제거: {sessionId}");
        }
    }

    private void MovePlayer(string sessionId, Vector2 dir)
    {
        connectedBots[sessionId].MoveBot(dir);
    }

    private void JumpPlayer(string sessionId)
    {
        connectedBots[sessionId].JumpBot();
    }

    private Vector2 ParseVector2(string param)
    {
        try
        {
            string[] split = param.Split(',');
            if (split.Length >= 2)
            {
                float x = float.Parse(split[0]);
                float y = float.Parse(split[1]);
                return new Vector2(x, y);
            }
        }
        catch { }
        return Vector2.zero; // 실패 시 기본값
    }
}
