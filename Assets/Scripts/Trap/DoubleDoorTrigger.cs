using UnityEngine;
using Unity.Netcode;

public class DoubleDoorTrigger : NetworkBehaviour
{
    [Header("Door References")]
    public Transform leftDoor;   // Door_DoubleLeft
    public Transform rightDoor;  // Door_DoubleRight

    [Header("Settings")]
    public float openSpeed = 3f;
    public float leftOpenAngle = -90f;
    public float rightOpenAngle = 90f;

    [Header("Audio")]
    public AudioSource audioSource; // 문 오디오 소스
    public AudioClip doorOpenClip; // 문 열리는 사운드
    [Range(0f, 1f)] public float doorOpenVolume = 0.7f; // 문 열리는 볼륨

    private readonly NetworkVariable<bool> hasOpened = new(false);
    private readonly NetworkVariable<bool> isAnimating = new(false);
    private Vector3 leftInitialRotation;
    private Vector3 rightInitialRotation;

    // 목표 회전값
    private Vector3 LeftTargetRotation => new Vector3(
        leftInitialRotation.x,
        leftInitialRotation.y + leftOpenAngle,
        leftInitialRotation.z
    );

    private Vector3 RightTargetRotation => new Vector3(
        rightInitialRotation.x,
        rightInitialRotation.y + rightOpenAngle,
        rightInitialRotation.z
    );

    [Header("NavMesh Controller")]
    public DoorNavObstacle doorNavObstacle;

    void Start()
    {
        // 초기 회전값 저장 (Euler Angles)
        leftInitialRotation = leftDoor.localEulerAngles;
        rightInitialRotation = rightDoor.localEulerAngles;

        if (doorNavObstacle == null)
        {
            doorNavObstacle = GetComponent<DoorNavObstacle>();
        }

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

    void OnTriggerEnter(Collider other)
    {
        // 스크립트가 비활성화 상태면 트리거 무시
        if (!enabled) return;

        // 서버만 문 열기 처리
        if (!IsServer) return;

        if (!other.CompareTag("Player")) return;

        if (!hasOpened.Value)
        {
            hasOpened.Value = true;
            isAnimating.Value = true;

            // 문 열리는 사운드 재생
            PlayDoorOpenSoundClientRpc();

            // door의 Obstacle 제어 : 문 열기
            if (doorNavObstacle != null)
            {
                doorNavObstacle.OpenDoor();
            }
        }
    }

    void Update()
    {
        // 모든 클라이언트에서 애니메이션 재생 (NetworkVariable로 동기화)
        if (isAnimating.Value)
        {
            // EulerAngles로 부드럽게 회전
            leftDoor.localEulerAngles = new Vector3(
                leftDoor.localEulerAngles.x,
                Mathf.LerpAngle(leftDoor.localEulerAngles.y, LeftTargetRotation.y, Time.deltaTime * openSpeed),
                leftDoor.localEulerAngles.z
            );

            rightDoor.localEulerAngles = new Vector3(
                rightDoor.localEulerAngles.x,
                Mathf.LerpAngle(rightDoor.localEulerAngles.y, RightTargetRotation.y, Time.deltaTime * openSpeed),
                rightDoor.localEulerAngles.z
            );

            // 거의 다 열렸으면 애니메이션 중지 (서버만)
            if (IsServer && Mathf.Abs(Mathf.DeltaAngle(leftDoor.localEulerAngles.y, LeftTargetRotation.y)) < 0.1f)
            {
                leftDoor.localEulerAngles = LeftTargetRotation;
                rightDoor.localEulerAngles = RightTargetRotation;
                isAnimating.Value = false;
            }
        }
    }

    [ClientRpc]
    private void PlayDoorOpenSoundClientRpc()
    {
        if (audioSource != null && doorOpenClip != null)
        {
            audioSource.PlayOneShot(doorOpenClip, doorOpenVolume * GetSFXVolume());
        }
    }

    private float GetSFXVolume()
    {
        return GameManager.Instance != null ? GameManager.Instance.GetSFXVolume() : 1f;
    }
}