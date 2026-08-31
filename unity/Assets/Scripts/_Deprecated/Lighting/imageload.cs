
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class imageload : MonoBehaviour
{
    public Image imagenCarga;
    public float tiempoDeCarga = 100f; // Tiempo de carga deseado en segundos

    // Start is called before the first frame update
    void Start()
    {
        StartCoroutine(CargarEscenaPrincipalAsync());
    }

    // Update is called once per frame
    void Update()
    {
        // No es necesario hacer nada en el Update porque el progreso se maneja en la coroutine
    }

    IEnumerator CargarEscenaPrincipalAsync()
    {
        // Cargar la escena principal de forma asíncrona pero sin activarla
        AsyncOperation asyncLoadPrincipal = SceneManager.LoadSceneAsync("Demo_Scene_Low");
        asyncLoadPrincipal.allowSceneActivation = false;

        float tiempoInicio = Time.time;
        float progreso = 0f;

        // Esperar mientras la escena principal se está cargando y la carga no haya llegado al 100%
        while (!asyncLoadPrincipal.isDone)
        {
            // Calcular el progreso basado en el tiempo transcurrido
            float tiempoTranscurrido = Time.time - tiempoInicio;
            progreso = tiempoTranscurrido / tiempoDeCarga;
            progreso = Mathf.Clamp01(progreso);

            // Actualizar la barra de carga con el progreso
            imagenCarga.fillAmount = progreso; 

            // Si el progreso llega al 100%, permitir que se active la escena principal
            if (progreso >= 1f)
            {
                progreso=1f;
                asyncLoadPrincipal.allowSceneActivation = true;
            }

            yield return null;
        }
    }
}