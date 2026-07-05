using UnityEngine;

public class Rope : MonoBehaviour
{
    public enum RopeType { Metal = 0, Normal = 1, Elastic = 2, Wooden = 3 }
    public RopeType type = RopeType.Metal;

    public float ropeLength = 8.0f;
    private float previousRopeLength;
    public float RopeLength => ropeLength;
    public float totalRopeMass = 0.5f; // 0 for a perfectly straight metal rope




    public bool isSimulating = false;
    
    [Header("References")]
    [SerializeField] Transform hangPoint;
    [SerializeField] Transform bucket;
    [SerializeField] private Vector3 bucketTopOffset = new Vector3(0f, -1.0f, 0f);


    [Header("Rope Settings")]
    [SerializeField] public int numSegments = 30;
    [SerializeField] public float ropeRadius = 0.05f;
    [SerializeField] public int radialSegments = 8;

    [Header("Physics")]
    [SerializeField] public float gravity = 9.81f;
    [SerializeField] public int constraintIterations = 15;
    [SerializeField] public int substeps = 4;

    private Vector3[] positions;
    private Vector3[] previousPositions;
    private float segmentLength;
    private float[] inverseMasses;

    // Mesh data
    private Mesh ropeMesh;
    private MeshFilter meshFilter;
    private Vector3[] vertices;
    private int[] triangles;
    private Vector2[] uv;



    public float CurrentStretchedLength { get; private set; }


    private static readonly (float stiffness, float damping, int iterations)[] Presets = new[]
  {
        (1.0f,    0.999f, 80),   // metal
        (1.0f,    0.995f, 80),   // wooden
        (0.90f,   0.998f, 75),   // normal
        (0.70f,   0.99f,  30)    //elastic
    };

    //iteration 1->100, damping 0.9->1, stiffness 0.01->1.0, 
    public RopeType Type { get => type; set => type = value; }
    public float RopeLengthProperty { get => ropeLength; set { ropeLength = Mathf.Max(0.1f, value); } }
    public float TotalRopeMass { get => totalRopeMass; set { totalRopeMass = Mathf.Max(0f, value); InitializeRope(); } }
    public int NumSegments { get => numSegments; set { if (numSegments != value && value >= 2) { numSegments = value; RebuildRopeMeshStructure(); } } }
    public float RopeRadius { get => ropeRadius; set => ropeRadius = Mathf.Max(0.001f, value); }
    public int RadialSegments { get => radialSegments; set { if (radialSegments != value && value >= 3) { radialSegments = value; RebuildRopeMeshStructure(); } } }
    public float Gravity { get => gravity; set => gravity = value; }
    public int ConstraintIterations { get => constraintIterations; set => constraintIterations = Mathf.Max(1, value); }
    public int Substeps { get => substeps; set => substeps = Mathf.Max(1, value); }

    void Start()
    {
        previousRopeLength = ropeLength;
         RebuildRopeMeshStructure();
        // InitializeRope();
        // InitializeMeshComponents();
        // AllocateMeshData();
        // BuildStaticMeshData();
        // UpdateVertexPositions();
    }
    public void ResetRope()
    {
        InitializeRope();
        UpdateVertexPositions();
    }
        public void RebuildRopeMeshStructure()
    {
        InitializeMeshComponents();
        InitializeRope();
        AllocateMeshData();
        BuildStaticMeshData();
        UpdateVertexPositions();
    }

    //void FixedUpdate()
    //{
    //    // Grab the correct preset based on the selected enum
    //    var preset = Presets[(int)type];

    //    float dt = Time.fixedDeltaTime / substeps;

    //    for (int s = 0; s < substeps; s++)
    //    {
    //        Simulate(dt, preset.damping);               // Pass damping
    //        ApplyConstraints(preset.stiffness, preset.iterations); // Pass stiffness & iterations
    //    }

    //    if (!Mathf.Approximately(ropeLength, previousRopeLength))
    //    {
    //        previousRopeLength = ropeLength;
    //        OnRopeLengthChanged();
    //    }

    //    CurrentStretchedLength = Vector3.Distance(positions[0], positions[numSegments]);
    //    UpdateVertexPositions();
    //}




    void FixedUpdate()
    {
        if (hangPoint == null) return; 
        // 1. Pin endpoints
     Vector3 attachPoint = bucket != null ? bucket.TransformPoint(bucketTopOffset) : hangPoint.position + Vector3.down * ropeLength;
         positions[0] = hangPoint.position;
        previousPositions[0] = hangPoint.position; // Hangpoint never moves

        positions[numSegments] = attachPoint;
        if (bucket != null)
        {

            Vector3 bucketVelocity = bucket.GetComponent<Pendulum>()?.getLinearVelocity() ?? Vector3.zero;
            previousPositions[numSegments] = attachPoint - (bucketVelocity * Time.fixedDeltaTime);
        }
        else
        {
            previousPositions[numSegments] = attachPoint;
        }

        var preset = Presets[(int)type];

        if (!isSimulating  || type == RopeType.Metal || type == RopeType.Wooden)
        {

            for (int i = 1; i < numSegments; i++)
            {
                float t = (float)i / numSegments;
                positions[i] = Vector3.Lerp(positions[0], positions[numSegments], t);
                previousPositions[i] = positions[i];
            }
        }
        else
        {

            float dt = Time.fixedDeltaTime / substeps;
            for (int s = 0; s < substeps; s++)
            {
                Simulate(dt, preset.damping);
                ApplyConstraints(preset.stiffness, preset.iterations);
            }
        }

        if (!Mathf.Approximately(ropeLength, previousRopeLength))
        {
            previousRopeLength = ropeLength;
            OnRopeLengthChanged();
        }

        CurrentStretchedLength = Vector3.Distance(positions[0], positions[numSegments]);
        UpdateVertexPositions();
    }






