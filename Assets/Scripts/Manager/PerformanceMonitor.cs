using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

public class PerformanceMonitor : NetworkBehaviour
{
    public static PerformanceMonitor Instance;

    [Header("UI References")]
    [SerializeField] private GameObject performancePanel; // FPS/Ping 표시 패널
    [SerializeField] private TMP_Text fpsText; // FPS 텍스트
    [SerializeField] private TMP_Text pingText; // Ping 텍스트

    [Header("Settings")]
    [SerializeField] private float updateInterval = 0.5f; // UI 업데이트 간격 (초)

    // FPS 계산
    private float fps = 0f;
    private float minFPS = 999f;
    private float maxFPS = 0f;

    // Ping 계산
    private float ping = 0f;
    private List<float> pingHistory = new List<float>(30);
    private const int PING_HISTORY_COUNT = 30;

    // 타이머
    private float uiUpdateTimer = 0f;

    // 네트워크 관련
    private NetworkManager nm;
    private UnityTransport utp;

    private bool isVisible = false;

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

    void Start()
    {
        // NetworkManager 가져오기
        nm = NetworkManager.Singleton;
        if (nm != null)
        {
            utp = nm.NetworkConfig.NetworkTransport as UnityTransport;
        }

        // PlayerPrefs에서 설정 불러오기
        isVisible = PlayerPrefs.GetInt("ShowPerformance", 0) == 1;
        UpdateVisibility();
    }

    void Update()
    {
        if (!isVisible) return;

        // 통계 업데이트
        UpdateStats();

        // UI 업데이트 (500ms마다)
        uiUpdateTimer += Time.deltaTime;
        if (uiUpdateTimer >= updateInterval)
        {
            uiUpdateTimer = 0f;
            UpdateUI();
        }
    }

    private void UpdateStats()
    {
        // FPS 계산
        float currentFPS = 1f / Time.unscaledDeltaTime;
        fps = currentFPS;
        minFPS = Mathf.Min(minFPS, currentFPS);
        maxFPS = Mathf.Max(maxFPS, currentFPS);

        // Ping 계산 (CompleteNGOProfiler 방식)
        if (IsClient && utp != null)
        {
            try
            {
                ping = utp.GetCurrentRtt(0);

                // 이동 평균 필터링
                if (pingHistory.Count >= PING_HISTORY_COUNT)
                {
                    pingHistory.RemoveAt(0);
                }
                pingHistory.Add(ping);
            }
            catch
            {
                // RTT를 가져올 수 없는 경우 무시
            }
        }
    }

    private void UpdateUI()
    {
        // FPS 텍스트 업데이트
        if (fpsText != null)
        {
            fpsText.text = $"FPS: {fps:F1}";

            // FPS에 따라 색상 변경
            if (fps >= 60)
                fpsText.color = Color.green;
            else if (fps >= 30)
                fpsText.color = Color.yellow;
            else
                fpsText.color = Color.red;
        }

        // Ping 텍스트 업데이트
        if (pingText != null)
        {
            if (IsClient && nm != null && nm.IsConnectedClient)
            {
                // 평균 Ping 계산
                float avgPing = 0f;
                if (pingHistory.Count > 0)
                {
                    foreach (float p in pingHistory)
                    {
                        avgPing += p;
                    }
                    avgPing /= pingHistory.Count;
                }

                pingText.text = $"Ping: {avgPing:F0}ms";

                // Ping에 따라 색상 변경
                if (avgPing <= 50)
                    pingText.color = Color.green;
                else if (avgPing <= 100)
                    pingText.color = Color.yellow;
                else
                    pingText.color = Color.red;
            }
            else
            {
                pingText.text = "Ping: --ms";
                pingText.color = Color.white;
            }
        }
    }

    public void ToggleVisibility(bool show)
    {
        isVisible = show;
        UpdateVisibility();

        // 설정 저장
        PlayerPrefs.SetInt("ShowPerformance", isVisible ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void UpdateVisibility()
    {
        if (performancePanel != null)
        {
            performancePanel.SetActive(isVisible);
        }
    }

    public bool IsVisible()
    {
        return isVisible;
    }
}
