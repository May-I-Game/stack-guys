using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class DeletPlate : NetworkBehaviour
{
    [Header("Dissolve Settings")]
    [SerializeField] private float dissolveTime = 2.0f;

    [Header("Visual Materials")]
    [SerializeField] private Material dissolveMat; // 디졸브용 Material (원본 에셋)

    // ⭐ 오디오 변수 추가 ⭐
    [Header("Audio Settings")]
    [SerializeField] private AudioSource audioSource; // 오디오 소스 컴포넌트 (씬에서 할당 필요)
    [SerializeField] private AudioClip dissolveSound; // 재생할 사운드 클립
    [SerializeField] private float dissolveVolume = 0.7f; // 사운드 볼륨

    private Material dissolveMatInstance; // 이 오브젝트만을 위한 Material 인스턴스 (개별 제어용)
    private bool hasActivated = false;
    private Renderer selfRenderer; // 자기 자신의 Renderer

    // 이 오브젝트의 활성화 상태만 네트워크로 동기화합니다.
    private NetworkVariable<bool> isSelfActive = new NetworkVariable<bool>(
        true,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private void Start()
    {
        // 💡 1. 스크립트가 부착된 자기 자신의 Renderer를 참조합니다.
        selfRenderer = GetComponent<Renderer>();

        if (selfRenderer != null && dissolveMat != null)
        {
            // 2. 디졸브용 Material의 인스턴스를 생성 (개별 제어 확보)
            dissolveMatInstance = new Material(dissolveMat);

            // 3. 렌더러에 생성된 인스턴스를 즉시 적용 (개별 제어 보장)
            selfRenderer.material = dissolveMatInstance;

            // 4. 초기 DissolveHeight 값을 설정합니다.
            dissolveMatInstance.SetFloat("_DissolveHeight", 0.02f);
        }
        else
        {
            Debug.LogError("DeletPlate: Renderer 또는 Dissolve Material이 없습니다.");
        }

        // ⭐ AudioSource가 없으면 스크립트가 붙은 곳에서 찾기 (선택적)
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        isSelfActive.OnValueChanged += OnSelfStateChanged;
    }

    public override void OnNetworkDespawn()
    {
        isSelfActive.OnValueChanged -= OnSelfStateChanged;

        // 인스턴스 정리
        if (dissolveMatInstance != null)
        {
            Destroy(dissolveMatInstance);
        }

        base.OnNetworkDespawn();
    }

    private void OnTriggerEnter(Collider other)
    {
        // 서버에서만 트리거 처리
        if (!IsServer) return;

        if (other.GetComponent<Rigidbody>() != null)
        {
            if (hasActivated) return;
            hasActivated = true;

            // 1. 모든 클라이언트에게 디졸브 애니메이션 시작을 명령하고 사운드를 재생합니다.
            StartDissolveAndSoundClientRpc(dissolveVolume); // 볼륨 값을 전달합니다.

            // 2. 서버에서 비활성화 명령을 내릴 타이밍을 코루틴으로 제어합니다.
            StartCoroutine(FadeSelfCoroutine());
        }
    }

    // 서버에서만 실행: 애니메이션이 끝난 후 비활성화 명령을 내릴 타이밍을 제어
    private IEnumerator FadeSelfCoroutine()
    {
        yield return new WaitForSeconds(dissolveTime + 0.5f);

        if (IsServer)
        {
            isSelfActive.Value = false;
        }
    }

    // 모든 클라이언트에서 디졸브 애니메이션과 사운드를 실행
    [ClientRpc]
    private void StartDissolveAndSoundClientRpc(float volume)
    {
        // 사운드 재생 로직
        if (audioSource != null && dissolveSound != null)
        {
            audioSource.PlayOneShot(dissolveSound, volume);
        }

        // 디졸브 애니메이션 시작
        StartCoroutine(DissolveAnimationCoroutine());
    }

    // 디졸브 애니메이션 로직 (모든 클라이언트에서 실행)
    private IEnumerator DissolveAnimationCoroutine()
    {
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

            if (dissolveMatInstance != null)
            {
                dissolveMatInstance.SetFloat("_DissolveHeight", currentCutoff);
            }

            yield return null;
        }

        // 최종 값 설정 (1.0f)
        if (dissolveMatInstance != null)
        {
            dissolveMatInstance.SetFloat("_DissolveHeight", endCutoff);
        }
    }

    private void OnSelfStateChanged(bool oldValue, bool newValue)
    {
        // NetworkVariable 값이 변경되면 모든 클라이언트에서 실행: gameObject.SetActive(newValue)
        if (gameObject != null)
        {
            gameObject.SetActive(newValue);
        }
    }
}