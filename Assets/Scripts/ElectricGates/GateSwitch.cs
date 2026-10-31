// Farm Chaos - little switch box next to each electric gate.
// Roll up to it and press E / tap USE to open or close that gate (only while the farm has power).
// The lamp shows the fence state: green = powered, red = no power. Powered + closed fences spark now and then.
using UnityEngine;

public class GateSwitch : Interactable
{
    /// <summary>Fired when a player uses a switch here (gate name, now open?). Online play sends it to everyone.</summary>
    public static event System.Action<string, bool> Toggled;

    public ElectricGate gate;
    public string penName = "pen";
    [Header("Look")]
    public Renderer lamp;
    public Material lampOn, lampOff;
    public Transform lever;
    public Material sparkMat;
    [Tooltip("Seconds between little sparks on a powered fence (random in this range)")]
    public Vector2 sparkEvery = new Vector2(1.2f, 3.5f);

    Transform spark;
    float sparkTimer, sparkLife;
    float leverAngle;

    void Start()
    {
        sparkTimer = Random.Range(sparkEvery.x, sparkEvery.y);
        if (sparkMat != null)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(g.GetComponent<Collider>());
            g.name = "Spark";
            g.GetComponent<Renderer>().sharedMaterial = sparkMat;
            g.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            g.transform.SetParent(transform, false);
            g.SetActive(false);
            spark = g.transform;
        }
    }

    void Update()
    {
        bool power = ElectricGate.PowerOn;
        bool open = gate != null && gate.IsOpen;

        hintMessage = !power ? $"No power! Fix the power box to work the {penName} gate."
                    : open ? $"Tap USE to close the {penName} gate"
                           : $"Tap USE to open the {penName} gate";

        if (lamp != null)
        {
            var m = power ? lampOn : lampOff;
            if (m != null && lamp.sharedMaterial != m) lamp.sharedMaterial = m;
            // gentle pulse while powered
            float s = power ? 1f + Mathf.Sin(Time.time * 3f) * 0.08f : 1f;
            lamp.transform.localScale = Vector3.one * 0.16f * s;
        }

        if (lever != null)
        {
            float target = open ? -40f : 40f;
            leverAngle = Mathf.MoveTowards(leverAngle, target, 300f * Time.deltaTime);
            lever.localRotation = Quaternion.Euler(leverAngle, 0f, 0f);
        }

        // little sparks on the wires = "this fence is live"
        if (spark != null)
        {
            if (sparkLife > 0f)
            {
                sparkLife -= Time.deltaTime;
                spark.localScale = Vector3.one * Mathf.Lerp(0.02f, 0.14f, Mathf.Sin(Mathf.Clamp01(sparkLife / 0.16f) * Mathf.PI));
                if (sparkLife <= 0f) spark.gameObject.SetActive(false);
            }
            else if (power && !open && gate != null && gate.wires.Count > 0)
            {
                sparkTimer -= Time.deltaTime;
                if (sparkTimer <= 0f)
                {
                    sparkTimer = Random.Range(sparkEvery.x, sparkEvery.y);
                    var w = gate.wires[Random.Range(0, gate.wires.Count)];
                    if (w != null && w.gameObject.activeInHierarchy)
                    {
                        var b = w.bounds;
                        spark.position = Vector3.Lerp(b.min, b.max, Random.value);
                        spark.position = new Vector3(spark.position.x, b.center.y, spark.position.z);
                        spark.gameObject.SetActive(true);
                        sparkLife = 0.16f;
                    }
                }
            }
        }
    }

    public override void Interact()
    {
        if (gate == null || !ElectricGate.PowerOn) return;
        gate.Toggle();
        Toggled?.Invoke(gate.name, gate.IsOpen);
    }
}
