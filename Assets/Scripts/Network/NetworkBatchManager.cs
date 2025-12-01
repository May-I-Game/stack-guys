using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public struct PlayerSnapshot : INetworkSerializeByMemcpy
{
    public ushort NetworkObjectId; // // 어느 오브젝트인가? (2 Byte)
    public short X, Y, Z;         // 위치 (6 Byte)
    public ushort YRotation;       // Y회전 (2 Byte) 

    // 100f : 1cm 단위 정밀도 (-327.68 ~ +327.67m)
    // 50f  : 2cm 단위 정밀도 (-655.36 ~ +655.34m)
    // 20f  : 5cm 단위 정밀도 (-1638.4 ~ +1638.35m)
    private const float COMPRESS_RATIO = 50f;

    // 생성자로 데이터를 넣을 때 자동 압축
    public PlayerSnapshot(ulong netId, Vector3 pos, float rotY)
    {
        NetworkObjectId = (ushort)netId; // 주의: ID 65535 넘으면 오버플로우

        X = (short)Mathf.RoundToInt(pos.x * COMPRESS_RATIO);
        Y = (short)Mathf.RoundToInt(pos.y * COMPRESS_RATIO);
        Z = (short)Mathf.RoundToInt(pos.z * COMPRESS_RATIO);

        float normalizedRot = Mathf.Repeat(rotY, 360f);
        YRotation = (ushort)(normalizedRot / 360f * 65535f); // 0.005도 오차
    }

    // 데이터를 꺼낼 때
    public void GetState(out ulong netId, out Vector3 pos, out float rotY)
    {
        netId = (ulong)NetworkObjectId;

        pos = new Vector3(
            X / COMPRESS_RATIO,
            Y / COMPRESS_RATIO,
            Z / COMPRESS_RATIO
        );

        rotY = (float)YRotation / 65535f * 360f;
    }
}

public struct ObjectSnapshot : INetworkSerializeByMemcpy
{
    public ushort NetworkObjectId; // // 어느 오브젝트인가? (2 Byte)
    public short X, Y, Z;         // 위치 (6 Byte)
    public ushort YRotation;       // Y회전 (2 Byte) 

    // 100f : 1cm 단위 정밀도 (-327.68 ~ +327.67m)
    // 50f  : 2cm 단위 정밀도 (-655.36 ~ +655.34m)
    // 20f  : 5cm 단위 정밀도 (-1638.4 ~ +1638.35m)
    private const float COMPRESS_RATIO = 50f;

    // 생성자로 데이터를 넣을 때 자동 압축
    public ObjectSnapshot(ulong netId, Vector3 pos, float rotY)
    {
        NetworkObjectId = (ushort)netId; // 주의: ID 65535 넘으면 오버플로우

        X = (short)Mathf.RoundToInt(pos.x * COMPRESS_RATIO);
        Y = (short)Mathf.RoundToInt(pos.y * COMPRESS_RATIO);
        Z = (short)Mathf.RoundToInt(pos.z * COMPRESS_RATIO);

        float normalizedRot = Mathf.Repeat(rotY, 360f);
        YRotation = (ushort)(normalizedRot / 360f * 65535f); // 0.005도 오차
    }

    // 데이터를 꺼낼 때
    public void GetState(out ulong netId, out Vector3 pos, out float rotY)
    {
        netId = (ulong)NetworkObjectId;

        pos = new Vector3(
            X / COMPRESS_RATIO,
            Y / COMPRESS_RATIO,
            Z / COMPRESS_RATIO
        );

        rotY = (float)YRotation / 65535f * 360f;
    }
}

public class NetworkBatchManager : NetworkBehaviour
{
    [SerializeField]
    private float syncDistance = 30f;
    private float _sqrSyncDistance;

    [SerializeField]
    private float posDeltaThreshold = 0.02f; // 2cm
    [SerializeField]
    private float rotDeltaThreshold = 1f; // 1도

