//  swept sphere-plane continuous collision detection

//void SweptSpherePlaneCollision(inout float3 pos, inout float3 vel, float3 prevPos, float radius, float restitution, float tangentDamping,
//    float3 canvasCentre, float3 canvasNormal, float3 canvasRight, float3 canvasUp, float2 canvasHalfSize)
//{
//    float3 n = canvasNormal;
//
//    float s0 = dot(prevPos - canvasCentre, n) - radius;
//    float s1 = dot(pos - canvasCentre, n) - radius;
//
//
//    if (s0 > 0 && s1 <= 0)
//    {
//        float tau = s0 / (s0 - s1);
//        float3 contact = prevPos + tau * (pos - prevPos);
//
//        float u = dot(contact - canvasCentre, canvasRight);
//        float v = dot(contact - canvasCentre, canvasUp);
//
//        if (abs(u) <= canvasHalfSize.x && abs(v) <= canvasHalfSize.y)
//        {
//            //vel = float3(0, 0, 0); // TEMP: freeze on contact
//            //pos = contact;
//            float3 vn = dot(vel, n) * n;
//            float3 vt = vel - vn;
//
//            //vel = -restitution * vn + (1 - tangentDamping) * vt;
//            vel = float3(0, 0, 0);
//            pos = contact + n * radius;
//        }
//    }
//}

void SweptSpherePlaneCollision(inout float3 pos, inout float3 vel, float3 prevPos, float radius,
    float restitution, float tangentDamping,
    float3 canvasCentre, float3 canvasNormal, float3 canvasRight, float3 canvasUp, float2 canvasHalfSize,
    out float2 hitUV, out bool didHit)
{
    didHit = false;
    hitUV = float2(0, 0);

    float3 n = canvasNormal;
    float s0 = dot(prevPos - canvasCentre, n) - radius;
    float s1 = dot(pos - canvasCentre, n) - radius;

    if (s0 > 0 && s1 <= 0)
    {
        float tau = s0 / (s0 - s1);
        float3 contact = prevPos + tau * (pos - prevPos);

        float u = dot(contact - canvasCentre, canvasRight);
        float v = dot(contact - canvasCentre, canvasUp);

        if (abs(u) <= canvasHalfSize.x && abs(v) <= canvasHalfSize.y)
        {
            vel = float3(0, 0, 0);
            pos = contact + n * radius;

            hitUV = float2(
                (u / canvasHalfSize.x + 1.0) * 0.5,
                (v / canvasHalfSize.y + 1.0) * 0.5
            );
            didHit = true;
        }
    }
}