using UnityEngine;

public class Gate : MonoBehaviour
{
    public Transform openPos;
    public Transform closedPos;
    public float speed = 2f;
    private bool shouldClose = false;

    public void CloseGate() { shouldClose = true; }

    void Update()
    {
        Vector3 target = shouldClose ? closedPos.position : openPos.position;
        transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
    }
}