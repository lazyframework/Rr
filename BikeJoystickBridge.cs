using UnityEngine;

// Menghubungkan Joystick Pack (Fixed/Floating/Dynamic/Variable Joystick) ke sepeda.
// Pasang di objek mana saja (misalnya Canvas), lalu isi:
//   Joystick = objek joystick di Canvas (Fixed Joystick dll.)
//   Bike     = BMX_Bike
// Butuh Joystick Pack sudah di-import (class "Joystick").
public class BikeJoystickBridge : MonoBehaviour
{
    public Joystick joystick;
    public BikeController bike;

    void Update()
    {
        if (bike == null || joystick == null) return;
        if (!bike.enabled) return;          // hanya saat pemain sedang naik sepeda
        bike.SetMobileMove(joystick.Vertical, joystick.Horizontal);
    }
}
