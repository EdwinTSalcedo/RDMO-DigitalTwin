using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
public class loadscence : MonoBehaviour
{
    public string mainSceneName; // Nombre de la escena principal que deseas cargar

    // Llamado cuando se inicia la escena de carga
    void Start()
    {
        StartCoroutine(LoadMainSceneAsync());
    }

    // Corrutina para cargar la escena principal de forma asíncrona
    private IEnumerator LoadMainSceneAsync()
    {
        // Inicia la carga de la escena principal
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(mainSceneName);

        // Mientras la carga no esté completada, actualiza el progreso en la interfaz de usuario
        while (!asyncLoad.isDone)
        {
            float progress = Mathf.Clamp01(asyncLoad.progress / 0.9f); // Divide entre 0.9f para asegurar que llegue al 100%
            // Aquí actualizas tu barra de progreso o texto con el valor 'progress'

            yield return null; // Permite que el motor de Unity continúe su ciclo para actualizar la interfaz de usuario.
        }
    }
}
