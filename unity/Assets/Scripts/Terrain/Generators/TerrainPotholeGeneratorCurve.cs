using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(CurveTerrainMapper))]
public class TerrainPotholeGeneratorCurve : MonoBehaviour
{
    [Header("Configuración Simple")]
    [Tooltip("Cantidad de baches a generar")]
    public int cantidadBaches = 10;
    
    [Tooltip("Material para los baches")]
    public Material bacheMaterial;
    
    [Header("Configuración de Bache")]
    [Tooltip("Tamaño de cada bache (metros)")]
    [Range(0.3f, 2f)]
    public float bacheSize = 0.8f;
    
    [Tooltip("Profundidad del bache (metros)")]
    [Range(0.05f, 0.3f)]
    public float bacheDepth = 0.1f;
    
    [Tooltip("Resolución de la malla del bache")]
    [Range(10, 50)]
    public int meshResolution = 20;

    [Header("Estado")]
    public bool generacionTerminada = false;

    private CurveTerrainMapper terrainMapper;
    private List<GameObject> baches = new List<GameObject>();

    private void Awake()
    {
        terrainMapper = GetComponent<CurveTerrainMapper>();
    }

    [ContextMenu("Generar Baches")]
    public void Generate()
    {
        Debug.Log("[TerrainPotholeGeneratorCurve] === INICIO DE GENERACIÓN ===");
        
        if (terrainMapper == null)
        {
            Debug.LogError("[TerrainPotholeGeneratorCurve] No se encontró CurveTerrainMapper!");
            return;
        }

        // Limpiar baches anteriores
        ClearBaches();
        
        Debug.Log("[TerrainPotholeGeneratorCurve] Obteniendo puntos planos del mapper...");
        
        // Obtener puntos planos del mapper
        List<CurveTerrainMapper.TerrainPoint> flatPoints = terrainMapper.GetFlatPoints();
        
        Debug.Log($"[TerrainPotholeGeneratorCurve] Puntos planos encontrados: {flatPoints.Count}");
        
        if (flatPoints.Count == 0)
        {
            Debug.LogWarning("[TerrainPotholeGeneratorCurve] No hay puntos planos disponibles. Ajusta el ángulo de detección o cambia el tipo de curvatura.");
            Debug.LogWarning($"[TerrainPotholeGeneratorCurve] Configuración actual - Curve Type: {terrainMapper.curveType}, Flat Angle: {terrainMapper.flatAngleThreshold}°");
            return;
        }

        if (bacheMaterial == null)
        {
            Debug.LogError("[TerrainPotholeGeneratorCurve] ¡No hay material asignado! Asigna un material en el Inspector.");
            return;
        }

        Debug.Log($"[TerrainPotholeGeneratorCurve] Generando {cantidadBaches} baches en {flatPoints.Count} puntos planos disponibles");

        int generated = 0;
        int attempts = 0;
        int maxAttempts = cantidadBaches * 50;

        List<Vector3> posiciones = new List<Vector3>();

        while (generated < cantidadBaches && attempts < maxAttempts)
        {
            attempts++;

            // Seleccionar punto plano aleatorio
            CurveTerrainMapper.TerrainPoint randomPoint = flatPoints[Random.Range(0, flatPoints.Count)];

            // Verificar que no se solape con otros baches
            bool tooClose = false;
            foreach (Vector3 existingPos in posiciones)
            {
                float distance = Vector3.Distance(new Vector3(randomPoint.position.x, 0, randomPoint.position.z), 
                                                   new Vector3(existingPos.x, 0, existingPos.z));
                if (distance < bacheSize * 1.5f)
                {
                    tooClose = true;
                    break;
                }
            }

            if (tooClose) continue;

            // Crear el bache
            GameObject bache = GenerateBache(randomPoint, generated);
            if (bache != null)
            {
                baches.Add(bache);
                posiciones.Add(randomPoint.position);
                generated++;
                Debug.Log($"[TerrainPotholeGeneratorCurve] Bache {generated} creado en {randomPoint.position}");
            }
            else
            {
                Debug.LogWarning($"[TerrainPotholeGeneratorCurve] Fallo al crear bache en intento {attempts}");
            }
        }

        generacionTerminada = true;
        Debug.Log($"[TerrainPotholeGeneratorCurve] Generación completada: {generated}/{cantidadBaches} baches creados");
    }

