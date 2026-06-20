using UnityEngine;

/// <summary>
/// REF §2.3 — Two states: Loiter → Dive
///
/// Loiter:
///     Circular orbit around spawn point at fixed altitude.
///
/// Dive:
///     Triggered permanently by isTracked.
///     Accelerates toward max speed.
///     Performs a true 60° dive.
///
/// Never returns to Loiter.
/// </summary>
public class UAVDrone : AerialTarget
{
    private enum State
    {
        Loiter,
        Dive
    }

    private State _state = State.Loiter;

    [Tooltip("Radius of loiter circle (m)")]
    public float loiterRadius = 1200f;

    [Tooltip("Rotation smoothing")]
    public float rotationSpeed = 3f;

    private Vector3 _loiterCenter;
    private float _loiterAltitude;

    private float desiredSpeed;

    protected override void InitializeTarget()
    {
        desiredSpeed = Random.Range(
            config.minSpeed,
            config.maxSpeed * 0.6f);

        currentSpeed = desiredSpeed;

        float groundY = GetTerrainYBelow();

        _loiterAltitude =
            groundY +
            Random.Range(
                config.minAltitude,
                config.maxAltitude);

        _loiterCenter = transform.position;
        _loiterCenter.y = 0f;

        //
        // Place drone on orbit circumference
        // instead of center.
        //
        transform.position += transform.right * loiterRadius;

        rb.linearVelocity =
            transform.forward * currentSpeed;
    }

    public override void UpdateMotion()
    {
        switch (_state)
        {
            case State.Loiter:
                UpdateLoiter();
                break;

            case State.Dive:
                UpdateDive();
                break;
        }

        UpdateRotation();

        currentAltitude = transform.position.y;

        Vector3 horizontalVel = rb.linearVelocity;
        horizontalVel.y = 0f;

        currentSpeed = horizontalVel.magnitude;
    }

    private void UpdateLoiter()
    {
        //
        // Radial vector from center.
        //
        Vector3 radial =
            transform.position - _loiterCenter;

        radial.y = 0f;

        if (radial.sqrMagnitude < 1f)
        {
            radial = transform.right * loiterRadius;
        }

        radial.Normalize();

        //
        // Tangent direction gives a perfect orbit.
        //
        Vector3 tangent =
            new Vector3(
                -radial.z,
                 0f,
                 radial.x);

        //
        // Correct radius drift.
        //
        float radiusError =
            radial.magnitude - loiterRadius;

        Vector3 correction =
            -radial * radiusError * 0.5f;

        Vector3 orbitVelocity =
            (tangent + correction).normalized
            * desiredSpeed;

        rb.linearVelocity = orbitVelocity;

        HoldAltitude(_loiterAltitude);

        if (isTracked)
        {
            _state = State.Dive;
        }
    }

    private void UpdateDive()
    {
        desiredSpeed = Mathf.MoveTowards(
            desiredSpeed,
            config.maxSpeed,
            3f * Time.fixedDeltaTime);

        Vector3 horizontalForward =
            new Vector3(
                rb.linearVelocity.x,
                0f,
                rb.linearVelocity.z);

        if (horizontalForward.sqrMagnitude < 0.01f)
        {
            horizontalForward = transform.forward;
        }

        horizontalForward.Normalize();

        //
        // True 60° dive.
        //
        float diveAngle = 60f * Mathf.Deg2Rad;

        Vector3 diveDirection =
            (
                horizontalForward * Mathf.Cos(diveAngle)
                +
                Vector3.down * Mathf.Sin(diveAngle)
            ).normalized;

        rb.linearVelocity = Vector3.Lerp(
            rb.linearVelocity,
            diveDirection * desiredSpeed,
            2f * Time.fixedDeltaTime);
    }

    private void UpdateRotation()
    {
        if (rb.linearVelocity.sqrMagnitude < 0.01f)
            return;

        Quaternion targetRotation =
            Quaternion.LookRotation(
                rb.linearVelocity.normalized);

        transform.rotation =
            Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.fixedDeltaTime);
    }
}