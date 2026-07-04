using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Generador de Rajaduras (Grietas) Premium.
/// Tiene paridad de características con el generador de Cocodrilo (Lips, Pitting, Micro-detail).
/// </summary>
[ExecuteInEditMode]
public class TerrainRajaduraGenerator : MonoBehaviour
{
    [Header("General")]
    public int Seed = 42;
    public Material material;
    public bool autoUpdate = false;

    [Header("Dimensiones Aleatorias")]
    public float minWidth = 0.5f; 
    public float maxWidth = 0.8f;
    public float minLength = 2.5f;
    public float maxLength = 4.0f;

    [Header("Rotación")]
    public bool randomizeRotation = true;
    [Range(0f, 360f)] public float minRotation = 0f;
    [Range(0f, 360f)] public float maxRotation = 360f;
    
    [SerializeField, HideInInspector] 
    private float currentRotation = 0f;

    [Header("Calidad")]
    [Tooltip("Polígonos por eje. A mayor número, más detalle pero más costo.")]
    [Range(20, 500)] public int polygonsX = 100;
    [Range(20, 500)] public int polygonsZ = 200;

    [Header("Patrón Rajadura")]
    [Tooltip("Cantidad de segmentos lógicos (bloques) a lo largo de la grieta.")]
    [Range(10, 100)] public int segmentCount = 40;
    
    [Tooltip("Ancho mínimo de las grietas (% del tamaño).")]
    [Range(0.01f, 2f)] public float minCrackWidthPercent = 0.5f;

    [Tooltip("Ancho máximo de las grietas (% del tamaño).")]
    [Range(0.01f, 5f)] public float maxCrackWidthPercent = 2.0f;
    
    [Tooltip("Profundidad de las grietas (% del tamaño promedio).")]
    [Range(0.01f, 10f)] public float crackDepthPercent = 2.5f;
    
    [Tooltip("Suavidad del borde de la grieta.")]
    [Range(1f, 4f)] public float crackSmoothness = 2f;

    [Header("Forma y Wiggle")]
    [Tooltip("Escala del zig-zag (Sinuosidad).")]
    [Range(0.1f, 10f)] public float wiggleScale = 2.5f;
    [Tooltip("Amplitud del movimiento lateral.")]
    [Range(0f, 0.5f)] public float wiggleAmplitude = 0.25f;
    [Tooltip("Inyecta ruido en las coordenadas para bordes 'mordidos'.")]
    [Range(0f, 1f)] public float edgeBiteAmount = 0.85f;
    [Range(20f, 100f)] public float edgeBiteScale = 35f;
    [Tooltip("Distorsión extra para que no sea una línea perfecta.")]
    [Range(0f, 1f)] public float distortion = 0.1f;

    [Header("Estadísticas y Debug")]
    public bool showPath = false;
    public bool showBranches = false;

    private void OnDrawGizmos()
    {
        if (showPath && pathPoints != null && pathPoints.Count >= 2)
        {
            Gizmos.color = Color.magenta;
            Quaternion rot = Quaternion.Euler(0, currentRotation, 0);

            for (int i = 0; i < pathPoints.Count - 1; i++)
            {
                Vector3 p1Local = rot * new Vector3(pathPoints[i].x, 0.05f, pathPoints[i].y);
                Vector3 p2Local = rot * new Vector3(pathPoints[i+1].x, 0.05f, pathPoints[i+1].y);
                
                Vector3 p1 = transform.TransformPoint(p1Local);
                Vector3 p2 = transform.TransformPoint(p2Local);
                Gizmos.DrawLine(p1, p2);
            }
        }

        if (showBranches && branches != null && pathPoints != null)
        {
            Gizmos.color = Color.cyan;
            Quaternion rot = Quaternion.Euler(0, currentRotation, 0);

            foreach (var branch in branches)
            {
                if (branch.startSegment < pathPoints.Count)
                {
                    Vector2 start = pathPoints[branch.startSegment];
                    Vector2 end = start + branch.direction * branch.length;
                    
                    Vector3 start3 = rot * new Vector3(start.x, 0.05f, start.y);
                    Vector3 end3 = rot * new Vector3(end.x, 0.05f, end.y);

                    Vector3 p1 = transform.TransformPoint(start3);
                    Vector3 p2 = transform.TransformPoint(end3);
                    Gizmos.DrawLine(p1, p2);
                    Gizmos.DrawSphere(p1, 0.02f);
                }
            }
        }
    }

