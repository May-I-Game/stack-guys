using System.Collections;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// LoginUIManager
///  - 게임 서버에 직접 연결
///  - 필요 시 매치메이킹 서버를 통해 게임 서버 정보 받아 연결
///  - WebGL에서는 WebSocket 모드로 전환
/// </summary>
public class LoginUIManager : MonoBehaviour
{
    // 서버 접속 주소 (에디터: 로컬, 빌드: EC2)
    [SerializeField] public string serverAddress = "127.0.0.1";
    [SerializeField] ushort serverPort = 7779;

    [Header("Production Server")]
    [SerializeField] private string productionServerAddress = "3.37.88.2";
    [SerializeField] private ushort productionServerPort = 7779;
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private Camera characterSelectCamera;
    [SerializeField] private GameObject characterSelectPopup;

    [Header("Matchmaking Server")]
    [SerializeField] private string productionMatchmakingUrl = "http://matchmaking-alb-1609632759.ap-northeast-2.elb.amazonaws.com";        // 프로덕션 매치메이킹 서버
    [SerializeField] private bool useMatchmaking = false;                                       // 매치메이킹 사용 여부

    [Header("Loading UI")]
    [Tooltip("로딩 중 표시할 UI 패널 (캔버스에 미리 배치되어 있어야 함)")]
    public GameObject loadingPanel;

    private int clientCharIndex;
    private string clientName;
    private bool isConnecting = false;
    private AudioSource audioSource;

    private const int MAX_NAME_BYTES = 48;

    // ========================== 매치메이킹 응답 클래스 ==========================
    [System.Serializable]
    private class MatchmakingResponse
    {
        public bool success;
        public string ticket_id;    // 매칭 추적용 티켓 ID
        public string player_id;
        public string status;       // QUEUED, MATCHED, FAILED ...
        public string message;
    }

    [System.Serializable]
    private class TicketStatusResponse
    {
        public string ticket_id;
        public string player_id;
        public string status;       // QUEUED, MATCHED, FAILED, TIMEOUT ...
        public string server_ip;    // 매칭 성공 시 게임 서버 IP
        public int server_port;     // 매칭 성공 시 게임 서버 포트
        public string session_id;
        public string message;
    }

    void Start()
    {
        characterSelectPopup?.SetActive(false);
        audioSource = GetComponent<AudioSource>();
        loadingPanel.SetActive(false);

        audioSource = GetComponent<AudioSource>();

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
        CancelInvoke(nameof(CheckConnectionTimeout));
    }

