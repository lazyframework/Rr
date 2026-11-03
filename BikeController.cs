using UnityEngine;
using UnityEngine.InputSystem;
using StarterAssets;

// Pasang di BMX_Bike. Dinyalakan/dimatikan oleh VehicleSystem.
// Input yang didukung (semuanya dijumlahkan):
//  1. Keyboard: W/S gas, A/D belok, Space rem
//  2. Joystick Starter Assets (otomatis dari StarterAssetsInputs.move milik pemain)
//  3. Joystick lain lewat BikeJoystickBridge, atau panggil SetMobileMove()
//  4. Tombol rem UI: BrakeDown() / BrakeUp()
[RequireComponent(typeof(Rigidbody))]
public class BikeController : MonoBehaviour
{
    public float acceleration = 6f;
    public float maxSpeed = 9f;
    public float brakeForce = 12f;
    public float friction = 2f;
    public float turnSpeed = 90f;

    [Header("Joystick")]
    public bool useStarterAssetsJoystick = true;
    public float deadZone = 0.1f;

    Rigidbody rb;
    StarterAssetsInputs saInputs;

    float mobileThrottle, mobileSteer;
    bool mobileBrake;
    float throttle, steer;
    bool brake;

    public float Steer => steer;   // dibaca BikeDrive untuk memutar setir

    void Awake()
    {
        rb = GetComponent<Rigidbody>();

        // Box collider tanpa material punya gesekan 0.6 yang melawan gas.
        var slide = new PhysicsMaterial("BikeSlide")
        {
            dynamicFriction = 0f,
            staticFriction = 0f,
            bounciness = 0f,
            frictionCombine = PhysicsMaterialCombine.Minimum,
            bounceCombine = PhysicsMaterialCombine.Minimum
        };
        foreach (var c in GetComponentsInChildren<Collider>())
            if (!c.isTrigger) c.sharedMaterial = slide;
    }

    void OnEnable()
    {
        if (saInputs == null) saInputs = FindFirstObjectByType<StarterAssetsInputs>();
        if (saInputs != null) saInputs.move = Vector2.zero;   // buang sisa input lama
    }

    void OnDisable()
    {
        throttle = 0f; steer = 0f; brake = false;
        mobileThrottle = 0f; mobileSteer = 0f; mobileBrake = false;
    }

    // ---- API untuk joystick / tombol UI ----
    public void SetMobileMove(float t, float s) { mobileThrottle = t; mobileSteer = s; }
    public void SetMobileMove(Vector2 v) { mobileThrottle = v.y; mobileSteer = v.x; }
    public void SetMobileBrake(bool b) { mobileBrake = b; }
    public void BrakeDown() { mobileBrake = true; }
    public void BrakeUp() { mobileBrake = false; }
    public void SetMobileInput(float t, float s, bool b) { mobileThrottle = t; mobileSteer = s; mobileBrake = b; }

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

        float jt = 0f, js = 0f;
        if (useStarterAssetsJoystick && saInputs != null)
        {
            jt = saInputs.move.y;
            js = saInputs.move.x;
        }

        throttle = Clamp(kt + mobileThrottle + jt);
        steer = Clamp(ks + mobileSteer + js);
        brake = kb || mobileBrake;
    }

    float Clamp(float v)
    {
        v = Mathf.Clamp(v, -1f, 1f);
        return Mathf.Abs(v) < deadZone ? 0f : v;
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
