using UnityEngine;

public class PlayerTools : MonoBehaviour
{
    public static PlayerTools instance;

    public bool hasRope = false;
    public bool hasStick = false;

    void Awake()
    {
        if (instance == null) instance = this;
    }
}