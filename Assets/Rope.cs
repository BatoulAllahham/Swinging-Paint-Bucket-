
using UnityEngine;

public class Rope : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Transform hangPoint;
    [SerializeField] Transform bucket;

    [Header("Rope Settings")]
    [SerializeField] int numSegments = 30; //for wiggling 
    [SerializeField] float ropeLength = 10.0f;
    [SerializeField] float ropeRadius = 0.05f;
    [SerializeField] int radialSegments = 8;

    [Header("Physics")]
    [SerializeField] float gravity = 9.81f;
    [SerializeField] int constraintIterations = 5;//for wiggling
    [SerializeField] int substeps = 4;

    // Verlet data
    private Vector3[] positions;
    private Vector3[] previousPositions;
    private float segmentLength;

    // Mesh data (Created ONCE)
    private Mesh ropeMesh;
    private MeshFilter meshFilter;
    private Vector3[] vertices;
    private int[] triangles;
    private Vector2[] uv;

    void Start()
    {
        InitializeRope();
        InitializeMeshComponents();
        AllocateMeshData();
        BuildStaticMeshData();
        UpdateVertexPositions(); // Set initial visual positions
    }

    void FixedUpdate()
    {
        // Divide the frame time by the number of substeps
        float dt = Time.fixedDeltaTime / substeps;

        for (int s = 0; s < substeps; s++)
        {
            Simulate(dt);
            ApplyConstraints();
        }

        UpdateVertexPositions(); // Update visuals once per frame
    }

    void InitializeRope()
    {
        segmentLength = ropeLength / numSegments;
        positions = new Vector3[numSegments + 1];
        previousPositions = new Vector3[numSegments + 1];

        Vector3 direction = (bucket.position - hangPoint.position).normalized;
        if (direction == Vector3.zero) direction = Vector3.down; // Fallback just in case

        for (int i = 0; i <= numSegments; i++)
        {
            positions[i] = hangPoint.position + direction * segmentLength * i;
            previousPositions[i] = positions[i];
        }
    }

    void InitializeMeshComponents()
    {
        meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null) meshFilter = gameObject.AddComponent<MeshFilter>();

        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.material = new Material(Shader.Find("Standard"));
            meshRenderer.material.color = new Color(0.6f, 0.4f, 0.2f); // Brown
        }

        ropeMesh = new Mesh();
        meshFilter.mesh = ropeMesh;
    }

    void AllocateMeshData()
    {
        int numPoints = numSegments + 1;
        int vertexCount = numPoints * radialSegments;
        int triangleCount = numSegments * radialSegments * 2;

        vertices = new Vector3[vertexCount];
        triangles = new int[triangleCount * 3];
        uv = new Vector2[vertexCount];
    }


    void BuildStaticMeshData()
    {
        int numPoints = numSegments + 1;

        // Generate UVs
        for (int i = 0; i < numPoints; i++)
        {
            for (int j = 0; j < radialSegments; j++)
            {
                int vertexIndex = i * radialSegments + j;
                float u = (float)j / radialSegments;
                float v = (float)i / numSegments;
                uv[vertexIndex] = new Vector2(u, v);
            }
        }

        // Generate Triangles
        int triangleIndex = 0;
        for (int i = 0; i < numSegments; i++)
        {
            for (int j = 0; j < radialSegments; j++)
            {
                int current = i * radialSegments + j;
                int next = i * radialSegments + (j + 1) % radialSegments;
                int below = (i + 1) * radialSegments + j;
                int belowNext = (i + 1) * radialSegments + (j + 1) % radialSegments;

                triangles[triangleIndex++] = current;
                triangles[triangleIndex++] = below;
                triangles[triangleIndex++] = next;

                triangles[triangleIndex++] = next;
                triangles[triangleIndex++] = below;
                triangles[triangleIndex++] = belowNext;
            }
        }

        // Assign static data to mesh once
        ropeMesh.vertices = vertices;     
        ropeMesh.triangles = triangles;
        ropeMesh.uv = uv;
    }
   
    void UpdateVertexPositions()
    {
        int numPoints = numSegments + 1;

        for (int i = 0; i < numPoints; i++)
        {
            Vector3 pointPos = positions[i];

            // Calculate tangent
            Vector3 tangent;
            if (i == 0)
                tangent = (positions[1] - positions[0]).normalized;
            else if (i == numPoints - 1)
                tangent = (positions[numPoints - 1] - positions[numPoints - 2]).normalized;
            else
                tangent = (positions[i + 1] - positions[i - 1]).normalized;

            // Calculate perpendicular vectors
            Vector3 up = Vector3.up;
            if (Mathf.Abs(Vector3.Dot(tangent, up)) > 0.99f)
                up = Vector3.right;

            Vector3 normal = Vector3.Cross(tangent, up).normalized;
            Vector3 binormal = Vector3.Cross(tangent, normal).normalized;

            // Update ring vertices
            for (int j = 0; j < radialSegments; j++)
            {
                int vertexIndex = i * radialSegments + j;
                float angle = (float)j / radialSegments * Mathf.PI * 2f;

                float cosAngle = Mathf.Cos(angle) * ropeRadius;
                float sinAngle = Mathf.Sin(angle) * ropeRadius;

                Vector3 worldVertexPos = pointPos + normal * cosAngle + binormal * sinAngle;

                // Convert to local space
                vertices[vertexIndex] = transform.InverseTransformPoint(worldVertexPos);
            }
        }

        // Upload updated vertices to GPU
        ropeMesh.vertices = vertices;
        ropeMesh.RecalculateNormals();
        ropeMesh.RecalculateBounds();
    }

    void Simulate(float dt)
    {
        float dtSquared = dt * dt;

        for (int i = 1; i < numSegments; i++)
        {
            Vector3 velocity = positions[i] - previousPositions[i];
            velocity *= 0.99f; // Restore normal damping! 0.5f was way too harsh.
            Vector3 newPosition = positions[i] + velocity + Vector3.down * gravity * dtSquared;

            previousPositions[i] = positions[i];
            positions[i] = newPosition;
        }
    }

    void ApplyConstraints()
    {
        positions[0] = hangPoint.position;
        positions[numSegments] = bucket.position;

        for (int iter = 0; iter < constraintIterations; iter++)
        {
            for (int i = 0; i < numSegments; i++)
            {
                Vector3 delta = positions[i + 1] - positions[i];
                float currentDistance = delta.magnitude;

                if (currentDistance == 0f) continue;

                float difference = (currentDistance - segmentLength) / currentDistance;
                Vector3 correction = delta * difference * 0.5f;

                if (i != 0)
                    positions[i] += correction;
                if (i + 1 != numSegments)
                    positions[i + 1] -= correction;
            }

            positions[0] = hangPoint.position;
            positions[numSegments] = bucket.position;
        }
    }
}