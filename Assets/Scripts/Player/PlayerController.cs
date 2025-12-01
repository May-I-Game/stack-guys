using Unity.Cinemachine;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlayerController : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] protected float walkSpeed = 4f;
    [SerializeField] protected float rotationSpeed = 10f;

    [Header("Jump Settings")]
    [SerializeField] private float jumpForce = 3f;
    [SerializeField] private float diveForce = 4f; // 다이브할 때 앞으로 가는 힘
    [SerializeField] private float diveDownForce = 1f; // 다이브할 때 아래로 가는 힘

    [Header("Grab Settings")]
    [SerializeField] private float grabRange = 1.15f; // 잡기 범위
    [SerializeField] private float holdHeight = 0.6f; // 머리 위 높이
    [SerializeField] private float holdDistance = 0.1f; // 플레이어 앞쪽 거리
    [SerializeField] private float throwForce = 5f; // 던지기 힘
    [SerializeField] private int escapeRequiredJumps = 5; // 탈출에 필요한 점프 횟수
    private Vector3 grabbedColliderCenter;  // 잡은 콜라이더의 월드 센터
    private Vector3 grabbedColliderSize;    // 잡은 콜라이더의 크기
    private Collider[] grabColliders = new Collider[10]; // GC 최적화: 사전 할당

    [Header("Collision")]
    [SerializeField] private float groundCheckDist = 0.1f;
    [SerializeField] private LayerMask groundLayerMask = -1; // 땅으로 인식할 레이어 (최적화용, -1 = 모든 레이어)
    [SerializeField] private float bounceForce = 5f; // 튕겨나가는 힘
    private RaycastHit[] groundHits = new RaycastHit[3];

    [Header("Network Optimization")]
    [Tooltip("이동 속도 동기화 임계값. 이 값 이상 변할 때만 동기화. 권장: 0.5")]
    [SerializeField] private float speedThreshold = 0.5f;  // 0.5 m/s 이상 변화만
    [Tooltip("땅 체크 간격 (프레임). 1=매프레임, 2=2프레임마다. 권장: 2")]
    [SerializeField] protected int groundCheckInterval = 2;  // 2프레임마다 체크 (50Hz → 25Hz)
    [Tooltip("보간 속도. 값이 클수록 빠르게 보간됨. 권장: 10~20")]
    [SerializeField] private float lerpSpeed = 15f;
    [Tooltip("보간되는 시간. 값이 작을수록 반응이 빠름. 권장: 0.1")]
    [SerializeField] private float smoothTime = 0.1f;

    [Header("Components")]
    protected Rigidbody rb;
    private CapsuleCollider col;
    private Animator animator;
    private PlayerInputHandler inputHandler;
    protected PlayerBuffManager buffManager;
    private PlayerCanvasManager canvasManager;
    private PlayerEffectManager effectManager;
    private CinemachineCamera cam;
    protected RespawnManager respawnManager;     // 리스폰 리스트를 사용하기 위하여 선언

    [Header("Runtime variable")]
    protected Vector2 moveDir = Vector2.zero;
    private Vector3 lastHeldObjectPosition = Vector3.zero;  // 마지막 잡은 오브젝트 위치
    protected bool isJumpQueued;
    protected bool isGrabQueued;
    private Vector3 deathPosition;  // 죽은 위치 저장용
    private int respawnId = 0;

    [SerializeField] private NetworkObject bodyPrefab;

    protected NetworkVariable<bool> netIsMove = new NetworkVariable<bool>(false); // 움직이는중인지
    protected NetworkVariable<bool> netIsGrounded = new NetworkVariable<bool>(true); // 땅인지
    protected bool isDiving = false; // 공중 다이브 중인지
    protected bool isDiveGrounded = false; // 다이브 착지 상태 (이동 불가)
    protected NetworkVariable<bool> netIsDeath = new NetworkVariable<bool>(false); // 죽었는지
    protected bool isHit = false; // 충돌 상태 (이동 불가)
    protected bool canDive = false; // 다이브 가능 상태 (점프 중)
    protected float diveGroundedTime = 0f; // 다이브 착지 타이머 (안전장치)

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

    private NetworkVariable<FixedString32Bytes> playerName = new NetworkVariable<FixedString32Bytes>(
        "",
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    // 시네마틱 동기화를 위한 사용자 입력 무시 변수
    protected NetworkVariable<bool> inputEnabled =
        new NetworkVariable<bool>(true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // GC 최적화: WaitForSeconds 캐싱
    private WaitForSeconds botRespawnWait;
    // hit 타이머 변수 (관심 영역 밖 봇을 위한 타이머)
    protected float hitTime = 0f;
    protected float hitDuration = 1.5f;                           // 애니메이션 길이보다 약간 길게

    #region property
    // 버프 적용 배율 (봇이면 1로 처리)
    public float SpeedMul => buffManager != null ? buffManager.SpeedMultiplier : 1f;
    public float JumpMul => buffManager != null ? buffManager.JumpMultiplier : 1f;

    public bool InputEnabled
    {
        get => inputEnabled.Value;
        set
        {
            if (!IsServer) return;
            inputEnabled.Value = value;
        }
    }

    public int RespawnId
    {
        get => RespawnId;
        set
        {
            if (!IsServer) return;
            RespawnId = value;
        }
    }
    #endregion

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

        if (IsServer)
        {
            // 잡기 해제하고 가기
            ReleaseGrab();
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
        canvasManager = GetComponent<PlayerCanvasManager>();
        effectManager = GetComponent<PlayerEffectManager>();

        // GC 최적화: WaitForSeconds 사전 생성
        botRespawnWait = new WaitForSeconds(2.267f);

        // Animator가 설정되지 않았다면 자동으로 찾기
        animator = animator != null ? animator : GetComponent<Animator>();
        animator = animator != null ? animator : GetComponentInChildren<Animator>();
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

        // 입력 핸들러에서 입력을 보낼지 결정
        if (inputHandler.ShouldSendInput(out Vector2 moveInput))
        {
            MovePlayerServerRpc(moveInput);
        }

        // 점프 입력
        if (inputHandler.JumpInput)
        {
            // 점프 사운드 즉시 로컬 재생 (지연 방지)
            effectManager.PlayJumpSoundLocal();

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
        effectManager.UpdateFootstepSoundLocal();
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

        // 다이브 착지 타이머 체크 (안전장치: 애니메이션 이벤트가 호출되지 않을 경우 대비)
        if (isDiveGrounded)
        {
            diveGroundedTime += Time.fixedDeltaTime;
            if (diveGroundedTime >= 1.5f) // 1.5초 후 자동 해제
            {
                Debug.LogWarning("[다이브 착지] 타이머 초과로 자동 해제");
                isDiveGrounded = false;
                diveGroundedTime = 0f;
            }
        }

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
            Vector3 delta = _targetPos - transform.position;

            transform.position = _targetPos;
            transform.rotation = Quaternion.Euler(0, _targetRotY, 0);
            _currentVelocity = Vector3.zero;

            if (IsOwner)
            {
                cam.OnTargetObjectWarped(this.transform, delta);
            }

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
        if (IsServer)
        {
            inputEnabled.Value = enabled;
        }
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

        moveDir = direction;
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

        // 충돌 중이거나 다이브 착지 중이거나 잡힌 상태면 입력 무시
        if (isHit || isDiveGrounded || netIsGrabbed.Value)
        {
            return;
        }

        // 무언가를 들고 있으면 공중에서도 던지기 허용
        if (isHolding)
        {
            isGrabQueued = true;
            return;
        }

        // 잡기는 땅에 있을 때만 가능
        if (!netIsGrounded.Value)
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
        DoRespawn();
    }

    // 끼임 탈출용 리스폰 요청 (UIManager에서 호출)
    public void RequestEscapeRespawn()
    {
        if (!IsOwner) return;

        // ServerRpc 호출하여 현재 리스폰 지점으로 이동
        EscapeRespawnServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void EscapeRespawnServerRpc()
    {
        if (!IsServer) return;

        // 리스폰 리스트 가져오기
        int index = respawnId;

        var dest = respawnManager.respawnPoints[index];
        if (!dest) { Debug.LogWarning("Respawn Transform null"); return; }

        // DoRespawnTeleport를 사용하여 리스폰 요청
        DoRespawnTeleport(dest.position, dest.rotation);
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
        diveGroundedTime = 0f; // 타이머 리셋
    }

    [ServerRpc(RequireOwnership = false)]
    public void ResetStateServerRpc()
    {
        ResetPlayerState();
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

    [ClientRpc]
    private void ClearInputStateClientRpc()
    {
        if (inputHandler != null)
        {
            inputHandler.ResetInputState();
        }
    }

    // 도착 UI 애니메이션 표시 (본인만 표시)
    [ClientRpc]
    private void ShowArrivalUIClientRpc(int rank)
    {
        // 본인만 애니메이션 실행
        if (!IsOwner) return;

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowArrivalAnimation(rank);
            UIManager.Instance.ToggleEscapeButton(false); // 도착 시 탈출 버튼 비활성화
            Debug.Log($"[PlayerController] 도착 UI 표시 - {rank}등");
        }
        else
        {
            Debug.LogWarning("[PlayerController] UIManager.Instance가 null입니다.");
        }
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
            // 현재 각도와 차이가 클 때만 회전 적용 (약 0.5도 이상 차이날 때)
            if (Quaternion.Angle(rb.rotation, targetRotation) > 0.05f)
            {
                rb.MoveRotation(targetRotation);
            }

            netIsMove.Value = true;
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
                    rb.AddForce(Vector3.forward * 5f, ForceMode.Impulse);
                }
                else
                {
                    rb.AddForce(Vector3.up * currentJumpForce, ForceMode.Impulse);
                }
                // 점프 이펙트 재생
                effectManager.PlayJumpEffects();

                netIsGrounded.Value = false; // 점프 시 강제로 false 설정
                canDive = true; // 점프 후 다이브 가능
            }
            // 공중에 있을 때: 다이브
            else if (canDive && !isDiving && !isHolding && !netIsGrounded.Value)
            {
                PlayerDive();
            }
        }

        isJumpQueued = false;
    }

    private void PlayerDive()
    {
        // 땅에 있으면 다이브 불가
        if (netIsGrounded.Value)
        {
            Debug.Log("[다이브] 땅에 있어서 다이브 불가");
            return;
        }

        isDiving = true;
        canDive = false;

        // 현재 바라보는 방향으로 앞으로 힘 가하기
        Vector3 diveDirection = transform.forward * diveForce + Vector3.down * diveDownForce;
        rb.linearVelocity = Vector3.zero; // 기존 속도 초기화
        rb.AddForce(diveDirection, ForceMode.Impulse);

        // 다이브 시작 사운드 즉시 로컬 재생 (Owner만, 지연 방지)
        if (IsOwner)
        {
            effectManager.PlayDiveStartSoundLocal();
        }

        // 다이브 시작 사운드 재생 (다른 플레이어들을 위해)
        effectManager.PlayDiveStartSound();

        // 다이브 애니메이션 실행 (공중)
        SetTriggerClientRpc("Dive");
    }

    // 다이브 착지 처리
    private void OnDiveLand()
    {
        if (!isDiving) return;

        isDiving = false;
        isDiveGrounded = true;
        diveGroundedTime = 0f; // 타이머 시작

        // 이동 입력 초기화 (걷기 파티클 즉시 재생 방지)
        moveDir = Vector2.zero;
        netIsMove.Value = false;

        // 다이브 착지 이펙트 재생
        effectManager.PlayDiveLandEffects();

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
            if (otherPlayer != null && !otherPlayer.netIsGrabbed.Value && !otherPlayer.isHolding && !otherPlayer.netIsDeath.Value)
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
        canvasManager.ToggleArrow(false); // 화살표 끄기

        Collider targetCollider = otherPlayer.GetComponent<Collider>();
        if (targetCollider != null)
        {
            grabbedColliderCenter = targetCollider.bounds.center;
            grabbedColliderSize = targetCollider.bounds.size;
        }

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
    }

    private void GrabObject(IGrabbable grabbable)
    {
        holdingObject = grabbable.GameObj;
        isHolding = true;
        holdingTargetId = grabbable.NetId;
        canvasManager.ToggleArrow(false); // 화살표 끄기

        // 콜라이더 정보 저장
        Collider targetCollider = grabbable.GameObj.GetComponent<Collider>();
        if (targetCollider != null)
        {
            grabbedColliderCenter = targetCollider.bounds.center;
            grabbedColliderSize = targetCollider.bounds.size;
        }

        // NEW: GrabbableObject에 잡혔음을 알림 (NetworkTransform 최적화)
        grabbable.OnGrabbed(this);

        // 레이어 저장 및 비활성화 (충돌 무시용)
        heldObjectOriginLayer = grabbable.GameObj.layer;
        grabbable.GameObj.layer = LayerMask.NameToLayer("HeldObject");
    }

    private void TryThrow()
    {
        // 잡은게 없으면 입력 무시
        if (holdingObject == null)
        {
            Debug.Log($"[잡기] 잡은 오브젝트가 없는데 잡기 중!!");
            return;
        }

        // 던지기 효과음 재생
        effectManager.PlayThrowSound();

        // 던지기 방향 계산 (앞쪽 + 약간 위)
        Vector3 throwDirection = Vector3.zero;

        // 플레이어를 던지는 경우
        PlayerController targetPlayer = holdingObject.GetComponent<PlayerController>();
        if (targetPlayer != null)
        {
            throwDirection = (transform.forward + Vector3.up * 0.5f).normalized;
            ThrowPlayer(targetPlayer, throwDirection);
        }

        // 오브젝트를 던지는 경우
        GrabbableObject grabbable = holdingObject.GetComponent<GrabbableObject>();
        if (grabbable != null)
        {
            throwDirection = (transform.forward + Vector3.up * 0.2f).normalized;
            ThrowObject(grabbable, throwDirection);
        }

        holdingObject = null;
        heldPlayerCache = null;  // 캐시 클리어
        isHolding = false;
        holdingTargetId = 0;
        canvasManager.ToggleArrow(true); // 화살표 켜기

        // 던진 후 다이브 방지 (점프 상태에서 던지면 다이브 안되게)
        canDive = false;

        // 콜라이더 정보 초기화
        grabbedColliderCenter = Vector3.zero;
        grabbedColliderSize = Vector3.zero;
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

        // Rigidbody 물리 활성화
        target.Rb.WakeUp();

        // 충돌 재활성화 (원래 레이어로 복구)
        target.GameObj.layer = heldObjectOriginLayer;
        SetTriggerClientRpc("Throw");

        // 플레이어의 현재 이동 속도 계산
        Vector3 playerVelocity = Vector3.zero;
        if (moveDir.magnitude >= 0.1f)
        {
            float currentSpeed = walkSpeed * SpeedMul;
            playerVelocity = new Vector3(moveDir.x, 0, moveDir.y) * currentSpeed;
        }

        // 던지는 방향 속도 + 플레이어 이동 속도 합산
        Vector3 throwVelocity = throwDirection * throwForce + playerVelocity;

        // ForceMode.VelocityChange - mass 무시, 즉시 velocity 변경
        // 다음 물리 업데이트까지 기다림 WaitForFixedUpdate 후 적용 (안정적인 물리 적용)
        StartCoroutine(ApplyThrowForce(target.Rb, throwVelocity));
    }

    private System.Collections.IEnumerator ApplyThrowForce(Rigidbody rb, Vector3 velocity)
    {
        // 다음 FixedUpdate까지 대기
        yield return new WaitForFixedUpdate();

        if (rb != null && !rb.isKinematic)
        {
            rb.AddForce(velocity, ForceMode.VelocityChange);
        }
    }

    protected void PlayerHeld()
    {
        if (holdingObject == null) return;

        // 현재 콜라이더 정보 가져오기
        Collider currentCollider = holdingObject.GetComponent<Collider>();
        if (currentCollider == null) return;

        // 콜라이더의 하단을 기준으로 위치 계산
        float objectBottomOffset = grabbedColliderSize.y * 0.5f;

        // 목표 위치: 플레이어 머리 위 + 물체의 절반 높이만큼 위
        // 이 위치는 콜라이더 하단이 놓일 위치
        Vector3 targetColliderBottom = transform.position
            + transform.forward * holdDistance
            + Vector3.up * holdHeight;

        // 콜라이더 하단에서 센터까지의 오프셋
        Vector3 colliderCenterOffset = Vector3.up * objectBottomOffset;

        // 콜라이더 센터의 목표 위치
        Vector3 targetColliderCenter = targetColliderBottom + colliderCenterOffset;

        // 현재 콜라이더 센터
        Vector3 currentColliderCenter = currentCollider.bounds.center;

        // Transform의 위치 = 콜라이더 센터 목표 위치 + (Transform 위치 - 콜라이더 센터)
        Vector3 transformOffset = holdingObject.transform.position - currentColliderCenter;
        Vector3 finalPosition = targetColliderCenter + transformOffset;

        // 위치 업데이트 (최적화: 큰 변화가 있을 때만)
        float positionDelta = Vector3.Distance(finalPosition, lastHeldObjectPosition);
        if (positionDelta >= 0.01f)
        {
            holdingObject.transform.position = finalPosition;
            lastHeldObjectPosition = finalPosition;

            // 플레이어를 들고 있는 경우 회전도 맞춤
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

        // 콜라이더 정보 초기화
        grabbedBy.grabbedColliderCenter = Vector3.zero;
        grabbedBy.grabbedColliderSize = Vector3.zero;

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

                    // 콜라이더 정보 초기화
                    grabbedBy.grabbedColliderCenter = Vector3.zero;
                    grabbedBy.grabbedColliderSize = Vector3.zero;
                }
            }
        }

        isHolding = false;
        holdingTargetId = 0;
        netIsGrabbed.Value = false;
        grabberId = 0;
        heldPlayerCache = null;
        escapeJumpCount = 0;
        canvasManager.ToggleArrow(true); // 화살표 켜기

        // 콜라이더 정보 초기화
        grabbedColliderCenter = Vector3.zero;
        grabbedColliderSize = Vector3.zero;
    }

    public void PlayerDeath(bool isOceanDeath = false)
    {
        if (netIsDeath.Value) return;

        // 죽은 위치 저장 (시체 생성용)
        // deathPosition = transform.position;

        netIsDeath.Value = true;

        // 인풋벡터 초기화
        moveDir = Vector2.zero;
        ReleaseGrab();

        // 죽음 사운드 재생 (죽음 타입에 따라)
        effectManager.PlayDeathSound(isOceanDeath);

        SetTriggerClientRpc("Death");

        // 봇은 서버가 Owner이므로 직접 리스폰 타이머 시작
        if (this is BotController || this is ConsoleBotController)
        {
            StartCoroutine(BotRespawnDelay());
        }
    }

    // 무적 버프 아이템 끝나고 땅이 Death 태그인지 체크
    public void CheckDeathZoneOnInvincibilityEnd()
    {
        if (!IsServer || netIsDeath.Value) return;

        // 현재 위치에서 Death 태그 오브젝트와 겹치는지 체크
        Collider[] hits = Physics.OverlapSphere(transform.position, 0.5f);
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Death"))
            {
                PlayerDeath(isOceanDeath: false);
                return;
            }
        }
    }

    // 봇 전용 리스폰 타이머 (애니메이션 길이 2.3초)
    private System.Collections.IEnumerator BotRespawnDelay()
    {
        //yield return botRespawnWait;  // GC 최적화: 캐싱된 WaitForSeconds 사용

        // 10초에서 30초 사이의 랜덤 시간 설정
        //float randomRespawnTime = Random.Range(10f, 30f);
        yield return new WaitForSeconds(10f);
        DoRespawn();
    }

    // 시체 생성 + 텔레포트
    private void DoRespawn()
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
        int index = respawnId;

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

        // 리스폰 이펙트 재생
        effectManager.PlayRespawnEffects();

        ResetPlayerState();
    }

    // 좌표를 이용한 텔레포트
    // 순간이동에도 쓰이므로 public
    public void DoRespawnTeleport(Vector3 pos, Quaternion rot)
    {
        if (!IsServer) return;

        // 이동/회전 속도 초기화
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        transform.position = pos;
        transform.rotation = rot;

        ResetPlayerState();
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
        isJumpQueued = false;
        isGrabQueued = false;

        // 클라이언트 측의 입력 상태도 초기화하도록 RPC 호출
        ClearInputStateClientRpc();

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

    public void OnItemGet()
    {
        effectManager.PlayBuffPickupEffect();
    }

    public void OnGoaled(int rank)
    {
        inputEnabled.Value = false;
        ReleaseGrab();
        ForceClearInputOnServer();
        SetTriggerClientRpc("Win");

        // 본인 클라이언트에게만 도착 UI 애니메이션 표시 및 탈출 버튼 비활성화
        ShowArrivalUIClientRpc(rank);
    }

    // 시상대로 이동 시 호출 (Win 애니메이션만 재생, UI는 표시하지 않음)
    public void OnPodium()
    {
        inputEnabled.Value = false;
        ReleaseGrab();
        ForceClearInputOnServer();
        SetTriggerClientRpc("Win");
    }
    #endregion

    // 충돌관리 로직
    #region Physics
    protected void GroundCheck()
    {
        if (!IsServer) return;

        // 타임 슬라이싱: NetworkObjectId에 따라 실행 프레임을 분산시켜 부하를 1/N로 줄임
        if (Time.frameCount % groundCheckInterval != (int)NetworkObjectId % groundCheckInterval) return;

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
    }

    // 특정 물체와 충돌할 때
    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer) return;

        // Tag로 구분하여 다른 애니메이션 재생
        switch (collision.gameObject.tag)
        {
            case "Ocean":
                // 물에 빠져서 죽음
                PlayerDeath(isOceanDeath: true);
                break;

            case "Death":
                // 무적 버프 중이면 죽지 않음
                if (buffManager != null && buffManager.IsInvincible) break;
                // 일반 죽음
                PlayerDeath(isOceanDeath: false);
                break;

            case "weakObstacles":
                // 죽었으면 영향받지 않음
                if (netIsDeath.Value) break;
                // 무적 버프 중이면 피격되지 않음
                if (buffManager != null && buffManager.IsInvincible) break;
                // 피격 사운드 재생
                effectManager.PlayHitSound();
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
                // 죽었으면 영향받지 않음
                if (netIsDeath.Value) break;
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
        isDiving = false;
        isDiveGrounded = false;
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

        if (netIsMove.Value && netIsGrounded.Value && !netIsDeath.Value && !isDiveGrounded)
        {
            effectManager.PlayWalkParticle();
        }
    }
    #endregion
}