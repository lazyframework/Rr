using UnityEngine;
using System.Collections.Generic;

// Pasang di Car_16 (objek induk mobil). Memutar ban sesuai kecepatan dan membelokkan ban depan.
// Tidak bergantung pada script kendali mobil: kecepatan dan arah dibaca dari pergerakan mobil.
// Syarat: ban harus objek terpisah (child) dari body mobil.
public class CarWheelVisuals : MonoBehaviour
{
    [Header("Ban (kosongkan untuk dicari otomatis)")]
    public Transform[] frontWheels;      // berputar + belok
    public Transform[] rearWheels;       // berputar
    public bool autoFindWheels = true;
    public string[] keywords = { "wheel", "tire", "tyre", "ban", "roda" };

    [Header("Pengaturan")]
    public float wheelRadius = 0f;       // 0 = otomatis dari ukuran ban
    public float wheelBase = 2.6f;       // jarak as depan ke as belakang (m)
    public float maxSteerAngle = 30f;
    public float steerSpeed = 200f;
    public bool invertSpin = false;      // ban berputar mundur? centang
    public bool invertSteer = false;     // ban belok berlawanan? centang

    class W { public Transform t; public Vector3 localCenter; public bool front; public float radius; }
    readonly List<W> wheels = new List<W>();
    Vector3 lastPos;
    float lastYaw, steer;

    void Start()
    {
        if (autoFindWheels && (frontWheels == null || frontWheels.Length == 0) && (rearWheels == null || rearWheels.Length == 0))
            AutoFind();
        else
        {
            if (frontWheels != null) foreach (var t in frontWheels) Add(t, true);
            if (rearWheels != null) foreach (var t in rearWheels) Add(t, false);
        }

        if (wheels.Count == 0) Debug.LogWarning("CarWheelVisuals: tidak ada ban ditemukan. Isi slot Front/Rear Wheels, atau pastikan ban adalah objek terpisah di dalam mobil.");

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

            bool nested = false;                       // lewati kalau induknya sudah terpilih
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
        if (Mathf.Abs(speed) > 80f) return;            // mobil dipindah (teleport), abaikan frame ini

        // Sudut belok dari kecepatan putar mobil (model sepeda/mobil sederhana)
        float target = 0f;
        if (Mathf.Abs(speed) > 0.5f)
            target = Mathf.Atan(wheelBase * yawRate * Mathf.Deg2Rad / speed) * Mathf.Rad2Deg;
        target = Mathf.Clamp(target, -maxSteerAngle, maxSteerAngle) * (invertSteer ? -1f : 1f);

        float prev = steer;
        steer = Mathf.MoveTowards(steer, target, steerSpeed * dt);
        float dSteer = steer - prev;

        foreach (var w in wheels)
        {
            if (w.t == null) continue;
            Vector3 c = w.t.TransformPoint(w.localCenter);

            if (w.front && Mathf.Abs(dSteer) > 0.0001f)
                w.t.RotateAround(c, transform.up, dSteer);

            Vector3 axle = w.front ? Quaternion.AngleAxis(steer, transform.up) * transform.right : transform.right;
            float deg = speed * dt / w.radius * Mathf.Rad2Deg * (invertSpin ? -1f : 1f);
            w.t.RotateAround(c, axle, deg);
        }
    }
}
