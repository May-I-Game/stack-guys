using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

public class NetworkGameManager : MonoBehaviour
{
    public static NetworkGameManager Instance;
    private NetworkManager networkManager;

    [Header("Game Settings")]
    [SerializeField] private bool isServerMod;
    [SerializeField] private string gameSceneName = "GameScene";
    [SerializeField] private GameObject[] characterPrefabs;

    [Header("Background Handling")]
    [SerializeField] private float maxBackgroundTime = 30f;

    [Header("Matchmaking Heartbeat")]
    [SerializeField] private string matchmakingServerUrl = "http://matchmaking-alb-1609632759.ap-northeast-2.elb.amazonaws.com";
    [SerializeField] private string serverPublicIP = "3.37.88.2"; // Fallback IP (로컬 개발용)
    [SerializeField] private int heartbeatInterval = 5;

    // EC2 메타데이터에서 자동 감지된 Public IP (ASG 대응)
    private string detectedPublicIP = null;

    private bool hasInitialized = false;
    private Dictionary<ulong, int> clientCharacterSelections = new Dictionary<ulong, int>();
    private Dictionary<ulong, string> clientPlayerNames = new Dictionary<ulong, string>();

    // 백그라운드 처리 변수
    private bool isInBackground = false;
    private float backgroundStartTime = 0f;

    public bool isObserver { get; private set; } = false;

    private void Awake()
    {
        // 싱글톤 패턴으로 중복 방지
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // 백그라운드에서도 게임 실행 유지 (연결 끊김 방지)
        Application.runInBackground = true;

        // 한 번만 초기화
        if (!hasInitialized)
        {
            Initialize();
            hasInitialized = true;
        }

#if UNITY_SERVER
        if (isServerMod)
        {
            StartServerAndLoadScene();
        }
        else
        {
            Debug.Log("--- SERVER BUILD CLIENT MOD DETECTED ---");
        }
#elif DUMMY_CLIENT
        Debug.Log("--- BOT CLIENT BUILD DETECTED ---");
#else
        Debug.Log("--- CLIENT BUILD DETECTED ---");
#endif
    }

    private void Initialize()
    {
        networkManager = NetworkManager.Singleton;
        if (networkManager == null)
        {
            Debug.LogError("NetworkManger.Singleton is null!");
            return;
        }

        // 이벤트 구독
        networkManager.OnClientConnectedCallback += OnClientConnected;
        networkManager.OnClientDisconnectCallback += OnClientDisconnected;

        // connectionApproval 설정
        networkManager.NetworkConfig.ConnectionApproval = true;
        networkManager.ConnectionApprovalCallback += ApprovalCheck;
    }

    private void OnDestroy()
    {
        // instance가 자신일 때만 정리
        if (Instance == this)
        {
            if (networkManager != null)
            {
                networkManager.OnClientConnectedCallback -= OnClientConnected;
                networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
                networkManager.ConnectionApprovalCallback -= ApprovalCheck;
            }
            Instance = null;
        }
    }

    private void StartServerAndLoadScene()
    {
        Debug.Log("--- SERVER BUILD DETECTED ---");
        Debug.Log("-----  SERVER START  -----");

        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport != null)
        {
            transport.UseWebSockets = true;

            // 타임아웃 설정 - 렉에 더 관대하게 (서버 측)
            transport.ConnectTimeoutMS = 10000;      // 연결 타임아웃: 10초
            transport.MaxConnectAttempts = 10;        // 최대 연결 시도: 10번
            transport.DisconnectTimeoutMS = 30000;   // 연결 해제 타임아웃: 30초
            transport.HeartbeatTimeoutMS = 2000;     // 하트비트 타임아웃: 2초

            Debug.Log("[Server] WebSocket mode enabled for WebGL clients");
            Debug.Log("[Server] Timeout settings configured for better lag tolerance");
        }

        // 서버 시작
        NetworkManager.Singleton.StartServer();

        // 🔥 ASG 대응: EC2 메타데이터로 Public IP 자동 감지 후 매치메이킹 등록
        StartCoroutine(DetectPublicIPAndRegister());

