using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.Networking;


public class DummyGameStarter : MonoBehaviour
{
    [Header("Bot Configuration")]
    [SerializeField] private int clientCharIndex = 12;
    [SerializeField] private string clientName = "BotClient";

    [Header("Connection Settings")]
    [SerializeField] private bool useMatchmaking = false;
    [SerializeField] private string matchmakingUrl = "http://matchmaking-alb-1609632759.ap-northeast-2.elb.amazonaws.com";

    [Header("Direct Connection (if not using matchmaking)")]
    [SerializeField] private bool isLocalMod;
    [SerializeField] private string serverAddress = "3.37.88.2";
    [SerializeField] private ushort serverPort = 7779;

    private const int MAX_NAME_BYTES = 48;
    private bool isConnecting;
    private string playerId;

    private void Start()
    {
#if DUMMY_CLIENT
        StartCoroutine(WaitAndConnect());
#endif
    }

    private IEnumerator WaitAndConnect()
    {
        // NetworkManager가 초기화될 때까지 대기
        while (NetworkManager.Singleton == null)
        {
            Debug.Log("[DummyGameStarter] Waiting for NetworkManager...");
            yield return new WaitForSeconds(0.1f);
        }

        // NetworkManager가 이미 시작되었는지 확인
        while (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer)
        {
            Debug.Log("[DummyGameStarter] Waiting for NetworkManager to be ready...");
            yield return new WaitForSeconds(0.1f);
        }

        //networkManager 콜백 구독
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

        Debug.Log("[DummyGameStarter] NetworkManager ready, starting connection...");

        // Player ID 생성 (매치메이킹용)
        playerId = System.Guid.NewGuid().ToString();
        Debug.Log($"[DummyGameStarter] Generated Player ID: {playerId}");

        // 추가 안전성을 위해 약간 더 대기
        yield return new WaitForSeconds(0.5f);

        // 매치메이킹 또는 직접 연결
        if (useMatchmaking)
        {
            Debug.Log("[DummyGameStarter] Using matchmaking server...");
            StartCoroutine(FindGameAndConnect());
        }
        else
        {
            Debug.Log("[DummyGameStarter] Using direct connection...");
            ConnectToServer(serverAddress, serverPort);
        }
    }

    private void OnDestroy()
    {
#if DUMMY_CLIENT
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
        CancelInvoke(nameof(CheckConnectionTimeout));
#endif
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"[DummyGameStarter] OnClientConnected called - ClientId: {clientId}, LocalClientId: {NetworkManager.Singleton.LocalClientId}");

