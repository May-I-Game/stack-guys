using UnityEngine;

// 일정 시간 동안 점프 높이
public class JumpBoostItem : BuffItem
{
    protected override void ApplyBuffToPlayer(PlayerController player)
    {
        // PlayerBuffManager가 있는지 확인
        PlayerBuffManager buffManager = player.GetComponent<PlayerBuffManager>();
        if (buffManager == null)
        {
            Debug.LogError($"[JumpBoostItem] PlayerBuffManager를 찾을 수 없습니다: {player.gameObject.name}");
            return;
        }

        // 버프 데이터 생성
        BuffData buffData = new BuffData
        {
            type = BuffType.Jump,
            value = buffValue,      // Inspector에서 설정한 배율
            duration = buffDuration // Inspector에서 설정한 지속시간
        };

        // 버프 적용 (IBuffable 인터페이스 사용)
        buffManager.ApplyBuff(buffData);
    }
}