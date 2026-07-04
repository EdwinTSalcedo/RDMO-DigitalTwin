using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Generador de Bache Cocodrilo - Enfoque Puro Voronoi (Geométrico).
/// </summary>
[ExecuteInEditMode]
public class TerrainCocodriloGenerator : MonoBehaviour
{
    [Header("General")]
    public int Seed = 42;
    public Material material;
    public bool autoUpdate = false;

    [Header("Dimensiones Aleatorias")]
    public float minWidth = 1.5f;
    public float maxWidth = 2.5f;
    public float minLength = 2.5f;
    public float maxLength = 3.5f;

    public enum MetricType { Euclidean, Chebyshev, Minkowski }

    [Header("Fractura Angular (Alligator)")]
    public MetricType distanceMetric = MetricType.Chebyshev;
    [Tooltip("Solo para Minkowski. <1 = más cóncavo, 1 = diamante, 2 = círculo.")]
    [Range(0.1f, 3.0f)] public float minkowskiP = 0.5f;
    [Tooltip("Inyecta ruido en las coordenadas antes del Voronoi para bordes 'mordidos'.")]
    [Range(0f, 1f)] public float edgeBiteAmount = 0.4f;
    [Range(20f, 100f)] public float edgeBiteScale = 50f;
    
    // Variables internas para generación (no editables)
    private float width;
    private float length;

    [Header("Calidad")]
    [Tooltip("Polígonos por eje. A mayor número, menos 'dientes de sierra', pero más costo.")]
    [Range(20, 500)] public int polygonsX = 120;
    [Range(20, 500)] public int polygonsZ = 120;

    [Header("Patrón Cocodrilo")]
    [Tooltip("Cantidad de bloques poligonales.")]
    [Range(10, 100)] public int cellCount = 40;
    
    [Tooltip("Ancho mínimo de las grietas (% del tamaño).")]
    [Range(0.01f, 2f)] public float minCrackWidthPercent = 0.5f;

    [Tooltip("Ancho máximo de las grietas (% del tamaño).")]
    [Range(0.01f, 5f)] public float maxCrackWidthPercent = 2.0f;
    
    [Tooltip("Profundidad de las grietas.")]
    [Range(0.01f, 0.4f)] public float crackDepth = 0.03f;
    
    [Tooltip("Suavidad del borde de la grieta.")]
    [Range(1f, 4f)] public float crackSmoothness = 2f;

    [Header("Variación Orgánica")]
    [Tooltip("Escala del ruido para variar el ancho (mayor = cambios más rápidos).")]
    [Range(0.1f, 10f)] public float widthNoiseScale = 2.0f;

    [Tooltip("Irregularidad de los bordes (dientes naturales).")]
    [Range(0f, 0.5f)] public float edgeIrregularity = 0.2f;

    [Tooltip("Escala del ruido de irregularidad (mayor = más detalle fino).")]
    [Range(5f, 50f)] public float irregularityScale = 20f;

    [Tooltip("Distorsión leve para que no sean líneas perfectas (0 = rectas).")]
    [Range(0f, 1f)] public float distortion = 0.1f;

    [Header("Variación de Profundidad")]
    [Tooltip("Escala del ruido de profundidad (Musgrave style).")]
    [Range(0.1f, 10f)] public float depthNoiseScale = 2.0f;
    [Tooltip("Cuánto varía la profundidad a lo largo de la grieta.")]
    [Range(0f, 1f)] public float depthVariation = 0.3f;
    [Tooltip("Suavidad del borde (SDF). 0 = abrupto, 1 = muy suave.")]
    [Range(0f, 1f)] public float edgeSmoothness = 0.2f;
    [Tooltip("Profundidad mínima del fondo (clamp) para evitar picos.")]
    [Range(0f, 0.1f)] public float minFloorDepth = 0.01f;

    [Header("Desorden de Segmentos")]
    [Tooltip("Desfase de altura aleatorio por cada bloque de asfalto.")]
    [Range(0f, 0.04f)] public float cellHeightVariation = 0.01f;
    [Tooltip("Inclinación aleatoria de los bloques.")]
    [Range(0f, 0.1f)] public float cellTiltAmount = 0.02f;

