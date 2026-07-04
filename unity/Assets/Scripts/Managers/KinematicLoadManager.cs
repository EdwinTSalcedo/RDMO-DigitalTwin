using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Este script busca objetos con los tags "Car" y "Person" al inicio de la escena,
/// los pone en modo Kinematic para evitar que se caigan o se muevan por la física
/// durante la carga (mientras se generan baches o calles), y los libera cuando
/// la carga ha finalizado.
/// </summary>
public class KinematicLoadManager : MonoBehaviour
{
    [Header("Configuración de Tags")]
    public string[] targetTags = { "Car", "Person" };
    
    [Header("Referencias")]
    [Tooltip("Opcional: Si no se asigna, buscará automáticamente un SceneInitializer.")]
    public SceneInitializer sceneInitializer;
    
    [Header("Seguridad")]
    [Tooltip("Segundos adicionales de espera después de que SceneInitializer dice estar listo, para que la física se estabilice.")]
    public float safetyDelay = 2.5f;

    private struct AffectedObject
    {
        public GameObject gameObject;
        public Rigidbody rigidbody;
    }

    private List<AffectedObject> affectedObjects = new List<AffectedObject>();

    private void Start()
    {
        // Iniciamos el proceso
        StartCoroutine(ExecuteWorkflow());
    }

    private IEnumerator ExecuteWorkflow()
    {
        // 1. Encontrar y congelar objetos
        // Hacemos una pequeña espera para asegurar que los objetos base de la escena estén instanciados
        yield return new WaitForEndOfFrame();
        FindAndFreezeObjects();
        
        // --- NUEVO: Desactivar inmediatamente todos los objetos por defecto ---
        // Esto evita que aparezcan antes de que TrafficDensityController tenga oportunidad de aplicar OFF
        DeactivateAllTrackedObjects();

        // 2. Esperar a que la escena termine de cargar por completo
        // Intentamos detectar el SceneInitializer si no está asignado
        if (sceneInitializer == null)
        {
            sceneInitializer = Object.FindFirstObjectByType<SceneInitializer>();
        }

        if (sceneInitializer != null)
        {
            Debug.Log("[KinematicLoadManager] Esperando a que SceneInitializer finalice la generación...");
            while (!sceneInitializer.IsInitializeComplete)
            {
                yield return null;
            }
            
            // --- MEJORADO: Margen de seguridad más amplio para estabilización física ---
            Debug.Log($"[KinematicLoadManager] SceneInitializer completado. Esperando {safetyDelay}s para estabilización física...");
            yield return new WaitForSeconds(safetyDelay);
        }
        else
        {
            // Si no hay initializer, esperamos a que la escena "Mode_Load" (si existe) se descargue
            Debug.Log("[KinematicLoadManager] No se detectó SceneInitializer. Esperando descarga de escena de carga...");
            bool isLoading = true;
            while (isLoading)
            {
                isLoading = false;
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    if (SceneManager.GetSceneAt(i).name == "Mode_Load")
                    {
                        isLoading = true;
                        break;
                    }
                }
                if (isLoading) yield return new WaitForSeconds(0.1f);
            }
            
            // --- MEJORADO: También esperar en este caso ---
            Debug.Log($"[KinematicLoadManager] Escena de carga descargada. Esperando {safetyDelay}s adicionales...");
            yield return new WaitForSeconds(safetyDelay);
        }

        // 3. Desactivar el modo Kinematic (pero mantener objetos desactivados para TrafficDensityController)
        UnfreezeObjects();
    }

    private void FindAndFreezeObjects()
    {
        affectedObjects.Clear();
        
        // Buscamos todos los objetos en la escena, incluidos los desactivados
        GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        
        foreach (var obj in allObjects)
        {
            bool match = false;
            foreach (string tag in targetTags)
            {
                if (obj.CompareTag(tag))
                {
                    match = true;
                    break;
                }
            }

            if (match)
            {
                Rigidbody rb = obj.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = true;
                    AffectedObject ao = new AffectedObject { gameObject = obj, rigidbody = rb };
                    if (!affectedObjects.Contains(ao))
                    {
                        affectedObjects.Add(ao);
                    }
                }
            }
        }
        
        Debug.Log($"[KinematicLoadManager] {affectedObjects.Count} objetos puestos en modo Kinematic.");
    }

    private void UnfreezeObjects()
    {
        int count = 0;
        int preserved = 0;
        foreach (var ao in affectedObjects)
        {
            if (ao.gameObject == null || ao.rigidbody == null) continue;
            
            // --- NUEVO: NO desactivar objetos que tienen CarPatrol o RectangularPatrol ---
            CarPatrol carPatrol = ao.gameObject.GetComponent<CarPatrol>();
            RectangularPatrol rectPatrol = ao.gameObject.GetComponent<RectangularPatrol>();
            
            ao.rigidbody.isKinematic = false;
            
            if (carPatrol != null || rectPatrol != null)
            {
                // Estos vehículos ya están activos, solo descongelar física
                preserved++;
            }
            else
            {
                // Mantener desactivados para que TrafficDensityController los controle
                ao.gameObject.SetActive(false);
                count++;
            }
        }
        
        Debug.Log($"[KinematicLoadManager] Carga completada. Kinematic desactivado. Mantenidos desactivados: {count}. Vehículos patrulleros activos: {preserved}.");
    }

    private void DeactivateAllTrackedObjects()
    {
        int count = 0;
        int skipped = 0;
        foreach (var ao in affectedObjects)
        {
            if (ao.gameObject == null) continue;
            
            // --- NUEVO: NO desactivar objetos que tienen CarPatrol o RectangularPatrol ---
            // Estos necesitan estar activos para inicializarse
            CarPatrol carPatrol = ao.gameObject.GetComponent<CarPatrol>();
            RectangularPatrol rectPatrol = ao.gameObject.GetComponent<RectangularPatrol>();
            
            if (carPatrol != null || rectPatrol != null)
            {
                skipped++;
                continue;
            }
            
            ao.gameObject.SetActive(false);
            count++;
        }
        Debug.Log($"[KinematicLoadManager] Desactivados {count} objetos por defecto (Tráfico en OFF). Se preservaron {skipped} vehículos patrulleros.");
    }

    /// <summary>
    /// Método público por si se desea activar la liberación manualmente desde otro script.
    /// </summary>
    public void ManualRelease()
    {
        StopAllCoroutines();
        UnfreezeObjects();
    }
}
