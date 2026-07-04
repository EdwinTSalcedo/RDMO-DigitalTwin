using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(TerrainPotholeGeneratorCurve))]
[RequireComponent(typeof(CurveTerrainMapper))]
public class GeneradorDeCalleCurve : MonoBehaviour
{
    [Header("Configuración Simple")]
    [Tooltip("Material de la calle")]
    public Material calleMaterial;
    
    [Tooltip("Altura de la calle sobre el terreno")]
    [Range(0f, 0.1f)]
    public float alturaOffset = 0.01f;

    [Tooltip("Resolución de la malla de calle")]
    [Range(10, 100)]
    public int meshResolution = 40;

    private TerrainPotholeGeneratorCurve generadorBaches;
    private CurveTerrainMapper terrainMapper;
    private GameObject calleObject;

    private void Awake()
    {
        generadorBaches = GetComponent<TerrainPotholeGeneratorCurve>();
        terrainMapper = GetComponent<CurveTerrainMapper>();
    }

    [ContextMenu("Generar Calle")]
    public void Generate()
    {
        if (terrainMapper == null)
        {
            Debug.LogError("[GeneradorDeCalleCurve] No se encontró CurveTerrainMapper!");
            return;
        }

        if (generadorBaches == null || !generadorBaches.generacionTerminada)
        {
            Debug.LogWarning("[GeneradorDeCalleCurve] Esperando a que termine la generación de baches...");
            return;
        }

        // Limpiar calle anterior
        ClearCalle();

        Debug.Log("[GeneradorDeCalleCurve] Generando calle curveada...");

        // Crear objeto de calle
        calleObject = new GameObject("Calle_Curve");
        calleObject.transform.SetParent(transform);
        calleObject.transform.position = transform.position;
        calleObject.layer = 7;

        // Generar malla de calle
        Mesh calleMesh = CreateCalleMesh();

        // Configurar componentes
        MeshFilter mf = calleObject.AddComponent<MeshFilter>();
        mf.sharedMesh = calleMesh;

        MeshRenderer mr = calleObject.AddComponent<MeshRenderer>();
        mr.sharedMaterial = calleMaterial;

        MeshCollider mc = calleObject.AddComponent<MeshCollider>();
        mc.sharedMesh = calleMesh;

        Debug.Log("[GeneradorDeCalleCurve] Calle generada exitosamente");
    }

    private Mesh CreateCalleMesh()
    {
        Mesh mesh = new Mesh();
        mesh.name = "Calle_Curve_Mesh";
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; // Soporte para más vértices

        float areaSize = terrainMapper.areaSize;
        int resolution = meshResolution;
        float halfSize = areaSize * 0.5f;
        float step = areaSize / resolution;

        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();

        // Obtener bounds de todos los baches para recortarlos
        List<Bounds> bacheBounds = GetBacheBounds();

        // Generar vértices usando datos del terreno generado
        for (int z = 0; z <= resolution; z++)
        {
            for (int x = 0; x <= resolution; x++)
            {
                // Posición local
                float localX = -halfSize + (x * step);
                float localZ = -halfSize + (z * step);

                // Posición mundial
                float worldX = transform.position.x + localX;
                float worldZ = transform.position.z + localZ;

                // Obtener punto del terreno generado
                CurveTerrainMapper.TerrainPoint terrainPoint = terrainMapper.GetPoint(worldX, worldZ);

                // Elevar ligeramente sobre el terreno
                Vector3 vertexPos = terrainPoint.position + terrainPoint.normal * alturaOffset;

                // Convertir a espacio local
                vertices.Add(calleObject.transform.InverseTransformPoint(vertexPos));
                
                // UVs con tiling
                uvs.Add(new Vector2(localX * 0.5f, localZ * 0.5f));
            }
        }

        // Generar triángulos (evitando áreas de baches)
        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                int i = z * (resolution + 1) + x;

                // Verificar si este quad intersecta con algún bache
                Vector3 quadCenter = (vertices[i] + vertices[i + resolution + 2]) * 0.5f;
                Vector3 worldQuadCenter = calleObject.transform.TransformPoint(quadCenter);

                bool intersectsBache = false;
                foreach (Bounds bounds in bacheBounds)
                {
                    if (bounds.Contains(worldQuadCenter))
                    {
                        intersectsBache = true;
                        break;
                    }
                }

                // Solo agregar triángulos si no intersecta con baches
                if (!intersectsBache)
                {
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
        }

        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    private List<Bounds> GetBacheBounds()
    {
        List<Bounds> bounds = new List<Bounds>();

        if (generadorBaches == null) return bounds;

        // Obtener todos los renderers de baches
        Renderer[] bacheRenderers = generadorBaches.GetComponentsInChildren<Renderer>();
        
        foreach (Renderer renderer in bacheRenderers)
        {
            if (renderer.gameObject.CompareTag("Pothole"))
            {
                // Expandir bounds ligeramente para asegurar separación
                Bounds expandedBounds = renderer.bounds;
                expandedBounds.Expand(0.2f);
                bounds.Add(expandedBounds);
            }
        }

        return bounds;
    }

    private void ClearCalle()
    {
        if (calleObject != null)
        {
            if (Application.isPlaying)
                Destroy(calleObject);
            else
                DestroyImmediate(calleObject);
            
            calleObject = null;
        }
    }

    private void OnDisable()
    {
        ClearCalle();
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(GeneradorDeCalleCurve))]
public class GeneradorDeCalleCurveEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        GeneradorDeCalleCurve generator = (GeneradorDeCalleCurve)target;
        
        GUILayout.Space(10);
        
        if (GUILayout.Button("🛣️ GENERAR CALLE", GUILayout.Height(40)))
        {
            generator.Generate();
        }
    }
}
#endif