    [Header("Brutalismo y Textura")]
    [Tooltip("Serrado extra en los bordes para imitar piedras.")]
    [Range(0f, 1f)] public float edgeSerration = 0.5f;
    [Range(50f, 300f)] public float serrationScale = 150f;
    [Tooltip("Hace que las piedras resalten más.")]
    [Range(0f, 0.5f)] public float stoneHighlight = 0.1f;

    [Header("Efecto Labio y Borde")]
    [Tooltip("Altura del labio/bulto en los bordes. Positivo = hacia arriba, Negativo = hacia abajo.")]
    [Range(-0.02f, 0.02f)] public float lipHeight = 0.005f;
    [Tooltip("Ancho absoluto del área afectada (en metros) desde el borde de la grieta.")]
    [Range(0.01f, 0.2f)] public float lipWidth = 0.05f;
    [Tooltip("Cuánto se 'hunde' o redondea el borde justo antes de la grieta.")]
    [Range(0f, 0.02f)] public float edgeRoundingDepth = 0.008f;

    [Header("Capas de Detalle Surface")]
    [Tooltip("Pequeños puntos donde se saltó el material.")]
    [Range(0f, 0.01f)] public float pittingAmount = 0.002f;
    [Range(50f, 200f)] public float pittingScale = 120f;
    [Tooltip("Rugosidad micro-textural para evitar brillo plástico.")]
    [Range(0f, 0.005f)] public float microRoughness = 0.001f;
    [Range(100f, 500f)] public float microScale = 300f;

    [Header("Bordes Orgánicos")]
    [Tooltip("Distancia mínima al borde sin grietas (% del tamaño).")]
    [Range(5f, 25f)] public float bordeMinPercent = 10f;
    [Tooltip("Distancia máxima al borde (% del tamaño).")]
    [Range(10f, 35f)] public float bordeMaxPercent = 18f;
    
    [Header("Estadísticas y Debug")]
    public bool showSeeds = false;
    [SerializeField] private int vertexCount;
    [SerializeField] private int triangleCount;
    
    private float minCrackWidth;
    private float maxCrackWidth;
    private float bordeMin;
    private float bordeMax;
    private List<Vector2> currentSeeds;

    private void OnDrawGizmos()
    {
        if (!showSeeds || currentSeeds == null) return;
        
        Gizmos.color = Color.red;
        foreach (Vector2 seed in currentSeeds)
        {
            Vector3 worldPos = transform.TransformPoint(new Vector3(seed.x, 0.05f, seed.y));
            Gizmos.DrawSphere(worldPos, 0.05f); // "Cuentas" (Beads)
        }
    }

