using Unity.Netcode;
using UnityEngine;

public abstract class BuffItem : InteractiveItem
{
    [Header("Buff Settings")]
    [SerializeField] protected float buffDuration = 5f; // 버프 지속 시간
    [SerializeField] protected float buffValue = 1.5f;  // 버프 값

    public GameObject buffEffectPrefab; // 파티클 프리팹

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
            // 그래도 아이템은 Despawn
            base.ActivateItem();
            return;
        }

        // 자식 클래스에서 구현한 버프 적용
        ApplyBuffToPlayer(Holder);

        // 버프 적용 이펙트 재생 (모든 클라이언트)
        SpawnBuffEffectClientRpc(transform.position);

        base.ActivateItem();
    }

    // 버프를 받을 플레이어
    protected abstract void ApplyBuffToPlayer(PlayerController player);

    // 버프 적용 시각 효과, 오버라이드하여 아이템별 고유 이펙트 가능
    [ClientRpc]
    protected virtual void SpawnBuffEffectClientRpc(Vector3 position)
    {
        // 파티클 효과
        if (buffEffectPrefab != null)
        {
            Instantiate(buffEffectPrefab, position, Quaternion.identity);
        }
    }
}