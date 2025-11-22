using UnityEngine;
using UnityEngine.UI;

public class Options : MonoBehaviour
{
    [Header("Screen Buttons")]
    public Button fullButton;

    [Header("Settings")]
    public Toggle performanceToggle;    // FPS/Ping 표시
    public Toggle effectsToggle;        // 다른 플레이어 이펙트
    public Toggle mobileUIToggle;       // 모바일 UI 표시


    [Header("Volume Settings")]
    public Slider masterVolumeSlider;   // 마스터 볼륨
    public Button masterMuteButton;     // 마스터 뮤트 버튼
    public Slider bgmVolumeSlider;      // 배경음악 볼륨
    public Button bgmMuteButton;        // 배경음악 뮤트 버튼
    public Slider sfxVolumeSlider;      // 효과음 볼륨
    public Button sfxMuteButton;        // 효과음 뮤트 버튼

    // 뮤트 해제 시 복구용 변수
    private float lastMasterVolume = 1f;
    private float lastBGMVolume = 1f;
    private float lastSFXVolume = 1f;

    // VolumeButton 컴포넌트 참조
    private VolumeButton masterVolumeButtonComponent;
    private VolumeButton bgmVolumeButtonComponent;
    private VolumeButton sfxVolumeButtonComponent;

    private const string PERFORMANCE_KEY = "ShowPerformance";
    private const string EFFECTS_KEY = "ShowEffects";
    private const string MOBILE_UI_KEY = "ShowMobileUI";
    private const string MASTER_VOLUME_KEY = "MasterVolume";
    private const string BGM_VOLUME_KEY = "BGMVolume";
    private const string SFX_VOLUME_KEY = "SFXVolume";

    private GameObject mobileUICanvas; // UICanvas의 Mobile 자식 오브젝트

    void Start()
    {
        // 버튼 이벤트 연결
        if (fullButton != null)
            fullButton.onClick.AddListener(SetFullscreen);

        // VolumeButton 컴포넌트 가져오기
        if (masterMuteButton != null)
            masterVolumeButtonComponent = masterMuteButton.GetComponent<VolumeButton>();
        if (bgmMuteButton != null)
            bgmVolumeButtonComponent = bgmMuteButton.GetComponent<VolumeButton>();
        if (sfxMuteButton != null)
            sfxVolumeButtonComponent = sfxMuteButton.GetComponent<VolumeButton>();

        // 뮤트 버튼 이벤트 연결 (PointerDown으로 즉시 반응)
        if (masterMuteButton != null)
            SetupButtonPointerDown(masterMuteButton, ToggleMasterMute);
        if (bgmMuteButton != null)
            SetupButtonPointerDown(bgmMuteButton, ToggleBGMMute);
        if (sfxMuteButton != null)
            SetupButtonPointerDown(sfxMuteButton, ToggleSFXMute);

        // 설정 초기화
        InitializeSettings();
    }

    // PointerDown 이벤트 설정 (버튼을 누르는 순간 즉시 반응)
    private void SetupButtonPointerDown(Button button, UnityEngine.Events.UnityAction callback)
    {
        if (button == null) return;

        UnityEngine.EventSystems.EventTrigger trigger = button.gameObject.GetComponent<UnityEngine.EventSystems.EventTrigger>();
        if (trigger == null)
        {
            trigger = button.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
        }

        UnityEngine.EventSystems.EventTrigger.Entry entry = new UnityEngine.EventSystems.EventTrigger.Entry();
        entry.eventID = UnityEngine.EventSystems.EventTriggerType.PointerDown;
        entry.callback.AddListener((data) => { callback(); });
        trigger.triggers.Add(entry);
    }