    private float CalculateVoronoiDepth(float x, float z, List<Vector2> seeds, out float vertexMask)
    {
        vertexMask = 0f;
        float origX = x;
        float origZ = z;

        // 1. Distorsión de dominio (Bite & Serration) - Ajustado para evitar 'blobbing'
        if (edgeBiteAmount > 0f || edgeSerration > 0f)
        {
            // Usamos un factor de escala más pequeño para el warp para no destruir la topología
            float b1 = Mathf.PerlinNoise(x * edgeBiteScale, z * edgeBiteScale);
            float s1 = Mathf.PerlinNoise(x * serrationScale, z * serrationScale) * edgeSerration;
            
            float combinedBite = (b1 * 0.5f + s1 * 0.5f);
            
            // Reducimos el multiplicador para mantener líneas afiladas
            float warpFactor = (edgeBiteAmount + edgeSerration) * 0.05f; 
            x += (combinedBite - 0.5f) * warpFactor;
            z += (Mathf.PerlinNoise(x * edgeBiteScale + 123f, z * edgeBiteScale + 456f) - 0.5f) * warpFactor;
        }

        if (edgeIrregularity > 0f)
        {
            float warpX = (Mathf.PerlinNoise(x * irregularityScale, z * irregularityScale) - 0.5f) * edgeIrregularity;
            float warpZ = (Mathf.PerlinNoise(x * irregularityScale + 15.3f, z * irregularityScale + 15.3f) - 0.5f) * edgeIrregularity;
            x += warpX;
            z += warpZ;
        }

        // 2. Voronoi con métricas y extracción de ID de celda
        float d1 = float.MaxValue;
        float d2 = float.MaxValue;
        Vector2 closestSeed = Vector2.zero;
        
        foreach (var seed in seeds)
        {
            float dx = Mathf.Abs(x - seed.x);
            float dz = Mathf.Abs(z - seed.y);
            float d = 0f;

            switch (distanceMetric)
            {
                case MetricType.Euclidean: d = Mathf.Sqrt(dx * dx + dz * dz); break;
                case MetricType.Chebyshev: d = Mathf.Max(dx, dz); break;
                case MetricType.Minkowski: d = Mathf.Pow(Mathf.Pow(dx, minkowskiP) + Mathf.Pow(dz, minkowskiP), 1f / minkowskiP); break;
            }

            if (d < d1) { d2 = d1; d1 = d; closestSeed = seed; }
            else if (d < d2) { d2 = d; }
        }

        float distToEdge = d2 - d1;

        // 3. Variación de celda (Tilt y Height Offset)
        float cellHash = (closestSeed.x * 123.456f + closestSeed.y * 456.789f) % 1.0f;
        float cellOffset = (cellHash - 0.5f) * cellHeightVariation;
        // Tilt simple basado en la distancia al centro de la celda
        float tiltX = (x - closestSeed.x) * (cellHash * 2f - 1f) * cellTiltAmount;
        float tiltZ = (z - closestSeed.y) * (Mathf.Cos(cellHash * 10f) * 2f - 1f) * cellTiltAmount;
        float totalCellEffect = cellOffset + tiltX + tiltZ;

        // 4. Variación de ancho
        float noise = Mathf.PerlinNoise(x * widthNoiseScale + Seed, z * widthNoiseScale + Seed);
        float currentCrackWidth = Mathf.Lerp(minCrackWidth, maxCrackWidth, noise);
        
        // Usamos SOLO el ancho real de la grieta, sin "influence radius"
        // Esto evita que grietas cercanas se fusionen visualmente

        // 5. Lip & Rounding logic
        float edgeEffect = 0f;
        float outerRadius = currentCrackWidth + lipWidth;
        
        if (distToEdge < outerRadius && distToEdge > currentCrackWidth)
        {
            float tEdge = (distToEdge - currentCrackWidth) / lipWidth;
            
            // Bulge (Upward lip)
            float bulge = Mathf.Sin(tEdge * Mathf.PI) * lipHeight;
            
            // Rounding (Downward break at the very edge)
            float rounding = -Mathf.Exp(-tEdge * 10f) * edgeRoundingDepth;
            
            edgeEffect = bulge + rounding;
        }

        // Usamos currentCrackWidth en lugar de influenceRadius para el check
        if (distToEdge > currentCrackWidth)
        {
            return totalCellEffect + edgeEffect; 
        }

        // 6. Profundidad con SDF suave (Smooth Distance Field)
        float dNoise = Mathf.PerlinNoise(x * depthNoiseScale + Seed + 500f, z * depthNoiseScale + Seed + 500f);
        float depthMod = Mathf.Lerp(1f - depthVariation, 1f, dNoise);
        
        // Normalizamos la distancia respecto al ancho real de la grieta
        float t = distToEdge / currentCrackWidth;
        
        // SDF Profile con SmoothStep SOLO dentro del ancho de la grieta
        float profile = 0f;
        if (distToEdge <= currentCrackWidth)
        {
            // Dentro de la grieta: profundidad completa con transición suave
            float innerT = distToEdge / currentCrackWidth;
            profile = Mathf.SmoothStep(1f, 0f, innerT - edgeSmoothness);
        }
        
        // Aplicamos la profundidad con clamp en el fondo para uniformidad
        float rawDepth = crackDepth * profile * depthMod;
        float finalDepth = Mathf.Max(rawDepth, profile > 0.5f ? minFloorDepth : 0f);

        // 7. Roughness y Detail
        float detail = 0f;
        if (pittingAmount > 0f || microRoughness > 0f)
        {
            float stoneNoise = Mathf.PerlinNoise(origX * microScale, origZ * microScale);
            float p = Mathf.PerlinNoise(origX * pittingScale + 77f, origZ * pittingScale + 77f);
            
            float edgeBoost = Mathf.Lerp(1.5f, 4f, 1f - t); 
            if (p > (0.88f / edgeBoost)) detail -= (p - (0.88f / edgeBoost)) * pittingAmount * 20f;
            
            detail += (stoneNoise - 0.5f) * microRoughness;
            if (stoneNoise > 0.7f) detail += (stoneNoise - 0.7f) * stoneHighlight; 
        }

        // Contrasted AO
        vertexMask = profile; 
        return (totalCellEffect - finalDepth + detail + edgeEffect);
    }

