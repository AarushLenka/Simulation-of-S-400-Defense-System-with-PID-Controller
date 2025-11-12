using UnityEngine;

public class TargetMover : MonoBehaviour
{
    [Tooltip("Speed of the circular motion")]
    public float speed = 2f;
    [Tooltip("Radius of the circle")]
    public float radius = 10f;

    private Vector3 startPosition;

    void Start()
    {
        // Remember the center of the circle
        startPosition = transform.position;
    }

    void Update()
    {
        // Calculate the new position in a circle
        float x = Mathf.Sin(Time.time * speed) * radius;
        float z = Mathf.Cos(Time.time * speed) * radius;

        transform.position = startPosition + new Vector3(x, 0, z);
    }
}