using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class DeletPlate : NetworkBehaviour
{
    [Header("Dissolve Settings")]
    [SerializeField] private float dissolveTime = 2.0f;

    [Header("Visual Materials")]
    [SerializeField] private Material dissolveMat; // 디졸브용 Material (원본 에셋)

    [Header("Audio Settings")]
    [SerializeField] private AudioSource audioSource; // 오디오 소스 컴포넌트 (씬에서 할당 필요)
    [SerializeField] private AudioClip dissolveSound; // 재생할 사운드 클립
    [SerializeField] private float dissolveVolume = 0.7f; // 사운드 볼륨

    private Material dissolveMatInstance;
    private bool hasActivated = false;
    private Renderer selfRenderer;

    private NetworkVariable<bool> isSelfActive = new NetworkVariable<bool>(
        true,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private void Start()
    {
        selfRenderer = GetComponent<Renderer>();

        if (selfRenderer != null && dissolveMat != null)
        {
            dissolveMatInstance = new Material(dissolveMat);
            selfRenderer.material = dissolveMatInstance;
            dissolveMatInstance.SetFloat("_DissolveHeight", 0.02f);
        }
        else
        {
            Debug.LogError("DeletPlate: Renderer 또는 Dissolve Material이 없습니다.");
        }

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

            StartDissolveAndSoundClientRpc(dissolveVolume);
            StartCoroutine(FadeSelfCoroutine());
        }
    }

    private IEnumerator FadeSelfCoroutine()
    {
        // 디졸브 시간 + 0.5초 대기 (이 타이밍은 그대로 유지하여 애니메이션이 끝난 후 비활성화합니다.)
        yield return new WaitForSeconds(dissolveTime + 0.5f);

        if (IsServer)
        {
            isSelfActive.Value = false;
        }
    }

    [ClientRpc]
    private void StartDissolveAndSoundClientRpc(float volume)
    {
        if (audioSource != null && dissolveSound != null)
        {
            audioSource.PlayOneShot(dissolveSound, volume);
        }

        StartCoroutine(DissolveAnimationCoroutine());
    }
    private IEnumerator DissolveAnimationCoroutine()
    {
        // yield return new WaitForSeconds(0.05f); ⬅️ 이 줄을 제거했습니다.

        float startTime = Time.time;
        float endCutoff = 1.0f;
        float startCutoff = 0.02f;

        // 디졸브 애니메이션은 즉시 시작됩니다.
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

        if (dissolveMatInstance != null)
        {
            dissolveMatInstance.SetFloat("_DissolveHeight", endCutoff);
        }
    }

    private void OnSelfStateChanged(bool oldValue, bool newValue)
    {
        if (gameObject != null)
        {
            gameObject.SetActive(newValue);
        }
    }
}