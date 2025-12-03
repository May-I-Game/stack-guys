using UnityEngine;

public class SkyboxRotator : MonoBehaviour
{
    // 여기서 rotationSpeed의 선언부를 확인하세요.
    // 반드시 float 값 뒤에 'f'를 붙여야 합니다.
    public float rotationSpeed = 1.0f; // 정확히 이렇게 입력해야 합니다.

    // 스카이박스 머티리얼의 Rotation 속성 이름
    private const string RotationPropertyName = "_Rotation";

    void Update()
    {
        float rotation = Time.time * rotationSpeed;

        RenderSettings.skybox.SetFloat(RotationPropertyName, rotation);
    }
}