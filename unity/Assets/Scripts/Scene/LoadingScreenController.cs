using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UI;

public class LoadingScreenController : MonoBehaviour
{

    public static int sceneIndex;
    public Image barraProgreso; // Referencia a la barra de progreso en la interfaz de usuario
    public List<GameObject> rootGameObjects;
    public List<string> excludedObjectNames; // Lista de nombres de objetos excluidos
    public float activationTimePerObject = 2f; // Tiempo en segundos por cada activación de objeto

    private float totalObjects;
    private float activeObjects = 1;
    // private bool activationInProgress; // Unused
    private string sceneToLoad;
    AsyncOperation asyncLoad;

    void Start()
    {
        // Determinar qué escena cargar según la variable global
        switch (sceneIndex)
        {
            case 0:
                sceneToLoad = "Mode_Data";
                break;
            case 1:
                sceneToLoad = "Mode_Model";
                break;
            case 2:
                sceneToLoad = "Mode_Capture";
                break;
            default:
                sceneToLoad = "Mode_Data"; // Escena por defecto
                Debug.LogWarning($"sceneIndex inválido: {sceneIndex}. Cargando escena por defecto.");
                break;
        }
        
        Debug.Log($"Cargando escena: {sceneToLoad} (sceneIndex: {sceneIndex})");
        
        // Cargar la escena especificada aditivamente
        asyncLoad = SceneManager.LoadSceneAsync(sceneToLoad, LoadSceneMode.Additive);

        // Registrar el evento completed para obtener la referencia a la escena cargada
        asyncLoad.completed += OnSceneLoaded;
    }

    // Conjunto de objetos gestionados por SceneInitializer que no deben activarse
    private HashSet<GameObject> managedObjects = new HashSet<GameObject>();

    private void OnSceneLoaded(AsyncOperation operation)
    {
        // Se llama cuando la escena ha terminado de cargarse aditivamente
        Scene sceneCargada = SceneManager.GetSceneByName(sceneToLoad);
        rootGameObjects = sceneCargada.GetRootGameObjects().Where(obj => !excludedObjectNames.Contains(obj.name)).ToList();
        
        // Detectar si hay un SceneInitializer y recolectar sus objetos para NO activarlos manualmente
        managedObjects.Clear();

        // Limpiar EventSystems duplicados para evitar spam de errores y memory leak por logs
        var eventSystems = Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None);
        if (eventSystems.Length > 1)
        {
            Debug.Log($"Detectados {eventSystems.Length} EventSystems. Eliminando duplicados...");
            // Conservar el primero, destruir el resto
            for (int i = 1; i < eventSystems.Length; i++)
            {
                if (eventSystems[i].gameObject.scene == sceneCargada) // Preferiblemente destruir el de la escena cargada
                {
                    Destroy(eventSystems[i].gameObject);
                }
                else
                {
                     Destroy(eventSystems[i].gameObject);
                }
            }
        }

        foreach (var rootObj in rootGameObjects)
        {
            var initializer = rootObj.GetComponent<SceneInitializer>();
            if (initializer != null)
            {
                // El Initializer mismo debe estar activo para correr
                // Pero sus dependencias NO deben ser tocadas por este LoadingScreen
                CollectManagedObjects(initializer);
            }
        }

        totalObjects = CountParentObjects(rootGameObjects);
        activeObjects = 1;
        // activationInProgress = true;

        // Comenzar la activación progresiva estándar
        StartCoroutine(ActivateObjectsProgressively());
    }

    private void CollectManagedObjects(SceneInitializer init)
    {
        if (init.calleGenerator) managedObjects.Add(init.calleGenerator.gameObject);
        if (init.bachesGenerator) managedObjects.Add(init.bachesGenerator.gameObject);

        if (init.navMeshDrone) managedObjects.Add(init.navMeshDrone.gameObject);
        if (init.gameLogicToggle) 
        {
            managedObjects.Add(init.gameLogicToggle.gameObject);
            if (init.gameLogicToggle.groupA != null)
                foreach(var g in init.gameLogicToggle.groupA) if(g) managedObjects.Add(g);
            if (init.gameLogicToggle.groupB != null)
                foreach(var g in init.gameLogicToggle.groupB) if(g) managedObjects.Add(g);
            if (init.gameLogicToggle.exceptions != null)
                foreach(var g in init.gameLogicToggle.exceptions) if(g) managedObjects.Add(g);
        }
    }

    private System.Collections.IEnumerator ActivateObjectsProgressively()
    {
        foreach (GameObject obj in rootGameObjects)
        {
            if (obj == null) continue;

            // Si es el SceneInitializer, asegurarse que esté activo
            if (obj.GetComponent<SceneInitializer>() != null)
            {
                obj.SetActive(true);
            }
            
            ActivateObjectAndChildren(obj);

            // Calcular el porcentaje de objetos activados con respecto al total de objetos
            float percentage = activeObjects / totalObjects;

            // Mapear el porcentaje a un valor entre 0 y 1
            float mappedValue = Mathf.Clamp01(percentage);

            // Establecer el valor mapeado en la propiedad fillAmount de la imagen barraProgreso
            barraProgreso.fillAmount = mappedValue;

            // Esperar el tiempo especificado antes de activar el siguiente objeto
            yield return new WaitForSeconds(activationTimePerObject);
            activeObjects++;
        }

        // --- FASE 2: Iniciar SceneInitializer (Generadores -> NavMesh -> Lógica) ---
        // Buscar el initializer si fue detectado previamente
        SceneInitializer initializer = null;
        foreach (var obj in rootGameObjects)
        {
            if (obj == null) continue;
            if (managedObjects.Contains(obj)) continue; 
            
            initializer = obj.GetComponent<SceneInitializer>();
            if (initializer != null) break;
        }

        if (initializer != null)
        {
            Debug.Log("[LoadingScreen] Iniciando SceneInitializer...");
            // Asegurar que el objeto esté activo
            initializer.gameObject.SetActive(true);
            
            // Llamar al método de inicio
            initializer.BeginInitialization();

            // Esperar a que termine
            while (!initializer.IsInitializeComplete)
            {
                yield return null;
            }
            Debug.Log("[LoadingScreen] SceneInitializer completado.");
        }

        barraProgreso.fillAmount = 1.0f;
        yield return new WaitForSeconds(0.5f);

        // activationInProgress = false; 
        AsyncOperation asyncUnload = SceneManager.UnloadSceneAsync("Mode_Load");
    }

    private void ActivateObjectAndChildren(GameObject parent)
    {
        if (parent == null) return;
        // Si el objeto está en la lista de exclusion del SceneInitializer, NO tocarlo
        if (managedObjects.Contains(parent)) return;

        if (parent != null && !parent.activeSelf && !excludedObjectNames.Contains(parent.name))
        {
            parent.SetActive(true);

            // Activar los hijos recursivamente
            int childCount = parent.transform.childCount;
            for (int i = 0; i < childCount; i++)
            {
                GameObject child = parent.transform.GetChild(i).gameObject;
                ActivateObjectAndChildren(child);
            }
        }
    }

    private int CountParentObjects(List<GameObject> objects)
    {
        return objects.Count;
    }
}

// Clase global para manejar la selección de escena
    
