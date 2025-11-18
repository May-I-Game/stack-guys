using UnityEngine;

/// <summary>
/// 플레이어가 가까이 오면 소리를 재생하는 컴포넌트
/// 빙빙 돌아가는 장애물 등에 사용
/// </summary>
public class ProximitySound : MonoBehaviour
{
    [Header("Audio Settings")]
    public AudioSource audioSource; // 오디오 소스
    public AudioClip proximityClip; // 근접 시 재생할 사운드
    [Range(0f, 1f)] public float maxVolume = 0.7f; // 최대 볼륨

    [Header("Distance Settings")]
    public float triggerDistance = 5f; // 소리가 시작되는 거리
    public float maxDistance = 10f; // 소리가 완전히 사라지는 거리
    public bool fadeByDistance = true; // 거리에 따라 볼륨 페이드

    private Transform player;
    private bool isPlaying = false;

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

        // 오디오 소스 기본 설정
        audioSource.playOnAwake = false;
        audioSource.loop = true;
        audioSource.spatialBlend = 1f; // 3D 사운드
        audioSource.minDistance = triggerDistance;
        audioSource.maxDistance = maxDistance;
    }

    private void Update()
    {
        // 플레이어 찾기 (처음에만 또는 플레이어가 없을 때)
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                player = playerObj.transform;
            }
            else
            {
                return; // 플레이어를 찾지 못하면 리턴
            }
        }

        // 거리 계산
        float distance = Vector3.Distance(transform.position, player.position);

        // 거리에 따라 소리 재생/중지
        if (distance <= triggerDistance && !isPlaying)
        {
            PlaySound();
        }
        else if (distance > triggerDistance && isPlaying)
        {
            StopSound();
        }

        // 거리에 따라 볼륨 조절
        if (isPlaying && fadeByDistance)
        {
            // triggerDistance에서 maxVolume, maxDistance에서 0
            float volumeScale = 1f - Mathf.Clamp01((distance - triggerDistance) / (maxDistance - triggerDistance));
            audioSource.volume = volumeScale * maxVolume;
        }
    }

    private void PlaySound()
    {
        if (audioSource != null && proximityClip != null)
        {
            audioSource.clip = proximityClip;
            audioSource.volume = maxVolume;
            audioSource.Play();
            isPlaying = true;
        }
    }

    private void StopSound()
    {
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
            isPlaying = false;
        }
    }

    private void OnDisable()
    {
        // 비활성화되면 소리 중지
        StopSound();
    }

    // 기즈모로 범위 표시 (에디터에서만)
    private void OnDrawGizmosSelected()
    {
        // 트리거 거리 (초록색)
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, triggerDistance);

        // 최대 거리 (빨간색)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, maxDistance);
    }
}
