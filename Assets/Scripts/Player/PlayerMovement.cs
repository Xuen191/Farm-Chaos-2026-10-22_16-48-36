using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    private CharacterController controller;
    private Animator animator;
    
    public Transform cam;             // 主摄像机
    public float walkSpeed = 4f;      // 走路速度
    public float runSpeed = 7f;       // 跑步速度
    public float jumpHeight = 1.5f;   // 跳跃高度
    public float gravity = -19.62f;   // 重力加速度（给两倍重力会让跳跃手感更干脆）
    
    public float turnSmoothTime = 0.1f; 
    float turnSmoothVelocity;

    private Vector3 velocity;
    private bool isGrounded;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();

        if (cam == null && Camera.main != null)
        {
            cam = Camera.main.transform;
        }
    }

    void Update()
    {
        // 1. 地面检测（防止角色卡在空中或无限下落）
        isGrounded = controller.isGrounded;
        Debug.Log("Is Grounded: " + isGrounded + " | Velocity Y: " + velocity.y);
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f; 
        }

        float horizontal = Input.GetAxisRaw("Horizontal"); 
        float vertical = Input.GetAxisRaw("Vertical");    
        Vector3 direction = new Vector3(horizontal, 0f, vertical).normalized;

        // 2. 检测是否按住 Shift 键跑步
        bool isRunning = Input.GetKey(KeyCode.LeftShift) && direction.magnitude >= 0.1f;
        float currentSpeed = isRunning ? runSpeed : walkSpeed;

        // 传给 Animator 的速度计算
        if (animator != null)
        {
            float targetAnimSpeed = 0f;

            if (direction.magnitude >= 0.1f)
            {
                // 走路传 1f，跑步传 1.3f（你可以根据动画实际情况微调这个 1.3f）
                targetAnimSpeed = isRunning ? 1.3f : 1f; 
            }

            // 直接赋值，松手时立刻干净利落地归零 (Idle)
            animator.SetFloat("Speed", targetAnimSpeed);
        }

        if (direction.magnitude >= 0.1f)
        {
            // 计算旋转和移动
            float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + cam.eulerAngles.y;
            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnSmoothVelocity, turnSmoothTime);
            transform.rotation = Quaternion.Euler(0f, angle, 0f);

            Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
            controller.Move(moveDir.normalized * currentSpeed * Time.deltaTime);
        }

        // 3. 跳跃逻辑（按下空格键 Space）
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            
            // 如果你后面配置了跳跃/落地动画，可以取消下面这行的注释
            animator.SetTrigger("Jump");
        }

        // 4. 应用重力并向下移动
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}