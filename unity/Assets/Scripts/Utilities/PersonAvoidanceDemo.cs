using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ejemplo: Múltiples personas en una área, evitando colisiones entre sí
/// </summary>
public class PersonAvoidanceDemo : MonoBehaviour
{
    [Header("Configuración")]
    [SerializeField] private int numberOfPeople = 4;
    [SerializeField] private Vector3 areaCenter = Vector3.zero;
    [SerializeField] private Vector3 areaSize = new Vector3(20, 2, 20);
    [SerializeField] private float moveSpeed = 1.5f;

    [Header("Evitación")]
    [SerializeField] private float detectionRadius = 2.5f;
    [SerializeField] private float pauseDistance = 0.8f;
    [SerializeField] private bool enableAvoidance = true;

    private List<PersonController> people = new List<PersonController>();

    private void Start()
    {
        if (numberOfPeople > 0)
        {
            CreatePeopleWithAvoidance();
        }
    }

    [ContextMenu("Crear Personas con Evitación")]
    public void CreatePeopleWithAvoidance()
    {
        // Limpiar anteriores
        foreach (var person in people)
        {
            if (person != null)
                DestroyImmediate(person.gameObject);
        }
        people.Clear();

        Debug.Log($"Creating {numberOfPeople} people with avoidance in area {areaSize}");

        // Crear personas distribuidas en el área
        for (int i = 0; i < numberOfPeople; i++)
        {
            // Posición aleatoria dentro del área
            Vector3 randomOffset = new Vector3(
                Random.Range(-areaSize.x * 0.4f, areaSize.x * 0.4f),
                0,
                Random.Range(-areaSize.z * 0.4f, areaSize.z * 0.4f)
            );
            Vector3 spawnPos = areaCenter + randomOffset;

            // Crear GameObject
            GameObject personGO = new GameObject($"Person_{i}");
            personGO.transform.position = spawnPos;

            // Agregar PersonController
            PersonController controller = personGO.AddComponent<PersonController>();

            // Generar ruta dentro del área
            List<Vector3> route = GenerateRandomRoute(spawnPos, 5);
            controller.SetWaypoints(route);

            // Configurar evitación
            controller.SetInfluenceArea(areaCenter, areaSize);
            controller.SetAvoidanceEnabled(enableAvoidance);

            people.Add(controller);
            Debug.Log($"Person {i} created at {spawnPos}");
        }
    }

    /// <summary>
    /// Generar una ruta aleatoria dentro del área
    /// </summary>
    private List<Vector3> GenerateRandomRoute(Vector3 startPos, int waypointCount)
    {
        List<Vector3> route = new List<Vector3> { startPos };

        for (int i = 0; i < waypointCount; i++)
        {
            Vector3 randomOffset = new Vector3(
                Random.Range(-areaSize.x * 0.35f, areaSize.x * 0.35f),
                0,
                Random.Range(-areaSize.z * 0.35f, areaSize.z * 0.35f)
            );
            Vector3 waypoint = areaCenter + randomOffset;
            route.Add(waypoint);
        }

        // Retornar al inicio
        route.Add(startPos);

        return route;
    }

    [ContextMenu("Mostrar Estadísticas")]
    public void PrintStatistics()
    {
        Debug.Log("=== Estadísticas de Personas ===");
        int avoidingCount = 0;
        
        for (int i = 0; i < people.Count; i++)
        {
            if (people[i] != null)
            {
                bool isAvoiding = people[i].IsAvoiding();
                if (isAvoiding) avoidingCount++;
                
                float distance = people[i].GetDistanceToCurrentWaypoint();
                int wpIndex = people[i].GetCurrentWaypointIndex();
                
                string status = isAvoiding ? "[EVITANDO]" : "[NORMAL]";
                Debug.Log($"Person {i} {status}: WP {wpIndex}, Dist: {distance:F2}m");
            }
        }

        Debug.Log($"Total evitando: {avoidingCount}/{people.Count}");
    }

    [ContextMenu("Pausar Todos")]
    public void PauseAll()
    {
        foreach (var person in people)
        {
            if (person != null)
                person.SetMoving(false);
        }
    }

    [ContextMenu("Reanudar Todos")]
    public void ResumeAll()
    {
        foreach (var person in people)
        {
            if (person != null)
                person.SetMoving(true);
        }
    }

    [ContextMenu("Cambiar Área")]
    public void ChangeArea()
    {
        Vector3 newCenter = areaCenter + new Vector3(Random.Range(-5f, 5f), 0, Random.Range(-5f, 5f));
        areaCenter = newCenter;

        foreach (var person in people)
        {
            if (person != null)
                person.SetInfluenceArea(areaCenter, areaSize);
        }

        Debug.Log($"Área movida a {areaCenter}");
    }

    /// <summary>
    /// Hacer que una persona específica huya
    /// </summary>
    public void MakePersonFlee(int index, Vector3 fleeTarget)
    {
        if (index >= 0 && index < people.Count && people[index] != null)
        {
            // Establecer ruta de huida
            Vector3 escapeDirection = (fleeTarget - people[index].transform.position).normalized;
            Vector3 escapePoint = people[index].transform.position - escapeDirection * 5f;
            
            people[index].SetWaypoints(new List<Vector3> { escapePoint });
            Debug.Log($"Person {index} huye hacia {escapePoint}");
        }
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 300, 200));
        
        GUILayout.Label("== Persona Avoidance Demo ==", new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold });
        
        if (GUILayout.Button("Crear Personas", GUILayout.Height(30)))
        {
            CreatePeopleWithAvoidance();
        }

        if (GUILayout.Button("Mostrar Estadísticas", GUILayout.Height(30)))
        {
            PrintStatistics();
        }

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Pausar", GUILayout.Height(25)))
            PauseAll();
        if (GUILayout.Button("Reanudar", GUILayout.Height(25)))
            ResumeAll();
        GUILayout.EndHorizontal();

        if (GUILayout.Button("Cambiar Área", GUILayout.Height(30)))
        {
            ChangeArea();
        }

        GUILayout.Label($"Personas: {people.Count}");
        GUILayout.Label($"Detección: {detectionRadius}m");
        GUILayout.Label($"Pausa: {pauseDistance}m");

        GUILayout.EndArea();
    }
}
