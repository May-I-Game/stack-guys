using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Linq;

public class DeletPlate : NetworkBehaviour
{
    [Header("Wall Settings")]
    [SerializeField] private GameObject wall;
    [SerializeField] private float dissolveTime = 2.0f; // ★ 디졸브 총 진행 시간 (2초 고정)

    // 디졸브 효과를 제어하는 Material 인스턴스를 저장할 변수
    private Material wallMaterial;

    [Header("Visual Materials (Triggered State)")]
    [SerializeField] private Material triggerVisualMat; // 캐릭터 접촉 시 잠깐 바뀌는 Material
    private Material originalGlassMat; // 디졸브 시작 시 사용될 원래 Material 인스턴스

    // 이 스크립트가 한 번 실행되었는지 확인하는 플래그
    private bool hasActivated = false;
    private Renderer wallRenderer; // 벽의 Renderer 컴포넌트

    // 벽 활성화 상태를 네트워크로 동기화
    private NetworkVariable<bool> isWallActive = new NetworkVariable<bool>(
        true,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // Cutoff 진행 상태를 네트워크로 동기화
    private NetworkVariable<float> wallCutoff = new NetworkVariable<float>(
        0.02f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private void Start()
    {
        // 1. Renderer 및 Material 초기화
        if (wall != null)
        {
            wallRenderer = wall.GetComponent<Renderer>();
            if (wallRenderer != null)
            {
                // 인스턴스 Material을 가져와 저장 (개별 제어 가능하도록)
                wallMaterial = wallRenderer.material;
                originalGlassMat = wallMaterial; // 디졸브 시작 시 돌아갈 원래 메테리얼 저장
            }
        }

        // 2. 초기 Cutoff 설정
        if (wallMaterial != null)
        {
            wallMaterial.SetFloat("_Hight_Cutoff", 0.02f);
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (wall != null)
        {
            // wall.SetActive(isWallActive.Value); // 서버에서 isWallActive가 false면 바로 비활성화
        }

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
        // 서버에서만 로직 실행
        if (!IsServer) return;

        // Rigidbody를 가진 캐릭터만 반응하도록 필터링
        if (other.GetComponent<Rigidbody>() != null)
        {
            // 1회성 발동
            if (hasActivated) return;

            hasActivated = true;

            // 1. 메테리얼을 즉시 변경 (triggerVisualMat로 잠깐 변경)
            ChangeMaterialClientRpc(true);

            // 2. 벽 Fade 시작
            StopCoroutine(FadeWallCoroutine());
            StartCoroutine(FadeWallCoroutine());
        }
    }

    private IEnumerator FadeWallCoroutine()
    {
        // 1. 디졸브를 시작할 원래 메테리얼로 되돌립니다.
        // 이 시점부터 wallMaterial 인스턴스에 _Hight_Cutoff 값이 적용되기 시작합니다.
        ChangeMaterialClientRpc(false);

        float startTime = Time.time;
        float endCutoff = 1.0f;
        float startCutoff = 0.02f;

        // 2. dissolveTime(2초) 동안 Cutoff 값을 선형적으로 증가시킵니다.
        while (Time.time < startTime + dissolveTime)
        {
            float elapsed = Time.time - startTime;
            float ratio = elapsed / dissolveTime;

            float currentCutoff = Mathf.Lerp(startCutoff, endCutoff, ratio);

            wallCutoff.Value = currentCutoff; // 네트워크 변수 업데이트

            yield return null;
        }

        // 3. 확실하게 끝 값을 설정 (1.0f)
        wallCutoff.Value = endCutoff;

        // 4. 완전히 투명해진 후 비활성화
        yield return new WaitForSeconds(0.5f);
        isWallActive.Value = false; // OnWallStateChanged를 통해 SetActive(false) 실행
    }

    private void OnWallStateChanged(bool oldValue, bool newValue)
    {
        // 모든 클라이언트에서 벽 활성화 상태 업데이트
        if (wall != null)
        {
            wall.SetActive(newValue);
        }
    }

    private void OnWallCutoffChanged(float oldValue, float newValue)
    {
        // 모든 클라이언트에서 Material의 Cutoff 값 업데이트
        if (wallMaterial != null)
        {
            wallMaterial.SetFloat("_Hight_Cutoff", newValue);
        }
    }

    [ClientRpc]
    void ChangeMaterialClientRpc(bool triggered)
    {
        if (wallRenderer == null) return;

        var mats = wallRenderer.materials;

        if (mats.Length > 0)
        {
            // triggered가 true면 triggerVisualMat, false면 originalGlassMat로 변경
            mats[0] = triggered ? triggerVisualMat : originalGlassMat;
            wallRenderer.materials = mats;
        }
    }
}