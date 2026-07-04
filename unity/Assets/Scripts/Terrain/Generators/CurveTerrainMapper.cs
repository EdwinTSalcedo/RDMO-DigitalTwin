using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Genera datos de superficie curva proceduralmente para generadores de terreno.
/// Este componente CREA la geometría curva, no mapea una existente.
/// </summary>
[DisallowMultipleComponent]
public class CurveTerrainMapper : MonoBehaviour
{
    [Header("Configuración de Área")]
    [Tooltip("Tamaño del área de generación (metros)")]
    public float areaSize = 10f;
    
    [Tooltip("Resolución del mapa de altura (puntos por metro)")]
    [Range(1, 10)]
    public int resolution = 2;

    [Header("Configuración de Curvatura")]
    [Tooltip("Tipo de curvatura a generar")]
    public CurveType curveType = CurveType.Wave;
    
    [Tooltip("Amplitud de la curvatura (altura máxima)")]
    [Range(0f, 5f)]
    public float amplitude = 1f;
    
    [Tooltip("Frecuencia de ondulación")]
    [Range(0.1f, 5f)]
    public float frequency = 1f;
    
    [Tooltip("Semilla para generación procedural")]
    public int seed = 12345;

    [Header("Detección de Zonas Planas")]
    [Tooltip("Ángulo máximo para considerar una zona como plana (grados)")]
    [Range(0f, 45f)]
    public float flatAngleThreshold = 15f;

    public enum CurveType
    {
        Flat,           // Completamente plano
        Wave,           // Ondulación sinusoidal
        Hills,          // Colinas suaves
        Noise,          // Ruido Perlin
        Custom          // Personalizado
    }

    // Datos generados
    private Dictionary<Vector2Int, TerrainPoint> heightMap;
    private bool isGenerated = false;

    /// <summary>
    /// Estructura que contiene información de un punto en el terreno
    /// </summary>
    public struct TerrainPoint
    {
        public Vector3 position;
        public Vector3 normal;
        public bool isFlat;         // Si esta zona es plana (buena para baches)
        public float slope;         // Pendiente en grados
        
        public TerrainPoint(Vector3 pos, Vector3 norm, float slopeAngle)
        {
            position = pos;
            normal = norm;
            slope = slopeAngle;
            isFlat = slopeAngle < 15f; // Por defecto
        }
    }

    private void Awake()
    {
        GenerateHeightMap();
    }

    /// <summary>
    /// Genera el mapa de altura proceduralmente
    /// </summary>
    [ContextMenu("Generar Mapa de Altura")]
    public void GenerateHeightMap()
    {
        Random.InitState(seed);
        heightMap = new Dictionary<Vector2Int, TerrainPoint>();

        float halfSize = areaSize * 0.5f;
        int totalPoints = Mathf.CeilToInt(areaSize * resolution);
        float step = areaSize / totalPoints;

        Debug.Log($"[CurveTerrainMapper] Generando mapa {totalPoints}x{totalPoints} puntos...");

        // Generar altura para cada punto
        for (int z = 0; z <= totalPoints; z++)
        {
            for (int x = 0; x <= totalPoints; x++)
            {
                float worldX = transform.position.x - halfSize + (x * step);
                float worldZ = transform.position.z - halfSize + (z * step);
                
                float height = CalculateHeight(worldX, worldZ);
                Vector3 position = new Vector3(worldX, transform.position.y + height, worldZ);
                
                // Calcular normal aproximada (usando puntos vecinos)
                Vector3 normal = CalculateNormal(worldX, worldZ, step);
                
                // Calcular pendiente
                float slope = Vector3.Angle(Vector3.up, normal);
                
                TerrainPoint point = new TerrainPoint(position, normal, slope);
                point.isFlat = slope < flatAngleThreshold;
                
                heightMap[new Vector2Int(x, z)] = point;
            }
        }

        isGenerated = true;
        Debug.Log($"[CurveTerrainMapper] Mapa generado: {heightMap.Count} puntos");
    }

    /// <summary>
    /// Calcula la altura en una posición específica según el tipo de curvatura
    /// </summary>
    private float CalculateHeight(float worldX, float worldZ)
    {
        float localX = worldX - transform.position.x;
        float localZ = worldZ - transform.position.z;

        switch (curveType)
        {
            case CurveType.Flat:
                return 0f;

            case CurveType.Wave:
                // Ondulación sinusoidal en dirección Z
                return Mathf.Sin(localZ * frequency) * amplitude;

            case CurveType.Hills:
                // Colinas usando seno en ambas direcciones
                float hillX = Mathf.Sin(localX * frequency * 0.5f);
                float hillZ = Mathf.Sin(localZ * frequency * 0.5f);
                return (hillX + hillZ) * 0.5f * amplitude;

            case CurveType.Noise:
                // Ruido Perlin para terreno orgánico
                float noiseX = localX * frequency * 0.1f + seed;
                float noiseZ = localZ * frequency * 0.1f + seed;
                return Mathf.PerlinNoise(noiseX, noiseZ) * amplitude * 2f - amplitude;

            default:
                return 0f;
        }
    }

