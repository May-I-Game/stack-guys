using NUnit.Framework.Internal;
using Unity.Cinemachine;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlayerController : NetworkBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 4f;
    public float rotationSpeed = 10f;

    [Header("Jump Settings")]
    public float jumpForce = 3f;
    public float diveForce = 4f; // 다이브할 때 앞으로 가는 힘
    public float diveDownForce = 1f; // 다이브할 때 아래로 가는 힘

    [Header("Grap Settings")]
    public float grabRange = 1f; // 잡기 범위
    public float holdHeight = 0.6f; // 머리 위 높이
    public float holdDistance = 0.1f; // 플레이어 앞쪽 거리
    public float throwForce = 5f; // 던지기 힘
    public int escapeRequiredJumps = 5; // 탈출에 필요한 점프 횟수

    [Header("Collision")]
    public float groundCheckDist = 0.1f;
    private RaycastHit[] groundHits = new RaycastHit[3];
    private Collider[] grabColliders = new Collider[10]; // GC 최적화: 사전 할당
    public LayerMask groundLayerMask = -1; // 땅으로 인식할 레이어 (최적화용, -1 = 모든 레이어)
    public float bounceForce = 5f; // 튕겨나가는 힘

    [Header("Animation")]
    public Animator animator;

    [Header("Particle Effects")]
    public ParticleSystem walkParticle; // 걷기 파티클
    public ParticleSystem jumpParticle; // 점프 파티클
    public ParticleSystem diveLandParticle; // 다이브 착지 파티클
    public ParticleSystem respawnParticle; // 리스폰 파티클

    [Header("Buff Effects")]
    public ParticleSystem buffPickupEffect;          // 아이템 먹는 순간 번쩍
    public ParticleSystem jumpBuffLoopEffect;        // 점프 버프 루프
    public ParticleSystem speedBuffLoopEffect;       // 속도 버프 루프
    public ParticleSystem invincibleBuffLoopEffect;  // 무적 버프 루프

    // 버프 적용 배율 (봇이면 1로 처리)
    protected float SpeedMul => buffManager != null ? buffManager.SpeedMultiplier : 1f;
    protected float JumpMul => buffManager != null ? buffManager.JumpMultiplier : 1f;

    [Header("Audio")]
    public AudioSource footstepAudioSource; // 발걸음 오디오 소스
    public AudioClip footstepClip; // 발걸음 사운드 클립
    [Range(0f, 1f)] public float footstepVolume = 0.5f; // 발걸음 볼륨
    public float footstepInterval = 0.4f; // 발걸음 재생 간격 (초)

    public AudioSource jumpAudioSource; // 점프 오디오 소스
    public AudioClip jumpVoiceClip; // 점프 캐릭터 보이스 클립
    public AudioClip jumpEffectClip; // 점프 효과음 클립
    [Range(0f, 1f)] public float jumpVoiceVolume = 0.7f; // 점프 보이스 볼륨
    [Range(0f, 1f)] public float jumpEffectVolume = 0.5f; // 점프 효과음 볼륨

    public AudioSource diveAudioSource; // 다이브 오디오 소스
    public AudioClip diveStartClip; // 다이브 시작 효과음
    public AudioClip diveLandVoiceClip; // 다이브 착지 캐릭터 보이스
    public AudioClip diveLandImpactClip; // 다이브 착지 바닥 충돌음
    [Range(0f, 1f)] public float diveStartVolume = 0.6f; // 다이브 시작 볼륨
    [Range(0f, 1f)] public float diveLandVoiceVolume = 0.7f; // 다이브 착지 보이스 볼륨
    [Range(0f, 1f)] public float diveLandImpactVolume = 0.8f; // 다이브 착지 충돌음 볼륨

    public AudioSource buffAudioSource; // 버프 아이템 오디오 소스
    public AudioClip buffPickupClip; // 버프 아이템 획득 사운드
    [Range(0f, 1f)] public float buffPickupVolume = 0.7f; // 버프 아이템 획득 볼륨

    public AudioSource buffLoopAudioSource; // 버프 루프 오디오 소스
    public AudioClip buffLoopClip; // 버프 루프 사운드 (속도/점프 버프용)
    public AudioClip invincibleBuffLoopClip; // 무적 버프 루프 사운드
    [Range(0f, 1f)] public float buffLoopVolume = 0.5f; // 버프 루프 볼륨

    [Header("Network Optimization")]
    [Tooltip("입력 전송 최소 간격 (초). 모바일 조이스틱 떨림 방지. 권장: 0.033~0.05")]
    public float inputSendInterval = 0.05f;  // 50ms = 20Hz
    [Tooltip("입력 변화량 임계값. 이 값 이상 변할 때만 즉시 전송. 권장: 0.1")]
    public float inputDeltaThreshold = 0.1f;  // 10% 변화
    [Tooltip("이동 속도 동기화 임계값. 이 값 이상 변할 때만 동기화. 권장: 0.5")]
    public float speedThreshold = 0.5f;  // 0.5 m/s 이상 변화만
    [Tooltip("땅 체크 간격 (프레임). 1=매프레임, 2=2프레임마다. 권장: 2")]
    public int groundCheckInterval = 2;  // 2프레임마다 체크 (50Hz → 25Hz)

    public float lerpSpeed = 15f;
    public float smoothTime = 0.1f;

    [Header("Player Info")]
    private NetworkVariable<FixedString32Bytes> playerName = new NetworkVariable<FixedString32Bytes>(
        "",
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    protected Rigidbody rb;
    private CapsuleCollider col;
    private PlayerInputHandler inputHandler;
    protected PlayerBuffManager buffManager;    // 버프 관리자

    protected Vector2 moveDir = Vector2.zero;
    private Vector2 lastSentInput = Vector2.zero;  // 실제로 서버에 전송한 마지막 입력
    private float lastInputSendTime = 0f;  // 마지막 입력 전송 시간

    // GC 최적화: WaitForSeconds 캐싱
    private WaitForSeconds botRespawnWait;
    private Vector3 lastHeldObjectPosition = Vector3.zero;  // 마지막 잡은 오브젝트 위치
    protected bool isJumpQueued;
    protected bool isGrabQueued;
    private Vector3 deathPosition;  // 죽은 위치 저장용

    public NetworkObject bodyPrefab;

    protected NetworkVariable<bool> netIsMove = new NetworkVariable<bool>(false); // 움직이는중인지
    protected NetworkVariable<bool> netIsGrounded = new NetworkVariable<bool>(true); // 땅인지
    protected bool isDiving = false; // 공중 다이브 중인지
    protected bool isDiveGrounded = false; // 다이브 착지 상태 (이동 불가)
    protected NetworkVariable<bool> netIsDeath = new NetworkVariable<bool>(false); // 죽었는지
    protected bool isHit = false; // 충돌 상태 (이동 불가)
    protected bool canDive = false; // 다이브 가능 상태 (점프 중)

    // 걷기 파티클 재생 상태 추적
    private bool isWalkParticlePlaying = false;

    // 발걸음 사운드 타이머
    private float footstepTimer = 0f;

    // 잡기 관련 변수
    protected NetworkVariable<bool> netIsGrabbed = new NetworkVariable<bool>(false); // 잡혀있는지
    protected bool isHolding = false; // 잡고 있는지
    private ulong grabberId = 0; // 누구한테 잡혔는지
    private ulong holdingTargetId = 0; // 누구를 잡고있는지

    protected GameObject holdingObject = null; // 실제로 들고 있는 오브젝트
    private PlayerController heldPlayerCache = null; // 잡은 플레이어 캐시 (최적화)
    private int heldObjectOriginLayer;
    private int escapeJumpCount = 0; // 탈출 시도 횟수

    // 위치 동기화용 변수
    protected Vector3 _targetPos;
    protected float _targetRotY;
    private Vector3 _currentVelocity;

    // 마지막 동기화 상태 변수 (Dirty Check)
    private Vector3 _lastSyncedPos;
    private float _lastSyncedRotY;
    private bool _lastSyncStateInitialized = false;

    private CinemachineCamera cam;

    // 리스폰 구역 Index 값
    public NetworkVariable<int> RespawnId = new(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [SerializeField] protected RespawnManager respawnManager;     // 리스폰 리스트를 사용하기 위하여 선언

    // 시네마틱 동기화를 위한 사용자 입력 무시 변수
    public NetworkVariable<bool> inputEnabled = new NetworkVariable<bool>(true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // hit 타이머 변수 (관심 영역 밖 봇을 위한 타이머)
    protected float hitTime = 0f;
    protected float hitDuration = 1.5f;                           // 애니메이션 길이보다 약간 길게

    public override void OnNetworkSpawn()
    {
        // 서버만 물리 활성화 (서버 권위 방식)
        // 클라이언트는 NetworkTransform으로 위치만 동기화
        if (IsServer)
        {
            EnablePhysics(true);
        }
        else
        {
            EnablePhysics(false);
        }

        if (BatchNetworkManager.Instance != null)
        {
            BatchNetworkManager.Instance.RegisterPlayer(NetworkObjectId, this);
        }

        // 초기 위치 동기화
        _targetPos = transform.position;
        _targetRotY = transform.rotation.eulerAngles.y;

        if (IsOwner)
        {
            cam = FindAnyObjectByType<CinemachineCamera>();
            cam.Follow = this.transform;

            string savedName = PlayerPrefs.GetString("player_name", ""); // 소문자!
            playerName.Value = savedName;
            Debug.Log($"플레이어 이름 설정: {savedName}");
        }
    }

    // 디스폰 때 등록 해제 (안 하면 에러 남)
    public override void OnNetworkDespawn()
    {
        if (BatchNetworkManager.Instance != null)
        {
            BatchNetworkManager.Instance.UnregisterPlayer(NetworkObjectId);
        }
    }

    public void EnablePhysics(bool on)
    {
        if (rb)
        {
            rb.isKinematic = !on;
            rb.detectCollisions = on;
        }
        // Collider는 항상 켜두되, 클라이언트는 Trigger 전용 (물리 충돌 없음)
        if (col)
        {
            col.enabled = true;  // 항상 활성화
            col.isTrigger = !on; // 서버: Collision, 클라이언트: Trigger
        }
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<CapsuleCollider>();
    }

    protected virtual void Start()
    {
        inputHandler = GetComponent<PlayerInputHandler>();
        respawnManager = FindFirstObjectByType<RespawnManager>();

        buffManager = GetComponent<PlayerBuffManager>();

        // GC 최적화: WaitForSeconds 사전 생성
        botRespawnWait = new WaitForSeconds(2.267f);

        // Animator가 설정되지 않았다면 자동으로 찾기
        animator = animator != null ? animator : GetComponent<Animator>();
        animator = animator != null ? animator : GetComponentInChildren<Animator>();

        // AudioSource 초기 설정 (Owner만 설정)
        if (IsOwner && footstepAudioSource != null)
        {
            footstepAudioSource.playOnAwake = false;
        }
    }

    protected virtual void Update()
    {
        //클라이언트만 Update 수행
        if (IsServer) return;

        //본인이 아닌 캐릭터, 혹은 input이 비활성화 되어있을 때는 애니메이션만 최신화
        if (!IsOwner || !inputEnabled.Value)
        {
            InterpolateMovement();
            UpdateAnimation();
            return;
        }

        Vector2 currentInput = inputHandler.MoveInput;

        // ===== 입력 동기화 최적화 (모바일 조이스틱 기준) =====
        float timeSinceLastSend = Time.time - lastInputSendTime;
        float inputDelta = Vector2.Distance(currentInput, lastSentInput);

        bool shouldSendInput = false;

        // 조건 1: 이동 시작 (정지 → 이동)
        if (lastSentInput.magnitude < 0.01f && currentInput.magnitude >= 0.1f)
        {
            shouldSendInput = true;  // 즉시 전송 (반응성 최우선)
        }
        // 조건 2: 완전히 멈춤 (이동 → 정지)
        else if (lastSentInput.magnitude >= 0.1f && currentInput.magnitude < 0.01f)
        {
            shouldSendInput = true;  // 즉시 전송 (멈춤은 즉각 반영)
        }
        // 조건 3: 큰 방향 전환 (임계값 이상 변화)
        else if (inputDelta >= inputDeltaThreshold)
        {
            shouldSendInput = true;  // 즉시 전송 (급격한 방향 전환)
        }
        // 조건 4: 일정 시간마다 전송 (조이스틱 유지 시 주기적 동기화)
        else if (timeSinceLastSend >= inputSendInterval && inputDelta > 0.001f)
        {
            shouldSendInput = true;  // 주기적 전송 (미세 변화 누적 반영)
        }

        if (shouldSendInput)
        {
            MovePlayerServerRpc(currentInput);
            lastSentInput = currentInput;
            lastInputSendTime = Time.time;
        }

        // 점프 입력
        if (inputHandler.JumpInput)
        {
            // 점프 사운드 즉시 로컬 재생 (지연 방지)
            PlayJumpSoundLocal();

            JumpPlayerServerRpc();
            inputHandler.ResetJumpInput();
        }

        // 잡기 입력
        if (inputHandler.GrabInput)
        {
            GrabPlayerServerRpc();
            inputHandler.ResetGrabInput();
        }

        InterpolateMovement();
        UpdateAnimation();
        // 파티클 상태가 업데이트된 후 발걸음 사운드 재생
        UpdateFootstepSoundLocal();
    }

    protected virtual void FixedUpdate()
    {
        // 서버만 로직 처리
        if (!IsServer) return;
        // 죽었으면 처리 무시
        if (netIsDeath.Value) return;

        ServerPerformanceProfiler.Start("PlayerController.FixedUpdate");
        // 땅 체크
        GroundCheck();
        // 이동 처리
        PlayerMove();

        // 점프 요청이 있으면
        if (isJumpQueued)
        {
            // 점프 처리
            ServerPerformanceProfiler.Start("PlayerController.Jump");
            PlayerJump();
            ServerPerformanceProfiler.End("PlayerController.Jump");
        }
        // 잡기 요청이 있으면
        if (isGrabQueued)
        {
            // 잡기 처리
            ServerPerformanceProfiler.Start("PlayerController.Grab");
            PlayerGrab();
            ServerPerformanceProfiler.End("PlayerController.Grab");
        }
        // 잡고 있으면
        if (isHolding && holdingObject != null)
        {
            // 들기 처리
            ServerPerformanceProfiler.Start("PlayerController.Holding");
            PlayerHeld();
            ServerPerformanceProfiler.End("PlayerController.Holding");
        }
        ServerPerformanceProfiler.End("PlayerController.FixedUpdate");
    }

    // 매니저가 호출해주는 함수 (패킷 도착 시)
    public void UpdateTargetState(Vector3 newPos, float newRotY)
    {
        _targetPos = newPos;
        _targetRotY = newRotY;
    }

    protected void InterpolateMovement()
    {
        // 너무 멀면 SmoothDamp 하지 말고 그냥 강제 이동 (텔레포트로 간주)
        float sqrDist = (transform.position - _targetPos).sqrMagnitude;
        if (sqrDist > 9.0f) // 3 * 3 = 9
        {
            transform.position = _targetPos;
            transform.rotation = Quaternion.Euler(0, _targetRotY, 0);
            _currentVelocity = Vector3.zero;
            return;
        }

        // 위치 보간 (부드럽게)
        transform.position = Vector3.SmoothDamp(
            transform.position,
            _targetPos,
            ref _currentVelocity,
            smoothTime
        );

        // 회전 보간 (Y축만)
        Quaternion targetRot = Quaternion.Euler(0, _targetRotY, 0);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * lerpSpeed);
    }

    public void SetInputEnabled(bool enabled)
    {
        inputEnabled.Value = enabled;
    }

    public string GetPlayerName()
    {
        string name = playerName.Value.ToString();
        return string.IsNullOrEmpty(name) ? $"Player{OwnerClientId}" : name;
    }

    // 클라에서 서버에게 요청할 Rpc 모음, 봇의 소유권 문제 때문에 false 설정
    #region ServerRpcs
    [ServerRpc(Delivery = RpcDelivery.Unreliable)]
    protected void MovePlayerServerRpc(Vector2 direction)
    {
        if (!inputEnabled.Value) return;

        // 이동 방향 임계값 체크: 방향 변화가 크거나 멈출 때만 동기화
        Vector2 directionDelta = direction - moveDir;
        if (directionDelta.magnitude >= inputDeltaThreshold || direction == Vector2.zero)
        {
            moveDir = direction;
        }
    }

    [ServerRpc(Delivery = RpcDelivery.Unreliable)]
    protected void JumpPlayerServerRpc()
    {
        if (!inputEnabled.Value) return;

        // 충돌 중이거나 다이브 착지 중이면 입력 무시
        if (isHit || isDiveGrounded)
        {
            return;
        }

        isJumpQueued = true;
    }

    [ServerRpc(Delivery = RpcDelivery.Unreliable)]
    private void GrabPlayerServerRpc()
    {
        if (!inputEnabled.Value) return;

        // 충돌 중이거나 공중에 있거나 다이브 착지 중이거나 잡힌 상태면 입력 무시
        if (isHit || !netIsGrounded.Value || isDiveGrounded || netIsGrabbed.Value)
        {
            return;
        }

        isGrabQueued = true;
    }

    // 애니메이션 이벤트에서 호출됨
    public void RespawnPlayer()
    {
        // Owner만 실행 (다른 클라이언트는 무시)
        if (!IsOwner) return;

        // 봇은 이미 BotRespawnDelay()로 리스폰하므로 무시
        if (this is BotController) return;

        // ServerRpc 호출 (시체 생성 + 텔레포트)
        RespawnPlayerServerRpc();
    }

    // ServerRpc: 서버에서 시체 생성 + 텔레포트 실행
    [ServerRpc(RequireOwnership = false)]
    private void RespawnPlayerServerRpc()
    {
        DoRespawnTeleport();
    }

    // 애니메이션이 끝날때 호출되는 함수
    [ServerRpc(RequireOwnership = false)]
    public void ResetHitStateServerRpc()
    {
        //이제 이동 가능
        isHit = false;
    }

    [ServerRpc(RequireOwnership = false)]
    public void ResetDiveGroundedStateServerRpc()
    {
        // Debug.Log("다이브리셋 호출됨!!");
        isDiveGrounded = false;
    }

    [ServerRpc(RequireOwnership = false)]
    public void ResetStateServerRpc()
    {
        ResetPlayerState();
    }
    #endregion

    // 서버에서 실제로 실행할 로직
    // 여기에 있는 모든 로직은 서버만 실행해야함!!!!!!!!
    #region ServerLogic
    protected void PlayerMove()
    {
        // 충돌 중이거나 다이브 착지 중이거나 잡힌 상태면 입력 무시
        if (isHit || isDiveGrounded || netIsGrabbed.Value) return;

        // 이동 요청이 있으면
        if (moveDir.magnitude >= 0.1f)
        {
            ServerPerformanceProfiler.Start("PlayerController.Move");

            // 이동 버프 적용
            float currentSpeed = walkSpeed * SpeedMul;

            // 이동
            Vector3 movement = new Vector3(
                moveDir.x,
                0,
                moveDir.y
            ) * currentSpeed * Time.fixedDeltaTime;
            rb.MovePosition(rb.position + movement);

            // 회전
            Quaternion targetRotation = Quaternion.LookRotation(movement);
            transform.rotation = targetRotation;

            netIsMove.Value = true;
            ServerPerformanceProfiler.End("PlayerController.Move");
        }
        else
        {
            netIsMove.Value = false;
        }
    }

    public bool GetLastSyncedState(out Vector3 lastPos, out float lastRotY)
    {
        lastPos = _lastSyncedPos;
        lastRotY = _lastSyncedRotY;
        return _lastSyncStateInitialized;
    }

    public void SetLastSyncedState(Vector3 newPos, float newRotY)
    {
        _lastSyncedPos = newPos;
        _lastSyncedRotY = newRotY;
        _lastSyncStateInitialized = true;
    }

    protected void PlayerJump()
    {
        // 잡혔으면 탈출시도
        if (netIsGrabbed.Value)
        {
            Debug.Log($"[잡기] 플레이어 탈출 시도: {escapeJumpCount}");
            escapeJumpCount++;
            if (escapeJumpCount >= escapeRequiredJumps)
            {
                EscapeFromGrap();
            }
        }

        else
        {
            // 땅에 있을 때: 점프
            if (netIsGrounded.Value)
            {
                // 점프 버프 적용
                float currentJumpForce = jumpForce * JumpMul;

                // 봇일때 점프
                if (this is BotController bot)
                {
                    rb.AddForce(Vector3.up * currentJumpForce, ForceMode.Impulse);
                    rb.AddForce(Vector3.forward * 3f, ForceMode.Impulse);
                }
                else
                {
                    rb.AddForce(Vector3.up * currentJumpForce, ForceMode.Impulse);
                }
                // 점프 파티클 재생
                PlayJumpParticle();
                // 점프 사운드 재생
                PlayJumpSound();

                netIsGrounded.Value = false; // 점프 시 강제로 false 설정
                canDive = true; // 점프 후 다이브 가능
            }
            // 공중에 있을 때: 다이브
            else if (canDive && !isDiving && !isHolding)
            {
                PlayerDive();
            }
        }

        isJumpQueued = false;
    }

    private void PlayerDive()
    {
        isDiving = true;
        canDive = false;

        // 현재 바라보는 방향으로 앞으로 힘 가하기
        Vector3 diveDirection = transform.forward * diveForce + Vector3.down * diveDownForce;
        rb.linearVelocity = Vector3.zero; // 기존 속도 초기화
        rb.AddForce(diveDirection, ForceMode.Impulse);

        // 다이브 시작 사운드 즉시 로컬 재생 (Owner만, 지연 방지)
        if (IsOwner)
        {
            PlayDiveStartSoundLocal();
        }

        // 다이브 시작 사운드 재생 (다른 플레이어들을 위해)
        PlayDiveStartSound();

        // 다이브 애니메이션 실행 (공중)
        SetTriggerClientRpc("Dive");
    }

    // 다이브 착지 처리
    private void OnDiveLand()
    {
        if (!isDiving) return;

        isDiving = false;
        isDiveGrounded = true;

        // 이동 입력 초기화 (걷기 파티클 즉시 재생 방지)
        moveDir = Vector2.zero;
        netIsMove.Value = false;

        // 다이브 착지 파티클 재생
        PlayDiveLandParticle();
        // 다이브 착지 사운드 재생
        PlayDiveLandSound();

        Debug.Log("[다이브 착지] 착지 애니메이션 재생, 조작 불가");
        SetTriggerClientRpc("DiveLand");
    }

    protected void PlayerGrab()
    {
        // 잡기중이 아니면 잡기시도
        if (!isHolding)
        {
            TryGrab();
        }

        // 잡기 중이면 던지기 시도
        else
        {
            TryThrow();
        }

        isGrabQueued = false;
    }

    private void TryGrab()
    {
        float scale = transform.localScale.x;
        Vector3 grabOffset = transform.forward * 1f * scale
                           + transform.up * 1f * scale;

        // GC 최적화: NonAlloc 버전 사용
        int count = Physics.OverlapBoxNonAlloc(
            transform.position + grabOffset,
            Vector3.one * grabRange * scale,
            grabColliders,
            transform.rotation
        );

        for (int i = 0; i < count; i++)
        {
            Collider col = grabColliders[i];

            // 자기자신 제외
            if (col.gameObject == this.gameObject) continue;

            // 다른 플레이어 체크
            PlayerController otherPlayer = col.GetComponent<PlayerController>();
            if (otherPlayer != null && !otherPlayer.netIsGrabbed.Value && !otherPlayer.isHolding)
            {
                // 무적 버프가 있으면 잡을 수 없음
                if (otherPlayer.buffManager != null && otherPlayer.buffManager.IsInvincible)
                {
                    continue;
                }

                GrabPlayer(otherPlayer);
                return;
            }

            // 오브젝트 체크
            IGrabbable grabbable = col.GetComponent<IGrabbable>();
            if (grabbable != null && !grabbable.IsGrabbed)
            {
                GrabObject(grabbable);
                return;
            }
        }
    }

    protected virtual void OnDrawGizmos()
    {
        // 에디터/프리팹 모드에서도 안전하게 동작하도록 보완
        if (col == null)
        {
            col = GetComponent<CapsuleCollider>();
            if (col == null)
            {
                // 콜라이더가 없으면 기즈모를 그리지 않음
                return;
            }
        }

        float scale = transform.localScale.x;
        Vector3 grabOffset = transform.forward * 1f * scale
                           + transform.up * 1f * scale;

        Gizmos.color = Color.red;
        Vector3 center = transform.position + grabOffset;
        Vector3 size = (Vector3.one * grabRange * scale) * 2;

        // 기즈모의 좌표계 행렬을 현재 오브젝트의 회전값으로 변경
        Matrix4x4 rotationMatrix = Matrix4x4.TRS(center, transform.rotation, size);
        Gizmos.matrix = rotationMatrix;

        // 행렬에서 이미 위치와 크기를 적용했으므로, 여기서는 1x1x1 큐브를 그립니다.
        Gizmos.DrawWireCube(Vector3.zero, Vector3.one);

        // 행렬 초기화 (다른 기즈모에 영향 주지 않기 위해)
        Gizmos.matrix = Matrix4x4.identity;
    }

    private void GrabPlayer(PlayerController otherPlayer)
    {
        holdingObject = otherPlayer.gameObject;
        heldPlayerCache = otherPlayer;  // 캐싱 (GetComponent 방지)
        isHolding = true;
        holdingTargetId = otherPlayer.NetworkObjectId;

        // 상대방 상태 변경
        otherPlayer.netIsGrabbed.Value = true;
        otherPlayer.grabberId = this.NetworkObjectId;
        otherPlayer.escapeJumpCount = 0;

        // 상대방 물리 비활성화
        if (otherPlayer.rb != null)
        {
            otherPlayer.rb.isKinematic = true;
        }

        // 레이어 저장 및 비활성화 (충돌 무시용)
        heldObjectOriginLayer = otherPlayer.gameObject.layer;
        otherPlayer.gameObject.layer = LayerMask.NameToLayer("HeldObject");

        //Debug.Log($"[잡기] 오브젝트 레이어 변환: {otherPlayer.gameObject.layer}");
        //Debug.Log($"[잡기] 플레이어를 잡았습니다: {otherPlayer.gameObject.name}");
    }

    private void GrabObject(IGrabbable grabbable)
    {
        holdingObject = grabbable.GameObj;
        isHolding = true;
        holdingTargetId = grabbable.NetId;

        // NEW: GrabbableObject에 잡혔음을 알림 (NetworkTransform 최적화)
        grabbable.OnGrabbed(this);

        // 레이어 저장 및 비활성화 (충돌 무시용)
        heldObjectOriginLayer = grabbable.GameObj.layer;
        grabbable.GameObj.layer = LayerMask.NameToLayer("HeldObject");

        //Debug.Log($"[잡기] 오브젝트 레이어 변환: {grabbable.gameObject.layer}");
        //Debug.Log($"[잡기] 오브젝트를 잡았습니다: {grabbable.name}");
    }

    private void TryThrow()
    {
        // 잡은게 없으면 입력 무시
        if (holdingObject == null)
        {
            Debug.Log($"[잡기] 잡은 오브젝트가 없는데 잡기 중!!");
            return;
        }

        // 던지기 방향 계산 (앞쪽 + 약간 위)
        Vector3 throwDirection = (transform.forward + Vector3.up * 0.5f).normalized;

        // 플레이어를 던지는 경우
        PlayerController targetPlayer = holdingObject.GetComponent<PlayerController>();
        if (targetPlayer != null)
        {
            ThrowPlayer(targetPlayer, throwDirection);
        }

        // 오브젝트를 던지는 경우
        GrabbableObject grabbable = holdingObject.GetComponent<GrabbableObject>();
        if (grabbable != null)
        {
            ThrowObject(grabbable, throwDirection);
        }

        holdingObject = null;
        heldPlayerCache = null;  // 캐시 클리어
        isHolding = false;
        holdingTargetId = 0;
    }

    private void ThrowPlayer(PlayerController target, Vector3 throwDirection)
    {
        target.netIsGrabbed.Value = false;
        target.grabberId = 0;
        target.escapeJumpCount = 0;

        // 물리 재활성화 및 힘 가하기
        if (target.rb != null)
        {
            target.rb.isKinematic = false;
            target.rb.AddForce(throwDirection * throwForce, ForceMode.Impulse);
        }
        // 충돌 재활성화
        target.gameObject.layer = heldObjectOriginLayer;
        SetTriggerClientRpc("Throw");

        //Debug.Log($"[잡기] 오브젝트 레이어 변환: {target.gameObject.layer}");
        //Debug.Log("[잡기] 오브젝트를 던졌습니다");
    }

    private void ThrowObject(IGrabbable target, Vector3 throwDirection)
    {
        // NEW: GrabbableObject에 던져졌음을 알림 (NetworkTransform 최적화)
        target.OnThrown();
        target.Rb.AddForce(throwDirection * throwForce, ForceMode.Impulse);

        // 충돌 재활성화
        target.GameObj.layer = heldObjectOriginLayer;
        SetTriggerClientRpc("Throw");

        //Debug.Log($"[잡기] 오브젝트 레이어 변환: {target.gameObject.layer}");
        //Debug.Log("[잡기] 오브젝트를 던졌습니다");
    }

    protected void PlayerHeld()
    {
        // 머리 위 위치 계산
        Vector3 targetPosition = transform.position
            + transform.forward * holdDistance
            + Vector3.up * holdHeight;

        // 최적화: 위치가 크게 변했을 때만 업데이트 (0.01m = 1cm 이상)
        float positionDelta = Vector3.Distance(targetPosition, lastHeldObjectPosition);
        if (positionDelta >= 0.01f)
        {
            holdingObject.transform.position = targetPosition;
            lastHeldObjectPosition = targetPosition;

            // 플레이어를 들고 있는 경우 회전도 맞춤 (캐시 사용)
            if (heldPlayerCache != null)
            {
                holdingObject.transform.rotation = transform.rotation;
            }
        }
    }

    private void EscapeFromGrap()
    {
        if (grabberId == 0) return;

        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(grabberId, out NetworkObject grabberObject))
        {
            netIsGrabbed.Value = false;
            grabberId = 0;
            rb.isKinematic = false;
            return;
        }

        PlayerController grabbedBy = grabberObject.GetComponent<PlayerController>();

        // 잡고 있던 플레이어의 상태 해제
        grabbedBy.holdingObject = null;
        grabbedBy.heldPlayerCache = null;  // 캐시 클리어
        grabbedBy.isHolding = false;
        grabbedBy.holdingTargetId = 0;

        // 내 상태 해제
        netIsGrabbed.Value = false;
        grabberId = 0;
        escapeJumpCount = 0;

        // 물리 재활성화 및 점프
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.AddForce(Vector3.up * jumpForce * 1.5f, ForceMode.Impulse);
        }

        SetTriggerClientRpc("Escape");
        Debug.Log("[탈출] 성공적으로 탈출했습니다!");
    }

    public void ReleaseGrab()
    {
        // 서버에서만 실행
        if (!IsServer) return;

        // 내가 무언가를 들고 있었다면
        if (isHolding && holdingObject != null)
        {
            PlayerController heldPlayer = holdingObject.GetComponent<PlayerController>();
            if (heldPlayer != null)
            {
                heldPlayer.netIsGrabbed.Value = false;
                heldPlayer.grabberId = 0;
                if (heldPlayer.rb != null)
                {
                    heldPlayer.rb.isKinematic = false;
                }
                // 레이어 복구
                heldPlayer.gameObject.layer = heldObjectOriginLayer;
            }

            else
            {
                GrabbableObject grabbable = holdingObject.GetComponent<GrabbableObject>();
                if (grabbable != null)
                {
                    grabbable.OnReleased();

                    Rigidbody targetRb = grabbable.GetComponent<Rigidbody>();
                    if (targetRb != null)
                    {
                        targetRb.isKinematic = false;
                    }
                    // 레이어 복구
                    grabbable.gameObject.layer = heldObjectOriginLayer;
                }
            }

            holdingObject = null;
        }

        // 내가 잡혀있었다면 - 나 자신의 물리 복구
        if (netIsGrabbed.Value)
        {
            // 물리 재활성화
            if (rb != null)
            {
                rb.isKinematic = false;
            }

            // 잡고 있던 사람의 상태 업데이트
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(grabberId, out NetworkObject grabberObject))
            {
                PlayerController grabbedBy = grabberObject.GetComponent<PlayerController>();
                if (grabbedBy != null)
                {
                    grabbedBy.holdingObject = null;
                    grabbedBy.heldPlayerCache = null;
                    grabbedBy.isHolding = false;
                    grabbedBy.holdingTargetId = 0;
                }
            }
        }

        isHolding = false;
        holdingTargetId = 0;
        netIsGrabbed.Value = false;
        grabberId = 0;
        heldPlayerCache = null;
        escapeJumpCount = 0;
    }

    public void PlayerDeath()
    {
        if (netIsDeath.Value) return;

        // 죽은 위치 저장 (시체 생성용)
        // deathPosition = transform.position;

        netIsDeath.Value = true;

        // 인풋벡터 초기화
        moveDir = Vector2.zero;
        ReleaseGrab();

        SetTriggerClientRpc("Death");

        // 봇은 서버가 Owner이므로 직접 리스폰 타이머 시작
        if (this is BotController || this is ConsoleBotController)
        {
            StartCoroutine(BotRespawnDelay());
        }
    }

    // 봇 전용 리스폰 타이머 (애니메이션 길이 2.3초)
    private System.Collections.IEnumerator BotRespawnDelay()
    {
        //yield return botRespawnWait;  // GC 최적화: 캐싱된 WaitForSeconds 사용

        // 7초에서 10초 사이의 랜덤 시간 설정
        float randomRespawnTime = Random.Range(7f, 10f);
        yield return new WaitForSeconds(randomRespawnTime);
        DoRespawnTeleport();
    }

    // 시체 생성 + 텔레포트
    private void DoRespawnTeleport()
    {
        if (!IsServer) return;

        // 시체 생성 (리스폰 시점에 생성하여 자연스러움)
        if (bodyPrefab != null)
        {
            NetworkObject body = NetworkManager.Singleton.SpawnManager.InstantiateAndSpawn(
                bodyPrefab,
                position: transform.position,
                rotation: transform.rotation
            );

            // Layer 설정: DeadBody (Layer 10) - 거리 기반 컬링 적용
            SetLayerRecursively(body.gameObject, 10);
        }

        // 리스폰 리스트 가져오기
        int index = RespawnId.Value;

        var dest = respawnManager.respawnPoints[index];
        if (!dest) { Debug.LogWarning("Respawn Transform null"); return; }

        // 이동/회전 속도 초기화
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        transform.position = dest.position;
        transform.rotation = dest.rotation;

        // 리스폰 파티클 재생
        PlayRespawnParticle();

        ResetPlayerState();
    }

    // 좌표를 이용한 텔레포트
    // 순간이동에도 쓰이므로 public
    public void DoRespawn(Vector3 pos, Quaternion rot)
    {
        if (!IsServer) return;

        // 이동/회전 속도 초기화
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        Vector3 delta = pos - transform.position;

        transform.position = pos;
        transform.rotation = rot;

        ClientRpcParams clientRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { OwnerClientId }
            }
        };

        CamWarpClientRpc(delta, clientRpcParams);

        ResetPlayerState();
    }

    [ClientRpc]
    private void CamWarpClientRpc(Vector3 delta, ClientRpcParams clientRpcParams = default)
    {
        cam.OnTargetObjectWarped(this.transform, delta);
        cam.ForceCameraPosition(
            cam.transform.position + delta,
            cam.transform.rotation
        );
    }

    private void ResetPlayerState()
    {
        // 이동/점프 관련 상태 최소 초기화
        moveDir = Vector2.zero;
        isJumpQueued = false;
        netIsGrounded.Value = true;
        isDiving = false;
        isDiveGrounded = false;
        netIsDeath.Value = false;
        canDive = false;
        isHit = false;

        // 애니메이터도 각 클라에서 리셋
        ResetAnimClientRpc();
    }

    // 서버에서 입력 및 물리 상태를 강제로 초기화
    public void ForceClearInputOnServer()
    {
        if (!IsServer) return;

        moveDir = Vector2.zero;
        lastSentInput = Vector2.zero;
        isJumpQueued = false;
        isGrabQueued = false;

        // 물리 속도도 초기화하여 잔여 움직임 제거
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // 네트워크 플래그 초기화
        netIsMove.Value = false;
    }

    // 오브젝트와 자식들의 레이어를 재귀적으로 설정
    private void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }
    // 점프 파티클 재생 (서버에서 호출, 모든 클라이언트에서 재생)
    private void PlayJumpParticle()
    {
        if (jumpParticle != null)
        {
            PlayJumpParticleClientRpc();
        }
    }

    [ClientRpc]
    private void PlayJumpParticleClientRpc()
    {
        if (jumpParticle != null)
        {
            jumpParticle.Play();
        }
    }

    // 점프 사운드 로컬 재생 (Owner 전용, 즉시 재생)
    private void PlayJumpSoundLocal()
    {
        if (jumpAudioSource != null)
        {
            // 캐릭터 보이스 재생
            if (jumpVoiceClip != null)
            {
                jumpAudioSource.PlayOneShot(jumpVoiceClip, jumpVoiceVolume);
            }

            // 효과음 재생
            if (jumpEffectClip != null)
            {
                jumpAudioSource.PlayOneShot(jumpEffectClip, jumpEffectVolume);
            }
        }
    }

    // 점프 사운드 재생 (서버에서 호출, 다른 클라이언트에서 재생)
    private void PlayJumpSound()
    {
        PlayJumpSoundClientRpc();
    }

    [ClientRpc]
    private void PlayJumpSoundClientRpc()
    {
        // Owner는 이미 로컬에서 재생했으므로 스킵
        if (IsOwner) return;

        if (jumpAudioSource != null)
        {
            // 캐릭터 보이스 재생
            if (jumpVoiceClip != null)
            {
                jumpAudioSource.PlayOneShot(jumpVoiceClip, jumpVoiceVolume);
            }

            // 효과음 재생
            if (jumpEffectClip != null)
            {
                jumpAudioSource.PlayOneShot(jumpEffectClip, jumpEffectVolume);
            }
        }
    }

    // 다이브 시작 사운드 로컬 재생 (Owner 전용, 즉시 재생)
    private void PlayDiveStartSoundLocal()
    {
        if (diveAudioSource != null && diveStartClip != null)
        {
            diveAudioSource.PlayOneShot(diveStartClip, diveStartVolume);
        }
    }

    // 다이브 시작 사운드 재생 (서버에서 호출, 다른 클라이언트에서 재생)
    private void PlayDiveStartSound()
    {
        PlayDiveStartSoundClientRpc();
    }

    [ClientRpc]
    private void PlayDiveStartSoundClientRpc()
    {
        // Owner는 이미 로컬에서 재생했으므로 스킵
        if (IsOwner) return;

        if (diveAudioSource != null && diveStartClip != null)
        {
            diveAudioSource.PlayOneShot(diveStartClip, diveStartVolume);
        }
    }

    // 다이브 착지 사운드 재생 (서버에서 호출, 모든 클라이언트에서 재생)
    private void PlayDiveLandSound()
    {
        PlayDiveLandSoundClientRpc();
    }

    [ClientRpc]
    private void PlayDiveLandSoundClientRpc()
    {
        if (diveAudioSource != null)
        {
            // 캐릭터 보이스 재생
            if (diveLandVoiceClip != null)
            {
                diveAudioSource.PlayOneShot(diveLandVoiceClip, diveLandVoiceVolume);
            }

            // 바닥 충돌음 재생
            if (diveLandImpactClip != null)
            {
                diveAudioSource.PlayOneShot(diveLandImpactClip, diveLandImpactVolume);
            }
        }
    }

    // 다이브 착지 파티클 재생 (서버에서 호출, 모든 클라이언트에서 재생)
    private void PlayDiveLandParticle()
    {
        if (diveLandParticle != null)
        {
            PlayDiveLandParticleClientRpc();
        }
    }

    [ClientRpc]
    private void PlayDiveLandParticleClientRpc()
    {
        if (diveLandParticle != null)
        {
            diveLandParticle.Play();
        }
    }

    // 리스폰 파티클 재생 (서버에서 호출, 모든 클라이언트에서 재생)
    private void PlayRespawnParticle()
    {
        if (respawnParticle != null)
        {
            PlayRespawnParticleClientRpc();
        }
    }

    [ClientRpc]
    private void PlayRespawnParticleClientRpc()
    {
        if (respawnParticle != null)
        {
            respawnParticle.Play();
        }
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

    [ClientRpc]
    private void PlayBuffPickupEffectClientRpc()
    {
        // 파티클 재생
        if (buffPickupEffect != null)
        {
            buffPickupEffect.Play();
        }

        // 사운드 재생
        if (buffAudioSource != null && buffPickupClip != null)
        {
            buffAudioSource.PlayOneShot(buffPickupClip, buffPickupVolume);
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

    [ClientRpc]
    private void SetSpeedBuffEffectClientRpc(bool enabled)
    {
        ToggleLoopEffect(speedBuffLoopEffect, enabled);
        ToggleLoopSound(buffLoopClip, enabled);
    }

    [ClientRpc]
    private void SetJumpBuffEffectClientRpc(bool enabled)
    {
        ToggleLoopEffect(jumpBuffLoopEffect, enabled);
        ToggleLoopSound(buffLoopClip, enabled);
    }

    [ClientRpc]
    private void SetInvincibleBuffEffectClientRpc(bool enabled)
    {
        ToggleLoopEffect(invincibleBuffLoopEffect, enabled);
        ToggleLoopSound(invincibleBuffLoopClip, enabled);
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
        if (buffLoopAudioSource == null || clip == null) return;

        if (enabled)
        {
            // 루프 사운드 재생
            if (!buffLoopAudioSource.isPlaying || buffLoopAudioSource.clip != clip)
            {
                buffLoopAudioSource.clip = clip;
                buffLoopAudioSource.volume = buffLoopVolume;
                buffLoopAudioSource.loop = true;
                buffLoopAudioSource.Play();
            }
        }
        else
        {
            // 루프 사운드 중지
            if (buffLoopAudioSource.isPlaying && buffLoopAudioSource.clip == clip)
            {
                buffLoopAudioSource.Stop();
                buffLoopAudioSource.clip = null;
            }
        }
    }

    // 발걸음 사운드 업데이트 (로컬 클라이언트에서만 호출)
    private void UpdateFootstepSoundLocal()
    {
        if (footstepAudioSource == null || footstepClip == null) return;

        // 파티클이 재생 중일 때만 발걸음 소리 재생
        if (isWalkParticlePlaying)
        {
            footstepTimer += Time.deltaTime;

            // 타이머가 간격을 넘으면 발걸음 소리 재생
            if (footstepTimer >= footstepInterval)
            {
                footstepAudioSource.PlayOneShot(footstepClip, footstepVolume);
                footstepTimer = 0f; // 타이머 리셋
            }
        }
        else
        {
            // 걷지 않으면 타이머 리셋
            footstepTimer = 0.3f;
        }
    }
    #endregion

    // 충돌관리 로직
    #region Physics
    protected void GroundCheck()
    {
        if (!IsServer) return;

        // 프레임 스키핑: Unity 전역 프레임 카운터 사용 (모든 플레이어가 동기화됨)
        if (Time.frameCount % groundCheckInterval != 0) return;

        ServerPerformanceProfiler.Start("PlayerController.GroundCheck");
        // 캐싱된 계산 (매번 계산하지 않도록)
        float offsetDist = col.height / 2f - col.radius;
        Vector3 bottomSphereCenter = col.center + (Vector3.down * offsetDist);
        Vector3 castOrigin = transform.TransformPoint(bottomSphereCenter);
        float scale = transform.localScale.y;
        float scaledRadius = col.radius * scale * 0.95f;
        float scaledDistance = groundCheckDist * scale;

        int hitCount = Physics.SphereCastNonAlloc(
            castOrigin,
            scaledRadius,
            Vector3.down,
            groundHits,
            scaledDistance,
            groundLayerMask  // LayerMask로 필터링 (Physics 쿼리 최적화)
        );

        bool isGrounded = false;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = groundHits[i];

            // 자기자신 제외
            if (hit.collider == null || hit.collider == col) continue;
            // 경사로/벽면 제외 (0.7 = 약 45도 경사)
            if (hit.normal.y < 0.7f) continue;

            // Debug.Log($"{hit.collider.name}을 땅으로 감지!!");
            isGrounded = true;
            break;
        }

        // NetworkVariable은 값이 실제로 변경될 때만 업데이트 (Netcode 자동 처리)
        netIsGrounded.Value = isGrounded;

        // 착지 시 처리 (최적화: 조건을 미리 체크)
        if (isGrounded && rb.linearVelocity.y <= 0.1f)
        {
            if (isDiving)
            {
                OnDiveLand();
            }

            if (canDive)
            {
                canDive = false;
            }
        }
        ServerPerformanceProfiler.End("PlayerController.GroundCheck");
    }

    // 특정 물체와 충돌할 때
    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer) return;

        // Tag로 구분하여 다른 애니메이션 재생
        switch (collision.gameObject.tag)
        {
            case "Ocean":
            case "Death":
                // 캐릭터가 가지고 있는 리스폰 인덱스로 이동
                PlayerDeath();
                break;

            case "weakObstacles":
                // 충돌 지점의 평균 법선 벡터 계산
                Vector3 avgNormal = Vector3.zero;
                foreach (ContactPoint contact in collision.contacts)
                {
                    avgNormal += contact.normal;
                }
                avgNormal /= collision.contacts.Length;

                // 장애물에 부딪힘
                PlayHitAnimation("weakHit");
                BouncePlayer(avgNormal, bounceForce);
                break;

            case "StrongObstacles":
                // 가시에 부딪힘
                PlayHitAnimation("StrongHit");
                break;

            default:
                // 매칭되지 않은 Tag
                // Debug.Log($"[경고] 매칭되지 않은 Tag: {collision.gameObject.tag}");
                break;
        }
    }

    // 플레이어 튕겨나가기 함수
    private void BouncePlayer(Vector3 normal, float force)
    {
        // 현재 속도 초기화
        rb.linearVelocity = Vector3.zero;

        // 법선 방향으로 힘 가하기 (위쪽 방향 추가)
        Vector3 bounceDirection = (normal + Vector3.up * 0.3f).normalized;
        rb.AddForce(bounceDirection * force, ForceMode.Impulse);

        // Debug.Log($"[튕겨나가기] 방향: {bounceDirection}, 힘: {force}");
    }
    #endregion

    // 서버에서 클라한테 시킬 Rpc 모음
    #region clientRpcs
    [ClientRpc]
    protected void SetTriggerClientRpc(string triggerName)
    {
        if (animator != null)
        {
            animator.SetTrigger(triggerName);
        }
    }

    [ClientRpc]
    protected void ResetAnimClientRpc()
    {
        if (animator == null) return;

        animator.Rebind();                                  // 바인딩 초기화
    }
    #endregion

    // 애니메이션 로직들
    #region Animation
    // 애니메이션 재생 함수
    private void PlayHitAnimation(string triggerName)
    {
        if (isHit || animator == null)
        {
            return;
        }

        // Animator Controller에 해당 Parameter가 있는지 확인
        bool hasParameter = false;
        foreach (var param in animator.parameters)
        {
            if (param.name == triggerName && param.type == AnimatorControllerParameterType.Trigger)
            {
                hasParameter = true;
                break;
            }
        }

        if (!hasParameter)
        {
            //디버깅 animator에 해당하는 parameter가 없을 경우
            Debug.Log("현재 Animator Parameters:");
            foreach (var param in animator.parameters)
            {
                Debug.Log($"  - {param.name} ({param.type})");
            }
            return;
        }

        // 이동 차단 및 Trigger 실행
        isHit = true;

        // 타이머 시작
        hitTime = 0f;

        SetTriggerClientRpc(triggerName);
    }

    protected void UpdateAnimation()
    {
        if (animator != null)
        {
            // 이동 상태를 애니메이터에 전달
            animator.SetBool("IsMoving", netIsMove.Value);
            // 점프 상태를 애니메이터에 전달
            animator.SetBool("IsGrounded", netIsGrounded.Value);
            // 잡힌 상태를 애니메이터에 전달
            animator.SetBool("IsGrabbed", netIsGrabbed.Value);
        }

        // 파티클 제어: 땅에서 걷고 있을 때만 재생
        if (walkParticle != null)
        {
            bool shouldPlayParticle = netIsMove.Value && netIsGrounded.Value && !netIsDeath.Value && !isDiveGrounded;

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
    #endregion
}