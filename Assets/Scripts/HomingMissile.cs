using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class HomingMissile : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("The moving target to chase")]
    public Transform target;

    [Header("Physics")]
    // EVEN MORE THRUST: Ensures it's not just a tail-chase
    [SerializeField] private float thrust = 250f; 
    [SerializeField] private float maxAngularVelocity = 50f;

    [Header("PID Gains")]
    // TUNED GAINS: More aggressive 'P' but with a stronger 'D' to dampen it
    [SerializeField] private float p = 2.0f; // Was 1.0
    [SerializeField] private float i = 0.01f;
    [SerializeField] private float d = 0.5f; // Was 0.2

    private Rigidbody rb;
    private PIDController pitchPID;
    private PIDController yawPID;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.maxAngularVelocity = maxAngularVelocity;

        // Initialize PID controllers
        pitchPID = new PIDController(p, i, d);
        yawPID = new PIDController(p, i, d);

        // Point at the target to start
        if(target != null)
        {
            transform.LookAt(target);
        }
    }

    void FixedUpdate()
    {
        if (target == null)
        {
            // Target is lost or destroyed, destroy the missile
            Debug.Log("<color=yellow>Missile target lost - self-destructing</color>");
            Destroy(gameObject);
            return;
        }

        // --- PID Guidance Logic ---
        Vector3 directionToTarget = (target.position - transform.position).normalized;
        float pitchError = Vector3.SignedAngle(transform.forward, directionToTarget, transform.right);
        float yawError = Vector3.SignedAngle(transform.forward, directionToTarget, transform.up);

        float pitchTorque = pitchPID.Update(pitchError, Time.fixedDeltaTime);
        float yawTorque = yawPID.Update(yawError, Time.fixedDeltaTime);

        rb.AddTorque(transform.right * pitchTorque + transform.up * yawTorque);
        // --- End of Logic ---

        // Apply constant forward thrust
        rb.AddForce(transform.forward * thrust);
    }

    // On collision, destroy both
    void OnCollisionEnter(Collision collision)
    {
        if (collision.transform == target)
        {
            Debug.Log($"<color=red>*** TARGET DESTROYED *** Missile hit {target.name} at {target.position}</color>");
            Destroy(target.gameObject); // Destroy the target
            Destroy(gameObject);        // Destroy the missile
        }
        else
        {
            Debug.Log($"<color=orange>Missile collided with {collision.gameObject.name} (not the target)</color>");
        }
    }
}