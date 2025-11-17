using Unity.Netcode;
using UnityEngine;

// 공통 버프 + 이펙트 처리
public abstract class BuffItem : InteractiveItem
{
    [Header("Buff Settings")]
    [SerializeField] protected float buffDuration = 5f; // 버프 지속 시간
    [SerializeField] protected float buffValue = 1.5f;  // 버프 값

    [Header("VFX Settings")]
    [SerializeField] private GameObject buffLoopEffectPrefab;   // 파티클 프리팹
    [SerializeField] private GameObject pickupEffectPrefab;     // 아이템 먹는 순간 이펙트

    // 지속 이펙트 플레이어 기준 로컬 위치 (머리 위 등)
    [SerializeField] private Vector3 loopEffectOffset = new Vector3(0f, 1.0f, 0f);


    // 아이템이 활성화될 때 호출 - UseOnGrab
    protected override void ActivateItem()
    {
        // 서버에서만 실행
        if (!IsServer)
        {
            return;
        }

        // 잡은 플레이어가 없으면 오류
        if (Holder == null)
        {
            Debug.LogWarning("[BuffItem] Holder가 null입니다!");
            // 그래도 아이템은 Despawn
            base.ActivateItem();
            return;
        }

        //Debug.Log($"[BuffItem] {Holder.gameObject.name}에게 버프 적용 시작");

        // 자식 클래스에서 구현한 버프 적용 (서버에서는 Holder 적용 가능)
        ApplyBuffToPlayer(Holder);

        // 아이템 먹는 순간 이펙트
        PickupEffectClientRpc(Holder.transform.position);

        BuffLoopEffectClientRpc(Holder.NetworkObjectId, loopEffectOffset, buffDuration);

        // 버프 지속 동안 플레이어 이펙트
        NetworkObject playerNetObj = Holder.GetComponent<NetworkObject>();
        if (playerNetObj != null)
        {
            BuffLoopEffectClientRpc(
                playerNetObj.NetworkObjectId,
                loopEffectOffset,
                buffDuration
                );
        }

        base.ActivateItem();
    }

    // 버프를 받을 플레이어
    protected abstract void ApplyBuffToPlayer(PlayerController player);

    // 아이템 먹는 순간 이펙트, 오버라이드하여 아이템별 고유 이펙트 가능
    [ClientRpc]
    protected virtual void PickupEffectClientRpc(Vector3 position)
    {
        // 프리펩 체크
        if (pickupEffectPrefab == null)
        {
            return;
        }

        Instantiate(pickupEffectPrefab, position, Quaternion.identity);
    }

    // 버프 적용 이펙트 효과
    [ClientRpc]
    protected virtual void BuffLoopEffectClientRpc(ulong playerNetworkId, Vector3 localOffset, float duration)
    {
        // 프리펩 체크
        if (buffLoopEffectPrefab == null)
        {
            return;
        }

        // 네트워크로 해당 플레이어 찾기
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(
                playerNetworkId, out NetworkObject playerNetObj))
        {
            Debug.LogWarning($"[BuffItem] StartBuffLoopEffectClientRpc: 플레이어 찾기 실패 (id={playerNetworkId})");
            return;
        }

        Transform playerTransform = playerNetObj.transform;

        // 이펙트를 플레이어의 자식으로 생성
        GameObject fx = Object.Instantiate(buffLoopEffectPrefab, playerTransform);
        fx.transform.position = localOffset;

        Destroy(fx, duration + 0.1f);
    }
}