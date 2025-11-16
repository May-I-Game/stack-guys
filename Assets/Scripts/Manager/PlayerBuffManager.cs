using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;
using Microsoft.IdentityModel.Tokens;

// 버프 관리 전담 매니저
public class PlayerBuffManager : NetworkBehaviour, IBuffable
{
    //////////////////////////////////////////////////////////////////////
    // 네트워크 동기화 변수
    //////////////////////////////////////////////////////////////////////

    // 속도 배율
    private NetworkVariable<float> netSpeedMultiplier = new NetworkVariable<float>(1f);

    // 점프력 배율
    private NetworkVariable<float> netJumpMultiplier = new NetworkVariable<float>(1f);

    // 무적 상태 여부
    private NetworkVariable<bool> netIsInvincible = new NetworkVariable<bool>(false);

    //////////////////////////////////////////////////////////////////////
    // 서버 전용 변수
    //////////////////////////////////////////////////////////////////////

    // 현재 활성화된 버프들
    private Dictionary<BuffType, BuffData> activeBuffs = new Dictionary<BuffType, BuffData>();

    // 버프 만료 타이머 코루틴들
    private Dictionary<BuffType, Coroutine> buffTimers = new Dictionary<BuffType, Coroutine>();

    //////////////////////////////////////////////////////////////////////
    // Public 프로퍼티 (외부 접근용, PlayerController에서 참조)
    //////////////////////////////////////////////////////////////////////

    // 현재 속도 배율
    public float SpeedMultiplier => netSpeedMultiplier.Value;

    // 현재 점프력 배율 
    public float JumpMultiplier => netJumpMultiplier.Value;

    // 현재 무적 상태 여부
    public bool IsInvincible => netIsInvincible.Value;

    //////////////////////////////////////////////////////////////////////
    // IBuffable 인터페이스 구현
    //////////////////////////////////////////////////////////////////////

    // 버프 적용
    public void ApplyBuff(BuffData data)
    {
        // 서버에서만 버프 적용
        if (!IsServer)
        {
            return;
        }

        // 같은 타입 버프 있으면 갱신
        if (activeBuffs.ContainsKey(data.type))
        {
            RemoveBuff(data.type);
        }

        // 버프 데이터 저장
        activeBuffs[data.type] = data;

        // 버프 효과 적용
        ApplyBuffEffect(data);

        // 만료 타이머 시작
        Coroutine timer = StartCoroutine(BuffTimer(data));
        buffTimers[data.type] = timer;
    }

    // 버프 제거
    public void RemoveBuff(BuffType type)
    {
        // 서버에서만 버프 제거
        if (!IsServer)
        {
            return;
        }

        // 버프가 없으면 무시
        if (!activeBuffs.ContainsKey(type))
        {
            return;
        }

        // 버프 효과 제거
        BuffData data = activeBuffs[type];
        RemoveBuffEffect(data);

        // 타이머 중지
        if (buffTimers.ContainsKey(type))
        {
            if (buffTimers[type] != null)
            {
                StopCoroutine(buffTimers[type]);
            }

            buffTimers.Remove(type);
        }
    }

    // 특정 버프 활성화 확인
    public bool HasBuff(BuffType type)
    {
        return activeBuffs.ContainsKey(type);
    }

    //////////////////////////////////////////////////////////////////////
    // 버프 효과 적용/제거
    //////////////////////////////////////////////////////////////////////

    // 버프 효과 실제로 적용
    private void ApplyBuffEffect(BuffData data)
    {
        switch (data.type)
        {
            // 속도 배율 설정
            case BuffType.Speed:
                netSpeedMultiplier.Value = data.value;
                break;

            // 점프력 배율 설정
            case BuffType.Jump:
                netJumpMultiplier.Value = data.value;
                break;

            // 무적 상태 해제
            case BuffType.Invincibility:
                netIsInvincible.Value = true;
                break;
        }
    }

    // 버프 효과 제거한 뒤 원래 상태로 복구
    private void RemoveBuffEffect(BuffData data)
    {
        switch (data.type)
        {
            // 속도 복구
            case BuffType.Speed:
                netSpeedMultiplier.Value = 1f;
                break;

            // 점프력 복구
            case BuffType.Jump:
                netJumpMultiplier.Value = 1f;
                break;

            // 무적 상태 해제
            case BuffType.Invincibility:
                netIsInvincible.Value = false;
                break;
        }
    }

    private IEnumerator BuffTimer(BuffData data)
    {
        // duration 초 대기
        yield return new WaitForSeconds(data.duration);

        // 시간 경과 후 버프 제거
        RemoveBuff(data.type);
    }

    // 네트워크 해제 시 모든 타이머 중지
    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        // 네트워크 해제 시 모든 타이머 중지
        if (IsServer)
        {
            foreach (var timer in buffTimers.Values)
            {
                if (timer != null)
                {
                    StopCoroutine(timer);
                }
            }

            buffTimers.Clear();
            activeBuffs.Clear();
        }
    }

    //////////////////////////////////////////////////////////////////////
    // 디버그
    //////////////////////////////////////////////////////////////////////

    // Inspector에서 현재 버프 상태 확인용
    [Header("Buff Info")]
    [SerializeField] private float debugSpeedMultiplier = 1f;
    [SerializeField] private float debugJumpMultiplier = 1f;
    [SerializeField] private bool debugIsInvincible = false;

    private void Update()
    {
        // Inspector에 현재 상태 표시 (디버깅용)
        debugSpeedMultiplier = SpeedMultiplier;
        debugJumpMultiplier = JumpMultiplier;
        debugIsInvincible = IsInvincible;
    }
}
