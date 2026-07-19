using UnityEngine;
using RVO;

/// <summary>
/// Controlador de agente RVO2.
/// Deja que RVO2 calcule internamente la evasión de obstáculos y agentes.
/// NO usa raycast propio para evitar conflictos con el simulador RVO.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class RVOAgentNavigator : MonoBehaviour
{
    [Header("Configuración del Agente RVO")]
    [SerializeField] private float neighborDist    = 15f;
    [SerializeField] private int   maxNeighbors    = 10;
    [SerializeField] private float timeHorizon     = 5f;
    [SerializeField] private float timeHorizonObst = 5f;   // ← más alto = más tiempo para rodear
    [SerializeField] private float radius          = 0.5f;
    [SerializeField] private float maxSpeed        = 5f;

    [Header("Comportamiento")]
    [SerializeField] private Transform target;
    [SerializeField] private float stoppingDistance = 0.5f; // Distancia para detenerse en el target

    [Header("Debug")]
    [SerializeField] private bool drawDebugGizmos = true;

    // --- Estado interno ---
    private int        rvoAgentId = -1;
    private Rigidbody  rb;
    private bool       isRegistered = false;
    private RVO.Vector2 rvoPreferredVelocity;

    // -----------------------------------------------------------------------
    #region Unity Lifecycle

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotationX |
                         RigidbodyConstraints.FreezeRotationY |
                         RigidbodyConstraints.FreezeRotationZ;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
    }

    private void Start()
    {
        // Registrar en el simulador RVO
        RVO.Vector2 pos = ToRVO(transform.position);
        rvoAgentId = Simulator.Instance.addAgent(
            pos,
            neighborDist,
            maxNeighbors,
            timeHorizon,
            timeHorizonObst,
            radius,
            maxSpeed,
            new RVO.Vector2(0f, 0f)
        );

        isRegistered = (rvoAgentId >= 0);

        if (isRegistered)
        {
            // Registrarse en el manager para que FixedUpdate lo incluya
            RVOSimulationManager.Instance.RegisterNavigator(this);
            Debug.Log($"[RVO NAV] '{gameObject.name}' registrado con ID: {rvoAgentId}");
        }
        else
            Debug.LogError($"[RVO NAV] '{gameObject.name}' no pudo registrarse en RVO.");
    }

    /// <summary>
    /// Llamado por RVOSimulationManager ANTES de doStep().
    /// Sincroniza la posición actual y calcula la velocidad preferida.
    /// </summary>
    public void PrepareStep()
    {
        if (!isRegistered) return;

        // Sincronizar posición actual de Unity con el simulador RVO
        Simulator.Instance.setAgentPosition(rvoAgentId, ToRVO(transform.position));

        // Calcular velocidad preferida: dirección pura hacia el destino a velocidad máxima
        rvoPreferredVelocity = ComputePreferredVelocity();
        Simulator.Instance.setAgentPrefVelocity(rvoAgentId, rvoPreferredVelocity);
    }

    /// <summary>
    /// Llamado por RVOSimulationManager DESPUÉS de doStep().
    /// Aplica la velocidad calculada por RVO al Rigidbody.
    /// </summary>
    public void ApplyRVOVelocity()
    {
        if (!isRegistered) return;

        // Obtener la velocidad que RVO calculó (ya incluye evasión de obstáculos)
        RVO.Vector2 rvoVel = Simulator.Instance.getAgentVelocity(rvoAgentId);

        Vector3 velocity3D = new Vector3(rvoVel.x(), 0f, rvoVel.y());

        // Mover usando MovePosition para respetar la física
        rb.MovePosition(transform.position + velocity3D * Time.fixedDeltaTime);
    }

    #endregion

    // -----------------------------------------------------------------------
    #region Lógica principal

    private RVO.Vector2 ComputePreferredVelocity()
    {
        if (target == null)
            return new RVO.Vector2(0f, 0f);

        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;

        float distToTarget = toTarget.magnitude;

        // Si ya llegó al destino, velocidad cero
        if (distToTarget <= stoppingDistance)
            return new RVO.Vector2(0f, 0f);

        // Velocidad preferida: dirección normalizada × velocidad máxima
        // RVO2 es susceptible a quedarse atrapado en mínimos locales si choca
        // perfecta y perpendicularmente contra un muro plano (simetría perfecta).
        // Añadimos un minúsculo ruido para romper la simetría y forzar a que elija un lado.
        Vector3 dir = toTarget.normalized;
        float noise = Mathf.Sin(Time.time * 5f + rvoAgentId) * 0.05f;
        Vector3 noisyDir = new Vector3(dir.x + noise, 0f, dir.z - noise).normalized;
        
        return new RVO.Vector2(noisyDir.x, noisyDir.z) * maxSpeed;
    }

    #endregion

    // -----------------------------------------------------------------------
    #region API pública

    public void SetTarget(Transform newTarget)   => target = newTarget;
    public int  GetRVOAgentId()                  => rvoAgentId;
    public bool IsRegistered()                   => isRegistered;
    public RVO.Vector2 GetPreferredVelocity()    => rvoPreferredVelocity;

    #endregion

    // -----------------------------------------------------------------------
    #region Utilidades

    private static RVO.Vector2 ToRVO(Vector3 v) => new RVO.Vector2(v.x, v.z);

    #endregion

    // -----------------------------------------------------------------------
    #region Gizmos

    private void OnDrawGizmos()
    {
#if UNITY_EDITOR
        if (!drawDebugGizmos || !Application.isPlaying) return;

        // Radio del agente
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, radius);

        // Velocidad preferida (azul)
        if (rvoPreferredVelocity.x() != 0f || rvoPreferredVelocity.y() != 0f)
        {
            Gizmos.color = Color.blue;
            Vector3 prefVelDir = new Vector3(rvoPreferredVelocity.x(), 0f, rvoPreferredVelocity.y()).normalized;
            Gizmos.DrawLine(transform.position, transform.position + prefVelDir * 2f);
        }

        // Velocidad real de RVO (verde)
        if (isRegistered && Application.isPlaying)
        {
            RVO.Vector2 rvoVel = Simulator.Instance.getAgentVelocity(rvoAgentId);
            if (rvoVel.x() != 0f || rvoVel.y() != 0f)
            {
                Gizmos.color = Color.green;
                Vector3 velDir = new Vector3(rvoVel.x(), 0f, rvoVel.y()).normalized;
                Gizmos.DrawLine(transform.position, transform.position + velDir * 2f);
            }
        }

        // Línea hacia objetivo (amarillo)
        if (target != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, target.position);
        }
#endif
    }

    #endregion

    private void OnDestroy()
    {
        // Desregistrar del manager
        if (RVOSimulationManager.Instance != null)
            RVOSimulationManager.Instance.UnregisterNavigator(this);

        // No hay API para quitar agentes en RVO2 C#, pero podemos poner vel=0
        if (isRegistered)
        {
            try { Simulator.Instance.setAgentPrefVelocity(rvoAgentId, new RVO.Vector2(0f, 0f)); }
            catch { }
        }
    }
}
