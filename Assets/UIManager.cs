using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [SerializeField] private GameObject resultPanel; // 하얀 결과 화면
    [SerializeField] private Button mainButton;

    [Header("Options Button")]
    [SerializeField] private Button optionsButton;
    [SerializeField] private Button closeOptionsButton;
    [SerializeField] private GameObject optionsPanel;   // 옵션창 (Panel)
    [SerializeField] private GameObject options;

    [Header("Guide Button")]
    [SerializeField] private Button GuideButton;
    [SerializeField] private Button closeGuideButton;
    [SerializeField] private Button closeItemButton;
    [SerializeField] private GameObject GuidePanel;   // 가이드창 (Panel)
    [SerializeField] private GameObject ItemPanel;
    [SerializeField] private GameObject guide;
    [SerializeField] private Button left;
    [SerializeField] private Button right;
    [SerializeField] private GameObject first;
    [SerializeField] private GameObject second;

    public static UIManager Instance;

    private void Awake()
    {
        // 싱글톤 패턴
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // UI 초기 숨기기
        if (resultPanel != null)
            resultPanel.SetActive(false);

        // 버튼 이벤트 연결
        if (mainButton != null)
            mainButton.onClick.AddListener(GoToMain);

        //옵션 버튼 연결
        if (optionsButton != null)
            optionsButton.onClick.AddListener(() => { ToggleOptionPanel(true); });

        if (closeOptionsButton != null)
            closeOptionsButton.onClick.AddListener(() => { ToggleOptionPanel(false); });

        if (GuideButton != null)
            GuideButton.onClick.AddListener(() => { ToggleGuidePanel(true); });

        if (closeGuideButton != null)
            closeGuideButton.onClick.AddListener(() => { ToggleGuidePanel(false); });

        if (closeItemButton != null)
            closeItemButton.onClick.AddListener(() => { ToggleItemPanel(false); });

        if (left != null)
            left.onClick.AddListener(Left_page);

        if (right != null)
            right.onClick.AddListener(Right_page);
    }

    public void ToggleOptionPanel(bool on)
    {
        if (optionsPanel != null)
            optionsPanel.SetActive(on);
    }

    public void ToggleOptionButton(bool on)
    {
        if (options != null)
            options.SetActive(on);
    }

    public void ToggleGuidePanel(bool on)
    {
        if (GuidePanel != null)
            GuidePanel.SetActive(on);
    }

    public void ToggleGuideButton(bool on)
    {
        if (guide != null)
            guide.SetActive(on);
    }

    public void ToggleItemPanel(bool on)
    {
        if (ItemPanel != null)
            ItemPanel.SetActive(on);
    }

    public void ToggleResultPanel(bool on)
    {
        if (resultPanel != null)
            resultPanel.SetActive(on);
    }

    public void Left_page()
    {
        if (second != null)
            second.SetActive(false);
        if (first != null)
            first.SetActive(true);
    }

    public void Right_page()
    {
        if (first != null)
            first.SetActive(false);
        if (second != null)
            second.SetActive(true);
    }

    private void GoToMain()
    {
        NetworkManager.Singleton.Shutdown();
        SceneManager.LoadScene("Login");
    }
}
