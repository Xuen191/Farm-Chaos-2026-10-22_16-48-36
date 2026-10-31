using UnityEngine;

public class Billboard : MonoBehaviour
{
    void Update()
    {
        // Makes the "!" always look at the player's camera
        if (Camera.main != null)
            transform.LookAt(transform.position + Camera.main.transform.rotation * Vector3.forward,
                             Camera.main.transform.rotation * Vector3.up);
    }
}