        //성공적으로 자신이 연결됨
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            Debug.Log("[DummyGameStarter] Successfully connected to server!");
            CancelInvoke(nameof(CheckConnectionTimeout));
            isConnecting = false;
        }
        else
        {
            Debug.Log($"[DummyGameStarter] Other client connected: {clientId}");
        }
    }
    private void OnClientDisconnected(ulong clientId)
    {
        //자신이 연결 해제됨
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            Debug.Log("Disconnected from server");
            if (isConnecting)
            {
                Debug.Log("Connected failed");
                isConnecting = false;
            }
        }
    }

    private void ConnectToServer(string address, ushort port)
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("[DummyGameStarter] NetworkManager not found!");
            isConnecting = false;
            return;
        }

        isConnecting = true;

        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport == null)
        {
            Debug.LogError("[DummyGameStarter] UnityTransport not found!");
            isConnecting = false;
            return;
        }

        Debug.Log($"[DummyGameStarter] Attempting to connect to {address}:{port}");

        // WebSocket 활성화 (게임 서버가 WebSocket으로 동작)
        transport.UseWebSockets = true;
        transport.SetConnectionData(address, port);

        Debug.Log($"[DummyGameStarter] Transport configured - UseWebSockets: {transport.UseWebSockets}, Protocol: {transport.Protocol}");

        // ConnectionData 구성: [1바이트 캐릭터][이름(UTF8)]
        byte[] nameBytes = TruncateUtf8(clientName, MAX_NAME_BYTES);
        byte[] payload = new byte[1 + nameBytes.Length];
        payload[0] = (byte)clientCharIndex;
        System.Array.Copy(nameBytes, 0, payload, 1, nameBytes.Length);

        NetworkManager.Singleton.NetworkConfig.ConnectionData = payload;

        //서버로 캐릭터 이름을 보내기
        PlayerPrefs.SetString("player_name", clientName);
        PlayerPrefs.Save();

        Debug.Log($"[DummyGameStarter] Character Index: {clientCharIndex}, Name: {clientName}");
        Debug.Log($"[DummyGameStarter] Connection payload size: {payload.Length} bytes");

        //클라이언트 시작
        bool startResult = NetworkManager.Singleton.StartClient();

        //클라-서버 연결 실패했을 경우
        if (!startResult)
        {
            Debug.LogError("[DummyGameStarter] StartClient() returned false - 연결 실패");
            isConnecting = false;
            return;
        }

        Debug.Log("[DummyGameStarter] StartClient() succeeded, waiting for connection...");
        Invoke(nameof(CheckConnectionTimeout), 10f);
    }

    // UTF-8 바이트 안전 자르기
    private static byte[] TruncateUtf8(string s, int maxBytes)
    {
        var src = System.Text.Encoding.UTF8.GetBytes(s ?? "");
        if (src.Length <= maxBytes) return src;
        int len = maxBytes;
        while (len > 0 && (src[len] & 0b1100_0000) == 0b1000_0000) len--;
        var dst = new byte[len];
        System.Array.Copy(src, dst, len);
        return dst;
    }

    private void CheckConnectionTimeout()
    {
        if (isConnecting && NetworkManager.Singleton != null && !NetworkManager.Singleton.IsConnectedClient)
        {
            Debug.LogWarning("[DummyGameStarter] Connection timeout!");
            if (NetworkManager.Singleton.IsClient)
            {
                NetworkManager.Singleton.Shutdown();
            }
            isConnecting = false;
        }
    }

    // ==================== 매치메이킹 관련 ====================

    [System.Serializable]
    private class MatchmakingRequest
    {
        public string player_id;
    }

    [System.Serializable]
    private class MatchmakingResponse
    {
        public string ticket_id;
        public string message;
    }

    [System.Serializable]
    private class TicketStatusResponse
    {
        public string status;
        public string server_ip;
        public int server_port;
        public string message;
    }

    private IEnumerator FindGameAndConnect()
    {
        Debug.Log($"[DummyGameStarter] Requesting matchmaking with player_id: {playerId}");

        // 매치메이킹 요청 전송
        MatchmakingRequest request = new MatchmakingRequest { player_id = playerId };
        string jsonRequest = JsonUtility.ToJson(request);

        using (UnityWebRequest www = new UnityWebRequest(matchmakingUrl + "/api/find-game", "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonRequest);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[DummyGameStarter] Matchmaking request failed: {www.error}");
                isConnecting = false;
                yield break;
            }

            string responseText = www.downloadHandler.text;
            Debug.Log($"[DummyGameStarter] Matchmaking response: {responseText}");

            MatchmakingResponse response = JsonUtility.FromJson<MatchmakingResponse>(responseText);

            if (string.IsNullOrEmpty(response.ticket_id))
            {
                Debug.LogError("[DummyGameStarter] Failed to get ticket_id from matchmaking server");
                isConnecting = false;
                yield break;
            }

            Debug.Log($"[DummyGameStarter] Received ticket_id: {response.ticket_id}");

            // 티켓 상태 폴링 시작
            yield return StartCoroutine(PollTicketStatus(response.ticket_id));
        }
    }

    private IEnumerator PollTicketStatus(string ticketId)
    {
        const int maxPolls = 30;
        const float pollInterval = 1f;
        int pollCount = 0;

        while (pollCount < maxPolls)
        {
            pollCount++;
            Debug.Log($"[DummyGameStarter] Polling ticket status ({pollCount}/{maxPolls})...");

            string url = $"{matchmakingUrl}/api/ticket-status?ticket_id={ticketId}&player_id={playerId}";

            using (UnityWebRequest www = UnityWebRequest.Get(url))
            {
                yield return www.SendWebRequest();

                if (www.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[DummyGameStarter] Ticket polling failed: {www.error}");
                    yield return new WaitForSeconds(pollInterval);
                    continue;
                }

                string responseText = www.downloadHandler.text;
                Debug.Log($"[DummyGameStarter] Ticket status response: {responseText}");

                TicketStatusResponse status = JsonUtility.FromJson<TicketStatusResponse>(responseText);

                if (status.status.ToUpper() == "MATCHED")
                {
                    Debug.Log($"[DummyGameStarter] Match found! Server: {status.server_ip}:{status.server_port}");
                    ConnectToServer(status.server_ip, (ushort)status.server_port);
                    yield break;
                }
                else if (status.status.ToUpper() == "QUEUED")
                {
                    Debug.Log("[DummyGameStarter] Still searching for match...");
                }
                else
                {
                    Debug.LogWarning($"[DummyGameStarter] Unknown status: {status.status}");
                }
            }

            yield return new WaitForSeconds(pollInterval);
        }

        Debug.LogError("[DummyGameStarter] Matchmaking timeout - no match found");
        isConnecting = false;
    }
}
