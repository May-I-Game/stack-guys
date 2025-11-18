using UnityEngine;

public class CameraCulling : MonoBehaviour
{
    public float bodyCullingDistance = 20f;
    public float playerCullingDistance = 30f;

    void Start()
    {
        Camera cam = GetComponent<Camera>();
        if (cam == null) return;

        float[] distances = new float[32];
        distances[6] = playerCullingDistance;  // Layer 6 (Player)
        distances[10] = bodyCullingDistance; // Layer 10 (DeadBody)
        cam.layerCullDistances = distances;
    }
}
