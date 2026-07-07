using UnityEngine;
using System.IO;
using System.Text;
using System.Collections.Generic;
using PaintSim.Fluid.Simulation; 

public class SimulationReporter : MonoBehaviour
{
    public void ExportReport(FluidSim[] buckets, Pendulum p, Rope r)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("--- FULL SIMULATION EXPERIMENT REPORT ---");
        sb.AppendLine($"Date: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();


        sb.AppendLine("=== SPH / FLUID SETTINGS ===");
        if (buckets != null)
        {
            for (int i = 0; i < buckets.Length; i++)
            {
                var b = buckets[i];
                sb.AppendLine($"--- Bucket {i} ---");
                sb.AppendLine($"  Gravity: {b.gravity}");
                sb.AppendLine($"  Smoothing Radius: {b.smoothingRadius}");
                sb.AppendLine($"  Target Density: {b.targetDensity}");
                sb.AppendLine($"  Pressure Multiplier: {b.pressureMultiplier}");
                sb.AppendLine($"  Near Pressure: {b.nearPressureMultiplier}");
                sb.AppendLine($"  Viscosity: {b.viscosityStrength}");
                sb.AppendLine($"  Stiffness: {b.springStiffness}");
                sb.AppendLine($"  Collision Damping: {b.collisionDamping}");
                sb.AppendLine($"  Weight/Particle: {b.weightPerParticle}");
                sb.AppendLine($"  Temperature: {b.temperature}");
                sb.AppendLine($"  Humidity: {b.humidity}");
                sb.AppendLine($"  Evaporation Rate: {b.evaporationRate}");
                sb.AppendLine($"  Hole Size: {b.holeSize}");
                sb.AppendLine($"  Hole Position: {b.holePosition}");
            }
        }


        sb.AppendLine("\n=== PENDULUM SETTINGS ===");
        if (p != null)
        {
            sb.AppendLine($"  Theta Degree: {p.ThetaDegree}");
            sb.AppendLine($"  Phi Degree: {p.PhiDegree}");
            sb.AppendLine($"  Theta Velocity: {p.ThetaAngularVelocity}");
            sb.AppendLine($"  Phi Velocity: {p.PhiAngularVelocity}");
            sb.AppendLine($"  Gravity: {p.Gravity}");
            sb.AppendLine($"  Air Density: {p.AirDensity}");
            sb.AppendLine($"  Drag: {p.DragCoefficient}");
            sb.AppendLine($"  Swinging Rate: {p.SwingingRate}");
        }
        else { sb.AppendLine("  (No Pendulum active)"); }


        sb.AppendLine("\n=== ROPE SETTINGS ===");
        if (r != null)
        {
            sb.AppendLine($"  Rope Length: {r.RopeLengthProperty}");
            sb.AppendLine($"  Total Mass: {r.TotalRopeMass}");
            sb.AppendLine($"  Segments: {r.NumSegments}");
            sb.AppendLine($"  Rope Radius: {r.RopeRadius}");
            sb.AppendLine($"  Gravity: {r.Gravity}");
        }
        else { sb.AppendLine("  (No Rope active)"); }


        string filename = $"SimulationReport_{System.DateTime.Now:yyyyMMdd_HHmmss}.txt";
        string path = Path.Combine(Application.persistentDataPath, filename);
        
   try 
        {
       
            File.WriteAllText(path, sb.ToString());
            UnityEngine.Debug.Log($"Report saved to: {path}");

         
            OpenFolderAndSelectFile(path);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError($"Failed to save/open report: {e.Message}");
        }
    }
private void OpenFolderAndSelectFile(string filePath)
    {
        // Check if we are on Windows
        #if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            // Normalize path for Windows (\ instead of /)
            string normalizedPath = filePath.Replace("/", "\\");
            
            // Start explorer.exe with the /select argument to highlight the file
            Process.Start("explorer.exe", $"/select, \"{normalizedPath}\"");
        #else
            // Fallback for other platforms (just logs, as explorer.exe is Windows-only)
            UnityEngine.Debug.Log("Auto-open folder is only supported on Windows.");
        #endif
    }
}