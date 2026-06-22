using System;
using UnityEngine;
using UnityEngine.UIElements;
using static TMPro.SpriteAssetUtilities.TexturePacker_JsonArray;

public class Pendulum : MonoBehaviour
{
    //To connect the object in the scen with the script, they must have identical names to the ones in the scene


    public float bucketRadius = 1.0f;
    [SerializeField] float thetaAngularVelocity = 30.0f;
    [SerializeField] float phiAngularVelocity = 30.0f;

    [SerializeField] float thetaDegree = 45.0f;
    [SerializeField] float phiDegree = 45.0f;
    [SerializeField] float gravity = 9.81f;
    [SerializeField] float ropeLength = 10.0f;
    [SerializeField] float airDensity = 1.225f;
    //[SerializeField] float mass = 1.0f;

    [SerializeField] Transform hangPoint;
    [SerializeField] Transform rope;

    private float thetaRadian;
    private float phiRadian;
    private float thetaAngularVelo;
    private float phiAngularVelo;
    private bool isDragging = false;
    private float cameraDistance;



    public void OnMouseDown()
    {
        isDragging = true;
        cameraDistance = Vector3.Distance(Camera.main.transform.position, transform.position);
        thetaAngularVelo = 0.0f;
        phiAngularVelo = 0.0f;
    }

    public void OnMouseDrag()
    {
        // create an invisible plane that faces the camera (like a sheet of glass) at the height of the hang point
        Plane dragPlane = new Plane(-Camera.main.transform.forward, hangPoint.position);

        // shoot a ray from the mouse position
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        // find where the ray hits the plane
        if (dragPlane.Raycast(ray, out float distance))
        {
            Vector3 hitPoint = ray.GetPoint(distance);

            // constrain the position to the rope length 
            Vector3 offset = hitPoint - hangPoint.position;
            offset = offset.normalized * ropeLength;
            Vector3 constrainedPos = hangPoint.position + offset;

            // apply position and update the rope visually
            transform.position = constrainedPos;
            //UpdateRope();

            // convert the new position back to Theta and Phi radians
            Vector3 relPos = constrainedPos - hangPoint.position;

            //added Mathf.Clamp to prevent math errors if you drag directly above the pivot
            thetaRadian = Mathf.Acos(Mathf.Clamp(-relPos.y / ropeLength, -1f, 1f));
            phiRadian = Mathf.Atan2(-relPos.z, relPos.x);

            thetaDegree = thetaRadian * Mathf.Rad2Deg;
            phiDegree = phiRadian * Mathf.Rad2Deg;
        }
    }

    void OnMouseUp()
    {
        isDragging = false;
    }



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





    float getThetaAngularAcceleration(float thetaAngularVelo, float phiAngularVelo, float thetaRadian)
    {
        float sinTheta = Mathf.Sin(thetaRadian);
        if (Mathf.Abs(sinTheta) < 0.001f)
        {
            sinTheta = 0.001f + 0.0001f;
        }

        float thetaAngularAcceleration = Mathf.Pow(phiAngularVelo, 2) * Mathf.Sin(thetaRadian) * Mathf.Cos(thetaRadian) - gravity * Mathf.Sin(thetaRadian) / ropeLength - (0.5f * airDensity * Mathf.PI * Mathf.Pow(bucketRadius, 2)) * 0.5f * thetaAngularVelo / Mathf.Pow(ropeLength, 2);
        return thetaAngularAcceleration;
    }

    float getPhiAngularAcceleration(float thetaAngularVelo, float phiAngularVelo, float thetaRadian)
    {

        float sinTheta = Mathf.Sin(thetaRadian);
        if (Mathf.Abs(sinTheta) < 0.001f)
        {
            sinTheta = 0.001f + 0.0001f;
        }
        float phiAngularAcceleration = -2 * (Mathf.Cos(thetaRadian) / sinTheta * thetaAngularVelo * phiAngularVelo) - (0.5f * airDensity * Mathf.PI * Mathf.Pow(bucketRadius, 2)) * 0.5f * phiAngularVelo / Mathf.Pow(ropeLength, 2);
        return phiAngularAcceleration;
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

        thetaRadian = Mathf.Deg2Rad * thetaDegree;
        phiRadian = Mathf.Deg2Rad * phiDegree;
        thetaAngularVelo = Mathf.Deg2Rad * thetaAngularVelocity;
        phiAngularVelo = Mathf.Deg2Rad * phiAngularVelocity;
        UpdateRope();
        transform.position = getPosition() + hangPoint.position;


    }


    private void FixedUpdate()
    {
        if (!isDragging)
        {
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

        }
        //UpdateRope();



    }


    void UpdateRope()
    {
        Vector3 direction = transform.position - hangPoint.position;  //pivot to bucket
        rope.position = hangPoint.position + direction * 0.5f; //to place the rope halfway between the pivot and the bucket as meshes are modeled centered around their own origin.
        rope.up = direction.normalized;
        Vector3 scale = rope.localScale;
        scale.y = direction.magnitude;   //compute the length of the rope and set it as the y scale of the rope
        rope.localScale = scale; //to stretch the rope to the correct length
    }


    public Vector3 getLinearVelocity()
    {
        return new Vector3(thetaAngularVelo / ropeLength, phiAngularVelo / ropeLength, 0.0f);
    }



}