    // 빠른 검색을 위해 캐싱해둠
    private Dictionary<ulong, PlayerController> _spawnedPlayers = new Dictionary<ulong, PlayerController>();
    private Dictionary<ulong, IBatchSyncObject> _spawnedObjects = new Dictionary<ulong, IBatchSyncObject>();
    // 재사용할 리스트 (GC 방지) - 미리 넉넉하게 할당
    private List<PlayerSnapshot> _playerSnapshotBuffer = new List<PlayerSnapshot>(150);
    private List<ObjectSnapshot> _objectSnapshotBuffer = new List<ObjectSnapshot>(1000);
    // 이번 틱에서 더티한 동기화 대상
    private HashSet<ulong> _dirtyPlayers = new HashSet<ulong>();
    private HashSet<ulong> _dirtyObjects = new HashSet<ulong>();

    private const int MAX_SNAPSHOTS_PER_PACKET = 100;

    // 로그 집계용
    private float _logTimer = 0f;
    private long _totalSnapshotsSentInSecond = 0;

    public static NetworkBatchManager Instance;

    private void Awake()
    {
        Instance = this;
        _sqrSyncDistance = syncDistance * syncDistance;
    }

    private void Update()
    {
        // 서버가 아니거나, 연결된 클라가 없으면 중지
        if (!IsServer || NetworkManager.Singleton.ConnectedClientsIds.Count == 0) return;

        _logTimer += Time.deltaTime;

        // 1초마다
        if (_logTimer >= 1.0f)
        {
            // 합산된 로그 출력 (개수 * 10바이트 * 8비트) / 1000 = 총 Kbps
            float totalKbps = (_totalSnapshotsSentInSecond * 10 * 8) / 1000f;

            Debug.Log(
                $"[BatchNetworkManager] 총 전송 스냅샷: {_totalSnapshotsSentInSecond}개, " +
                $"현재 서버 아웃바운드: {totalKbps:F1}Kbps"
            );

            _logTimer -= 1.0f;
            _totalSnapshotsSentInSecond = 0;
        }
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.NetworkTickSystem.Tick += OnNetworkTick;
        }

        if (IsClient)
        {
            NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("BatchMovePlayer", ReceivePlayerBatchUpdate);
            NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("BatchMoveObject", ReceiveObjectBatchUpdate);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.NetworkTickSystem.Tick -= OnNetworkTick;
        }

