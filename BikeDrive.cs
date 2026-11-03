using UnityEngine;
using System.Collections.Generic;

// Pasang di BMX_Bike. Memutar roda, crank, pedal, dan setir.
// Posisi kaki diambil dari mesh pedal yang SEBENARNYA, jadi kaki selalu menempel di pedal
// saat maju, mundur, dan belok kiri/kanan. Posisi tangan diambil dari mesh Grips.
public class BikeDrive : MonoBehaviour
{
    public Rigidbody rb;
    public Transform bb;                 // objek "BB"
    public Transform cranks, pedals;     // objek "Cranks" dan "Pedals"
    public Transform frontHub, rearHub;  // Front_hub dan Rear_Hub
    public Transform[] frontParts;       // Front_Rim, Front_Tire, Front_Spokes, Front_hub, Front_Valve
    public Transform[] rearParts;        // Rear_Rim, Rear_Tire, Rear_Spokes, Rear_Hub, Rear_Valve
    public Transform grips;              // kosong = dicari otomatis (objek bernama "Grips")

    [Header("Setir (opsional)")]
    public Transform steerPivot;         // "Headset" (kosong = dicari otomatis)
    public Transform[] steerParts;       // Fork, Handlebars, Grips, Barends, Cap, Headset, Front_Pegs, Front_Guards (TANPA bagian roda depan)
    public float maxSteerAngle = 30f;
    public float steerSpeed = 200f;
    public float headTubeTilt = 18f;     // kemiringan sumbu setir; kalau setir terasa miring aneh, coba 0

    [Header("Roda & Crank")]
    public float wheelRadius = 0f;       // 0 = dihitung otomatis dari roda depan
    public float gearRatio = 2.77f;
    public bool pedalFollowsReverse = true;   // pedal ikut berputar mundur saat sepeda mundur

    [Header("Kaki & Tangan")]
    public float footHeight = 0.04f;     // telapak sedikit di atas pedal
    public float handInset = 0.85f;      // posisi tangan di grip (1 = ujung)

    [Header("Cadangan (dipakai kalau mesh pedal tidak bisa dibaca)")]
    public float crankLength = 0.19f;
    public float pedalSide = 0.12f;
    public float angleOffset = 0f;

    BikeController controller;
    float crankAngle, steerAngle;
    Vector3 bbLocal, frontHubLocal, rearHubLocal, steerPivotLocal;
    bool hasBB, hasFrontHub, hasRearHub, hasSteer, hasPedalMesh, hasGrips;
    Vector3[] pedalLocal = new Vector3[2];
    Transform gripTf;
    Vector3 gripLocalL, gripLocalR;
    List<Transform> steered = new List<Transform>();

    public bool HasGrips => hasGrips;

    void Start()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        controller = GetComponent<BikeController>();
        if (grips == null) grips = FindChild("Grips");
        if (steerPivot == null) steerPivot = FindChild("Headset");

        hasBB = bb != null && LocalCenter(bb, out bbLocal);
        hasFrontHub = frontHub != null && LocalCenter(frontHub, out frontHubLocal);
        hasRearHub = rearHub != null && LocalCenter(rearHub, out rearHubLocal);

        if (wheelRadius <= 0f) wheelRadius = AutoRadius();
        CachePedals();
        CacheGrips();