    [Header("Variación Orgánica")]
    [Tooltip("Escala del ruido para variar el ancho.")]
    [Range(0.1f, 10f)] public float widthNoiseScale = 4.5f;
    [Tooltip("Irregularidad de los bordes.")]
    [Range(0f, 0.5f)] public float edgeIrregularity = 0.45f;
    [Tooltip("Escala del ruido de irregularidad.")]
    [Range(5f, 50f)] public float irregularityScale = 12f;

    [Header("Transiciones y Segmentación")]
    [Tooltip("Hace que los cambios de ancho sean bruscos en lugar de suaves.")]
    [Range(0f, 1f)] public float harshTransitions = 0.7f;
    [Tooltip("Cantidad de segmentos visibles (bloques de asfalto separados).")]
    [Range(0f, 1f)] public float segmentVisibility = 0.5f;
    [Tooltip("Escala de los cortes entre segmentos.")]
    [Range(5f, 50f)] public float segmentCutScale = 15f;

    [Header("Ramificaciones Secundarias")]
    [Tooltip("Probabilidad de que aparezca una ramificación (0-1).")]
    [Range(0f, 1f)] public float branchProbability = 0.4f;
    [Tooltip("Longitud mínima de las ramas (% del tamaño promedio).")]
    [Range(5f, 50f)] public float minBranchLengthPercent = 15f;
    [Tooltip("Longitud máxima de las ramas (% del tamaño promedio).")]
    [Range(10f, 100f)] public float maxBranchLengthPercent = 40f;
    [Tooltip("Ángulo de desviación de las ramas (grados).")]
    [Range(15f, 90f)] public float branchAngle = 50f;
    [Tooltip("Ancho de las ramas (% del ancho principal).")]
    [Range(0.1f, 1f)] public float branchWidthPercent = 0.7f;

    [Header("Variación de Profundidad")]
    [Tooltip("Escala del ruido de profundidad.")]
    [Range(0.1f, 10f)] public float depthNoiseScale = 2.0f;
    [Tooltip("Cuánto varía la profundidad a lo largo de la grieta.")]
    [Range(0f, 1f)] public float depthVariation = 0.3f;
    [Tooltip("Suavidad del borde (SDF).")]
    [Range(0f, 1f)] public float edgeSmoothness = 0.2f;
    [Tooltip("Profundidad mínima del fondo (% del tamaño promedio).")]
    [Range(0f, 2f)] public float minFloorDepthPercent = 0.5f;

    [Header("Desorden de Segmentos")]
    [Tooltip("Desfase de altura aleatorio por cada tramo (% del tamaño promedio).")]
    [Range(0f, 2f)] public float cellHeightVariationPercent = 0.5f;
    [Tooltip("Inclinación aleatoria de los bloques (% del tamaño promedio).")]
    [Range(0f, 5f)] public float cellTiltAmountPercent = 1f;

    [Header("Brutalismo y Textura")]
    [Tooltip("Serrado extra en los bordes para imitar piedras.")]
    [Range(0f, 1f)] public float edgeSerration = 0.85f;
    [Range(50f, 300f)] public float serrationScale = 80f;
    [Tooltip("Hace que las piedras resalten más.")]
    [Range(0f, 0.5f)] public float stoneHighlight = 0.1f;

    [Header("Efecto Labio y Borde (Lips)")]
    [Tooltip("Altura del bulto en los bordes (% del tamaño promedio). Positivo = arriba.")]
    [Range(-1f, 1f)] public float lipHeightPercent = 0.3f;
    [Tooltip("Ancho del área afectada desde el borde (% del tamaño promedio).")]
    [Range(0.5f, 10f)] public float lipWidthPercent = 3f;
    [Tooltip("Hundimiento justo antes de la grieta (% del tamaño promedio).")]
    [Range(0f, 1f)] public float edgeRoundingDepthPercent = 0.5f;