        if (IsClient)
        {
            NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler("BatchMovePlayer");
            NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler("BatchMoveObject");
        }
    }

    public void RegisterPlayer(ulong netId, PlayerController player)
    {
        if (!_spawnedPlayers.ContainsKey(netId)) _spawnedPlayers.Add(netId, player);
    }

    public void UnregisterPlayer(ulong netId)
    {
        if (_spawnedPlayers.ContainsKey(netId)) _spawnedPlayers.Remove(netId);
    }

    public void RegisterObject(ulong netId, IBatchSyncObject obj)
    {
        if (!_spawnedObjects.ContainsKey(netId)) _spawnedObjects.Add(netId, obj);
    }

    public void UnregisterObject(ulong netId)
    {
        if (_spawnedObjects.ContainsKey(netId)) _spawnedObjects.Remove(netId);
    }

    // ================= Server Side =================
    // 서버가 매 틱마다 모든 플레이어 위치를 한번에 전송
    private void OnNetworkTick()
    {
        // 리스너 없으면 보낼 필요 없음
        if (NetworkManager.Singleton.ConnectedClientsIds.Count == 0) return;

        UpdateDirtyPlayers();
        UpdateDirtyObjects();

        SendBatchUpdate();
    }

    private void UpdateDirtyPlayers()
    {
        _dirtyPlayers.Clear();

        foreach (var kvp in _spawnedPlayers)
        {
            ulong netId = kvp.Key;
            PlayerController player = kvp.Value;

            if (IsDirtyPlayer(netId, player))
            {
                _dirtyPlayers.Add(netId);
            }
        }
    }

    private void UpdateDirtyObjects()
    {
        _dirtyObjects.Clear();

        foreach (var kvp in _spawnedObjects)
        {
            ulong netId = kvp.Key;
            IBatchSyncObject obj = kvp.Value;

            if (IsDirtyObject(netId, obj))
            {
                _dirtyObjects.Add(netId);
            }
        }
    }

    private void SendBatchUpdate()
    {
        // 클라이언트별로 개별 전송 (각자 시야에 보이는 것만)
        foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            NetworkObject observer = NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject;
            if (observer == null) continue;

            SendPlayerBatch(clientId, observer.transform.position);
            SendObjectBatch(clientId, observer.transform.position);
        }
    }

    private void SendPlayerBatch(ulong clientId, Vector3 observerPos)
    {
        // 스냅샷 버퍼 초기화
        _playerSnapshotBuffer.Clear();
        foreach (var netId in _dirtyPlayers)
        {
            if (!_spawnedPlayers.TryGetValue(netId, out PlayerController other)) continue;

            if (!GameManager.Instance.IsEnded)
            {
                // 관심영역(AOI) 체크 (거리 기반)
                float sqrDistance = (observerPos - other.transform.position).sqrMagnitude;
                if (sqrDistance > _sqrSyncDistance) continue;
            }

            // other을 스냅샷에 추가해서 동기화
            _playerSnapshotBuffer.Add(new PlayerSnapshot(
                netId,
                other.transform.position,
                other.transform.rotation.eulerAngles.y
            ));

            // 버퍼가 꽉 차면 즉시 전송하고 비움 (MTU 안전장치)
            if (_playerSnapshotBuffer.Count >= MAX_SNAPSHOTS_PER_PACKET)
            {
                SendPlayerSnapshots(clientId, _playerSnapshotBuffer);
                _playerSnapshotBuffer.Clear();
            }
        }

        if (_playerSnapshotBuffer.Count == 0) return;

        // 남은 스냅샷 전송
        SendPlayerSnapshots(clientId, _playerSnapshotBuffer);
    }

    private void SendObjectBatch(ulong clientId, Vector3 observerPos)
    {
        // 스냅샷 버퍼 초기화
        _objectSnapshotBuffer.Clear();
        foreach (var netId in _dirtyObjects)
        {
            if (!_spawnedObjects.TryGetValue(netId, out IBatchSyncObject other)) continue;

            // 관심영역(AOI) 체크 (거리 기반)
            float sqrDistance = (observerPos - other.Transform.position).sqrMagnitude;
            if (sqrDistance > _sqrSyncDistance) continue;

            // other을 스냅샷에 추가해서 동기화
            _objectSnapshotBuffer.Add(new ObjectSnapshot(
                netId,
                other.Transform.position,
                other.Transform.rotation.eulerAngles.y
            ));

            // 버퍼가 꽉 차면 즉시 전송하고 비움 (MTU 안전장치)
            if (_objectSnapshotBuffer.Count >= MAX_SNAPSHOTS_PER_PACKET)
            {
                SendObjectSnapshots(clientId, _objectSnapshotBuffer);
                _objectSnapshotBuffer.Clear();
            }
        }

        if (_objectSnapshotBuffer.Count == 0) return;

        // 남은 스냅샷 전송
        SendObjectSnapshots(clientId, _objectSnapshotBuffer);
    }

    private void SendPlayerSnapshots(ulong clientId, List<PlayerSnapshot> snapshots)
    {
        using var writer = new FastBufferWriter(snapshots.Count * 10 + 64, Allocator.Temp);
        writer.WriteValueSafe(snapshots.ToArray());

        NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(
            "BatchMovePlayer",
            clientId, // 특정 클라이언트에게만 전송
            writer,
            NetworkDelivery.UnreliableSequenced
        );

        _totalSnapshotsSentInSecond += snapshots.Count;
    }

    private void SendObjectSnapshots(ulong clientId, List<ObjectSnapshot> snapshots)
    {
        using var writer = new FastBufferWriter(snapshots.Count * 10 + 64, Allocator.Temp);
        writer.WriteValueSafe(snapshots.ToArray());

        NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(
            "BatchMoveObject",
            clientId, // 특정 클라이언트에게만 전송
            writer,
            NetworkDelivery.UnreliableSequenced
        );

        _totalSnapshotsSentInSecond += snapshots.Count;
    }

    private bool IsDirtyPlayer(ulong netId, PlayerController player)
    {
        Vector3 currentPos = player.transform.position;
        float currentRotY = player.transform.rotation.eulerAngles.y;

        // 마지막 동기화 상태 가져오기
        if (!player.GetLastSyncedState(out Vector3 lastPos, out float lastRotY))
        {
            // 초기화되지 않았으면 true (첫 전송)
            player.SetLastSyncedState(currentPos, currentRotY);
            return true;
        }

        // 위치 변화량 계산
        float posDelta = Vector3.Distance(currentPos, lastPos);
        // 회전 변화량 계산
        float rotDelta = Mathf.Abs(Mathf.DeltaAngle(lastRotY, currentRotY));

        // 임계값을 넘으면 더티 (변화 있음)
        bool isDirty = posDelta >= posDeltaThreshold || rotDelta >= rotDeltaThreshold;

        if (isDirty)
        {
            // 동기화된 상태 업데이트
            player.SetLastSyncedState(currentPos, currentRotY);
        }

        return isDirty;
    }

    private bool IsDirtyObject(ulong netId, IBatchSyncObject obj)
    {
        Vector3 currentPos = obj.Transform.position;
        float currentRotY = obj.Transform.rotation.eulerAngles.y;

        // 마지막 동기화 상태 가져오기
        if (!obj.GetLastSyncedState(out Vector3 lastPos, out float lastRotY))
        {
            // 초기화되지 않았으면 true (첫 전송)
            obj.SetLastSyncedState(currentPos, currentRotY);
            return true;
        }

        // 위치 변화량 계산
        float posDelta = Vector3.Distance(currentPos, lastPos);
        // 회전 변화량 계산
        float rotDelta = Mathf.Abs(Mathf.DeltaAngle(lastRotY, currentRotY));

        // 임계값을 넘으면 더티 (변화 있음)
        bool isDirty = posDelta >= posDeltaThreshold || rotDelta >= rotDeltaThreshold;

        if (isDirty)
        {
            // 동기화된 상태 업데이트
            obj.SetLastSyncedState(currentPos, currentRotY);
        }

        return isDirty;
    }

    // ================= Client Side =================
    private void ReceivePlayerBatchUpdate(ulong senderId, FastBufferReader reader)
    {
        // 배열 전체를 한 번에 읽음
        reader.ReadValueSafe(out PlayerSnapshot[] snapshots);

        // 각 플레이어에게 데이터 뿌려주기
        foreach (var snap in snapshots)
        {
            snap.GetState(out ulong netId, out Vector3 pos, out float rotY);

            if (_spawnedPlayers.TryGetValue(netId, out var player))
            {
                // 바로 이동시키지 말고, 목표지점만 설정해줌 (보간은 플레이어가 알아서)
                player.UpdateTargetState(pos, rotY);
            }
        }
    }

    private void ReceiveObjectBatchUpdate(ulong senderId, FastBufferReader reader)
    {
        // 배열 전체를 한 번에 읽음
        reader.ReadValueSafe(out ObjectSnapshot[] snapshots);

        // 각 오브젝트에게 데이터 뿌려주기
        foreach (var snap in snapshots)
        {
            snap.GetState(out ulong netId, out Vector3 pos, out float rotY);

            if (_spawnedObjects.TryGetValue(netId, out var obj))
            {
                // 바로 이동시키지 말고, 목표지점만 설정해줌 (보간은 오브젝트가 알아서)
                obj.UpdateTargetState(pos, rotY);
            }
        }
    }
}