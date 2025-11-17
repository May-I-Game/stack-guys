using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.EventSystems;

public class CameraInput : MonoBehaviour
{
    private CinemachinePanTilt cam;

    [Header("Touch Region Settings")]
    [SerializeField, Range(0.05f, 1f)]
    private float topRegionPercent = 0.60f;   // 화면 상단 퍼센트 (카메라 드래그 영역)
    [SerializeField]
    private bool ignoreUIOnStart = true; // UI 위에서 드래그 시작 무시

    [Header("Drag Settings")]
    [SerializeField] private float panSensitivity = 0.1f;  // pan (좌우) 민감도
    [SerializeField] private float tiltSensitivity = 0.1f; // tilt (상하) 민감도
    [SerializeField] private bool allowTiltDrag = true;   // 상하 드래그 허용 여부

    private int cameraTouchId = -1; // 현재 카메라를 조작 중인 터치 ID
    private bool dragActive = false;
    private Vector2 prevTouchPos; // 드래그 이전 위치 저장

    private void Start()
    {
        cam = GetComponent<CinemachinePanTilt>();
    }

    private void Update()
    {
        if (cam == null) return;

        HandleInput();
    }

    private void HandleInput()
    {
        // 터치 입력 우선 처리
        if (Input.touchCount > 0)
        {
            HandleTouchDrag();
        }
        // 터치가 없으면 마우스 입력 처리 (PC/에디터)
        else
        {
            HandleMouseDrag();

            // 마우스 입력도 없으면 드래그 상태 해제
            if (!Input.GetMouseButton(0) && cameraTouchId != -1)
            {
                cameraTouchId = -1;
                dragActive = false;
            }
        }
    }

    // ==================== 터치 입력 처리 ====================
    private void HandleTouchDrag()
    {
        // 모든 터치 검사
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);

            // 터치 시작
            if (touch.phase == TouchPhase.Began)
            {
                // 드래그 시작 조건: 할당된 터치 없음 & "상단 영역" & (옵션) UI 위 아님
                if (cameraTouchId == -1 && IsTouchInValidRegion(touch.position))
                {
                    if (!ignoreUIOnStart || !IsPointerOverUIRaycast(touch.position))
                    {
                        cameraTouchId = touch.fingerId;
                        dragActive = true;
                        prevTouchPos = touch.position;
                    }
                }
            }

            // 터치 중 (터치 이동)
            else if (touch.phase == TouchPhase.Moved)
            {
                // 할당된 터치만 처리
                if (touch.fingerId == cameraTouchId && dragActive)
                {
                    Vector2 delta = touch.position - prevTouchPos;

                    // 좌우 → PanAxis
                    float newPan = cam.PanAxis.Value + (delta.x * panSensitivity);
                    cam.PanAxis.Value = WrapAngle(newPan); // Wrap 로직 적용

                    // 상하 → TiltAxis
                    if (allowTiltDrag)
                    {
                        // 위로 드래그(delta.y > 0)시 카메라가 위를 보도록 Tilt 값 감소
                        // Clamp 로직 적용 (컴포넌트의 Range 설정 사용)
                        cam.TiltAxis.Value = Mathf.Clamp(
                            cam.TiltAxis.Value - (delta.y * tiltSensitivity),
                            cam.TiltAxis.Range.x,
                            cam.TiltAxis.Range.y
                        );
                    }

                    prevTouchPos = touch.position; // 현재 위치를 이전 위치로 갱신
                }
            }

            // 터치 종료
            else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                if (touch.fingerId == cameraTouchId)
                {
                    cameraTouchId = -1;
                    dragActive = false;
                }
            }
        }
    }

    // ==================== 마우스 입력 처리 (에디터/PC) ====================
    private void HandleMouseDrag()
    {
        Vector3 mousePos = Input.mousePosition;

        // 드래그 시작 조건: 좌클릭 눌린 프레임 & "상단 영역" & (옵션) UI 위 아님
        if (Input.GetMouseButtonDown(0))
        {
            if (cameraTouchId == -1 && IsTouchInValidRegion(mousePos))
            {
                if (!ignoreUIOnStart || !IsPointerOverUIRaycast(mousePos))
                {
                    cameraTouchId = 0; // 마우스는 터치 ID 0으로 처리
                    dragActive = true;
                    prevTouchPos = mousePos; // 시작 위치 저장
                }
            }
        }

        // 드래그 중
        else if (Input.GetMouseButton(0) && cameraTouchId == 0 && dragActive)
        {
            Vector2 delta = (Vector2)mousePos - prevTouchPos;

            // 좌우 → PanAxis
            float newPan = cam.PanAxis.Value + (delta.x * panSensitivity);
            cam.PanAxis.Value = WrapAngle(newPan); // Wrap 로직 적용

            // 상하 → TiltAxis
            if (allowTiltDrag)
            {
                // 위로 드래그(delta.y > 0)시 카메라가 위를 보도록 Tilt 값 감소
                // Clamp 로직 적용 (컴포넌트의 Range 설정 사용)
                cam.TiltAxis.Value = Mathf.Clamp(
                    cam.TiltAxis.Value - (delta.y * tiltSensitivity),
                    cam.TiltAxis.Range.x,
                    cam.TiltAxis.Range.y
                );
            }

            prevTouchPos = mousePos; // 현재 위치를 이전 위치로 갱신
        }

        // 드래그 종료
        else if (Input.GetMouseButtonUp(0) && cameraTouchId == 0)
        {
            cameraTouchId = -1;
            dragActive = false;
        }
    }

    // ==================== 유효 영역 체크 ====================
    bool IsTouchInValidRegion(Vector2 screenPosition)
    {
        // 화면 하단을 제외한 나머지 영역 (조이스틱 영역과 겹치지 않음)
        float bottomThreshold = Screen.height * (1 - topRegionPercent);
        return screenPosition.y >= bottomThreshold
               && screenPosition.y <= Screen.height
               && screenPosition.x >= 0
               && screenPosition.x <= Screen.width;
    }

    // ==================== UI 체크 ====================
    bool IsPointerOverUIRaycast(Vector2 screenPosition)
    {
        if (EventSystem.current == null) return false;

        // PointerEventData를 직접 생성해서 레이캐스트
        PointerEventData eventData = new PointerEventData(EventSystem.current)
        {
            position = screenPosition
        };

        var results = new System.Collections.Generic.List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        return results.Count > 0;
    }

    // ==================== 헬퍼 함수 ====================
    private float WrapAngle(float angle)
    {
        if (angle > 180f)
        {
            angle -= 360f;
        }
        else if (angle < -180f)
        {
            angle += 360f;
        }
        return angle;
    }
}