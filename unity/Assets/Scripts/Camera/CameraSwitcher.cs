using UnityEngine;

public class CameraSwitcher : MonoBehaviour
{
    public Camera camera1;
    public Camera camera2;

    /// <summary>
    /// Intercambia entre las dos cámaras activando una y desactivando la otra.
    /// </summary>
    public void IntercambiarCamara()
    {
        bool cam1Activa = camera1.enabled;

        camera1.enabled = !cam1Activa;
        camera2.enabled = cam1Activa;
    }

    /// <summary>
    /// Activa únicamente la cámara 1 y desactiva la cámara 2.
    /// </summary>
    public void ActivarCamara1()
    {
        if (camera1 != null) camera1.enabled = true;
        if (camera2 != null) camera2.enabled = false;
    }

    /// <summary>
    /// Activa únicamente la cámara 2 y desactiva la cámara 1.
    /// </summary>
    public void ActivarCamara2()
    {
        if (camera1 != null) camera1.enabled = false;
        if (camera2 != null) camera2.enabled = true;
    }
}
