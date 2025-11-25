using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class ItemSpawner : MonoBehaviour
{
    [System.Serializable]
    public class SpawnableItem
    {
        // 스폰할 아이템 프리팹
        public NetworkObject itemPrefab;

        // 스폰 가중치 (높을수록 자주 나옴, 1~10)
        public float spawnWeight = 1f;
    }

    // 스폰할 아이템 리스트 (랜덤 선택)
    [Header("Spawn Settings")]
    public List<SpawnableItem> spawnableItems = new List<SpawnableItem>();

    // 아이템을 공중으로 띄울 거리
    public float spawnHeightOffset = 0.3f;

    // 스폰 간격 (초)
    public float spawnInterval = 5f;

    // 스폰 위치 Transform
    public Transform spawnPoint;

    [Header("Debug")]
    public bool enableDebugLog = true;

    [SerializeField]
    // 현재 스폰된 아이템 (서버만 관리)
    private NetworkObject currentItem = null;

    // 다음 스폰 시간 (서버만 관리)
    private float nextSpawnTime = 0f;

    private bool IsEditor => EditorManager.Instance;

    private void Update()
    {
        if (IsEditor)
        {
            if (!EditorManager.Instance.IsGame) return;

            EditorSpawnLogic();
        }

        else
        {
            // 서버에서만 스폰 처리
            if (!NetworkManager.Singleton.IsServer) return;

            SpawnLogic();
        }
    }

    private void EditorSpawnLogic()
    {
        // 스폰 시간이 되었고 현재 아이템이 없을 때
        if (Time.time >= nextSpawnTime && currentItem == null)
        {
            SpawnRandomItem();
            nextSpawnTime = Time.time + spawnInterval;
        }
    }

    private void SpawnLogic()
    {
        // 스폰 시간이 되었고 현재 아이템이 없을 때
        if (Time.time >= nextSpawnTime && currentItem == null)
        {
            SpawnRandomItem();
            nextSpawnTime = Time.time + spawnInterval;
        }

        // 현재 아이템이 파괴되었는지 체크
        if (currentItem != null && !currentItem.IsSpawned)
        {
            currentItem = null;
            if (enableDebugLog)
            {
                Debug.Log($"[ItemSpawner] 아이템이 사용됨 다음 스폰: {spawnInterval}초 후");
            }
        }
    }

    // 랜덤 아이템을 스폰
    private void SpawnRandomItem()
    {
        // 스폰 가능한 아이템이 없으면 무시
        if (spawnableItems == null || spawnableItems.Count == 0)
        {
            Debug.LogWarning("[ItemSpawner] spawnableItems 리스트가 비어있음");
        }

        // 총 가중치 계산
        float totalWeight = 0f;
        foreach (var item in spawnableItems)
        {
            if (item.itemPrefab != null)
            {
                totalWeight += item.spawnWeight;
            }
        }

        if (totalWeight <= 0f)
        {
            Debug.LogWarning("[ItemSpawner] 유효한 아이템이 없음");
        }

        // 가중치 기반 랜덤 선택
        float randomValue = Random.Range(0f, totalWeight);
        float cumulativeWeight = 0f;

        foreach (var item in spawnableItems)
        {
            if (item.itemPrefab == null) continue;

            cumulativeWeight += item.spawnWeight;
            if (randomValue <= cumulativeWeight)
            {
                // 아이템 스폰
                Vector3 basePos = spawnPoint != null ? spawnPoint.position : transform.position;

                Vector3 spawnPos = basePos + Vector3.up * spawnHeightOffset;
                Quaternion spawnRot = spawnPoint != null ? spawnPoint.rotation : transform.rotation;

                currentItem = Instantiate(item.itemPrefab, spawnPos, spawnRot);

                // 스폰한 아이템에 스포너를 기록
                InteractiveItem interactiveItem = currentItem.GetComponent<InteractiveItem>();
                if (interactiveItem != null)
                {
                    interactiveItem.SourceSpawner = this;
                }

                if (!IsEditor)
                {
                    currentItem.Spawn();
                }

                if (enableDebugLog)
                {
                    Debug.Log($"[ItemSpawner] {item.itemPrefab.name} 스폰됨 at {spawnPos}");
                }

                break;
            }
        }
    }

    // 아이템이 사용되었을 때 외부에서 호출하는 메서드
    public void OnItemConsumed()
    {
        if (!NetworkManager.Singleton.IsServer) return;

        currentItem = null;
        nextSpawnTime = Time.time + spawnInterval;
    }
}
