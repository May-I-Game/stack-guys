using Unity.Netcode;

// 데이터 종류 + 인터페이스

// 버프의 종류
public enum BuffType
{
    None = 0,           // 버프 없음
    Speed = 1,          // 속도 증가
    Jump = 2,           // 점프력 증가
    Invincibility = 3   // 무적 (잡기 무시)
}

// 버프 상태를 나타내는 데이터
[System.Serializable]
public struct BuffData : INetworkSerializable
{
    public BuffType type;           // 버프 종류
    public float value;             // 버프 값
    public float duration;          // 지속 시간

    // 버프 데이터 직렬화
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref type);
        serializer.SerializeValue(ref value);
        serializer.SerializeValue(ref duration);
    }
}

// 버프를 받을 수 있는 플레이어가 구현해야 하는 인터페이스
public interface IBuffable
{
    // 버프를 적용 (서버에서만 호출)
    void ApplyBuff(BuffData data);

    // 특정 버프를 제거 (서버에서만 호출)
    void RemoveBuff(BuffType type);

    // 특정 버프 활성화 확인
    bool HasBuff(BuffType type);
}