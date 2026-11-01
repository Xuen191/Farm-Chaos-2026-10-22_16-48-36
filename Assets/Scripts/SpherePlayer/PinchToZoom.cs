using UnityEngine;

public class PinchToZoom : MonoBehaviour
{
    [Header("Zoom Settings")]
    public float minDistance = 4f;   
    public float maxDistance = 15f;   
    public float zoomSpeed = 0.05f; 

    private Camera cam;
    private SpherePlayer player;     

    void Start()
    {
        cam = GetComponent<Camera>();
        player = FindAnyObjectByType<SpherePlayer>();
    }

    void Update()
    {
        if (player == null) return;

        if (Input.touchCount == 2)
        {
            Touch touchZero = Input.GetTouch(0);
            Touch touchOne = Input.GetTouch(1);

            Vector2 touchZeroPrevPos = touchZero.position - touchZero.deltaPosition;
            Vector2 touchOnePrevPos = touchOne.position - touchOne.deltaPosition;
            float prevMagnitude = (touchZeroPrevPos - touchOnePrevPos).magnitude;

            float currentMagnitude = (touchZero.position - touchOne.position).magnitude;

            float difference = currentMagnitude - prevMagnitude;

            ZoomCamera(difference * zoomSpeed);
        }
    }

    void ZoomCamera(float increment)
    {
        float currentDistance = Vector3.Distance(cam.transform.position, player.transform.position);

        float newDistance = currentDistance - increment;

        newDistance = Mathf.Clamp(newDistance, minDistance, maxDistance);

        Vector3 direction = (cam.transform.position - player.transform.position).normalized;
        cam.transform.position = player.transform.position + direction * newDistance;
    }
}