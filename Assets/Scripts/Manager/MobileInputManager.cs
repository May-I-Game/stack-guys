using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class MobileInputManager : MonoBehaviour
{
    public static MobileInputManager Instance; //싱글톤

    public FloatingJoystick joystick;
    public Button jumpButton;
    public Button grabButton;
    public Button stickerButton;
    public GameObject stickerPanel;
    public Button[] stickerReqButtons;

    [SerializeField] private GameObject mobileUI; // 켜고 끌 GameObject

    [Header("UI Sound Effects")]
    [SerializeField] private AudioSource uiAudioSource; // UI 효과음 소스
    [SerializeField] private AudioClip buttonClickClip; // 버튼 클릭 효과음
    [Range(0f, 1f)][SerializeField] private float uiVolume = 0.7f; // UI 효과음 볼륨

    void Awake()
    {
        //싱글톤 설정
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // stickerButton에 PointerDown 이벤트로 효과음 추가
        if (stickerButton != null)
        {
            SetupButtonPointerDown(stickerButton, PlayButtonClickSound);
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

    public void ToggleCanvas()
    {
        if (mobileUI != null)
        {
            mobileUI.SetActive(!mobileUI.activeSelf);
        }
    }

    public void ShowCanvas()
    {
        if (mobileUI != null)
        {
            mobileUI.SetActive(true);
        }
    }

    public void HideCanvas()
    {
        if (mobileUI != null)
        {
            mobileUI.SetActive(false);
        }
    }

    public void ShowStickerPanel()
    {
        if (stickerPanel != null)
        {
            stickerPanel.SetActive(true);
        }
    }

    public void HideStickerPanel()
    {
        if (stickerPanel != null)
        {
            stickerPanel.SetActive(false);
        }
    }

    // UI 버튼 클릭 효과음 재생
    public void PlayButtonClickSound()
    {
        if (uiAudioSource != null && buttonClickClip != null)
        {
            uiAudioSource.PlayOneShot(buttonClickClip, uiVolume);
        }
    }
}
