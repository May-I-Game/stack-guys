using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class DeletPlate : NetworkBehaviour
{
    [Header("Wall Settings")]
    [SerializeField] private GameObject wall;
    [SerializeField] private Material wallMaterial; // 벽의 Glass Material
    [SerializeField] private float fadeSpeed = 1f; // 사라지는 속도

    [Header("Button Visual")]
    [SerializeField] private Transform buttonTransform;
    [SerializeField] private float pressDepth = 0.1f;
    [SerializeField] private float buttonSpeed = 10f;

    private int objectsOnPlate = 0;
    private bool isPressed = false;
    private bool hasActivated = false;
    private Vector3 originalPosition;
    private Vector3 targetPosition;

    // 벽 활성화 상태를 네트워크로 동기화
    private NetworkVariable<bool> isWallActive = new NetworkVariable<bool>(
        true,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // Fade 진행 상태를 네트워크로 동기화
    private NetworkVariable<float> wallCutoff = new NetworkVariable<float>(
        0.02f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private void Start()
    {
        if (buttonTransform != null)
        {
            originalPosition = buttonTransform.localPosition;
            targetPosition = originalPosition;
        }

        // Material이 없으면 wall에서 가져오기
        if (wallMaterial == null && wall != null)
        {
            Renderer wallRenderer = wall.GetComponent<Renderer>();
            if (wallRenderer != null)
            {
                wallMaterial = wallRenderer.material;
            }
        }

        // 초기 Cutoff 설정
        if (wallMaterial != null)
        {
            wallMaterial.SetFloat("_Hight_Cutoff", 0.02f);
        }
    }

    private void Update()
    {
        // 버튼 애니메이션
        if (buttonTransform != null)
        {
            buttonTransform.localPosition = Vector3.Lerp(
                buttonTransform.localPosition,
                targetPosition,
                Time.deltaTime * buttonSpeed
            );
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // 초기 상태 적용
        if (wall != null)
        {
            wall.SetActive(isWallActive.Value);
        }

        // NetworkVariable 리스너 등록
        isWallActive.OnValueChanged += OnWallStateChanged;
        wallCutoff.OnValueChanged += OnWallCutoffChanged;
    }

    public override void OnNetworkDespawn()
    {
        isWallActive.OnValueChanged -= OnWallStateChanged;
        wallCutoff.OnValueChanged -= OnWallCutoffChanged;
        base.OnNetworkDespawn();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        if (other.GetComponent<Rigidbody>() != null)
        {
            if (hasActivated) return;

            hasActivated = true;
            isPressed = true;
            Debug.Log("버튼 눌림! (1회성)");

            // 버튼 내려가기
            if (buttonTransform != null)
            {
                targetPosition = originalPosition + Vector3.down * pressDepth;
            }

            // 벽 Fade 시작
            if (IsServer)
            {
                StartCoroutine(FadeWallCoroutine());
            }
        }
    }

    private IEnumerator FadeWallCoroutine()
    {
        float currentCutoff = 0.02f;

        // Cutoff 값을 증가시켜 투명하게
        while (currentCutoff < 1f)
        {
            currentCutoff += Time.deltaTime * fadeSpeed;
            wallCutoff.Value = currentCutoff;

            yield return null;
        }

        // 완전히 투명해진 후 비활성화
        yield return new WaitForSeconds(0.5f);
        isWallActive.Value = false;
    }

    private void OnWallStateChanged(bool oldValue, bool newValue)
    {
        Debug.Log($"[DeletPlate] 벽 상태 변경: {oldValue} -> {newValue}");

        if (wall != null)
        {
            wall.SetActive(newValue);
        }
    }

    private void OnWallCutoffChanged(float oldValue, float newValue)
    {
        // 모든 클라이언트에서 Material 업데이트
        if (wallMaterial != null)
        {
            wallMaterial.SetFloat("_Hight_Cutoff", newValue);
        }
    }
}