    void OnRopeLengthChanged()
    {
        segmentLength = ropeLength / numSegments;
        InitializeRope();
    }

    void InitializeRope()
    {
          if (hangPoint == null) return;
        segmentLength = ropeLength / numSegments;
        positions = new Vector3[numSegments + 1];
        previousPositions = new Vector3[numSegments + 1];
        inverseMasses = new float[numSegments + 1];

        Vector3 attachPoint = bucket != null ? bucket.TransformPoint(bucketTopOffset) : hangPoint.position + Vector3.down * ropeLength;
        Vector3 direction = (attachPoint - hangPoint.position).normalized;
        if (direction == Vector3.zero) direction = Vector3.down;

        float segmentMass = totalRopeMass / numSegments;

        for (int i = 0; i <= numSegments; i++)
        {
            if (i == numSegments)
                positions[i] = attachPoint;
            else
                positions[i] = hangPoint.position + direction * segmentLength * i;

            previousPositions[i] = positions[i];


            if (i == 0)
                inverseMasses[i] = 0f;
            else if (i == numSegments)
                inverseMasses[i] = 0f;
            else
                inverseMasses[i] = (segmentMass > 0) ? 1.0f / segmentMass : 0f;
        }
    }

    void Simulate(float dt, float dampingFactor)
    {
        float dtSquared = dt * dt;

        for (int i = 1; i < numSegments; i++)
        {
            Vector3 velocity = positions[i] - previousPositions[i];
            velocity *= dampingFactor;
            Vector3 newPosition = positions[i] + velocity + Vector3.down * gravity * dtSquared;
            previousPositions[i] = positions[i];
            positions[i] = newPosition;
        }
    }

    void ApplyConstraints(float stiffnessFactor, int iterations)
    {
        // Pin endpoints
        previousPositions[numSegments] = positions[numSegments];
        positions[numSegments] = bucket.TransformPoint(bucketTopOffset);
        positions[0] = hangPoint.position;

        for (int iter = 0; iter < iterations; iter++)
        {
            for (int i = 0; i < numSegments; i++)
            {
                Vector3 delta = positions[i + 1] - positions[i];
                float currentDistance = delta.magnitude;
                if (currentDistance == 0f) continue;

                float w1 = inverseMasses[i];
                float w2 = inverseMasses[i + 1];
                float totalInverseMass = w1 + w2;

                if (totalInverseMass == 0f) continue;

                float difference = (currentDistance - segmentLength) / currentDistance;
                Vector3 correction = delta * difference;


                correction *= stiffnessFactor;

                if (w1 > 0) positions[i] += correction * (w1 / totalInverseMass);
                if (w2 > 0) positions[i + 1] -= correction * (w2 / totalInverseMass);
            }

            // Re-pin endpoints after solving
            positions[0] = hangPoint.position;
            positions[numSegments] = bucket.TransformPoint(bucketTopOffset);
        }
    }



    void UpdateVertexPositions()
    {

        int numPoints = numSegments + 1;
        // Loop through each point in the rope 

        for (int i = 0; i < numPoints; i++)
        {
            Vector3 pointPos = positions[i];
            //find the direction of the rope at this point to create a local coordinate system
            Vector3 tangent;
            if (i == 0) //at the start don't look behind, just look forward
                tangent = (positions[1] - positions[0]).normalized;
            else if (i == numPoints - 1) // at the end don't look forward, just look behind
                tangent = (positions[numPoints - 1] - positions[numPoints - 2]).normalized;
            else
                tangent = (positions[i + 1] - positions[i - 1]).normalized;

            Vector3 up = Vector3.up; //if the rope is up or down; we don't use the cross product
            if (Mathf.Abs(Vector3.Dot(tangent, up)) > 0.99f)
                up = Vector3.right;

            Vector3 normal = Vector3.Cross(tangent, up).normalized;
            Vector3 binormal = Vector3.Cross(tangent, normal).normalized;

            //vertex = P_i + (ropeRadius * cos(angle) * normal) + (ropeRadius * sin(angle) * binormal)
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




    void InitializeMeshComponents()
    {
        meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null) meshFilter = gameObject.AddComponent<MeshFilter>();

        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.material = new Material(Shader.Find("Standard"));
            meshRenderer.material.color = new Color(0.6f, 0.4f, 0.2f);
        }

       if (ropeMesh == null)
        {
            ropeMesh = new Mesh();
            meshFilter.mesh = ropeMesh;
        }
    }

    void AllocateMeshData()
    {
        int numPoints = numSegments + 1;//31 point
        int vertexCount = numPoints * radialSegments; // 31 * 8 = 248 vertices
        int triangleCount = numSegments * radialSegments * 2; // 30 * 8 * 2 = 480 triangles

        vertices = new Vector3[vertexCount];
        triangles = new int[triangleCount * 3];//1440 vertices 
        uv = new Vector2[vertexCount];
    }


    //fill the uv and triangles 
    void BuildStaticMeshData()
    {
        int numPoints = numSegments + 1;

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



}