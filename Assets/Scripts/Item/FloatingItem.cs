using UnityEngine;

// 아이템이 위아래로 둥둥 떠다니는 효과
public class Floating : MonoBehaviour
{
    [Header("Float Settings")]
    [SerializeField] private float floatAmplitude = 0.1f;   // 위아래로 움직이는 거리
    [SerializeField] private float floatSpeed = 1f;         // 움직이는 속도
    [SerializeField] private bool enableRotation = true;    // 회전 활성화 여부
    [SerializeField] private float rotationSpeed = 30f;     // 회전 속도

    private Vector3 startPosition;
    private float timeOffset;
    
    private void Start()
    {
        // 시작 위치 저장
        startPosition = transform.position;

        // 랜덤 오프셋 추가
        timeOffset = Random.Range(0f, 2f * Mathf.PI);
    }
    
    private void Update()
    {
        // 위아래로 둥둥 떠다니는 효과 (Sin 함수 사용)
        float yOffset = Mathf.Sin(Time.time * floatSpeed + timeOffset) * floatAmplitude;
        transform.position = startPosition + Vector3.up * yOffset;
    
        // 회전 효과 (Y축 기준)
        if (enableRotation)
        {
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
        }
    }

    // 아이템이 잡혔을 때 호출하여 효과 중지
    public void StopFloating()
    {
        enabled = false;
    }
}
