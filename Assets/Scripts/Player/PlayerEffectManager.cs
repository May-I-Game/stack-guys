using Unity.Netcode;
using UnityEngine;

public class PlayerEffectManager : NetworkBehaviour
{
    [Header("FootStep Audio")]
    [SerializeField] private AudioSource footstepAudioSource; // 발걸음 오디오 소스
    [SerializeField] private AudioClip footstepClip; // 발걸음 사운드 클립
    [SerializeField, Range(0f, 1f)] private float footstepVolume = 0.5f; // 발걸음 볼륨
    [SerializeField] private float footstepInterval = 0.4f; // 발걸음 재생 간격 (초)

    // 발걸음 사운드 타이머
    private float footstepTimer = 0f;

    [Header("Jump Audio")]
    [SerializeField] private AudioSource jumpAudioSource; // 점프 오디오 소스
    [SerializeField] private AudioClip jumpVoiceClip; // 점프 캐릭터 보이스 클립
    [SerializeField] private AudioClip jumpEffectClip; // 점프 효과음 클립
    [SerializeField, Range(0f, 1f)] private float jumpVoiceVolume = 0.7f; // 점프 보이스 볼륨
    [SerializeField, Range(0f, 1f)] private float jumpEffectVolume = 0.5f; // 점프 효과음 볼륨

    [Header("Dive Audio")]
    [SerializeField] private AudioSource diveAudioSource; // 다이브 오디오 소스
    [SerializeField] private AudioClip diveStartClip; // 다이브 시작 효과음
    [SerializeField] private AudioClip diveLandVoiceClip; // 다이브 착지 캐릭터 보이스
    [SerializeField] private AudioClip diveLandImpactClip; // 다이브 착지 바닥 충돌음
    [SerializeField, Range(0f, 1f)] private float diveStartVolume = 0.6f; // 다이브 시작 볼륨
    [SerializeField, Range(0f, 1f)] private float diveLandVoiceVolume = 0.7f; // 다이브 착지 보이스 볼륨
    [SerializeField, Range(0f, 1f)] private float diveLandImpactVolume = 0.8f; // 다이브 착지 충돌음 볼륨

    [Header("Throw Audio")]
    [SerializeField] private AudioSource throwAudioSource; // 던지기 오디오 소스
    [SerializeField] private AudioClip throwClip; // 던지기 효과음
    [SerializeField, Range(0f, 1f)] private float throwVolume = 0.7f; // 던지기 볼륨

    [Header("Buff Audio")]
    [SerializeField] private AudioSource buffAudioSource; // 버프 아이템 오디오 소스
    [SerializeField] private AudioClip buffPickupClip; // 버프 아이템 획득 사운드
    [SerializeField, Range(0f, 1f)] private float buffPickupVolume = 0.7f; // 버프 아이템 획득 볼륨
    [SerializeField] private AudioSource buffLoopAudioSource; // 버프 루프 오디오 소스 (속도/점프)
    [SerializeField] private AudioClip buffLoopClip; // 버프 루프 사운드 (속도/점프 버프용)
    [SerializeField, Range(0f, 1f)] private float buffLoopVolume = 0.5f; // 버프 루프 볼륨
    [SerializeField] private AudioSource invincibleBuffLoopAudioSource; // 무적 버프 루프 오디오 소스
    [SerializeField] private AudioClip invincibleBuffLoopClip; // 무적 버프 루프 사운드
    [SerializeField, Range(0f, 1f)] private float invincibleBuffLoopVolume = 0.5f; // 무적 버프 루프 볼륨

    [Header("Hit Audio")]
    [SerializeField] private AudioSource hitAudioSource; // 피격 오디오 소스
    [SerializeField] private AudioClip hitVoiceClip; // 피격 음성 효과음 (항상 재생)
    [SerializeField] private AudioClip hitImpactClip; // 피격 환경 효과음 (충돌음)
    [SerializeField, Range(0f, 1f)] private float hitVolume = 0.7f; // 피격 볼륨

    [Header("Death Audio")]
    [SerializeField] private AudioSource deathAudioSource; // 죽음 오디오 소스
    [SerializeField] private AudioClip deathVoiceClip; // 죽음 음성 효과음 (항상 재생)
    [SerializeField] private AudioClip deathSpikeClip; // 가시/장애물 죽음 환경음 (Death 태그)
    [SerializeField] private AudioClip deathOceanClip; // 물에 빠진 죽음 환경음 (Ocean 태그)
    [SerializeField, Range(0f, 1f)] private float deathVolume = 0.7f; // 죽음 볼륨

    [Header("Respawn Audio")]
    [SerializeField] private AudioSource respawnAudioSource; // 리스폰 오디오 소스
    [SerializeField] private AudioClip respawnClip; // 리스폰 효과음
    [SerializeField, Range(0f, 1f)] private float respawnVolume = 0.7f; // 리스폰 볼륨

