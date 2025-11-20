using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(Collider))]
public class JumpPad : NetworkBehaviour
{
    [Header("점프 설정")]
    [SerializeField] private float launchForce = 20f;

    [Header("쿨다운 설정")]
    [SerializeField] private float cooldownTime = 0.5f;
    private float lastLaunchTime = -999f;

    [Header("Audio")]
    public AudioSource audioSource;
    [SerializeField] private AudioClip jumpClip;  // 점프 사운드
    [SerializeField] private AudioClip windClip;  // 바람 사운드

    [Range(0f, 1f)] public float jumpVolume = 0.7f;
    [Range(0f, 1f)] public float windVolume = 0.7f;

    private Collider triggerCollider;

    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();
        triggerCollider.isTrigger = true;

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f; // 3D 사운드
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer)
            return;

        if (Time.time - lastLaunchTime < cooldownTime)
            return;

        if (!other.CompareTag("Player"))
            return;

        Rigidbody rb = other.GetComponent<Rigidbody>();
        if (rb == null)
            return;

        LaunchPlayer(rb);
        lastLaunchTime = Time.time;

        PlayLaunchSoundClientRpc();
    }

    private void LaunchPlayer(Rigidbody rb)
    {
        rb.linearVelocity = Vector3.zero;
        Vector3 direction = transform.up;

        rb.AddForce(direction * launchForce, ForceMode.VelocityChange);
    }

    [ClientRpc]
    private void PlayLaunchSoundClientRpc()
    {
        if (audioSource == null) return;

        if (jumpClip != null)
            audioSource.PlayOneShot(jumpClip, jumpVolume);

        if (windClip != null)
            audioSource.PlayOneShot(windClip, windVolume);
    }
}
