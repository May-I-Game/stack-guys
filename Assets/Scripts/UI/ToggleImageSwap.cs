using UnityEngine;
using UnityEngine.UI;

public class ToggleImageSwap : MonoBehaviour
{
    public Toggle toggle;        // 이 토글
    public Image targetImage;    // 이미지 컴포넌트 (지금 FPSPing 이미지)
    public Sprite onSprite;      // 켜졌을 때
    public Sprite offSprite;     // 꺼졌을 때

    void Awake()
    {
        // 처음 상태 반영
        UpdateImage(toggle.isOn);

        // 값 바뀔 때마다 호출
        toggle.onValueChanged.AddListener(UpdateImage);
    }

    void UpdateImage(bool isOn)
    {
        targetImage.sprite = isOn ? onSprite : offSprite;
    }
}
