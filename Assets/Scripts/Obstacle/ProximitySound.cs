using UnityEngine;

/// <summary>
/// Unity3D 기본 AudioSource를 사용하여 3D 사운드를 재생하는 컴포넌트
/// 빙빙 돌아가는 장애물 등에 사용
/// </summary>
public class ProximitySound : MonoBehaviour
{
    [Header("Audio Settings")]
    public AudioSource audioSource; // 오디오 소스
    public AudioClip proximityClip; // 재생할 사운드
    [Range(0f, 1f)] public float volume = 0.7f; // 볼륨

    [Header("3D Audio Settings")]
    public float minDistance = 1f; // 최소 거리 (이 거리 안에서는 최대 볼륨)
    public float maxDistance = 10f; // 최대 거리 (이 거리 밖에서는 소리 안 들림)
    public AudioRolloffMode rolloffMode = AudioRolloffMode.Linear; // 거리 감쇠 모드

    private void Start()
    {
        // 오디오 소스 자동 설정
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        // Unity 3D 오디오 시스템 설정
        audioSource.clip = proximityClip;
        audioSource.playOnAwake = true;
        audioSource.loop = true;
        audioSource.volume = volume * GetSFXVolume();
        audioSource.spatialBlend = 1f; // 완전히 3D 사운드
        audioSource.rolloffMode = rolloffMode;
        audioSource.minDistance = minDistance;
        audioSource.maxDistance = maxDistance;

        // 사운드 재생
        if (proximityClip != null)
        {
            audioSource.Play();
        }
    }

    private void Update()
    {
        // SFX 볼륨 실시간 적용
        if (audioSource != null)
        {
            audioSource.volume = volume * GetSFXVolume();
        }
    }

    private void OnDisable()
    {
        // 비활성화되면 소리 중지
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
    }

    private float GetSFXVolume()
    {
        return GameManager.Instance != null ? GameManager.Instance.GetSFXVolume() : 1f;
    }

    // 기즈모로 범위 표시 (에디터에서만)
    private void OnDrawGizmosSelected()
    {
        // 최소 거리 (초록색)
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, minDistance);

        // 최대 거리 (빨간색)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, maxDistance);
    }
}
