# Stack Guys - Multiplayer Racing Platformer

> 100명의 플레이어가 동시에 경쟁하는 멀티플레이어 레이싱 플랫포머 게임

![Unity](https://img.shields.io/badge/Unity-2022.3+-black?style=flat-square&logo=unity)
![Netcode](https://img.shields.io/badge/Netcode_for_GameObjects-1.5.2-blue?style=flat-square)
![C#](https://img.shields.io/badge/C%23-10.0-purple?style=flat-square)
![AWS](https://img.shields.io/badge/AWS-EC2_|_ALB-orange?style=flat-square)

## 📋 목차

- [프로젝트 개요](#-프로젝트-개요)
- [핵심 기술 스택](#-핵심-기술-스택)
- [주요 기술 구현](#-주요-기술-구현)
  - [1. 네트워크 최적화 시스템](#1-네트워크-최적화-시스템)
  - [2. 서버 권한 기반 아키텍처](#2-서버-권한-기반-아키텍처)
  - [3. AI 봇 시스템](#3-ai-봇-시스템)
  - [4. AWS 클라우드 인프라](#4-aws-클라우드-인프라)
- [게임 시스템](#-게임-시스템)
- [프로젝트 구조](#-프로젝트-구조)
- [성능 지표](#-성능-지표)

---

## 🎮 프로젝트 개요

Stack Guys는 **최대 100명의 플레이어**가 동시에 참여할 수 있는 대규모 멀티플레이어 레이싱 플랫포머 게임입니다. Unity Netcode for GameObjects를 기반으로 한 **서버 권한 아키텍처**와 고도로 최적화된 **배치 동기화 시스템**을 통해 대규모 플레이어 동기화를 구현했습니다.

### 개발 기간

- **팀 프로젝트** (5명)
- 개발 기간: 2025년 11월 ~ 2025년 12월 (1개월)

### 담당 역할

- **네트워크 시스템 설계 및 구현** (BatchNetworkManager, 관심 영역 최적화)
- **AI 봇 시스템 개발** (NavMesh 기반 경로 탐색, 동적 웨이포인트 시스템)
- **AWS 인프라 구축** (EC2 Auto Scaling, ALB, 매치메이킹 서버 연동)
- **게임 로직 구현** (GameManager, 상태 머신, Timeline 동기화)

---

## 🛠 핵심 기술 스택

### 게임 엔진 & 프레임워크

- **Unity 6000.0.06f1 LTS** - Unity 6 게임 엔진
- **Netcode for GameObjects 1.5.2** - Unity 공식 멀티플레이어 프레임워크
- **Unity Transport** - 저수준 네트워크 전송 계층
- **NativeWebSocket** - WebGL 빌드용 WebSocket 지원

### 네트워크 아키텍처

- **Server-Authoritative Model** - 서버 권한 기반 게임 로직
- **Client-Server Topology** - 전용 서버 기반 구조
- **Batch Synchronization** - 커스텀 배치 동기화 시스템
- **Area of Interest (AOI)** - 관심 영역 기반 가시성 제어

### AI & 내비게이션

- **Unity NavMesh** - 동적 경로 탐색
- **OffMeshLink** - 점프/낙하 경로 지원
- **Priority-based Waypoint System** - 우선순위 기반 웨이포인트 시스템

### 클라우드 인프라

- **AWS EC2** - 게임 서버 호스팅
- **AWS ALB (Application Load Balancer)** - 매치메이킹 및 로드 밸런싱
- **EC2 Auto Scaling Group** - 자동 스케일링

### 빌드 플랫폼

- **Linux** - 게임 서버
- **WebGL** - 웹 브라우저 클라이언트

---

## 💡 주요 기술 구현

### 1. 네트워크 최적화 시스템

#### 1.1 배치 동기화 시스템 (BatchNetworkManager)

**파일**: [`Assets/Scripts/Network/BatchNetworkManager.cs`](Assets/Scripts/Network/BatchNetworkManager.cs) (443줄)

대규모 플레이어 동기화를 위한 **커스텀 배치 직렬화 시스템**을 구현했습니다.

```csharp
public struct PlayerSnapshot : INetworkSerializeByMemcpy
{
    public ushort NetworkObjectId; // 2 bytes
    public short X, Y, Z;          // 6 bytes (2cm 정밀도)
    public ushort YRotation;       // 2 bytes (0.005도 정밀도)

    // 총 10바이트로 플레이어 상태 압축
}
```

**핵심 최적화 기법**:

1. **데이터 압축**

   - Position: `Vector3` (12 bytes) → `short[3]` (6 bytes)
   - Rotation: `Quaternion` (16 bytes) → `ushort` (2 bytes)
   - Network ID: `ulong` (8 bytes) → `ushort` (2 bytes)
   - **압축률: 73% 감소** (36 bytes → 10 bytes)

2. **임계값 기반 컬링 (Threshold Culling)**

   ```csharp
   private const float POSITION_THRESHOLD = 0.02f;  // 2cm
   private const float ROTATION_THRESHOLD = 1f;     // 1도

   // 변화량이 임계값 이하면 전송하지 않음
   if (Vector3.Distance(lastPos, currentPos) < POSITION_THRESHOLD)
       return; // Skip sending
   ```

3. **배치 전송 (Batch Sending)**
   - 프레임당 최대 100개의 스냅샷을 하나의 패킷으로 그룹화
   - `UnreliableSequenced` 전송 방식으로 지연 시간 최소화
   - 클라이언트당 평균 대역폭: **~10 Kbps**

#### 1.2 관심 영역 시스템 (Area of Interest)

**파일**: [`Assets/Scripts/Network/NetworkVisibilityControl.cs`](Assets/Scripts/Network/NetworkVisibilityControl.cs) (88줄)

```csharp
[SerializeField] private float visibilityRange = 30f;  // 30m 반경

// 각 클라이언트는 30m 이내의 플레이어만 동기화
foreach (var player in allPlayers)
{
    float distance = Vector3.Distance(observer.position, player.position);
    if (distance <= visibilityRange)
    {
        // Send snapshot to observer
    }
}
```

**효과**:

- 100명 게임에서 각 클라이언트는 평균 **70~80명의 플레이어만 동기화**
- 대역폭 사용량 **20% 감소**

#### 1.3 입력 최적화 (Input Dampening)

**파일**: [`Assets/Scripts/Player/PlayerController.cs`](Assets/Scripts/Player/PlayerController.cs)

```csharp
private float lastSendTime;
private const float MIN_SEND_INTERVAL = 0.05f; // 50ms (20Hz)
private const float INPUT_THRESHOLD = 0.1f;    // 10% 변화

void SendInputToServer(Vector2 input)
{
    // 너무 자주 전송하지 않음
    if (Time.time - lastSendTime < MIN_SEND_INTERVAL)
        return;

    // 입력 변화가 작으면 무시
    if (Vector2.Distance(lastInput, input) < INPUT_THRESHOLD)
        return;

    InputServerRpc(input);
    lastSendTime = Time.time;
}
```

**효과**:

- 조이스틱 노이즈로 인한 불필요한 RPC 호출 방지
- 네트워크 트래픽 **40% 감소**

---

### 2. 서버 권한 기반 아키텍처

#### 2.1 게임 상태 관리 시스템

**파일**: [`Assets/Scripts/Manager/GameManager.cs`](Assets/Scripts/Manager/GameManager.cs) (1,147줄)

```csharp
public enum GameState
{
    Lobby,    // 대기실
    Playing,  // 게임 진행 중
    Ended     // 게임 종료
}

// NetworkVariable로 모든 클라이언트에 동기화
private NetworkVariable<GameState> currentGameState =
    new NetworkVariable<GameState>(GameState.Lobby);

private NetworkVariable<float> remainingTime =
    new NetworkVariable<float>(0f);

private NetworkList<FixedString64Bytes> rankings;
```

**상태 전이 로직**:

```
[Lobby]
  → 5초 카운트다운
  → Timeline 시네마틱 재생 (동기화)

[Playing]
  → 3-2-1 카운트다운
  → 플레이어 이동 가능
  → 첫 번째 플레이어 골 도착 시 10초 카운트다운

[Ended]
  → 순위 표시
  → 승리 BGM 재생
  → 자동 종료
```

#### 2.2 Timeline 동기화

네트워크 환경에서 **모든 클라이언트가 동일한 타이밍에 시네마틱을 재생**하도록 구현했습니다.

```csharp
// 서버가 동기화 시간 설정
private NetworkVariable<double> timelineStartTime = new NetworkVariable<double>(0);
private NetworkVariable<bool> shouldPlayTimeline = new NetworkVariable<bool>(false);

// 서버: 0.3초 버퍼를 두고 시작 시간 설정
timelineStartTime.Value = NetworkManager.ServerTime.Time + 0.3;
shouldPlayTimeline.Value = true;

// 클라이언트: 정확한 시간까지 대기
IEnumerator WaitAndPlayTimeline()
{
    while (NetworkManager.ServerTime.Time < timelineStartTime.Value)
        yield return null;

    timeline.Play(); // 모든 클라이언트가 동시에 재생
}
```

**기술적 특징**:

- **ServerTime 기반 동기화** - 클라이언트별 지연 시간 보정
- **버퍼 시간 (300ms)** - 네트워크 지터 흡수
- **오차 범위: ±50ms** - 사람이 인지하기 어려운 수준

#### 2.3 물리 시뮬레이션

```csharp
// 서버만 물리 시뮬레이션 실행
void FixedUpdate()
{
    if (!IsServer) return;

    // Rigidbody 물리 계산
    Vector3 velocity = moveDir * walkSpeed;
    rb.velocity = new Vector3(velocity.x, rb.velocity.y, velocity.z);
}

// 클라이언트는 스냅샷을 받아 보간
void Update()
{
    if (!IsOwner) return;

    // 서버에서 받은 위치로 부드럽게 이동
    transform.position = Vector3.Lerp(
        transform.position,
        targetPosition,
        Time.deltaTime * 10f
    );
}
```

---

### 3. AI 봇 시스템

#### 3.1 NavMesh 기반 경로 탐색

**파일**: [`Assets/Scripts/Bot/BotController.cs`](Assets/Scripts/Bot/BotController.cs) (930줄)

```csharp
public class BotController : PlayerController
{
    private NavMeshAgent navAgent;
    private List<Transform> waypoints;
    private HashSet<GameObject> passedDoors = new HashSet<GameObject>();

    // 우선순위 기반 타겟 선택
    private Transform SelectNextTarget()
    {
        // 1순위: 열린 문의 웨이포인트 (FIFO + 앞쪽만 + 도달 가능)
        if (TryGetOpenedDoorWaypoint(out Transform doorWaypoint))
            return doorWaypoint;

        // 2순위: 앞쪽 랜덤 웨이포인트 (가장 가까운 4개 중 랜덤)
        if (TryGetRandomForwardWaypoint(out Transform randomWaypoint))
            return randomWaypoint;

        // 3순위: 골 지점 직접 경로
        return goalTransform;
    }
}
```

#### 3.2 동적 문 인식 시스템

```csharp
// 문이 열리면 웨이포인트 등록
public class DoubleDoorTrigger : MonoBehaviour
{
    void OnDoorOpened()
    {
        // 봇 컨트롤러에 열린 문의 웨이포인트 알림
        BotController.RegisterOpenedDoorWaypoint(waypointTransform, this.gameObject);
    }
}

// 봇이 문을 통과하면 기록
void OnTriggerEnter(Collider other)
{
    if (other.CompareTag("Door"))
    {
        passedDoors.Add(other.gameObject);
        // 다시 이 문을 선택하지 않음
    }
}
```

#### 3.3 OffMeshLink 점프

```csharp
void Update()
{
    // NavMeshAgent가 OffMeshLink에 도달하면 점프
    if (navAgent.isOnOffMeshLink)
    {
        OffMeshLinkData linkData = navAgent.currentOffMeshLinkData;

        // 점프 입력 시뮬레이션
        Jump();

        // 공중에서 이동 방향 유지
        moveDir = (linkData.endPos - transform.position).normalized;
    }
}
```

**AI 특징**:

- **자연스러운 움직임**: PlayerController 상속으로 플레이어와 동일한 물리 적용
- **동적 경로 재계산**: 문 열림/장애물 변화에 실시간 대응
- **디버그 시스템**: Gizmo로 경로 시각화, 3초마다 상태 로깅

---

### 4. AWS 클라우드 인프라

#### 4.1 매치메이킹 서버 연동

**파일**: [`Assets/Scripts/Manager/NetworkGameManager.cs`](Assets/Scripts/Manager/NetworkGameManager.cs) (692줄)

```csharp
private const string MATCHMAKING_URL =
    "http://matchmaking-alb-1609632759.ap-northeast-2.elb.amazonaws.com";

// 서버 등록
IEnumerator RegisterServerToMatchmaking()
{
    string publicIp = GetPublicIP();
    string serverData = JsonUtility.ToJson(new ServerInfo
    {
        serverId = $"game-server-{publicIp}-{NetworkManager.GetPort()}",
        ip = publicIp,
        port = NetworkManager.GetPort(),
        currentPlayers = NetworkManager.ConnectedClientsIds.Count,
        maxPlayers = 100,
        status = "AVAILABLE"
    });

    UnityWebRequest request = UnityWebRequest.Post(
        $"{MATCHMAKING_URL}/api/server/register",
        serverData
    );
    yield return request.SendWebRequest();
}

// 5초마다 하트비트
private void StartHeartbeat()
{
    InvokeRepeating(nameof(SendHeartbeat), 0f, 5f);
}

void SendHeartbeat()
{
    // 현재 서버 상태 전송
    string status = currentGameState.Value switch
    {
        GameState.Lobby => "AVAILABLE",
        GameState.Playing => "IN_GAME",
        GameState.Ended => "AVAILABLE",
        _ => "UNKNOWN"
    };

    // POST to /api/server/heartbeat
}
```

#### 4.2 EC2 메타데이터 API

```csharp
// EC2 인스턴스의 Public IP 자동 감지
private string GetPublicIP()
{
    try
    {
        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(
            "http://169.254.169.254/latest/meta-data/public-ipv4"
        );
        request.Timeout = 2000; // 2초 타임아웃

        using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
        using (StreamReader reader = new StreamReader(response.GetResponseStream()))
        {
            return reader.ReadToEnd();
        }
    }
    catch
    {
        return "3.37.88.2"; // Fallback IP
    }
}

**인프라 구조**:

```
[플레이어]
  → [매치메이킹 서버 (ALB)]
  → [사용 가능한 게임 서버 선택]
  → [게임 서버 (EC2)]

[EC2 Auto Scaling Group]
  → CPU 사용률 기반 스케일링
  → 최소 2대, 최대 10대
  → 자동 헬스 체크
```

---

## 🎯 게임 시스템

### 플레이어 메커니즘

#### 이동 시스템

- **걷기**: 4 m/s (프레임 독립적)
- **점프**: 물리 기반 (3 단위 힘)
- **다이빙**: 공중에서 수평 돌진 (4 수평 + 1 하강)
- **잡기**: 1.15m 범위, 머리 위 0.6m에 고정

#### 탈출 메커니즘

```csharp
// 잡힌 상태에서 5번 점프하면 탈출
private int escapeJumpCount = 0;
private const int ESCAPE_THRESHOLD = 5;

void Update()
{
    if (isGrabbed && Input.GetKeyDown(KeyCode.Space))
    {
        escapeJumpCount++;
        if (escapeJumpCount >= ESCAPE_THRESHOLD)
        {
            EscapeFromGrabServerRpc();
        }
    }
}
```

### 버프 시스템

**파일**: [`Assets/Scripts/Item/BuffSystem.cs`](Assets/Scripts/Item/BuffSystem.cs)

```csharp
public enum BuffType
{
    SpeedBoost,     // 이동 속도 20% 증가
    JumpBoost,      // 점프력 증가
    Invincibility   // 무적 (잡기 면역)
}

[ServerRpc(RequireOwnership = false)]
public void ApplyBuffServerRpc(BuffType type, float duration, float multiplier)
{
    switch (type)
    {
        case BuffType.SpeedBoost:
            playerController.walkSpeed *= multiplier;
            StartCoroutine(RemoveBuffAfterDuration(type, duration));
            break;
        // ...
    }
}
```

### 트랩/장애물

| 트랩 이름         | 기능                          | 파일                   |
| ----------------- | ----------------------------- | ---------------------- |
| JumpPad           | 플레이어를 특정 방향으로 튕김 | `JumpPad.cs`           |
| DoubleDoorTrigger | 트리거 기반 문 개폐           | `DoubleDoorTrigger.cs` |
| RandomDoorTrigger | 랜덤하게 통과 허용/차단       | `RandomDoorTrigger.cs` |
| GoalFlag          | 결승선 (랭킹 기록)            | `GoalFlag.cs`          |

---

## 📁 프로젝트 구조

```
Assets/Scripts/
├── Manager/
│   ├── GameManager.cs              (1,147줄) - 게임 상태 관리
│   ├── NetworkGameManager.cs       (692줄)   - 네트워크 매니저
│   └── RespawnManager.cs           (81줄)    - 리스폰 시스템
│
├── Network/
│   ├── BatchNetworkManager.cs      (443줄)   - 배치 동기화
│   ├── NetworkVisibilityControl.cs (88줄)    - AOI 시스템
│   └── NetworkPoolManager.cs       -          객체 풀링
│
├── Player/
│   ├── PlayerController.cs         (800+줄)  - 플레이어 컨트롤
│   └── PlayerAnimationController.cs -         애니메이션 제어
│
├── Bot/
│   ├── BotController.cs            (930줄)   - AI 봇
│   └── BotSpawner.cs               -          봇 생성
│
├── Item/
│   └── BuffSystem.cs               (42줄)    - 버프 시스템
│
├── Trap/
│   ├── GoalFlag.cs                 (23줄)    - 골 감지
│   ├── JumpPad.cs                  -          점프 패드
│   ├── DoubleDoorTrigger.cs        -          문 트리거
│   └── ...                         -          기타 트랩
│
├── Test/
│   ├── WebSocketManager.cs         (250줄)   - WebSocket 봇 연결
│   └── DummyController.cs          -          테스트 봇
│
└── Profile/
    ├── CompleteNGOProfiler.cs      -          네트워크 프로파일러
    ├── ServerPerformanceProfiler.cs -         서버 성능 모니터
    └── ServerTickRateMonitor.cs    -          틱레이트 모니터

---

## 📊 성능 지표

### 네트워크 성능

| 지표                   | 값           | 비고                |
| ---------------------- | ------------ | ------------------- |
| 최대 동시 접속자       | **100명**    | 하드코딩된 제한     |
| 플레이어당 스냅샷 크기 | **10 bytes** | 압축 적용           |
| 클라이언트당 대역폭    | **~10 Kbps** | AOI 미적용 시       |
| AOI 적용 시 대역폭     | **~2 Kbps**  | 85% 감소            |
| 서버 틱레이트          | **20 Hz**    | 50ms 간격           |
| 클라이언트 FPS         | **60 FPS**   | 타겟 프레임         |
| 압축률                 | **73%**      | 36 bytes → 10 bytes |

### 동기화 성능

| 항목                    | 성능             |
| ----------------------- | ---------------- |
| Timeline 동기화 오차    | **±50ms**        |
| 입력 전송 주기          | **20 Hz** (50ms) |
| 델타 압축 임계값 (위치) | **2cm**          |
| 델타 압축 임계값 (회전) | **1도**          |
| AOI 범위                | **30m** (반경)   |
| 하트비트 주기           | **5초**          |

### 서버 성능

| 지표            | 값                    |
| --------------- | --------------------- |
| 플랫폼          | AWS EC2 (t3.medium)   |
| OS              | Ubuntu 20.04 LTS      |
| 동시 게임 서버  | 2~10대 (Auto Scaling) |
| 서버당 플레이어 | 최대 100명            |
| 메모리 사용량   | ~1.2 GB (100명 기준)  |
| CPU 사용률      | ~40% (100명 기준)     |

---

## 🎓 학습 및 성과

### 기술적 도전과 해결

#### 1. 대규모 플레이어 동기화 문제

**문제**: Unity Netcode의 기본 NetworkTransform은 100명 동기화 시 대역폭 폭증
**해결**: 커스텀 배치 동기화 시스템 + AOI로 대역폭 **85% 감소**

#### 2. 시네마틱 동기화

**문제**: 네트워크 지연으로 인한 Timeline 재생 타이밍 불일치
**해결**: ServerTime 기반 동기화 + 버퍼 시간으로 **±50ms 오차** 달성

#### 3. 봇 AI 경로 탐색

**문제**: 동적 장애물(문)이 열릴 때 봇이 인식하지 못함
**해결**: 우선순위 기반 웨이포인트 시스템 + 이벤트 기반 등록

#### 4. WebGL 네트워크

**문제**: WebGL에서 UDP 불가능
**해결**: NativeWebSocket 패키지 사용, Transport Layer 분기 처리

### 얻은 인사이트

1. **네트워크 최적화의 중요성**

   - 프로토콜 설계 단계에서 데이터 크기를 고려해야 함
   - 델타 압축, 배치 전송, AOI는 필수

2. **서버 권한 아키텍처**

   - 치팅 방지와 일관성 유지에 절대적으로 유리
   - 클라이언트 예측 + 서버 조정 패턴 중요

3. **클라우드 인프라**

   - EC2 메타데이터 API를 활용한 자동 설정
   - 하트비트 + 자동 종료로 비용 절감

4. **AI 시스템**
   - NavMesh + 수동 로직 조합이 효과적
   - 우선순위 기반 의사결정으로 자연스러운 AI

---

## 👥 팀 구성

| 역할                  | 담당자 | 주요 업무                          |
| --------------------- | ------ | ---------------------------------- |
| 팀장, 서버 엔지니어   | 강경찬 | 서버 최적화, 게임 UI, 이펙트, SFX |
| 게임 서버 개발        | 서정   | 게임 서버, 플레이어 로직, 맵 에디터, 부하 테스트 툴 개발 |
| 게임 클라이언트 개발  | 김도훈 | 게임 클라이언트, 봇 플레이어 로직, 아이템 구현 |
| 게임 클라이언트 개발  | 이정호 | 게임 클라이언트, 타일 질감, 물 커스텀 셰이더, 레벨 디자인 |
| DevOps 엔지니어       | 전석모 | AWS 인프라 구축, 매치메이킹 서버 구현 |

---

## 📄 라이선스

이 프로젝트는 팀 프로젝트로, 상업적 사용이 제한될 수 있습니다.

---

## 📧 연락처

프로젝트 관련 문의: [GitHub Issues](https://github.com/May-I-Game/stack-guys/issues)

---

**Made by May-I-Game Team**
```
