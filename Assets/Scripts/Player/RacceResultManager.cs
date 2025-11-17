using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using System.Linq;

public class RaceResultManager : NetworkBehaviour
{
    // === 1. 도착 정보 저장용 구조체 ===
    public struct FinishInfo : INetworkSerializable
    {
        public ulong clientId;
        public float finishTime;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref clientId);
            serializer.SerializeValue(ref finishTime);
        }
    }

    // 도착 완료 플레이어 리스트 (서버만 관리)
    private readonly List<FinishInfo> finishedPlayers = new List<FinishInfo>();

    // 시상대에 올릴 최종 3인의 ID를 저장할 NetworkList
    public NetworkList<ulong> PodiumClientIds { get; private set; }

    // 게임이 끝났는지 여부 (GameManager의 IsGame을 참조해야 하지만, 단순화를 위해 여기에 플래그를 둠)
    private NetworkVariable<bool> isGameActive = new NetworkVariable<bool>(true);

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            PodiumClientIds = new NetworkList<ulong>();
            // 게임 시작 시 초기 상태 설정
            isGameActive.Value = true;
        }
        base.OnNetworkSpawn();
    }

    // === 2. 골 플래그에서 호출될 함수 (서버에서만 실행) ===
    public void RegisterPlayerFinish(ulong clientId, float finishTime)
    {
        // ⚠️ 서버 권한 체크 (안전 장치)
        if (!IsServer) return;

        // ⚠️ 게임이 이미 종료되었으면 등록하지 않음
        if (!isGameActive.Value) return;

        // 중복 체크: 이미 도착한 플레이어는 다시 등록하지 않습니다.
        if (finishedPlayers.Any(info => info.clientId == clientId))
        {
            Debug.LogWarning($"[RaceResultManager] 플레이어 {clientId}는 이미 도착했습니다. 중복 등록 차단.");
            return;
        }

        // 도착 정보 저장
        FinishInfo newInfo = new FinishInfo { clientId = clientId, finishTime = finishTime };
        finishedPlayers.Add(newInfo);

        Debug.Log($"[RaceResultManager] 플레이어 {clientId} 도착 ({finishedPlayers.Count}등).");

        // 3등까지 확정되었는지 확인
        int requiredFinishers = 3;
        if (finishedPlayers.Count >= requiredFinishers)
        {
            EndGameAndSetupPodium();
        }
    }

    // === 3. 게임 종료 및 시상대 설정 (서버에서만 실행) ===
    private void EndGameAndSetupPodium()
    {
        if (!IsServer || !isGameActive.Value) return;

        isGameActive.Value = false; // 게임 종료 플래그 설정

        // 1. 도착 시간 기준으로 정렬
        var sortedWinners = finishedPlayers.OrderBy(info => info.finishTime).ToList();

        // 2. 상위 3명 클라이언트 ID를 NetworkList에 저장
        PodiumClientIds.Clear();
        for (int i = 0; i < Mathf.Min(3, sortedWinners.Count); i++)
        {
            PodiumClientIds.Add(sortedWinners[i].clientId);
            Debug.Log($"순위 {i + 1}등: Client ID {sortedWinners[i].clientId}");
        }

        // 3. 모든 클라이언트에게 시네마틱 시작 명령 전송
        StartCinematicClientRpc();
    }

    // === 4. 모든 클라이언트에게 시네마틱 시작 명령 ===
    [ClientRpc]
    private void StartCinematicClientRpc()
    {
        Debug.Log("[Client] 서버로부터 시네마틱 시작 명령 수신. 시상대 설정 시작.");

        // 시상대 배치 및 시네마틱 카메라 활성화 로직 호출
        SetupPodiumOnClient(PodiumClientIds);

        // 예: CinematicManager.Instance.StartCinematic();
    }

    // === 5. 시상대 배치 로직 (클라이언트에서 실행) ===
    private void SetupPodiumOnClient(NetworkList<ulong> winners)
    {
        // 이 로직에서 winners[0] (1등), winners[1] (2등), winners[2] (3등) ID를 사용하여
        // 해당 플레이어의 GameObject를 찾아 시상대 위치로 이동시키는 코드를 작성하세요.
        // 이 부분은 기존 GameManager에 있던 시네마틱 로직을 가져와야 합니다.

        if (winners.Count > 0)
        {
            Debug.Log($"클라이언트: 1등 ID {winners[0]}를 시상대 1위 자리로 배치.");
            // 예시: FindPlayerObject(winners[0]).transform.position = podiumPosition1;
        }
    }

    // 외부에서 게임 진행 상태를 확인할 수 있도록
    public bool IsGameActive()
    {
        return isGameActive.Value;
    }
}