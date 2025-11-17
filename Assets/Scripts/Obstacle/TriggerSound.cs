using UnityEngine;

/// <summary>
/// 특정 콜라이더 영역 안에 있으면 소리를 재생하는 컴포넌트
/// Trigger 콜라이더를 사용하여 플레이어가 들어오면 소리 시작, 나가면 소리 중지
/// </summary>
public class TriggerSound : MonoBehaviour
{
    [Header("Audio Settings")]
    public AudioSource audioSource;
    public AudioClip soundClip;
    [Range(0f, 1f)] public float volume = 0.7f;

    [Header("Settings")]
    public bool loopSound = true; // 루프 재생 여부

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
        audioSource.loop = loopSound;
        audioSource.volume = volume;
        audioSource.spatialBlend = 1f; // 3D 사운드
    }

    private void OnTriggerEnter(Collider other)
    {
        // 플레이어가 들어왔는지 확인
        if (other.CompareTag("Player"))
        {
            PlaySound();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // 플레이어가 나갔는지 확인
        if (other.CompareTag("Player"))
        {
            StopSound();
        }
    }

    private void PlaySound()
    {
        if (audioSource != null && soundClip != null && !audioSource.isPlaying)
        {
            audioSource.clip = soundClip;
            audioSource.Play();
            Debug.Log("통돌이 효과음 재생중");
        }
    }

    private void StopSound()
    {
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
    }

    private void OnDisable()
    {
        // 비활성화되면 소리 중지
        StopSound();
    }
}
