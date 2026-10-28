#if UNITY_EDITOR
// Farm Chaos - remembers the original daytime lighting so "Lighting: Day" can restore it.
using UnityEngine;

public class FarmDaySnapshot : ScriptableObject
{
    public Color sunColor; public float sunIntensity; public Quaternion sunRotation;
    public int ambientMode; public Color ambientSky, ambientEquator, ambientGround, ambientLight; public float ambientIntensity;
    public bool fog;
    public int camClearFlags; public Color camBackground; public bool camPostProcessing;
    public Material skybox;
}
#endif
