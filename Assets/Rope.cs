using UnityEngine;

public class Rope : MonoBehaviour
{
    public Transform hangingPoint;
    public Transform bucket;

    private LineRenderer line;

    void Start()
    {
        line = GetComponent<LineRenderer>();

        line.positionCount = 2;
    }

    void Update()
    {
        line.SetPosition(0, hangingPoint.position);
        line.SetPosition(1, bucket.position);
    }
}