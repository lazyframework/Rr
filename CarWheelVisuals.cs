using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;

// Pasang di Car_16. Memutar ban, membelokkan ban, dan menggerakkan mobil pakai WASD.
// Mobil HANYA bisa digerakkan jika isPlayerInside bernilai true.
public class CarWheelVisuals : MonoBehaviour
{
    [Header("Ban (kosongkan untuk dicari otomatis)")]
    public Transform[] frontWheels;      
    public Transform[] rearWheels;       
    public bool autoFindWheels = true;
    public string[] keywords = { "wheel", "tire", "tyre", "ban", "roda" };

    [Header("Pengaturan Visual Ban")]
    public float wheelRadius = 0f;       
    public float wheelBase = 2.6f;       
    public float maxSteerAngle = 30f;
    public float steerSpeed = 200f;
    public bool invertSpin = false;      
    public bool invertSteer = false;     

    [Header("Pengaturan Kendali Mobil (WASD)")]
    public float motorForce = 15f;       
    public float maxSpeed = 15f;         
    public float turnSpeed = 60f;        

    // Variabel pengunci: hanya bisa digerakkan jika Player masuk
    [HideInInspector] public bool isPlayerInside = false; 

    class W { public Transform t; public Vector3 localCenter; public bool front; public float radius; }
    readonly List<W> wheels = new List<W>();
    
    Vector3 lastPos;
    float lastYaw, visualSteer;
    
    Rigidbody rb;
    float currentSpeed = 0f;
    float actualSteerAngle = 0f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("CarWheelVisuals: Tambahkan komponen Rigidbody di mobil ini!");
        }

        if (autoFindWheels && (frontWheels == null || frontWheels.Length == 0) && (rearWheels == null || rearWheels.Length == 0))
            AutoFind();
        else
        {
            if (frontWheels != null) foreach (var t in frontWheels) Add(t, true);
            if (rearWheels != null) foreach (var t in rearWheels) Add(t, false);
        }

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

        // JIKA PLAYER TIDAK DI DALAM MOBIL, REM MOBIL DAN JANGAN BACA INPUT
        if (!isPlayerInside)
        {
            currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, motorForce * Time.fixedDeltaTime);
            // Hentikan laju mobil secara perlahan (hanya biarkan gravitasi Y)
            Vector3 localVel = transform.InverseTransformDirection(rb.linearVelocity);
            localVel.x = Mathf.MoveTowards(localVel.x, 0f, Time.fixedDeltaTime * 10f);
            localVel.z = Mathf.MoveTowards(localVel.z, 0f, Time.fixedDeltaTime * 10f);
            rb.linearVelocity = transform.TransformDirection(localVel);
            
            rb.angularVelocity = Vector3.zero;
            return; // Hentikan eksekusi kode di bawahnya
        }

        // --- KODE DI BAWAH INI HANYA JALAN JIKA PLAYER DI DALAM MOBIL ---

        float throttle = 0f;
        float steerInput = 0f;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed) throttle = 1f;
            if (Keyboard.current.sKey.isPressed) throttle = -1f;

            if (Keyboard.current.dKey.isPressed) steerInput = 1f;
            if (Keyboard.current.aKey.isPressed) steerInput = -1f;
        }

        currentSpeed = Mathf.MoveTowards(currentSpeed, throttle * maxSpeed, motorForce * Time.fixedDeltaTime);
        
        Vector3 moveDirection = transform.forward * currentSpeed;
        rb.linearVelocity = new Vector3(moveDirection.x, rb.linearVelocity.y, moveDirection.z);

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

        Vector3 delta = transform.position - lastPos;
        float speed = Vector3.Dot(delta, transform.forward) / dt;
        float yaw = transform.eulerAngles.y;
        float yawRate = Mathf.DeltaAngle(lastYaw, yaw) / dt;
        
        lastPos = transform.position;
        lastYaw = yaw;
        
        if (Mathf.Abs(speed) > 80f) return;

        float target = 0f;
        if (Mathf.Abs(speed) > 0.5f)
            target = Mathf.Atan(wheelBase * yawRate * Mathf.Deg2Rad / speed) * Mathf.Rad2Deg;
        
        target = Mathf.Clamp(target, -maxSteerAngle, maxSteerAngle) * (invertSteer ? -1f : 1f);

        float prev = visualSteer;
        visualSteer = Mathf.MoveTowards(visualSteer, target, steerSpeed * dt);
        float dSteer = visualSteer - prev;

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
