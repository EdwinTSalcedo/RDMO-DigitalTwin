using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ejemplo de uso de PersonController
/// Demuestra cómo crear y configurar personas con rutas
/// </summary>
public class PersonControllerExample : MonoBehaviour
{
    [Header("Configuración")]
    [SerializeField] private int numberOfPeople = 3;
    [SerializeField] private Vector3 spawnPoint = Vector3.zero;
    [SerializeField] private float spacing = 2f;
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private bool autoCreatePeople = false;

    private List<PersonController> people = new List<PersonController>();

    [ContextMenu("Crear Personas")]
    public void CreatePeople()
    {
        // Limpiar personas anteriores
        foreach (var person in people)
        {
            if (person != null)
                DestroyImmediate(person.gameObject);
        }
        people.Clear();

        Debug.Log($"Creating {numberOfPeople} people...");

        // Crear N personas
        for (int i = 0; i < numberOfPeople; i++)
        {
            GameObject personGO = new GameObject($"Person_{i}");
            personGO.transform.parent = transform;
            personGO.transform.position = spawnPoint + Vector3.right * (i * spacing);

            // Agregar componentes
            PersonController controller = personGO.AddComponent<PersonController>();

            // Crear una ruta simple para cada persona
            List<Vector3> route = GenerateRandomRoute(personGO.transform.position);
            controller.SetWaypoints(route);

            people.Add(controller);
        }

        Debug.Log($"{people.Count} personas creadas exitosamente");
    }

    [ContextMenu("Pausar Todos")]
    public void PauseAll()
    {
        foreach (var person in people)
        {
            if (person != null)
                person.SetMoving(false);
        }
        Debug.Log("Todas las personas pausadas");
    }

    [ContextMenu("Reanudar Todos")]
    public void ResumeAll()
    {
        foreach (var person in people)
        {
            if (person != null)
                person.SetMoving(true);
        }
        Debug.Log("Todas las personas reanudadas");
    }

    [ContextMenu("Reiniciar Rutas")]
    public void RestartAllRoutes()
    {
        foreach (var person in people)
        {
            if (person != null)
                person.RestartRoute();
        }
        Debug.Log("Todas las rutas reiniciadas");
    }

    /// <summary>
    /// Generar una ruta cuadrada aleatoria
    /// </summary>
    private List<Vector3> GenerateRandomRoute(Vector3 startPos)
    {
        float randomSize = Random.Range(5f, 15f);
        
        List<Vector3> route = new List<Vector3>
        {
            startPos,
            startPos + Vector3.right * randomSize + Vector3.forward * Random.Range(-2f, 2f),
            startPos + Vector3.right * randomSize + Vector3.forward * randomSize,
            startPos + Vector3.forward * randomSize
        };

        return route;
    }

    /// <summary>
    /// Ejemplo: Ruta en forma de círculo
    /// </summary>
    public List<Vector3> GenerateCircularRoute(Vector3 center, float radius, int segments)
    {
        List<Vector3> route = new List<Vector3>();
        float angleStep = 360f / segments;

        for (int i = 0; i < segments; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            Vector3 pos = center + new Vector3(
                Mathf.Cos(angle) * radius,
                0,
                Mathf.Sin(angle) * radius
            );
            route.Add(pos);
        }

        return route;
    }

    /// <summary>
    /// Ejemplo: Crear una persona específica en una posición
    /// </summary>
    public PersonController CreatePersonAt(Vector3 position, string name = "Person")
    {
        GameObject personGO = new GameObject(name);
        personGO.transform.parent = transform;
        personGO.transform.position = position;

        PersonController controller = personGO.AddComponent<PersonController>();
        people.Add(controller);

        return controller;
    }

    private void Start()
    {
        if (autoCreatePeople)
        {
            CreatePeople();
        }
    }

    /// <summary>
    /// Visualizar información de todas las personas
    /// </summary>
    private void OnGUI()
    {
        if (GUILayout.Button("Mostrar Info Personas", GUILayout.Width(200), GUILayout.Height(30)))
        {
            foreach (var person in people)
            {
                if (person != null)
                {
                    Debug.Log($"{person.gameObject.name}: " +
                        $"Waypoint {person.GetCurrentWaypointIndex()}, " +
                        $"Distancia: {person.GetDistanceToCurrentWaypoint():F2}m");
                }
            }
        }
    }
}
