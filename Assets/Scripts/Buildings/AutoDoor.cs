// Farm Chaos - door that opens when the player comes close and closes when they leave.
// Swing doors open AWAY from the player; slide doors slide sideways.
// Other scripts can lock it:  GetComponent<AutoDoor>().locked = true;
using System.Collections.Generic;
using UnityEngine;

public class AutoDoor : MonoBehaviour
{
    public enum Mode { Swing, Slide }

    public Mode mode = Mode.Swing;
    [Tooltip("Swing: hinge pivots. Slide: the sliding panels.")]
    public Transform[] leaves;
    [Tooltip("Swing: +1 for a left-hand leaf, -1 for a right-hand leaf. Slide: direction along local X.")]
    public float[] leafSigns;
    public float openAngle = 95f;
    public float slideDistance = 2.5f;
    public float openSpeed = 3f;
    public bool locked;
    public string playerTag = "Player";

    readonly HashSet<Collider> inside = new HashSet<Collider>();
    Quaternion[] closedRot;
    Vector3[] closedPos;
    float t;          // 0 = closed, 1 = open
    float swingSign = 1f;

    public bool IsOpen => t > 0.5f;

    void Awake()
    {
        closedRot = new Quaternion[leaves.Length];
        closedPos = new Vector3[leaves.Length];
        for (int i = 0; i < leaves.Length; i++)
        {
            closedRot[i] = leaves[i].localRotation;
            closedPos[i] = leaves[i].localPosition;
        }
    }

    bool IsPlayer(Collider c) =>
        c.CompareTag(playerTag) || (c.attachedRigidbody != null && c.attachedRigidbody.CompareTag(playerTag));

    void OnTriggerEnter(Collider c)
    {
        if (!IsPlayer(c)) return;
        if (inside.Count == 0 && t < 0.05f)
        {
            // player outside (in front of the door) -> swing inwards, and vice-versa
            float side = Vector3.Dot(c.transform.position - transform.position, transform.forward);
            swingSign = side > 0f ? -1f : 1f;
        }
        inside.Add(c);
    }

    void OnTriggerExit(Collider c) { inside.Remove(c); }

    void Update()
    {
        inside.RemoveWhere(c => c == null || !c.gameObject.activeInHierarchy);
        float target = (inside.Count > 0 && !locked) ? 1f : 0f;
        t = Mathf.MoveTowards(t, target, openSpeed * Time.deltaTime);
        float e = Mathf.SmoothStep(0f, 1f, t);
        for (int i = 0; i < leaves.Length; i++)
        {
            float s = (leafSigns != null && i < leafSigns.Length) ? leafSigns[i] : 1f;
            if (mode == Mode.Swing)
                leaves[i].localRotation = closedRot[i] * Quaternion.Euler(0f, -s * swingSign * openAngle * e, 0f);
            else
                leaves[i].localPosition = closedPos[i] + Vector3.right * s * slideDistance * e;
        }
    }
}
