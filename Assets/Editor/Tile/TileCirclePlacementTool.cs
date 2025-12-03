using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class TileCirclePlacementTool : EditorWindow
{
    private float radius = 5f;
    private float angleRange = 180f;
    private int count = 5;
    private bool faceCenter = true;
    private bool previewMode = true;

    // 축 선택 및 방향 전환
    private enum Axis { X, Y, Z }
    private Axis selectedAxis = Axis.Y;
    private bool invertDirection = false;

    // 클릭 지점 모드
    private enum ClickMode { Center, EdgePoint }
    private ClickMode clickMode = ClickMode.Center;

    // Prefab 사용 여부 (기본 ON 고정)
    private bool usePrefab = true;

    // 클릭 모드: true = 위치 선택, false = 오브젝트 선택
    // 더 이상 필요없음 - 제거됨

    private List<Vector3> previewPositions = new List<Vector3>();

    [MenuItem("Tools/Tile/Circle Placement Tool")]
    public static void ShowWindow()
    {
        GetWindow<TileCirclePlacementTool>("Circle Placement");
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    private void OnGUI()
    {
        GUILayout.Label("Tile Circle Placement Tool", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        radius = EditorGUILayout.FloatField("Radius", radius);
        angleRange = EditorGUILayout.Slider("Angle Range (°)", angleRange, 0f, 360f);
        count = EditorGUILayout.IntSlider("Count", count, 1, 100);
        faceCenter = EditorGUILayout.Toggle("Face Center", faceCenter);
        previewMode = EditorGUILayout.Toggle("Show Preview", previewMode);

        EditorGUILayout.Space();
        GUILayout.Label("Axis Settings", EditorStyles.boldLabel);

        selectedAxis = (Axis)EditorGUILayout.EnumPopup("Rotation Axis", selectedAxis);
        invertDirection = EditorGUILayout.Toggle("Invert Direction", invertDirection);

        EditorGUILayout.Space();
        GUILayout.Label("Click Mode", EditorStyles.boldLabel);

        clickMode = (ClickMode)EditorGUILayout.EnumPopup("Click Point Mode", clickMode);

        string modeDescription = clickMode == ClickMode.Center
            ? "선택한 프리팹 위치가 원의 중심이 됩니다"
            : "선택한 프리팹 위치가 원 둘레의 시작점이 됩니다";
        EditorGUILayout.HelpBox(modeDescription, MessageType.None);

        EditorGUILayout.Space();

        if (GUILayout.Button("Apply Placement", GUILayout.Height(30)))
        {
            ApplyPlacement();
        }

        EditorGUILayout.HelpBox(
            "1️⃣ Scene에서 Prefab 선택\n" +
            "2️⃣ Click Point Mode, 축, Radius 등 조정\n" +
            "3️⃣ Apply Placement 버튼 클릭",
            MessageType.Info);
    }

    private Vector3 clickPoint;
    private bool hasClickPoint = false;

    private void OnSceneGUI(SceneView sceneView)
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null) return;

        Vector3 objectPosition = selected.transform.position;

        // 실제 중심점 계산
        Vector3 centerPoint = GetActualCenterPoint(objectPosition);

        // 원 시각화
        Handles.color = Color.yellow;
        Vector3 normal = GetAxisVector(selectedAxis);
        Handles.DrawWireDisc(centerPoint, normal, radius);

        // 선택한 오브젝트 위치 표시
        Handles.color = clickMode == ClickMode.Center ? Color.cyan : Color.magenta;
        Handles.DrawSolidDisc(objectPosition, normal, 0.2f);
        Handles.Label(objectPosition + Vector3.up * 0.5f,
            clickMode == ClickMode.Center ? "Center" : "Start Point");

        // 프리뷰 계산
        UpdatePreviewPositions(objectPosition);

        if (previewMode)
        {
            Handles.color = new Color(0f, 1f, 0f, 0.25f);
            foreach (var pos in previewPositions)
                Handles.DrawWireCube(pos, Vector3.one * 0.5f);
        }

        SceneView.RepaintAll();
    }

    private Vector3 GetActualCenterPoint(Vector3 objectPosition)
    {
        if (clickMode == ClickMode.Center)
        {
            return objectPosition;
        }
        else // EdgePoint
        {
            // 0도 위치의 offset을 구해서 중심 계산
            Vector3 offset = GetOffsetVector(0f);

            // 오브젝트 위치에서 offset의 반대 방향으로 이동하면 중심
            return objectPosition - offset;
        }
    }

    private Vector3 GetAxisVector(Axis axis)
    {
        switch (axis)
        {
            case Axis.X: return invertDirection ? Vector3.left : Vector3.right;
            case Axis.Y: return invertDirection ? Vector3.down : Vector3.up;
            case Axis.Z: return invertDirection ? Vector3.back : Vector3.forward;
            default: return Vector3.up;
        }
    }

    private Vector3 GetOffsetVector(float angle)
    {
        // 기준점이 항상 아래(270도)에 오도록 -90도 오프셋 추가
        float adjustedAngle = angle - 90f;
        float angleRad = adjustedAngle * Mathf.Deg2Rad;
        float direction = invertDirection ? -1f : 1f;

        switch (selectedAxis)
        {
            case Axis.X:
                // X축 회전: YZ 평면에서 원
                return new Vector3(0,
                    Mathf.Cos(angleRad) * radius,
                    Mathf.Sin(angleRad) * radius * direction);

            case Axis.Y:
                // Y축 회전: XZ 평면에서 원 (수평)
                return new Vector3(
                    Mathf.Cos(angleRad) * radius,
                    0,
                    Mathf.Sin(angleRad) * radius * direction);

            case Axis.Z:
                // Z축 회전: XY 평면에서 원 (Z축은 고정, XY만 변함)
                return new Vector3(
                    Mathf.Cos(angleRad) * radius,
                    Mathf.Sin(angleRad) * radius * direction,
                    0);

            default:
                return Vector3.forward * radius;
        }
    }

    private void UpdatePreviewPositions(Vector3 objectPosition)
    {
        previewPositions.Clear();
        if (count <= 0) return;

        Vector3 centerPoint = GetActualCenterPoint(objectPosition);

        // 1번째부터 시작 (0번째는 기존 오브젝트 위치라서 제외)
        float startAngle = 0f;

        // 360도일 때는 마지막이 처음과 겹치지 않도록 count개로 나눔
        float step = (angleRange >= 360f) ? angleRange / count :
                     (count > 1) ? angleRange / (count - 1) : 0f;

        for (int i = 1; i < count; i++) // i = 1부터 시작
        {
            float angle = startAngle + step * i;
            Vector3 offset = GetOffsetVector(angle);
            Vector3 pos = centerPoint + offset;
            previewPositions.Add(pos);
        }
    }

    private Quaternion GetRotationForPosition(GameObject originalObject, float angleOffset)
    {
        // 원본 오브젝트의 회전값 가져오기
        Quaternion originalRotation = originalObject.transform.rotation;

        if (!faceCenter)
        {
            return originalRotation;
        }

        // 기준 프리팹의 회전에서 angleOffset만큼 추가 회전
        float direction = invertDirection ? -1f : 1f;
        Quaternion additionalRotation = Quaternion.identity;

        switch (selectedAxis)
        {
            case Axis.X:
                additionalRotation = Quaternion.Euler(angleOffset * direction, 0, 0);
                break;

            case Axis.Y:
                additionalRotation = Quaternion.Euler(0, angleOffset * direction, 0);
                break;

            case Axis.Z:
                additionalRotation = Quaternion.Euler(0, 0, angleOffset * direction);
                break;
        }

        // 원본 회전에 추가 회전 적용
        return additionalRotation * originalRotation;
    }

    private void ApplyPlacement()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            Debug.LogWarning("⚠️ Scene에서 오브젝트를 선택해주세요!");
            return;
        }

        Vector3 objectPosition = selected.transform.position;
        Vector3 centerPoint = GetActualCenterPoint(objectPosition);

        // 프리뷰 위치 업데이트
        UpdatePreviewPositions(objectPosition);

        Undo.IncrementCurrentGroup();

        // 첫 번째 프리팹은 0도 오프셋 (기준), 나머지는 각도를 더해감
        float step = (angleRange >= 360f) ? angleRange / count :
                     (count > 1) ? angleRange / (count - 1) : 0f;

        if (!usePrefab)
        {
            // 일반 모드: 선택한 오브젝트는 그대로 두고 나머지만 복제
            if (previewPositions.Count > 0)
            {
                // 기존 오브젝트는 그대로 유지
                for (int i = 0; i < previewPositions.Count; i++)
                {
                    Vector3 pos = previewPositions[i];
                    GameObject newObj = Instantiate(selected);
                    newObj.transform.position = pos;

                    // (i+1)번째 오브젝트는 step * (i+1) 만큼 회전
                    float angleOffset = step * (i + 1);
                    newObj.transform.rotation = GetRotationForPosition(selected, angleOffset);

                    Undo.RegisterCreatedObjectUndo(newObj, "Place Tile Circle");
                }

                Debug.Log($"✅ [Object Mode] Created {previewPositions.Count} objects (기존 오브젝트 유지)");
            }
        }
        else
        {
            // Prefab 모드: 원본 Prefab으로부터 생성
            GameObject prefabSource = PrefabUtility.GetCorrespondingObjectFromSource(selected);
            if (prefabSource == null)
            {
                Debug.LogWarning($"⚠️ 선택한 오브젝트 '{selected.name}'가 Prefab이 아닙니다! Use Prefab 옵션을 끄거나 Prefab을 선택하세요.");
                return;
            }

            // 기존 오브젝트는 그대로 유지하고 나머지만 생성
            for (int i = 0; i < previewPositions.Count; i++)
            {
                Vector3 pos = previewPositions[i];
                GameObject newObj = (GameObject)PrefabUtility.InstantiatePrefab(prefabSource);
                newObj.transform.position = pos;

                // (i+1)번째 오브젝트는 step * (i+1) 만큼 회전
                float angleOffset = step * (i + 1);
                newObj.transform.rotation = GetRotationForPosition(selected, angleOffset);

                Undo.RegisterCreatedObjectUndo(newObj, "Place Tile Circle");
            }

            Debug.Log($"✅ [Prefab Mode] Created {previewPositions.Count} prefabs from: {prefabSource.name} (기존 오브젝트 유지)");
        }
    }
}