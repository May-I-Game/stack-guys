using UnityEngine;

public interface IBatchSyncObject
{
    // NetworkObject 컴포넌트가 붙어있어야 합니다.
    Transform Transform { get; }

    // 서버: 마지막 동기화 상태 관리
    bool GetLastSyncedState(out Vector3 lastPos, out float lastRotY);
    void SetLastSyncedState(Vector3 currentPos, float currentRotY);

    // 클라이언트: 수신한 목표 상태 업데이트
    void UpdateTargetState(Vector3 pos, float rotY);
}
