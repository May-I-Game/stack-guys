using UnityEngine;
using System.Collections;

public class FadingPlatform : MonoBehaviour
{
    [Header("Fade Settings")]
    [SerializeField] private float fadeSpeed = 1f; // 사라지는 속도
    [SerializeField] private float destroyDelay = 0.5f; // 완전히 사라진 후 비활성화까지 대기 시간

    [Header("Material Settings")]
    [SerializeField] private Material glassMaterial; // Glass 머티리얼

    private bool isFading = false;
    private float currentCutoff = 0.02f; // 초기 Cutoff 값
    private Renderer platformRenderer;

    private void Start()
    {
        platformRenderer = GetComponent<Renderer>();

        // 머티리얼이 설정되지 않았다면 현재 머티리얼 사용
        if (glassMaterial == null)
        {
            glassMaterial = platformRenderer.material;
        }

        // 초기 Cutoff 값 설정
        glassMaterial.SetFloat("_Hight_Cutoff", currentCutoff);
    }

    private void OnCollisionEnter(Collision collision)
    {
        // 플레이어가 밟았을 때
        if (collision.gameObject.CompareTag("Player") && !isFading)
        {
            StartCoroutine(FadeOut());
        }
    }

    // 또는 Trigger를 사용하려면 이 메서드를 사용
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !isFading)
        {
            StartCoroutine(FadeOut());
        }
    }

    private IEnumerator FadeOut()
    {
        isFading = true;

        // Cutoff 값을 증가시켜 투명하게 만듦
        while (currentCutoff < 1f)
        {
            currentCutoff += Time.deltaTime * fadeSpeed;
            glassMaterial.SetFloat("_Hight_Cutoff", currentCutoff);
            yield return null;
        }

        // 완전히 투명해진 후 대기
        yield return new WaitForSeconds(destroyDelay);

        // 오브젝트 비활성화 (또는 Destroy)
        gameObject.SetActive(false);
        // 또는 완전히 삭제하려면: Destroy(gameObject);
    }

    // 발판을 다시 활성화하려면 이 메서드 호출
    public void ResetPlatform()
    {
        isFading = false;
        currentCutoff = 0.02f;
        glassMaterial.SetFloat("_Hight_Cutoff", currentCutoff);
        gameObject.SetActive(true);
    }
}