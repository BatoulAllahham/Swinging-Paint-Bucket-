using System;
using UnityEngine;

public class Pendulum : MonoBehaviour
{
    //To connect the object in the scen with the script, they must have identical names to the ones in the scene


    [Header("Angles and Velocities")]
  
    [SerializeField] float thetaAngularVelocity = 30.0f;
    [SerializeField] float phiAngularVelocity = 30.0f;
    [SerializeField] float thetaDegree = 45.0f;
    [SerializeField] float phiDegree = 45.0f;

    [Header("Forces")]
    [SerializeField] float gravity = 9.81f;
    [SerializeField] float airDensity = 1.225f;
    [SerializeField] float dragCoefficient = 1.0f; // Cd, ~1.0–1.2 for a bucket shape
    // base racket mass (without fluid) set to 1
    [SerializeField] float mass = 1.0f;
    [Header("Dependencies")]
    public TotalWeightManager weightManager;
    private float baseMass=1;

    [Header("Objects")]
    [SerializeField] Transform hangPoint;
    [SerializeField] Transform rope;
    public float bucketRadius = 1.0f;
    private float ropeLength;
    private Rope ropeComponent;



    private float thetaRadian;
    private float phiRadian;
    private float thetaAngularVelo;
    private float phiAngularVelo;
    private bool isDragging = false;
   

 



    //private float previousTheta;
    //private float previousPhi;
    //private float previousTime;

    //public void OnMouseDown()
    //{
    //    isDragging = true;
    //    previousTheta = thetaRadian;
    //    previousPhi = phiRadian;
    //    previousTime = Time.time;
    //}
    //// Add these at the top of the class with the other private fields
    //private Vector3[] velocityBuffer = new Vector3[5];
    //private int velocityBufferIndex = 0;

    //public void OnMouseDrag()
    //{
    //    Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
    //    Vector3 center = hangPoint.position;

    //    // Cast ray against sphere of radius ropeLength centered at hangPoint
    //    Vector3 oc = ray.origin - center;
    //    float a = Vector3.Dot(ray.direction, ray.direction);
    //    float b = 2.0f * Vector3.Dot(oc, ray.direction);
    //    float c = Vector3.Dot(oc, oc) - ropeLength * ropeLength;
    //    float discriminant = b * b - 4 * a * c;

    //    Vector3 constrainedPos;
    //    if (discriminant >= 0)
    //    {
    //        // Ray hits sphere — take closer intersection
    //        float t = (-b - Mathf.Sqrt(discriminant)) / (2.0f * a);
    //        if (t < 0) t = (-b + Mathf.Sqrt(discriminant)) / (2.0f * a);
    //        constrainedPos = ray.origin + t * ray.direction;
    //    }
    //    else
    //    {
    //        // Ray misses sphere — project closest point on ray onto sphere surface
    //        float t = Mathf.Max(0, -Vector3.Dot(oc, ray.direction) / a);
    //        Vector3 closest = ray.origin + t * ray.direction;
    //        constrainedPos = center + (closest - center).normalized * ropeLength;
    //    }

    //    transform.position = constrainedPos;

    //    // Convert to spherical coordinates
    //    Vector3 relPos = constrainedPos - center;
    //    thetaRadian = Mathf.Acos(Mathf.Clamp(-relPos.y / ropeLength, -1f, 1f));
    //    thetaRadian = Mathf.Max(thetaRadian, 5.0f * Mathf.Deg2Rad); // singularity guard
    //    phiRadian = Mathf.Atan2(-relPos.z, relPos.x);

    //    // Estimate angular velocity
    //    float dt = Time.time - previousTime;
    //    if (dt > 0.0001f)
    //    {
    //        float thetaVel = (thetaRadian - previousTheta) / dt;

    //        float deltaPhiRad = Mathf.DeltaAngle(
    //            previousPhi * Mathf.Rad2Deg,
    //            phiRadian * Mathf.Rad2Deg
    //        ) * Mathf.Deg2Rad;
    //        float phiVel = deltaPhiRad / dt;

    //        // Store in rolling buffer
    //        velocityBuffer[velocityBufferIndex % 5] = new Vector3(thetaVel, phiVel, 0);
    //        velocityBufferIndex++;