    [Header("Particle Effects")]
    [SerializeField] private ParticleSystem walkParticle; // 걷기 파티클
    [SerializeField] private ParticleSystem jumpParticle; // 점프 파티클
    [SerializeField] private ParticleSystem diveLandParticle; // 다이브 착지 파티클
    [SerializeField] private ParticleSystem respawnParticle; // 리스폰 파티클

    private bool isWalkParticlePlaying = false;

    [Header("Buff Effects")]
    [SerializeField] private ParticleSystem buffPickupEffect;          // 아이템 먹는 순간 번쩍
    [SerializeField] private ParticleSystem jumpBuffLoopEffect;        // 점프 버프 루프
    [SerializeField] private ParticleSystem speedBuffLoopEffect;       // 속도 버프 루프
    [SerializeField] private ParticleSystem invincibleBuffLoopEffect;  // 무적 버프 루프

    private void Start()
    {
        // AudioSource 초기 설정 (Owner만 설정)
        if (IsOwner && footstepAudioSource != null)
        {
            footstepAudioSource.playOnAwake = false;
        }
    }

    [ClientRpc]
    private void SetSpeedBuffEffectClientRpc(bool enabled)
    {
        // 로컬 플레이어가 아니고 이펙트 설정이 꺼져있으면 이펙트 비활성화
        bool shouldShowEffect = IsOwner || PlayerPrefs.GetInt("ShowEffects", 1) == 1;

        ToggleLoopEffect(speedBuffLoopEffect, enabled && shouldShowEffect);
        ToggleLoopSound(buffLoopClip, enabled);
    }

    [ClientRpc]
    private void SetJumpBuffEffectClientRpc(bool enabled)
    {
        // 로컬 플레이어가 아니고 이펙트 설정이 꺼져있으면 이펙트 비활성화
        bool shouldShowEffect = IsOwner || PlayerPrefs.GetInt("ShowEffects", 1) == 1;

        ToggleLoopEffect(jumpBuffLoopEffect, enabled && shouldShowEffect);
        ToggleLoopSound(buffLoopClip, enabled);
    }

    [ClientRpc]
    private void SetInvincibleBuffEffectClientRpc(bool enabled)
    {
        // 로컬 플레이어가 아니고 이펙트 설정이 꺼져있으면 이펙트 비활성화
        bool shouldShowEffect = IsOwner || PlayerPrefs.GetInt("ShowEffects", 1) == 1;

        ToggleLoopEffect(invincibleBuffLoopEffect, enabled && shouldShowEffect);
        ToggleLoopSound(invincibleBuffLoopClip, enabled);
    }

    [ClientRpc]
    private void PlayBuffPickupEffectClientRpc()
    {
        // 로컬 플레이어가 아니고 이펙트 설정이 꺼져있으면 재생하지 않음
        bool showEffects = IsOwner || PlayerPrefs.GetInt("ShowEffects", 1) == 1;

        // 파티클 재생
        if (showEffects && buffPickupEffect != null)
        {
            buffPickupEffect.Play();
        }

        // 사운드 재생
        if (buffAudioSource != null && buffPickupClip != null)
        {
            buffAudioSource.PlayOneShot(buffPickupClip, buffPickupVolume * GetSFXVolume());
        }
    }

    [ClientRpc]
    private void PlayJumpEffectsClientRpc()
    {
        // Particle
        // 로컬 플레이어가 아니고 이펙트 설정이 꺼져있으면 재생하지 않음
        if (!IsOwner && PlayerPrefs.GetInt("ShowEffects", 1) == 0) return;

        if (jumpParticle != null)
        {
            jumpParticle.Play();
        }

        // Sound
        // Owner는 이미 로컬에서 재생했으므로 스킵
        if (IsOwner) return;

        if (jumpAudioSource != null)
        {
            float sfxVol = GetSFXVolume();

            // 캐릭터 보이스 재생
            if (jumpVoiceClip != null)
            {
                jumpAudioSource.PlayOneShot(jumpVoiceClip, jumpVoiceVolume * sfxVol);
            }

            // 효과음 재생
            if (jumpEffectClip != null)
            {
                jumpAudioSource.PlayOneShot(jumpEffectClip, jumpEffectVolume * sfxVol);
            }
        }
    }

    [ClientRpc]
    private void PlayDiveStartSoundClientRpc()
    {
        // Owner는 이미 로컬에서 재생했으므로 스킵
        if (IsOwner) return;

        if (diveAudioSource != null && diveStartClip != null)
        {
            diveAudioSource.PlayOneShot(diveStartClip, diveStartVolume * GetSFXVolume());
        }
    }

