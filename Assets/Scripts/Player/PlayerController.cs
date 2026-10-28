using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float speed = 5f;
    public float mouseSensitivity = 2f;
    public VirtualJoystick joystick;

    private CharacterController cc;
    private float pitch = 0f;

    void Start() 
    { 
        cc = GetComponent<CharacterController>(); 
        //Cursor.lockState = CursorLockMode.Locked;
        //Cursor.visible = false;
    }

    void Update()
    {
        bool isOverUI = UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();

        if (!isOverUI)
        {

            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
            
            transform.Rotate(0, mouseX, 0);
            pitch -= mouseY;
            pitch = Mathf.Clamp(pitch, -80f, 80f);
            
            if (Camera.main != null)
                Camera.main.transform.localEulerAngles = new Vector3(pitch, 0, 0);
        }


        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        if (joystick != null && joystick.inputDirection.magnitude > 0.1f)
        {
            h = joystick.inputDirection.x;
            v = joystick.inputDirection.y;
        }

        Vector3 move = transform.right * h + transform.forward * v;
        
        if (cc != null)
            cc.Move(move * speed * Time.deltaTime);
    }
}