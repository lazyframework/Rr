using UnityEngine;

// Dipasang otomatis oleh VehicleSystem pada PlayerArmature. Tidak perlu diisi manual.
[RequireComponent(typeof(Animator))]
public class RiderIK : MonoBehaviour
{
    public BikeDrive bike;
    public Transform grips;          // cadangan, hanya dipakai kalau BikeDrive tidak menemukan mesh Grips
    public float handWidth = 0.28f;
    [Range(0, 1)] public float weight = 1f;

    Animator anim;
    void Awake() { anim = GetComponent<Animator>(); }

    void OnAnimatorIK(int layer)
    {
        if (bike == null || transform.parent != bike.transform) return;

        Foot(AvatarIKGoal.LeftFoot,  AvatarIKHint.LeftKnee,  -1);
        Foot(AvatarIKGoal.RightFoot, AvatarIKHint.RightKnee, +1);

        if (bike.HasGrips)
        {
            Hand(AvatarIKGoal.LeftHand,  bike.HandPos(-1));
            Hand(AvatarIKGoal.RightHand, bike.HandPos(+1));
        }
        else if (grips != null)
        {
            Hand(AvatarIKGoal.LeftHand,  grips.position - bike.transform.right * handWidth);
            Hand(AvatarIKGoal.RightHand, grips.position + bike.transform.right * handWidth);
        }
    }

    void Foot(AvatarIKGoal g, AvatarIKHint h, int side)
    {
        anim.SetIKPositionWeight(g, weight);
        anim.SetIKRotationWeight(g, weight);
        anim.SetIKPosition(g, bike.FootPos(side));
        anim.SetIKRotation(g, bike.transform.rotation);   // telapak datar
        anim.SetIKHintPositionWeight(h, weight);
        anim.SetIKHintPosition(h, bike.KneePos(side));
    }

    void Hand(AvatarIKGoal g, Vector3 pos)
    {
        anim.SetIKPositionWeight(g, weight);
        anim.SetIKPosition(g, pos);
    }

    void OnDisable()
    {
        if (anim == null) return;
        foreach (AvatarIKGoal g in System.Enum.GetValues(typeof(AvatarIKGoal)))
        { anim.SetIKPositionWeight(g, 0); anim.SetIKRotationWeight(g, 0); }
        foreach (AvatarIKHint h in System.Enum.GetValues(typeof(AvatarIKHint)))
            anim.SetIKHintPositionWeight(h, 0);
    }
}
