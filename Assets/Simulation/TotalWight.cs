using UnityEngine;

public class TotalWeightManager : MonoBehaviour
{
    public float totalCombinedWeight = 0f;
    private Seb.Fluid.Simulation.FluidSim[] allBuckets; // Array of main sim scripts

    void Start()
    {
        // Find every bucket in the scene automatically
        allBuckets = FindObjectsByType<Seb.Fluid.Simulation.FluidSim>(FindObjectsSortMode.None);
    }

    void Update()
    {
        float tempWeight = 0f;
        foreach (var bucket in allBuckets)
        {
            tempWeight += bucket.currentBucketWeight;
        }
        totalCombinedWeight = tempWeight;
    }
}