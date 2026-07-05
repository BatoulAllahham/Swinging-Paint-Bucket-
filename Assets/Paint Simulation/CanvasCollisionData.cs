using UnityEngine;

namespace PaintSim.Fluid.Simulation
{
    public class CanvasCollisionData : MonoBehaviour
    {
        [Header("Reference to the Canvas cube")]
        public Transform canvasTransform;
        //CANVAS GEOMETRY
        // The cube is flat (thin on Y), so its "up" face normal is world-up
        public Vector3 Centre => canvasTransform.position;
        public Vector3 Normal => canvasTransform.up.normalized;

        // The two WIDE axes of the flat top face are X (right) and Z (forward)
        public Vector3 Right => canvasTransform.right.normalized;
        public Vector3 Up => canvasTransform.forward.normalized; // "v" axis of the plane

        //  localScale.x and localScale.z (the wide dimensions), not x/y
        public Vector2 HalfSize => new Vector2(
            canvasTransform.localScale.x * 0.5f,
            canvasTransform.localScale.z * 0.5f
        );

        public void SetShaderParams(ComputeShader compute)
        {
            Debug.Log($"Canvas centre={Centre}, normal={Normal}, right={Right}, up={Up}, halfSize={HalfSize}");
            compute.SetVector("canvasCentre", Centre);
            compute.SetVector("canvasNormal", Normal);
            compute.SetVector("canvasRight", Right);
            compute.SetVector("canvasUp", Up);
            compute.SetVector("canvasHalfSize", HalfSize);
        }
    }
}