
using UnityEngine;

public class Rope : MonoBehaviour
{
    public float ropeLength = 8.0f; // the total L
    public float RopeLength => ropeLength;

    [Header("References")]
    [SerializeField] Transform hangPoint;
    [SerializeField] Transform bucket;
    [SerializeField] private Vector3 bucketTopOffset = new Vector3(0f, 0.5f, 0f);

    [Header("Rope Settings")]
    [SerializeField] int numSegments = 30; //how many pieces we brake the rope into
    [SerializeField] float ropeRadius = 0.05f;
    [SerializeField] int radialSegments = 8; //in one ring, how many vertices we have

    [Header("Physics")]
    [SerializeField] float gravity = 9.81f;
    [SerializeField] int constraintIterations = 5; //the No. of iteration 
    [SerializeField] int substeps = 4;

    // Verlet data
    private Vector3[] positions; //current position P_i
    private Vector3[] previousPositions; // P_i-1
    private float segmentLength; //L_segment

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
        UpdateVertexPositions(); 
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime / substeps;

        for (int s = 0; s < substeps; s++)
        {
            Simulate(dt);
            ApplyConstraints();
        }

        UpdateVertexPositions();
    }

    //(1st step) : just put the points in their place
    void InitializeRope()
    {

        segmentLength = ropeLength / numSegments;
        //for 30 points we need 31 positions (0-30)
        positions = new Vector3[numSegments + 1];
        previousPositions = new Vector3[numSegments + 1];

        //d = (End - Start) / ||End - Start||
        Vector3 attachPoint = bucket.TransformPoint(bucketTopOffset);
        Vector3 direction = (attachPoint - hangPoint.position).normalized;
        if (direction == Vector3.zero) direction = Vector3.down;
        //loop over all the points and set them in the initial position
        for (int i = 0; i <= numSegments; i++)
        {
            //P_i = Start + d * (i * L_segment)
            // If it's the last point, pin it to the top of the bucket!
            if (i == numSegments)
                positions[i] = attachPoint;
            else
                positions[i] = hangPoint.position + direction * segmentLength * i;

            previousPositions[i] = positions[i];
        }
           
    }


    //(2nd step) : Verlet integration, we make the points move according to the physics
    void Simulate(float dt)
    {
        //||delta||^2 , out the loop to make it easier for the GPU
        float dtSquared = dt * dt;

        for (int i = 1; i < numSegments; i++) //skip i = 0 (hangpoint) and i = numSegments (bucket), they are pinned
        {
            //d = P_old - P_older
            Vector3 velocity = positions[i] - previousPositions[i];
            velocity *= 0.99f; // to drain a tiny bit of energy to avoid infinite oscillation
            //P_new = P_old + (P_old - P_older) + (A * delta^2)
            Vector3 newPosition = positions[i] + velocity + Vector3.down * gravity * dtSquared; //a = g , vector down is the direction
            //update the positions : current position -> previous ,  new  -> current 
            previousPositions[i] = positions[i];
            positions[i] = newPosition;
        }
    }

    //(3rd step) : Apply constraints relaxation , to keep the distsnce between the segments static / constant
    void ApplyConstraints()
    {
        //pinned points
        positions[0] = hangPoint.position;
        positions[numSegments] = bucket.TransformPoint(bucketTopOffset);        
        for (int iter = 0; iter < constraintIterations; iter++)    //iteration to reduce the erros
        {
            //we go over each pair of points
            for (int i = 0; i < numSegments; i++)
            {
                //delta  = P_i+1 - P_i
                Vector3 delta = positions[i + 1] - positions[i];
                //d = ||delta|| 
                float currentDistance = delta.magnitude;
                //divide by zero check
                if (currentDistance == 0f) continue;
                //Error = (d - L_segment) / d
                float difference = (currentDistance - segmentLength) / currentDistance;
                //Correction = delta * Error * 0.5
                Vector3 correction = delta * difference * 0.5f;

                if (i != 0) //pinned 
                    positions[i] += correction;
                if (i + 1 != numSegments)//pinned
                    positions[i + 1] -= correction;
            }

            positions[0] = hangPoint.position;
            positions[numSegments] = bucket.TransformPoint(bucketTopOffset);
        }
    }




    //(4th step) : Update the vertex positions 
    //Tangent : direction, Normal : Right/left, Binormal : forwad/backward
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

        ropeMesh = new Mesh();
        meshFilter.mesh = ropeMesh;
    }


    // to optimize the work, we calculate the data of the whole rope and allocate the needed memory for it and it runs only once
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