    private void OnClientConnected(ulong clientId)
    {
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            Debug.Log("✅ Successfully connected to server!");
            CancelInvoke(nameof(CheckConnectionTimeout));
            isConnecting = false;
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            Debug.Log("Disconnected from server");
            if (isConnecting)
            {
                Debug.Log("❌ Connection failed");
                isConnecting = false;
                OnConnectionFailed("Client connection attempt failed.");
            }
        }
    }

    // ========================== 캐릭터 선택 ==========================
    public void OnClickPresentCharacter()
    {
        if (characterSelectPopup == null) return;
        characterSelectPopup.SetActive(true);
    }

    private void PlayButtonSound()
    {
        if (audioSource != null && audioSource.clip != null)
            audioSource.PlayOneShot(audioSource.clip);
    }

    private void OnCharacterSelected(int index)
    {
        if (characterSelectCamera != null)
            characterSelectCamera.transform.localPosition = new Vector3(-2f * index, 0, 0);

        clientCharIndex = index;
        characterSelectPopup?.SetActive(false);
    }

    public void OnClickOuterPanel()
    {
        characterSelectPopup?.SetActive(false);
    }

    // ========================== Start 버튼 ==========================
    public void OnClickStart()
    {
        if (isConnecting)
        {
            Debug.Log("이미 연결 중입니다...");
            return;
        }

        // 모바일 기기 감지 및 전체화면 전환
        if (IsMobileDevice())
        {
            RequestFullScreen();
        }

        // 1. 로딩 UI 활성화 (모달 창 띄우기)
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(true);
            // (선택 사항) 로딩 애니메이션 시작 가능
        }

        clientName = (nameInput?.text ?? "").Trim();
        if (string.IsNullOrEmpty(clientName))
            clientName = "Player_" + Random.Range(1000, 9999);


        // ✅ 매치메이킹 사용 여부에 따라 분기
        if (useMatchmaking)
        {
            // 매치메이킹 서버를 통해서 "어느 게임 서버로 갈지"를 먼저 정함
            StartCoroutine(FindGameAndConnect(productionMatchmakingUrl));
        }
        else
        {
            // 기존 방식: 지정된 게임 서버에 직접 연결
#if UNITY_EDITOR
            ConnectToServer(serverAddress, serverPort);
#else
            ConnectToServer(productionServerAddress, productionServerPort);
#endif
        }
    }

    // 연결 실패 시 호출될 함수
    public void OnConnectionFailed(string reason)
    {
        Debug.LogError($"연결 실패: {reason}");

        // 1. 로딩 UI 비활성화
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(false);
        }
        isConnecting = false;

        // TODO: 필요하면 여기서 팝업 띄워서 에러 메시지 보여주기
    }

    // ========================== 서버 연결 로직 ==========================
    private void ConnectToServer(string serverAddress, ushort serverPort)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null)
        {
            Debug.LogError("❌ NetworkManager not found!");
            OnConnectionFailed($"NetworkManager not found!");
            isConnecting = false;
            return;
        }

        var transport = nm.GetComponent<UnityTransport>();
        if (transport == null)
        {
            Debug.LogError("❌ UnityTransport missing on NetworkManager");
            OnConnectionFailed($"missing on NetworkManager");
            isConnecting = false;
            return;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        transport.UseWebSockets = true;  // WebGL 강제 WebSocket
#endif
        transport.SetConnectionData(serverAddress, serverPort);
        Debug.Log($"Connecting to {serverAddress}:{serverPort} ...");

        // ✅ Transport 상태 확인
        Debug.Log($"[Transport] Protocol: {transport.Protocol}");
        Debug.Log($"[Transport] UseWebSockets: {transport.UseWebSockets}");

        // ConnectionData 구성: [1바이트 캐릭터][이름(UTF8)]
        byte[] nameBytes = TruncateUtf8(clientName, MAX_NAME_BYTES);
        byte[] payload = new byte[1 + nameBytes.Length];
        payload[0] = (byte)clientCharIndex;
        System.Array.Copy(nameBytes, 0, payload, 1, nameBytes.Length);

        nm.NetworkConfig.ConnectionData = payload;
        PlayerPrefs.SetString("player_name", clientName);
        PlayerPrefs.Save();

        if (!nm.StartClient())
        {
            Debug.LogError("❌ StartClient failed");
            OnConnectionFailed($"StartClient failed");
            isConnecting = false;
            return;
        }

        // 연결 시도 타임아웃 체크 (isConnecting 플래그는 매치메이킹 쪽에서 세팅됨)
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
            Debug.Log("⏰ Connection timeout!");
            if (NetworkManager.Singleton.IsClient)
                NetworkManager.Singleton.Shutdown();
            isConnecting = false;

            OnConnectionFailed("Connection attempt timed out (10s).");
        }
    }

    // ========================== 매치메이킹 로직 ==========================
    private IEnumerator FindGameAndConnect(string matchmakingUrl)
    {
        Debug.Log($"🎮 매치메이킹 요청: {matchmakingUrl}");
        isConnecting = true;

        // 1. 매치메이킹 요청
        string findGameUrl = $"{matchmakingUrl}/api/find-game";
        string jsonBody = $"{{\"player_id\":\"{System.Guid.NewGuid()}\"}}";

        using (UnityWebRequest www = new UnityWebRequest(findGameUrl, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"❌ 매치메이킹 요청 실패: {www.error}");
                OnConnectionFailed($"매치메이킹 요청 실패: {www.error}");
                yield break;
            }

            string responseText = www.downloadHandler.text;
            Debug.Log($"📨 매치메이킹 응답: {responseText}");

            MatchmakingResponse response = JsonUtility.FromJson<MatchmakingResponse>(responseText);
            if (!response.success)
            {
                Debug.LogError($"❌ 매치메이킹 실패: {response.message}");
                OnConnectionFailed($"매치메이킹 실패: {response.message}");
                yield break;
            }

            // 2. 티켓 상태 폴링 시작
            yield return StartCoroutine(PollTicketStatus(matchmakingUrl, response.ticket_id, response.player_id));
        }
    }

    private IEnumerator PollTicketStatus(string matchmakingUrl, string ticketId, string playerId)
    {
        Debug.Log($"🔍 티켓 상태 확인 시작: {ticketId}");
        float timeoutSeconds = 60f;
        float elapsed = 0f;

        while (elapsed < timeoutSeconds)
        {
            string statusUrl = $"{matchmakingUrl}/api/ticket-status?ticket_id={ticketId}&player_id={playerId}";

            using (UnityWebRequest www = UnityWebRequest.Get(statusUrl))
            {
                yield return www.SendWebRequest();

                if (www.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"⚠️ 티켓 상태 조회 실패: {www.error}");
                    yield return new WaitForSeconds(2f);
                    elapsed += 2f;
                    continue;
                }

                string responseText = www.downloadHandler.text;
                TicketStatusResponse status = JsonUtility.FromJson<TicketStatusResponse>(responseText);

                Debug.Log($"📊 티켓 상태: {status.status}");

                // 매칭 성공 → 해당 서버로 연결
                if (status.status == "MATCHED" && !string.IsNullOrEmpty(status.server_ip))
                {
                    Debug.Log($"✅ 매칭 성공! 서버: {status.server_ip}:{status.server_port}");
                    ConnectToServer(status.server_ip, (ushort)status.server_port);
                    yield break;
                }
                // 매칭 실패 / 타임아웃
                else if (status.status == "FAILED" || status.status == "TIMEOUT")
                {
                    Debug.LogError($"❌ 매칭 실패: {status.message}");
                    OnConnectionFailed($"매칭 실패: {status.message}");
                    yield break;
                }

                // 2초 대기 후 재시도
                yield return new WaitForSeconds(2f);
                elapsed += 2f;
            }
        }

        // 60초 타임아웃
        Debug.LogError("⏰ 매칭 타임아웃");
        OnConnectionFailed("매칭 타임아웃 (60초)");
    }

    // ========================== 모바일 기기 감지 및 전체화면 ==========================

    // 모바일 기기 감지
    private bool IsMobileDevice()
    {
        // WebGL에서 실행 중인지 확인
        if (Application.platform == RuntimePlatform.WebGLPlayer)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // JavaScript를 통해 User Agent 확인
            return IsMobileUserAgent();
#else
            return false;
#endif
        }
        else
        {
            // 네이티브 플랫폼에서는 SystemInfo 사용
            return SystemInfo.operatingSystem.Contains("Android") ||
                   SystemInfo.operatingSystem.Contains("iPhone") ||
                   SystemInfo.operatingSystem.Contains("iPad");
        }
    }

    // WebGL에서 전체화면 요청
    private void RequestFullScreen()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // JavaScript를 통해 전체화면 요청
        RequestWebGLFullScreen();
#else
        // 네이티브 플랫폼에서는 Screen API 사용
        Screen.fullScreen = true;
#endif
        Debug.Log("📱 모바일 기기 감지: 전체화면 요청");
    }

    // JavaScript 함수 선언 (WebGL 전용)
    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern bool IsMobileUserAgent();

    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern void RequestWebGLFullScreen();
}
