using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class BotManager : NetworkBehaviour
{
    public static BotManager Singleton;                  // 싱글톤 패턴

    [Header("Bot Prefabs")]
    [SerializeField] private NetworkObject[] botPrefabs = new NetworkObject[12];

    [Header("Debug Spawn Points")]
    [SerializeField] private Transform[] debugSpawnPoints;

    private List<GameObject> activeBots = new List<GameObject>();

    private void Awake()
    {
        if (Singleton == null)
        {
            Singleton = this;
        }
        else
        {
            Destroy(gameObject);                        // 중복된 Bot 매니저 삭제
            return;
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            // 디버그 스폰 포인트가 있으면 먼저 생성
            if (debugSpawnPoints != null && debugSpawnPoints.Length > 0)
            {
                StartCoroutine(SpawnDebugBotsDelayed());
            }
        }
    }

    // 디버깅용 봇 스폰
    private IEnumerator SpawnDebugBotsDelayed()
    {
        // 0.5초 대기
        yield return new WaitForSeconds(0.5f);

        for (int i = 0; i < debugSpawnPoints.Length; i++)
        {
            if (debugSpawnPoints[i] != null)
            {
                CreateDebugBot(debugSpawnPoints[i]);
            }
        }

        //Debug.Log($"[BotManager] {debugSpawnPoints.Length}개의 디버그 봇 생성 완료");
    }

    // 디버그 봇 생성 (풀 사용 안 함)
    private void CreateDebugBot(Transform spawnPoint)
    {
        // 90도 회전
        Quaternion spawnRotation = spawnPoint.rotation * Quaternion.Euler(0, 90, 0);

        // 랜덤 봇 프리팹 선택
        NetworkObject selectedBotPrefab = GetRandomBotPrefab();
        if (selectedBotPrefab == null)
        {
            Debug.LogError("[BotManager] 사용 가능한 봇 프리팹이 없음");
            return;
        }

        // 봇 인스턴스 생성
        NetworkObject botNetobj = NetworkManager.Singleton.SpawnManager.InstantiateAndSpawn(
            selectedBotPrefab,
            position: spawnPoint.position,
            rotation: spawnRotation
        );

        // 네트워크 오브젝트 스폰
        if (botNetobj != null)
        {
            BotController botController = botNetobj.GetComponent<BotController>();
            if (botController != null)
            {
                // 봇 이름 설정
                PlayerCanvasManager nameSync = botNetobj.GetComponent<PlayerCanvasManager>();
                if (nameSync != null)
                {
                    //string botName = NetworkBotIdentity.GenerateBotName();
                    string botName = ("Bot");
                    nameSync.SetPlayerName(botName);
                }

                // 입력 차단
                botController.SetInputEnabled(false);
            }

            // activeBots에 추가 (디버그 봇도 활성화)
            activeBots.Add(botNetobj.gameObject);
        }
        else
        {
            Debug.LogError("[BotManager] NetworkObject 컴포넌트가 없음");
        }
    }

    public override void OnDestroy()
    {
        base.OnDestroy();

        if (Singleton == this)
        {
            Singleton = null;
        }
    }

    // startIndex부터 끝까지 봇 배치 (네트워크 부하 분산을 위해 코루틴 사용)
    public void SpawnBotsFromIndex(int startIndex, Transform[] spawnPoints)
    {
        // 서버에서만 봇 생성 가능
        if (!IsServer) return;

        // 봇 프리팹 확인
        if (botPrefabs == null || botPrefabs.Length == 0)
        {
            Debug.LogError("[BotManager] 봇 프리팹이 설정 안됨");
            return;
        }

        // startIndex부터 끝까지 봇 생성할 개수 계산
        int botsToSpawn = spawnPoints.Length - startIndex;

        // 생성할 봇이 없으면 종료
        if (botsToSpawn <= 0)
        {
            return;
        }

        // 네트워크 부하 분산을 위해 코루틴으로 순차 스폰
        StartCoroutine(SpawnBotsSequentially(startIndex, botsToSpawn, spawnPoints));
    }

    // 봇을 시간차로 스폰하여 send queue full 방지
    private IEnumerator SpawnBotsSequentially(int startIndex, int botsToSpawn, Transform[] spawnPoints)
    {
        for (int i = startIndex; i < startIndex + botsToSpawn; i++)
        {
            // 랜덤 봇 프리팹 선택
            NetworkObject selectedBotPrefab = GetRandomBotPrefab();
            if (selectedBotPrefab == null)
            {
                Debug.LogWarning("[BotManager] 사용 가능한 봇 프리팹이 없어 스킵");
                continue;
            }

            // 봇 인스턴스 생성
            NetworkObject botNetobj = NetworkManager.Singleton.SpawnManager.InstantiateAndSpawn(
                selectedBotPrefab,
                position: transform.position,
                rotation: transform.rotation
            );

            // 봇 이름 설정
            PlayerCanvasManager nameSync = botNetobj.GetComponent<PlayerCanvasManager>();
            if (nameSync != null)
            {
                // 고유한 봇 이름 생성
                string botName = NetworkBotIdentity.GenerateBotName();

                // 모든 클라이언트에 이름 동기화
                nameSync.SetPlayerName(botName);
            }
            else
            {
                Debug.LogWarning("[BotManager] PlayerNameSync 컴포넌트가 없음");
            }

            // 입력 비활성화
            BotController botController = botNetobj.GetComponent<BotController>();
            botController.SetInputEnabled(false);

            // 게임 스폰 포인트로 텔레포트
            TeleportBotToGamePosition(botNetobj.gameObject, spawnPoints[i]);

            // 활성화된 봇 리스트에 추가
            activeBots.Add(botNetobj.gameObject);

            // 봇 스폰 후 2프레임 대기 (스폰+텔레포트 동기화 시간)
            yield return null;
            yield return null;
        }
    }

    // 랜덤한 봇 프리팹 선택
    private NetworkObject GetRandomBotPrefab()
    {
        // null이 아닌 프리팹만 필터링
        List<NetworkObject> validPrefabs = new List<NetworkObject>();
        foreach (var prefab in botPrefabs)
        {
            if (prefab != null)
            {
                validPrefabs.Add(prefab);
            }
        }

        if (validPrefabs.Count == 0)
        {
            return null;
        }

        // 랜덤 선택
        return validPrefabs[Random.Range(0, validPrefabs.Count)];
    }

    // 봇을 게임 스폰 포인트로 텔레포트
    private void TeleportBotToGamePosition(GameObject bot, Transform spawnPoint)
    {
        // 90도 회전
        Vector3 targetPosition = spawnPoint.position;
        Quaternion targetRotation = spawnPoint.rotation * Quaternion.Euler(0, 90, 0);

        // NavMeshAgent 비활성화 후 이동 (NavMesh 문제 방지)
        UnityEngine.AI.NavMeshAgent navAgent = bot.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (navAgent != null)
        {
            navAgent.enabled = false;
        }

        // BotController의 DoRespawn으로 텔레포트
        BotController botController = bot.GetComponent<BotController>();
        if (botController != null)
        {
            botController.DoRespawnTeleport(targetPosition, targetRotation);
        }

        // 새로운 NavMesh에서 NavMeshAgent 재활성화
        if (navAgent != null)
        {
            navAgent.enabled = true;
            navAgent.Warp(targetPosition); // NavAgent 위치 이동
        }
    }

    // 생성된 모든 봇의 입력을 활성화
    public void EnableAllBots()
    {
        // 서버에서만 실행
        if (!IsServer) return;

        // 활성화된 모든 봇을 순회하면서 입력 활성화
        foreach (var bot in activeBots)
        {
            if (bot == null) continue;

            // BotController가 있으면 봇임
            BotController botController = bot.GetComponent<BotController>();
            if (botController != null)
            {
                // 봇의 입력 활성화
                botController.SetInputEnabled(true);
            }
        }

        Debug.Log($"[BotManager] {activeBots.Count}개의 봇 입력 활성화");
    }

    // 생성된 모든 봇 정리
    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            // 활성화된 봇 정리 (디버그 봇 포함)
            foreach (var bot in activeBots)
            {
                if (bot != null)
                {
                    NetworkObject netObj = bot.GetComponent<NetworkObject>();
                    if (netObj != null && netObj.IsSpawned)
                    {
                        netObj.Despawn();
                    }
                }
            }

            activeBots.Clear();
        }
    }
}