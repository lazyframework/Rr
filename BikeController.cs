using UnityEngine;
using UnityEngine.InputSystem;

// Pasang di BMX_Bike. Dinyalakan/dimatikan oleh VehicleSystem.
// Keyboard: W/S gas, A/D belok, Space rem.
// Mobile: panggil SetMobileInput() dari joystick UI.
[RequireComponent(typeof(Rigidbody))]
public class BikeController : MonoBehaviour
{
    public float acceleration = 6f;
    public float maxSpeed = 9f;
    public float brakeForce = 12f;
    public float friction = 2f;
    public float turnSpeed = 90f;

    Rigidbody rb;
    float mobileThrottle, mobileSteer;
    bool mobileBrake;
    float throttle, steer;
    bool brake;

    void Awake() { rb = GetComponent<Rigidbody>(); }

    void OnDisable() { throttle = 0f; steer = 0f; brake = false; mobileThrottle = 0f; mobileSteer = 0f; mobileBrake = false; }

    public void SetMobileInput(float t, float s, bool b)
    {
        mobileThrottle = t; mobileSteer = s; mobileBrake = b;
    }

    void Update()
    {
        float kt = 0f, ks = 0f; bool kb = false;
        var k = Keyboard.current;
        if (k != null)
        {
            kt = (k.wKey.isPressed ? 1f : 0f) - (k.sKey.isPressed ? 1f : 0f);
            ks = (k.dKey.isPressed ? 1f : 0f) - (k.aKey.isPressed ? 1f : 0f);
            kb = k.spaceKey.isPressed;
        }
        throttle = Mathf.Clamp(kt + mobileThrottle, -1f, 1f);
        steer = Mathf.Clamp(ks + mobileSteer, -1f, 1f);
        brake = kb || mobileBrake;
    }

    void FixedUpdate()
    {
        Vector3 v = rb.linearVelocity;
        float fwd = Vector3.Dot(v, transform.forward);

        fwd += throttle * acceleration * Time.fixedDeltaTime;
        if (brake) fwd = Mathf.MoveTowards(fwd, 0f, brakeForce * Time.fixedDeltaTime);
        if (Mathf.Abs(throttle) < 0.01f) fwd = Mathf.MoveTowards(fwd, 0f, friction * Time.fixedDeltaTime);
        fwd = Mathf.Clamp(fwd, -maxSpeed * 0.3f, maxSpeed);

        rb.linearVelocity = transform.forward * fwd + Vector3.up * v.y;

        if (Mathf.Abs(fwd) > 0.2f)
        {
            float yaw = steer * turnSpeed * Mathf.Clamp01(Mathf.Abs(fwd) / 3f) * Mathf.Sign(fwd);
            rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, yaw * Time.fixedDeltaTime, 0f));
        }
    }
}
