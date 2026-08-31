using UnityEngine;

/// <summary>
/// Marca una superficie como "walkable" (caminable) o tipo de terreno.
/// Permite al agente saber dónde puede caminar y recibir eventos.
/// </summary>
public class RVOTerrainDetector : MonoBehaviour
{
    [System.Flags]
    public enum TerrainType
    {
        None = 0,
        Ground = 1,           // Terreno plano normal
        Grass = 2,            // Pasto (más lento)
        Water = 4,            // Agua (impasable)
        Mud = 8,              // Fango (más lento)
        Stone = 16,           // Piedra (normal)
        Ice = 32,             // Hielo (resbaladizo)
        Lava = 64,            // Lava (mortal)
        Sand = 128           // Arena (más lento)
    }

    [Header("Configuración de Terreno")]
    [SerializeField] private TerrainType terrainType = TerrainType.Ground;
    [SerializeField] private bool isWalkable = true;
    [SerializeField] private float speedMultiplier = 1.0f; // 0.5 = mitad de velocidad
    [SerializeField] private bool isDangerous = false; // Causa daño
    [SerializeField] private float damagePerSecond = 0f;
    
    [Header("Detección")]
    [SerializeField] private Collider terrainCollider;
    [SerializeField] private LayerMask agentLayer;
    
    private Color debugColor = Color.green;
    private Collider col;
    
    private void Start()
    {
        col = GetComponent<Collider>();
        if (col == null)
        {
            Debug.LogError($"[RVO TERRAIN] {gameObject.name} necesita un Collider");
            return;
        }
        
        // Establecer color de debug basado en tipo
        debugColor = GetColorForTerrainType(terrainType);
    }
    
    /// <summary>Detecta si el agente está sobre este terreno.</summary>
    public bool IsAgentOnTerrain(RVOAgentController agent)
    {
        if (!isWalkable) return false;
        
        Vector3 agentPos = agent.transform.position;
        
        // Evitar error con MeshCollider no convexo
        try
        {
            Vector3 closestPoint = col.ClosestPoint(agentPos);
            float distance = Vector3.Distance(agentPos, closestPoint);
            return distance < 0.1f; // Margen de 0.1 unidades
        }
        catch
        {
            // Si falla ClosestPoint, usar distancia simple
            Vector3 terrainCenter = col.bounds.center;
            float distance = Vector3.Distance(agentPos, terrainCenter);
            return distance < 5f; // Margen más grande como fallback
        }
    }
    
    /// <summary>Obtiene el modificador de velocidad para este terreno.</summary>
    public float GetSpeedModifier()
    {
        return isWalkable ? speedMultiplier : 0f;
    }
    
    /// <summary>Determina si el agente puede estar en este terreno.</summary>
    public bool CanAgentWalk()
    {
        return isWalkable;
    }
    
    /// <summary>Devuelve si este terreno causa daño.</summary>
    public bool IsDangerous() => isDangerous;
    
    /// <summary>Devuelve daño por segundo si aplica.</summary>
    public float GetDamagePerSecond() => isDangerous ? damagePerSecond : 0f;
    
    /// <summary>Obtiene el tipo de terreno.</summary>
    public TerrainType GetTerrainType() => terrainType;
    
    /// <summary>Ottiene color visual para debugging.</summary>
    public Color GetDebugColor() => debugColor;
    
    private Color GetColorForTerrainType(TerrainType type)
    {
        return type switch
        {
            TerrainType.Ground => Color.green,
            TerrainType.Grass => new Color(0.2f, 0.8f, 0.2f),
            TerrainType.Water => Color.blue,
            TerrainType.Mud => new Color(0.6f, 0.4f, 0.2f),
            TerrainType.Stone => Color.gray,
            TerrainType.Ice => Color.cyan,
            TerrainType.Lava => new Color(1f, 0.5f, 0f),
            TerrainType.Sand => new Color(1f, 0.9f, 0.5f),
            _ => Color.white
        };
    }
    
    private void OnDrawGizmos()
    {
        #if UNITY_EDITOR
        if (!Application.isPlaying) return;
        
        Collider c = GetComponent<Collider>();
        if (c != null)
        {
            Gizmos.color = debugColor;
            Gizmos.DrawWireCube(c.bounds.center, c.bounds.size);
        }
        #endif
    }
}
