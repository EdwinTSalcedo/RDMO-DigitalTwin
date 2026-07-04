using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public enum TrafficDensity
{
    Off = 0,
    Low = 1,
    Medium = 2,
    High = 3
}

public class TrafficDensityController : MonoBehaviour
{
    [Header("UI Configuration")]
    [Tooltip("El botón que ciclará la densidad de tráfico. Asignar en el Inspector.")]
    public Button densityButton;
    [Tooltip("El texto (TMP) del botón. Si es nulo, el script lo buscará en los hijos del botón automáticamente.")]
    public TMP_Text densityButtonText;

    [Header("Estado Actual")]
    [Tooltip("La densidad actual de agentes dinámicos.")]
    public TrafficDensity currentDensity = TrafficDensity.Off;

    [Header("Límites de Densidad")]
    public int lowLimit = 7;
    public int mediumLimit = 14;

    private struct InitialState
    {
        public GameObject obj;
        public Vector3 position;
        public Quaternion rotation;
    }

    private List<InitialState> carStates = new List<InitialState>();
    private List<InitialState> personStates = new List<InitialState>();
    
    private SceneInitializer sceneInitializer;

    private void Start()
    {
        // Capturamos el estado inicial esperando a que SceneInitializer complete
        StartCoroutine(WaitForSceneAndSetup());
    }
    
    private IEnumerator WaitForSceneAndSetup()
    {
        // Buscar SceneInitializer
        sceneInitializer = Object.FindFirstObjectByType<SceneInitializer>();
        
        if (sceneInitializer != null)
        {
            // Esperar a que SceneInitializer complete
            while (!sceneInitializer.IsInitializeComplete)
            {
                yield return null;
            }
            // Pequeño margen de seguridad
            yield return new WaitForSeconds(0.5f);
        }
        else
        {
            // Si no hay SceneInitializer, esperar un tiempo prudencial
            yield return new WaitForSeconds(1f);
        }
        
        InitialSetup();
    }

    private void InitialSetup()
    {
        CaptureInitialStates();
        UpdateUIButton();
        ApplyDensity();
    }

    /// <summary>
    /// Encuentra todos los objetos con tags 'Car' y 'Person' y guarda su estado inicial.
    /// </summary>
    [ContextMenu("Recapture Objects")]
    public void CaptureInitialStates()
    {
        carStates.Clear();
        personStates.Clear();

        // Buscamos todos los GameObjects (incluyendo desactivados)
        GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        
        Debug.Log($"[TrafficDensity] Buscando en {allObjects.Length} GameObjects en la escena...");

        foreach (var obj in allObjects)
        {
            // Verificamos el tag (comparación insensible a mayúsculas para evitar errores comunes)
            string tag = obj.tag;
            
            if (tag.Equals("Car", System.StringComparison.OrdinalIgnoreCase))
            {
                carStates.Add(new InitialState { 
                    obj = obj, 
                    position = obj.transform.position, 
                    rotation = obj.transform.rotation 
                });
            }
            else if (tag.Equals("Person", System.StringComparison.OrdinalIgnoreCase))
            {
                personStates.Add(new InitialState { 
                    obj = obj, 
                    position = obj.transform.position, 
                    rotation = obj.transform.rotation 
                });
            }
        }

        Debug.Log($"[TrafficDensity] Captura completada: {carStates.Count} autos y {personStates.Count} personas encontrados.");
        
        if (carStates.Count == 0 && personStates.Count == 0)
        {
            Debug.LogWarning("[TrafficDensity] ¡ADVERTENCIA! No se encontró ningún objeto con los tags 'Car' o 'Person'. Por favor, verifica que los objetos en la escena tengan estos tags asignados (mayúsculas o minúsculas).");
        }
    }

    /// <summary>
    /// Cicla la densidad de tráfico en el orden: Off -> Low -> Medium -> High -> Off.
    /// </summary>
    public void CycleDensityMode()
    {
        int next = ((int)currentDensity + 1) % 4;
        currentDensity = (TrafficDensity)next;
        
        Debug.Log($"[TrafficDensity] La densidad ha cambiado a: {currentDensity}");
        UpdateUIButton();
        ApplyDensity();
    }

    /// <summary>
    /// Resetea la posición de todos los agentes y activa solo la cantidad correspondiente a la densidad.
    /// </summary>
    private void ApplyDensity()
    {
        // Si no se capturaron objetos al inicio (ej. por generación tardía), intentar capturarlos ahora
        if (carStates.Count == 0 && personStates.Count == 0)
        {
            Debug.Log("[TrafficDensity] Listas vacías, reintentando captura...");
            CaptureInitialStates();
        }

        int limit = GetLimitForDensity(currentDensity);
        Debug.Log($"[TrafficDensity] Aplicando densidad {currentDensity} (Límite: {limit}) a {carStates.Count} autos y {personStates.Count} personas.");

        ApplyToGroup(carStates, limit);
        ApplyToGroup(personStates, limit);
    }

    private int GetLimitForDensity(TrafficDensity density)
    {
        switch (density)
        {
            case TrafficDensity.Off: return 0;
            case TrafficDensity.Low: return lowLimit;
            case TrafficDensity.Medium: return mediumLimit;
            case TrafficDensity.High: return int.MaxValue;
            default: return 0;
        }
    }

    private void ApplyToGroup(List<InitialState> states, int limit)
    {
        for (int i = 0; i < states.Count; i++)
        {
            var state = states[i];
            if (state.obj == null) continue;

            // Resetear posición y rotación
            state.obj.transform.position = state.position;
            state.obj.transform.rotation = state.rotation;

            // Activar o desactivar según el límite
            bool shouldBeActive = i < limit;
            state.obj.SetActive(shouldBeActive);
            
            // Si el objeto tiene un NavMeshAgent o Rigidbody, es posible que necesite resetear velocidades
            UnityEngine.AI.NavMeshAgent agent = state.obj.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null && agent.enabled)
            {
                agent.velocity = Vector3.zero;
                agent.ResetPath();
            }

            Rigidbody rb = state.obj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
    }

    private void UpdateUIButton()
    {
        if (densityButton == null) return;
        TMP_Text label = densityButtonText != null ? densityButtonText : densityButton.GetComponentInChildren<TMP_Text>();
        if (label != null)
        {
            string densityText = currentDensity == TrafficDensity.Off ? "OFF" : currentDensity.ToString();
            label.text = "Traffic:" + densityText;
        }
    }
}

