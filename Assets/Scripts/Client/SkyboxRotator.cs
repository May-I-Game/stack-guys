using UnityEngine;

public class SkyboxRotator : MonoBehaviour
{
    public float ratationSpeed = 1.0f;

    private const string RotationPropertyName = "_Rotation";

    void Update()
    {
        float rotation = Time.time * ratationSpeed;

        RenderSettings.skybox.SetFloat(RotationPropertyName, rotation);
    }
}
