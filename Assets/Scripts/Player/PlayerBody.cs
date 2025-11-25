using Unity.Netcode;
using UnityEngine;

public class PlayerBody : NetworkBehaviour, IBatchSyncObject
{
    private Vector3 _lastSyncedPos;
    private float _lastSyncedRotY;

    // 클라이언트 보간용 목표 지점
    private Vector3 _targetPos;
    private float _targetRotY;

    // 보간 속도
    private float _lerpSpeed = 20f;

    // 로컬 모드로 전환되었는지 여부
    private bool _isLocalMode = false;

    public Transform Transform => transform;

    public override void OnNetworkSpawn()
    {
        // 초기값 설정
        _targetPos = transform.position;
        _targetRotY = transform.rotation.eulerAngles.y;

        // 스폰 시 배칭 매니저에 등록 (서버/클라 모두)
        BatchNetworkManager.Instance.RegisterObject(NetworkObjectId, this);
    }

    public override void OnNetworkDespawn()
    {
        // 디스폰 시 안전하게 해제
        BatchNetworkManager.Instance.UnregisterObject(NetworkObjectId);
    }

    private void Update()
    {
        // 로컬 모드거나 서버인 경우 보간 불필요 (서버는 물리/로직으로 이동)
        if (_isLocalMode || IsServer) return;

        // 3. 클라이언트 보간 (NetworkTransform 대체)
        transform.position = Vector3.Lerp(transform.position, _targetPos, Time.deltaTime * _lerpSpeed);

        Quaternion targetRot = Quaternion.Euler(0, _targetRotY, 0);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * _lerpSpeed);
    }

    public bool GetLastSyncedState(out Vector3 lastPos, out float lastRotY)
    {
        lastPos = _lastSyncedPos;
        lastRotY = _lastSyncedRotY;
        return true; // 항상 true (초기화 여부는 로직에 따라 조절 가능)
    }

    public void SetLastSyncedState(Vector3 currentPos, float currentRotY)
    {
        _lastSyncedPos = currentPos;
        _lastSyncedRotY = currentRotY;
    }

    // 클라이언트: 서버에서 받은 위치로 목표 설정
    public void UpdateTargetState(Vector3 pos, float rotY)
    {
        if (_isLocalMode) return; // 로컬 모드면 무시
        _targetPos = pos;
        _targetRotY = rotY;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer) return;

        if (collision.gameObject.CompareTag("Ocean"))
        {
            // 서버에서 먼저 배칭 해제 (더 이상 전송 안 함)
            BatchNetworkManager.Instance.UnregisterObject(NetworkObjectId);

            // 클라이언트들에게 로컬 모드 전환 알림
            ConvertToLocalClientRpc();
        }
    }

    [ClientRpc]
    private void ConvertToLocalClientRpc()
    {
        ConvertToLocal();
    }

    private void ConvertToLocal()
    {
        if (_isLocalMode) return;
        _isLocalMode = true;

        // 클라이언트에서도 배칭 목록에서 제거 (수신 처리 중단)
        if (BatchNetworkManager.Instance != null)
        {
            BatchNetworkManager.Instance.UnregisterObject(NetworkObjectId);
        }

        // 예: 리지드바디가 있다면 키네마틱을 끄고 물리 효과를 줌
        var rb = GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = false;

        Debug.Log($"{this.gameObject}: 배칭 동기화 중단, 로컬 모드로 전환됨");

        // 만약 이 스크립트의 Update(보간)도 완전히 끄고 싶다면:
        this.enabled = false; 
    }
}
