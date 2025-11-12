using UnityEngine;

public class AirspaceSpawner : MonoBehaviour
{
    [Header("Prefab Settings")]
    public GameObject[] prefabs; // Array of aircraft/object prefabs to spawn
    
    [Header("Airspace Settings")]
    public float radius = 100f; // Radius of the airspace
    public float altitude = 50f; // Default altitude (Y position)
    
    [Header("Spawn Settings")]
    public int numberOfObjects = 10; // How many objects to spawn
    public float spawnInterval = 1f; // Time between spawns (0 = spawn all at once)
    public bool spawnOnStart = true;
    public bool loopSpawning = true; // Enable continuous spawning
    
    [Header("Travel Settings")]
    public float minSpeed = 10f; // Minimum travel speed
    public float maxSpeed = 30f; // Maximum travel speed
    public bool randomizeSpeed = true;
    
    [Header("Position Randomization")]
    public bool randomizeStartPosition = true;
    public bool randomizeEndPosition = true;
    [Range(0f, 360f)]
    public float startAngleMin = 0f; // Minimum starting angle (degrees)
    [Range(0f, 360f)]
    public float startAngleMax = 360f; // Maximum starting angle (degrees)
    [Range(0f, 360f)]
    public float endAngleMin = 0f; // Minimum ending angle (degrees)
    [Range(0f, 360f)]
    public float endAngleMax = 360f; // Maximum ending angle (degrees)
    
    [Header("Altitude Randomization")]
    public bool randomizeAltitude = false;
    public float minAltitude = 30f;
    public float maxAltitude = 70f;
    
    [Header("Radius Randomization")]
    public bool randomizeRadiusOffset = false;
    public float minRadiusOffset = -10f;
    public float maxRadiusOffset = 10f;
    
    [Header("Destruction Settings")]
    public bool destroyOnArrival = true;
    public float destroyDelay = 0f;

    private float spawnTimer = 0f;
    private int spawnedCount = 0;

    void Start()
    {
        if (spawnOnStart && spawnInterval == 0f)
        {
            // Spawn all objects at once
            for (int i = 0; i < numberOfObjects; i++)
            {
                SpawnObject();
            }
        }
    }

    void Update()
    {
        if (spawnOnStart && spawnInterval > 0f)
        {
            spawnTimer += Time.deltaTime;
            if (spawnTimer >= spawnInterval)
            {
                // Check if we should continue spawning
                if (loopSpawning || spawnedCount < numberOfObjects)
                {
                    SpawnObject();
                    spawnTimer = 0f;
                    
                    // Reset counter if looping
                    if (loopSpawning && spawnedCount >= numberOfObjects)
                    {
                        spawnedCount = 0;
                    }
                }
            }
        }
    }

    public void SpawnObject()
    {
        if (prefabs == null || prefabs.Length == 0)
        {
            Debug.LogWarning("No prefabs assigned to AirspaceSpawner!");
            return;
        }

        // Select random prefab
        GameObject prefab = prefabs[Random.Range(0, prefabs.Length)];
        
        // Calculate radius with optional randomization
        float spawnRadius = radius;
        if (randomizeRadiusOffset)
        {
            spawnRadius += Random.Range(minRadiusOffset, maxRadiusOffset);
        }

        // Calculate altitude
        float spawnAltitude = randomizeAltitude 
            ? Random.Range(minAltitude, maxAltitude) 
            : altitude;

        // Calculate start and end angles
        float startAngle = randomizeStartPosition 
            ? Random.Range(startAngleMin, startAngleMax) 
            : startAngleMin;
        
        float endAngle = randomizeEndPosition 
            ? Random.Range(endAngleMin, endAngleMax) 
            : endAngleMin;

        // Convert angles to radians and calculate positions on X-Z plane
        float startRad = startAngle * Mathf.Deg2Rad;
        float endRad = endAngle * Mathf.Deg2Rad;

        // Positions on X-Z plane with Y as altitude
        Vector3 startPos = transform.position + new Vector3(
            Mathf.Cos(startRad) * spawnRadius,
            spawnAltitude,
            Mathf.Sin(startRad) * spawnRadius
        );

        Vector3 endPos = transform.position + new Vector3(
            Mathf.Cos(endRad) * spawnRadius,
            spawnAltitude,
            Mathf.Sin(endRad) * spawnRadius
        );

        // Calculate speed
        float speed = randomizeSpeed 
            ? Random.Range(minSpeed, maxSpeed) 
            : minSpeed;

        // Instantiate object
        GameObject obj = Instantiate(prefab, startPos, Quaternion.identity);
        
        // Add movement component
        ObjectMover mover = obj.AddComponent<ObjectMover>();
        mover.Initialize(startPos, endPos, speed, destroyOnArrival, destroyDelay);

        spawnedCount++;
    }