    [ClientRpc]
    private void PlayDiveLandEffectsClientRpc()
    {
        // Sound
        if (diveAudioSource != null)
        {
            float sfxVol = GetSFXVolume();

            // 캐릭터 보이스 재생
            if (diveLandVoiceClip != null)
            {
                diveAudioSource.PlayOneShot(diveLandVoiceClip, diveLandVoiceVolume * sfxVol);
            }

            // 바닥 충돌음 재생
            if (diveLandImpactClip != null)
            {
                diveAudioSource.PlayOneShot(diveLandImpactClip, diveLandImpactVolume * sfxVol);
            }
        }

        // Particle
        // 로컬 플레이어가 아니고 이펙트 설정이 꺼져있으면 재생하지 않음
        if (!IsOwner && PlayerPrefs.GetInt("ShowEffects", 1) == 0) return;

        if (diveLandParticle != null)
        {
            diveLandParticle.Play();
        }
    }

    [ClientRpc]
    private void PlayRespawnEffectsClientRpc()
    {
        // Particle
        // 로컬 플레이어가 아니고 이펙트 설정이 꺼져있으면 재생하지 않음
        if (!IsOwner && PlayerPrefs.GetInt("ShowEffects", 1) == 0) return;

        if (respawnParticle != null)
        {
            respawnParticle.Play();
        }

        // Sound
        if (respawnAudioSource != null && respawnClip != null)
        {
            respawnAudioSource.PlayOneShot(respawnClip, respawnVolume * GetSFXVolume());
        }
    }

    [ClientRpc]
    private void PlayDeathSoundClientRpc(bool isOceanDeath)
    {
        if (deathAudioSource != null)
        {
            float sfxVol = GetSFXVolume();

            // 1. 음성 효과음 재생 (항상)
            if (deathVoiceClip != null)
            {
                deathAudioSource.PlayOneShot(deathVoiceClip, deathVolume * sfxVol);
            }

            // 2. 환경 효과음 재생 (태그에 따라)
            AudioClip environmentClip = isOceanDeath ? deathOceanClip : deathSpikeClip;
            if (environmentClip != null)
            {
                deathAudioSource.PlayOneShot(environmentClip, deathVolume * sfxVol);
            }
        }
    }

    [ClientRpc]
    private void PlayHitSoundClientRpc()
    {
        if (hitAudioSource != null)
        {
            float sfxVol = GetSFXVolume();

            // 1. 음성 효과음 재생 (항상)
            if (hitVoiceClip != null)
            {
                hitAudioSource.PlayOneShot(hitVoiceClip, hitVolume * sfxVol);
            }

            // 2. 환경 효과음 재생 (충돌음)
            if (hitImpactClip != null)
            {
                hitAudioSource.PlayOneShot(hitImpactClip, hitVolume * sfxVol);
            }
        }
    }

    [ClientRpc]
    private void PlayThrowSoundClientRpc()
    {
        if (throwAudioSource != null && throwClip != null)
        {
            throwAudioSource.PlayOneShot(throwClip, throwVolume * GetSFXVolume());
        }
    }

    public void PlayWalkParticle()
    {
        // 파티클 제어: 땅에서 걷고 있을 때만 재생
        if (walkParticle != null)
        {
            // 로컬 플레이어가 아니고 이펙트 설정이 꺼져있으면 파티클 재생하지 않음
            bool showEffects = IsOwner || PlayerPrefs.GetInt("ShowEffects", 1) == 1;
            bool shouldPlayParticle = showEffects;

            if (shouldPlayParticle && !isWalkParticlePlaying)
            {
                walkParticle.Clear(); // 기존 파티클 제거
                walkParticle.Play(true); // 재생 (자식 포함)
                isWalkParticlePlaying = true;
            }
            else if (!shouldPlayParticle && isWalkParticlePlaying)
            {
                walkParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                isWalkParticlePlaying = false;
            }
        }
    }

    public void PlayJumpEffects()
    {
        PlayJumpEffectsClientRpc();
    }

    // SFX 볼륨 적용 헬퍼 함수
    private float GetSFXVolume()
    {
        return GameManager.Instance != null ? GameManager.Instance.GetSFXVolume() : 1f;
    }

    // 점프 사운드 로컬 재생 (Owner 전용, 즉시 재생)
    public void PlayJumpSoundLocal()
    {
        if (jumpAudioSource != null)
        {
            float sfxVol = GetSFXVolume();

            // 캐릭터 보이스 재생
            if (jumpVoiceClip != null)
            {
                jumpAudioSource.PlayOneShot(jumpVoiceClip, jumpVoiceVolume * sfxVol);
            }

            // 효과음 재생
            if (jumpEffectClip != null)
            {
                jumpAudioSource.PlayOneShot(jumpEffectClip, jumpEffectVolume * sfxVol);
            }
        }
    }

