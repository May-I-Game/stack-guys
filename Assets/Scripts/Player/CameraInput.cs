using Unity.Cinemachine;
using UnityEngine;

public class CameraInput : MonoBehaviour
{
    CinemachineInputAxisController camInput;

    void Start()
    {
        camInput = GetComponent<CinemachineInputAxisController>();
    }

    void Update()
    {
        if (Input.GetKey(KeyCode.Mouse0) || Input.touchCount > 0)
        {
            camInput.enabled = true;
        }
        else
        {
            camInput.enabled = false;
        }
    }
}
