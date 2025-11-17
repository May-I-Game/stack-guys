using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// 아이템 자동 스폰 시스템
public class ItemSpawner : NetworkBehaviour
{
    [System.Serializable]
    public class SpawnableItem
    {
        // 스폰할 아이템 프리팹
        public NetworkObject itemPrefab;

        // 스폰 가중치 (1 ~ 10f)
        [Range(1f, 10f)]
        public float spawnWeight = 1f;
    }

    // 스폰할 아이템 리스트 (랜덤 선택)
    [Header("Spawn Settings")]
    public List<SpawnableItem> spawnableItems = new List<SpawnableItem>();

    // 스폰 간격 (초)
    public float spawnInterval = 10f;

    // 스폰 위치 Transform (없으면 이 오브젝트 위치)
    public Transform spawnPoint;

    // 아이템이 바닥에서 띄워진 거리
    public float spawnHeightOffset = 0.3f;

    [Header("Debug")]
    public bool enableDebugLog = true;

    // 현재 스폰된 아이템 (서버만 관리)
    private NetworkObject currentItem = null;

    // 다음 스폰 시간 (서버만 관리)
    private float nextSpawnTime = 0f;

    private void Update()
    {
        // 서버에서만 스폰 처리
        if (!IsServer) return;

        // 스폰 시간이 되었고, 현재 아이템이 없을 때
        if (Time.time >= nextSpawnTime && currentItem == null)
        {
            SpawnRandomItem();
            nextSpawnTime = Time.time + spawnInterval;
        }

        // 현재 아이템이 파괴되었는지 체크
        if (currentItem != null && !currentItem.IsSpawned)
        {
            currentItem = null;
        }
    }

    // 랜덤 아이템 스폰 (가중치 기반)
    private void SpawnRandomItem()
    {
        // 스폰 가능한 아이템이 없으면 무시
        if (spawnableItems == null || spawnableItems.Count == 0)
        {
            Debug.LogWarning("[ItemSpawner] spawnableItems 리스트 없음");
            return;
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
            Debug.LogWarning("[ItemSpawner] 유효한 아이템 없음");
            return;
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
                // 아이템 띄우기
                Vector3 basePos = spawnPoint != null ? spawnPoint.position : transform.position;

                // 아이템 스폰
                Vector3 spawnPos = basePos + Vector3.up * spawnHeightOffset;
                Quaternion spawnRot = spawnPoint != null ? spawnPoint.rotation : transform.rotation;

                currentItem = Instantiate(item.itemPrefab, spawnPos, spawnRot);
                currentItem.Spawn();

                if (enableDebugLog)
                {
                    Debug.Log($"[ItemSpawner] {item.itemPrefab.name} 스폰됨 at {spawnPos}");
                }

                break;
            }
        }
    }
}
