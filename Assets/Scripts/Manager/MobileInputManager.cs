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

    private Canvas canvas;

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
        canvas = GetComponent<Canvas>();
    }

    public void ToggleCanvas()
    {
        if (canvas != null)
        {
            canvas.enabled = !canvas.enabled;
        }
    }

    public void ShowCanvas()
    {
        if (canvas != null)
        {
            canvas.enabled = true;
        }
    }

    public void HideCanvas()
    {
        if (canvas != null)
        {
            canvas.enabled = false;
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
