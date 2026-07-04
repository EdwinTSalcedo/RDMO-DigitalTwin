using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Demo de integración de PersonController en una escena del Detector de Baches
/// Muestra cómo crear personas que se mueven alrededor de la escena
/// </summary>
public class DemoPersonInScene : MonoBehaviour
{
    [Header("Configuración de Personas")]
    [SerializeField] private int numGuards = 2;
    [SerializeField] private Vector3 firstGuardPos = new Vector3(0, 0, 0);
    [SerializeField] private Vector3 secondGuardPos = new Vector3(5, 0, 0);

    [Header("Áreas de Patrulla")]
    [SerializeField] private Vector3 areaMin = new Vector3(-10, 0, -10);
    [SerializeField] private Vector3 areaMax = new Vector3(10, 0, 10);
    [SerializeField] private int waypointsPerSide = 3;

    [Header("Comportamiento")]
    [SerializeField] private float moveSpeed = 1.5f;
    [SerializeField] private float stopDistanceIfDroneNear = 5f;
    [SerializeField] private bool autoStart = false;

    private List<PersonController> guards = new List<PersonController>();
    private DroneNavMeshController droneController;

    private void Start()
    {
        // Buscar el drone en la escena
        GameObject droneGO = GameObject.Find("Drone");
        if (droneGO != null)
        {
            droneController = droneGO.GetComponent<DroneNavMeshController>();
        }

        if (autoStart)
        {
            CreateGuards();
        }
    }

    [ContextMenu("Crear Guardias")]
    public void CreateGuards()
    {
        // Limpiar guardias anteriores
        foreach (var guard in guards)
        {
            if (guard != null)
                DestroyImmediate(guard.gameObject);
        }
        guards.Clear();

        // Guardia 1: Patrulla rectangular
        PersonController guard1 = CreateGuardWithPatrol(
            "Guard_1",
            firstGuardPos,
            GenerateRectangularPatrol(areaMin, areaMax, waypointsPerSide)
        );
        guards.Add(guard1);

        // Guardia 2: Ruta diferente
        PersonController guard2 = CreateGuardWithPatrol(
            "Guard_2",
            secondGuardPos,
            GenerateCircularPatrol(areaMin + areaMax) * 0.5f, 4, 8
        );
        guards.Add(guard2);

        Debug.Log($"✓ {guards.Count} guardias creados exitosamente");
    }

    private PersonController CreateGuardWithPatrol(string name, Vector3 startPos, List<Vector3> route)
    {
        GameObject guardGO = new GameObject(name);
        guardGO.transform.position = startPos;
        guardGO.layer = 0; // Default layer

        PersonController controller = guardGO.AddComponent<PersonController>();
        controller.SetWaypoints(route);

        return controller;
    }

    /// <summary>
    /// Generar patrulla rectangular alrededor de un área
    /// </summary>
    private List<Vector3> GenerateRectangularPatrol(Vector3 min, Vector3 max, int pointsPerSide)
    {
        List<Vector3> waypoints = new List<Vector3>();

        // Esquina inferior izquierda
        for (int i = 0; i < pointsPerSide; i++)
        {
            float t = (float)i / (pointsPerSide - 1);
            waypoints.Add(Vector3.Lerp(min, max, t));
        }

        // Lado derecho subiendo
        for (int i = 1; i < pointsPerSide; i++)
        {
            float t = (float)i / (pointsPerSide - 1);
            Vector3 right = min + (max - min);
            right.z = Mathf.Lerp(min.z, max.z, t);
            waypoints.Add(right);
        }

        // Lado superior
        for (int i = pointsPerSide - 2; i >= 0; i--)
        {
            float t = (float)i / (pointsPerSide - 1);
            Vector3 top = max;
            top.x = Mathf.Lerp(max.x, min.x, 1 - t);
            waypoints.Add(top);
        }

        // Lado izquierdo bajando
        for (int i = 1; i < pointsPerSide - 1; i++)
        {
            float t = (float)i / (pointsPerSide - 1);
            Vector3 left = min;
            left.z = Mathf.Lerp(max.z, min.z, t);
            waypoints.Add(left);
        }

        return waypoints;
    }

    /// <summary>
    /// Generar patrulla circular
    /// </summary>
    private List<Vector3> GenerateCircularPatrol(Vector3 center, int segments = 8, float radius = 5f)
    {
        List<Vector3> waypoints = new List<Vector3>();
        float angleStep = 360f / segments;

        for (int i = 0; i < segments; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            Vector3 pos = center + new Vector3(
                Mathf.Cos(angle) * radius,
                0,
                Mathf.Sin(angle) * radius
            );
            pos.y = center.y;
            waypoints.Add(pos);
        }

        return waypoints;
    }

    [ContextMenu("Pausar Todos los Guardias")]
    public void PauseAllGuards()
    {
        foreach (var guard in guards)
        {
            if (guard != null)
                guard.SetMoving(false);
        }
        Debug.Log("Todos los guardias pausados");
    }

    [ContextMenu("Reanudar Todos los Guardias")]
    public void ResumeAllGuards()
    {
        foreach (var guard in guards)
        {
            if (guard != null)
                guard.SetMoving(true);
        }
        Debug.Log("Todos los guardias reanudados");
    }

    [ContextMenu("Mostrar Estado")]
    public void PrintStatus()
    {
        Debug.Log("=== Estado de Guardias ===");
        for (int i = 0; i < guards.Count; i++)
        {
            if (guards[i] != null)
            {
                float dist = guards[i].GetDistanceToCurrentWaypoint();
                int wpIndex = guards[i].GetCurrentWaypointIndex();
                Debug.Log($"Guard {i}: Waypoint {wpIndex}, Distancia: {dist:F2}m");
            }
        }
    }

    /// <summary>
    /// Reaccionar cuando el drone pasa cerca de los guardias
    /// </summary>
    public void RespondToNearbyDrone(Vector3 dronePosition)
    {
        foreach (var guard in guards)
        {
            if (guard == null) continue;

            float distanceToDrone = Vector3.Distance(guard.transform.position, dronePosition);

            if (distanceToDrone < stopDistanceIfDroneNear)
            {
                Debug.Log($"¡Guardia {guard.gameObject.name} notó al dron!");
                guard.SetMoving(false);
            }
            else
            {
                guard.SetMoving(true);
            }
        }
    }
}
