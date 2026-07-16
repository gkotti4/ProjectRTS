using UnityEngine;

/// <summary>
/// Temporary runtime harness for testing SoldierMotor mass and impulse behavior.
/// Attach this to any active scene object, then assign the target/source soldiers.
/// Delete it once the impulse foundation is validated.
/// </summary>
public class SoldierImpulseTester : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SoldierMotor targetMotor; // populate manually at runtime
    [SerializeField] private SoldierMotor sourceMotor;

    [Header("External Impulse Test")]
    [SerializeField] private Vector3 externalImpulseDirection = Vector3.forward;
    [SerializeField] private float externalImpulseMagnitude = 4f;
    [SerializeField] private float externalImpulseDuration = 0.25f;

    [Header("Body Impulse Test")]
    [SerializeField] private float bodyImpulseMultiplier = 1f;
    [SerializeField] private float bodyImpulseDuration = 0.25f;

    void Update()
    {
        if (targetMotor == null)
            return;

        if (Input.GetKeyDown(KeyCode.I))
            TestExternalImpulse();

        if (Input.GetKeyDown(KeyCode.O))
            TestBodyImpulse();

        if (Input.GetKeyDown(KeyCode.P))
            targetMotor.ClearExternalImpulse();

        if (Input.GetKeyDown(KeyCode.L))
            LogCurrentValues();
    }

    void TestExternalImpulse()
    {
        Vector3 direction = externalImpulseDirection;

        if (direction.sqrMagnitude <= 0.0001f)
            direction = transform.forward;

        targetMotor.ApplyExternalImpulse(
            direction,
            externalImpulseMagnitude,
            externalImpulseDuration);

        Debug.Log(
            $"External impulse applied. " +
            $"Impulse={externalImpulseMagnitude:F2}, " +
            $"TargetMass={targetMotor.BodyMass:F2}, " +
            $"ExpectedInitialSpeed=" +
            $"{externalImpulseMagnitude / targetMotor.BodyMass:F2} m/s",
            targetMotor);
    }

    void TestBodyImpulse()
    {
        if (sourceMotor == null)
        {
            Debug.LogWarning(
                "Body impulse test requires a Source Motor.",
                this);

            return;
        }

        Vector3 direction =
            targetMotor.transform.position -
            sourceMotor.transform.position;

        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
            direction = sourceMotor.transform.forward;

        targetMotor.ApplyBodyImpulse(
            sourceMotor,
            direction,
            bodyImpulseMultiplier,
            bodyImpulseDuration);

        Debug.Log(
            $"Body impulse requested. " +
            $"SourceMass={sourceMotor.BodyMass:F2}, " +
            $"SourceVelocity={sourceMotor.Velocity.magnitude:F2}, " +
            $"Multiplier={bodyImpulseMultiplier:F2}",
            targetMotor);
    }

    void LogCurrentValues()
    {
        Debug.Log(
            $"TargetMass={targetMotor.BodyMass:F2}, " +
            $"IsBeingPushed={targetMotor.IsBeingPushed}, " +
            $"PushVelocity={targetMotor.ExternalPushVelocity}, " +
            $"PushSpeed={targetMotor.ExternalPushVelocity.magnitude:F2}",
            targetMotor);
    }
}