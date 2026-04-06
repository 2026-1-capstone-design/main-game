using UnityEngine;
using UnityEngine.InputSystem; 

public class CameraView : MonoBehaviour
{
    public float rotationSpeed = 60f;
    public float zoomSpeed = 10f;

    private float _pitch;
    private float _yaw;

    private void Start()
    {
        _pitch = transform.eulerAngles.x;
        _yaw = transform.eulerAngles.y;
    }

    private void Update()
    {
        float horizontal = 0f;
        float vertical = 0f;
        float scroll = 0f;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed) vertical += 1f;
            if (Keyboard.current.sKey.isPressed) vertical -= 1f;
            if (Keyboard.current.dKey.isPressed) horizontal += 1f;
            if (Keyboard.current.aKey.isPressed) horizontal -= 1f;
        }

        if (Mouse.current != null)
        {
            scroll = Mouse.current.scroll.ReadValue().y * 0.01f;
        }

        _yaw += horizontal * rotationSpeed * Time.deltaTime;
        _pitch -= vertical * rotationSpeed * Time.deltaTime;

        _pitch = Mathf.Clamp(_pitch, 5f, 85f);

        transform.eulerAngles = new Vector3(_pitch, _yaw, 0f);

        if (Mathf.Abs(scroll) > 0.001f)
        {
            transform.Translate(Vector3.forward * scroll * zoomSpeed, Space.Self);
        }
    }
}