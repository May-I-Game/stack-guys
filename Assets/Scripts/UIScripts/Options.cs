using UnityEngine;
using UnityEngine.UI;

public class Options : MonoBehaviour
{
    [Header("Screen Buttons")]
    public Button fullButton;

    [Header("Settings")]
    public Toggle performanceToggle;    // FPS/Ping 표시
    public Toggle effectsToggle;        // 다른 플레이어 이펙트

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

    private const string PERFORMANCE_KEY = "ShowPerformance";
    private const string EFFECTS_KEY = "ShowEffects";
    private const string MASTER_VOLUME_KEY = "MasterVolume";
    private const string BGM_VOLUME_KEY = "BGMVolume";
    private const string SFX_VOLUME_KEY = "SFXVolume";

    void Start()
    {
        // 버튼 이벤트 연결
        if (fullButton != null)
            fullButton.onClick.AddListener(SetFullscreen);

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

        // Master Volume Slider
        if (masterVolumeSlider != null)
        {
            float masterVolume = PlayerPrefs.GetFloat(MASTER_VOLUME_KEY, 1f);
            masterVolumeSlider.value = masterVolume;
            lastMasterVolume = masterVolume;
            AudioListener.volume = masterVolume;
            masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
        }

        // BGM Volume Slider
        if (bgmVolumeSlider != null)
        {
            float bgmVolume = PlayerPrefs.GetFloat(BGM_VOLUME_KEY, 1f);
            bgmVolumeSlider.value = bgmVolume;
            lastBGMVolume = bgmVolume;
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetBGMVolume(bgmVolume);
            }
            bgmVolumeSlider.onValueChanged.AddListener(OnBGMVolumeChanged);
        }

        // SFX Volume Slider
        if (sfxVolumeSlider != null)
        {
            float sfxVolume = PlayerPrefs.GetFloat(SFX_VOLUME_KEY, 1f);
            sfxVolumeSlider.value = sfxVolume;
            lastSFXVolume = sfxVolume;
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetSFXVolume(sfxVolume);
            }
            sfxVolumeSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
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
