//  swept sphere-plane continuous collision detection

void SweptSpherePlaneCollision(inout float3 pos, inout float3 vel, float3 prevPos, float radius, float restitution, float tangentDamping,
    float3 canvasCentre, float3 canvasNormal, float3 canvasRight, float3 canvasUp, float2 canvasHalfSize)
{
    float3 n = canvasNormal;

    float s0 = dot(prevPos - canvasCentre, n) - radius;
    float s1 = dot(pos - canvasCentre, n) - radius;

    //if (s0 > 0 && s1 <= 0)
   //{
        //float tau = s0 / (s0 - s1);
       // float3 contact = prevPos + tau * (pos - prevPos);

        //float u = dot(contact - canvasCentre, canvasRight);
        //float v = dot(contact - canvasCentre, canvasUp);

        //if (abs(u) <= canvasHalfSize.x && abs(v) <= canvasHalfSize.y)
        //{
          //  float3 vn = dot(vel, n) * n;
          //  float3 vt = vel - vn;

          //  vel = -restitution * vn + (1 - tangentDamping) * vt;
          //  pos = contact + n * radius;
       // }
  //  }
    if (s0 > 0 && s1 <= 0)
    {
        float tau = s0 / (s0 - s1);
        float3 contact = prevPos + tau * (pos - prevPos);

        float u = dot(contact - canvasCentre, canvasRight);
        float v = dot(contact - canvasCentre, canvasUp);

        if (abs(u) <= canvasHalfSize.x && abs(v) <= canvasHalfSize.y)
        {
            vel = float3(0, 0, 0); // TEMP: freeze on contact
            pos = contact;
        }
    }
}

//void SweptSpherePlaneCollision(inout float3 pos, inout float3 vel, float3 prevPos, float radius, float restitution, float tangentDamping,
//    float3 canvasCentre, float3 canvasNormal, float3 canvasRight, float3 canvasUp, float2 canvasHalfSize)
//{
//    float3 n = canvasNormal;
//
//    float s0 = dot(prevPos - canvasCentre, n) - radius;
//    float s1 = dot(pos - canvasCentre, n) - radius;
//
//    // TEMP DEBUG: force visible color/velocity change for ALL particles, regardless of condition
//    vel += canvasNormal * 0.001; // tiny nudge, won't break sim but proves the function runs
//
//    if (s0 > 0 && s1 <= 0)
//    {
//        vel = float3(0, 10, 0); // TEMP: launch particles that cross the plane straight up
//    }
//}