using UnityEngine;

public class Rope1 : MonoBehaviour
{
    public enum RopeType
    {
        Normal = 0,
        Metal = 1,

    }

    public RopeType CurrentRopeType => ropeType;



    [System.Serializable]
    public struct RopeProperties
    {
        public float compliance;
        public float damping;
        public bool useSpringForces;
        public int constraintIterations;
        public int substeps;
    }
    public float ropeLength = 8.0f;
    private float previousRopeLength;
    public float RopeLength => ropeLength;

    [Header("References")]
    [SerializeField] Transform hangPoint;
    [SerializeField] Transform bucket;
    [SerializeField] private Vector3 bucketTopOffset = new Vector3(0f, 0.5f, 0f);

    [Header("Rope Settings")]
    [SerializeField] int numSegments = 30;
    [SerializeField] float ropeRadius = 0.05f;
    [SerializeField] int radialSegments = 8;

    [Header("Rope Type")]
    [SerializeField] RopeType ropeType = RopeType.Normal;
    private RopeType previousRopeType;
    private RopeProperties activeProperties;

    [Header("Spring Settings")]
    [SerializeField] float topStiffness = 25f;     // near hangPoint, stays tighter
    [SerializeField] float bottomStiffness = 1.5f; // near bucket, stretches the most — LOW on purpose
    [SerializeField] float ropeMass = 0.3f;        // total rope mass, NOT the bucket
    private float segmentMass;

    [Header("Physics")]
    [SerializeField] float gravity = 9.81f;
    //[SerializeField] int constraintIterations = 5;
    //[SerializeField] int substeps = 4;

    private Vector3[] positions;
    private Vector3[] previousPositions;
    private Vector3[] forces;
    private float segmentLength;

    private Mesh ropeMesh;
    private MeshFilter meshFilter;
    private Vector3[] vertices;
    private int[] triangles;
    private Vector2[] uv;

    void Start()
    {
        previousRopeLength = ropeLength;
        previousRopeType = ropeType;
        activeProperties = GetPropertiesForType(ropeType);
        segmentMass = ropeMass / numSegments;
        //Debug.Log("Rope initialized with type: " + ropeType + " | useSpringForces = " + activeProperties.useSpringForces);


        InitializeRope();
        InitializeMeshComponents();
        AllocateMeshData();
        BuildStaticMeshData();
        UpdateVertexPositions();
    }

    void FixedUpdate()
    {
        if (!Mathf.Approximately(ropeLength, previousRopeLength))
        {
            previousRopeLength = ropeLength;
            OnRopeLengthChanged();
        }

        //if (ropeType == RopeType.Spring)
        //    SyncToActualBucketDistance();

        if (ropeType != previousRopeType)
        {
            previousRopeType = ropeType;
            activeProperties = GetPropertiesForType(ropeType);
            //Debug.Log("Rope type switched to: " + ropeType);
        }

        float dt = Time.fixedDeltaTime / activeProperties.substeps;

        for (int s = 0; s < activeProperties.substeps; s++)
        {
            Simulate(dt);
            ApplyConstraints(dt);
        }

        UpdateVertexPositions();
    }

    RopeProperties GetPropertiesForType(RopeType type)
    {
        switch (type)
        {
            case RopeType.Normal:
                return new RopeProperties
                {
                    compliance = 0.0006f,
                    damping = 0.99f,
                    useSpringForces = false,
                    constraintIterations = 12,
                    substeps = 6
                };

            case RopeType.Metal:
                return new RopeProperties
                {
                    compliance = 0.0f,
                    damping = 0.999f,
                    useSpringForces = false,
                    constraintIterations = 25,
                    substeps = 10
                };


            default:
                return GetPropertiesForType(RopeType.Normal);
        }
    }

    void OnRopeLengthChanged()
    {
        segmentLength = ropeLength / numSegments;
        InitializeRope();
    }

    //void SyncToActualBucketDistance()
    //{
    //    float actualDistance = Vector3.Distance(hangPoint.position, bucket.TransformPoint(bucketTopOffset));
    //    segmentLength = actualDistance / numSegments;
    //}




