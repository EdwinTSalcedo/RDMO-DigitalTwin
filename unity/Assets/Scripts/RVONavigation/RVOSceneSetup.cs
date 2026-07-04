using UnityEngine;

public class RVOSceneSetup : MonoBehaviour
{
    [SerializeField] private bool autoSetupOnStart = true;
    
    private void Start()
    {
        if (autoSetupOnStart)
        {
            SetupRVOScene();
        }
    }
    
    public void SetupRVOScene()
    {
        Debug.Log("[RVO] Iniciando setup de escena RVO...");
        
        // Paso 1: Procesar todos los obstáculos
        RVOObstacle[] allObstacles = FindObjectsByType<RVOObstacle>(FindObjectsSortMode.None);
        foreach (RVOObstacle obstacle in allObstacles)
        {
            obstacle.RegisterInRVO();
        }
        
        // Paso 2: Procesar obstáculos en simulador
        RVOSimulationManager.Instance.ProcessAllObstacles();
        
        // Paso 3: Registrar agentes (automático en Start() de RVOAgentController)
        RVOAgentController[] allAgents = FindObjectsByType<RVOAgentController>(FindObjectsSortMode.None);
        Debug.Log($"[RVO] {allAgents.Length} agentes encontrados en escena");
        
        Debug.Log("[RVO] Setup de escena completado");
    }
}
