using UnityEngine;
using RVO;

/// <summary>
/// Define un obstáculo dinámico (que se mueve).
/// Se diferencia del obstáculo estático porque se actualiza cada frame.
/// </summary>
public class RVODynamicObstacle : MonoBehaviour
{
    [Header("Configuración")]
    [SerializeField] private bool usePhysics = true; // Usar Rigidbody para posición
    [SerializeField] private float updateFrequency = 0.1f; // Actualizar cada X segundos
    [SerializeField] private float radius = 0.5f;
    
    [Header("Detección")]
    [SerializeField] private Collider obstacleCollider;
    [SerializeField] private Rigidbody obstacleRigidbody;
    
    private float timeSinceLastUpdate = 0f;
    private UnityEngine.Vector2 lastPosition;
    private RVO.Vector2 currentVelocity;
    private bool isInitialized = false;
    
    private void Start()
    {
        // Obtener collider automáticamente
        if (obstacleCollider == null)
        {
            obstacleCollider = GetComponent<Collider>();
        }
        
        // Obtener Rigidbody si existe
        if (usePhysics && obstacleRigidbody == null)
        {
            obstacleRigidbody = GetComponent<Rigidbody>();
        }
        
        if (obstacleCollider == null)
        {
            Debug.LogError($"[RVO DYNAMIC OBSTACLE] {gameObject.name} necesita un Collider");
            enabled = false;
            return;
        }
        
        lastPosition = new UnityEngine.Vector2(transform.position.x, transform.position.z);
        currentVelocity = new RVO.Vector2(0, 0);
        isInitialized = true;
        
        Debug.Log($"[RVO DYNAMIC] Obstáculo dinámico '{gameObject.name}' inicializado");
    }
    
    private void Update()
    {
        if (!isInitialized) return;
        
        timeSinceLastUpdate += Time.deltaTime;
        
        // Actualizar posición y velocidad del obstáculo
        if (timeSinceLastUpdate >= updateFrequency)
        {
            UpdateObstaclePosition();
            timeSinceLastUpdate = 0f;
        }
    }
    
    /// <summary>Actualiza la posición del obstáculo en el simulador RVO.</summary>
    private void UpdateObstaclePosition()
    {
        Vector3 currentWorldPos = transform.position;
        UnityEngine.Vector2 newPosition = new UnityEngine.Vector2(currentWorldPos.x, currentWorldPos.z);
        
        // Calcular velocidad basada en movimiento
        RVO.Vector2 velocity = new RVO.Vector2(newPosition.x - lastPosition.x, newPosition.y - lastPosition.y);
        currentVelocity = velocity / updateFrequency;
        lastPosition = newPosition;
        
        // Aquí podrías actualizar la posición en RVO si lo necesitas
        // Nota: RVO2 por defecto maneja obstáculos estáticos
        // Para dinámicos necesitarías reconstruir el árbol o usar otro enfoque
    }
    
    /// <summary>Obtiene la posición actual del obstáculo.</summary>
    public RVO.Vector2 GetPosition()
    {
        Vector3 pos = transform.position;
        return new RVO.Vector2(pos.x, pos.z);
    }
    
    /// <summary>Obtiene la velocidad actual del obstáculo.</summary>
    public RVO.Vector2 GetVelocity()
    {
        return currentVelocity;
    }
    
    /// <summary>Obtiene el radio del obstáculo (para detección).</summary>
    public float GetRadius() => radius;
    
    /// <summary>Comprueba si el obstáculo es dinámico.</summary>
    public bool IsDynamic() => true;
    
    /// <summary>Obtiene los límites del obstáculo.</summary>
    public Bounds GetBounds()
    {
        return obstacleCollider != null ? obstacleCollider.bounds : new Bounds();
    }
    
    /// <summary>Comprueba si un agente está cerca de este obstáculo.</summary>
    public bool IsAgentNearby(RVOAgentController agent, float detectionRadius)
    {
        Vector3 agentPos = agent.transform.position;
        Vector3 obstaclePos = transform.position;
        
        float distance = Vector3.Distance(
            new Vector3(agentPos.x, 0, agentPos.z),
            new Vector3(obstaclePos.x, 0, obstaclePos.z)
        );
        
        return distance < detectionRadius;
    }
    
    private void OnDrawGizmos()
    {
        #if UNITY_EDITOR
        if (!Application.isPlaying) return;
        
        // Dibujar radio de obstáculo
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, radius);
        
        // Dibujar velocidad
        if (RVOMath.abs(currentVelocity) > 0.01f)
        {
            Gizmos.color = Color.yellow;
            RVO.Vector2 vel = RVOMath.normalize(currentVelocity) * 2f;
            Gizmos.DrawLine(
                transform.position,
                transform.position + new Vector3(vel.x(), 0, vel.y())
            );
        }
        #endif
    }
}
