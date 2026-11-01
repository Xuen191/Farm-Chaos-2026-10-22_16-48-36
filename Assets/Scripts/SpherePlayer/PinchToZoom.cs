using UnityEngine;

// Pinch with two fingers to zoom the camera in / out.
// Farm Chaos: the zoom goes through CameraFollow (which places the camera every frame - moving the camera here
// directly got undone straight away). Only counts when BOTH fingers are on the right half of the screen,
// so holding the joystick and tapping GRAB never zooms by accident. Zoom limits live in CameraFollow.
public class PinchToZoom : MonoBehaviour
{
    [Header("Zoom Settings")]
    public float minDistance = 4f;     // (kept so the scene's values still load - CameraFollow sets the real limits)
    public float maxDistance = 15f;
    public float zoomSpeed = 0.05f;

    void Update()
    {
        if (Input.touchCount != 2) return;
        Touch a = Input.GetTouch(0), b = Input.GetTouch(1);
        float half = Screen.width * 0.5f;
        if (a.position.x < half || b.position.x < half) return;      // one finger is on the joystick side

        float prev = ((a.position - a.deltaPosition) - (b.position - b.deltaPosition)).magnitude;
        float now = (a.position - b.position).magnitude;
        CameraFollow.AddZoom((now - prev) * zoomSpeed * 1080f / Mathf.Max(Screen.height, 1));
    }
}
