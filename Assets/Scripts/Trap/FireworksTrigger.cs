using UnityEngine;
using Unity.Netcode; // 사용하는 네트워크 솔루션의 네임스페이스를 추가하세요.

public class FireworksTrigger : NetworkBehaviour // NetworkBehaviour 상속 필수
{
    // 1. AudioSource 컴포넌트를 연결할 변수
    [SerializeField] private AudioSource sfxAudioSource;

    // 2. firework 오디오 클립을 연결할 변수
    // (PlayOneShot을 사용하기 위해 AudioSource가 아닌 AudioClip이 필요합니다.)
    [SerializeField] private AudioClip fireworkClip;

    // 💡 모든 ParticleSystem을 배열로 연결합니다. (이전에 했던 방식)
    public ParticleSystem[] fireworksParticleSystems;

    // (1) 서버에서만 충돌 감지 (Server-Authoritative)
    private void OnTriggerEnter(Collider other)
    {
        // 서버에서만 실행되도록 체크
        if (!IsServer) return;

        // 플레이어 태그 확인 (플레이어에 NetworkObject가 붙어 있어야 함)
        if (other.CompareTag("Player"))
        {
            // (2) 서버가 충돌 감지 후, 모든 클라이언트에게 재생 명령을 보냅니다.
            PlayFireworksClientRpc();

            // (선택 사항) 서버에서도 바로 재생
            PlayFireworksLocally();
        }
    }

    // 로컬에서 불꽃놀이를 재생하는 함수
    private void PlayFireworksLocally()
    {
        foreach (ParticleSystem ps in fireworksParticleSystems)
        {
            if (ps != null)
            {
                ps.Play();
            }
        }
    }

    // (3) ClientRpc: 서버가 호출하면 모든 클라이언트가 이 함수를 실행합니다.
    // RequireOwnership = false를 설정해야 소유권 없는 객체도 제어 가능
    [ClientRpc]
    private void PlayFireworksClientRpc(ClientRpcParams clientRpcParams = default)
    {
        // 모든 클라이언트가 이 명령을 받아 각자 불꽃놀이를 재생합니다.
        PlayFireworksLocally();
        PlayFireworkSound();
    }
    /// <summary>
    /// 특정 이벤트 발생 시 불꽃놀이 소리를 재생하는 함수
    /// </summary>
    public void PlayFireworkSound()
    {
        // PlayOneShot을 사용하면 현재 재생 중인 다른 소리가 있더라도 겹쳐서 재생됩니다.
        // 불꽃놀이처럼 '이벤트성 짧은 소리'에 적합합니다.
        sfxAudioSource.PlayOneShot(fireworkClip);
    }

}