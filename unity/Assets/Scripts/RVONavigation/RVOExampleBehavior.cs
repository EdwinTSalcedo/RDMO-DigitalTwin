using UnityEngine;

/// <summary>
/// Script de ejemplo para demostrar cómo usar RVO2 en una escena Unity.
/// Coloca este script en el Hierarchy junto con los agentes y mantén presionadas
/// las teclas para ver cómo se comportan los agentes con evitación de colisiones.
/// </summary>
public class RVOExampleBehavior : MonoBehaviour
{
    [Header("Control Manual")]
    [SerializeField] private bool enableManualControl = true;
    [SerializeField] private float manualSpeed = 3f;
    [SerializeField] private KeyCode forwardKey = KeyCode.W;
    [SerializeField] private KeyCode backwardKey = KeyCode.S;
    [SerializeField] private KeyCode leftKey = KeyCode.A;
    [SerializeField] private KeyCode rightKey = KeyCode.D;
    
    [Header("Target IA")]
    [SerializeField] private Transform targetGoal;
    [SerializeField] private float targetDetectionRadius = 0.5f;
    
    private RVOAgentController agentController;
    private bool usingManualControl = false;
    
    private void Start()
    {
        agentController = GetComponent<RVOAgentController>();
        
        if (agentController == null)
        {
            Debug.LogError($"[EJEMPLO] {gameObject.name} no tiene RVOAgentController");
            enabled = false;
            return;
        }
        
        Debug.Log($"[EJEMPLO] Agente '{gameObject.name}' inicializado");
        Debug.Log($"[EJEMPLO] Controles: W/A/S/D para movimiento manual, ESPACIO para IA automática");
    }
    
    private void Update()
    {
        HandleInput();
    }
    
    private void HandleInput()
    {
        // Cambiar entre control manual e IA con ESPACIO
        if (Input.GetKeyDown(KeyCode.Space))
        {
            usingManualControl = !usingManualControl;
            
            if (usingManualControl)
            {
                Debug.Log($"[EJEMPLO] {gameObject.name}: CONTROL MANUAL activado");
                agentController.ClearManualVelocity();
            }
            else
            {
                Debug.Log($"[EJEMPLO] {gameObject.name}: IA automática activada");
                if (targetGoal != null)
                {
                    agentController.SetTarget(targetGoal);
                }
            }
        }
        
        // Movimiento manual
        if (enableManualControl && usingManualControl)
        {
            Vector2 inputVelocity = Vector2.zero;
            
            if (Input.GetKey(forwardKey))
                inputVelocity.y += manualSpeed;
            if (Input.GetKey(backwardKey))
                inputVelocity.y -= manualSpeed;
            if (Input.GetKey(leftKey))
                inputVelocity.x -= manualSpeed;
            if (Input.GetKey(rightKey))
                inputVelocity.x += manualSpeed;
            
            if (inputVelocity != Vector2.zero)
            {
                agentController.SetManualVelocity(inputVelocity.normalized * manualSpeed);
            }
            else
            {
                agentController.ClearManualVelocity();
            }
        }
        
        // Detección de llegada a objetivo
        if (targetGoal != null && !usingManualControl)
        {
            float distToTarget = Vector3.Distance(transform.position, targetGoal.position);
            if (distToTarget < targetDetectionRadius)
            {
                agentController.ClearManualVelocity();
            }
        }
    }
}