    private void OnValidate()
    {
#if UNITY_EDITOR
        if (!autoUpdate) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (this == null || this.gameObject == null) return;
        
        EditorApplication.delayCall -= DelayedGenerate;
        EditorApplication.delayCall += DelayedGenerate;
#endif
    }

#if UNITY_EDITOR
    private void DelayedGenerate()
    {
        if (this == null || this.gameObject == null) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        
        try { GenerateIntegratedMesh(); }
        catch (System.Exception e) { Debug.LogWarning($"TerrainCocodriloGenerator: {e.Message}"); }
    }
#endif

    private void Awake()
    {
        if (Application.isPlaying)
        {
            Seed = Random.Range(0, 1000000);
            GenerateIntegratedMesh();
        }
    }

    [ContextMenu("Generar Malla")]
    public void GenerateIntegratedMesh()
    {
        Random.InitState(Seed);
        
        // Determinar dimensiones basadas en la semilla
        width = Random.Range(minWidth, maxWidth);
        length = Random.Range(minLength, maxLength);

        float avgSize = (width + length) * 0.5f;
        minCrackWidth = avgSize * (minCrackWidthPercent / 100f);
        maxCrackWidth = avgSize * (maxCrackWidthPercent / 100f);
        bordeMin = avgSize * (bordeMinPercent / 100f);
        bordeMax = avgSize * (bordeMaxPercent / 100f);

        Transform existing = transform.Find("CocodriloMesh");
        if (existing != null)
        {
            if (Application.isPlaying) Destroy(existing.gameObject);
            else DestroyImmediate(existing.gameObject);
        }

        GameObject meshObj = new GameObject("CocodriloMesh");
        meshObj.transform.SetParent(this.transform, false);
        meshObj.transform.localPosition = Vector3.zero;
        meshObj.layer = 7;

        MeshFilter mf = meshObj.AddComponent<MeshFilter>();
        MeshRenderer mr = meshObj.AddComponent<MeshRenderer>();
        MeshCollider mc = meshObj.AddComponent<MeshCollider>();
        
        if (material != null) mr.sharedMaterial = material;
        
        // Generar semillas Voronoi
        currentSeeds = new List<Vector2>();
        for (int i = 0; i < cellCount; i++)
        {
            currentSeeds.Add(new Vector2(
                Random.Range(-width * 0.6f, width * 0.6f),
                Random.Range(-length * 0.6f, length * 0.6f)
            ));
        }

        Mesh mesh = GenerateMesh(currentSeeds);
        mesh.name = "Proc_Croc_Voronoi";
        
        vertexCount = mesh.vertexCount;
        triangleCount = mesh.triangles.Length / 3;
        
        mf.sharedMesh = mesh;
        mc.sharedMesh = mesh;
    }

