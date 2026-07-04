using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controlador para una persona (cápsula) que se mueve siguiendo una ruta definida de waypoints
/// </summary>
public class PersonController : MonoBehaviour
{
    [Header("Waypoints")]
    [SerializeField] private List<Vector3> waypoints = new List<Vector3>();
    [SerializeField] private bool loopRoute = true;
    [SerializeField] private float waypointTolerance = 0.2f;

    [Header("Movimiento")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float rotationSpeed = 5f;
    [SerializeField] private bool rotateTowardsWaypoint = true;

    [Header("Física")]
    [SerializeField] private float capsuleHeight = 1.8f;
    [SerializeField] private float capsuleRadius = 0.3f;

    [Header("Evitación de Personas")]
    [SerializeField] private bool enableAvoidance = true;
    [SerializeField] private float detectionRadius = 2f;
    [SerializeField] private float pauseWhenTooClose = 0.8f;
    [SerializeField] private float avoidanceForce = 0.5f;

    [Header("Área de Influencia")]
    [SerializeField] private Vector3 influenceAreaCenter;
    [SerializeField] private Vector3 influenceAreaSize = new Vector3(20, 2, 20);
    [SerializeField] private bool constrainToArea = true;

    [Header("Visualización")]
    [SerializeField] private bool showWaypoints = true;
    [SerializeField] private bool showPath = true;
    [SerializeField] private bool showAvoidanceRadius = false;
    [SerializeField] private bool showInfluenceArea = false;
    [SerializeField] private Color waypointColor = Color.cyan;
    [SerializeField] private Color pathColor = Color.green;
    [SerializeField] private Color avoidanceColor = Color.yellow;

    private int currentWaypointIndex = 0;
    private Rigidbody rb;
    private bool isMoving = false;
    private float pauseTimer = 0f;
    private Vector3 avoidanceDirection = Vector3.zero;
    private float avoidanceTimer = 0f;

    private void Start()
    {
        // Obtener o crear Rigidbody
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }

        // Crear cápsula si no existe
        if (GetComponent<CapsuleCollider>() == null)
        {
            CapsuleCollider capsule = gameObject.AddComponent<CapsuleCollider>();
            capsule.height = capsuleHeight;
            capsule.radius = capsuleRadius;
        }

        // Agregar waypoint inicial si no hay
        if (waypoints.Count == 0)
        {
            waypoints.Add(transform.position);
            waypoints.Add(transform.position + Vector3.forward * 5f);
        }

        // Inicializar área de influencia (si no está establecida)
        if (influenceAreaCenter == Vector3.zero && waypoints.Count > 0)
        {
            influenceAreaCenter = waypoints[0];
        }

        isMoving = true;
    }

    private void Update()
    {
        if (!isMoving || waypoints.Count == 0)
            return;

        // Actualizar timers
        if (pauseTimer > 0)
            pauseTimer -= Time.deltaTime;
        
        if (avoidanceTimer > 0)
            avoidanceTimer -= Time.deltaTime;

        // Detectar y evitar otras personas
        if (enableAvoidance)
        {
            DetectAndAvoidOthers();
        }

        // Mover hacia el waypoint (si no está pausado)
        if (pauseTimer <= 0)
        {
            MoveTowardsWaypoint();
        }
    }

    private void MoveTowardsWaypoint()
    {
        Vector3 currentWaypoint = waypoints[currentWaypointIndex];
        Vector3 direction = (currentWaypoint - transform.position).normalized;
        float distanceToWaypoint = Vector3.Distance(transform.position, currentWaypoint);

        // Aplicar dirección de evitación
        if (avoidanceTimer > 0 && avoidanceDirection.magnitude > 0)
        {
            direction = Vector3.Lerp(direction, avoidanceDirection, avoidanceForce);
            direction.Normalize();
        }

        // Rotar hacia el waypoint
        if (rotateTowardsWaypoint && direction.magnitude > 0)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
        }

