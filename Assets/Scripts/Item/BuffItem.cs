using Unity.Netcode;
using UnityEngine;

// 공통 버프 + 이펙트 처리
public abstract class BuffItem : InteractiveItem
{
    [Header("Buff Settings")]
    [SerializeField] protected float buffDuration = 5f; // 버프 지속 시간
    [SerializeField] protected float buffValue = 1.5f;  // 버프 값

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

        // 플레이어의 버프 픽업 이펙트 재생
        Holder.PlayBuffPickupEffect();

        base.ActivateItem();
    }

    // 버프를 받을 플레이어
    protected abstract void ApplyBuffToPlayer(PlayerController player);
}