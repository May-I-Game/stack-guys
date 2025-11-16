using Unity.Netcode;
using UnityEngine;

public class NetworkSpinner : NetworkBehaviour
{
    public enum Axis { X, Y, Z }
    public enum SpaceMode { Local, World }

    [Header("Spin Settings")]
    [SerializeField] private Axis axis = Axis.Y;
    [SerializeField] private float degreesPerSecond = 180f;
    [SerializeField] private SpaceMode spaceMode = SpaceMode.Local;
    [SerializeField] private bool clockWise = true;
    [SerializeField] private bool randomizeStartAngle = false;

    // 초기 각도는 서버에서 결정
    private readonly NetworkVariable<float> netStartAngle = new NetworkVariable<float>();

    private Vector3 axisVector;
    private Quaternion initialRotation;

    private void Awake()
    {
        // 축 벡터 미리 계산
        axisVector = axis switch
        {
            Axis.X => Vector3.right,
            Axis.Y => Vector3.up,
            Axis.Z => Vector3.forward,
            _ => Vector3.up
        };

        initialRotation = (spaceMode == SpaceMode.Local) ? transform.localRotation : transform.rotation;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // 서버에서만 초기 각도 설정 (랜덤 등)
            if (randomizeStartAngle)
            {
                float startAngle = Random.Range(0f, 360f);
                netStartAngle.Value = startAngle;
            }
            else
            {
                netStartAngle.Value = 0f;
            }
        }
    }

    private void Update()
    {
        // 게임 매니저 조건 체크 (원래 코드 유지)
        if (!(GameManager.instance && GameManager.instance.IsGame)) return;

        CalculateRotation();
    }

    private void CalculateRotation()
    {
        // Time.deltaTime을 더하는게 아니라, "현재 서버 시간"을 기반으로 절대각을 계산함
        // 모든 클라이언트는 NetworkManager를 통해 동기화된 ServerTime을 사용하여 동일한 결과를 얻음
        double time = NetworkManager.Singleton.ServerTime.Time;

        float dir = clockWise ? -1f : 1f;

        // 공식: (속도 * 시간) + 초기각도
        // 모듈로 연산(% 360)을 통해 숫자가 무한히 커지는 것 방지
        float currentAngle = (float)((time * degreesPerSecond * dir) % 360.0f) + netStartAngle.Value;

        // 회전 적용
        ApplyRotation(currentAngle);
    }

    private void ApplyRotation(float angle)
    {
        // 쿼터니온 곱셈은 순서중요함!!!!
        Quaternion spinRotation = Quaternion.AngleAxis(angle, axisVector);

        if (spaceMode == SpaceMode.Local)
        {
            // 쿼터니언을 사용하여 축에 맞는 회전 생성
            transform.localRotation = initialRotation * spinRotation;
        }
        else
        {
            transform.rotation = initialRotation * spinRotation;
        }
    }
}
