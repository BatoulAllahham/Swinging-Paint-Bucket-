using UnityEngine;
using Seb.Fluid.Simulation;

/// <summary>
/// Attach to the Rack GameObject alongside the Pendulum component.
/// At startup, finds all FluidSim children and auto-sizes the Box Collider
/// to contain all of them. Add or remove bucket children in the hierarchy
/// and the collider updates automatically on next Play.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class BucketRack : MonoBehaviour
{
    [Tooltip("Extra padding added around the buckets on each axis")]
    public Vector3 padding = new Vector3(0.5f, 0.5f, 0.5f);

    void Start()
    {
        FitColliderToBuckets();
    }

    void FitColliderToBuckets()
    {
        // Gather all FluidSim components in children (not on this object itself)
        FluidSim[] buckets = GetComponentsInChildren<FluidSim>();

        if (buckets.Length == 0)
        {
            Debug.LogWarning("BucketRack: no FluidSim children found — collider not resized.");
            return;
        }

        // Calculate bounds in local space by converting each bucket's
        // world position into this Rack's local space
        Vector3 min = Vector3.positiveInfinity;
        Vector3 max = Vector3.negativeInfinity;

        foreach (FluidSim bucket in buckets)
        {
            // Use the bucket's own scale as a rough size proxy
            Vector3 localPos = transform.InverseTransformPoint(bucket.transform.position);
            Vector3 halfSize = bucket.transform.localScale * 0.5f;

            min = Vector3.Min(min, localPos - halfSize);
            max = Vector3.Max(max, localPos + halfSize);
        }

        // Apply padding
        min -= padding;
        max += padding;

        // Set the Box Collider
        BoxCollider col = GetComponent<BoxCollider>();
        col.center = (min + max) * 0.5f;
        col.size   = max - min;

        Debug.Log($"BucketRack: fitted collider to {buckets.Length} bucket(s) " +
                  $"— center={col.center}, size={col.size}");
    }

    // Draws a wire cube in the Scene view so you can see the rack bounds
    void OnDrawGizmosSelected()
    {
        BoxCollider col = GetComponent<BoxCollider>();
        if (col == null) return;

        Gizmos.color = new Color(1f, 0.8f, 0f, 0.5f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(col.center, col.size);
    }
}