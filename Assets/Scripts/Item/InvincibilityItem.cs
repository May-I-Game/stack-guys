using Unity.Netcode;
using UnityEngine;

// 아이템을 잡는 순간 일정 시간 동안 다른 플레이어가 잡을 수 없음
public class InvincibilityItem : BuffItem
{
    protected override void ApplyBuffToPlayer(PlayerController player)
    {
        PlayerBuffManager buffManager = player.GetComponent<PlayerBuffManager>();
        if (buffManager == null)
        {
            return;
        }

        // 무적 버프 데이터 생성
        BuffData buffData = new BuffData
        {
            type = BuffType.Invincibility,
            value = 1f,
            duration = buffDuration
        };

        // 버프 적용 (IBuffAble 사용)
        buffManager.ApplyBuff(buffData);
    }

    [ClientRpc]
    protected override void PickupEffectClientRpc(Vector3 position)
    {
        base.PickupEffectClientRpc(position);

        // 무적 이펙트
    }
}