        // Mover hacia el waypoint
        if (distanceToWaypoint > waypointTolerance)
        {
            Vector3 newPosition = transform.position + direction * moveSpeed * Time.deltaTime;
            
            // Mantener dentro del área de influencia
            if (constrainToArea)
            {
                newPosition = ClampToInfluenceArea(newPosition);
            }

            transform.position = newPosition;
        }
        else
        {
            // Waypoint alcanzado
            AdvanceToNextWaypoint();
        }
    }

    /// <summary>
    /// Detectar y evitar a otras personas cercanas
    /// </summary>
    private void DetectAndAvoidOthers()
    {
        Collider[] nearbyColliders = Physics.OverlapSphere(transform.position, detectionRadius);
        
        Vector3 avoidanceForceVector = Vector3.zero;
        int personCount = 0;

        foreach (Collider col in nearbyColliders)
        {
            // Saltar si es el mismo objeto
            if (col.gameObject == gameObject)
                continue;

            // Buscar PersonController en el objeto
            PersonController otherPerson = col.GetComponent<PersonController>();
            if (otherPerson == null)
                continue;

            personCount++;
            Vector3 directionAway = (transform.position - otherPerson.transform.position).normalized;
            float distance = Vector3.Distance(transform.position, otherPerson.transform.position);
            
            // Si está muy cerca, pausar
            if (distance < pauseWhenTooClose)
            {
                pauseTimer = 0.5f;
                return;
            }

            // Acumular dirección de evitación
            float weight = 1f - (distance / detectionRadius);
            avoidanceForceVector += directionAway * weight;
        }

        // Aplicar evitación si hay personas cercanas
        if (personCount > 0)
        {
            avoidanceDirection = avoidanceForceVector.normalized;
            avoidanceTimer = 1f;
        }
        else
        {
            avoidanceTimer = 0f;
        }
    }

    /// <summary>
    /// Limitar la posición al área de influencia
    /// </summary>
    private Vector3 ClampToInfluenceArea(Vector3 position)
    {
        Vector3 relative = position - influenceAreaCenter;
        
        relative.x = Mathf.Clamp(relative.x, -influenceAreaSize.x * 0.5f, influenceAreaSize.x * 0.5f);
        relative.y = Mathf.Clamp(relative.y, -influenceAreaSize.y * 0.5f, influenceAreaSize.y * 0.5f);
        relative.z = Mathf.Clamp(relative.z, -influenceAreaSize.z * 0.5f, influenceAreaSize.z * 0.5f);
        
        return influenceAreaCenter + relative;
    }

    private void AdvanceToNextWaypoint()
    {
        currentWaypointIndex++;

        if (currentWaypointIndex >= waypoints.Count)
        {
            if (loopRoute)
            {
                currentWaypointIndex = 0;
                Debug.Log($"[PersonController] Ruta reiniciada desde waypoint 0");
            }
            else
            {
                currentWaypointIndex = waypoints.Count - 1;
                isMoving = false;
                Debug.Log($"[PersonController] Ruta completada");
            }
        }
        else
        {
            Debug.Log($"[PersonController] Avanzando hacia waypoint {currentWaypointIndex}");
        }
    }

    /// <summary>
    /// Agregar un waypoint a la ruta
    /// </summary>
    public void AddWaypoint(Vector3 position)
    {
        waypoints.Add(position);
        Debug.Log($"[PersonController] Waypoint {waypoints.Count - 1} agregado en {position}");
    }

    /// <summary>
    /// Establecer todos los waypoints de una vez
    /// </summary>
    public void SetWaypoints(List<Vector3> newWaypoints)
    {
        waypoints = new List<Vector3>(newWaypoints);
        currentWaypointIndex = 0;
        isMoving = true;
        Debug.Log($"[PersonController] {waypoints.Count} waypoints establecidos");
    }

    /// <summary>
    /// Pausar/Reanudar movimiento
    /// </summary>
    public void SetMoving(bool moving)
    {
        isMoving = moving;
    }

    /// <summary>
    /// Reiniciar la ruta desde el principio
    /// </summary>
    public void RestartRoute()
    {
        currentWaypointIndex = 0;
        isMoving = true;
        Debug.Log($"[PersonController] Ruta reiniciada");
    }

    /// <summary>
    /// Obtener el waypoint actual
    /// </summary>
    public Vector3 GetCurrentWaypoint()
    {
        if (waypoints.Count == 0) return transform.position;
        return waypoints[currentWaypointIndex];
    }

    /// <summary>
    /// Obtener el índice del waypoint actual
    /// </summary>
    public int GetCurrentWaypointIndex()
    {
        return currentWaypointIndex;
    }

    /// <summary>
    /// Obtener distancia al waypoint actual
    /// </summary>
    public float GetDistanceToCurrentWaypoint()
    {
        return Vector3.Distance(transform.position, GetCurrentWaypoint());
    }

    /// <summary>
    /// Establecer el área de influencia
    /// </summary>
    public void SetInfluenceArea(Vector3 center, Vector3 size)
    {
        influenceAreaCenter = center;
        influenceAreaSize = size;
    }

    /// <summary>
    /// Habilitar/deshabilitar la evitación de personas
    /// </summary>
    public void SetAvoidanceEnabled(bool enabled)
    {
        enableAvoidance = enabled;
    }

    /// <summary>
    /// Obtener si está evitando a alguien en este momento
    /// </summary>
    public bool IsAvoiding()
    {
        return avoidanceTimer > 0 || pauseTimer > 0;
    }

    private void OnDrawGizmosSelected()
    {
        if (waypoints == null || waypoints.Count == 0)
            return;

        // Dibujar waypoints
        if (showWaypoints)
        {
            for (int i = 0; i < waypoints.Count; i++)
            {
                Gizmos.color = (i == currentWaypointIndex) ? Color.red : waypointColor;
                float size = (i == currentWaypointIndex) ? 0.3f : 0.2f;
                Gizmos.DrawSphere(waypoints[i], size);
                
                // Número del waypoint
                Vector3 labelPos = waypoints[i] + Vector3.up * 0.5f;
            }
        }

        // Dibujar ruta
        if (showPath)
        {
            Gizmos.color = pathColor;
            for (int i = 0; i < waypoints.Count - 1; i++)
            {
                Gizmos.DrawLine(waypoints[i], waypoints[i + 1]);
            }

            // Línea de cierre si es loop
            if (loopRoute && waypoints.Count > 1)
            {
                Gizmos.color = new Color(pathColor.r, pathColor.g, pathColor.b, 0.5f);
                Gizmos.DrawLine(waypoints[waypoints.Count - 1], waypoints[0]);
            }
        }

        // Dibujar radio de detección
        if (showAvoidanceRadius && Application.isPlaying)
        {
            Gizmos.color = avoidanceColor;
            Gizmos.DrawWireSphere(transform.position, detectionRadius);
        }

        // Dibujar área de influencia
        if (showInfluenceArea)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
            DrawBox(influenceAreaCenter, influenceAreaSize, Quaternion.identity);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        // Dibujar la cápsula en tiempo de edición
        if (Application.isPlaying)
            return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, new Vector3(capsuleRadius * 2, capsuleHeight, capsuleRadius * 2));

        // Dibujar área de influencia en modo edición
        if (showInfluenceArea && influenceAreaCenter != Vector3.zero)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f);
            DrawBox(influenceAreaCenter, influenceAreaSize, Quaternion.identity);
        }
    }

    private void DrawBox(Vector3 center, Vector3 size, Quaternion rotation)
    {
        Vector3 halfSize = size * 0.5f;
        Vector3[] corners = new Vector3[8]
        {
            center + rotation * new Vector3(-halfSize.x, -halfSize.y, -halfSize.z),
            center + rotation * new Vector3(halfSize.x, -halfSize.y, -halfSize.z),
            center + rotation * new Vector3(halfSize.x, halfSize.y, -halfSize.z),
            center + rotation * new Vector3(-halfSize.x, halfSize.y, -halfSize.z),
            center + rotation * new Vector3(-halfSize.x, -halfSize.y, halfSize.z),
            center + rotation * new Vector3(halfSize.x, -halfSize.y, halfSize.z),
            center + rotation * new Vector3(halfSize.x, halfSize.y, halfSize.z),
            center + rotation * new Vector3(-halfSize.x, halfSize.y, halfSize.z)
        };

        // Dibujar aristas
        Gizmos.DrawLine(corners[0], corners[1]);
        Gizmos.DrawLine(corners[1], corners[2]);
        Gizmos.DrawLine(corners[2], corners[3]);
        Gizmos.DrawLine(corners[3], corners[0]);
        Gizmos.DrawLine(corners[4], corners[5]);
        Gizmos.DrawLine(corners[5], corners[6]);
        Gizmos.DrawLine(corners[6], corners[7]);
        Gizmos.DrawLine(corners[7], corners[4]);
        Gizmos.DrawLine(corners[0], corners[4]);
        Gizmos.DrawLine(corners[1], corners[5]);
        Gizmos.DrawLine(corners[2], corners[6]);
        Gizmos.DrawLine(corners[3], corners[7]);
    }
#endif
}
