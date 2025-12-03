using UnityEngine;
using UnityEngine.UI; // UI를 다루기 위해 필수

public class VolumeButton : MonoBehaviour
{
    [Header("아이콘을 표시하는 이미지 컴포넌트")]
    public Image targetImage;

    [Header("바꿀 스프라이트 이미지들")]
    public Sprite soundOnSprite;  // 소리 켜짐 아이콘
    public Sprite soundOffSprite; // 소리 꺼짐 아이콘

    private bool isMuted = false; // 현재 상태 (기본은 켜짐)

    // 슬라이더 값에 따라 아이콘 업데이트 (외부에서 호출)
    public void UpdateIcon(float sliderValue)
    {
        bool shouldBeMuted = sliderValue <= 0.01f;

        if (shouldBeMuted != isMuted)
        {
            isMuted = shouldBeMuted;
            UpdateSprite();
        }
    }

    // 아이콘 상태 설정 (외부에서 호출)
    public void SetMuted(bool muted)
    {
        isMuted = muted;
        UpdateSprite();
    }

    // 현재 뮤트 상태 반환
    public bool IsMuted()
    {
        return isMuted;
    }

    // 스프라이트 업데이트
    private void UpdateSprite()
    {
        if (targetImage != null)
        {
            targetImage.sprite = isMuted ? soundOffSprite : soundOnSprite;
        }
    }
}