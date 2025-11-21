using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class DeletPlate : NetworkBehaviour
{
    [Header("Wall Settings")]
    [SerializeField] private GameObject wall;
    [SerializeField] private float dissolveTime = 2.0f;

    [Header("Visual Materials")]
    [SerializeField] private Material triggerVisualMat; // 캐릭터 접촉 시 잠깐 바뀌는 Material
    [SerializeField] private Material dissolveMat; // 디졸브용 Material (원본을 여기에 할당)

    private Material dissolveMatInstance; // 디졸브용 Material의 인스턴스
    private bool hasActivated = false;
    private Renderer wallRenderer;

    private NetworkVariable<bool> isWallActive = new NetworkVariable<bool>(
        true,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<float> wallCutoff = new NetworkVariable<float>(
        0.02f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private void Start()
    {
        if (wall != null)
        {
            wallRenderer = wall.GetComponent<Renderer>();

            if (wallRenderer != null && dissolveMat != null)
            {
                // 디졸브용 Material의 인스턴스를 미리 생성
                dissolveMatInstance = new Material(dissolveMat);
                dissolveMatInstance.SetFloat("_Hight_Cutoff", 0.02f);
            }
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        isWallActive.OnValueChanged += OnWallStateChanged;
        wallCutoff.OnValueChanged += OnWallCutoffChanged;
    }

    public override void OnNetworkDespawn()
    {
        isWallActive.OnValueChanged -= OnWallStateChanged;
        wallCutoff.OnValueChanged -= OnWallCutoffChanged;

        // 인스턴스 정리
        if (dissolveMatInstance != null)
        {
            Destroy(dissolveMatInstance);
        }

        base.OnNetworkDespawn();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        if (other.GetComponent<Rigidbody>() != null)
        {
            if (hasActivated) return;
            hasActivated = true;

            // 1. 메테리얼을 triggerVisualMat로 변경
            ChangeMaterialClientRpc(true);

            // 2. 짧은 딜레이 후 디졸브 시작 (triggerVisualMat을 잠깐 보여주기 위해)
            StartCoroutine(FadeWallCoroutine());
        }
    }

    private IEnumerator FadeWallCoroutine()
    {
        // triggerVisualMat를 잠깐 보여줌 (0.2초 정도)
        yield return new WaitForSeconds(0.2f);

        // 디졸브용 Material로 변경
        ChangeMaterialClientRpc(false);

        // 잠깐 대기 (Material 교체가 완료되도록)
        yield return new WaitForSeconds(0.05f);

        float startTime = Time.time;
        float endCutoff = 1.0f;
        float startCutoff = 0.02f;

        // dissolveTime 동안 Cutoff 값 증가
        while (Time.time < startTime + dissolveTime)
        {
            float elapsed = Time.time - startTime;
            float ratio = elapsed / dissolveTime;
            float currentCutoff = Mathf.Lerp(startCutoff, endCutoff, ratio);

            wallCutoff.Value = currentCutoff;

            yield return null;
        }

        wallCutoff.Value = endCutoff;

        // 완전히 투명해진 후 비활성화
        yield return new WaitForSeconds(0.5f);
        isWallActive.Value = false;
    }

    private void OnWallStateChanged(bool oldValue, bool newValue)
    {
        if (wall != null)
        {
            wall.SetActive(newValue);
        }
    }

    private void OnWallCutoffChanged(float oldValue, float newValue)
    {
        // dissolveMatInstance에 Cutoff 값 적용
        if (dissolveMatInstance != null)
        {
            dissolveMatInstance.SetFloat("_Hight_Cutoff", newValue);
        }
    }

    [ClientRpc]
    void ChangeMaterialClientRpc(bool triggered)
    {
        if (wallRenderer == null) return;

        if (triggered)
        {
            // triggerVisualMat로 변경
            if (triggerVisualMat != null)
            {
                wallRenderer.material = triggerVisualMat;
            }
        }
        else
        {
            // 디졸브용 Material 인스턴스로 변경
            if (dissolveMatInstance != null)
            {
                wallRenderer.material = dissolveMatInstance;
            }
        }
    }
}