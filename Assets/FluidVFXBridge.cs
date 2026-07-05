using UnityEngine;
using UnityEngine.VFX;
using PaintSim.Fluid.Simulation;

public class FluidVFXBridge : MonoBehaviour
{
    
    public FluidSim fluidSimulation;
    
    private VisualEffect vfxComponent;
    private bool isLinked = false;

    void Awake()
    {
        vfxComponent = GetComponent<VisualEffect>();
    }

    void Update()
    {
        // If we already established the link, or the simulation reference is missing, skip
        if (isLinked || fluidSimulation == null) return;

        // FOOLPROOF CHECK: Wait until the simulation buffer exists and has data
        if (fluidSimulation.positionBuffer != null && fluidSimulation.positionBuffer.count > 0)
        {
            LinkBufferToVFX();
        }
    }

    void LinkBufferToVFX()
    {
        if (vfxComponent != null)
        {
            // 1. Link the VRAM position array pointer
            vfxComponent.SetGraphicsBuffer("ParticlePositions", fluidSimulation.positionBuffer);
            
            // 2. Pass the exact particle count dynamically
            int totalParticles = fluidSimulation.positionBuffer.count; 
            vfxComponent.SetInt("ParticleCount", totalParticles);

            // 3. Pass the color if the property exists in the graph
            if (vfxComponent.HasVector4("FluidColor"))
            {
                vfxComponent.SetVector4("FluidColor", fluidSimulation.paintColour);
            }

            isLinked = true;
            Debug.Log($"<color=green>Fluid VFX Bridge: Successfully Auto-Linked {totalParticles} fluid particles!</color>");
        }
    }
}