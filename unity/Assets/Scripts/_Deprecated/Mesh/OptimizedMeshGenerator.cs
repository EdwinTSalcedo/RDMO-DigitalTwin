using UnityEngine;

public class OptimizedMeshGenerator : MonoBehaviour 
{
    [Header("Configuración")]
    public Texture2D heightMap;
    public Material meshMaterial;
    [Tooltip("Tamaño de cada píxel en unidades del mundo")]
    public float pixelSize = 0.5f;
    [Tooltip("Nivel de detalle (0.1 = bajo, 1 = alto)")]
    [Range(0.1f, 1f)] public float detailLevel = 0.2f;

    void Start()
    {
        GenerateMesh();
    }

    void GenerateMesh()
    {
        // Calcular paso de muestreo basado en el nivel de detalle
        int step = Mathf.Clamp(Mathf.RoundToInt(10f / detailLevel), 1, 64);

        // Crear arrays directamente con tamaño precalculado
        int width = heightMap.width / step;
        int height = heightMap.height / step;
        int vertexCount = width * height;
        int triangleCount = (width - 1) * (height - 1) * 6;

        Vector3[] vertices = new Vector3[vertexCount];
        Vector2[] uv = new Vector2[vertexCount];
        int[] triangles = new int[triangleCount];

        // Calcular centro
        float centerX = (width - 1) * pixelSize * 0.5f;
        float centerZ = (height - 1) * pixelSize * 0.5f;

        // Generar vértices (optimizado sin usar bucles anidados)
        for (int i = 0, z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++, i++)
            {
                float xPos = x * pixelSize - centerX;
                float zPos = z * pixelSize - centerZ;
                vertices[i] = new Vector3(xPos, 0, zPos);
                uv[i] = new Vector2((float)x / width, (float)z / height);
            }
        }

        // Generar triángulos (patrón predecible)
        for (int ti = 0, vi = 0, z = 0; z < height - 1; z++, vi++)
        {
            for (int x = 0; x < width - 1; x++, ti += 6, vi++)
            {
                triangles[ti] = vi;
                triangles[ti + 1] = vi + width;
                triangles[ti + 2] = vi + 1;
                triangles[ti + 3] = vi + 1;
                triangles[ti + 4] = vi + width;
                triangles[ti + 5] = vi + width + 1;
            }
        }

        // Crear y asignar malla
        Mesh mesh = new Mesh
        {
            vertices = vertices,
            uv = uv,
            triangles = triangles
        };
        mesh.RecalculateNormals();

        GetComponent<MeshFilter>().mesh = mesh;
        GetComponent<MeshRenderer>().material = meshMaterial;
    }
}