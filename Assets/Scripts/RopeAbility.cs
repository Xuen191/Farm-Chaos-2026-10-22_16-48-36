using UnityEngine;
using System.Collections;
using TMPro;

public class RopeAbility : MonoBehaviour
{
    [Header("Settings")]
    public float tieRadius = 4f;         
    public float tieDuration = 20f;

    [Header("Visuals & Audio")]
    public Material ropeMaterial;      
    public AudioClip tieSound;           
    public GameObject tiedTextPrefab;    

    private bool hasRope = false;
    private bool isTying = false; 

    public void PickUpRope()
    {
        hasRope = true;
        isTying = false;
    }

    public bool TryTieSpy(out string errorMessage)
    {
        errorMessage = "";

        if (!hasRope)
        {
            errorMessage = "You need to find a rope first!";
            return false; 
        }

        if (isTying) 
        {
            errorMessage = "You are already tying someone!";
            return false; 
        }

        Collider[] allPlayers = Physics.OverlapSphere(transform.position, tieRadius);
        
        Transform targetSpy = null;
        foreach (var col in allPlayers)
        {
            Transform root = col.transform.root; 
            if (root.GetComponent<SpyTag>() != null)
            {
                targetSpy = root;
                break;
            }
        }


        if (targetSpy == null)
        {
            errorMessage = "No spy nearby to tie!";
            return false; 
        }

        
        if (tieSound != null)
            AudioSource.PlayClipAtPoint(tieSound, targetSpy.position);

        StartCoroutine(TieRoutine(targetSpy.gameObject));
        
        return true; 
    }

    private IEnumerator TieRoutine(GameObject target)
    {
        isTying = true;

        GameObject ropeObj = new GameObject("RopeVisual");
        LineRenderer lr = ropeObj.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.startWidth = 0.1f;
        lr.endWidth = 0.1f;
        if (ropeMaterial != null) lr.material = ropeMaterial;
        else lr.material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = new Color(0.4f, 0.2f, 0.1f) };

        GameObject tiedUI = null;
        if (tiedTextPrefab != null)
        {
            tiedUI = Instantiate(tiedTextPrefab, target.transform.position + Vector3.up * 2f, Quaternion.identity);
        }

        Rigidbody rb = target.GetComponent<Rigidbody>();
        if (rb != null) { rb.linearVelocity = Vector3.zero; rb.isKinematic = true; }

        SpherePlayer spyMovement = target.GetComponent<SpherePlayer>();
        if (spyMovement != null) spyMovement.enabled = false;

        float timer = tieDuration;

        while (timer > 0f)
        {
            timer -= Time.deltaTime;

            lr.SetPosition(0, transform.root.position + Vector3.up * 0.5f);
            lr.SetPosition(1, target.transform.position + Vector3.up * 0.5f);

            if (tiedUI != null)
            {
                tiedUI.transform.position = target.transform.position + Vector3.up * 2f;
                var textMesh = tiedUI.GetComponentInChildren<TextMeshPro>();
                if (textMesh != null) textMesh.text = "Tied: " + Mathf.CeilToInt(timer) + "s";
            }

            yield return null;
        }

        if (rb != null) rb.isKinematic = false;
        if (spyMovement != null) spyMovement.enabled = true; 

        Destroy(ropeObj);
        if (tiedUI != null) Destroy(tiedUI);
        
        isTying = false;
        hasRope = true; 
    }
}