    //        // Average over buffer for stable handoff
    //        Vector3 avgVel = Vector3.zero;
    //        int count = Mathf.Min(velocityBufferIndex, 5);
    //        for (int i = 0; i < count; i++)
    //            avgVel += velocityBuffer[i];
    //        avgVel /= count;

    //        thetaAngularVelo = avgVel.x;
    //        phiAngularVelo = avgVel.y;
    //    }

    //    previousTheta = thetaRadian;
    //    previousPhi = phiRadian;
    //    previousTime = Time.time;

    //    // Keep inspector in sync
    //    thetaDegree = thetaRadian * Mathf.Rad2Deg;
    //    phiDegree = phiRadian * Mathf.Rad2Deg;
    //    thetaAngularVelocity = thetaAngularVelo * Mathf.Rad2Deg;
    //    phiAngularVelocity = phiAngularVelo * Mathf.Rad2Deg;
    //}

    //public void OnMouseUp()
    //{
    //    isDragging = false;
    //    velocityBufferIndex = 0; // reset buffer for next drag
    //}

    float getXCoordinate()
    {
        float xCoordinate = ropeLength * Mathf.Sin(thetaRadian) * Mathf.Cos(phiRadian);
        return xCoordinate;
    }

    float getYCoordinate()
    {
        float yCoordinate = -ropeLength * Mathf.Cos(thetaRadian);
        return yCoordinate;
    }

    float getZCoordinate()
    {
        float zCoordinate = -ropeLength * Mathf.Sin(thetaRadian) * Mathf.Sin(phiRadian);
        return zCoordinate;
    }

    Vector3 getPosition()
    {
        Vector3 position = new Vector3(getXCoordinate(), getYCoordinate(), getZCoordinate());
        return position;
    }



    float getSpeed(float thetaAngularVelo, float phiAngularVelo, float thetaRadian)
    {
        float sinTheta = Mathf.Sin(thetaRadian);
        return ropeLength * Mathf.Sqrt(
            thetaAngularVelo * thetaAngularVelo +
            sinTheta * sinTheta * phiAngularVelo * phiAngularVelo
        );
    }

    float getThetaAngularAcceleration(float thetaAngularVelo, float phiAngularVelo, float thetaRadian)
    {
        float sinTheta = Mathf.Sin(thetaRadian);
        if (Mathf.Abs(sinTheta) < 0.001f)
            sinTheta = 0.0011f;

        float conservative = phiAngularVelo * phiAngularVelo * sinTheta * Mathf.Cos(thetaRadian)
                           - (gravity / ropeLength) * sinTheta;

        float dragFactor = (0.5f * airDensity * dragCoefficient * Mathf.PI * bucketRadius * bucketRadius) / mass;
        float drag = -dragFactor * getSpeed(thetaAngularVelo, phiAngularVelo, thetaRadian) * thetaAngularVelo;

        return conservative + drag;
    }


    float getPhiAngularAcceleration(float thetaAngularVelo, float phiAngularVelo, float thetaRadian)
    {
        float sinTheta = Mathf.Sin(thetaRadian);
        if (Mathf.Abs(sinTheta) < 0.001f)
            sinTheta = 0.0011f;

        float conservative = -2.0f * (Mathf.Cos(thetaRadian) / sinTheta) * thetaAngularVelo * phiAngularVelo;

        float dragFactor = (0.5f * airDensity * dragCoefficient * Mathf.PI * bucketRadius * bucketRadius) / mass;
        float drag = -dragFactor * getSpeed(thetaAngularVelo, phiAngularVelo, thetaRadian) * phiAngularVelo;

        return conservative + drag;
    }






    float[] Step(float thetaAngularVelo, float phiAngularVelo, float thetaRadian)
    {
        //[θ̈,φ̈,θ̇,φ̇]
        float[] ret = {
        getThetaAngularAcceleration(thetaAngularVelo, phiAngularVelo, thetaRadian),
        getPhiAngularAcceleration(thetaAngularVelo, phiAngularVelo, thetaRadian),
        thetaAngularVelo,
        phiAngularVelo
    };
        return ret;
    }



