using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

public class Bomb : InteractiveItem
{
    [Header("Bomb Settings")]
    public float explosionRadius = 3f;
    public float explosionForce = 10f;
    public GameObject explosionEffectPrefab; // 폭발 이펙트 프리팹

    [Header("Audio")]
    public AudioSource bombAudioSource; // 폭탄 오디오 소스
    public AudioClip explosionClip; // 폭탄 폭발 사운드
    [Range(0f, 1f)] public float explosionVolume = 0.8f; // 폭발 볼륨

    private Floating floatingComponent; // Floating 컴포넌트 참조
    private NetworkTransform networkTransform; // NetworkTransform 컴포넌트 참조

    protected override void OnCollisionEnter(Collision collision)
    {
        //Debug.Log($"[Bomb] 충돌 감지! 대상: {collision.gameObject.name}");
        //Debug.Log($"[Bomb] IsGrabbed: {IsGrabbed}, wasThrown 확인 필요");

        base.OnCollisionEnter(collision);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Floating 컴포넌트 가져오기
        floatingComponent = GetComponent<Floating>();

        // NetworkTransform 컴포넌트 가져오기
        networkTransform = GetComponent<NetworkTransform>();

        // 스포너에 있을 때는 NetworkTransform 비활성화
        if (networkTransform != null)
        {
            networkTransform.enabled = false;
        }

        // 서버만 물리 활성화 (권위 서버 모델)
        if (IsServer)
        {
            if (Rb != null)
            {
                Rb.isKinematic = false;  // 서버: 물리 계산
                Rb.useGravity = true;

                // 폭탄 생성 시 위치 고정 (잡을 때까지)
                Rb.constraints = RigidbodyConstraints.FreezePosition;
            }
        }
        else
        {
            // 클라이언트는 Kinematic (NetworkTransform으로 위치만 받음)
            if (Rb != null)
            {
                Rb.isKinematic = true;
            }
        }
    }

    public override void OnGrabbed(PlayerController player)
    {
        // Floating 효과 중지 (먼저 실행)
        if (floatingComponent != null)
        {
            floatingComponent.StopFloating();
        }

        // NetworkTransform 먼저 활성화 (로컬에서 즉시)
        if (networkTransform != null)
        {
            networkTransform.enabled = true;
        }

        // 서버에서 위치 제약 해제 및 동기화
        if (IsServer)
        {
            if (Rb != null)
            {
                Rb.constraints = RigidbodyConstraints.None;
            }

            // 모든 클라이언트에 NetworkTransform 활성화 동기화
            EnableNetworkTransformClientRpc();
        }

        base.OnGrabbed(player);
    }

    [ClientRpc]
    private void EnableNetworkTransformClientRpc()
    {
        // 모든 클라이언트에서 NetworkTransform 활성화
        if (networkTransform != null)
        {
            networkTransform.enabled = true;
        }
    }

    protected override void ActivateItem()
    {
        // 1. 시각 효과 및 사운드 (ClientRPC로 모든 클라이언트에게 재생)
        SpawnEffectClientRpc(transform.position);

        // 2. 범위 내 물리 효과 (서버 처리)
        Collider[] colliders = Physics.OverlapSphere(transform.position, explosionRadius);
        //Debug.Log($"[Bomb] 범위 내 충돌체 {colliders.Length}개 감지");

        foreach (Collider hit in colliders)
        {
            // 플레이어 넉백 처리
            PlayerController pc = hit.GetComponent<PlayerController>();
            if (pc != null)
            {
                // 범위 내에서 던진 사람은 충격파 제외
                if (thrower != null && pc == thrower)
                {
                    continue;
                }

                // 무적 버프가 있으면 넉백 효과 무시
                PlayerBuffManager buffManager = pc.GetComponent<PlayerBuffManager>();
                if (buffManager != null && buffManager.IsInvincible)
                {
                    continue;
                }

                // PlayerController에 넉백 함수가 없다면 Rigidbody에 직접 가함
                Rigidbody pcRb = pc.GetComponent<Rigidbody>();
                if (pcRb != null && !pcRb.isKinematic)
                {
                    pcRb.AddExplosionForce(explosionForce, transform.position, explosionRadius, 1f, ForceMode.Impulse);
                    //Debug.Log($"[Bomb] 충격파 적용: {pc.gameObject.name}");
                }
            }
        }

        // 3. 부모 클래스의 로직 실행 (Despawn 등)
        base.ActivateItem();
    }

    [ClientRpc]
    private void SpawnEffectClientRpc(Vector3 pos)
    {
        // 폭발 이펙트 생성
        if (explosionEffectPrefab != null)
        {
            Instantiate(explosionEffectPrefab, pos, Quaternion.identity);
        }

        // 폭발 사운드 재생 (이펙트와 별도로 재생)
        if (explosionClip != null)
        {
            // 3D 공간 사운드로 재생
            AudioSource.PlayClipAtPoint(explosionClip, pos, explosionVolume * GetSFXVolume());
        }
    }

    private float GetSFXVolume()
    {
        return GameManager.Instance != null ? GameManager.Instance.GetSFXVolume() : 1f;
    }

}