    // 다이브 시작 사운드 로컬 재생 (Owner 전용, 즉시 재생)
    public void PlayDiveStartSoundLocal()
    {
        if (diveAudioSource != null && diveStartClip != null)
        {
            diveAudioSource.PlayOneShot(diveStartClip, diveStartVolume * GetSFXVolume());
        }
    }

    // 다이브 시작 사운드 재생 (서버에서 호출, 다른 클라이언트에서 재생)
    public void PlayDiveStartSound()
    {
        PlayDiveStartSoundClientRpc();
    }

    // 다이브 착지 사운드 재생 (서버에서 호출, 모든 클라이언트에서 재생)
    public void PlayDiveLandEffects()
    {
        PlayDiveLandEffectsClientRpc();
    }

    // 리스폰 파티클 재생 (서버에서 호출, 모든 클라이언트에서 재생)
    public void PlayRespawnEffects()
    {
        PlayRespawnEffectsClientRpc();
    }

    // 피격 사운드 재생 (서버에서 호출, 모든 클라이언트에서 재생)
    public void PlayHitSound()
    {
        PlayHitSoundClientRpc();
    }

    public void PlayThrowSound()
    {
        PlayThrowSoundClientRpc();
    }

    //////////////////////////////////////////////////////////////
    // 버프 아이템 이펙트 (1회용, 루프용)
    // 서버 권한 기반으로 제어, 실제 재생은 ClientRpc로 각 클라이언트에서 처리
    //////////////////////////////////////////////////////////////

    // 아이템 먹는 순간 1번 이펙트 재생용
    public void PlayBuffPickupEffect()
    {
        if (!IsServer) return;

        // 이펙트가 실제로 세팅되어 있을 때만 RPC 호출
        if (buffPickupEffect != null)
        {
            PlayBuffPickupEffectClientRpc();
        }
    }

    // 버프 타입별 루프용 이펙트 on/off
    public void SetBuffLoopEffect(BuffType type, bool enabled)
    {
        if (!IsServer) return;

        switch (type)
        {
            case BuffType.Speed:
                SetSpeedBuffEffectClientRpc(enabled);
                break;

            case BuffType.Jump:
                SetJumpBuffEffectClientRpc(enabled);
                break;

            case BuffType.Invincibility:
                SetInvincibleBuffEffectClientRpc(enabled);
                break;
        }
    }

    // 버프 시스템 공통 토글 함수
    private void ToggleLoopEffect(ParticleSystem ps, bool enabled)
    {
        if (ps == null) return;

        if (enabled)
        {
            if (!ps.isPlaying)
                ps.Play();
        }
        else
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    // 버프 루프 사운드 토글 함수
    private void ToggleLoopSound(AudioClip clip, bool enabled)
    {
        if (clip == null) return;

        // 무적 버프인 경우 별도 오디오 소스 사용
        bool isInvincibleClip = (clip == invincibleBuffLoopClip);
        AudioSource targetAudioSource = isInvincibleClip ? invincibleBuffLoopAudioSource : buffLoopAudioSource;
        float targetVolume = isInvincibleClip ? invincibleBuffLoopVolume : buffLoopVolume;

        if (targetAudioSource == null) return;

        if (enabled)
        {
            // 루프 사운드 재생
            if (!targetAudioSource.isPlaying || targetAudioSource.clip != clip)
            {
                targetAudioSource.clip = clip;
                targetAudioSource.volume = targetVolume * GetSFXVolume();
                targetAudioSource.loop = true;
                targetAudioSource.Play();
            }
        }
        else
        {
            // 루프 사운드 중지
            if (targetAudioSource.isPlaying && targetAudioSource.clip == clip)
            {
                targetAudioSource.Stop();
                targetAudioSource.clip = null;
            }
        }
    }

    // 발걸음 사운드 업데이트 (로컬 클라이언트에서만 호출)
    public void UpdateFootstepSoundLocal()
    {
        if (footstepAudioSource == null || footstepClip == null) return;

        // 파티클이 재생 중일 때만 발걸음 소리 재생
        if (isWalkParticlePlaying)
        {
            footstepTimer += Time.deltaTime;

            // 타이머가 간격을 넘으면 발걸음 소리 재생
            if (footstepTimer >= footstepInterval)
            {
                footstepAudioSource.PlayOneShot(footstepClip, footstepVolume * GetSFXVolume());
                footstepTimer = 0f; // 타이머 리셋
            }
        }
        else
        {
            // 걷지 않으면 타이머 리셋
            footstepTimer = 0.3f;
        }
    }

    // 죽음 사운드 재생 (서버에서 호출, 모든 클라이언트에서 재생)
    public void PlayDeathSound(bool isOceanDeath)
    {
        PlayDeathSoundClientRpc(isOceanDeath);
    }
}
