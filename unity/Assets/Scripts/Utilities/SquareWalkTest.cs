using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Demo simple: Personas caminando en cuadrados sin evitación
/// </summary>
public class SquareWalkTest : MonoBehaviour
{
    [Header("Configuración")]
    [SerializeField] private int numberOfPeople = 4;
    [SerializeField] private float squareSize = 10f;
    [SerializeField] private float spacing = 15f;
    [SerializeField] private float moveSpeed = 1.5f;

    private List<PersonController> people = new List<PersonController>();

    private void Start()
    {
        CreatePeopleInSquares();
    }

    [ContextMenu("Crear Personas Caminando en Cuadrados")]
    public void CreatePeopleInSquares()
    {
        // Limpiar anteriores
        foreach (var person in people)
        {
            if (person != null)
                DestroyImmediate(person.gameObject);
        }
        people.Clear();

        Debug.Log($"✓ Creando {numberOfPeople} personas caminando en cuadrados...");

        // Crear personas en grid
        int gridSize = Mathf.CeilToInt(Mathf.Sqrt(numberOfPeople));
        
        for (int i = 0; i < numberOfPeople; i++)
        {
            // Posición en grid (para que tengan espacios)
            int gridX = i % gridSize;
            int gridZ = i / gridSize;
            
            Vector3 centerPos = new Vector3(
                gridX * spacing,
                0,
                gridZ * spacing
            );

            // Crear GameObject
            GameObject personGO = new GameObject($"Person_{i}");
            personGO.transform.position = centerPos;

            // Agregar PersonController
            PersonController controller = personGO.AddComponent<PersonController>();

            // Crear cuadrado alrededor de centerPos
            List<Vector3> cuadrado = new List<Vector3>
            {
                centerPos + new Vector3(0, 0, 0),
                centerPos + new Vector3(squareSize, 0, 0),
                centerPos + new Vector3(squareSize, 0, squareSize),
                centerPos + new Vector3(0, 0, squareSize)
            };

            controller.SetWaypoints(cuadrado);
            
            // SIN evitación
            controller.SetAvoidanceEnabled(false);

            people.Add(controller);
            Debug.Log($"  Person {i} en cuadrado centrado en {centerPos}");
        }

        Debug.Log($"✅ {people.Count} personas creadas caminando en cuadrados");
    }

    [ContextMenu("Pausar Todos")]
    public void PauseAll()
    {
        foreach (var person in people)
        {
            if (person != null)
                person.SetMoving(false);
        }
        Debug.Log("⏸️ Todos pausados");
    }

    [ContextMenu("Reanudar Todos")]
    public void ResumeAll()
    {
        foreach (var person in people)
        {
            if (person != null)
                person.SetMoving(true);
        }
        Debug.Log("▶️ Todos reanudados");
    }

    [ContextMenu("Mostrar Info")]
    public void PrintInfo()
    {
        Debug.Log("=== Info de Personas ===");
        for (int i = 0; i < people.Count; i++)
        {
            if (people[i] != null)
            {
                Debug.Log($"Person {i}: WP {people[i].GetCurrentWaypointIndex()}, " +
                         $"Dist: {people[i].GetDistanceToCurrentWaypoint():F2}m");
            }
        }
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 300, 150));
        
        GUILayout.Label("🟩 SQUARE WALK TEST (Sin Evitación)", 
            new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold });
        
        GUILayout.Space(10);
        
        if (GUILayout.Button("▶️ Crear Personas en Cuadrados", GUILayout.Height(30)))
            CreatePeopleInSquares();
        
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("⏸ Pausar", GUILayout.Height(25)))
            PauseAll();
        if (GUILayout.Button("▶ Reanudar", GUILayout.Height(25)))
            ResumeAll();
        GUILayout.EndHorizontal();

        if (GUILayout.Button("ℹ️ Info", GUILayout.Height(25)))
            PrintInfo();

        GUILayout.Label($"Personas: {people.Count}");
        GUILayout.Label($"Tamaño cuadrado: {squareSize}m");

        GUILayout.EndArea();
    }
}
