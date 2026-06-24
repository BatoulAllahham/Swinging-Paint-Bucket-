using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Seb.Fluid.Simulation
{
    public class Spawner3D : MonoBehaviour
    {
        public enum SpawnShape { Cylinder, Box }

        [Header("Container Settings")]
        public Transform bucketTransform;
        public SpawnShape bucketShape = SpawnShape.Cylinder;

        [Header("User Workflow Controls")]
        [Range(0.0f, 1.0f)] 
        [Tooltip("0 = Empty, 0.5 = Half Full, 1.0 = Filled to the Brim")]
        public float fillPercentage = 0.5f;
        
        [Range(0.1f, 1.0f)] 
        [Tooltip("Safety margin to keep paint from spawning right inside the bucket walls.")]
        public float wallRadiusMargin = 0.95f;

        [Header("Physics Settings (Set and Forget)")]
        [Tooltip("This MUST match your Fluid Simulation's Target Density setting.")]
        public int simulationTargetDensity = 600;
        public float3 initialVel;
        public float jitterStrength = 0.002f;

        [Header("Debug Info")]
        public int debug_num_particles;

        public SpawnData GetSpawnData()
        {
            if (bucketTransform == null) return default;

            List<float3> pointsList = new List<float3>();
            GeneratePhysicalGrid(pointsList, false);

            float3[] points = pointsList.ToArray();
            float3[] velocities = new float3[points.Length];
            for (int i = 0; i < velocities.Length; i++) velocities[i] = initialVel;

            return new SpawnData() { points = points, velocities = velocities };
        }

private int GeneratePhysicalGrid(List<float3> outPoints, bool countOnly)
{
    if (bucketTransform == null) return 0;

    // 1. Fix the Unity Cylinder Trap: Cylinders are naturally 2 units tall at scale 1
    float meshHeightFactor = (bucketShape == SpawnShape.Cylinder) ? 2f : 1f;

    Vector3 bucketScale = bucketTransform.lossyScale;
    float totalHeight = bucketScale.y * meshHeightFactor;
    
    // Unity cylinder radius is 0.5 at scale 1 (diameter is 1), so this remains correct
    float radius = (bucketScale.x * 0.5f) * wallRadiusMargin;
    float radiusSq = radius * radius;

    // 2. Lock individual particle spacing to the simulation's physical rest requirements
    float fixedSpacing = 1f / Mathf.Pow(simulationTargetDensity, 1f / 3f);

    // 3. Calculate how many columns/rows fit across the width/depth
    int numX = Mathf.CeilToInt(bucketScale.x / fixedSpacing);
    int numZ = Mathf.CeilToInt(bucketScale.z / fixedSpacing);
    
    // 4. Calculate exactly how many vertical layers fit within the target FILL PERCENTAGE
    float targetFillHeight = totalHeight * fillPercentage;
    int numY = Mathf.CeilToInt(targetFillHeight / fixedSpacing);
    
    if (fillPercentage > 0 && numY == 0) numY = 1;

    int particleCount = 0;
    Vector3 bucketCenter = bucketTransform.position;

    // 5. Build the grid from the bottom up
    for (int y = 0; y < numY; y++)
    {
        for (int x = 0; x < numX; x++)
        {
            for (int z = 0; z < numZ; z++)
            {
                // Center the X and Z coordinates relative to the middle of the bucket
                float localX = (x - (numX - 1) * 0.5f) * fixedSpacing;
                float localZ = (z - (numZ - 1) * 0.5f) * fixedSpacing;

                // Start Y at the absolute bottom floor of the bucket (-half height) and stack upwards
                float localY = (y * fixedSpacing) - (totalHeight * 0.5f);

                // If using a cylinder container, slice off the corners
                if (bucketShape == SpawnShape.Cylinder)
                {
                    if ((localX * localX + localZ * localZ) > radiusSq) continue;
                }

                // Safety check: Don't let floating-point rounding spawn particles past the absolute top
                if (localY > (totalHeight * 0.5f)) continue;

                particleCount++;

                if (!countOnly && outPoints != null)
                {
                    // Convert the local grid offsets into world coordinates based on the bucket's rotation/position
                    Vector3 worldPos = bucketCenter +
                                      (bucketTransform.right * localX) +
                                      (bucketTransform.up * localY) +
                                      (bucketTransform.forward * localZ);

                    float3 jitter = UnityEngine.Random.insideUnitSphere * jitterStrength;
                    outPoints.Add(new float3(worldPos.x, worldPos.y, worldPos.z) + jitter);
                }
            }
        }
    }

    return particleCount;
}
        void OnValidate() => debug_num_particles = GeneratePhysicalGrid(null, true);

        private void OnDrawGizmos()
        {
            if (bucketTransform == null) return;
            Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
            
            // Draw a visual indicator of where the fluid fill line will stop
            Vector3 centerOffset = bucketTransform.up * ((totalHeight * fillPercentage * 0.5f) - (totalHeight * 0.5f));
            // (Standard visual Gizmo implementation tracking bucketTransform)
        }

private float totalHeight => bucketTransform ? bucketTransform.lossyScale.y * ((bucketShape == SpawnShape.Cylinder) ? 2f : 1f) : 1f;
        public struct SpawnData
        {
            public float3[] points;
            public float3[] velocities;
        }
    }
}