        // Daftar bagian yang ikut setir (tanpa duplikat)
        var set = new HashSet<Transform>();
        if (steerParts != null) foreach (var p in steerParts) if (p) set.Add(p);
        if (frontParts != null) foreach (var p in frontParts) if (p) set.Add(p);
        steered.AddRange(set);
        hasSteer = steerPivot != null && steerParts != null && steerParts.Length > 0
                   && LocalCenter(steerPivot, out steerPivotLocal);
    }

    void Update()
    {
        if (rb == null) return;
        float dt = Time.deltaTime;
        float v = Vector3.Dot(rb.linearVelocity, transform.forward);
        float wheelDeg = v * dt / wheelRadius * Mathf.Rad2Deg;

        // 1. Setir
        if (hasSteer)
        {
            float input = controller != null ? controller.Steer : 0f;
            float prev = steerAngle;
            steerAngle = Mathf.MoveTowards(steerAngle, input * maxSteerAngle, steerSpeed * dt);
            float d = steerAngle - prev;
            if (Mathf.Abs(d) > 0.0001f)
            {
                Vector3 pivot = steerPivot.TransformPoint(steerPivotLocal);
                Vector3 axis = transform.TransformDirection(Quaternion.AngleAxis(-headTubeTilt, Vector3.right) * Vector3.up);
                foreach (var p in steered) p.RotateAround(pivot, axis, d);
            }
        }

        // 2. Roda depan (sumbu ikut berbelok) dan belakang
        if (frontHub != null && frontParts != null)
        {
            Vector3 c = hasFrontHub ? frontHub.TransformPoint(frontHubLocal) : frontHub.position;
            Vector3 axle = Quaternion.AngleAxis(steerAngle, transform.up) * transform.right;
            foreach (var p in frontParts) if (p) p.RotateAround(c, axle, wheelDeg);
        }
        if (rearHub != null && rearParts != null)
        {
            Vector3 c = hasRearHub ? rearHub.TransformPoint(rearHubLocal) : rearHub.position;
            foreach (var p in rearParts) if (p) p.RotateAround(c, transform.right, wheelDeg);
        }

        // 3. Crank dan pedal (searah roda, maju dan mundur)
        float crankDeg = wheelDeg / gearRatio;
        if (!pedalFollowsReverse && v < 0f) crankDeg = 0f;
        crankAngle += crankDeg;
        if (bb != null)
        {
            Vector3 bbPos = hasBB ? bb.TransformPoint(bbLocal) : bb.position;
            if (cranks) cranks.RotateAround(bbPos, transform.right, crankDeg);
            if (pedals) pedals.RotateAround(bbPos, transform.right, crankDeg);
        }
    }

    // ---------- Posisi untuk IK ----------
    // side = -1 kiri, +1 kanan
    public Vector3 FootPos(int side)
    {
        if (hasPedalMesh)
            return pedals.TransformPoint(pedalLocal[side < 0 ? 0 : 1]) + transform.up * footHeight;

        float a = (crankAngle + angleOffset + (side > 0 ? 180f : 0f)) * Mathf.Deg2Rad;
        Vector3 circle = transform.up * Mathf.Cos(a) + transform.forward * Mathf.Sin(a);
        Vector3 center = hasBB ? bb.TransformPoint(bbLocal) : bb.position;
        return center + circle * crankLength + transform.right * (side * pedalSide);
    }

    public Vector3 KneePos(int side)
    {
        return FootPos(side) + transform.forward * 0.4f + transform.up * 0.3f
               + transform.right * (side * 0.1f);
    }

    public Vector3 HandPos(int side)
    {
        return gripTf.TransformPoint(side < 0 ? gripLocalL : gripLocalR);
    }

    // ---------- Persiapan ----------
    void CachePedals()
    {
        if (pedals == null) return;
        var mf = pedals.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return;
        Mesh m = mf.sharedMesh;
        if (!m.isReadable)
        {
            Debug.LogWarning("BikeDrive: aktifkan Read/Write pada file model sepeda (Project > klik model > tab Model > Read/Write) supaya kaki menempel tepat di pedal. Sementara memakai posisi cadangan.");
            return;
        }

        Vector3[] verts = m.vertices;
        var used = new HashSet<int>(m.GetTriangles(0));   // submesh 0 = pedal (submesh 1 = rantai)
        Vector3 sumL = Vector3.zero, sumR = Vector3.zero; int nL = 0, nR = 0;
        foreach (int i in used)
        {
            float x = transform.InverseTransformPoint(pedals.TransformPoint(verts[i])).x;
            if (x < 0f) { sumL += verts[i]; nL++; } else { sumR += verts[i]; nR++; }
        }
        if (nL > 0 && nR > 0)
        {
            pedalLocal[0] = sumL / nL;
            pedalLocal[1] = sumR / nR;
            hasPedalMesh = true;
        }
    }

    void CacheGrips()
    {
        if (grips == null) return;
        var mf = grips.GetComponent<MeshFilter>();
        if (mf == null) mf = grips.GetComponentInChildren<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return;

        gripTf = mf.transform;
        Bounds mb = mf.sharedMesh.bounds;
        Vector3 n = gripTf.InverseTransformDirection(transform.right).normalized;
        float h = Mathf.Abs(n.x) * mb.extents.x + Mathf.Abs(n.y) * mb.extents.y + Mathf.Abs(n.z) * mb.extents.z;
        gripLocalL = mb.center - n * h * handInset;
        gripLocalR = mb.center + n * h * handInset;
        hasGrips = true;
    }

    float AutoRadius()
    {
        bool init = false; Bounds b = new Bounds();
        if (frontParts != null)
            foreach (var p in frontParts)
            {
                if (p == null) continue;
                var r = p.GetComponent<Renderer>();
                if (r == null) continue;
                if (!init) { b = r.bounds; init = true; } else b.Encapsulate(r.bounds);
            }
        if (!init) return 0.25f;
        float[] e = { b.extents.x, b.extents.y, b.extents.z };
        System.Array.Sort(e);
        return Mathf.Max(0.05f, (e[1] + e[2]) * 0.5f);   // dua sisi terbesar = jari-jari roda
    }

    bool LocalCenter(Transform t, out Vector3 c)
    {
        Renderer r = t.GetComponent<Renderer>();
        if (r == null) r = t.GetComponentInChildren<Renderer>();
        if (r == null) { c = Vector3.zero; return false; }
        c = t.InverseTransformPoint(r.bounds.center);
        return true;
    }

    Transform FindChild(string n)
    {
        foreach (var t in GetComponentsInChildren<Transform>(true))
            if (t.name == n) return t;
        return null;
    }

    void OnDrawGizmos()
    {
        if (!Application.isPlaying || bb == null) return;
        Gizmos.color = Color.red;   Gizmos.DrawSphere(FootPos(-1), 0.03f);
        Gizmos.color = Color.green; Gizmos.DrawSphere(FootPos(1), 0.03f);
        if (hasGrips)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(HandPos(-1), 0.03f); Gizmos.DrawSphere(HandPos(1), 0.03f);
        }
    }
}