    float[] RungeKutta_4th(float dt)
    {

        //k2​=f(yn​+2dt​k1​)
        float[] k1 = Step(thetaAngularVelo, phiAngularVelo, thetaRadian);
        float[] k2 = Step(thetaAngularVelo + k1[0] * 0.5f * dt, phiAngularVelo + 0.5f * k1[1] * dt, thetaRadian + 0.5f * k1[2] * dt);
        float[] k3 = Step(thetaAngularVelo + 0.5f * k2[0] * dt, phiAngularVelo + 0.5f * k2[1] * dt, thetaRadian + 0.5f * k2[2] * dt);
        float[] k4 = Step(thetaAngularVelo + k3[0] * dt, phiAngularVelo + k3[1] * dt, thetaRadian + k3[2] * dt);

        float[] sum = {
            dt * (k1[0] + 2*k2[0] + 2*k3[0] + k4[0]) / 6,   //[Δθ̇,Δφ̇,Δθ,Δφ],,,,,,,,,Δy=6dt​(k1​+2k2​+2k3​+k4​) rule 
            dt * (k1[1] + 2*k2[1] + 2*k3[1] + k4[1]) / 6,
            dt * (k1[2] + 2*k2[2] + 2*k3[2] + k4[2]) / 6,
            dt * (k1[3] + 2*k2[3] + 2*k3[3] + k4[3]) / 6
        };

        return sum;
    }



 
    void Start()
    {
        
        mass=baseMass;
        ropeComponent = rope.GetComponent<Rope>();
        ropeLength = ropeComponent.RopeLength;

        thetaRadian = Mathf.Deg2Rad * thetaDegree;
        phiRadian = Mathf.Deg2Rad * phiDegree;
        thetaAngularVelo = Mathf.Deg2Rad * thetaAngularVelocity;
        phiAngularVelo = Mathf.Deg2Rad * phiAngularVelocity;
        transform.position = getPosition() + hangPoint.position;


    }


    private void FixedUpdate()
    {
        ropeLength = ropeComponent.RopeLength;
        if (!isDragging)
        {
            // get mass for all the fluids carried at the moment in the racket from weight manager
        if (weightManager != null)
        {
            mass = baseMass + weightManager.totalCombinedWeight;
        }
            float[] state = RungeKutta_4th(Time.fixedDeltaTime);
            thetaAngularVelo += state[0];       //θ˙new​=θ˙old​+Δθ˙
            phiAngularVelo += state[1];         //ϕ˙​new​=ϕ˙​old​+Δϕ˙​

            thetaAngularVelocity = thetaAngularVelo * Mathf.Rad2Deg;
            phiAngularVelocity = phiAngularVelo * Mathf.Rad2Deg;

            thetaRadian += state[2];      //equivalent to θnew​=θold​+Δθ       
            phiRadian += state[3];          //ϕnew​=ϕold​+Δϕ

            thetaDegree = thetaRadian * Mathf.Rad2Deg;
            phiDegree = phiRadian * Mathf.Rad2Deg;

            transform.position = getPosition() + hangPoint.position;

            Quaternion Orientation = Quaternion.LookRotation(new Vector3(-transform.position.x, -transform.position.y, -transform.position.z));
            Quaternion correction = Quaternion.Inverse(Quaternion.LookRotation(Vector3.up, transform.position));
            transform.rotation = Orientation * correction;


            //transform.up = (hangPoint.position - transform.position).normalized;

            //transform.rotation = Quaternion.identity;



        }
        //UpdateRope();



    }




    public Vector3 getLinearVelocity()
    {
        float cosTheta = Mathf.Cos(thetaRadian);
        float sinTheta = Mathf.Sin(thetaRadian);
        float cosPhi = Mathf.Cos(phiRadian);
        float sinPhi = Mathf.Sin(phiRadian);

        float vx = ropeLength * (thetaAngularVelo * cosTheta * cosPhi - phiAngularVelo * sinTheta * sinPhi);
        float vy = ropeLength * thetaAngularVelo * sinTheta;
        float vz = -ropeLength * (thetaAngularVelo * cosTheta * sinPhi + phiAngularVelo * sinTheta * cosPhi);

        return new Vector3(vx, vy, vz);
    }



    //last solution for the rotation of the bucket : 
    //private Quaternion initialRotation;
    //in the start method : initialRotation = transform.rotation;
    //in fixedUpdate :
    //Vector3 ropeDirection = (hangPoint.position - transform.position).normalized;
    //transform.rotation = Quaternion.FromToRotation(Vector3.up, ropeDirection)* initialRotation;



}

