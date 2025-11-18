using Unity.Netcode;
using UnityEngine;

public enum ItemTriggerType
{
    UseOnGrab,   // 잡는 순간 바로 사용
    UseOnImpact  // 던져서 어딘가에 닿으면 사용
}

public abstract class InteractiveItem : GrabbableObject
{
    [Header("Item Settings")]
    [SerializeField] private ItemTriggerType triggerType;

    private bool wasThrown = false;
    protected PlayerController thrower;

    // 아이템을 스폰한 스포너 (스폰 시 설정됨)
    public ItemSpawner SourceSpawner { get; set; }

    public override void OnGrabbed(PlayerController player)
    {
        base.OnGrabbed(player);

        //Debug.Log($"[InteractiveItem] OnGrabbed 호출! Player: {player.gameObject.name}, IsServer: {NetworkManager.Singleton.IsServer}, triggerType: {triggerType}");

        if (!NetworkManager.Singleton.IsServer) return;
        wasThrown = false;
        thrower = null;

        // 잡자마자 사용하는 타입이면 바로 발동
        if (triggerType == ItemTriggerType.UseOnGrab)
        {
            ActivateItem();
        }
        else
        {
            Debug.Log($"[InteractiveItem] UseOnGrab이 아님. triggerType: {triggerType}");
        }
    }

    public override void OnThrown()
    {
        // 들고 있는 사람을 던진 사람으로 저장
        thrower = Holder;
        wasThrown = true;

        base.OnThrown();

        //Debug.Log($"[InteractiveItem] OnThrown 호출됨! wasThrown: {wasThrown}, thrower: {(thrower != null ? thrower.gameObject.name : "null")}");
    }

    protected virtual void OnCollisionEnter(Collision collision)
    {
        if (!NetworkManager.Singleton.IsServer) return;

        //Debug.Log($"[InteractiveItem] triggerType: {triggerType}, wasThrown: {wasThrown}");

        // 던져진 상태이고 + UseOnImpact 아이템이라면
        if (triggerType == ItemTriggerType.UseOnImpact && wasThrown)
        {
            // Debug.Log("[InteractiveItem] 조건 통과! ActivateItem 호출");

            // 던진 사람과 충돌시 터지지 않음
            if (thrower != null && collision.gameObject == thrower.gameObject) return;

            ActivateItem();
        }
        else
        {
            //Debug.Log($"[InteractiveItem] 조건 실패 - triggerType: {triggerType}, wasThrown: {wasThrown}");
        }
    }

    // 실제 아이템 효과 (자식 클래스에서 구현)
    protected virtual void ActivateItem()
    {
        if (!IsSpawned) return;
        //Debug.Log($"[Item] {gameObject.name} 사용됨!");

        // 스포너에게 아이템이 사용되었음을 알림
        if (SourceSpawner != null)
        {
            SourceSpawner.OnItemConsumed();
        }

        // 플레이어 릴리즈 그랩
        if (Holder != null)
        {
            Holder.ReleaseGrab();
        }

        GetComponent<NetworkObject>().Despawn();
    }
}
