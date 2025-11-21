using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

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
    [SerializeField] private GameObject GuidePanel;   // 가이드창 (Panel)
    [SerializeField] private GameObject guide;
    [SerializeField] private GameObject firstPC;       // 1페이지 PC용
    [SerializeField] private GameObject firstMobile;   // 1페이지 모바일용
    [SerializeField] private GameObject second;        // 2페이지
    [SerializeField] private GameObject third;         // 3페이지
    [SerializeField] private GameObject fourth;        // 4페이지
    [SerializeField] private GameObject fifth;         // 5페이지

    [Header("UI Sound Effects")]
    [SerializeField] private AudioSource uiAudioSource; // UI 효과음 소스
    [SerializeField] private AudioClip buttonClickClip; // 버튼 클릭 효과음
    [Range(0f, 1f)][SerializeField] private float uiVolume = 0.7f; // UI 효과음 볼륨

    public static UIManager Instance;

    // 가이드 페이지 관리
    private int currentGuidePage = 1;                  // 현재 페이지 (1~5)
    private bool hasClosedGuideOnce = false;          // 닫기 버튼 클릭 이력
    private bool isMobilePlatform = false;             // 모바일 플랫폼 여부

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
        // 플랫폼 감지
        DetectPlatform();

        // UI 초기 숨기기
        if (resultPanel != null)
            resultPanel.SetActive(false);

        // 버튼 이벤트 연결 (PointerDown으로 즉시 반응)
        if (mainButton != null)
            SetupButtonPointerDown(mainButton, GoToMain);

        //옵션 버튼 연결
        if (optionsButton != null)
            SetupButtonPointerDown(optionsButton, OnOptionsButtonClicked);

        if (closeOptionsButton != null)
            SetupButtonPointerDown(closeOptionsButton, () =>
            {
                PlayButtonClickSound(); // 효과음 재생
                ToggleOptionPanel(false);
            });

        if (GuideButton != null)
            SetupButtonPointerDown(GuideButton, OnGuideButtonClicked);

        // 각 페이지의 네비게이션 버튼 및 닫기 버튼 이벤트 연결
        SetupPageNavigationButtons();

        // 게임 시작 시 가이드 자동으로 열기
        OpenGuidePanel();
    }

    // 옵션 버튼 클릭 시 호출
    private void OnOptionsButtonClicked()
    {
        PlayButtonClickSound(); // 효과음 재생

        bool isOptionPanelActive = optionsPanel != null && optionsPanel.activeSelf;

        if (isOptionPanelActive)
        {
            // 이미 열려있으면 닫기
            ToggleOptionPanel(false);
        }
        else
        {
            // 닫혀있으면 열기 (가이드 패널은 닫기)
            ToggleGuidePanel(false);
            ToggleOptionPanel(true);
        }
    }

    // 가이드 버튼 클릭 시 호출
    private void OnGuideButtonClicked()
    {
        PlayButtonClickSound(); // 효과음 재생

        bool isGuidePanelActive = GuidePanel != null && GuidePanel.activeSelf;

        if (isGuidePanelActive)
        {
            // 이미 열려있으면 닫기
            CloseGuidePanel();
        }
        else
        {
            // 닫혀있으면 열기 (옵션 패널은 닫기, 1페이지로 초기화)
            ToggleOptionPanel(false);
            OpenGuidePanel();
        }
    }

    // 가이드 패널 열기 (항상 1페이지부터 시작)
    private void OpenGuidePanel()
    {
        if (GuidePanel == null) return;

        GuidePanel.SetActive(true);
        currentGuidePage = 1;
        NavigateToPage(1);
        // NavigateToPage 안에서 UpdateCloseButtonState()가 이미 호출됨
    }

    // 가이드 패널 닫기
    private void CloseGuidePanel()
    {
        if (GuidePanel == null) return;

        GuidePanel.SetActive(false);
    }

    // 닫기 버튼 클릭 시 호출
    private void OnCloseGuideButtonClicked()
    {
        PlayButtonClickSound(); // 효과음 재생

        hasClosedGuideOnce = true;  // 닫기 버튼 클릭 이력 저장
        CloseGuidePanel();
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
    public void ToggleResultPanel(bool on)
    {
        if (resultPanel != null)
            resultPanel.SetActive(on);
    }

    // ========================== 가이드 페이지 네비게이션 ==========================

    // 이전 페이지로 이동
    private void PreviousPage()
    {
        PlayButtonClickSound(); // 효과음 재생

        if (currentGuidePage > 1)
        {
            currentGuidePage--;
            NavigateToPage(currentGuidePage);
        }
    }

    // 다음 페이지로 이동
    private void NextPage()
    {
        PlayButtonClickSound(); // 효과음 재생

        if (currentGuidePage < 5)
        {
            currentGuidePage++;
            NavigateToPage(currentGuidePage);
        }
    }

    // 특정 페이지로 이동
    private void NavigateToPage(int pageNumber)
    {
        // 모든 페이지 비활성화
        if (firstPC != null) firstPC.SetActive(false);
        if (firstMobile != null) firstMobile.SetActive(false);
        if (second != null) second.SetActive(false);
        if (third != null) third.SetActive(false);
        if (fourth != null) fourth.SetActive(false);
        if (fifth != null) fifth.SetActive(false);

        // 해당 페이지 활성화
        switch (pageNumber)
        {
            case 1:
                // 플랫폼에 따라 PC용 또는 모바일용 첫 페이지 활성화
                if (isMobilePlatform)
                {
                    if (firstMobile != null) firstMobile.SetActive(true);
                }
                else
                {
                    if (firstPC != null) firstPC.SetActive(true);
                }
                break;
            case 2:
                if (second != null) second.SetActive(true);
                break;
            case 3:
                if (third != null) third.SetActive(true);
                break;
            case 4:
                if (fourth != null) fourth.SetActive(true);
                break;
            case 5:
                if (fifth != null) fifth.SetActive(true);
                break;
        }

        // 닫기 버튼 상태 업데이트
        UpdateCloseButtonState();
    }

    // 각 페이지의 네비게이션 버튼 이벤트 설정
    private void SetupPageNavigationButtons()
    {
        SetupButtonsForPage(firstPC);
        SetupButtonsForPage(firstMobile);
        SetupButtonsForPage(second);
        SetupButtonsForPage(third);
        SetupButtonsForPage(fourth);
        SetupButtonsForPage(fifth);
    }

    // 특정 페이지의 버튼들에 이벤트 연결
    private void SetupButtonsForPage(GameObject page)
    {
        if (page == null) return;

        // 페이지 내부의 모든 버튼 찾기
        Button[] buttons = page.GetComponentsInChildren<Button>(true);

        foreach (Button btn in buttons)
        {
            // 버튼 이름으로 구분
            if (btn.name == "Left")
            {
                SetupButtonPointerDown(btn, PreviousPage);
            }
            else if (btn.name == "Right")
            {
                SetupButtonPointerDown(btn, NextPage);
            }
            else if (btn.name == "close")
            {
                SetupButtonPointerDown(btn, OnCloseGuideButtonClicked);
            }
        }
    }

    // 닫기 버튼 상태 업데이트
    private void UpdateCloseButtonState()
    {
        // 한 번이라도 닫기를 눌렀거나 5페이지인 경우 활성화
        bool shouldEnable = hasClosedGuideOnce || currentGuidePage == 5;

        // 모든 페이지의 Close 버튼 찾아서 상태 업데이트
        UpdateCloseButtonForPage(firstPC, shouldEnable);
        UpdateCloseButtonForPage(firstMobile, shouldEnable);
        UpdateCloseButtonForPage(second, shouldEnable);
        UpdateCloseButtonForPage(third, shouldEnable);
        UpdateCloseButtonForPage(fourth, shouldEnable);
        UpdateCloseButtonForPage(fifth, shouldEnable);
    }

    // 특정 페이지의 close 버튼 상태 업데이트
    private void UpdateCloseButtonForPage(GameObject page, bool shouldEnable)
    {
        if (page == null) return;

        Button[] buttons = page.GetComponentsInChildren<Button>(true);
        foreach (Button btn in buttons)
        {
            if (btn.name == "close")
            {
                btn.interactable = shouldEnable;

                // 비활성화 시 이미지 어둡게 처리
                Image btnImage = btn.GetComponent<Image>();
                if (btnImage != null)
                {
                    btnImage.color = shouldEnable ? Color.white : new Color(0.3f, 0.3f, 0.3f, 0.5f);
                }

                // 닫기 버튼의 TMP_Text 자식 요소 처리
                // 한번 읽은 이후(hasClosedGuideOnce == true)에는 텍스트 비활성화
                TMP_Text textChild = btn.GetComponentInChildren<TMP_Text>();
                if (textChild != null && hasClosedGuideOnce)
                {
                    textChild.gameObject.SetActive(false);
                }
            }
        }
    }

    private void GoToMain()
    {
        NetworkManager.Singleton.Shutdown();
        SceneManager.LoadScene("Login");
    }

    // ========================== 플랫폼 감지 ==========================

    // 플랫폼 감지
    private void DetectPlatform()
    {
        if (Application.platform == RuntimePlatform.WebGLPlayer)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // WebGL에서는 JavaScript를 통해 User Agent 확인
            isMobilePlatform = IsMobileUserAgent();
#else
            isMobilePlatform = false;
#endif
        }
        else
        {
            // 네이티브 플랫폼에서는 SystemInfo 사용
            isMobilePlatform = SystemInfo.operatingSystem.Contains("Android") ||
                               SystemInfo.operatingSystem.Contains("iPhone") ||
                               SystemInfo.operatingSystem.Contains("iPad");
        }

        Debug.Log($"📱 플랫폼 감지: {(isMobilePlatform ? "모바일" : "PC")}");
    }

    // JavaScript 함수 선언 (WebGL 전용)
    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern bool IsMobileUserAgent();

    // ========================== UI 효과음 ==========================

    // UI 버튼 클릭 효과음 재생
    public void PlayButtonClickSound()
    {
        if (uiAudioSource != null && buttonClickClip != null)
        {
            uiAudioSource.PlayOneShot(buttonClickClip, uiVolume);
        }
    }

    // PointerDown 이벤트 설정 (버튼을 누르는 순간 즉시 반응)
    private void SetupButtonPointerDown(Button button, UnityEngine.Events.UnityAction callback)
    {
        if (button == null) return;

        EventTrigger trigger = button.gameObject.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = button.gameObject.AddComponent<EventTrigger>();
        }

        EventTrigger.Entry entry = new EventTrigger.Entry();
        entry.eventID = EventTriggerType.PointerDown;
        entry.callback.AddListener((data) => { callback(); });
        trigger.triggers.Add(entry);
    }

}