    private Mesh GenerateMesh(List<Vector2> seeds)
    {
        Mesh mesh = new Mesh();
        
        float stepX = width / polygonsX;
        float stepZ = length / polygonsZ;
        float halfW = width * 0.5f;
        float halfL = length * 0.5f;

        // Precalcular bordes irregulares
        float noiseScale = 2f;
        float noiseOffX = Random.Range(0f, 100f);
        
        float[] bordeIzq = new float[polygonsZ + 1];
        float[] bordeDer = new float[polygonsZ + 1];
        float[] bordeDet = new float[polygonsX + 1];
        float[] bordeFre = new float[polygonsX + 1];

        for (int z = 0; z <= polygonsZ; z++)
        {
            float t = (float)z / polygonsZ;
            bordeIzq[z] = Mathf.Lerp(bordeMin, bordeMax, Mathf.PerlinNoise(0.1f + noiseOffX, t * noiseScale));
            bordeDer[z] = Mathf.Lerp(bordeMin, bordeMax, Mathf.PerlinNoise(10.73f + noiseOffX, t * noiseScale));
        }
        
        for (int x = 0; x <= polygonsX; x++)
        {
            float t = (float)x / polygonsX;
            bordeDet[x] = Mathf.Lerp(bordeMin, bordeMax, Mathf.PerlinNoise(t * noiseScale + noiseOffX, 0.3f));
            bordeFre[x] = Mathf.Lerp(bordeMin, bordeMax, Mathf.PerlinNoise(t * noiseScale + noiseOffX, 15.41f));
        }

        Vector3[] vertices = new Vector3[(polygonsX + 1) * (polygonsZ + 1)];
        Color[] colors = new Color[vertices.Length];
        int index = 0;

        for (int z = 0; z <= polygonsZ; z++)
        {
            float localZ = z * stepZ;
            float worldZ = localZ - halfL;
            
            for (int x = 0; x <= polygonsX; x++)
            {
                float localX = x * stepX;
                float worldX = localX - halfW;

                // 1. Calcular máscara de borde del bache completo
                float dLeft = localX - bordeIzq[z];
                float dRight = (width - bordeDer[z]) - localX;
                float dBack = localZ - bordeDet[x];
                float dFront = (length - bordeFre[x]) - localZ;
                float minD = Mathf.Min(dLeft, dRight, dBack, dFront);
                
                float borderFactor = 0f;
                if (minD > 0f)
                {
                    float t = Mathf.Clamp01(minD / (Mathf.Max(width, length) * 0.05f));
                    borderFactor = Mathf.SmoothStep(0f, 1f, t);
                }

                // 2. Calcular profundidad de grieta Voronoi
                float heightOffset = 0f;
                float vMask = 0f;
                if (borderFactor > 0.01f)
                {
                    heightOffset = CalculateVoronoiDepth(worldX, worldZ, seeds, out vMask);
                }

                // 3. Aplicar altura con CLAMP para evitar picos en intersecciones
                float finalHeight = heightOffset * borderFactor;
                // CRITICAL: Limitar la profundidad máxima para evitar que se acumulen múltiples grietas
                finalHeight = Mathf.Max(finalHeight, -crackDepth * 1.2f);
                
                vertices[index] = new Vector3(worldX, finalHeight, worldZ);
                
                // Color: R = Dirt/AO (profundidad), G = Micro Detail, B = Lip Mask factor
                float dirt = vMask * borderFactor;
                colors[index] = new Color(dirt, 0.5f + (heightOffset * 2f), 0f, 1f);
                index++;
            }
        }

        // Generar triángulos
        int[] triangles = new int[polygonsX * polygonsZ * 6];
        int ti = 0;
        
        for (int z = 0; z < polygonsZ; z++)
        {
            for (int x = 0; x < polygonsX; x++)
            {
                int v = z * (polygonsX + 1) + x;
                triangles[ti++] = v;
                triangles[ti++] = v + polygonsX + 1;
                triangles[ti++] = v + 1;
                triangles[ti++] = v + 1;
                triangles[ti++] = v + polygonsX + 1;
                triangles[ti++] = v + polygonsX + 2;
            }
        }

        mesh.vertices = vertices;
        mesh.colors = colors;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        
        return mesh;
    }

 
}