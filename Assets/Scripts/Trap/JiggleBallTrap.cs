using Unity.Netcode;
using UnityEngine;

public class JiggleBallTrap : MonoBehaviour
{
    [Header("Rotate Settings")]
    [SerializeField] float rightZ = 60f;
    [SerializeField] float leftZ = -60f;
    [SerializeField] float rotationSpeed = 50f;
    [SerializeField] float stayTime = 0.5f;

    // 미리 계산해둘 변수들 (최적화)
    private float moveDuration;
    private float totalCycleDuration;
    private Quaternion baseRotation;

    private bool IsEditor => EditorManager.Instance;
    private bool IsGame => GameManager.Instance ? GameManager.Instance.IsGame : EditorManager.Instance.IsGame;

    private void Awake()
    {
        // 속도 0 방지
        if (rotationSpeed <= 0) rotationSpeed = 50f;

        // 1. 이동 시간 계산 (거리 / 속도)
        float angleDistance = Mathf.Abs(Mathf.DeltaAngle(rightZ, leftZ));
        moveDuration = angleDistance / rotationSpeed;

        // 2. 전체 사이클 시간 = (갈때 + 쉴때) * 2
        totalCycleDuration = (moveDuration + stayTime) * 2f;

        // 3. 에디터에 배치된 X, Y축 회전값 저장 (Z축만 스크립트로 제어하기 위함)
        baseRotation = transform.localRotation;
    }

    private void Update()
    {
        // 게임 진행 중일 때만 작동
        if (!IsGame) return;

        CalculateMovement();
    }

    private void CalculateMovement()
    {
        double currentTime = IsEditor ? Time.time : NetworkManager.Singleton.ServerTime.Time;

        // 전체 주기 안에서 현재 시간의 위치 (0 ~ totalCycleDuration)
        float cycleTime = (float)(currentTime % totalCycleDuration);

        float targetZ = rightZ; // 기본값

        // --- 4단계 동작 로직 ---

        // 1. Right -> Left 이동
        if (cycleTime < moveDuration)
        {
            float t = cycleTime / moveDuration;
            t = Mathf.SmoothStep(0f, 1f, t);
            targetZ = Mathf.Lerp(rightZ, leftZ, t);
        }
        // 2. Left 대기
        else if (cycleTime < moveDuration + stayTime)
        {
            targetZ = leftZ;
        }
        // 3. Left -> Right 이동
        else if (cycleTime < (moveDuration * 2f) + stayTime)
        {
            float progress = cycleTime - (moveDuration + stayTime);
            float t = progress / moveDuration;
            t = Mathf.SmoothStep(0f, 1f, t);
            targetZ = Mathf.Lerp(leftZ, rightZ, t);
        }
        // 4. Right 대기 (나머지 시간)
        else
        {
            targetZ = rightZ;
        }

        // 회전 적용: 기존 X,Y는 유지하고 Z만 갈아끼움
        Vector3 currentEuler = baseRotation.eulerAngles;
        transform.localRotation = Quaternion.Euler(currentEuler.x, currentEuler.y, targetZ);
    }
}