    private void InitializeSettings()
    {
        // UICanvas의 Mobile 오브젝트 찾기
        GameObject uiCanvas = GameObject.Find("UICanvas");
        if (uiCanvas != null)
        {
            Transform mobileTransform = uiCanvas.transform.Find("Mobile");
            if (mobileTransform != null)
            {
                mobileUICanvas = mobileTransform.gameObject;
            }
        }

        // Performance Toggle (기본값: OFF)
        if (performanceToggle != null)
        {
            bool showPerformance = PlayerPrefs.GetInt(PERFORMANCE_KEY, 0) == 1; // 기본값 꺼짐
            performanceToggle.isOn = showPerformance;
            performanceToggle.onValueChanged.AddListener(OnPerformanceToggleChanged);
        }

        // Effects Toggle (기본값: ON)
        if (effectsToggle != null)
        {
            bool showEffects = PlayerPrefs.GetInt(EFFECTS_KEY, 1) == 1; // 기본값 켜짐
            effectsToggle.isOn = showEffects;
            effectsToggle.onValueChanged.AddListener(OnEffectsToggleChanged);
        }

        // Mobile UI Toggle - 현재 Mobile 오브젝트의 활성화 상태를 읽어서 반영
        if (mobileUIToggle != null)
        {
            // 실제 Mobile GameObject의 현재 상태를 읽음
            bool currentMobileUIState = mobileUICanvas != null && mobileUICanvas.activeSelf;
            mobileUIToggle.isOn = currentMobileUIState;
            mobileUIToggle.onValueChanged.AddListener(OnMobileUIToggleChanged);
        }

        // Master Volume Slider - PlayerPrefs에서 읽고 실제 오디오에 적용
        if (masterVolumeSlider != null)
        {
            float masterVolume = PlayerPrefs.GetFloat(MASTER_VOLUME_KEY, 1f);
            AudioListener.volume = masterVolume; // 실제 볼륨 적용
            masterVolumeSlider.value = masterVolume;
            lastMasterVolume = masterVolume > 0.01f ? masterVolume : 1f;
            masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);

            // 초기 아이콘 상태 설정
            if (masterVolumeButtonComponent != null)
            {
                masterVolumeButtonComponent.UpdateIcon(masterVolume);
            }
        }

        // BGM Volume Slider - PlayerPrefs에서 읽고 실제 오디오에 적용
        if (bgmVolumeSlider != null)
        {
            float bgmVolume = PlayerPrefs.GetFloat(BGM_VOLUME_KEY, 1f);
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetBGMVolume(bgmVolume); // 실제 볼륨 적용
            }
            bgmVolumeSlider.value = bgmVolume;
            lastBGMVolume = bgmVolume > 0.01f ? bgmVolume : 1f;
            bgmVolumeSlider.onValueChanged.AddListener(OnBGMVolumeChanged);

