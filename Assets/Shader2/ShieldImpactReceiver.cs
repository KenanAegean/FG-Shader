using UnityEngine;

public class ShieldImpactReceiver : MonoBehaviour
{
    [SerializeField] Renderer targetRenderer; // assign your shield MeshRenderer in Inspector
    [SerializeField] string[] hitPropNames = { "_Hit1", "_Hit2", "_Hit3", "_Hit4" };

    Material _mat;
    int _next = 0;

    void Awake()
    {
        if (!targetRenderer) targetRenderer = GetComponent<Renderer>();
        _mat = targetRenderer.material; // instanced
    }

    public void AddHit(Vector3 worldHitPos)
    {
        Vector3 local = transform.InverseTransformPoint(worldHitPos);
        Vector4 packed = new Vector4(local.x, local.y, local.z, Time.time);
        _mat.SetVector(hitPropNames[_next], packed);
        _next = (_next + 1) % hitPropNames.Length;
    }
}
