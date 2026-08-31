using UnityEngine;
using System.Collections.Generic;
using RVO;

/// <summary>
/// Gestor central de la simulación RVO2.
/// Ejecuta el paso de simulación y sincroniza posiciones con Unity.
/// Soporta tanto RVOAgentController (legacy) como RVOAgentNavigator (nuevo).
/// </summary>
public class RVOSimulationManager : MonoBehaviour
{
    [Header("Simulación RVO")]
    [SerializeField] private float timeStep   = 0.016f; // ≈ 60 FPS
    [SerializeField] private int   numWorkers = 0;       // 0 = auto

    [Header("Parámetros por defecto de Agentes")]
    [SerializeField] private float defaultNeighborDist    = 15f;
    [SerializeField] private int   defaultMaxNeighbors    = 10;
    [SerializeField] private float defaultTimeHorizon     = 5f;
    [SerializeField] private float defaultTimeHorizonObst = 5f;
    [SerializeField] private float defaultRadius          = 0.5f;
    [SerializeField] private float defaultMaxSpeed        = 5f;

    // -----------------------------------------------------------------------
    private static RVOSimulationManager instance;

    // Agentes legacy (RVOAgentController)
    private List<RVOAgentController> legacyAgents = new List<RVOAgentController>();

    // Agentes nuevos (RVOAgentNavigator)
    private List<RVOAgentNavigator> navigators = new List<RVOAgentNavigator>();

    // Obstáculos
    private List<RVOObstacle> obstacles = new List<RVOObstacle>();

    // Acumulador de tiempo para paso fijo
    // (accumulatedTime eliminado — no se usó en la lógica final)

    // -----------------------------------------------------------------------
    #region Singleton

    public static RVOSimulationManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<RVOSimulationManager>();
                if (instance == null)
                {
                    var go = new GameObject("RVOSimulationManager");
                    instance = go.AddComponent<RVOSimulationManager>();
                }
            }
            return instance;
        }
    }

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            InitializeSimulation();
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    #endregion

    // -----------------------------------------------------------------------
    #region Inicialización

    private void InitializeSimulation()
    {
        Simulator.Instance.Clear();
        Simulator.Instance.setTimeStep(timeStep);
        Simulator.Instance.SetNumWorkers(numWorkers);
        Simulator.Instance.setAgentDefaults(
            defaultNeighborDist,
            defaultMaxNeighbors,
            defaultTimeHorizon,
            defaultTimeHorizonObst,
            defaultRadius,
            defaultMaxSpeed,
            new RVO.Vector2(0f, 0f)
        );

        Debug.Log("[RVO MGR] Simulación inicializada.");
    }

    #endregion

    // -----------------------------------------------------------------------
    #region Update principal

    private void FixedUpdate()
    {
        // --- 1. Preparar velocidades preferidas ---

        // Agentes legacy
        foreach (var agent in legacyAgents)
            agent.UpdatePreferredVelocity();

        // Navigators: sincronizar posición y calcular pref-velocity
        foreach (var nav in navigators)
            nav.PrepareStep();

        // --- 2. Avanzar el simulador RVO ---
        Simulator.Instance.doStep();

        // --- 3. Aplicar resultados al mundo de Unity ---

        // Agentes legacy: sincronizar con transform directo
        foreach (var agent in legacyAgents)
            agent.SyncPositionFromRVO();

        // Navigators: aplicar velocidad RVO al Rigidbody
        foreach (var nav in navigators)
            nav.ApplyRVOVelocity();
    }

    #endregion

    // -----------------------------------------------------------------------
    #region Registro de agentes

    public void RegisterAgent(RVOAgentController agent)
    {
        if (!legacyAgents.Contains(agent))
        {
            legacyAgents.Add(agent);
            Debug.Log($"[RVO MGR] Agente legacy '{agent.gameObject.name}' registrado.");
        }
    }

    public void UnregisterAgent(RVOAgentController agent)
    {
        legacyAgents.Remove(agent);
    }

    public void RegisterNavigator(RVOAgentNavigator nav)
    {
        if (!navigators.Contains(nav))
        {
            navigators.Add(nav);
            Debug.Log($"[RVO MGR] Navigator '{nav.gameObject.name}' registrado.");
        }
    }

    public void UnregisterNavigator(RVOAgentNavigator nav)
    {
        navigators.Remove(nav);
    }

    #endregion

    // -----------------------------------------------------------------------
    #region Registro de obstáculos

    public void RegisterObstacle(RVOObstacle obstacle)
    {
        if (!obstacles.Contains(obstacle))
            obstacles.Add(obstacle);
    }

    public void UnregisterObstacle(RVOObstacle obstacle)
    {
        obstacles.Remove(obstacle);
    }

    /// <summary>
    /// Llama a processObstacles() para que RVO construya el árbol de obstáculos.
    /// Debe llamarse DESPUÉS de que todos los obstáculos hayan sido añadidos con addObstacle().
    /// </summary>
    public void ProcessAllObstacles()
    {
        Simulator.Instance.processObstacles();
        Debug.Log($"[RVO MGR] {Simulator.Instance.getNumObstacleVertices()} vértices de obstáculos procesados.");
    }

    #endregion

    // -----------------------------------------------------------------------
    #region Utilitarios

    public int GetAgentCount()    => legacyAgents.Count + navigators.Count;
    public int GetObstacleCount() => obstacles.Count;

    /// <summary>Devuelve la lista de agentes legacy (RVOAgentController).</summary>
    public List<RVOAgentController> GetLegacyAgents() => legacyAgents;

    /// <summary>Devuelve la lista de navigators (RVOAgentNavigator).</summary>
    public List<RVOAgentNavigator>  GetNavigators()   => navigators;

    #endregion

}
