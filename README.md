# Stack Guys - Multiplayer Racing Platformer

> 100명의 플레이어가 동시에 경쟁하는 멀티플레이어 레이싱 플랫포머 게임

![Unity](https://img.shields.io/badge/Unity-2022.3+-black?style=flat-square&logo=unity)
![Netcode](https://img.shields.io/badge/Netcode_for_GameObjects-1.5.2-blue?style=flat-square)
![C#](https://img.shields.io/badge/C%23-10.0-purple?style=flat-square)
![AWS](https://img.shields.io/badge/AWS-EC2_|_ALB-orange?style=flat-square)

## 📋 목차

- [프로젝트 개요](#-프로젝트-개요)
- [게임 시스템](#-게임-시스템)
- [프로젝트 구조](#-프로젝트-구조)
- [성능 지표](#-성능-지표)
- [학습 및 성과](#-학습-및-성과)
- [팀 구성](#-팀-구성)

## 🎮 프로젝트 개요

Stack Guys는 **최대 100명의 플레이어**가 동시에 참여할 수 있는 대규모 멀티플레이어 레이싱 플랫포머 게임입니다. Unity Netcode for GameObjects를 기반으로 한 **서버 권한 아키텍처**와 고도로 최적화된 **배치 동기화 시스템**을 통해 대규모 플레이어 동기화를 구현했습니다.

### 개발 기간

- **팀 프로젝트** (5명)
- 개발 기간: 2025년 11월 ~ 2025년 12월 (1개월)

### 시스템 특징징

- **네트워크 시스템 설계 및 구현** (BatchNetworkManager, 관심 영역 최적화)
- **AI 봇 시스템 개발** (NavMesh 기반 경로 탐색, 동적 웨이포인트 시스템)
- **AWS 인프라 구축** (EC2 Auto Scaling, ALB, 매치메이킹 서버 연동)
- **매칭 서버** (자동 헬스 체크를 통한 매칭 로직)

---

## 🎯 게임 시스템

### 플레이어 메커니즘

#### 이동 시스템

- **걷기**: 4 m/s (프레임 독립적)
- **점프**: 물리 기반 (3 단위 힘)
- **다이빙**: 공중에서 수평 돌진 (4 수평 + 1 하강)
- **잡기**: 1.15m 범위, 머리 위 0.6m에 고정

#### 탈출 메커니즘
- 잡힌 상태에서 5번 점프하면 탈출

### 아이템 시스템
- 속도 버프 아이템: 이동 속도 20% 증가
- 점프 버프 아이템: 점프력 증가
- 무적 아이템:  무적 (잡기 면역)
- 폭탄 아이템: 잡은 후 던져서 충격파를 발생하는 아이템

### 트랩/장애물

| 트랩 이름         | 기능                          | 파일                   |
| ----------------- | ----------------------------- | ---------------------- |
| JumpPad           | 플레이어를 특정 방향으로 튕김 | `JumpPad.cs`           |
| DoubleDoorTrigger | 트리거 기반 문 개폐           | `DoubleDoorTrigger.cs` |
| RandomDoorTrigger | 랜덤하게 통과 허용/차단       | `RandomDoorTrigger.cs` |
| GoalFlag          | 결승선 (랭킹 기록)            | `GoalFlag.cs`          |


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
```

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
| 팀장, 게임 서버 개발  | 강경찬 | 서버 최적화, 게임 UI, 이펙트, SFX |
| 게임 서버 개발        | 서정   | 게임 서버, 플레이어 로직, 맵 에디터, 부하 테스트 툴 개발 |
| 게임 클라이언트 개발  | 김도훈 | 게임 클라이언트, 봇 AI, 아이템 구현 |
| 게임 클라이언트 개발  | 이정호 | 게임 클라이언트, 타일 질감, 물 커스텀 셰이더, 레벨 디자인, 타일 에디터, 게임 UI |
| DevOps               | 전석모 | 인프라 구축, 매치메이킹 서버 구현 |

---

## 📄 라이선스

본 프로젝트는 학습용으로 개발되며, 상업적 목적이 없습니다. 따라서 본 프로젝트에서는 어떠한 수익도 발생시키지 않습니다.

---

## 📧 연락처

프로젝트 관련 문의: [GitHub Issues](https://github.com/May-I-Game/stack-guys/issues)

---

**Made by May-I-Game Team**