        // GameScene 로드 (필요한 경우)
        if (SceneManager.GetActiveScene().name != gameSceneName)
        {
            NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
        }
    }

    private IEnumerator DetectPublicIPAndRegister()
    {
#if UNITY_SERVER && !UNITY_EDITOR
        // 서버 빌드에서만 EC2 메타데이터 API 호출
        string metadataUrl = "http://169.254.169.254/latest/meta-data/public-ipv4";

        Debug.Log("[EC2] Detecting Public IP from metadata...");

        using (UnityWebRequest www = UnityWebRequest.Get(metadataUrl))
        {
            www.timeout = 5; // 5초 타임아웃
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                detectedPublicIP = www.downloadHandler.text.Trim();
                Debug.Log($"✅ [EC2] Auto-detected Public IP: {detectedPublicIP}");
            }
            else
            {
                Debug.LogWarning($"⚠️ [EC2] Failed to get metadata (error: {www.error}), using fallback IP: {serverPublicIP}");
                detectedPublicIP = serverPublicIP;
            }
        }
#else
        // 에디터 또는 클라이언트 빌드에서는 Inspector 값 사용
        Debug.Log($"[EC2] Not a server build, using Inspector IP: {serverPublicIP}");
        detectedPublicIP = serverPublicIP;
        yield return null;