    [Header("Capas de Detalle Surface")]
    [Tooltip("Pequeños puntos de salto de material (% del tamaño promedio).")]
    [Range(0f, 0.5f)] public float pittingAmountPercent = 0.08f;
    [Range(50f, 200f)] public float pittingScale = 91.5f;
    [Tooltip("Rugosidad micro-textural (% del tamaño promedio).")]
    [Range(0f, 0.2f)] public float microRoughnessPercent = 0.004f;
    [Range(100f, 500f)] public float microScale = 428f;

    [Header("Bordes Orgánicos (Fade out)")]
    [Tooltip("Distancia mínima al borde para desvanecer (% del tamaño).")]
    [Range(5f, 25f)] public float bordeMinPercent = 10f;
    [Tooltip("Distancia máxima al borde (% del tamaño).")]
    [Range(10f, 35f)] public float bordeMaxPercent = 18f;

    // Variables internas
    private float width;
    private float length;
    private float avgSize;
    private List<Vector2> pathPoints;
    private List<BranchInfo> branches;

    private struct BranchInfo
    {
        public int startSegment;
        public Vector2 direction;
        public float length;
        public float width;
    }

    [Header("Estadísticas y Debug")]
    [SerializeField] private int vertexCount;
    [SerializeField] private int triangleCount;

    private void OnValidate()
    {
#if UNITY_EDITOR
        if (!autoUpdate) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        
        EditorApplication.delayCall -= Generate;
        EditorApplication.delayCall += Generate;
#endif
    }

    private void Awake()
    {
        if (Application.isPlaying)
        {
            Seed = Random.Range(0, 1000000);
            Generate();
        }
    }

    [ContextMenu("Generar Rajadura")]
    public void Generate()
    {
        if (this == null) return;
        Random.InitState(Seed);
        
        width = Random.Range(minWidth, maxWidth);
        length = Random.Range(minLength, maxLength);
        avgSize = (width + length) * 0.5f;

        if (randomizeRotation)
            currentRotation = Random.Range(minRotation, maxRotation);
        else
            currentRotation = 0f;

        Transform existing = transform.Find("RajaduraMesh");
        if (existing != null)
        {
            if (Application.isPlaying) Destroy(existing.gameObject);
            else DestroyImmediate(existing.gameObject);
        }

        GameObject meshObj = new GameObject("RajaduraMesh");
        meshObj.transform.SetParent(this.transform, false);
        meshObj.transform.localPosition = Vector3.zero;
        meshObj.transform.localRotation = Quaternion.Euler(0f, currentRotation, 0f);
        meshObj.layer = 7;
        meshObj.tag = "Rajadura";

        MeshFilter mf = meshObj.AddComponent<MeshFilter>();
        MeshRenderer mr = meshObj.AddComponent<MeshRenderer>();
        MeshCollider mc = meshObj.AddComponent<MeshCollider>();
        
        if (material != null) mr.sharedMaterial = material;
        
        // Generar "Backbone" de la rajadura
        pathPoints = new List<Vector2>();
        int pathRes = segmentCount;
        float segmentZ = length / pathRes;
        float seedWiggle = Random.Range(0f, 1000f);
        
        for (int i = 0; i <= pathRes; i++)
        {
            float t = (float)i / pathRes;
            float z = t * length - (length * 0.5f);
            float x = (Mathf.PerlinNoise(t * wiggleScale, seedWiggle) - 0.5f) * 2f * (width * wiggleAmplitude);
            pathPoints.Add(new Vector2(x, z));
        }

        // Generar ramificaciones
        branches = new List<BranchInfo>();
        if (branchProbability > 0f)
        {
            for (int i = 1; i < pathRes; i++)
            {
                if (Random.value < branchProbability)
                {
                    float angle = Random.Range(-branchAngle, branchAngle) * Mathf.Deg2Rad;
                    Vector2 dir = pathPoints[i] - pathPoints[i - 1];
                    dir.Normalize();
                    Vector2 perpDir = new Vector2(-dir.y, dir.x);
                    Vector2 branchDir = (dir * Mathf.Cos(angle) + perpDir * Mathf.Sin(angle)).normalized;
                    
                    branches.Add(new BranchInfo
                    {
                        startSegment = i,
                        direction = branchDir,
                        length = Random.Range(minBranchLengthPercent, maxBranchLengthPercent) * avgSize / 100f,
                        width = avgSize * (maxCrackWidthPercent / 100f) * branchWidthPercent
                    });
                }
            }
        }

        Mesh mesh = GenerateMesh();
        mf.sharedMesh = mesh;
        mc.sharedMesh = mesh;

        vertexCount = mesh.vertexCount;
        triangleCount = mesh.triangles.Length / 3;
    }

