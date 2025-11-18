using UnityEngine;
using UnityEngine.UI;

public class MobileInputManager : MonoBehaviour
{
    public static MobileInputManager Instance; //싱글톤

    public FixedJoystick joystick;
    public Button jumpButton;
    public Button grabButton;
    public Button stickerButton;
    public GameObject stickerPanel;
    public Button[] stickerReqButtons;

    [SerializeField] private GameObject mobileUI; // 켜고 끌 GameObject

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
}
