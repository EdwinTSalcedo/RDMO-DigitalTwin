using UnityEngine;
using RVO;

/// <summary>
/// Script de debug para visualizar información de la simulación RVO.
/// Presiona ENTER para ver el estado de todos los agentes en consola.
/// </summary>
public class RVODebugger : MonoBehaviour
{
    [Header("Visualización")]
    [SerializeField] private bool drawPositions  = true;
    [SerializeField] private bool drawVelocities = true;
    [SerializeField] private Color positionColor = Color.green;
    [SerializeField] private Color velocityColor = Color.blue;
    [SerializeField] private float arrowScale    = 1.5f;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Return))
            PrintRVOState();
    }

    private void OnDrawGizmos()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying) return;

        var manager = RVOSimulationManager.Instance;
        if (manager == null) return;

        // --- Agentes legacy (RVOAgentController) ---
        foreach (var agent in manager.GetLegacyAgents())
        {
            DrawAgentGizmo(agent.GetRVOAgentId(), agent.transform.position.y);
        }

        // --- Navigators (RVOAgentNavigator) ---
        foreach (var nav in manager.GetNavigators())
        {
            DrawAgentGizmo(nav.GetRVOAgentId(), nav.transform.position.y);
        }
#endif
    }

    private void DrawAgentGizmo(int rvoId, float yPos)
    {
        if (rvoId < 0) return;

        RVO.Vector2 rvoPos = Simulator.Instance.getAgentPosition(rvoId);
        RVO.Vector2 rvoVel = Simulator.Instance.getAgentVelocity(rvoId);

        Vector3 pos = new Vector3(rvoPos.x(), yPos, rvoPos.y());
        Vector3 vel = new Vector3(rvoVel.x(), 0f, rvoVel.y());

        if (drawPositions)
        {
            Gizmos.color = positionColor;
            Gizmos.DrawWireSphere(pos, 0.25f);
        }

        if (drawVelocities && vel.magnitude > 0.01f)
        {
            Gizmos.color = velocityColor;
            Gizmos.DrawLine(pos, pos + vel.normalized * arrowScale);
        }
    }

    public void PrintRVOState()
    {
        var manager = RVOSimulationManager.Instance;
        if (manager == null)
        {
            Debug.Log("[RVO DEBUG] Manager no encontrado");
            return;
        }

        Debug.Log("===== ESTADO DE SIMULACIÓN RVO =====");
        Debug.Log($"Tiempo global: {Simulator.Instance.getGlobalTime():F3}s");
        Debug.Log($"Agentes activos: {manager.GetAgentCount()}");
        Debug.Log($"Vértices de obstáculos: {Simulator.Instance.getNumObstacleVertices()}");
        Debug.Log("");

        // --- Agentes legacy ---
        int agentNum = 0;
        foreach (var agent in manager.GetLegacyAgents())
        {
            PrintAgentInfo(agent.GetRVOAgentId(), agent.gameObject.name, ref agentNum);
        }

        // --- Navigators ---
        foreach (var nav in manager.GetNavigators())
        {
            PrintAgentInfo(nav.GetRVOAgentId(), nav.gameObject.name, ref agentNum);
        }

        Debug.Log("=====================================");
    }

    private void PrintAgentInfo(int rvoId, string name, ref int agentNum)
    {
        if (rvoId < 0) return;

        RVO.Vector2 pos      = Simulator.Instance.getAgentPosition(rvoId);
        RVO.Vector2 vel      = Simulator.Instance.getAgentVelocity(rvoId);
        RVO.Vector2 prefVel  = Simulator.Instance.getAgentPrefVelocity(rvoId);
        int agentNeighbors   = Simulator.Instance.getAgentNumAgentNeighbors(rvoId);
        int obstNeighbors    = Simulator.Instance.getAgentNumObstacleNeighbors(rvoId);
        int orcaLines        = Simulator.Instance.getAgentOrcaLines(rvoId).Count;

        Debug.Log($"Agente {agentNum} ({name}):");
        Debug.Log($"  ID RVO          : {rvoId}");
        Debug.Log($"  Posición        : ({pos.x():F2}, {pos.y():F2})");
        Debug.Log($"  Velocidad       : ({vel.x():F2}, {vel.y():F2}) | mag: {RVOMath.abs(vel):F2}");
        Debug.Log($"  Vel. preferida  : ({prefVel.x():F2}, {prefVel.y():F2}) | mag: {RVOMath.abs(prefVel):F2}");
        Debug.Log($"  Vecinos agentes : {agentNeighbors}");
        Debug.Log($"  Vecinos obstác. : {obstNeighbors}");
        Debug.Log($"  Líneas ORCA     : {orcaLines}");
        Debug.Log("");

        agentNum++;
    }
}
