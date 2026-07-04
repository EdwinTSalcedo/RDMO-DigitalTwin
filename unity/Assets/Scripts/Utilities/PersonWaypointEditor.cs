using UnityEngine;

/// <summary>
/// Editor de waypoints para PersonController
/// Permite agregar, remover e editar waypoints visualmente en el editor
/// </summary>
[ExecuteInEditMode]
public class PersonWaypointEditor : MonoBehaviour
{
    [Header("Editor de Waypoints")]
    [SerializeField] private PersonController personController;
    [SerializeField] private bool showEditorGizmos = true;
    [SerializeField] private float gizmoSize = 0.3f;
    [SerializeField] private Color selectedWaypointColor = Color.red;
    [SerializeField] private Color unselectedWaypointColor = Color.blue;
    [SerializeField] private int selectedWaypointIndex = -1;

    [SerializeField] private float snapToGridSize = 0f; // 0 = sin snap
    [SerializeField] private Vector3 newWaypointOffset = Vector3.forward * 2f;

    private void OnEnable()
    {
        if (personController == null)
        {
            personController = GetComponent<PersonController>();
        }
    }

    /// <summary>
    /// Agregar un nuevo waypoint en la posición especificada
    /// </summary>
    public void AddWaypointAtPosition(Vector3 position)
    {
        if (snapToGridSize > 0)
        {
            position.x = Mathf.Round(position.x / snapToGridSize) * snapToGridSize;
            position.z = Mathf.Round(position.z / snapToGridSize) * snapToGridSize;
        }

        personController.AddWaypoint(position);
    }

    /// <summary>
    /// Agregar un waypoint relativo a la posición actual del controlador
    /// </summary>
    public void AddWaypointRelative()
    {
        Vector3 newPos = transform.position + newWaypointOffset;
        AddWaypointAtPosition(newPos);
    }

    /// <summary>
    /// Agregar waypoint en la posición del objeto
    /// </summary>
    public void AddWaypointHere()
    {
        AddWaypointAtPosition(transform.position);
    }

    /// <summary>
    /// Teleportar el PersonController al siguiente waypoint
    /// </summary>
    public void TeleportToNextWaypoint()
    {
        int nextIndex = personController.GetCurrentWaypointIndex() + 1;
        transform.position = personController.GetCurrentWaypoint() + Vector3.up;
    }

    /// <summary>
    /// Test: Jugar la ruta
    /// </summary>
    public void PlayRoute()
    {
        if (Application.isPlaying)
        {
            personController.RestartRoute();
            personController.SetMoving(true);
        }
    }

    /// <summary>
    /// Test: Pausar la ruta
    /// </summary>
    public void PauseRoute()
    {
        if (Application.isPlaying)
        {
            personController.SetMoving(false);
        }
    }
}