    /// <summary>
    /// Calcula la normal en un punto usando diferencias finitas
    /// </summary>
    private Vector3 CalculateNormal(float worldX, float worldZ, float step)
    {
        float heightL = CalculateHeight(worldX - step, worldZ);
        float heightR = CalculateHeight(worldX + step, worldZ);
        float heightD = CalculateHeight(worldX, worldZ - step);
        float heightU = CalculateHeight(worldX, worldZ + step);

        Vector3 tangentX = new Vector3(step * 2f, heightR - heightL, 0f);
        Vector3 tangentZ = new Vector3(0f, heightU - heightD, step * 2f);

        return Vector3.Cross(tangentZ, tangentX).normalized;
    }

    /// <summary>
    /// Obtiene el punto del terreno en una posición mundial
    /// </summary>
    public TerrainPoint GetPoint(float worldX, float worldZ)
    {
        if (!isGenerated)
        {
            GenerateHeightMap();
        }

        // Convertir a coordenadas de grid
        float halfSize = areaSize * 0.5f;
        int totalPoints = Mathf.CeilToInt(areaSize * resolution);
        float step = areaSize / totalPoints;

        float localX = worldX - (transform.position.x - halfSize);
        float localZ = worldZ - (transform.position.z - halfSize);

        int gridX = Mathf.RoundToInt(localX / step);
        int gridZ = Mathf.RoundToInt(localZ / step);

        // Clamp a límites
        gridX = Mathf.Clamp(gridX, 0, totalPoints);
        gridZ = Mathf.Clamp(gridZ, 0, totalPoints);

        Vector2Int key = new Vector2Int(gridX, gridZ);

        if (heightMap.ContainsKey(key))
        {
            return heightMap[key];
        }

        // Fallback: calcular en tiempo real
        float height = CalculateHeight(worldX, worldZ);
        Vector3 position = new Vector3(worldX, transform.position.y + height, worldZ);
        Vector3 normal = CalculateNormal(worldX, worldZ, step);
        float slope = Vector3.Angle(Vector3.up, normal);
        
        TerrainPoint point = new TerrainPoint(position, normal, slope);
        point.isFlat = slope < flatAngleThreshold;
        
        return point;
    }

    /// <summary>
    /// Obtiene todos los puntos planos (buenos para colocar baches)
    /// </summary>
    public List<TerrainPoint> GetFlatPoints()
    {
        if (!isGenerated)
        {
            GenerateHeightMap();
        }

        List<TerrainPoint> flatPoints = new List<TerrainPoint>();

        foreach (var kvp in heightMap)
        {
            if (kvp.Value.isFlat)
            {
                flatPoints.Add(kvp.Value);
            }
        }

        return flatPoints;
    }

    /// <summary>
    /// Obtiene todos los puntos del mapa
    /// </summary>
    public List<TerrainPoint> GetAllPoints()
    {
        if (!isGenerated)
        {
            GenerateHeightMap();
        }

        return new List<TerrainPoint>(heightMap.Values);
    }

    /// <summary>
    /// Limpia el mapa generado
    /// </summary>
    public void ClearMap()
    {
        if (heightMap != null)
        {
            heightMap.Clear();
        }
        isGenerated = false;
    }

    /// <summary>
    /// Visualización en el editor
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (!isGenerated || heightMap == null) return;

        // Dibujar puntos del mapa
        foreach (var kvp in heightMap)
        {
            TerrainPoint point = kvp.Value;
            
            // Color según si es plano o no
            Gizmos.color = point.isFlat ? Color.green : Color.red;
            Gizmos.DrawSphere(point.position, 0.05f);
            
            // Dibujar normal
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(point.position, point.position + point.normal * 0.3f);
        }

        // Dibujar límites del área
        Gizmos.color = Color.yellow;
        float halfSize = areaSize * 0.5f;
        Vector3 center = transform.position;
        Vector3 size = new Vector3(areaSize, 0.1f, areaSize);
        Gizmos.DrawWireCube(center, size);
    }
}