            // 초기 아이콘 상태 설정
            if (bgmVolumeButtonComponent != null)
            {
                bgmVolumeButtonComponent.UpdateIcon(bgmVolume);
            }
        }

        // SFX Volume Slider - PlayerPrefs에서 읽고 실제 오디오에 적용
        if (sfxVolumeSlider != null)
        {
            float sfxVolume = PlayerPrefs.GetFloat(SFX_VOLUME_KEY, 1f);
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetSFXVolume(sfxVolume); // 실제 볼륨 적용
            }
            sfxVolumeSlider.value = sfxVolume;
            lastSFXVolume = sfxVolume > 0.01f ? sfxVolume : 1f;
            sfxVolumeSlider.onValueChanged.AddListener(OnSFXVolumeChanged);

            // 초기 아이콘 상태 설정
            if (sfxVolumeButtonComponent != null)
            {
                sfxVolumeButtonComponent.UpdateIcon(sfxVolume);
            }
        }
    }

    // ===================== 설정 콜백 =====================
    private void OnPerformanceToggleChanged(bool isOn)
    {
        PlayerPrefs.SetInt(PERFORMANCE_KEY, isOn ? 1 : 0);
        PlayerPrefs.Save();

        if (CompleteNGOProfiler.Instance != null)
        {
            CompleteNGOProfiler.Instance.ToggleVisibility(isOn);
        }
    }

    private void OnEffectsToggleChanged(bool isOn)
    {
        PlayerPrefs.SetInt(EFFECTS_KEY, isOn ? 1 : 0);
        PlayerPrefs.Save();

        // 설정이 변경되면 현재 재생 중인 다른 플레이어의 파티클을 즉시 중지/재생
        // PlayerController.cs의 ClientRpc 메서드들이 PlayerPrefs를 확인하여 자동으로 처리
    }

    private void OnMobileUIToggleChanged(bool isOn)
    {
        PlayerPrefs.SetInt(MOBILE_UI_KEY, isOn ? 1 : 0);
        PlayerPrefs.Save();

        // Mobile UI 활성화/비활성화
        if (mobileUICanvas != null)
        {
            mobileUICanvas.SetActive(isOn);
        }
    }

    // ===================== 볼륨 설정 =====================
    private void OnMasterVolumeChanged(float value)
    {
        // 슬라이더 변경 시 lastVolume 업데이트 (뮤트가 아닌 경우)
        if (value > 0.01f)
        {
            lastMasterVolume = value;
        }

        AudioListener.volume = value;
        PlayerPrefs.SetFloat(MASTER_VOLUME_KEY, value);
        PlayerPrefs.Save();

        // 버튼 아이콘 업데이트
        if (masterVolumeButtonComponent != null)
        {
            masterVolumeButtonComponent.UpdateIcon(value);
        }
    }

    private void OnBGMVolumeChanged(float value)
    {
        // 슬라이더 변경 시 lastVolume 업데이트 (뮤트가 아닌 경우)
        if (value > 0.01f)
        {
            lastBGMVolume = value;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetBGMVolume(value);
        }
        PlayerPrefs.SetFloat(BGM_VOLUME_KEY, value);
        PlayerPrefs.Save();

        // 버튼 아이콘 업데이트
        if (bgmVolumeButtonComponent != null)
        {
            bgmVolumeButtonComponent.UpdateIcon(value);
        }
    }

    private void OnSFXVolumeChanged(float value)
    {
        // 슬라이더 변경 시 lastVolume 업데이트 (뮤트가 아닌 경우)
        if (value > 0.01f)
        {
            lastSFXVolume = value;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetSFXVolume(value);
        }
        PlayerPrefs.SetFloat(SFX_VOLUME_KEY, value);
        PlayerPrefs.Save();

        // 버튼 아이콘 업데이트
        if (sfxVolumeButtonComponent != null)
        {
            sfxVolumeButtonComponent.UpdateIcon(value);
        }
    }

    // ===================== 뮤트 토글 =====================
    private void ToggleMasterMute()
    {
        if (masterVolumeSlider == null) return;

        if (masterVolumeSlider.value > 0.01f)
        {
            // 현재 볼륨을 저장하고 뮤트
            lastMasterVolume = masterVolumeSlider.value;
            masterVolumeSlider.value = 0f;
        }
        else
        {
            // 저장된 볼륨으로 복구
            masterVolumeSlider.value = lastMasterVolume;
        }
    }

    private void ToggleBGMMute()
    {
        if (bgmVolumeSlider == null) return;

        if (bgmVolumeSlider.value > 0.01f)
        {
            // 현재 볼륨을 저장하고 뮤트
            lastBGMVolume = bgmVolumeSlider.value;
            bgmVolumeSlider.value = 0f;
        }
        else
        {
            // 저장된 볼륨으로 복구
            bgmVolumeSlider.value = lastBGMVolume;
        }
    }

    private void ToggleSFXMute()
    {
        if (sfxVolumeSlider == null) return;

        if (sfxVolumeSlider.value > 0.01f)
        {
            // 현재 볼륨을 저장하고 뮤트
            lastSFXVolume = sfxVolumeSlider.value;
            sfxVolumeSlider.value = 0f;
        }
        else
        {
            // 저장된 볼륨으로 복구
            sfxVolumeSlider.value = lastSFXVolume;
        }
    }

    // ===================== 화면 설정 =====================
    private void SetFullscreen()
    {
        Screen.fullScreen = true;
    }
}
