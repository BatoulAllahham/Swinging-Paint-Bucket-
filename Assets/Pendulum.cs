using System;
using UnityEngine;
using UnityEngine.UIElements;
using static TMPro.SpriteAssetUtilities.TexturePacker_JsonArray;

public class Pendulum : MonoBehaviour
{
    //To connect the object in the scen with the script, they must have identical names to the ones in the scene


    public float bucketRadius = 1.0f;
    //public float airDensity = 1.225f;
    [SerializeField] float thetaAngularVelocity = 30.0f;
    [SerializeField] float phiAngularVelocity = 30.0f;

    [SerializeField] float thetaDegree = 45.0f;
    [SerializeField] float phiDegree = 45.0f;
    [SerializeField] float gravity = 9.81f;
    [SerializeField] float ropeLength = 10.0f;
    [SerializeField] float airDensity = 1.225f;
    //[SerializeField] float mass = 1.0f;

    [SerializeField] Transform hangPoint;
    //[SerializeField] float hangPointRadius = 0.5f;
    [SerializeField] Transform rope;

    private float thetaRadian;
    private float phiRadian;
    private float thetaAngularVelo;
    private float phiAngularVelo;




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



    // Start is called once before the first execution of Update after the MonoBehaviour is created
    //We put here all the initializations
    void Start()
    {
       
        thetaRadian = Mathf.Deg2Rad * thetaDegree;
        phiRadian = Mathf.Deg2Rad * phiDegree;
        thetaAngularVelo = Mathf.Deg2Rad * thetaAngularVelocity;
        phiAngularVelo = Mathf.Deg2Rad * phiAngularVelocity;
        UpdateRope();
        transform.position = getPosition() + hangPoint.position;
        //UpdateRope();

        Debug.Log("Debug");

    }





    float getThetaAngularAcceleration(float thetaAngularVelo, float phiAngularVelo, float thetaRadian)
    {
        float sinTheta = Mathf.Sin(thetaRadian);
        if (Mathf.Abs(sinTheta) < 0.001f)
        {
            sinTheta = 0.001f + 0.0001f;
        }

        float thetaAngularAcceleration = Mathf.Pow(phiAngularVelo, 2) * Mathf.Sin(thetaRadian) * Mathf.Cos(thetaRadian) - gravity  * Mathf.Sin(thetaRadian) / ropeLength  - (0.5f * airDensity * Mathf.PI * Mathf.Pow(bucketRadius, 2)) * 0.5f* thetaAngularVelo / Mathf.Pow(ropeLength,2);
        return thetaAngularAcceleration;
    }

    float getPhiAngularAcceleration(float thetaAngularVelo, float phiAngularVelo, float thetaRadian)
    {

        float sinTheta = Mathf.Sin(thetaRadian);
        if (Mathf.Abs(sinTheta) < 0.001f)
        {
            sinTheta = 0.001f + 0.0001f;
        }
        float phiAngularAcceleration = -2 * (Mathf.Cos(thetaRadian) / sinTheta * thetaAngularVelo * phiAngularVelo) - (0.5f * airDensity * Mathf.PI * Mathf.Pow(bucketRadius, 2)) *0.5f* phiAngularVelo / Mathf.Pow(ropeLength,2);
        return phiAngularAcceleration;
    }




    float[] Step(float thetaAngularVelo, float phiAngularVelo, float thetaRadian)
    {
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
       
        float[] k1 = Step(thetaAngularVelo, phiAngularVelo, thetaRadian);
        float[] k2 = Step(thetaAngularVelo + k1[0] * 0.5f * dt, phiAngularVelo + 0.5f * k1[1] * dt, thetaRadian + 0.5f * k1[2] * dt);
        float[] k3 = Step(thetaAngularVelo + 0.5f * k2[0] * dt, phiAngularVelo + 0.5f * k2[1] * dt, thetaRadian + 0.5f * k2[2] * dt);
        float[] k4 = Step(thetaAngularVelo + k3[0] * dt, phiAngularVelo + k3[1] * dt, thetaRadian + k3[2] * dt);

        float[] sum = {
            dt * (k1[0] + 2*k2[0] + 2*k3[0] + k4[0]) / 6,
            dt * (k1[1] + 2*k2[1] + 2*k3[1] + k4[1]) / 6,
            dt * (k1[2] + 2*k2[2] + 2*k3[2] + k4[2]) / 6,
            dt * (k1[3] + 2*k2[3] + 2*k3[3] + k4[3]) / 6
        };

        return sum;
    }






    private void FixedUpdate()
    {
        float[] state = RungeKutta_4th(Time.fixedDeltaTime);
        thetaAngularVelo += state[0];
        phiAngularVelo += state[1];

        thetaAngularVelocity = thetaAngularVelo * Mathf.Rad2Deg;
        phiAngularVelocity = phiAngularVelo * Mathf.Rad2Deg;

        thetaRadian += state[2];       
        phiRadian += state[3];

        thetaDegree = thetaRadian * Mathf.Rad2Deg;
        phiDegree = phiRadian * Mathf.Rad2Deg;

        transform.position = getPosition() + hangPoint.position;



        Quaternion Orientation = Quaternion.LookRotation(new Vector3(-transform.position.x, -transform.position.y, -transform.position.z));

        Quaternion correction = Quaternion.Inverse(
                                   Quaternion.LookRotation(Vector3.up, transform.position)
                                );
        transform.rotation = Orientation * correction;
        UpdateRope();



    }


    void UpdateRope()
    {
        Vector3 direction = transform.position - hangPoint.position;
        rope.position = hangPoint.position + direction * 0.5f;
        rope.up = direction.normalized;
        Vector3 scale = rope.localScale;
        scale.y = direction.magnitude;
        rope.localScale = scale;
    }


    public Vector3 getLinearVelocity()
    {
        return new Vector3(thetaAngularVelo / ropeLength, phiAngularVelo / ropeLength, 0.0f);
    }



}

