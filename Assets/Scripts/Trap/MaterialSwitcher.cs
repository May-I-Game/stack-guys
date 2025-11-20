using UnityEngine;
using Unity.Netcode;

public class MaterialTriggerSwitcher : NetworkBehaviour
{
    public Material normalMat;
    public Material triggerMat;

    private Renderer rend;

    private void Awake()
    {
        rend = GetComponentInChildren<Renderer>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;
        if (!other.CompareTag("Player")) return;

        ChangeMaterialClientRpc(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsServer) return;
        if (!other.CompareTag("Player")) return;

        ChangeMaterialClientRpc(false);
    }

    [ClientRpc]
    void ChangeMaterialClientRpc(bool triggered)
    {
        if (rend == null) return;

        var mats = rend.materials;
        mats[0] = triggered ? triggerMat : normalMat;
        rend.materials = mats;
    }
}
