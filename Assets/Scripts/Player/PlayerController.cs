using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float speed = 5f;
    public float mouseSensitivity = 2f;
    private CharacterController cc;
    private float pitch = 0f;

    void Start() { cc = GetComponent<CharacterController>(); }

    void Update()
    {
        // Look
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
        transform.Rotate(0, mouseX, 0);
        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, -80f, 80f);
        Camera.main.transform.localEulerAngles = new Vector3(pitch, 0, 0);

        // Move
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        Vector3 move = transform.right * h + transform.forward * v;
        cc.Move(move * speed * Time.deltaTime);
    }
}