    private GameObject GenerateBache(CurveTerrainMapper.TerrainPoint centerPoint, int index)
    {
        GameObject bacheObj = new GameObject($"Bache_Curve_{index}");
        bacheObj.transform.SetParent(transform);
        bacheObj.transform.position = centerPoint.position;
        bacheObj.layer = 7;
        bacheObj.tag = "Pothole";

        // Generar malla del bache
        Mesh bacheMesh = CreateBacheMesh(centerPoint, bacheObj);
        
        // Configurar componentes
        MeshFilter mf = bacheObj.AddComponent<MeshFilter>();
        mf.sharedMesh = bacheMesh;

        MeshRenderer mr = bacheObj.AddComponent<MeshRenderer>();
        mr.sharedMaterial = bacheMaterial;

        MeshCollider mc = bacheObj.AddComponent<MeshCollider>();
        mc.sharedMesh = bacheMesh;

        return bacheObj;
    }

    private Mesh CreateBacheMesh(CurveTerrainMapper.TerrainPoint centerPoint, GameObject bacheObj)
    {
        Mesh mesh = new Mesh();
        mesh.name = "Bache_Curve_Mesh";

        int resolution = meshResolution;
        float halfSize = bacheSize * 0.5f;
        float step = bacheSize / resolution;

        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();

        // Generar vértices proyectados sobre el terreno
        for (int z = 0; z <= resolution; z++)
        {
            for (int x = 0; x <= resolution; x++)
            {
                // Posición local del vértice
                float localX = -halfSize + (x * step);
                float localZ = -halfSize + (z * step);

                // Posición mundial del vértice
                float worldX = centerPoint.position.x + localX;
                float worldZ = centerPoint.position.z + localZ;

                // Obtener punto del terreno generado
                CurveTerrainMapper.TerrainPoint vertexPoint = terrainMapper.GetPoint(worldX, worldZ);

                // Calcular profundidad del bache (más profundo en el centro)
                float distanceFromCenter = Mathf.Sqrt(localX * localX + localZ * localZ);
                float normalizedDist = Mathf.Clamp01(distanceFromCenter / halfSize);
                
                // Perfil suave del bache (más profundo en el centro)
                float depthFactor = 1f - (normalizedDist * normalizedDist);
                float depth = bacheDepth * depthFactor;

                // Aplicar profundidad a lo largo de la normal del terreno
                Vector3 vertexPos = vertexPoint.position - vertexPoint.normal * depth;

                // Convertir a espacio local del GameObject
                vertices.Add(bacheObj.transform.InverseTransformPoint(vertexPos));
                
                // UVs
                uvs.Add(new Vector2((float)x / resolution, (float)z / resolution));
            }
        }

        // Generar triángulos
        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                int i = z * (resolution + 1) + x;
                
                // Triángulo 1
                triangles.Add(i);
                triangles.Add(i + resolution + 1);
                triangles.Add(i + 1);
                
                // Triángulo 2
                triangles.Add(i + 1);
                triangles.Add(i + resolution + 1);
                triangles.Add(i + resolution + 2);
            }
        }

        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    private void ClearBaches()
    {
        foreach (GameObject bache in baches)
        {
            if (bache != null)
            {
                if (Application.isPlaying)
                    Destroy(bache);
                else
                    DestroyImmediate(bache);
            }
        }
        baches.Clear();
        generacionTerminada = false;
    }

    private void OnDisable()
    {
        ClearBaches();
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(TerrainPotholeGeneratorCurve))]
public class TerrainPotholeGeneratorCurveEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        TerrainPotholeGeneratorCurve generator = (TerrainPotholeGeneratorCurve)target;
        
        GUILayout.Space(10);
        
        if (GUILayout.Button("🔨 GENERAR BACHES", GUILayout.Height(40)))
        {
            generator.Generate();
        }
        
        if (generator.generacionTerminada)
        {
            EditorGUILayout.HelpBox("✅ Generación completada", MessageType.Info);
        }
    }
}
#endif