    // Helper method to spawn manually
    public void SpawnSingleObject()
    {
        SpawnObject();
    }

    // Reset spawn counter
    public void ResetSpawnCounter()
    {
        spawnedCount = 0;
    }

    // Stop/Start spawning
    public void StopSpawning()
    {
        spawnOnStart = false;
    }

    public void StartSpawning()
    {
        spawnOnStart = true;
    }

    // Visualize the airspace in editor
    void OnDrawGizmosSelected()
    {
        // Draw main altitude circle
        Gizmos.color = Color.yellow;
        DrawCircleXZ(transform.position, radius, altitude, 64);
        
        // Draw radius variation circles if enabled
        if (randomizeRadiusOffset)
        {
            Gizmos.color = Color.cyan;
            DrawCircleXZ(transform.position, radius + minRadiusOffset, altitude, 64);
            DrawCircleXZ(transform.position, radius + maxRadiusOffset, altitude, 64);
        }
        
        // Draw altitude variation circles if enabled
        if (randomizeAltitude)
        {
            Gizmos.color = Color.green;
            DrawCircleXZ(transform.position, radius, minAltitude, 64);
            Gizmos.color = Color.red;
            DrawCircleXZ(transform.position, radius, maxAltitude, 64);
        }
        
        // Draw vertical lines to show altitude range
        if (randomizeAltitude)
        {
            Gizmos.color = Color.white;
            for (int i = 0; i < 8; i++)
            {
                float angle = i * 45f * Mathf.Deg2Rad;
                Vector3 basePos = transform.position + new Vector3(
                    Mathf.Cos(angle) * radius,
                    minAltitude,
                    Mathf.Sin(angle) * radius
                );
                Vector3 topPos = basePos + Vector3.up * (maxAltitude - minAltitude);
                Gizmos.DrawLine(basePos, topPos);
            }
        }
    }

    void DrawCircleXZ(Vector3 center, float rad, float height, int segments)
    {
        float angleStep = 360f / segments;
        Vector3 prevPoint = center + new Vector3(rad, height, 0);
        
        for (int i = 1; i <= segments; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            Vector3 newPoint = center + new Vector3(
                Mathf.Cos(angle) * rad, 
                height, 
                Mathf.Sin(angle) * rad
            );
            Gizmos.DrawLine(prevPoint, newPoint);
            prevPoint = newPoint;
        }
    }
}

// Separate component to handle object movement
public class ObjectMover : MonoBehaviour
{
    private Vector3 startPosition;
    private Vector3 endPosition;
    private float speed;
    private bool shouldDestroy;
    private float destroyDelay;
    private float journeyLength;
    private float startTime;

    public void Initialize(Vector3 start, Vector3 end, float moveSpeed, bool destroy, float delay)
    {
        startPosition = start;
        endPosition = end;
        speed = moveSpeed;
        shouldDestroy = destroy;
        destroyDelay = delay;
        
        journeyLength = Vector3.Distance(start, end);
        startTime = Time.time;
    }

    void Update()
    {
        float distanceCovered = (Time.time - startTime) * speed;
        float fractionOfJourney = distanceCovered / journeyLength;

        transform.position = Vector3.Lerp(startPosition, endPosition, fractionOfJourney);

        // Rotate to face movement direction on X-Z plane
        if (fractionOfJourney < 1f)
        {
            Vector3 direction = (endPosition - startPosition).normalized;
            if (direction != Vector3.zero)
            {
                // Calculate rotation on Y axis to face direction in X-Z plane
                transform.rotation = Quaternion.LookRotation(direction);
            }
        }

        if (fractionOfJourney >= 1f)
        {
            if (shouldDestroy)
            {
                Destroy(gameObject, destroyDelay);
            }
            enabled = false; // Stop updating
        }
    }
}