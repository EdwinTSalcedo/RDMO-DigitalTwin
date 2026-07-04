using UnityEngine;
using RVO;

public class RVOAgentController : MonoBehaviour
{
    [Header("Configuración de Movimiento")]
    [SerializeField] private float neighborDist = 15f;
    [SerializeField] private int maxNeighbors = 10;
    [SerializeField] private float timeHorizon = 5f;
    [SerializeField] private float timeHorizonObst = 2f;
    [SerializeField] private float radius = 0.5f;
    [SerializeField] private float maxSpeed = 5f;
    
    [Header("Comportamiento")]
    [SerializeField] private Transform target;
    [SerializeField] private bool useManualVelocity = false;
    [SerializeField] private UnityEngine.Vector2 manualVelocity = UnityEngine.Vector2.zero;
    
    private int rvoAgentId = -1;
    private Rigidbody rb;
    private RVO.Vector2 preferredVelocity = new RVO.Vector2(0, 0);
    
    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.constraints = RigidbodyConstraints.FreezeRotationX | 
                           RigidbodyConstraints.FreezeRotationY | 
                           RigidbodyConstraints.FreezeRotationZ;
            rb.useGravity = false;
        }
        
        // Registrar en el manager RVO
        RVOSimulationManager.Instance.RegisterAgent(this);
        
        // Registrar en simulador RVO
        RVO.Vector2 pos = new RVO.Vector2(transform.position.x, transform.position.z);
        rvoAgentId = Simulator.Instance.addAgent(
            pos,
            neighborDist,
            maxNeighbors,
            timeHorizon,
            timeHorizonObst,
            radius,
            maxSpeed,
            new RVO.Vector2(0, 0)
        );
        
        Debug.Log($"[RVO] Agente '{gameObject.name}' creado con ID: {rvoAgentId}");
    }
    
    public void UpdatePreferredVelocity()
    {
        if (rvoAgentId < 0) return;
        
        if (useManualVelocity)
        {
            // Usar velocidad manual
            preferredVelocity = new RVO.Vector2 (manualVelocity.x, manualVelocity.y);
        }
        else if (target != null)
        {
            // Calcular dirección hacia objetivo
            Vector3 direction = (target.position - transform.position).normalized;
            preferredVelocity = new RVO.Vector2(direction.x, direction.z) * maxSpeed;
        }
        else
        {
            // Sin objetivo
            preferredVelocity = new RVO.Vector2(0, 0);
        }
        
        // Establecer velocidad preferida en RVO
        Simulator.Instance.setAgentPrefVelocity(rvoAgentId, preferredVelocity);
    }
    
    public void SyncPositionFromRVO()
    {
        if (rvoAgentId < 0) return;
        
        // Obtener nueva posición y velocidad de RVO
        RVO.Vector2 rvoPos = Simulator.Instance.getAgentPosition(rvoAgentId);
        RVO.Vector2 rvoVel = Simulator.Instance.getAgentVelocity(rvoAgentId);
        
        // Actualizar posición del GameObject
        transform.position = new Vector3(rvoPos.x(), transform.position.y, rvoPos.y());
        
        // Aplicar velocidad al Rigidbody (si existe)
        if (rb != null)
        {
            rb.linearVelocity = new Vector3(rvoVel.x(), rb.linearVelocity.y, rvoVel.y());
        }
    }
    
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
    
    public void SetManualVelocity(UnityEngine.Vector2 velocity)
    {
        manualVelocity = velocity;
        useManualVelocity = true;
    }
    
    public void ClearManualVelocity()
    {
        useManualVelocity = false;
        manualVelocity = UnityEngine.Vector2.zero;
    }
    
    public int GetRVOAgentId() => rvoAgentId;
    public RVO.Vector2 GetPreferredVelocity() => preferredVelocity;
    
    private void OnDestroy()
    {
        if (RVOSimulationManager.Instance != null)
        {
            RVOSimulationManager.Instance.UnregisterAgent(this);
        }
    }
}
