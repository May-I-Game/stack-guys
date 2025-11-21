using UnityEngine;
using UnityEngine.Events;
using Unity.Netcode;

public class PressButton : NetworkBehaviour
{
    [Header("Wall Settings")]
    [SerializeField] private GameObject wall;

    private Animator wallAnimator;

    [Header("Button Visual")]
    [SerializeField] private Transform buttonTransform; // 눌릴 버튼 오브젝트
    [SerializeField] private float pressDepth = 0.1f;   // 버튼이 눌리는 깊이
    [SerializeField] private float buttonSpeed = 10f;   // 버튼 애니메이션 속도

    [Header("Audio Settings")]
    [SerializeField] private AudioSource audioSource; // 오디오 소스
    [SerializeField] private AudioClip buttonPressClip; // 버튼 눌림 효과음
    [SerializeField] private AudioClip buttonReleaseClip; // 버튼 해제 효과음
    [Range(0f, 1f)][SerializeField] private float volume = 0.7f; // 볼륨

    private int objectsOnPlate = 0;
    private bool isPressed = false;

    private Vector3 originalPosition;  // 버튼의 원래 위치
    private Vector3 targetPosition;    // 버튼의 목표 위치

    // 벽 활성화 상태를 네트워크로 동기화 (서버만 쓰기 가능)
    private NetworkVariable<bool> isWallActive = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // [추가] 버튼의 목표 위치를 네트워크로 동기화 (버튼 애니메이션 제어용)
    private NetworkVariable<Vector3> networkTargetPosition = new NetworkVariable<Vector3>(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private void Awake()
    {
        // 버튼의 원래 위치 저장 (OnNetworkSpawn보다 먼저 실행됨)
        if (buttonTransform != null)
        {
            originalPosition = buttonTransform.localPosition;
        }
    }

private void Start()
{
    // Animator 컴포넌트 가져오기
    if (wall != null)
    {
        wallAnimator = wall.GetComponent<Animator>();
        if (wallAnimator == null)
        {
            Debug.LogError("[PressButton] Wall 오브젝트에 Animator 컴포넌트가 없습니다!");
        }
    }
    }

    private void Update()
    {
        // 버튼을 동기화된 목표 위치로 부드럽게 이동
        if (buttonTransform != null)
        {
            buttonTransform.localPosition = Vector3.Lerp(
                buttonTransform.localPosition,
                networkTargetPosition.Value, // 동기화된 목표 위치 사용
                Time.deltaTime * buttonSpeed
            );
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            // Start()에서 찾은 '올려져 있는' 버튼의 원래 위치로 초기화합니다.
            // 이 값이 모든 클라이언트에게 동기화되어, 버튼이 내려가 있지 않게 됩니다.
            networkTargetPosition.Value = originalPosition;

            // isWallActive도 초기화 (벽이 내려가 있어야 하므로 false)
            isWallActive.Value = false;
        }

        // 초기 벽 상태 적용
        UpdateWallState(isWallActive.Value);

        // NetworkVariable 값 변경 감지 리스너 등록
        isWallActive.OnValueChanged += OnWallStateChanged;
    }

    public override void OnNetworkDespawn()
    {
        // 리스너 해제
        isWallActive.OnValueChanged -= OnWallStateChanged;
        base.OnNetworkDespawn();
    }

    private void OnTriggerEnter(Collider other)
    {
        // 서버에서만 실행
        if (!IsServer) return;

        if (other.GetComponent<Rigidbody>() != null)
        {
            objectsOnPlate++;

            if (objectsOnPlate == 1 && !isPressed)
            {
                isPressed = true;
                Debug.Log("버튼 눌림!");

                // 버튼을 아래로 내림
                if (buttonTransform != null)
                {
                    // 서버에서 목표 위치를 설정 -> 클라이언트 동기화
                    networkTargetPosition.Value = originalPosition + Vector3.down * pressDepth;
                }

                // NetworkVariable 값 변경 -> 모든 클라이언트에 자동 동기화
                isWallActive.Value = true;
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // 서버에서만 실행
        if (!IsServer) return;

        if (other.GetComponent<Rigidbody>() != null)
        {
            objectsOnPlate--;

            if (objectsOnPlate == 0 && isPressed)
            {
                isPressed = false;
                Debug.Log("버튼 해제됨!");

                // 버튼을 원래 위치로 올림
                if (buttonTransform != null)
                {
                    // 서버에서 목표 위치를 설정 -> 클라이언트 동기화
                    networkTargetPosition.Value = originalPosition;
                }

                // NetworkVariable 값 변경 -> 모든 클라이언트에 자동 동기화
                isWallActive.Value = false;
            }
        }
    }

    //private void OnColliderEnter(Collider other)
    //{
    //    // 서버에서만 실행
    //    if (!IsServer) return;

    //    if (other.GetComponent<Rigidbody>() != null)
    //    {
    //        objectsOnPlate++;

    //        if (objectsOnPlate == 1 && !isPressed)
    //        {
    //            isPressed = true;
    //            Debug.Log("버튼 눌림!");

    //            // 버튼을 아래로 내림
    //            if (buttonTransform != null)
    //            {
    //                // 서버에서 목표 위치를 설정 -> 클라이언트 동기화
    //                networkTargetPosition.Value = originalPosition + Vector3.down * pressDepth;
    //            }

    //            // NetworkVariable 값 변경 -> 모든 클라이언트에 자동 동기화
    //            isWallActive.Value = true;
    //        }
    //    }
    //}

    //private void OnColliderExit(Collider other)
    //{
    //    // 서버에서만 실행
    //    if (!IsServer) return;

    //    if (other.GetComponent<Rigidbody>() != null)
    //    {
    //        objectsOnPlate--;

    //        if (objectsOnPlate == 0 && isPressed)
    //        {
    //            isPressed = false;
    //            Debug.Log("버튼 해제됨!");

    //            // 버튼을 원래 위치로 올림
    //            if (buttonTransform != null)
    //            {
    //                // 서버에서 목표 위치를 설정 -> 클라이언트 동기화
    //                networkTargetPosition.Value = originalPosition;
    //            }

    //            // NetworkVariable 값 변경 -> 모든 클라이언트에 자동 동기화
    //            isWallActive.Value = false;
    //        }
    //    }
    //}

    // NetworkVariable 값이 변경되면 모든 클라이언트에서 호출됨
    private void OnWallStateChanged(bool oldValue, bool newValue)
    {
        Debug.Log($"[PressButton] 벽 상태 변경: {oldValue} -> {newValue}");
        UpdateWallState(newValue);

        // 버튼 상태에 따라 효과음 재생
        PlayButtonSound(newValue);
    }

    // 버튼 효과음 재생
    private void PlayButtonSound(bool isPressed)
    {
        if (audioSource == null) return;

        AudioClip clipToPlay = isPressed ? buttonPressClip : buttonReleaseClip;

        if (clipToPlay != null)
        {
            audioSource.PlayOneShot(clipToPlay, volume);
        }
    }

    // 벽의 활성화 상태 업데이트 (NetworkVariable 값이 변경될 때 모든 클라이언트에서 호출됨)
    private void UpdateWallState(bool active)
    {
        // 1. 벽 게임 오브젝트가 할당되었는지 확인 (Active/Deactive 로직은 제거)
        if (wall == null)
        {
            Debug.LogWarning("[PressButton] Wall GameObject가 할당되지 않았습니다!");
            return;
        }

        // 2. Animator 컴포넌트가 할당되었는지 확인
        if (wallAnimator != null)
        {
            // active 값이 곧 Animator의 "Button" Bool 파라미터의 상태가 됩니다.
            // true일 때 Wall_up 방향으로, false일 때 Wall_down 방향으로 전환됩니다.
            wallAnimator.SetBool("Button", active);

            Debug.Log($"[PressButton] Animator Bool 'Button'을 {(active ? "True" : "False")}로 설정. 벽 애니메이션 시작.");
        }
        else
        {
            // 애니메이터가 없으면 이전처럼 GameObject 활성화/비활성화를 대신 사용 (선택 사항)
            // wall.SetActive(active); 
            Debug.LogWarning("[PressButton] Wall Animator가 할당되지 않았습니다! (애니메이션이 재생되지 않습니다)");
        }
    }
}