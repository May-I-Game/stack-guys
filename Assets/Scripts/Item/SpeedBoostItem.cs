using Unity.Netcode;
using UnityEngine;

// 플레이어에게 속도 버프(지속 시간, 속도 배율) 적용
public class SpeedBoostItem : BuffItem
{
    protected override void ApplyBuffToPlayer(PlayerController player)
    {
        // PlayerBuffManager 확인
        PlayerBuffManager buffManager = player.GetComponent<PlayerBuffManager>();
        if (buffManager == null)
        {
            return;
        }

        BuffData buffData = new BuffData()
        {
            type = BuffType.Speed,
            value = buffValue,              // Inspector에서 설정된 속도 배율
            duration = buffDuration         // Inspector에서 설정한 지속시간
        };

        // 버프 적용 (IBuffable 인터페이스 사용)
        buffManager.ApplyBuff(buffData);
    }

    [ClientRpc]
    protected override void SpawnBuffEffectClientRpc(Vector3 position)
    {
        base.SpawnBuffEffectClientRpc(position);

        // 속도 버프 이펙트 효과
    }
}
