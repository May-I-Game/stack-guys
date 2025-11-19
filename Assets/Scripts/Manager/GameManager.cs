using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

public enum GameState
{
    Lobby,
    Playing,
    Ended
}

public class GameManager : NetworkBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text gameStartcountdown; // 로비에서 게임 시작 전 카운트
    [SerializeField] private TMP_Text gameEndcountdown; // 1등이 결정난 이후 10초 카운트
    [SerializeField] private TMP_Text inGameReadyText; // 3..2..1.. 시작 텍스트
    [SerializeField] private TMP_Text NowPlayerCount; // 현재 접속자 수
    [SerializeField] private TMP_Text firstPlaceText;
    [SerializeField] private TMP_Text secondPlaceText;
    [SerializeField] private TMP_Text thirdPlaceText;
    [SerializeField] private TMP_Text QualifiedText;
    [SerializeField] private GameObject Mobile;
    [SerializeField] private GameObject FPSCount;
    [SerializeField] private GameObject PingCount;
    [SerializeField] private GameObject LobbyUI;
    [SerializeField] private GameObject gameUI;

    [Header("Settings")]
    [SerializeField] private float startCountdownTime = 5f;
    [SerializeField] private float endCountdownTime = 10.1f;
    [SerializeField] private string mainSceneName = "Login";

    [Header("Spawn Points")]
    [SerializeField] private Transform[] lobbySpawnPoints;
    [SerializeField] private Transform[] gameSpawnPoints;

    [Header("Timeline")]
    [SerializeField] private PlayableDirector timeline;

    [Header("Audio")]
    [SerializeField] private AudioSource lobbyBGM;
    [SerializeField] private AudioSource trackBGM;
    [SerializeField] private AudioSource victtoryBGM;
    [SerializeField] private AudioSource countdownAudioSource; // 카운트다운 효과음 소스
    [SerializeField] private AudioClip lobbyCountdownClip; // 로비 카운트다운 효과음 (5, 4, 3, 2, 1)
    [SerializeField] private AudioClip gameCountdownClip; // 인게임 카운트다운 효과음 (3, 2, 1)
    [SerializeField] private AudioClip countdownStartClip; // START 효과음
    [Range(0f, 1f)][SerializeField] private float countdownVolume = 0.7f;

    [Header("Podium")]
    [SerializeField] private Transform firstPlacePodium;
    [SerializeField] private Transform secondPlacePodium;
    [SerializeField] private Transform thirdPlacePodium;
    [SerializeField] private PlayableDirector podiumTimeline;
    [SerializeField] private float podiumTimelineDuration = 12f; // 타임라인 길이 (수동 설정)

    public bool IsLobby => currentGameState.Value == GameState.Lobby;
    public bool IsGame => currentGameState.Value == GameState.Playing;

    private bool isCountingDown = false;
    private Coroutine countdownCoroutine;

    private NetworkVariable<GameState> currentGameState = new NetworkVariable<GameState>(GameState.Lobby);
    private NetworkVariable<float> remainingTime = new NetworkVariable<float>(0f);
    private NetworkList<FixedString32Bytes> rankings;

    //시네마틱 실행용 동기화 시간 변수
    private NetworkVariable<double> timelineStartTime = new NetworkVariable<double>(0);
    private NetworkVariable<bool> shouldPlayTimeline = new NetworkVariable<bool>(false);
    private const float SYNC_BUFFER = 0.3f;

    //시상식 시네마틱 동기화 시간 변수
    private NetworkVariable<double> podiumTimelineStartTime = new NetworkVariable<double>(0);
    private NetworkVariable<bool> shouldPlayPodiumTimeline = new NetworkVariable<bool>(false);

    //로비 및 게임 종료 시 사용
    private NetworkVariable<int> currentPlayerCount = new NetworkVariable<int>(0);
    private NetworkVariable<bool> isStartCountdownActive = new NetworkVariable<bool>(false);
    private NetworkVariable<bool> isEndCountdownActive = new NetworkVariable<bool>(false);
    private NetworkVariable<bool> isGameReadyCountdownActive = new NetworkVariable<bool>(false);

    public static GameManager Instance;

    private void Awake()
    {
        // 싱글톤 패턴
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }

        rankings = new NetworkList<FixedString32Bytes>();
    }

    public override void OnNetworkSpawn()
    {
        // 커서 관리
        //Cursor.lockState = CursorLockMode.Locked;
        //Cursor.visible = false;

        if (IsServer)
        {
            currentGameState.Value = GameState.Lobby;

            NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;
        }

        if (IsClient)
        {
            remainingTime.OnValueChanged += UpdateCountDownUI;
            shouldPlayTimeline.OnValueChanged += OnTimelineTriggered; // 시네마틱 동기화 변수
            shouldPlayPodiumTimeline.OnValueChanged += OnPodiumTimelineTriggered;

            // 플레이어 수 변경 감지
            currentPlayerCount.OnValueChanged += UpdatePlayerCountUI;
            isStartCountdownActive.OnValueChanged += UpdateStartCountdownVisibility;
            isEndCountdownActive.OnValueChanged += UpdateEndCountdownVisibility;

            // 게임 도중 준비 카운트다운 감지
            isGameReadyCountdownActive.OnValueChanged += OnGameReadyCountdownChanged;

            // 도착한 플레이어 수 변경 감지
            rankings.OnListChanged += OnRankingsChanged;

            // 초기 상태 동기화 (새로 접속한 클라이언트를 위해)
            UpdatePlayerCountUI(0, currentPlayerCount.Value);
            UpdateStartCountdownVisibility(false, isStartCountdownActive.Value);
            UpdateEndCountdownVisibility(false, isEndCountdownActive.Value);
            UpdateQualifiedUI();

            // 카운트다운이 진행 중이면 현재 시간도 업데이트
            if (isStartCountdownActive.Value || isEndCountdownActive.Value)
            {
                UpdateCountDownUI(0, remainingTime.Value);
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
        }

        if (IsClient)
        {
            remainingTime.OnValueChanged -= UpdateCountDownUI;
            shouldPlayTimeline.OnValueChanged -= OnTimelineTriggered; // 시네마틱 동기화 변수

            // 플레이어 수 변경 감지
            currentPlayerCount.OnValueChanged -= UpdatePlayerCountUI;
            isStartCountdownActive.OnValueChanged -= UpdateStartCountdownVisibility;
            isEndCountdownActive.OnValueChanged -= UpdateEndCountdownVisibility;

            isGameReadyCountdownActive.OnValueChanged -= OnGameReadyCountdownChanged;

            // 도착한 플레이어 수 변경 감지
            rankings.OnListChanged -= OnRankingsChanged;
        }
    }
    private void Update()
    {
        // 클라이언트에서 Ctrl+Y 키 입력 감지
        if (IsClient && currentGameState.Value == GameState.Lobby)
        {
            if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.Y))
            {
                RequestStartGameServerRpc();
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestStartGameServerRpc()
    {
        // 이미 카운트다운 중이면 무시
        if (isCountingDown) return;

        // 조건 없이 바로 카운트다운 시작
        isCountingDown = true;
        isStartCountdownActive.Value = true;
        countdownCoroutine = StartCoroutine(StartGameCountdown());
        Debug.Log("게임 시작 카운트다운 시작! (Ctrl+Y 입력)");
    }

    private void HandleClientConnected(ulong clientId)
    {
        // 새로 접속한 플레이어를 로비 스폰 지점으로 이동
        MovePlayerToLobby(clientId);
        CheckPlayerCount();
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        CheckPlayerCount();
    }

    private void MovePlayerToLobby(ulong clientId)
    {
        NetworkObject playerObject = NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject;
        if (playerObject == null) return;

        Transform randomSpawnPoint = lobbySpawnPoints[Random.Range(0, lobbySpawnPoints.Length)];
        PlayerController player = playerObject.GetComponent<PlayerController>();
        player.DoRespawn(randomSpawnPoint.position, randomSpawnPoint.rotation);
    }

    private void CheckPlayerCount()
    {
        int playerCount = NetworkManager.Singleton.ConnectedClientsList.Count;
        currentPlayerCount.Value = playerCount;
    }

    private void UpdatePlayerCountUI(int priviousValue, int newValue)
    {
        if (NowPlayerCount != null)
        {
            NowPlayerCount.text = $"현재 참가자: {newValue}명";
        }
        UpdateQualifiedUI();
    }

    private void OnRankingsChanged(NetworkListEvent<FixedString32Bytes> changeEvent)
    {
        UpdateQualifiedUI();

        UpdateRankingUI();
    }

    private void UpdateQualifiedUI()
    {
        if (QualifiedText != null)
        {
            QualifiedText.text = $"도착 : {rankings.Count} / {currentPlayerCount.Value}";
        }
    }
    private void UpdateStartCountdownVisibility(bool previousValue, bool newValue)
    {
        if (gameStartcountdown != null)
        {
            gameStartcountdown.gameObject.SetActive(newValue);
        }
    }
    private void UpdateEndCountdownVisibility(bool previousValue, bool newValue)
    {
        if (gameEndcountdown != null)
        {
            gameEndcountdown.gameObject.SetActive(newValue);
        }
    }
    public void PlayerReachedGoal(string playerName, ulong clientId)
    {
        if (!IsServer) return;                 // 안전 가드
        if (currentGameState.Value != GameState.Playing) return;

        var playerObj = NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject;
        if (playerObj == null) return;


        var player = playerObj.GetComponent<PlayerController>();
        if (player != null)
        {
            player.inputEnabled.Value = false;
            player.ReleaseGrab();
            player.ForceClearInputOnServer();
        }


        rankings.Add(playerName);              // NetworkList는 서버에서만 쓰기

        if (rankings.Count == 1 && !isCountingDown)
        {
            isEndCountdownActive.Value = true;
            StartCoroutine(EndGameCountdown());
        }
    }

    // ✅ 클라이언트가 보낼 때만 쓰는 래퍼 RPC (선택)
    [ServerRpc(RequireOwnership = false)]
    public void PlayerReachedGoalServerRpc(string playerName, ulong clientId)
    {
        PlayerReachedGoal(playerName, clientId);
    }

    private IEnumerator StartGameCountdown()
    {
        isCountingDown = true;
        bool inputDisabled = false;

        // 게임 시작 카운트다운
        remainingTime.Value = startCountdownTime;
        while (remainingTime.Value > 0)
        {
            remainingTime.Value -= Time.deltaTime;

            // 텔레포트 전에 1초 전에 입력을 막음 (텔레포트 이후 위치 이동 버그 수정)
            if (remainingTime.Value <= 1 && !inputDisabled)
            {
                DisableAllPlayersInputOnServer();
                inputDisabled = true;
            }

            yield return null;
        }

        isCountingDown = false;

        // 게임 시작 처리
        StartGame();
    }

    private IEnumerator EndGameCountdown()
    {
        isCountingDown = true;

        // 게임 종료 카운트다운
        remainingTime.Value = endCountdownTime+0.1f;
        while (remainingTime.Value > 0)
        {
            remainingTime.Value -= Time.deltaTime;
            yield return null;
        }

        isCountingDown = false;

        // 게임 종료 처리
        EndGame();
    }

    private void StartGame()
    {
        if (!IsServer) return;

        currentGameState.Value = GameState.Playing;

        HideLobbyUIShowGameUIClientRpc();

        int i = 0;
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            NetworkObject playerObject = client.PlayerObject;
            if (playerObject == null) continue;

            // 봇이 아닌 실제 플레이어만 처리
            BotController botController = playerObject.GetComponent<BotController>();
            if (botController != null) continue;

            // 순환하면서 스폰 위치 지정
            Vector3 spawnPos = gameSpawnPoints[i % gameSpawnPoints.Length].position;

            // 플레이어 상태 초기화 (움직여진 상태에서 텔레포트 버그 수정)
            PlayerController controller = playerObject.GetComponent<PlayerController>();
            if (controller != null)
            {
                controller.inputEnabled.Value = false;
                controller.ReleaseGrab();
                controller.ForceClearInputOnServer();
            }

            // 해당 플레이어에게 텔레포트 명령
            controller.DoRespawn(spawnPos, Quaternion.identity);

            i++;
        }

        // 남는 자리 봇으로 스폰
        if (BotManager.Singleton != null)
        {
            BotManager.Singleton.SpawnBotsFromIndex(i, gameSpawnPoints);
        }
        else
        {
            Debug.LogWarning("[GameManager] BotManager.Singleton null");
        }

        timelineStartTime.Value = NetworkManager.Singleton.ServerTime.Time + SYNC_BUFFER;
        shouldPlayTimeline.Value = true;

        StartCoroutine(ServerEnableBotsAfterCinematic()); // 시네마틱이 끝나고 서버에서 봇을 활성화
    }

    private void EndGame()
    {
        if (!IsServer) return;

        // 매치메이킹 서버에 게임 종료 신호 전송
        NetworkGameManager networkManager = FindObjectOfType<NetworkGameManager>();
        if (networkManager != null)
        {
            networkManager.NotifyGameEnded();
        }

        // 클라에 결과 화면 표시
        StartCoroutine(PodiumCeremony());
    }

    // 클라이언트에 BGM 전환을 지시
    [ClientRpc]
    private void PlayVictoryBGMClientRpc()
    {
        // 클라이언트에서 트랙 BGM 정지 및 빅토리 BGM 재생
        if (trackBGM != null)
        {
            trackBGM.Stop();
        }

        if (victtoryBGM != null)
        {
            // 안전하게 활성화 및 재생
            if (!victtoryBGM.gameObject.activeInHierarchy)
                victtoryBGM.gameObject.SetActive(true);

            victtoryBGM.Stop();
            victtoryBGM.Play();
        }
    }

    // 현재 사용 중인 방식 (GameState를 뒤로 미루기)
    private IEnumerator PodiumCeremony()
    {
        Debug.Log("[GameManager - SERVER] 시상식 시작");
        PlayVictoryBGMClientRpc();
        // 1, 2, 3등 플레이어를 시상대로 이동
        Dictionary<string, ulong> playerNameToClientId = new Dictionary<string, ulong>();

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            NetworkObject playerObject = client.PlayerObject;
            if (playerObject == null) continue;

            // 봇은 제외
            BotController botController = playerObject.GetComponent<BotController>();
            if (botController != null) continue;

            PlayerController player = playerObject.GetComponent<PlayerController>();
            if (player == null) continue;

            string playerName = player.GetPlayerName();
            playerNameToClientId[playerName] = client.ClientId;
        }

        // 1등 이동
        if (rankings.Count > 0 && firstPlacePodium != null)
        {
            string firstName = rankings[0].ToString();
            if (playerNameToClientId.ContainsKey(firstName))
            {
                MovePlayerToPodiumPosition(playerNameToClientId[firstName], firstPlacePodium);
                Debug.Log($"[GameManager - SERVER] 1등 {firstName} 시상대로 이동");
            }
        }

        // 2등 이동
        if (rankings.Count > 1 && secondPlacePodium != null)
        {
            string secondName = rankings[1].ToString();
            if (playerNameToClientId.ContainsKey(secondName))
            {
                MovePlayerToPodiumPosition(playerNameToClientId[secondName], secondPlacePodium);
                Debug.Log($"[GameManager - SERVER] 2등 {secondName} 시상대로 이동");
            }
        }

        // 3등 이동
        if (rankings.Count > 2 && thirdPlacePodium != null)
        {
            string thirdName = rankings[2].ToString();
            if (playerNameToClientId.ContainsKey(thirdName))
            {
                MovePlayerToPodiumPosition(playerNameToClientId[thirdName], thirdPlacePodium);
                Debug.Log($"[GameManager - SERVER] 3등 {thirdName} 시상대로 이동");
            }
        }

        // 시상식 타임라인 동기화 시작
        podiumTimelineStartTime.Value = NetworkManager.Singleton.ServerTime.Time + SYNC_BUFFER;
        shouldPlayPodiumTimeline.Value = true;

        Debug.Log($"[GameManager - SERVER] 시상식 타임라인 시작 신호 전송");

        // 시상식 지속 시간 대기
        Debug.Log($"[GameManager - SERVER] {podiumTimelineDuration}초 대기 시작");
        yield return new WaitForSeconds(podiumTimelineDuration);

        Debug.Log("[GameManager - SERVER] 시상식 대기 완료, 결과 화면 표시");

        // 결과 화면 표시
        ShowResultsClientRpc();

        // 시상식이 완전히 끝난 후 GameState.Ended 설정 (최적화)
        currentGameState.Value = GameState.Ended;
        Debug.Log("[GameManager - SERVER] GameState를 Ended로 변경 (물리 연산 중단)");
    }

    private void OnPodiumTimelineTriggered(bool previous, bool current)
    {
        if (current && !previous)
        {
            StartCoroutine(PlayPodiumTimelineAtSyncTime());
        }
    }

    private IEnumerator PlayPodiumTimelineAtSyncTime()
    {
        Debug.Log("[GameManager] 시상식 타임라인 시작 대기 중...");

        // 네트워크 시간과 동기화
        while (NetworkManager.Singleton.ServerTime.Time < podiumTimelineStartTime.Value)
        {
            yield return null;
        }

        Debug.Log("[GameManager] 시상식 타임라인 재생 시작");

        // UI 숨기기
        if (gameEndcountdown != null)
            gameEndcountdown.gameObject.SetActive(false);
        if (Mobile != null)
            Mobile.SetActive(false);
        if (FPSCount != null)
            FPSCount.SetActive(false);
        if (PingCount != null)
            PingCount.SetActive(false);
        if (LobbyUI != null)
            LobbyUI.SetActive(false);
        if (gameUI != null)
            gameUI.SetActive(false);
        UIManager.Instance.ToggleOptionPanel(false);
        UIManager.Instance.ToggleOptionButton(false);
        UIManager.Instance.ToggleGuidePanel(false);
        UIManager.Instance.ToggleGuideButton(false);
        // 시상식 타임라인 활성화 및 재생
        if (podiumTimeline != null)
        {
            podiumTimeline.gameObject.SetActive(true);
            podiumTimeline.Play();
            Debug.Log($"[GameManager] 타임라인 재생 중... 길이: {podiumTimeline.duration}초");
        }
        else
        {
            Debug.LogWarning("[GameManager] podiumTimeline이 null입니다!");
        }

        // 커서 숨기기 (시상식 중에는)
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void MovePlayerToPodiumPosition(ulong clientId, Transform podiumTransform)
    {
        if (!NetworkManager.Singleton.ConnectedClients.ContainsKey(clientId))
        {
            Debug.LogWarning($"[Podium] ClientId {clientId}를 찾을 수 없습니다.");
            return;
        }

        NetworkObject playerObject = NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject;
        if (playerObject == null)
        {
            Debug.LogWarning($"[Podium] ClientId {clientId}의 PlayerObject를 찾을 수 없습니다.");
            return;
        }
        PlayerController player = playerObject.GetComponent<PlayerController>();
        player.inputEnabled.Value = false;
        player.ForceClearInputOnServer();

        // 텔레포트 (시상대 위치로 이동)
        player.DoRespawn(podiumTransform.position, podiumTransform.rotation);
    }

    private void OnGameReadyCountdownChanged(bool previous, bool current)
    {
        if (inGameReadyText != null)
        {
            inGameReadyText.gameObject.SetActive(current);
            if (!current)
            {
                inGameReadyText.text = "";
            }
            else
            {
                // 카운트다운 활성화 시 초기 텍스트는 빈 문자열
                inGameReadyText.text = "";
            }
        }
    }

    private IEnumerator ServerEnableBotsAfterCinematic()
    {
        // 클라이언트면 종료
        if (!IsServer) yield break;

        // 타임라인 종료 시각 = 시작 시각 + 재생 길이
        double target = timelineStartTime.Value + timeline.duration;

        // 한 프레임씩 대기
        while (NetworkManager.Singleton.ServerTime.Time < target)
            yield return null;

        // 2. 인게임 카운트다운 시작 (3, 2, 1)
        isGameReadyCountdownActive.Value = true;
        remainingTime.Value = 3.1f; // 3초 설정 (3.1로 시작해야 3이 표시되며 소리 재생됨)

        while (remainingTime.Value > 0)
        {
            remainingTime.Value -= Time.deltaTime;
            yield return null;
        }

        // 시네마틱이 끝나고 움직임 활성화
        BotManager.Singleton?.EnableAllBots();
        EnableAllPlayersInputOnServer();

        // UI 처리를 위해 remainingTime을 확실한 음수값으로 설정 (클라이언트가 "START!"를 띄우게 됨)
        remainingTime.Value = -1f;

        // 플레이어들은 이미 달리고 있지만, "START!" 텍스트는 1초간 더 보여주기
        yield return new WaitForSeconds(1.0f);

        // 1초 뒤에 UI를 끄기
        isGameReadyCountdownActive.Value = false;
    }

    [ClientRpc]
    private void ShowResultsClientRpc()
    {
        // 시상식 타임라인 끄기
        if (podiumTimeline != null)
        {
            podiumTimeline.Stop();
            podiumTimeline.gameObject.SetActive(false);
            Debug.Log("[GameManager - CLIENT] 타임라인 정지 및 비활성화");
        }

        //카운트 다운 내리기
        gameEndcountdown.gameObject.SetActive(false);

        if (Mobile != null)
        {
            Mobile.SetActive(false);
        }
        if (FPSCount != null)
        {
            FPSCount.SetActive(false);
        }
        if (PingCount != null)
        {
            PingCount.SetActive(false);
        }
        if (LobbyUI != null)
        {
            LobbyUI.SetActive(false);
        }
        if (gameUI != null)
        {
            gameUI.SetActive(false);
        }

        UIManager.Instance.ToggleOptionPanel(false);
        UIManager.Instance.ToggleOptionButton(false);
        UIManager.Instance.ToggleGuidePanel(false);
        UIManager.Instance.ToggleGuideButton(false);

        UIManager.Instance.ToggleResultPanel(true);

        // 커서 보이기
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 순위 표시
        UpdateRankingUI();
    }
    [ClientRpc]
    private void HideLobbyUIShowGameUIClientRpc()
    {
        if (gameStartcountdown != null)
        {
            gameStartcountdown.gameObject.SetActive(false);
        }
        if (NowPlayerCount != null)
        {
            NowPlayerCount.gameObject.SetActive(false);
        }
        if (FPSCount != null)
        {
            FPSCount.gameObject.SetActive(true);
        }
        if (PingCount != null)
        {
            PingCount.gameObject.SetActive(true);
        }
        if (LobbyUI != null)
        {
            LobbyUI.gameObject.SetActive(false);
        }
        if (gameUI != null)
        {
            gameUI.gameObject.SetActive(true);
        }

        UIManager.Instance.ToggleOptionPanel(false);
        UIManager.Instance.ToggleOptionButton(false);
        UIManager.Instance.ToggleGuidePanel(false);
        UIManager.Instance.ToggleGuideButton(false);
    }

    // 서버에서 모든 플레이어의 입력을 차단하고 상태 초기화
    private void DisableAllPlayersInputOnServer()
    {
        if (!IsServer) return;

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            NetworkObject playerObject = client.PlayerObject;
            if (playerObject == null) continue;

            PlayerController controller = playerObject.GetComponent<PlayerController>();
            if (controller != null)
            {
                controller.inputEnabled.Value = false;
                controller.ForceClearInputOnServer();
            }
        }

        Debug.Log("[GameManager] 모든 플레이어 입력 차단 완료");
    }

    // 서버에서 모든 플레이어의 입력을 활성화
    private void EnableAllPlayersInputOnServer()
    {
        if (!IsServer) return;

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            NetworkObject playerObject = client.PlayerObject;
            if (playerObject == null) continue;

            // 봇이 아닌 실제 플레이어만 처리
            BotController botController = playerObject.GetComponent<BotController>();
            if (botController != null) continue;

            PlayerController controller = playerObject.GetComponent<PlayerController>();
            if (controller != null)
            {
                controller.inputEnabled.Value = true;
            }
        }

        Debug.Log("[GameManager] 모든 플레이어 입력 활성화 완료");
    }

    private void UpdateCountDownUI(float prviousValue, float newValue)
    {
        // 로비 게임 시작 전 카운트다운
        if (IsLobby && gameStartcountdown != null)
        {
            int count = Mathf.CeilToInt(newValue);
            int previousCount = Mathf.CeilToInt(prviousValue);

            gameStartcountdown.text = $"{count}초 후 게임이 시작됩니다!!";

            // 숫자가 바뀔 때마다 로비 카운트다운 효과음 재생
            if (count != previousCount && count > 0 && countdownAudioSource != null && lobbyCountdownClip != null)
            {
                countdownAudioSource.PlayOneShot(lobbyCountdownClip, countdownVolume);
            }
        }

        // 시네마틱 직후 게임 시작 카운트 다운 (3, 2, 1, Start!)
        else if (isGameReadyCountdownActive.Value && inGameReadyText != null)
        {
            int count = Mathf.CeilToInt(newValue);
            int previousCount = Mathf.CeilToInt(prviousValue);

            if (count > 0)
            {
                inGameReadyText.text = count.ToString();

                // 숫자가 바뀔 때마다 인게임 카운트다운 효과음 재생 (3, 2, 1)
                if (count != previousCount && countdownAudioSource != null && gameCountdownClip != null)
                {
                    countdownAudioSource.PlayOneShot(gameCountdownClip, countdownVolume);
                }
            }
            else
            {
                inGameReadyText.text = "START!!";

                // START 효과음 재생 (한 번만)
                if (previousCount > 0 && countdownAudioSource != null && countdownStartClip != null)
                {
                    countdownAudioSource.PlayOneShot(countdownStartClip, countdownVolume);
                }
            }
        }

        // 게임 끝나고 10초 카운트다운
        else if (rankings.Count > 0 && gameEndcountdown != null)
        {
            int count = Mathf.CeilToInt(newValue);
            int previousCount = Mathf.CeilToInt(prviousValue);

            gameEndcountdown.text = Mathf.CeilToInt(newValue).ToString();
            // 숫자가 바뀔 때마다 로비 카운트다운 효과음 재생
            if (count != previousCount && count > 0 && countdownAudioSource != null && lobbyCountdownClip != null)
            {
                countdownAudioSource.PlayOneShot(lobbyCountdownClip, countdownVolume);
            }
        }
    }

    private void UpdateRankingUI()
    {
        Debug.Log($"[UpdateRankingUI] 순위 업데이트 - 총 {rankings.Count}명 도착");
        // 1등
        if (rankings.Count > 0 && firstPlaceText != null)
        {
            firstPlaceText.text = $"{rankings[0]}";
        }

        // 2등
        if (rankings.Count > 1 && secondPlaceText != null)
        {
            secondPlaceText.text = $"{rankings[1]}";
        }
        else if (secondPlaceText != null)
        {
            secondPlaceText.text = "-";
        }

        // 3등
        if (rankings.Count > 2 && thirdPlaceText != null)
        {
            thirdPlaceText.text = $"{rankings[2]}";
        }
        else if (thirdPlaceText != null)
        {
            thirdPlaceText.text = "-";
        }
    }

    private void GoToMain()
    {
        NetworkManager.Singleton.Shutdown();
        SceneManager.LoadScene(mainSceneName);
    }

    private void OnTimelineTriggered(bool previous, bool current)
    {
        if (current && !previous)
        {
            StartCoroutine(PlayTimelineAtSyncTime());
        }
    }

    private IEnumerator PlayTimelineAtSyncTime()
    {
        //로비 BGM 아웃
        lobbyBGM.Stop();

        // 네트워크 시간과 동기화
        while (NetworkManager.Singleton.ServerTime.Time < timelineStartTime.Value)
        {
            yield return null;
        }

        ToggleGameUI(false);

        //timeline재생
        timeline.Play();
        //트랙 bgm on
        trackBGM.Play();

        //Timeline종료 대기
        yield return new WaitForSeconds((float)timeline.duration);

        ToggleGameUI(true);

        if (!isGameReadyCountdownActive.Value)
        {
            if (inGameReadyText != null) inGameReadyText.gameObject.SetActive(false);
        }
    }

    private void ToggleGameUI(bool isActive)
    {
        if (LobbyUI != null) LobbyUI.SetActive(false); // 로비는 항상 끔
        if (Mobile != null) Mobile.SetActive(isActive);
        if (FPSCount != null) FPSCount.SetActive(isActive);
        if (PingCount != null) PingCount.SetActive(isActive);
        if (gameUI != null) gameUI.SetActive(isActive);

        // 게임 시작 후에는 Options와 Guide를 끔
        UIManager.Instance.ToggleOptionButton(false);
        UIManager.Instance.ToggleGuideButton(false);
    }

    private void OnTimelineFinished(PlayableDirector director)
    {
        director.stopped -= OnTimelineFinished;

        Debug.Log("Timeline 종료, 게임 플레이 시작");
    }

    // 캐릭터의 입력 온 오프는 서버에서만 가능
    //private void DisablePlayerInput()
    //{
    //    var localPlayer = GetLocalPlayer();
    //    if (localPlayer != null)
    //    {
    //        localPlayer.SetInputEnabled(false);
    //        localPlayer.ResetStateServerRpc();
    //    }
    //}
    //private void EnablePlayerInput()
    //{
    //    var localPlayer = GetLocalPlayer();
    //    if (localPlayer != null)
    //    {
    //        localPlayer.SetInputEnabled(true);
    //    }

    //    if (Mobile != null)
    //    {
    //        Mobile.SetActive(true);
    //    }
    //    if (FPSCount != null)
    //    {
    //        FPSCount.SetActive(true);
    //    }
    //    if (PingCount != null)
    //    {
    //        PingCount.SetActive(true);
    //    }
    //    if (LobbyUI != null)
    //    {
    //        LobbyUI.SetActive(true);
    //    }
    //    if (gameUI != null)
    //    {
    //        gameUI.SetActive(true);
    //    }
    //}

    private PlayerController GetLocalPlayer()
    {
        foreach (var player in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
        {
            if (player.IsOwner)
            {
                return player;
            }

        }
        return null;
    }
}