using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem; // Tambahan untuk membaca Keyboard

// Pasang di Car_16 (objek induk mobil). 
// Script ini sekarang bisa memutar ban, membelokkan ban, SEKALIGUS menggerakkan mobil pakai WASD.
public class CarWheelVisuals : MonoBehaviour
{
    [Header("Ban (kosongkan untuk dicari otomatis)")]
    public Transform[] frontWheels;      // berputar + belok
    public Transform[] rearWheels;       // berputar
    public bool autoFindWheels = true;
    public string[] keywords = { "wheel", "tire", "tyre", "ban", "roda" };

    [Header("Pengaturan Visual Ban")]
    public float wheelRadius = 0f;       // 0 = otomatis dari ukuran ban
    public float wheelBase = 2.6f;       // jarak as depan ke as belakang (m)
    public float maxSteerAngle = 30f;
    public float steerSpeed = 200f;
    public bool invertSpin = false;      // ban berputar mundur? centang
    public bool invertSteer = false;     // ban belok berlawanan? centang

    [Header("Pengaturan Kendali Mobil (WASD)")]
    public float motorForce = 15f;       // Kecepatan akselerasi mobil
    public float maxSpeed = 15f;         // Kecepatan maksimal mobil
    public float turnSpeed = 60f;        // Kecepatan belok mobil

    class W { public Transform t; public Vector3 localCenter; public bool front; public float radius; }
    readonly List<W> wheels = new List<W>();
    
    Vector3 lastPos;
    float lastYaw, visualSteer;
    
    // Variabel untuk fisika mobil
    Rigidbody rb;
    float currentSpeed = 0f;
    float actualSteerAngle = 0f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("CarWheelVisuals: Mobil tidak punya Rigidbody! Tambahkan komponen Rigidbody di Car_16 agar bisa bergerak.");
        }

        if (autoFindWheels && (frontWheels == null || frontWheels.Length == 0) && (rearWheels == null || rearWheels.Length == 0))
            AutoFind();
        else
        {
            if (frontWheels != null) foreach (var t in frontWheels) Add(t, true);
            if (rearWheels != null) foreach (var t in rearWheels) Add(t, false);
        }

        if (wheels.Count == 0) Debug.LogWarning("CarWheelVisuals: tidak ada ban ditemukan.");

        lastPos = transform.position;
        lastYaw = transform.eulerAngles.y;
    }

    void AutoFind()
    {
        var found = new List<Transform>();
        foreach (var c in GetComponentsInChildren<Transform>(true))
        {
            if (c == transform) continue;
            string n = c.name.ToLower();
            bool match = false;
            foreach (var k in keywords) if (n.Contains(k)) { match = true; break; }
            if (!match || c.GetComponentInChildren<Renderer>() == null) continue;

            bool nested = false;
            foreach (var f in found) if (c.IsChildOf(f)) { nested = true; break; }
            if (!nested) found.Add(c);
        }

        float meanZ = 0f;
        foreach (var t in found) meanZ += transform.InverseTransformPoint(CenterOf(t)).z;
        if (found.Count > 0) meanZ /= found.Count;

        foreach (var t in found)
            Add(t, transform.InverseTransformPoint(CenterOf(t)).z > meanZ);
    }

    Vector3 CenterOf(Transform t)
    {
        var r = t.GetComponentInChildren<Renderer>();
        return r != null ? r.bounds.center : t.position;
    }

    void Add(Transform t, bool front)
    {
        if (t == null) return;
        var r = t.GetComponentInChildren<Renderer>();
        var w = new W { t = t, front = front };
        w.localCenter = t.InverseTransformPoint(r != null ? r.bounds.center : t.position);

        float rad = wheelRadius;
        if (rad <= 0f && r != null)
        {
            float[] e = { r.bounds.extents.x, r.bounds.extents.y, r.bounds.extents.z };
            System.Array.Sort(e);
            rad = (e[1] + e[2]) * 0.5f;
        }
        w.radius = Mathf.Max(0.05f, rad);
        wheels.Add(w);
    }

    void FixedUpdate()
    {
        if (rb == null) return;

        // 1. BACA INPUT KEYBOARD (WASD)
        float throttle = 0f;
        float steerInput = 0f;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed) throttle = 1f;
            if (Keyboard.current.sKey.isPressed) throttle = -1f;

            if (Keyboard.current.dKey.isPressed) steerInput = 1f;
            if (Keyboard.current.aKey.isPressed) steerInput = -1f;
        }

        // 2. FISIKA MAJU/MUNDUR
        // Jika script ini aktif, mobil bisa digerakkan.
        currentSpeed = Mathf.MoveTowards(currentSpeed, throttle * maxSpeed, motorForce * Time.fixedDeltaTime);
        
        // Terapkan kecepatan ke Rigidbody
        Vector3 moveDirection = transform.forward * currentSpeed;
        rb.linearVelocity = new Vector3(moveDirection.x, rb.linearVelocity.y, moveDirection.z);

        // 3. FISIKA BELOK
        if (Mathf.Abs(currentSpeed) > 0.5f)
        {
            actualSteerAngle = Mathf.MoveTowards(actualSteerAngle, steerInput * maxSteerAngle, steerSpeed * Time.fixedDeltaTime);
            float turnAmount = actualSteerAngle * turnSpeed * Mathf.Sign(currentSpeed) * Time.fixedDeltaTime;
            
            Quaternion turnRotation = Quaternion.Euler(0f, turnAmount, 0f);
            rb.MoveRotation(rb.rotation * turnRotation);
        }
        else
        {
            actualSteerAngle = Mathf.MoveTowards(actualSteerAngle, 0f, steerSpeed * Time.fixedDeltaTime);
        }
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        // Hitung kecepatan roda berdasarkan pergerakan mobil
        Vector3 delta = transform.position - lastPos;
        float speed = Vector3.Dot(delta, transform.forward) / dt;
        float yaw = transform.eulerAngles.y;
        float yawRate = Mathf.DeltaAngle(lastYaw, yaw) / dt;
        
        lastPos = transform.position;
        lastYaw = yaw;
        
        if (Mathf.Abs(speed) > 80f) return;

        // Hitung sudut belok visual roda depan
        float target = 0f;
        if (Mathf.Abs(speed) > 0.5f)
            target = Mathf.Atan(wheelBase * yawRate * Mathf.Deg2Rad / speed) * Mathf.Rad2Deg;
        
        target = Mathf.Clamp(target, -maxSteerAngle, maxSteerAngle) * (invertSteer ? -1f : 1f);

        float prev = visualSteer;
        visualSteer = Mathf.MoveTowards(visualSteer, target, steerSpeed * dt);
        float dSteer = visualSteer - prev;

        // Putar mesh roda
        foreach (var w in wheels)
        {
            if (w.t == null) continue;
            Vector3 c = w.t.TransformPoint(w.localCenter);

            if (w.front && Mathf.Abs(dSteer) > 0.0001f)
                w.t.RotateAround(c, transform.up, dSteer);

            Vector3 axle = w.front ? Quaternion.AngleAxis(visualSteer, transform.up) * transform.right : transform.right;
            float deg = speed * dt / w.radius * Mathf.Rad2Deg * (invertSpin ? -1f : 1f);
            w.t.RotateAround(c, axle, deg);
        }
    }
}
