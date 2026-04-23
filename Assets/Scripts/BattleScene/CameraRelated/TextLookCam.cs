using UnityEngine;

public class TextLookCam : MonoBehaviour
{
    public Transform Cam;

    private void Start()
    {
        if (Camera.main != null)
        {
            Cam = Camera.main.transform;
        }
    }

    void Update()
    {
        if (Cam != null)
            transform.LookAt(transform.position + Cam.forward);
    }
}
