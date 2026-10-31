using UnityEngine;

public class HandAnchorLook : MonoBehaviour
{
    void LateUpdate()
    {
        transform.rotation = Quaternion.Euler(0, Camera.main.transform.eulerAngles.y, 0);
    }
}