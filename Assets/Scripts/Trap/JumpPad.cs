using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(BoxCollider))]
public class JumpPad : NetworkBehaviour
{
    [Header("점프 설정")]
    [SerializeField] private float launchForce = 20f;   // 발사 힘
    // [SerializeField] private float launchAngle = 45f;   // 발사 각도

    [Header("쿨다운 설정")]
    [SerializeField] private float cooldownTime = 0.5f; // 연속 발동 방지
    private float lastLaunchTime = -999f;

    [Header("Audio")]
    public AudioSource audioSource; // 점프패드 오디오 소스
    public AudioClip launchClip; // 점프패드 발사 사운드
    [Range(0f, 1f)] public float launchVolume = 0.7f; // 발사 볼륨

    private BoxCollider triggerCollider;

    private void Awake()
    {
        triggerCollider = GetComponent<BoxCollider>();
        triggerCollider.isTrigger = true;

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
        audioSource.spatialBlend = 1f; // 3D 사운드
    }

    // 플레이어 충돌 감지 및 점프 실행
    private void OnTriggerEnter(Collider other)
    {
        // 서버만 물리 처리 (클라이언트는 Trigger 감지만, 물리는 서버 권위)
        if (!IsServer)
            return;

        if (Time.time - lastLaunchTime < cooldownTime)
            return;

        if (!other.CompareTag("Player"))
            return;

        Rigidbody rb = other.GetComponent<Rigidbody>();
        if (rb == null)
            return;

        // 서버에서만 물리 적용
        LaunchPlayer(rb);
        lastLaunchTime = Time.time;

        // 점프패드 발사 사운드 재생
        PlayLaunchSoundClientRpc();
    }

    // 플레이어에게 발사 힘 적용
    private void LaunchPlayer(Rigidbody rb)
    {
        rb.linearVelocity = Vector3.zero;
        Vector3 direction = transform.up;

        rb.AddForce(direction * launchForce, ForceMode.VelocityChange);
    }

    [ClientRpc]
    private void PlayLaunchSoundClientRpc()
    {
        if (audioSource != null && launchClip != null)
        {
            audioSource.PlayOneShot(launchClip, launchVolume);
        }
    }
}
