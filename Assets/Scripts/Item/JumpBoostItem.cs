using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 점프력 증가 아이템
/// 잡는 순간 일정 시간 동안 점프 높이가 높아집니다.
///
/// 사용법:
/// 1. 프리팹에 이 스크립트 추가
/// 2. Inspector에서 triggerType = UseOnGrab 설정 (필수!)
/// 3. buffDuration: 지속 시간 (기본 5초)
/// 4. buffValue: 점프력 배율 (기본 1.5 = 150% 점프력)
///
/// 예시:
/// - buffValue = 1.5 → jumpForce 3 → 4.5로 증가 (더 높이 점프)
/// - buffValue = 2.0 → jumpForce 3 → 6으로 증가 (2배 높이)
/// </summary>
public class JumpBoostItem : BuffItem
{
    /// <summary>
    /// 플레이어에게 점프력 버프를 적용합니다
    /// </summary>
    /// <param name="player">버프를 받을 플레이어</param>
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

        Debug.Log($"[JumpBoostItem] {player.gameObject.name}에게 점프 버프 적용! 배율: {buffValue}, 지속시간: {buffDuration}초");
    }

    [ClientRpc]
    protected override void SpawnBuffEffectClientRpc(Vector3 position)
    {
        base.SpawnBuffEffectClientRpc(position);

        // TODO: 점프 버프 전용 파티클/사운드 추가
        // 예: 초록색 위쪽 화살표 이펙트, "쾅" 사운드
    }
}
