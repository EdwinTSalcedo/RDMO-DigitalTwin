using UnityEngine;

public class CameraUI : MonoBehaviour
{
    [Tooltip("Agrega todas tus cámaras aquí. La primera (Element 0) será la activa al iniciar.")]
    public Camera[] cameras;
    
    private int currentIndex = 0; 

    void Start()
    {
        // Forzar estrictamente la primera cámara (índice 0) al iniciar
        currentIndex = 0;
        AplicarCamaraActual();
    }

    public void ToggleSingleCameraMode() 
    {
        if (cameras == null || cameras.Length == 0) return;

        currentIndex++;
        if (currentIndex >= cameras.Length)
        {
            currentIndex = 0;
        }

        AplicarCamaraActual();
    }

    private void AplicarCamaraActual()
    {
        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] != null)
            {
                cameras[i].rect = new Rect(0, 0, 1, 1);

                if (i == currentIndex)
                {
                    // Forzar activación absoluta
                    cameras[i].gameObject.SetActive(true);
                    cameras[i].enabled = true;
                    cameras[i].depth = 50; // Asegurarse de que su renderizado tape todo
                }
                else
                {
                    // Apagar las demás
                    cameras[i].enabled = false;
                    cameras[i].depth = -10; 
                }
            }
        }
    }
}
