using System.Collections;
using UnityEngine;

namespace EasyPrimitiveAnimals
{
    public class AnimalController : MonoBehaviour
    {
        // Leg and body object variables
        public GameObject FrontLegL;
        public GameObject FrontLegR;
        public GameObject RearLegL;
        public GameObject RearLegR;

        // Leg and body rotation variables
        private Vector3 legStartPosA = new Vector3(10.0f, 0f, 0f);
        private Vector3 legEndPosA = new Vector3(-10.0f, 0f, 0f);

        private Vector3 legStartPosB = new Vector3(-10.0f, 0f, 0f);
        private Vector3 legEndPosB = new Vector3(10.0f, 0f, 0f);

        private float rotSpeed;

        // 移动与摄像机变量
        public Transform cam;             
        public float movSpeed = 4f;       
        public float turnSmoothTime = 0.1f; 
        float turnSmoothVelocity;

        private bool canPeck = true;
        private bool isDead = false; // 记录动物是否已经死亡

        private void Start()
        {
            // 自动寻找主摄像机
            if (cam == null && Camera.main != null)
            {
                cam = Camera.main.transform;
            }

            if (!this.gameObject.CompareTag("Chicken"))
            {
                // Ensure child objects of legs are named EPA_FL, EPA_FR, EPA_RL and EPA_RR so the searches below can assign them to leg variables.
                FrontLegL = transform.Find("BaseAnimal").transform.Find("Legs").transform.Find("EPA_FL").gameObject;    
                FrontLegR = transform.Find("BaseAnimal").transform.Find("Legs").transform.Find("EPA_FR").gameObject;    
                RearLegL = transform.Find("BaseAnimal").transform.Find("Legs").transform.Find("EPA_RL").gameObject;     
                RearLegR = transform.Find("BaseAnimal").transform.Find("Legs").transform.Find("EPA_RR").gameObject;     

                rotSpeed = movSpeed * 4; 
            }
        }

private void Update()
{
    if (isDead) return;

    if (Input.GetKeyDown(KeyCode.K))
    {
        Die();
        return;
    }

    // 1. 获取 WASD 输入（所有动物通用，包括鸡）
    float horizontal = Input.GetAxisRaw("Horizontal");
    float vertical = Input.GetAxisRaw("Vertical");
    Vector3 direction = new Vector3(horizontal, 0f, vertical).normalized;

    // 2. 所有动物都可以通过 WASD 移动和旋转
    if (direction.magnitude >= 0.1f)
    {
        float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + cam.eulerAngles.y;
        float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnSmoothVelocity, turnSmoothTime);
        transform.rotation = Quaternion.Euler(0f, angle, 0f);

        Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
        transform.Translate(moveDir.normalized * movSpeed * Time.deltaTime, Space.World);

        // 如果是四足动物，移动时摆动腿部
        if (!this.gameObject.CompareTag("Chicken"))
        {
            float lerp = 0.5f * (1.0f + Mathf.Sin(Mathf.PI * Time.time * this.rotSpeed));
            FrontLegL.transform.localRotation = Quaternion.Lerp(Quaternion.Euler(legStartPosA), Quaternion.Euler(legEndPosA), lerp);
            FrontLegR.transform.localRotation = Quaternion.Lerp(Quaternion.Euler(legStartPosB), Quaternion.Euler(legEndPosB), lerp);
            RearLegL.transform.localRotation = Quaternion.Lerp(Quaternion.Euler(legStartPosB), Quaternion.Euler(legEndPosB), lerp);
            RearLegR.transform.localRotation = Quaternion.Lerp(Quaternion.Euler(legStartPosA), Quaternion.Euler(legEndPosA), lerp);
        }
    }
    else
    {
        // 没按键移动时，如果是鸡，并且停下来了，就随机触发啄米
        if (this.gameObject.CompareTag("Chicken"))
        {
            if (Random.Range(0, 100) > 50 && canPeck)
            {
                StartCoroutine(TimeToPeck());
            }
        }
        else // 四足动物没动时复位腿部
        {
            FrontLegL.transform.localRotation = Quaternion.Euler(legStartPosA);
            FrontLegR.transform.localRotation = Quaternion.Euler(legStartPosB);
            RearLegL.transform.localRotation = Quaternion.Euler(legStartPosB);
            RearLegR.transform.localRotation = Quaternion.Euler(legStartPosA);
        }
    }
}

        // ========================== 死亡相关方法 ==========================

        // 外部调用此方法（或按 K 键）触发死亡
        public void Die()
        {
            if (isDead) return;
            isDead = true;

            StartCoroutine(SmoothFallAndDie());
        }

        // 平滑倒地协程
        private IEnumerator SmoothFallAndDie()
        {
            // 1. 关闭组件自身的移动控制
            this.enabled = false; 
            
            // 关闭碰撞体防止卡住角色或穿模
            CharacterController cc = GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            Collider col = GetComponent<Collider>();
            if (col != null) col.enabled = false;

            // 2. 计算倒地角度（以 Z 轴旋转 90 度实现侧身倒地）
            Quaternion startRot = transform.rotation;
            Quaternion endRot = startRot * Quaternion.Euler(0f, 0f, 90f); 

            float duration = 0.4f; // 倒地动画持续时间
            float elapsed = 0f;

            while (elapsed < duration)
            {
                transform.rotation = Quaternion.Lerp(startRot, endRot, elapsed / duration);
                elapsed += Time.deltaTime;
                yield return null;
            }
            transform.rotation = endRot; // 确保最后贴合姿态

            // 3. 倒地后保留尸体 3 秒，然后销毁（如果不希望销毁尸体，可以把下面两行删掉）
            yield return new WaitForSeconds(3f);
            Destroy(gameObject);
        }

        // ================================================================

        private IEnumerator TimeToPeck()
        {
            canPeck = false;
            this.transform.eulerAngles = new Vector3(45f, transform.eulerAngles.y, transform.eulerAngles.z);
            yield return new WaitForSeconds(0.2f);
            this.transform.eulerAngles = new Vector3(0f, transform.eulerAngles.y, transform.eulerAngles.z);
            yield return new WaitForSeconds(Random.Range(3f, 7f));
            canPeck = true;
        }
    }
}