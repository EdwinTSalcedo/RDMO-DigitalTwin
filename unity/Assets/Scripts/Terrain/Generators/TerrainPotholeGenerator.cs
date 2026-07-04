using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class TerrainPotholeGenerator : MonoBehaviour
{
    // ─── INTERNAL TYPES ─────────────────────────────────────────────────────

    public enum UnifiedMode
    {
        Mixed,          // Generates both types based on mixRatio
        OnlyBache,
        OnlyCrocodile
    }



    // ─── SETTINGS ───────────────────────────────────────────────────────────

    [Header("Main Configuration")]
    public UnifiedMode mode = UnifiedMode.Mixed;

    
    [Header("Weighted Mixing (Probabilidades)")]
    [Tooltip("Peso para generar Baches.")]
    [Range(0f, 1f)] public float chanceBache = 0.5f;
    [Tooltip("Peso para generar Cocodrilo.")]
    [Range(0f, 1f)] public float chanceCrocodile = 0.5f;
    [Tooltip("Peso para generar Rajadura (Linear Crack).")]
    [Range(0f, 1f)] public float chanceRajadura = 0.5f;

    public int Seed = 12345;

    public bool autoUpdate = false;
    public bool randomizeSeedOnStart = true;
    public bool modoCaptura = false;
    public Material sharedMaterial;

    public bool generacionTerminada = false;

    [Header("Spawner Settings")]
    public int cantidadBaches = 30;
    public float ladoArea = 10f;
    public float margenBorde = 0.5f;
    public LayerMask capasObstaculos = ~0; 
    [Tooltip("Si es true, concentra la generación al centro. Si es false, dispersa uniformemente por toda el área.")]
    public bool concentrateInMiddle = true;
    
    [Header("Separation Control")]
    [Tooltip("Margen mínimo de separación entre objetos generados (en metros).")]
    [Range(0f, 2f)] public float separationMargin = 0.3f; 
    
    [Header("Global Depth Control")]
    [Tooltip("Profundidad global que afecta a todos los baches generados.")]
    [Range(0.01f, 0.5f)] public float depthGlobal = 0.15f;

    [Header("Smart Optimization (Lazy Load)")]
    [Tooltip("Objetos (Vehículos/Cámaras) que activan la generación al acercarse.")]
    public List<Transform> activationTargets;
    [Tooltip("Distancia en metros para generar/activar el bache.")]
    public float activationDistance = 10f;
    [Tooltip("Reduce la resolución del collider. 1 = Igual, 16 = 1/16 resolución.")]
    [Range(1, 20)] public int colliderDownsample = 16;

    [Header("Bache Configuration")]
    public BacheConfig bacheSettings;
    // Removed old colliderDownsample location to consolidate in Smart Optimization header

    [Header("Crocodile Configuration")]
    public CrocodileConfig crocSettings;

    [Header("Rajadura (Crack) Configuration")]
    public RajaduraConfig rajaduraProfile;

    // Helper structs for Rajadura
    [System.Serializable]
    public struct BranchInfo
    {
        public int startSegment;
        public Vector2 direction;
        public float length;
        public float width;
    }

    [System.Serializable]
    public class RajaduraConfig
    {
        [Header("Dimensiones Aleatorias")]
        public float minWidth = 0.4f; 
        public float maxWidth = 1f;
        public float minLength = 1f;
        public float maxLength = 4f;

        [Header("Rotación")]
        public bool randomizeRotation = true;
        [Range(0f, 360f)] public float minRotation = 73f;
        [Range(0f, 360f)] public float maxRotation = 276f;

        [Header("Calidad")]
        [Range(10, 500)] public int polygonsX = 30;
        [Range(10, 500)] public int polygonsZ = 30;

        [Header("Patrón Rajadura")]
        [Range(10, 100)] public int segmentCount = 24;
        [Range(0.01f, 2f)] public float minCrackWidthPercent = 0.06f;
        [Range(0.01f, 5f)] public float maxCrackWidthPercent = 0.13f;
        [Range(0.01f, 10f)] public float crackDepthPercent = 4.54f;
        [Range(1f, 4f)] public float crackSmoothness = 2.59f;

        [Header("Forma y Wiggle")]
        [Range(0.1f, 10f)] public float wiggleScale = 7.06f;
        [Range(0f, 0.5f)] public float wiggleAmplitude = 0.128f;
        [Range(0f, 1f)] public float edgeBiteAmount = 0f;
        [Range(20f, 100f)] public float edgeBiteScale = 20f;
        [Range(0f, 1f)] public float distortion = 0f;

        [Header("Variación Orgánica")]
        [Range(0.1f, 10f)] public float widthNoiseScale = 0.1f;
        [Range(0f, 0.5f)] public float edgeIrregularity = 0.16f;
        [Range(5f, 50f)] public float irregularityScale = 33.6f;

        [Header("Transiciones y Segmentación")]
        [Range(0f, 1f)] public float harshTransitions = 0.384f;
        [Range(0f, 1f)] public float segmentVisibility = 0.413f;
        [Range(5f, 50f)] public float segmentCutScale = 23.6f;

        [Header("Ramificaciones Secundarias")]
        [Range(0f, 1f)] public float branchProbability = 0f;
        [Range(5f, 50f)] public float minBranchLengthPercent = 10.2f;
        [Range(10f, 100f)] public float maxBranchLengthPercent = 37.7f;
        [Range(15f, 90f)] public float branchAngle = 16.5f;
        [Range(0.1f, 1f)] public float branchWidthPercent = 0.202f;

        [Header("Variación de Profundidad")]
        [Range(0.1f, 10f)] public float depthNoiseScale = 3.56f;
        [Range(0f, 1f)] public float depthVariation = 0.472f;
        [Range(0f, 1f)] public float edgeSmoothness = 0.379f;
        [Range(0f, 2f)] public float minFloorDepthPercent = 0.797f;

        [Header("Desorden de Segmentos")]
        [Range(0f, 2f)] public float cellHeightVariationPercent = 0f;
        [Range(0f, 5f)] public float cellTiltAmountPercent = 0f;

        [Header("Brutalismo y Textura")]
        [Range(0f, 1f)] public float edgeSerration = 0f;
        [Range(50f, 300f)] public float serrationScale = 68.4f;
        [Range(0f, 0.5f)] public float stoneHighlight = 0.009f;

        [Header("Efecto Labio y Borde (Lips)")]
        [Range(-1f, 1f)] public float lipHeightPercent = -1f;
        [Range(0.5f, 10f)] public float lipWidthPercent = 0.5f;
        [Range(0f, 1f)] public float edgeRoundingDepthPercent = 0.5f;

        [Header("Capas de Detalle Surface")]
        [Range(0f, 0.5f)] public float pittingAmountPercent = 0.222f;
        [Range(50f, 200f)] public float pittingScale = 104.8f;
        [Range(0f, 0.2f)] public float microRoughnessPercent = 0.0695f;
        [Range(100f, 500f)] public float microScale = 232.7f;

        [Header("Bordes Orgánicos (Fade out)")]
        [Range(5f, 25f)] public float bordeMinPercent = 5f;
        [Range(10f, 35f)] public float bordeMaxPercent = 10f;
    }

    [System.Serializable]
    public class BacheConfig
    {
        [Header("Configuración Genética (Seed)")]
        [Tooltip("Semilla única. Cada número genera una variante diferente respetando los rangos.")]
        public int seed = 235;

        [Header("Dimensiones Generales")]
        [Tooltip("Ancho del área cuadrada del bache")]
        [Range(0.25f, 5f)] public float minWidth = 0.25f;
        [Range(0.25f, 5f)] public float maxWidth = 1f;
        [Range(0.25f, 5f)] public float minLength = 0.25f;
        [Range(0.25f, 5f)] public float maxLength = 1f;

        [Header("Resolución")]
        [Range(10, 254)] public int polygonsX = 30;
        [Range(10, 254)] public int polygonsZ = 30;

        [Header("Configuración Aleatoria")]
        [Tooltip("Cantidad de sub-baches que conforman el bache principal")]
        public int cantidadBachesAleatorios = 10;
        
        [Tooltip("Radio de cada bache como % del tamaño promedio")]
        public float minRadioPorcentaje = 3f;
        public float maxRadioPorcentaje = 15f;
        
        [Tooltip("Profundidad de cada bache en metros")]
        public float minProfundidad = 0.05f;
        public float maxProfundidad = 0.2f;
        
        [Tooltip("Deformación del bache (0 = circular, 1 = muy deformado)")]
        public float minDeformacion = 0.2f;
        public float maxDeformacion = 0.6f;
        
        [Tooltip("Irregularidad del borde (0 = suave, 1 = muy irregular)")]
        public float minIrregularidadBorde = 0.4f;
        public float maxIrregularidadBorde = 0.9f;
        
        [Tooltip("Porcentaje del fondo que es plano (0 = todo pendiente, 1 = muy plano)")]
        public float minFondoPlano = 0.2f;
        public float maxFondoPlano = 0.6f;
        
        [Tooltip("Variación de profundidad dentro del bache")]
        public float minVariacionProf = 0.3f;
        public float maxVariacionProf = 0.7f;

        [Header("Bordes Orgánicos")]
        [Tooltip("Margen mínimo del borde en metros")]
        public float bordeMin = 0.1f;
        [Tooltip("Margen máximo del borde en metros")]
        public float bordeMax = 0.3f;
        [Tooltip("Escala del ruido de borde")]
        public float noiseScale = 2f;
        [Tooltip("Suavidad de transición del borde")]
        [Range(0.1f, 5f)] public float bordeSuavidad = 1.5f;

        [Header("Profundidad Global")]
        [Tooltip("Profundidad máxima global que limita todos los baches")]
        [Range(0.01f, 1f)] public float profundidadMaximaGlobal = 0.2f;

        [Header("Recorte de Malla")]
        [Tooltip("Si es true, el bache se cortará rectangulamente en los bordes de la malla, ignorando los márgenes orgánicos.")]
        public bool cropToRectangularBounds = false;
        
        [Tooltip("Mínimo margen (%) - Solo usado cuando cropToRectangularBounds = true")]
        [Range(5, 30)] public int bordeMinPercent = 5;
        [Tooltip("Máximo margen (%) - Solo usado cuando cropToRectangularBounds = true")]
        [Range(10, 45)] public int bordeMaxPercent = 37;

        [Header("Ruido Extra")]
        public float minNoiseScale = 5f;
        public float maxNoiseScale = 20f;
        public float depthMultiplier = 1f;
    }

    [System.Serializable]
    public class CrocodileConfig
    {
        [Header("Dimensiones Aleatorias")]
        public float minWidth = 1.5f;
        public float maxWidth = 2.5f;
        public float minLength = 2.5f;
        public float maxLength = 3.5f;

        public enum MetricType { Euclidean, Chebyshev, Minkowski }
        
        [Header("Fractura Angular (Alligator)")]
        [Tooltip("Tipo de métrica de distancia Voronoi.")]
        public MetricType distanceMetric = MetricType.Minkowski;
        [Tooltip("Parámetro p de Minkowski (2 = Euclidean, inf = Chebyshev).")]
        [Range(1f, 5f)] public float minkowskiP = 2.33f;
        [Tooltip("Inyecta ruido en las coordenadas antes del Voronoi para bordes 'mordidos'.")]
        [Range(0f, 1f)] public float edgeBiteAmount = 0.08f;
        [Range(20f, 100f)] public float edgeBiteScale = 53.8f;

        [Header("Calidad")]
        [Tooltip("Polígonos por eje. A mayor número, menos 'dientes de sierra', pero más costo.")]
        [Range(10, 500)] public int polygonsX = 30;
        [Range(10, 500)] public int polygonsZ = 30;

        [Header("Patrón Cocodrilo")]
        [Tooltip("Cantidad mínima de bloques poligonales.")]
        [Range(10, 100)] public int minCellCount = 20;
        [Tooltip("Cantidad máxima de bloques poligonales.")]
        [Range(10, 100)] public int maxCellCount = 40;
        
        [Tooltip("Ancho mínimo de las grietas (% del tamaño).")]
        [Range(0.01f, 2f)] public float minCrackWidthPercent = 0.12f;

        [Tooltip("Ancho máximo de las grietas (% del tamaño).")]
        [Range(0.01f, 5f)] public float maxCrackWidthPercent = 1.29f;
        
        [Tooltip("Profundidad de las grietas.")]
        [Range(0.01f, 0.4f)] public float crackDepth = 0.145f;
        
        [Tooltip("Suavidad del borde de la grieta.")]
        [Range(1f, 4f)] public float crackSmoothness = 2.76f;

        [Header("Variación Orgánica")]
        [Tooltip("Escala del ruido para variar el ancho (mayor = cambios más rápidos).")]
        [Range(0.1f, 10f)] public float widthNoiseScale = 1.89f;

        [Tooltip("Irregularidad de los bordes (dientes naturales).")]
        [Range(0f, 0.5f)] public float edgeIrregularity = 0.141f;

        [Tooltip("Escala del ruido de irregularidad (mayor = más detalle fino).")]
        [Range(5f, 50f)] public float irregularityScale = 7f;

        [Tooltip("Distorsión leve para que no sean líneas perfectas (0 = rectas).")]
        [Range(0f, 1f)] public float distortion = 0.327f;

        [Header("Variación de Profundidad")]
        [Tooltip("Escala del ruido de profundidad (Musgrave style).")]
        [Range(0.1f, 10f)] public float depthNoiseScale = 1.34f;
        [Tooltip("Cuánto varía la profundidad a lo largo de la grieta.")]
        [Range(0f, 1f)] public float depthVariation = 0.271f;
        [Tooltip("Suavidad del borde (SDF). 0 = abrupto, 1 = muy suave.")]
        [Range(0f, 1f)] public float edgeSmoothness = 0.271f;
        [Tooltip("Profundidad mínima del fondo (clamp) para evitar picos.")]
        [Range(0f, 0.1f)] public float minFloorDepth = 0.0266f;

        [Header("Desorden de Segmentos")]
        [Tooltip("Desfase de altura aleatorio por cada bloque de asfalto.")]
        [Range(0f, 0.04f)] public float cellHeightVariation = 0f;
        [Tooltip("Inclinación aleatoria de los bloques.")]
        [Range(0f, 0.1f)] public float cellTiltAmount = 0f;

        [Header("Brutalismo y Textura")]
        [Tooltip("Serrado extra en los bordes para imitar piedras.")]
        [Range(0f, 1f)] public float edgeSerration = 0.04f;
        [Range(50f, 300f)] public float serrationScale = 96f;
        [Tooltip("Hace que las piedras resalten más.")]
        [Range(0f, 0.5f)] public float stoneHighlight = 0f;

        [Header("Efecto Labio y Borde")]
        [Tooltip("Altura del labio/bulto en los bordes. Positivo = hacia arriba, Negativo = hacia abajo.")]
        [Range(-0.02f, 0.02f)] public float lipHeight = -0.016f;
        [Tooltip("Ancho absoluto del área afectada (en metros) desde el borde de la grieta.")]
        [Range(0.01f, 0.2f)] public float lipWidth = 0.0721f;
        [Tooltip("Cuánto se 'hunde' o redondea el borde justo antes de la grieta.")]
        [Range(0f, 0.02f)] public float edgeRoundingDepth = 0.0178f;

        [Header("Capas de Detalle Surface")]
        [Tooltip("Pequeños puntos donde se saltó el material.")]
        [Range(0f, 0.01f)] public float pittingAmount = 0.00151f;
        [Range(50f, 200f)] public float pittingScale = 91.5f;
        [Tooltip("Rugosidad micro-textural para evitar brillo plástico.")]
        [Range(0f, 0.005f)] public float microRoughness = 0.00007f;
        [Range(100f, 500f)] public float microScale = 428f;

        [Header("Bordes Orgánicos")]
        [Tooltip("Distancia mínima al borde sin grietas (metros).")]
        public float bordeMin = 0.1f;
        [Tooltip("Distancia máxima al borde (metros).")]
        public float bordeMax = 0.3f;
        [Tooltip("Escala del ruido de borde.")]
        public float noiseScale = 2f;
        [Tooltip("Suavidad de transición del borde.")]
        [Range(0.1f, 5f)] public float bordeSuavidad = 1.5f;

        // Legacy/Unused now but kept to avoid serialization data loss immediately if needed, or remove? 
        // User asked for "lo mismo", so we prefer the explicit meter control.
        // [Range(0f, 15f)] public float bordeMinPercent = 0.0f; 
        // [Range(0f, 20f)] public float bordeMaxPercent = 0.1f;

        public float depthMultiplier = 1f;
    }

    // ─── UNITY EVENTS ───────────────────────────────────────────────────────
    
    private void Start()
    {
        if (Application.isPlaying && randomizeSeedOnStart)
        {
            // Use Ticks for much better entropy than Random.Range on start
            Seed = (int)(System.DateTime.Now.Ticks & 0x7FFFFFFF);
            Generate();
        }
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
        
        try { Generate(); }
        catch (System.Exception e) { Debug.LogWarning($"TerrainPotholeGenerator: {e.Message}"); }
    }
#endif

    // ─── LAZY LOADING SYSTEM ───────────────────────────────────────────────

    private class ObjectTracker
    {
        public GameObject gameObject;
        public Vector3 position;
        // Simplified tracker, we don't need seed/params anymore as mesh is generated upfront
    }

    private List<ObjectTracker> trackers = new List<ObjectTracker>();

    private void Update()
    {
        if (trackers == null || trackers.Count == 0 || activationTargets == null || activationTargets.Count == 0) return;

        float sqrDist = activationDistance * activationDistance;

        // Simple spatial check
        foreach (var t in trackers)
        {
            if (t.gameObject == null) continue;

            bool active = false;
            foreach (var target in activationTargets)
            {
                if (target == null) continue;
                if ((t.position - target.position).sqrMagnitude < sqrDist)
                {
                    active = true;
                    break;
                }
            }

            // Only toggle Active state, geometry is already there
            if (active != t.gameObject.activeSelf)
            {
                t.gameObject.SetActive(active);
            }
        }
    }

    // GenerateGeometryForTracker Removed (Code moved inline/simplified or used separate methods)
    // We will call the specific generation methods directly in the loop below.

    [ContextMenu("Generate Spawner")]
    public void Generate()
    {
        // 1. Cleanup children & trackers
        ClearChildren();
        trackers.Clear();

        Random.InitState(Seed);

        // Pre-calcular Pools de Mallas (Optimización masiva para 400 objetos)
        int poolSize = 5; // 5 variantes de cada tipo
        Mesh[] bacheVis = new Mesh[poolSize]; Mesh[] bacheCol = new Mesh[poolSize];
        Mesh[] crocVis = new Mesh[poolSize]; Mesh[] crocCol = new Mesh[poolSize];
        Mesh[] crackVis = new Mesh[poolSize]; Mesh[] crackCol = new Mesh[poolSize];
        
        for (int i = 0; i < poolSize; i++)
        {
            int s = Seed + i;
            // Rotación pre-horneada para rellenar el cuadrado completo. Se deforma mediante localScale al instanciar.
            float rotBase = Random.Range(0f, 360f);
            bacheVis[i] = GenerateBacheMesh(s, 1f, 1f, rotBase, bacheSettings.polygonsX, bacheSettings.polygonsZ);
            bacheCol[i] = GenerateBacheMesh(s, 1f, 1f, rotBase, Mathf.Max(2, bacheSettings.polygonsX / colliderDownsample), Mathf.Max(2, bacheSettings.polygonsZ / colliderDownsample));
            
            float rotCroc = Random.Range(0f, 360f);
            crocVis[i] = GenerateCrocodileMesh(s, 1f, 1f, rotCroc, crocSettings.polygonsX, crocSettings.polygonsZ);
            crocCol[i] = GenerateCrocodileMesh(s, 1f, 1f, rotCroc, Mathf.Max(2, crocSettings.polygonsX / colliderDownsample), Mathf.Max(2, crocSettings.polygonsZ / colliderDownsample));
            
            float rotCrack = rajaduraProfile.randomizeRotation ? Random.Range(rajaduraProfile.minRotation, rajaduraProfile.maxRotation) : 0f;
            crackVis[i] = GenerateRajaduraMesh(s, rajaduraProfile, 1f, 1f, rotCrack, rajaduraProfile.polygonsX, rajaduraProfile.polygonsZ);
            crackCol[i] = GenerateRajaduraMesh(s, rajaduraProfile, 1f, 1f, rotCrack, Mathf.Max(2, rajaduraProfile.polygonsX / colliderDownsample), Mathf.Max(2, rajaduraProfile.polygonsZ / colliderDownsample));
            
            bacheCol[i].name = "Pool_BacheCol"; crocCol[i].name = "Pool_CrocCol"; crackCol[i].name = "Pool_CrackCol";
        }

        // Calcular peso total
        float totalWeight = chanceBache + chanceCrocodile + chanceRajadura;
        if (totalWeight <= 0f) totalWeight = 1f;

        int generated = 0;
        int maxTotalAttempts = cantidadBaches * (modoCaptura ? 1000 : 100); 
        int totalAttempts = 0;

        float halfSide = ladoArea * 0.5f;
        float usableHalf = halfSide - margenBorde;

        List<Bounds> spawnedBounds = new List<Bounds>();

        // spread: 0 = solo centro, 1 = área completa.
        // Si no está concentrado, abarca toda el área desde el principio sin crecimiento.
        float spreadInitial = concentrateInMiddle ? 0.3f : 1.0f;
        float spreadGrowPerFail = concentrateInMiddle ? 0.015f : 0.0f;
        float spread = spreadInitial;

        while (generated < cantidadBaches && totalAttempts < maxTotalAttempts)
        {
            totalAttempts++;

            float r = Random.value * totalWeight;
            float w = 1f, l = 1f; 
            GameObject newObj = null;

            // Posición sesgada al centro (crece hacia los bordes si hay fallos)
            Vector3 pos = transform.position + SampleBiasedPosition(usableHalf, spread);

            if (r < chanceBache) {
                w = Random.Range(bacheSettings.minWidth, bacheSettings.maxWidth);
                l = Random.Range(bacheSettings.minLength, bacheSettings.maxLength);

                if (HasSpace(pos, w, l, spawnedBounds)) {
                    newObj = new GameObject($"Bache_{generated}");
                    newObj.tag = "Pothole"; newObj.layer = 7;
                    newObj.transform.SetParent(this.transform, false);
                    newObj.transform.position = pos;
                    int pIdx = Random.Range(0, poolSize);
                    SetupMeshComponents(newObj, bacheVis[pIdx], bacheCol[pIdx]);
                    newObj.transform.rotation = Quaternion.identity;
                    newObj.transform.localScale = new Vector3(w, 1f, l);
                    trackers.Add(new ObjectTracker { gameObject = newObj, position = pos });
                    spawnedBounds.Add(new Bounds(pos, new Vector3(w + (separationMargin * 2f), 1f, l + (separationMargin * 2f))));
                    generated++;
                    spread = spreadInitial; // reset al colocar con éxito
                } else {
                    spread = Mathf.Min(1f, spread + spreadGrowPerFail);
                }
            }
            else if (r < chanceBache + chanceCrocodile) {
                w = Random.Range(crocSettings.minWidth, crocSettings.maxWidth);
                l = Random.Range(crocSettings.minLength, crocSettings.maxLength);

                if (HasSpace(pos, w, l, spawnedBounds)) {
                    newObj = new GameObject($"Cocodrilo_{generated}");
                    newObj.tag = "Crocodile"; newObj.layer = 7;
                    newObj.transform.SetParent(this.transform, false);
                    newObj.transform.position = pos;
                    int pIdx = Random.Range(0, poolSize);
                    SetupMeshComponents(newObj, crocVis[pIdx], crocCol[pIdx]);
                    newObj.transform.rotation = Quaternion.identity;
                    newObj.transform.localScale = new Vector3(w, 1f, l);
                    trackers.Add(new ObjectTracker { gameObject = newObj, position = pos });
                    spawnedBounds.Add(new Bounds(pos, new Vector3(w + (separationMargin * 2f), 1f, l + (separationMargin * 2f))));
                    generated++;
                    spread = spreadInitial;
                } else {
                    spread = Mathf.Min(1f, spread + spreadGrowPerFail);
                }
            }
            else {
                w = Random.Range(rajaduraProfile.minWidth, rajaduraProfile.maxWidth);
                l = Random.Range(rajaduraProfile.minLength, rajaduraProfile.maxLength);

                if (HasSpace(pos, w, l, spawnedBounds)) {
                    newObj = new GameObject($"Rajadura_{generated}");
                    newObj.tag = "Crack"; newObj.layer = 7;
                    newObj.transform.SetParent(this.transform, false);
                    newObj.transform.position = pos;
                    int pIdx = Random.Range(0, poolSize);
                    SetupMeshComponents(newObj, crackVis[pIdx], crackCol[pIdx]);
                    newObj.transform.rotation = Quaternion.identity;
                    newObj.transform.localScale = new Vector3(w, 1f, l);
                    trackers.Add(new ObjectTracker { gameObject = newObj, position = pos });
                    spawnedBounds.Add(new Bounds(pos, new Vector3(w + (separationMargin * 2f), 1f, l + (separationMargin * 2f))));
                    generated++;
                    spread = spreadInitial;
                } else {
                    spread = Mathf.Min(1f, spread + spreadGrowPerFail);
                }
            }
        }

        if (modoCaptura)
        {
            generacionTerminada = true;
        }
    }


    // ─── POSITION SAMPLING ───────────────────────────────────────────────────

    /// <summary>
    /// Muestrea una posición 2D sesgada hacia el centro del área cuadrada.
    /// spread=0 solo el centro puntual, spread=1 área completa uniforme.
    /// Usa distribución gaussiana truncada aproximada (promedio de dos uniforms).
    /// </summary>
    private Vector3 SampleBiasedPosition(float usableHalf, float spread)
    {
        // Promedio de dos muestras uniformes → distribución triangular centrada
        // Aplicamos 'spread' para escalar el radio efectivo antes de clampar.
        float effectiveHalf = usableHalf * Mathf.Clamp01(spread);

        float rx = (Random.value + Random.value) * 0.5f; // 0..1 triangular
        float rz = (Random.value + Random.value) * 0.5f;
        // Mapear de [0,1] → [-effectiveHalf, +effectiveHalf]
        float x = (rx * 2f - 1f) * effectiveHalf;
        float z = (rz * 2f - 1f) * effectiveHalf;
        return new Vector3(x, 0f, z);
    }

    private int CantidadBachesSeguridad() => cantidadBaches * (modoCaptura ? 200 : 50);

    private void ClearChildren()
    {
        if (this == null) return;
        
        // Use a list to avoid modifying the collection while iterating
        List<GameObject> toDestroy = new List<GameObject>();
        foreach (Transform child in transform)
        {
            if (child != null) toDestroy.Add(child.gameObject);
        }

        foreach (var child in toDestroy)
        {
            if (child == null) continue;
            
            if (Application.isPlaying) Destroy(child);
            else DestroyImmediate(child);
        }
    }

    private void SetupMeshComponents(GameObject obj, Mesh visualMesh, Mesh collisionMesh)
    {
        var mf = obj.AddComponent<MeshFilter>();
        var mr = obj.AddComponent<MeshRenderer>();
        var mc = obj.AddComponent<MeshCollider>();
        if (sharedMaterial) mr.sharedMaterial = sharedMaterial;
        mf.sharedMesh = visualMesh;
        mc.sharedMesh = collisionMesh; // Use the low-res mesh for collisions
    }

    private bool HasSpace(Vector3 center, float w, float l, List<Bounds> existing)
    {
        // Agregar margen de separación a las dimensiones
        float bufferedW = w + (separationMargin * 2f);
        float bufferedL = l + (separationMargin * 2f);
        Vector3 size = new Vector3(bufferedW, 2f, bufferedL);
        Bounds newB = new Bounds(center, size);

        // 1 Check self-overlap with already spawned in this batch (con margen)
        foreach (var b in existing)
        {
            if (b.Intersects(newB)) return false;
        }

        // 2 Check physics overlap with world
        Collider[] hits = Physics.OverlapBox(center, size * 0.5f, Quaternion.identity, capasObstaculos, QueryTriggerInteraction.Ignore);
        foreach (var hit in hits)
        {
            // Ignore if the hit is a child of this spawner (already placed bache)
            // because we already check them via 'spawnedBounds'
            if (hit.transform.IsChildOf(transform)) continue;

            // -- NEW: Check for prohibited tag "Houses" --
            // Check the object itself and its parents
            if (hit.CompareTag("Houses")) return false;
            
            // Also check parent hierarchy in case the collider is a child of a House object
            Transform t = hit.transform.parent;
            while(t != null) {
                if(t.CompareTag("Houses")) return false;
                t = t.parent;
            }

            return false;
        }
        return true;
    }

    // ─── PROCEDURAL GENERATION CALLS ──────────────────────────────────────────

    // Internal Bache structure for generation
    private struct BacheInfo {
        public Vector2 pos;
        public float rad, prof, def, irreg, plano, varProf, suav;
        public int seed;
        public float rotation;
        public float scaleX;
        public float scaleZ;
    }

    // ─── GENERATE BACHE (Perlin Noise Puddle) ────────────────────────────────
    private Mesh GenerateBacheMesh(int seed, float width, float length, float rotationDegrees, int px, int pz)
    {
        Random.InitState(seed);
        // Calculate Rotated Bounding Box (AABB)
        float rad = rotationDegrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);
        float aabbWidth = Mathf.Abs(width * cos) + Mathf.Abs(length * sin);
        float aabbLength = Mathf.Abs(width * sin) + Mathf.Abs(length * cos);
        
        // Recalculate resolution to MAINTAIN TOTAL POLY COUNT (Conservation of complexity)
        float totalPolys = px * pz;
        float aspect = (aabbLength > 0.001f) ? aabbWidth / aabbLength : 1f;
        
        int aabbPx = Mathf.RoundToInt(Mathf.Sqrt(totalPolys * aspect));
        int aabbPz = Mathf.RoundToInt(Mathf.Sqrt(totalPolys / aspect));
        
        // Safety clamps
        aabbPx = Mathf.Clamp(aabbPx, 2, 500);
        aabbPz = Mathf.Clamp(aabbPz, 2, 500);

        Mesh mesh = new Mesh { name = "Proc_Bache_Baked" };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        float stepX = aabbWidth / aabbPx;
        float stepZ = aabbLength / aabbPz;
        float halfW = aabbWidth * 0.5f;
        float halfL = aabbLength * 0.5f;


        // --- GENERATE SUB-BACHES LIST ---
        float avgSize = (width + length) * 0.5f;
        var baches = new List<BacheInfo>();
        float mP = Mathf.Max(bacheSettings.bordeMax/width, bacheSettings.bordeMax/length) * 100f;
        mP = Mathf.Clamp(mP, 5f, 20f);
        float minP = mP, maxP = 100f - mP; // Percentage range 5..95 roughly

        // Initialize Random Anisotropy per sub-bache
        for(int i=0; i<bacheSettings.cantidadBachesAleatorios; i++) {
            float angleR = Random.Range(0f, 360f); // Precompute rotation
            float stretch = Random.Range(0.6f, 1.4f); // Stretch factor
            
            baches.Add(new BacheInfo {
                pos = new Vector2(Random.Range(minP, maxP), Random.Range(minP, maxP)),
                rad = Random.Range(bacheSettings.minRadioPorcentaje, bacheSettings.maxRadioPorcentaje),
                prof = Random.Range(bacheSettings.minProfundidad, bacheSettings.maxProfundidad),
                def = Random.Range(bacheSettings.minDeformacion, bacheSettings.maxDeformacion),
                irreg = Random.Range(bacheSettings.minIrregularidadBorde, bacheSettings.maxIrregularidadBorde),
                plano = Random.Range(bacheSettings.minFondoPlano, bacheSettings.maxFondoPlano),
                varProf = Random.Range(bacheSettings.minVariacionProf, bacheSettings.maxVariacionProf),
                suav = Random.Range(1f, 3f),
                seed = Random.Range(1000, 100000),
                rotation = angleR * Mathf.Deg2Rad, // Add these to struct if possible, or pack into 'suav' unused?
                // Actually, let's just use local vars inside BacheInfo or reconstruct seeds.
                // To keep it simple without changing struct deep definition if we can't see it (we can see it).
                // Let's modify the STRUCT first? No, we can just use the provided float params or extend struct.
                // Extend struct locally.
                scaleX = stretch,
                scaleZ = 1f / stretch
            });
        }
        
        Vector3[] vertices = new Vector3[(aabbPx + 1) * (aabbPz + 1)];
        Vector2[] uvs = new Vector2[vertices.Length];

        // Inverse rotation terms
        float invCos = Mathf.Cos(-rad);
        float invSin = Mathf.Sin(-rad);

        int index = 0;
        for (int z = 0; z <= aabbPz; z++)
        {
            float currentZ = z * stepZ - halfL;
            for (int x = 0; x <= aabbPx; x++)
            {
                float currentX = x * stepX - halfW;
                
                // Transform to local space of the pothole (Unrotated)
                float localX = currentX * invCos - currentZ * invSin;
                float localZ = currentX * invSin + currentZ * invCos;
                
                float y = 0f;
                // Check if inside original bounds (Rectangle centered at 0,0)
                    // 1. Domain Warping (Distorsionar el espacio para romper la forma)
                    float ns = bacheSettings.noiseScale;
                    float warpX = (Mathf.PerlinNoise(localX * ns, localZ * ns) - 0.5f) * bacheSettings.bordeMax * 2f;
                    float warpZ = (Mathf.PerlinNoise(localX * ns + 12.3f, localZ * ns + 45.6f) - 0.5f) * bacheSettings.bordeMax * 2f;
                    
                    float wx = localX + warpX;
                    float wz = localZ + warpZ;

                    // 2. Distancia al borde del rectángulo teórico (usando coordenadas distorsionadas)
                    float dx = (width * 0.5f) - Mathf.Abs(wx);
                    float dz = (length * 0.5f) - Mathf.Abs(wz);
                    
                    // Suavizar las esquinas (SDF Rounded Box)
                    float radius = Mathf.Min(width, length) * 0.25f;
                    float qx = Mathf.Max(Mathf.Abs(wx) - (width * 0.5f - radius), 0f);
                    float qz = Mathf.Max(Mathf.Abs(wz) - (length * 0.5f - radius), 0f);
                    float distToEdge = radius - Mathf.Sqrt(qx * qx + qz * qz);
                    if (qx == 0 && qz == 0) distToEdge = Mathf.Min(dx, dz);

                    // 3. Aplicar margen orgánico
                    float minBorderDist = distToEdge - bacheSettings.bordeMin;
                    
                    float borderFactor = 0f;
                    if (minBorderDist > 0f) {
                        float tBorder = Mathf.Clamp01(minBorderDist / (bacheSettings.bordeMax * bacheSettings.bordeSuavidad));
                        borderFactor = tBorder * tBorder * (3f - 2f * tBorder); // Smoothstep
                    }
                     if (borderFactor > 0.001f)
                    {
                        float sumDepth = 0f;
                        // Blending softness is controlled inline via k (cubic smooth max) below

                        // Accumulate sub-baches with SMOOTH UNION
                        foreach (var b in baches)
                        {
                            // 1. ANISOTROPY TRANSFORM
                            float bx = (b.pos.x / 100f - 0.5f) * width;
                            float bz = (b.pos.y / 100f - 0.5f) * length;
                            
                            float dx0 = localX - bx;
                            float dz0 = localZ - bz;
                            
                            // Rotate and Scale inv
                            float c = Mathf.Cos(-b.rotation);
                            float s = Mathf.Sin(-b.rotation);
                            float rx = dx0 * c - dz0 * s;
                            float rz = dx0 * s + dz0 * c;
                            
                            // Apply Non-Uniform Scale (Elliptical shape)
                            rx /= b.scaleX;
                            rz /= b.scaleZ;

                            float dist = Mathf.Sqrt(rx*rx + rz*rz);
                            float radM = avgSize * (b.rad / 100f);

                            if (dist > radM * 1.5f) continue;

                            float radEff = radM;
                            // Irregularity logic (FRACTAL)
                            if (b.irreg > 0f && dist > 0.001f) {
                                float ang = Mathf.Atan2(rz, rx);
                                // Using rx,rz for noise ensures irregularity follows rotation
                                float n1 = Mathf.PerlinNoise(ang * 2.5f + b.seed * 0.1f, 0f);
                                float n2 = Mathf.PerlinNoise(ang * 5.0f + b.seed * 0.1f + 13.5f, 0f) * 0.5f;
                                float n3 = Mathf.PerlinNoise(ang * 12.0f + b.seed * 0.1f + 27.1f, 0f) * 0.25f;
                                float noiseVal = (n1 + n2 + n3) / 1.75f;
                                float fac = 1f + (noiseVal - 0.5f) * 2f * b.irreg;
                                radEff *= Mathf.Max(0.2f, fac);
                            }

                            if (dist < radEff) {
                                // Deformation
                                Vector2 disp = Vector2.zero;
                                if (b.def > 0f) {
                                    float defScale = 5f / Mathf.Max(radM, 0.01f);
                                    float nx = Mathf.PerlinNoise(rx * defScale + b.seed, rz * defScale);
                                    float ny = Mathf.PerlinNoise(rx * defScale, rz * defScale + b.seed);
                                    disp = new Vector2(nx-0.5f, ny-0.5f) * 2f * b.def * radM;
                                }
                                
                                Vector2 distortedPos = new Vector2(rx, rz) - disp;
                                float dDist = distortedPos.magnitude;
                                float t = dDist / radEff;
                                
                                if (t < 1f) {
                                    float fP = 1f; 
                                    if(b.varProf > 0f) {
                                        float sp = 8f/Mathf.Max(radM, 0.01f);
                                        float np = Mathf.PerlinNoise(rx*sp + b.seed*0.2f, rz*sp + b.seed*0.2f);
                                        fP = 1f + (np - 0.5f) * 2f * b.varProf;
                                    }
                                    
                                    float fS = (t <= b.plano) ? 1f : 1f - Mathf.Pow((t - b.plano) / (1f - b.plano), b.suav);
                                    float profUsable = Mathf.Min(b.prof, bacheSettings.profundidadMaximaGlobal);
                                    float contribution = profUsable * fS * fP;
                                    
                                    // SMOOTH MAX (Soft Blending) - Exponential Smooth Maximum (LogSumExp variant approx)
                                    // sumDepth = ln(e^sum + e^contrib) / k
                                    // Or Polynomial smin cubic:
                                    // h = max(k - abs(a-b), 0.0) / k
                                    // res = max(a,b) + h*h*h*k*(1.0/6.0);
                                    
                                    float a = sumDepth;
                                    float bVal = contribution;
                                    
                                    // Using a simplified Cubic Smooth Max
                                    // k = blending distance in depth units (e.g. 0.1m)
                                    float k = 0.5f * bacheSettings.depthMultiplier * depthGlobal; 
                                    float h = Mathf.Max(k - Mathf.Abs(a - bVal), 0f) / k;
                                    sumDepth = Mathf.Max(a, bVal) + h * h * h * k * (1f / 6f);
                                }
                            }
                        }
                        y = -Mathf.Min(sumDepth, bacheSettings.profundidadMaximaGlobal) * borderFactor * depthGlobal;
                    }

                // Important: Vertex Position uses AABB coordinates (Mesh is flat and axis-aligned)
                vertices[index] = new Vector3(currentX, y, currentZ);
                uvs[index] = new Vector2((float)x / aabbPx, (float)z / aabbPz);
                index++;
            }
        }

        int[] triangles = new int[aabbPx * aabbPz * 6];
        int tIdx = 0;
        for (int z = 0; z < aabbPz; z++)
        {
            for (int x = 0; x < aabbPx; x++)
            {
                int i = z * (aabbPx + 1) + x;
                triangles[tIdx++] = i;
                triangles[tIdx++] = i + aabbPx + 1;
                triangles[tIdx++] = i + 1;
                triangles[tIdx++] = i + 1;
                triangles[tIdx++] = i + aabbPx + 1;
                triangles[tIdx++] = i + aabbPx + 2;
            }
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // ─── GENERATE CROCODILE (Voronoi Cracks) ────────────────────────────────

    private Mesh GenerateCrocodileMesh(int seed, float width, float length, float rotationDegrees, int px, int pz)
    {
        // Component logic removed
        Random.InitState(seed);

        // AABB Calc
        float rad = rotationDegrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);
        float aabbWidth = Mathf.Abs(width * cos) + Mathf.Abs(length * sin);
        float aabbLength = Mathf.Abs(width * sin) + Mathf.Abs(length * cos);
        
        // Recalculate resolution to MAINTAIN TOTAL POLY COUNT
        float totalPolys = px * pz;
        float aspect = (aabbLength > 0.001f) ? aabbWidth / aabbLength : 1f;
        
        int aabbPx = Mathf.RoundToInt(Mathf.Sqrt(totalPolys * aspect));
        int aabbPz = Mathf.RoundToInt(Mathf.Sqrt(totalPolys / aspect));
        
        aabbPx = Mathf.Clamp(aabbPx, 2, 500);
        aabbPz = Mathf.Clamp(aabbPz, 2, 500);
        
        float invCos = Mathf.Cos(-rad);
        float invSin = Mathf.Sin(-rad);

        float avgSize = (width + length) * 0.5f;

        float minCrackWidth = avgSize * (crocSettings.minCrackWidthPercent / 100f);
        float maxCrackWidth = avgSize * (crocSettings.maxCrackWidthPercent / 100f);
        
        // Determinar cantidad de celdas basado en el seed
        int cellCount = Random.Range(crocSettings.minCellCount, crocSettings.maxCellCount + 1);

        // Generar semillas Voronoi (En espacio local original)
        List<Vector2> seeds = new List<Vector2>();
        for (int i = 0; i < cellCount; i++)
        {
            seeds.Add(new Vector2(
                Random.Range(-width * 0.5f, width * 0.5f),
                Random.Range(-length * 0.5f, length * 0.5f)
            ));
        }
        Mesh mesh = new Mesh { name = "Proc_Croc_Voronoi_Baked" };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        float stepX = aabbWidth / aabbPx;
        float stepZ = aabbLength / aabbPz;
        float halfW = aabbWidth * 0.5f;
        float halfL = aabbLength * 0.5f;

        
        Vector3[] vertices = new Vector3[(aabbPx + 1) * (aabbPz + 1)];
        Vector2[] uvs = new Vector2[vertices.Length];
        Color[] colors = new Color[vertices.Length]; 
        int index = 0;

        for (int z = 0; z <= aabbPz; z++)
        {
            float currentZ = z * stepZ - halfL;
            for (int x = 0; x <= aabbPx; x++)
            {
                float currentX = x * stepX - halfW;

                // Transform to local space (Unrotated)
                float localX = currentX * invCos - currentZ * invSin;
                float localZ = currentX * invSin + currentZ * invCos;

                float y = 0f;
                colors[index] = Color.black; 

                    // 1. Domain Warping (Distorsionar espacio)
                    float ns = crocSettings.noiseScale;
                    float warpX = (Mathf.PerlinNoise(localX * ns, localZ * ns) - 0.5f) * crocSettings.bordeMax * 2f;
                    float warpZ = (Mathf.PerlinNoise(localX * ns + 55.1f, localZ * ns + 22.3f) - 0.5f) * crocSettings.bordeMax * 2f;
                    
                    float wx = localX + warpX;
                    float wz = localZ + warpZ;

                    // 2. Distancia al borde
                    float dx = (width * 0.5f) - Mathf.Abs(wx);
                    float dz = (length * 0.5f) - Mathf.Abs(wz);

                    float radius = Mathf.Min(width, length) * 0.25f;
                    float qx = Mathf.Max(Mathf.Abs(wx) - (width * 0.5f - radius), 0f);
                    float qz = Mathf.Max(Mathf.Abs(wz) - (length * 0.5f - radius), 0f);
                    float distToEdge = radius - Mathf.Sqrt(qx * qx + qz * qz);
                    if (qx == 0 && qz == 0) distToEdge = Mathf.Min(dx, dz);

                    // 3. Margen orgánico
                    float minBorderDist = distToEdge - crocSettings.bordeMin;
                    
                    float borderFactor = 0f;
                    if (minBorderDist > 0f) {
                        float tBorder = Mathf.Clamp01(minBorderDist / (crocSettings.bordeMax * crocSettings.bordeSuavidad));
                        borderFactor = tBorder * tBorder * (3f - 2f * tBorder); // Smoothstep
                    }

                    if (borderFactor > 0.001f)
                    {
                        // Inside Original Bounds & Border
                        // Call the Premium Calculation Logic
                        float vMask = 0f;
                        float depthVal = CalculateVoronoiDepth(localX, localZ, seeds, minCrackWidth, maxCrackWidth, seed, out vMask);
                        
                        y = depthVal * depthGlobal * borderFactor; // Apply Border Factor
                        colors[index] = new Color(vMask * borderFactor, 0.5f + (y*2f), 0f, 1f); 
                    }

                vertices[index] = new Vector3(currentX, y, currentZ);
                uvs[index] = new Vector2((float)x / aabbPx, (float)z / aabbPz);
                index++;
            }
        }

        // Triangles (same grid logic)
        int[] triangles = new int[aabbPx * aabbPz * 6];
        int tIdx = 0;
        for (int z = 0; z < aabbPz; z++)
        {
            for (int x = 0; x < aabbPx; x++)
            {
                int i = z * (aabbPx + 1) + x;
                triangles[tIdx++] = i;
                triangles[tIdx++] = i + aabbPx + 1;
                triangles[tIdx++] = i + 1;
                triangles[tIdx++] = i + 1;
                triangles[tIdx++] = i + aabbPx + 1;
                triangles[tIdx++] = i + aabbPx + 2;
            }
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.colors = colors; 
        mesh.uv = uvs;
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        return mesh;
    }

    private float CalculateVoronoiDepth(float x, float z, List<Vector2> seeds, float minCrackWidth, float maxCrackWidth, int instanceSeed, out float vertexMask)
    {
        vertexMask = 0f;
        float origX = x;
        float origZ = z;

        // 1. Distorsión de dominio (Bite & Serration) - Ajustado para evitar 'blobbing'
        if (crocSettings.edgeBiteAmount > 0f || crocSettings.edgeSerration > 0f)
        {
            // Usamos un factor de escala más pequeño para el warp para no destruir la topología
            float b1 = Mathf.PerlinNoise(x * crocSettings.edgeBiteScale, z * crocSettings.edgeBiteScale);
            float s1 = Mathf.PerlinNoise(x * crocSettings.serrationScale, z * crocSettings.serrationScale) * crocSettings.edgeSerration;
            
            float combinedBite = (b1 * 0.5f + s1 * 0.5f);
            
            // Reducimos el multiplicador para mantener líneas afiladas
            float warpFactor = (crocSettings.edgeBiteAmount + crocSettings.edgeSerration) * 0.05f; 
            x += (combinedBite - 0.5f) * warpFactor;
            z += (Mathf.PerlinNoise(x * crocSettings.edgeBiteScale + 123f, z * crocSettings.edgeBiteScale + 456f) - 0.5f) * warpFactor;
        }

        if (crocSettings.edgeIrregularity > 0f)
        {
            float warpX = (Mathf.PerlinNoise(x * crocSettings.irregularityScale, z * crocSettings.irregularityScale) - 0.5f) * crocSettings.edgeIrregularity;
            float warpZ = (Mathf.PerlinNoise(x * crocSettings.irregularityScale + 15.3f, z * crocSettings.irregularityScale + 15.3f) - 0.5f) * crocSettings.edgeIrregularity;
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

            switch (crocSettings.distanceMetric)
            {
                case CrocodileConfig.MetricType.Euclidean: d = Mathf.Sqrt(dx * dx + dz * dz); break;
                case CrocodileConfig.MetricType.Chebyshev: d = Mathf.Max(dx, dz); break;
                case CrocodileConfig.MetricType.Minkowski: d = Mathf.Pow(Mathf.Pow(dx, crocSettings.minkowskiP) + Mathf.Pow(dz, crocSettings.minkowskiP), 1f / crocSettings.minkowskiP); break;
            }

            if (d < d1) { d2 = d1; d1 = d; closestSeed = seed; }
            else if (d < d2) { d2 = d; }
        }

        float distToEdge = d2 - d1;

        // 3. Variación de celda (Tilt y Height Offset)
        float cellHash = (closestSeed.x * 123.456f + closestSeed.y * 456.789f) % 1.0f;
        float cellOffset = (cellHash - 0.5f) * crocSettings.cellHeightVariation;
        // Tilt simple basado en la distancia al centro de la celda
        float tiltX = (x - closestSeed.x) * (cellHash * 2f - 1f) * crocSettings.cellTiltAmount;
        float tiltZ = (z - closestSeed.y) * (Mathf.Cos(cellHash * 10f) * 2f - 1f) * crocSettings.cellTiltAmount;
        float totalCellEffect = cellOffset + tiltX + tiltZ;

        // 4. Variación de ancho
        float noise = Mathf.PerlinNoise(x * crocSettings.widthNoiseScale + instanceSeed, z * crocSettings.widthNoiseScale + instanceSeed);
        float currentCrackWidth = Mathf.Lerp(minCrackWidth, maxCrackWidth, noise);
        
        // Usamos SOLO el ancho real de la grieta, sin "influence radius"
        // Esto evita que grietas cercanas se fusionen visualmente

        // 5. Lip & Rounding logic
        float edgeEffect = 0f;
        float outerRadius = currentCrackWidth + crocSettings.lipWidth;
        
        if (distToEdge < outerRadius && distToEdge > currentCrackWidth)
        {
            float tEdge = (distToEdge - currentCrackWidth) / crocSettings.lipWidth;
            
            // Bulge (Upward lip)
            float bulge = Mathf.Sin(tEdge * Mathf.PI) * crocSettings.lipHeight;
            
            // Rounding (Downward break at the very edge)
            float rounding = -Mathf.Exp(-tEdge * 10f) * crocSettings.edgeRoundingDepth;
            
            edgeEffect = bulge + rounding;
        }

        // Usamos currentCrackWidth en lugar de influenceRadius para el check
        if (distToEdge > currentCrackWidth)
        {
            return totalCellEffect + edgeEffect; 
        }

        // 6. Profundidad con SDF suave (Smooth Distance Field)
        float dNoise = Mathf.PerlinNoise(x * crocSettings.depthNoiseScale + instanceSeed + 500f, z * crocSettings.depthNoiseScale + instanceSeed + 500f);
        float depthMod = Mathf.Lerp(1f - crocSettings.depthVariation, 1f, dNoise);
        
        // Normalizamos la distancia respecto al ancho real de la grieta
        float t = distToEdge / currentCrackWidth;
        
        // SDF Profile con SmoothStep SOLO dentro del ancho de la grieta
        float profile = 0f;
        if (distToEdge <= currentCrackWidth)
        {
            // Dentro de la grieta: profundidad completa con transición suave
            float innerT = distToEdge / currentCrackWidth;
            profile = Mathf.SmoothStep(1f, 0f, innerT - crocSettings.edgeSmoothness);
        }
        
        // Aplicamos la profundidad con clamp en el fondo para uniformidad
        float rawDepth = crocSettings.crackDepth * profile * depthMod;
        float finalDepth = Mathf.Max(rawDepth, profile > 0.5f ? crocSettings.minFloorDepth : 0f);

        // 7. Roughness y Detail
        float detail = 0f;
        if (crocSettings.pittingAmount > 0f || crocSettings.microRoughness > 0f)
        {
            float stoneNoise = Mathf.PerlinNoise(origX * crocSettings.microScale, origZ * crocSettings.microScale);
            float p = Mathf.PerlinNoise(origX * crocSettings.pittingScale + 77f, origZ * crocSettings.pittingScale + 77f);
            
            float edgeBoost = Mathf.Lerp(1.5f, 4f, 1f - t); 
            if (p > (0.88f / edgeBoost)) detail -= (p - (0.88f / edgeBoost)) * crocSettings.pittingAmount * 20f;
            
            detail += (stoneNoise - 0.5f) * crocSettings.microRoughness;
            if (stoneNoise > 0.7f) detail += (stoneNoise - 0.7f) * crocSettings.stoneHighlight; 
        }

        // Contrasted AO
        vertexMask = profile; 
        return (totalCellEffect - finalDepth + detail + edgeEffect);
    }

    // ─── GENERATE RAJADURA (Procedural Linear Crack) ──────────────────────────

    private Mesh GenerateRajaduraMesh(int seedOffset, RajaduraConfig config, float width, float length, float rotationDegrees, int px, int pz)
    {
        Random.InitState(Seed + seedOffset + 999); 
        float avgSize = (width + length) * 0.5f;
        
        // AABB Calc
        float rad = rotationDegrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);
        float aabbWidth = Mathf.Abs(width * cos) + Mathf.Abs(length * sin);
        float aabbLength = Mathf.Abs(width * sin) + Mathf.Abs(length * cos);
        
        // Recalculate resolution to MAINTAIN TOTAL POLY COUNT
        float totalPolys = px * pz;
        float aspect = (aabbLength > 0.001f) ? aabbWidth / aabbLength : 1f;
        
        int aabbPx = Mathf.RoundToInt(Mathf.Sqrt(totalPolys * aspect));
        int aabbPz = Mathf.RoundToInt(Mathf.Sqrt(totalPolys / aspect));

        aabbPx = Mathf.Clamp(aabbPx, 2, 500);
        aabbPz = Mathf.Clamp(aabbPz, 2, 500);

        float invCos = Mathf.Cos(-rad);
        float invSin = Mathf.Sin(-rad);

        // Generar Backbone (En espacio local original)
        List<Vector2> pathPoints = new List<Vector2>();
        int pathRes = config.segmentCount;
        float seedWiggle = Random.Range(0f, 1000f);

        for (int i = 0; i <= pathRes; i++)
        {
            float t = (float)i / pathRes;
            float z = t * length - (length * 0.5f);
            float x = (Mathf.PerlinNoise(t * config.wiggleScale, seedWiggle) - 0.5f) * 2f * (width * config.wiggleAmplitude);
            pathPoints.Add(new Vector2(x, z));
        }

        // Generar Ramas
        List<BranchInfo> branches = new List<BranchInfo>();
        if (config.branchProbability > 0f)
        {
            for (int i = 1; i < pathRes; i++)
            {
                if (Random.value < config.branchProbability)
                {
                    float angle = Random.Range(-config.branchAngle, config.branchAngle) * Mathf.Deg2Rad;
                    Vector2 dir = pathPoints[i] - pathPoints[i - 1];
                    dir.Normalize();
                    Vector2 perpDir = new Vector2(-dir.y, dir.x);
                    Vector2 branchDir = (dir * Mathf.Cos(angle) + perpDir * Mathf.Sin(angle)).normalized;
                    
                    branches.Add(new BranchInfo
                    {
                        startSegment = i,
                        direction = branchDir,
                        length = Random.Range(config.minBranchLengthPercent, config.maxBranchLengthPercent) * avgSize / 100f,
                        width = avgSize * (config.maxCrackWidthPercent / 100f) * config.branchWidthPercent
                    });
                }
            }
        }

        Mesh mesh = new Mesh { name = "Unified_Rajadura_Baked" };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        
        float stepX = aabbWidth / aabbPx;
        float stepZ = aabbLength / aabbPz;
        float halfW = aabbWidth * 0.5f;
        float halfL = aabbLength * 0.5f;

        Vector3[] vertices = new Vector3[(aabbPx + 1) * (aabbPz + 1)];
        Vector2[] uvs = new Vector2[vertices.Length];
        Color[] colors = new Color[vertices.Length];
        int idx = 0;

        for (int z = 0; z <= aabbPz; z++)
        {
            float currentZ = z * stepZ - halfL;
            for (int x = 0; x <= aabbPx; x++)
            {
                float currentX = x * stepX - halfW;
                
                // Transform to local
                float localX = currentX * invCos - currentZ * invSin;
                float localZ = currentX * invSin + currentZ * invCos;
                
                // Inside Bounds Check
                float borderFactor = 0f;
                // Simplified bounds check for baked mesh
                if (Mathf.Abs(localX) <= width * 0.5f && Mathf.Abs(localZ) <= length * 0.5f)
                {
                    // Calculate internal borders or assume 1 inside
                    borderFactor = 1f; 
                }

                float heightOffset = 0f;
                float vMask = 0f;
                if (borderFactor > 0.001f)
                {
                    // Use localX, localZ for noise generation
                    heightOffset = CalculatePremiumDepthRajadura(localX, localZ, config, seedOffset, avgSize, width, pathPoints, branches, out vMask);
                }

                float crackDepth = avgSize * (config.crackDepthPercent / 100f);
                float finalH = heightOffset * borderFactor;
                finalH = Mathf.Max(finalH, -crackDepth * 1.5f);

                // Use currentX (world relative for mesh) but localX logic for height
                vertices[idx] = new Vector3(currentX, finalH, currentZ);
                uvs[idx] = new Vector2((float)x / aabbPx, (float)z / aabbPz);
                colors[idx] = new Color(vMask * borderFactor, 0.5f + (heightOffset * 2f), 0f, 1f);
                idx++;
            }
        }

        int[] tris = new int[aabbPx * aabbPz * 6];
        int ti = 0;
        for (int z = 0; z < aabbPz; z++) {
            for (int x = 0; x < aabbPx; x++) {
                int v = z * (aabbPx + 1) + x;
                tris[ti++] = v; tris[ti++] = v + aabbPx + 1; tris[ti++] = v + 1;
                tris[ti++] = v + 1; tris[ti++] = v + aabbPx + 1; tris[ti++] = v + aabbPx + 2;
            }
        }

        mesh.vertices = vertices;
        mesh.colors = colors;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private float CalculatePremiumDepthRajadura(float x, float z, RajaduraConfig c, int seedOffset, float avgSize, float width, List<Vector2> pathPoints, List<BranchInfo> branches, out float vertexMask)
    {
        vertexMask = 0f;
        float origX = x; float origZ = z;

        // 1. Distorsión
        if (c.edgeBiteAmount > 0f || c.edgeSerration > 0f || c.edgeIrregularity > 0f)
        {
            float b1 = Mathf.PerlinNoise(x * c.edgeBiteScale, z * c.edgeBiteScale) * c.edgeBiteAmount;
            float s1 = Mathf.PerlinNoise(x * c.serrationScale, z * c.serrationScale) * c.edgeSerration;
            float i1 = (Mathf.PerlinNoise(x * c.irregularityScale, z * c.irregularityScale) - 0.5f) * c.edgeIrregularity;
            float combinedWarp = (b1 * 1.2f + s1 * 0.8f + i1 * 0.6f) * 0.25f;
            x += combinedWarp;
            z += (Mathf.PerlinNoise(x * 25f + 10f, z * 25f + 10f) - 0.5f) * combinedWarp;
        }

        // 2. Distancia a Path
        int segIdx = 0;
        float distToPath = GetDistToPathRajadura(x, z, pathPoints, out segIdx);

        // 3. Variación Segmento
        float segHash = (float)(segIdx * 123.456f + Seed + seedOffset) % 1.0f;
        float cellHeightVariation = avgSize * (c.cellHeightVariationPercent / 100f);
        float cellTiltAmount = avgSize * (c.cellTiltAmountPercent / 100f);
        float cellOffset = (segHash - 0.5f) * cellHeightVariation;
        
        float sideSign = (x > pathPoints[segIdx].x) ? 1f : -1f;
        float tiltX = sideSign * cellTiltAmount * (segHash * 2f - 1f);
        float totalCellEffect = cellOffset + (tiltX * (distToPath / width));

        // 4. Variación Ancho
        float wNoise = Mathf.PerlinNoise(x * c.widthNoiseScale + Seed + seedOffset, z * c.widthNoiseScale + Seed + seedOffset);
        if (c.harshTransitions > 0f) {
            float stepped = Mathf.Floor(wNoise * 5f) / 5f;
            wNoise = Mathf.Lerp(wNoise, stepped, c.harshTransitions);
        }
        if (c.segmentVisibility > 0f) {
            float segNoise = Mathf.PerlinNoise(x * c.segmentCutScale, z * c.segmentCutScale);
            if (segNoise > (1f - c.segmentVisibility * 0.3f)) wNoise *= 0.3f;
        }
        float dynWidth = Mathf.Lerp(avgSize * (c.minCrackWidthPercent / 100f), avgSize * (c.maxCrackWidthPercent / 100f), wNoise);

        // Ramas
        float minDistToBranch = float.MaxValue;
        float closestBranchWidth = 0f;
        foreach (var branch in branches)
        {
            Vector2 branchStart = pathPoints[branch.startSegment];
            Vector2 branchEnd = branchStart + branch.direction * branch.length;
            float distSq = DistToSegmentSq(new Vector2(x, z), branchStart, branchEnd);
            float dist = Mathf.Sqrt(distSq);
            if (dist < minDistToBranch) {
                minDistToBranch = dist;
                closestBranchWidth = branch.width;
            }
        }
        bool isInBranch = minDistToBranch < closestBranchWidth;
        float effectiveDistToPath = isInBranch ? minDistToBranch : distToPath;
        float effectiveWidth = isInBranch ? closestBranchWidth : dynWidth;

        // 5. Lips
        float lipHeight = avgSize * (c.lipHeightPercent / 100f);
        float lipWidth = avgSize * (c.lipWidthPercent / 100f);
        float edgeRoundingDepth = avgSize * (c.edgeRoundingDepthPercent / 100f);
        
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

        // 6. Profundidad
        float crackDepth = avgSize * (c.crackDepthPercent / 100f);
        float minFloorDepth = avgSize * (c.minFloorDepthPercent / 100f);
        float dNoise = Mathf.PerlinNoise(x * c.depthNoiseScale + Seed + seedOffset + 500f, z * c.depthNoiseScale + Seed + seedOffset + 500f);
        float dMod = Mathf.Lerp(1f - c.depthVariation, 1f, dNoise);
        
        float tInner = effectiveDistToPath / effectiveWidth;
        float profile = Mathf.SmoothStep(1f, 0f, tInner - c.edgeSmoothness);
        float rawD = crackDepth * profile * dMod;
        float finalD = Mathf.Max(rawD, profile > 0.5f ? minFloorDepth : 0f);

        // 7. Detail
        float pittingAmount = avgSize * (c.pittingAmountPercent / 100f);
        float microRoughness = avgSize * (c.microRoughnessPercent / 100f);
        float detail = 0f;
        if (pittingAmount > 0f || microRoughness > 0f)
        {
            float stoneNoise = Mathf.PerlinNoise(origX * c.microScale, origZ * c.microScale);
            float p = Mathf.PerlinNoise(origX * c.pittingScale, origZ * c.pittingScale);
            if (p > 0.85f) detail -= (p - 0.85f) * pittingAmount * 15f;
            detail += (stoneNoise - 0.5f) * microRoughness;
            if (stoneNoise > 0.7f) detail += (stoneNoise - 0.7f) * c.stoneHighlight;
        }

        vertexMask = profile;
        return (totalCellEffect + edgeEffect - finalD + detail);
    }

    private float GetDistToPathRajadura(float px, float pz, List<Vector2> pathPoints, out int closestSegment)
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

    // Helper for Point-Segment Distance Squared
    private float DistToSegmentSq(Vector2 p, Vector2 v, Vector2 w) 
    {
        float l2 = (v - w).sqrMagnitude;
        if (l2 == 0) return (p - v).sqrMagnitude;
        float t = Mathf.Max(0, Mathf.Min(1, Vector2.Dot(p - v, w - v) / l2));
        Vector2 projection = v + t * (w - v);
        return (p - projection).sqrMagnitude;
    }
}