    private Mesh GenerateMesh()
    {
        Mesh mesh = new Mesh { name = "Proc_Rajadura_Premium" };
        
        float stepX = width / polygonsX;
        float stepZ = length / polygonsZ;
        float halfW = width * 0.5f;
        float halfL = length * 0.5f;

        float bMin = avgSize * (bordeMinPercent / 100f);
        float bMax = avgSize * (bordeMaxPercent / 100f);

        // Precalcular bordes de la malla
        float[] bL = new float[polygonsZ + 1];
        float[] bR = new float[polygonsZ + 1];
        float[] bB = new float[polygonsX + 1];
        float[] bF = new float[polygonsX + 1];

        float nS = 3f;
        float nO = Random.Range(0f, 100f);
        for(int z=0; z<=polygonsZ; z++) {
            float t = (float)z/polygonsZ;
            bL[z] = Mathf.Lerp(bMin, bMax, Mathf.PerlinNoise(0.1f + nO, t * nS));
            bR[z] = Mathf.Lerp(bMin, bMax, Mathf.PerlinNoise(10.7f + nO, t * nS));
        }
        for(int x=0; x<=polygonsX; x++) {
            float t = (float)x/polygonsX;
            bB[x] = Mathf.Lerp(bMin, bMax, Mathf.PerlinNoise(t * nS + nO, 0.3f));
            bF[x] = Mathf.Lerp(bMin, bMax, Mathf.PerlinNoise(t * nS + nO, 15.4f));
        }

        Vector3[] vertices = new Vector3[(polygonsX+1)*(polygonsZ+1)];
        Color[] colors = new Color[vertices.Length];
        int idx = 0;

        for (int z = 0; z <= polygonsZ; z++)
        {
            float localZ = z * stepZ;
            float worldZ = localZ - halfL;
            for (int x = 0; x <= polygonsX; x++)
            {
                float localX = x * stepX;
                float worldX = localX - halfW;

                // 1. Mask de borde (Fade out en extremos de la malla)
                float dL = localX - bL[z];
                float dR = (width - bR[z]) - localX;
                float dB = localZ - bB[x];
                float dF = (length - bF[x]) - localZ;
                float minD = Mathf.Min(dL, dR, dB, dF);
                
                float borderFactor = 0f;
                if (minD > 0f) {
                    float t = Mathf.Clamp01(minD / (avgSize * 0.1f));
                    borderFactor = Mathf.SmoothStep(0f, 1f, t);
                }

                // 2. Cálculo de profundidad Premium (similar a Crocodile)
                float heightOffset = 0f;
                float vMask = 0f;
                if (borderFactor > 0.001f)
                {
                    heightOffset = CalculatePremiumDepth(worldX, worldZ, out vMask);
                }

                float crackDepth = avgSize * (crackDepthPercent / 100f);
                float finalH = heightOffset * borderFactor;
                finalH = Mathf.Max(finalH, -crackDepth * 1.5f); // Limitador

                vertices[idx] = new Vector3(worldX, finalH, worldZ);
                colors[idx] = new Color(vMask * borderFactor, 0.5f + (heightOffset * 2f), 0f, 1f);
                idx++;
            }
        }

        int[] tris = new int[polygonsX * polygonsZ * 6];
        int ti = 0;
        for (int z = 0; z < polygonsZ; z++) {
            for (int x = 0; x < polygonsX; x++) {
                int v = z * (polygonsX + 1) + x;
                tris[ti++] = v; tris[ti++] = v + polygonsX + 1; tris[ti++] = v + 1;
                tris[ti++] = v + 1; tris[ti++] = v + polygonsX + 1; tris[ti++] = v + polygonsX + 2;
            }
        }

        mesh.vertices = vertices;
        mesh.colors = colors;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private float CalculatePremiumDepth(float x, float z, out float vertexMask)
    {
        vertexMask = 0f;
        float origX = x;
        float origZ = z;

        // 1. Distorsión (Bite & Serration/Irregularity)
        if (edgeBiteAmount > 0f || edgeSerration > 0f || edgeIrregularity > 0f)
        {
            float b1 = Mathf.PerlinNoise(x * edgeBiteScale, z * edgeBiteScale) * edgeBiteAmount;
            float s1 = Mathf.PerlinNoise(x * serrationScale, z * serrationScale) * edgeSerration;
            float i1 = (Mathf.PerlinNoise(x * irregularityScale, z * irregularityScale) - 0.5f) * edgeIrregularity;
            
            float combinedWarp = (b1 * 1.2f + s1 * 0.8f + i1 * 0.6f) * 0.25f;
            x += combinedWarp;
            z += (Mathf.PerlinNoise(x * 25f + 10f, z * 25f + 10f) - 0.5f) * combinedWarp;
        }

        // 2. Distancia a la línea (SDF) y Segment ID
        int segIdx = 0;
        float distToPath = GetDistToPath(x, z, out segIdx);

        // 3. Variación de Segmento (Height & Tilt) - ¡Paridad con Cocodrilo!
        float segHash = (float)(segIdx * 123.456f + Seed) % 1.0f;
        float cellHeightVariation = avgSize * (cellHeightVariationPercent / 100f);
        float cellTiltAmount = avgSize * (cellTiltAmountPercent / 100f);
        float cellOffset = (segHash - 0.5f) * cellHeightVariation;
        
        // Tilt lateral basado en qué lado de la grieta estamos
        // Para simplificar, usaremos un vector perpendicular al segmento (aprox)
        float sideSign = (x > pathPoints[segIdx].x) ? 1f : -1f;
        float tiltX = sideSign * cellTiltAmount * (segHash * 2f - 1f);
        float totalCellEffect = cellOffset + (tiltX * (distToPath / width));

        // 4. Variación de ancho con transiciones bruscas
        float wNoise = Mathf.PerlinNoise(x * widthNoiseScale + Seed, z * widthNoiseScale + Seed);
        
        // Aplicar transiciones bruscas si está habilitado
        if (harshTransitions > 0f)
        {
            float stepped = Mathf.Floor(wNoise * 5f) / 5f; // Discretizar en 5 niveles
            wNoise = Mathf.Lerp(wNoise, stepped, harshTransitions);
        }
        
        // Aplicar segmentación visible
        if (segmentVisibility > 0f)
        {
            float segNoise = Mathf.PerlinNoise(x * segmentCutScale, z * segmentCutScale);
            if (segNoise > (1f - segmentVisibility * 0.3f))
            {
                wNoise *= 0.3f; // Estrechar bruscamente en los cortes
            }
        }
        
        float dynWidth = Mathf.Lerp(avgSize * (minCrackWidthPercent / 100f), avgSize * (maxCrackWidthPercent / 100f), wNoise);
        
        // Calcular distancia a ramas y usar la más cercana
        float minDistToBranch = float.MaxValue;
        float closestBranchWidth = 0f;
        foreach (var branch in branches)
        {
            Vector2 branchStart = pathPoints[branch.startSegment];
            Vector2 branchEnd = branchStart + branch.direction * branch.length;
            float distSq = DistToSegmentSq(new Vector2(x, z), branchStart, branchEnd);
            float dist = Mathf.Sqrt(distSq);
            if (dist < minDistToBranch)
            {
                minDistToBranch = dist;
                closestBranchWidth = branch.width;
            }
        }

        // Usar la distancia más cercana (rama o path principal)
        bool isInBranch = minDistToBranch < closestBranchWidth;
        float effectiveDistToPath = isInBranch ? minDistToBranch : distToPath;
        float effectiveWidth = isInBranch ? closestBranchWidth : dynWidth;

        // 5. Lips & Rounding
        float lipHeight = avgSize * (lipHeightPercent / 100f);
        float lipWidth = avgSize * (lipWidthPercent / 100f);
        float edgeRoundingDepth = avgSize * (edgeRoundingDepthPercent / 100f);
        
        float edgeEffect = 0f;
        float outerR = effectiveWidth + lipWidth;
        if (effectiveDistToPath < outerR && effectiveDistToPath > effectiveWidth)
        {
            float t = (effectiveDistToPath - effectiveWidth) / lipWidth;
            float bulge = Mathf.Sin(t * Mathf.PI) * lipHeight;
            float rounding = -Mathf.Exp(-t * 8f) * edgeRoundingDepth;
            edgeEffect = bulge + rounding;
        }

        if (effectiveDistToPath > effectiveWidth) return totalCellEffect + edgeEffect;

        // 6. Profundidad con SDF suave
        float crackDepth = avgSize * (crackDepthPercent / 100f);
        float minFloorDepth = avgSize * (minFloorDepthPercent / 100f);
        
        float dNoise = Mathf.PerlinNoise(x * depthNoiseScale + Seed + 500f, z * depthNoiseScale + Seed + 500f);
        float dMod = Mathf.Lerp(1f - depthVariation, 1f, dNoise);
        
        float tInner = effectiveDistToPath / effectiveWidth;
        float profile = Mathf.SmoothStep(1f, 0f, tInner - edgeSmoothness);
        
        float rawD = crackDepth * profile * dMod;
        float finalD = Mathf.Max(rawD, profile > 0.5f ? minFloorDepth : 0f);

        // 7. Roughness & Pitting
        float pittingAmount = avgSize * (pittingAmountPercent / 100f);
        float microRoughness = avgSize * (microRoughnessPercent / 100f);
        
        float detail = 0f;
        if (pittingAmount > 0f || microRoughness > 0f)
        {
            float stoneNoise = Mathf.PerlinNoise(origX * microScale, origZ * microScale);
            float p = Mathf.PerlinNoise(origX * pittingScale, origZ * pittingScale);
            if (p > 0.85f) detail -= (p - 0.85f) * pittingAmount * 15f;
            detail += (stoneNoise - 0.5f) * microRoughness;
            if (stoneNoise > 0.7f) detail += (stoneNoise - 0.7f) * stoneHighlight;
        }

        vertexMask = profile;
        return (totalCellEffect + edgeEffect - finalD + detail);
    }

    private float GetDistToPath(float px, float pz, out int closestSegment)
    {
        closestSegment = 0;
        float minDistSq = float.MaxValue;
        for (int i = 0; i < pathPoints.Count - 1; i++)
        {
            Vector2 p1 = pathPoints[i];
            Vector2 p2 = pathPoints[i + 1];
            float dSq = DistToSegmentSq(new Vector2(px, pz), p1, p2);
            if (dSq < minDistSq) {
                minDistSq = dSq;
                closestSegment = i;
            }
        }
        return Mathf.Sqrt(minDistSq);
    }

    private float DistToSegmentSq(Vector2 p, Vector2 a, Vector2 b)
    {
        float l2 = (a - b).sqrMagnitude;
        if (l2 == 0.0f) return (p - a).sqrMagnitude;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, b - a) / l2);
        return (p - (a + t * (b - a))).sqrMagnitude;
    }
}