#endif

        // 매치메이킹 서버 등록 + 하트비트 시작
        StartCoroutine(RegisterAndHeartbeat());
    }

    private string GetCurrentPublicIP()
    {
        // 감지된 IP가 있으면 사용, 없으면 fallback
        return detectedPublicIP ?? serverPublicIP;
    }

    private void SpawnPlayerForClient(ulong clientId)
    {
        // Dictionary에서 데이터 가져오기
        int characterIndex = clientCharacterSelections.ContainsKey(clientId) ? clientCharacterSelections[clientId] : 0;
        string playerName = clientPlayerNames.ContainsKey(clientId) ? clientPlayerNames[clientId] : $"Player_{clientId}";

        // 캐릭터 프리팹 선택
        GameObject playerPrefab = characterPrefabs[characterIndex];
        Vector3 spawnPosition = GetSpawnPosition(clientId);

        // 캐릭터 인스턴스 생성
        GameObject playerInstance = Instantiate(playerPrefab, spawnPosition, Quaternion.identity);
        NetworkObject networkObject = playerInstance.GetComponent<NetworkObject>();

        if (networkObject != null)
        {
            // 네트워크 오브젝트로 스폰
            networkObject.SpawnAsPlayerObject(clientId, true);

            // 이름을 UI text에 설정(스폰 직후)
            PlayerCanvasManager nameSync = playerInstance.GetComponent<PlayerCanvasManager>();
            if (nameSync != null)
            {
                nameSync.SetPlayerName(playerName);
                // Debug.Log($"[Server] Set PlayerName NetworkVariable to '{playerName}' for client {clientId}");
            }

            Debug.Log($"[Server] Spawned character {characterIndex} with name '{playerName}' for client {clientId}");
        }
        else
        {
            Debug.LogError("NetworkObject component missing on player prefab!");
        }
    }

    private Vector3 GetSpawnPosition(ulong clientId)
    {
        // 서버가 클라이언트의 캐릭터를 생성시킬 좌표를 반환
        return new Vector3(0, 1, 0);
    }

    private void OnClientConnected(ulong clientId)
    {
        if (networkManager.IsServer)
        {
            // 🔥 최대 인원 체크 (100명 제한)
            int currentPlayers = networkManager.ConnectedClients.Count + WebSocketManager.Instance.ConnectedBotCount;
            const int MAX_PLAYERS = 100;

            if (currentPlayers > MAX_PLAYERS)
            {
                Debug.LogError($"========== 서버 정원 초과로 킥 ==========");
                Debug.LogError($"[SERVER KICK] Reason: 최대 인원 초과");
                Debug.LogError($"[SERVER KICK] Client ID: {clientId}");
                Debug.LogError($"[SERVER KICK] Current Players: {currentPlayers} / Max: {MAX_PLAYERS}");
                Debug.LogError($"[SERVER KICK] Time: {System.DateTime.Now:HH:mm:ss}");
                Debug.LogError($"==========================================");
                networkManager.DisconnectClient(clientId);
                return;
            }

            // 옵저버 모드
            if (!clientPlayerNames.ContainsKey(clientId))
            {
                Debug.Log($"[Server Log] Observer connected. Client ID: {clientId}");
                Debug.Log($"[Server Log] Total players now: {networkManager.ConnectedClients.Count}");
            }
            else
            {
                Debug.Log($"[Server Log] Client connecting... Client ID: {clientId}");
                Debug.Log($"[Server Log] Name: {clientPlayerNames[clientId]}, Character: {clientCharacterSelections[clientId]}");
                Debug.Log($"[Server Log] Total players now: {networkManager.ConnectedClients.Count}");

                // 플레이어 스폰
                SpawnPlayerForClient(clientId);
            }
        }

        if (networkManager.IsClient && clientId == networkManager.LocalClientId)
        {
            Debug.Log("Successfully connected to server!");
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (networkManager.IsServer)
        {
            // 상세한 킥/연결 해제 로그
            string playerName = clientPlayerNames.ContainsKey(clientId) ? clientPlayerNames[clientId] : "Unknown";
            int characterIndex = clientCharacterSelections.ContainsKey(clientId) ? clientCharacterSelections[clientId] : -1;

            Debug.LogWarning($"========== 클라이언트 연결 해제 ==========");
            Debug.LogWarning($"[SERVER KICK/DISCONNECT] Client ID: {clientId}");
            Debug.LogWarning($"[SERVER KICK/DISCONNECT] Player Name: {playerName}");
            Debug.LogWarning($"[SERVER KICK/DISCONNECT] Character Index: {characterIndex}");
            Debug.LogWarning($"[SERVER KICK/DISCONNECT] Time: {System.DateTime.Now:HH:mm:ss}");
            Debug.LogWarning($"[SERVER KICK/DISCONNECT] Remaining Players: {networkManager.ConnectedClients.Count - 1}");
            Debug.LogWarning($"==========================================");

            // dictionary 정리
            if (clientCharacterSelections.ContainsKey(clientId))
            {
                clientCharacterSelections.Remove(clientId);
            }
            // 이름 딕셔너리도 정리(메모리 누수 방지)
            if (clientPlayerNames.ContainsKey(clientId))
            {
                clientPlayerNames.Remove(clientId);
            }
        }

        // 클라이언트의 경우만 Login으로 이동
        if (networkManager.IsClient && clientId == networkManager.LocalClientId)
        {
            Debug.LogError($"========== 서버로부터 연결 해제됨 ==========");
            Debug.LogError($"[CLIENT DISCONNECTED] Client ID: {clientId}");
            Debug.LogError($"[CLIENT DISCONNECTED] Time: {System.DateTime.Now:HH:mm:ss}");
            Debug.LogError($"==========================================");
#if DUMMY_CLIENT
            Application.Quit();
#else
            SceneManager.LoadScene("Login");
#endif
        }
    }

    // ConnectionApproval 콜백
    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        // 🔥 최대 인원 체크 (100명 제한) - 연결 승인 전에 먼저 체크
        const int MAX_PLAYERS = 100;
        int currentPlayers = networkManager.ConnectedClients.Count + WebSocketManager.Instance.ConnectedBotCount;

        if (currentPlayers >= MAX_PLAYERS)
        {
            Debug.LogError($"========== 연결 승인 거부 (정원 초과) ==========");
            Debug.LogError($"[APPROVAL DENIED] Reason: 서버 정원 초과");
            Debug.LogError($"[APPROVAL DENIED] Client ID: {request.ClientNetworkId}");
            Debug.LogError($"[APPROVAL DENIED] Current Players: {currentPlayers} / Max: {MAX_PLAYERS}");
            Debug.LogError($"[APPROVAL DENIED] Time: {System.DateTime.Now:HH:mm:ss}");
            Debug.LogError($"==========================================");
            response.Approved = false;
            response.Reason = "Server is full";
            return;
        }

        int characterIndex = 0;
        string playerName = null;

        if (isObserver)
        {
            Debug.Log($"[Server] Observer connection approved: {request.ClientNetworkId}");
        }
        else if (request.Payload != null && request.Payload.Length > 0)
        {
            // 캐릭터 인덱스 받아오기
            characterIndex = request.Payload[0];

            // 유효성 검사
            if (characterIndex < 0 || characterIndex >= characterPrefabs.Length)
            {
                Debug.LogWarning($"Invalid character Index {characterIndex}, using default 0");
                characterIndex = 0;
            }

            // 캐릭터 이름 파싱
            if (request.Payload.Length > 1)
            {
                playerName = System.Text.Encoding.UTF8.GetString(request.Payload, 1, request.Payload.Length - 1);
            }

            // 빈 문자열 처리
            if (string.IsNullOrEmpty(playerName))
            {
                playerName = $"Player_{request.ClientNetworkId}";
            }

            Debug.Log($"[Server] Client {request.ClientNetworkId}: Character={characterIndex}, Name={playerName}");

            // 서버 dictionary에 저장
            clientCharacterSelections[request.ClientNetworkId] = characterIndex;
            clientPlayerNames[request.ClientNetworkId] = playerName;
        }

        // 연결 승인
        response.Approved = true;
        response.CreatePlayerObject = false;
        Debug.Log($"Connection Approved ({currentPlayers + 1}/{MAX_PLAYERS})");
    }

    public void SetObserverMode()
    {
        isObserver = true;
    }

    // ==================== 백그라운드 / 포그라운드 처리 ====================

    // private void OnApplicationPause(bool pauseStatus)
    // {
    //     if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer) return;

    //     if (pauseStatus)
    //     {
    //         // 백그라운드로 전환
    //         OnEnterBackground();
    //     }
    //     else
    //     {
    //         // 포그라운드로 복귀
    //         OnReturnForeground();
    // }

    // private void OnApplicationFocus(bool hasFocus)
    // {
    //     if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer) return;

    //     if (!hasFocus && !isInBackground)
    //     {
    //         // 포커스 잃음 (Alt+Tab 등)
    //         OnEnterBackground();
    //     }
    //     else if (hasFocus && isInBackground)
    //     {
    //         // 포커스 복귀
    //         OnReturnForeground();
    //     }
    // }

    // private void OnEnterBackground()
    // {
    //     if (isInBackground) return;

    //     isInBackground = true;
    //     backgroundStartTime = Time.realtimeSinceStartup;

    //     Debug.Log("[NetworkGameManager] 백그라운드 전환 감지");

    //     if (networkManager == null) return;

    //     // TODO: 서버에 패킷전송 중지요청

    //     // Pause 모드
    //     Debug.Log("[NetworkGameManager] 백그라운드 일시정지 모드 (연결 유지)");
    //     PausePlayer();
    // }

    // private void OnReturnForeground()
    // {
    //     if (!isInBackground) return;

    //     float backgroundDuration = Time.realtimeSinceStartup - backgroundStartTime;
    //     isInBackground = false;

    //     Debug.Log($"[NetworkGameManager] 포그라운드 복귀 (백그라운드 시간: {backgroundDuration:F1}초)");

    //     if (networkManager == null) return;

    //     // TODO: 서버에 패킷전송 재개요청

    //     // 너무 오래 백그라운드에 있었으면 연결이 끊겼을 수 있음
    //     if (backgroundDuration > maxBackgroundTime)
    //     {
    //         Debug.LogWarning("[NetworkGameManager] 백그라운드 시간 초과 - 연결 확인 필요");

    //         if (!networkManager.IsConnectedClient)
    //         {
    //             Debug.LogError("[NetworkGameManager] 서버 연결 끊김 - 재연결 필요");
    //             return;
    //         }
    //     }

    //     // Resume 모드: 플레이어 제어 복원
    //     Debug.Log("[NetworkGameManager] 포그라운드 복귀 - 플레이어 제어 재개");
    //     ResumePlayer();
    // }

    // private void PausePlayer()
    // {
    //     // 로컬 플레이어 찾기
    //     if (networkManager.LocalClient != null && networkManager.LocalClient.PlayerObject != null)
    //     {
    //         GameObject playerObject = networkManager.LocalClient.PlayerObject.gameObject;
    //         PlayerController controller = playerObject.GetComponent<PlayerController>();

    //         if (controller != null)
    //         {
    //             // 입력 차단
    //             controller.SetInputEnabled(false);
    //             Debug.Log("[NetworkGameManager] 플레이어 입력 차단");
    //         }
    //     }
    // }

    // private void ResumePlayer()
    // {
    //     // 로컬 플레이어 찾기
    //     if (networkManager.LocalClient != null && networkManager.LocalClient.PlayerObject != null)
    //     {
    //         GameObject playerObject = networkManager.LocalClient.PlayerObject.gameObject;
    //         PlayerController controller = playerObject.GetComponent<PlayerController>();

    //         if (controller != null)
    //         {
    //             // 입력 재개
    //             controller.SetInputEnabled(true);
    //             Debug.Log("[NetworkGameManager] 플레이어 입력 재개");
    //         }
    //     }
    // }

    // ==================== 매치메이킹 서버 연동 ====================

    private IEnumerator RegisterAndHeartbeat()
    {
        // 서버 등록
        yield return StartCoroutine(RegisterServer());

        // 하트비트 반복 (WaitForSecondsRealtime 사용 - Time.timeScale 영향 없음)
        while (true)
        {
            yield return new WaitForSecondsRealtime(heartbeatInterval);
            yield return StartCoroutine(SendHeartbeat());
        }
    }

    private IEnumerator RegisterServer()
    {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        int port = transport != null ? transport.ConnectionData.Port : 7779;

        // 🔥 ASG 대응: 감지된 Public IP 사용
        string publicIP = GetCurrentPublicIP();

        // 🔥 Server ID를 IP:Port 조합으로 생성 (ASG에서 중복 방지)
        string serverId = $"game-server-{publicIP.Replace(".", "-")}-{port}";

        string jsonData =
            $"{{\"server_id\":\"{serverId}\",\"ip\":\"{publicIP}\",\"port\":{port},\"current_players\":0,\"max_players\":100,\"status\":\"AVAILABLE\"}}";

        using (UnityWebRequest www =
               new UnityWebRequest($"{matchmakingServerUrl}/api/server/register", "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"✅ 매치메이킹 서버 등록 완료: {serverId}");
            }
            else
            {
                Debug.LogWarning($"⚠️ 매치메이킹 서버 등록 실패: {www.error}");
            }
        }
    }

    private IEnumerator SendHeartbeat()
    {
        // 서버 전용 가드
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
        {
            Debug.LogWarning("[Heartbeat] Not a server or NetworkManager is null. Heartbeat stopped.");
            yield break;
        }

        int port = 7779;
        int currentPlayers = 0;
        string status = "AVAILABLE";
        string serverId = ""; // 🔥 try 블록 밖에서 선언

        try
        {
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            port = transport != null ? transport.ConnectionData.Port : 7779;

            // 🔥 ASG 대응: IP:Port 조합으로 고유한 Server ID 생성
            string publicIP = GetCurrentPublicIP();
            serverId = $"game-server-{publicIP.Replace(".", "-")}-{port}";

            currentPlayers = NetworkManager.Singleton.ConnectedClients.Count
                           + WebSocketManager.Instance.ConnectedBotCount;

            var gameManager = GameManager.Instance;
            if (gameManager != null && gameManager.IsSpawned)
            {
                try
                {
                    bool isGame = gameManager.IsGame;
                    bool isLobby = gameManager.IsLobby;

                    if (isGame || !isLobby)
                    {
                        status = "IN_GAME";
                    }
                    else if (currentPlayers >= 100)
                    {
                        status = "FULL";
                    }

                    Debug.Log(
                        $"[Heartbeat] Port:{port}, Players:{currentPlayers}, Status:{status}, IsGame:{isGame}, IsLobby:{isLobby}");

                    // IN_GAME 상태인데 플레이어 0명이면 서버 종료
                    if (status == "IN_GAME" && currentPlayers == 0)
                    {
                        Debug.LogWarning("[Heartbeat] IN_GAME 상태에서 플레이어 0명 → 서버 강제 종료 로직 실행");

#if UNITY_SERVER && !UNITY_EDITOR
                        Application.Quit();
#else
                        Debug.Log("[Heartbeat] (에디터/클라 빌드이므로 실제 종료는 하지 않음)");
#endif
                        yield break;
                    }
                }
                catch (System.Exception innerEx)
                {
                    Debug.LogError(
                        $"[Heartbeat] Exception accessing GameManager properties: {innerEx.Message}");
                }
            }
            else
            {
                Debug.LogWarning(
                    $"[Heartbeat] Port:{port}, Players:{currentPlayers}, Status:{status}, GameManager not available (instance={gameManager != null}, spawned={gameManager?.IsSpawned})");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError(
                $"[Heartbeat] Exception in heartbeat preparation: {e.Message}\n{e.StackTrace}");
        }

        // 🔥 ASG 대응: IP:Port 조합으로 고유한 Server ID 생성 (위에서 이미 선언됨)
        string jsonData =
            $"{{\"server_id\":\"{serverId}\",\"port\":{port},\"current_players\":{currentPlayers},\"status\":\"{status}\",\"cpu_usage\":0.0,\"memory_usage\":0.0}}";

        using (UnityWebRequest www =
               new UnityWebRequest($"{matchmakingServerUrl}/api/server/heartbeat", "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"⚠️ 하트비트 전송 실패: {www.error}");
            }
        }
    }

    // 게임 종료 신호를 매치메이킹 서버에 전송
    public void NotifyGameEnded()
    {
        StartCoroutine(SendGameEndedSignal());
    }

    private IEnumerator SendGameEndedSignal()
    {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        int port = transport != null ? transport.ConnectionData.Port : 7779;

        // 🔥 ASG 대응: IP:Port 조합으로 고유한 Server ID 생성
        string publicIP = GetCurrentPublicIP();
        string serverId = $"game-server-{publicIP.Replace(".", "-")}-{port}";

        string jsonData = $"{{\"server_id\":\"{serverId}\",\"port\":{port}}}";

        using (UnityWebRequest www = new UnityWebRequest($"{matchmakingServerUrl}/api/server/game-ended", "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"✅ 게임 종료 신호 전송 완료: {serverId}");
            }
            else
            {
                Debug.LogWarning($"⚠️ 게임 종료 신호 전송 실패: {www.error}");
            }
        }
    }
}