    void InitializeRope()
    {
        segmentLength = ropeLength / numSegments;
        positions = new Vector3[numSegments + 1];
        previousPositions = new Vector3[numSegments + 1];
        forces = new Vector3[numSegments + 1];

        Vector3 attachPoint = bucket.TransformPoint(bucketTopOffset);
        Vector3 direction = (attachPoint - hangPoint.position).normalized;
        if (direction == Vector3.zero) direction = Vector3.down;

        for (int i = 0; i <= numSegments; i++)
        {
            if (i == numSegments)
                positions[i] = attachPoint;
            else
                positions[i] = hangPoint.position + direction * segmentLength * i;

            previousPositions[i] = positions[i];
        }
    }

    void Simulate(float dt)
    {
        float dtSquared = dt * dt;

        for (int i = 1; i < numSegments; i++)
        {
            Vector3 velocity = (positions[i] - previousPositions[i]) * activeProperties.damping;
            Vector3 newPosition = positions[i] + velocity + Vector3.down * gravity * dtSquared;
            previousPositions[i] = positions[i];
            positions[i] = newPosition;
        }
    }






    void ApplyConstraints(float dt)
    {
        positions[0] = hangPoint.position;
        previousPositions[numSegments] = positions[numSegments];
        positions[numSegments] = bucket.TransformPoint(bucketTopOffset);

        if (activeProperties.useSpringForces)
        {
            float maxStretch = segmentLength * 3.0f;
            for (int i = 0; i < numSegments; i++)
            {
                Vector3 delta = positions[i + 1] - positions[i];
                float dist = delta.magnitude;
                if (dist > maxStretch)
                {
                    Vector3 dir = delta / dist;
                    Vector3 excess = dir * (dist - maxStretch) * 0.5f;
                    if (i != 0) positions[i] += excess;
                    if (i + 1 != numSegments) positions[i + 1] -= excess;
                }
            }
            return;
        }

        float dtSquared = dt * dt;
        for (int iter = 0; iter < activeProperties.constraintIterations; iter++)

        {
            for (int i = 0; i < numSegments; i++)
            {
                Vector3 delta = positions[i + 1] - positions[i];
                float currentDistance = delta.magnitude;
                if (currentDistance == 0f) continue;

                float compliance = activeProperties.compliance / dtSquared;
                float difference = (currentDistance - segmentLength) / (currentDistance * (1f + compliance));
                Vector3 correction = delta * difference * 0.5f;

                if (i != 0) positions[i] += correction;
                if (i + 1 != numSegments) positions[i + 1] -= correction;
            }

            positions[0] = hangPoint.position;
            positions[numSegments] = bucket.TransformPoint(bucketTopOffset);
        }
    }

    void UpdateVertexPositions()
    {
        int numPoints = numSegments + 1;

        for (int i = 0; i < numPoints; i++)
        {
            Vector3 pointPos = positions[i];
            Vector3 tangent;
            if (i == 0)
                tangent = (positions[1] - positions[0]).normalized;
            else if (i == numPoints - 1)
                tangent = (positions[numPoints - 1] - positions[numPoints - 2]).normalized;
            else
                tangent = (positions[i + 1] - positions[i - 1]).normalized;

            Vector3 up = Vector3.up;
            if (Mathf.Abs(Vector3.Dot(tangent, up)) > 0.99f)
                up = Vector3.right;

            Vector3 normal = Vector3.Cross(tangent, up).normalized;
            Vector3 binormal = Vector3.Cross(tangent, normal).normalized;

            for (int j = 0; j < radialSegments; j++)
            {
                int vertexIndex = i * radialSegments + j;
                float angle = (float)j / radialSegments * Mathf.PI * 2f;

                float cosAngle = Mathf.Cos(angle) * ropeRadius;
                float sinAngle = Mathf.Sin(angle) * ropeRadius;

                Vector3 worldVertexPos = pointPos + normal * cosAngle + binormal * sinAngle;
                vertices[vertexIndex] = transform.InverseTransformPoint(worldVertexPos);
            }
        }

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

        ropeMesh.vertices = vertices;
        ropeMesh.triangles = triangles;
        ropeMesh.uv = uv;
    }
}
