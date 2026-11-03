using UnityEngine;
using StarterAssets;
using UnityEngine.InputSystem;

public class VehicleSystem : MonoBehaviour
{
    [Header("Referensi Kendaraan")]
    public GameObject player;
    public GameObject car;
    public Transform carSeatPosition;

    public GameObject bike;
    [Tooltip("Titik PINGGUL pengendara di sadel. Kalau kosong pakai Bike Hip Local Pos")]
    public Transform bikeSeatPosition;
    public Vector3 bikeHipLocalPos = new Vector3(0f, 0.85f, -0.15f);
    [Tooltip("Objek Grips (stang). Kosong = dicari otomatis di dalam bike")]
    public Transform bikeGrips;

    [Header("Referensi Kamera")]
    public GameObject playerCamera;
    public GameObject carCamera;
    public GameObject bikeCamera;

    [Header("Referensi UI Mobile")]
    public GameObject btnInteraksi;

    bool isInVehicle = false;
    bool isInBike = false;

    ThirdPersonController playerController;
    CharacterController charController;
    PlayerInput playerInput;
    Animator playerAnim;
    RiderIK riderIK;

    BikeController bikeController;
    BikeDrive bikeDrive;
    Rigidbody bikeRb;

    void Start()
    {
        playerController = player.GetComponent<ThirdPersonController>();
        charController = player.GetComponent<CharacterController>();
        playerInput = player.GetComponent<PlayerInput>();
        playerAnim = player.GetComponent<Animator>();

        bikeController = bike.GetComponent<BikeController>();
        bikeDrive = bike.GetComponent<BikeDrive>();
        bikeRb = bike.GetComponent<Rigidbody>();
        if (bikeDrive != null && bikeDrive.rb == null) bikeDrive.rb = bikeRb;

        if (bikeGrips == null)
        {
            foreach (var t in bike.GetComponentsInChildren<Transform>(true))
                if (t.name == "Grips") { bikeGrips = t; break; }
        }

        // RiderIK dibuat otomatis, mati sampai pemain naik sepeda
        riderIK = player.GetComponent<RiderIK>();
        if (riderIK == null) riderIK = player.AddComponent<RiderIK>();
        riderIK.bike = bikeDrive;
        riderIK.grips = bikeGrips;
        riderIK.enabled = false;

        var carCtrl = car.GetComponent<CarJoystickController>();
        if (carCtrl != null) carCtrl.enabled = false;
        if (bikeController != null) bikeController.enabled = false;

        carCamera.SetActive(false);
        if (bikeCamera != null) bikeCamera.SetActive(false);
        if (btnInteraksi != null) btnInteraksi.SetActive(false);
    }

    void Update()
    {
        if (isInVehicle)
        {
            if (btnInteraksi != null) btnInteraksi.SetActive(true);

            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                ExitVehicle();
            return;
        }

        float distToCar = Vector3.Distance(player.transform.position, car.transform.position);
        float distToBike = Vector3.Distance(player.transform.position, bike.transform.position);

        if (btnInteraksi != null)
            btnInteraksi.SetActive(distToCar <= 4f || distToBike <= 3f);

        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            if (distToCar <= 4f) EnterCar();
            else if (distToBike <= 3f) EnterBike();
        }
    }

    // Dipanggil oleh Tombol UI saat di HP
    public void TombolAksiDitekan()
    {
        if (isInVehicle)
        {
            ExitVehicle();
        }
        else
        {
            float distToCar = Vector3.Distance(player.transform.position, car.transform.position);
            float distToBike = Vector3.Distance(player.transform.position, bike.transform.position);

            if (distToCar <= 4f) EnterCar();
            else if (distToBike <= 3f) EnterBike();
        }
    }

    void DisablePlayerControl()
    {
        playerController.enabled = false;
        charController.enabled = false;
        if (playerInput != null) playerInput.enabled = false;
    }

    void EnablePlayerControl()
    {
        playerController.enabled = true;
        charController.enabled = true;
        if (playerInput != null) playerInput.enabled = true;
    }

    void EnterCar()
    {
        isInVehicle = true; isInBike = false;
        DisablePlayerControl();

        player.transform.SetParent(car.transform);
        player.transform.position = carSeatPosition.position;
        player.transform.rotation = carSeatPosition.rotation;

        car.GetComponent<CarJoystickController>().enabled = true;
        playerCamera.SetActive(false); carCamera.SetActive(true);
    }

    void EnterBike()
    {
        isInVehicle = true; isInBike = true;
        DisablePlayerControl();

        // Jadikan child sepeda, jaga posisi dan skala dunia
        player.transform.SetParent(bike.transform, true);
        player.transform.rotation = bike.transform.rotation;

        // Hentikan animasi lari supaya pose awal netral
        if (playerAnim != null)
        {
            playerAnim.SetFloat("Speed", 0f);
            playerAnim.SetFloat("MotionSpeed", 1f);
            playerAnim.SetBool("Grounded", true);
            playerAnim.SetBool("Jump", false);
            playerAnim.SetBool("FreeFall", false);
            playerAnim.Update(0f);
        }

        // Taruh PINGGUL karakter tepat di sadel
        Vector3 target = bikeSeatPosition != null
            ? bikeSeatPosition.position
            : bike.transform.TransformPoint(bikeHipLocalPos);

        Transform hips = playerAnim != null ? playerAnim.GetBoneTransform(HumanBodyBones.Hips) : null;
        if (hips != null) player.transform.position += target - hips.position;
        else player.transform.position = target - Vector3.up * 0.95f;

        if (riderIK != null) riderIK.enabled = true;       // kaki ke pedal, tangan ke stang
        if (bikeController != null) bikeController.enabled = true;

        playerCamera.SetActive(false);
        if (bikeCamera != null) bikeCamera.SetActive(true);
    }

    void ExitVehicle()
    {
        isInVehicle = false;
        player.transform.SetParent(null, true);

        if (isInBike)
        {
            if (riderIK != null) riderIK.enabled = false;
            if (bikeController != null) bikeController.enabled = false;
            if (bikeRb != null) { bikeRb.linearVelocity = Vector3.zero; bikeRb.angularVelocity = Vector3.zero; }

            player.transform.position = bike.transform.position + (bike.transform.right * -1.5f) + (Vector3.up * 1f);
            player.transform.rotation = Quaternion.Euler(0f, bike.transform.eulerAngles.y, 0f);
            if (bikeCamera != null) bikeCamera.SetActive(false);
        }
        else
        {
            player.transform.position = car.transform.position + (car.transform.right * -2f) + (Vector3.up * 1f);
            player.transform.rotation = Quaternion.Euler(0f, car.transform.eulerAngles.y, 0f);
            car.GetComponent<CarJoystickController>().enabled = false;
            carCamera.SetActive(false);
        }

        EnablePlayerControl();
        playerCamera.SetActive(true);
        if (btnInteraksi != null) btnInteraksi.SetActive(false);
    }

    void OnDrawGizmosSelected()
    {
        if (bike == null) return;
        Gizmos.color = Color.yellow;
        Vector3 pos = bikeSeatPosition != null ? bikeSeatPosition.position : bike.transform.TransformPoint(bikeHipLocalPos);
        Gizmos.DrawWireSphere(pos, 0.12f);
    }
}
