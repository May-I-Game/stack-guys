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

    // 버튼을 누를 때 실행될 함수
    public void ToggleSound()
    {
        // 1. 상태 뒤집기 (켜짐 <-> 꺼짐)
        isMuted = !isMuted;

        // 2. 상태에 따라 이미지 교체
        if (isMuted)
        {
            targetImage.sprite = soundOffSprite;
        }
        else
        {
            targetImage.sprite = soundOnSprite;
        }
    }
}