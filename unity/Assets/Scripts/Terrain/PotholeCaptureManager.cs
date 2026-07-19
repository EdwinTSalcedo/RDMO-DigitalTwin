using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class PotholeCaptureManager : MonoBehaviour
{
    [Header("Dependencies")]
    public TerrainPotholeGenerator potholeGenerator;
    public Camera targetCamera;

    [Tooltip("Generadores de prefabs que se regeneran en cada ciclo con semilla propia.")]
    public List<PrefabObjectGenerator> prefabGenerators = new List<PrefabObjectGenerator>();

    [Header("Movement Settings")]
    public float movementSpeed = 5f;
    public float minHeight = 0.5f;
    public float maxHeight = 25f;

    [Header("Capture Settings")]
    public float autoInterval = 2.0f;
    [Range(0f, 1f)] public float minVisibilityPercentage = 0.35f;
    [Tooltip("Porcentaje MÍNIMO para que un bache sea detectado. 0.35 = 35%. " +
        "BAJO = Más permisivo. Se aplica DESPUÉS de filtrar áreas planas.")]
    public Vector2Int resolution = new Vector2Int(1270, 950);
    [Tooltip("Escala del Bounding Box (1 = Ajustado, 0.8 = Más pequeño, 1.2 = Más holgado)")]
    [Range(0.1f, 2f)] public float boundingBoxScale = 1.0f;
    
    [Header("Dead Area Filtering")]
    [Tooltip("Detectar y eliminar áreas planas/muertas en bordes de baches")]
    public bool enableDeadAreaFiltering = true;
    [Tooltip("Umbral para detectar normales planas. 0.95 = casi horizontal (solo elimina lo MUY plano). " +
        "ALTO = Más permisivo con baches legales. Predeterminado: 0.95")]
    [Range(0.5f, 0.99f)] public float flatSurfaceThreshold = 0.95f;
    [Tooltip("Reducción mínima de volumen para activar filtrado (0.30 = 30%). " +
        "Si el bounds se reduce por <30%, NO se filtra (bache legítimo). ALTO = Más conservador.")]
    [Range(0.01f, 0.5f)] public float minVolumeReduction = 0.30f;
    [Tooltip("Usar método avanzado con análisis de varianza en bordes")]
    public bool useAdvancedEdgeAnalysis = false;
    
    public Color colorPothole = Color.cyan;
    public Color colorCrocodile = Color.yellow;
    public Color colorRajadura = Color.magenta;
    public Color colorPerson = Color.green;
    public Color colorTag = Color.blue;

    [System.Serializable]
    public class CaptureElement
    {
        public string tag;
        public int classId;
        public string className;
        public Color boxColor;
    }

    [Header("Additional Elements to Capture")]
    [Tooltip("Agrega aquí otros tags que quieras capturar (e.g. Trash, Cone).")]
    public List<CaptureElement> additionalElements = new List<CaptureElement>();

    [Header("UI & Navigation")]
    public string menuScene = "Mode_Menu";

    [Header("Multi-Height Capture")]
    [Tooltip("Alturas a capturar en modo automático (en metros)")]
    public List<float> captureHeights = new List<float> { 15f, 20f, 25f };
    [Tooltip("Habilitar captura multi-altura automática")]
    public bool enableMultiHeightCapture = true;

    private bool isAutoMode = false;
    private Coroutine autoCoroutine;
    private float currentCaptureHeight = 0f;  // Altura actual en captura multi-altura

    // Movement state for UI buttons
    private bool isMovingUp = false;
    private bool isMovingDown = false;

    // ─── REUSABLE CAPTURE ASSETS ───
    private RenderTexture captureRT;
    private Texture2D texClean;
    private Texture2D texAnnotated;

    private void PrepareCaptureAssets()
    {
        if (captureRT == null || captureRT.width != resolution.x || captureRT.height != resolution.y)
        {
            // Limpiar recursos OLD completamente ANTES de crear nuevos
            if (captureRT != null)
            {
                captureRT.Release();
                Destroy(captureRT);
                captureRT = null;
            }
            if (texClean != null)
            {
                Destroy(texClean);
                texClean = null;
            }
            if (texAnnotated != null)
            {
                Destroy(texAnnotated);
                texAnnotated = null;
            }
            
            // Force GPU memory cleanup
            Graphics.SetRenderTarget(null);
            RenderTexture.active = null;

            // Crear nuevos recursos
            captureRT = new RenderTexture(resolution.x, resolution.y, 24);
            captureRT.name = "CaptureRT";
            captureRT.wrapMode = TextureWrapMode.Clamp;
            
            texClean = new Texture2D(resolution.x, resolution.y, TextureFormat.RGB24, false);
            texAnnotated = new Texture2D(resolution.x, resolution.y, TextureFormat.RGB24, false);
        }
    }

    void Start()
    {
        if (targetCamera == null) targetCamera = GetComponent<Camera>();
        if (potholeGenerator == null) potholeGenerator = Object.FindFirstObjectByType<TerrainPotholeGenerator>();
        
        if (targetCamera == null) Debug.LogError("CaptureManager: No Camera found!");
        if (potholeGenerator == null) Debug.LogWarning("CaptureManager: No TerrainPotholeGenerator found in scene!");

        // Configurar carpeta temporal para guardar capturas
        SetupTemporaryFolder();
    }

    private void SetupTemporaryFolder()
    {
        string tempFolder = @"E:\PotholeCaptureData";
        
        FileHandler.SetCustomFolder(tempFolder);
        Debug.Log($"<color=cyan>[PotholeCaptureManager] Carpeta temporal configurada: {tempFolder}</color>");
    }

    private void SetupFolderForHeight(float height)
    {
        string baseFolder = @"E:\PotholeCaptureData";
        string heightFolder = System.IO.Path.Combine(baseFolder, $"{height:F0}meters");
        
        // Crear carpeta si no existe
        if (!System.IO.Directory.Exists(heightFolder))
        {
            System.IO.Directory.CreateDirectory(heightFolder);
            Debug.Log($"<color=cyan>[Height Capture] Carpeta creada: {heightFolder}</color>");
        }
        
        FileHandler.SetCustomFolder(heightFolder);
        currentCaptureHeight = height;
        Debug.Log($"<color=yellow>[Height Capture] Capturando a altura: {height}m</color>");
    }

    void Update()
    {
        HandleHeightMovement();
    }

    void HandleHeightMovement()
    {
        float move = 0;
        if (isMovingUp || Input.GetKey(KeyCode.UpArrow)) move = 1;
        if (isMovingDown || Input.GetKey(KeyCode.DownArrow)) move = -1;

        if (move != 0)
        {
            Vector3 pos = transform.position;
            pos.y += move * movementSpeed * Time.deltaTime;
            pos.y = Mathf.Clamp(pos.y, minHeight, maxHeight);
            transform.position = pos;
        }
    }

    // ─── UI BUTTON FUNCTIONS ────────────────────────────────────────────────

    public void StartMovingUp() => isMovingUp = true;
    public void StopMovingUp() => isMovingUp = false;
    
    public void StartMovingDown() => isMovingDown = true;
    public void StopMovingDown() => isMovingDown = false;

    public void UIManualGenerate()
    {
        if (!isAutoMode)
        {
            RandomizeAndGenerate();
        }
        else
        {
            Debug.LogWarning("Cannot generate manually while Auto Mode is active. Turn it off first.");
        }
    }

    // CaptureScreenshot() and ToggleAutoMode() are also ready to be used by UI buttons

    public void ReturnToMenu()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(menuScene);
    }

    private int seedCounter = 0; // Add counter to ensure uniqueness

    public void RandomizeAndGenerate()
    {
        if (potholeGenerator == null)
        {
            Debug.LogError("<color=red>[ERROR] potholeGenerator is NULL! Assign it in the Inspector.</color>");
            return;
        }

        long ticks = System.DateTime.Now.Ticks;

        // ── Baches ────────────────────────────────────────────────────────────
        int oldSeed = potholeGenerator.Seed;
        potholeGenerator.Seed = (int)((ticks & 0x7FFFFFFF) + seedCounter);
        seedCounter++;
        int newSeed = potholeGenerator.Seed;

        Debug.Log($"<color=green>[SEED CHANGE] {oldSeed} → {newSeed}</color>");
        potholeGenerator.Generate();
        Debug.Log($"<color=green>[GENERATION] Complete with Seed: {newSeed}</color>");

        // ── Prefab Generators (semilla propia por cada uno) ───────────────────
        if (prefabGenerators != null)
        {
            for (int i = 0; i < prefabGenerators.Count; i++)
            {
                PrefabObjectGenerator gen = prefabGenerators[i];
                if (gen == null) continue;

                // Semilla independiente: base diferente a la de los baches
                gen.seed = (int)(((ticks >> 8) & 0x7FFFFFFF) + seedCounter * 31 + i * 1000003);
                gen.Generate();
            }
        }
    }


    public void CaptureScreenshot()
    {
        StartCoroutine(CaptureRoutine());
    }

    private IEnumerator CaptureRoutine()
    {
        yield return new WaitForEndOfFrame();

        PrepareCaptureAssets();

        RenderTexture previousRT = targetCamera.targetTexture;
        targetCamera.targetTexture = captureRT;

        try
        {
            // 2. CAPTURA LIMPIA (Clean)
            targetCamera.Render();
            RenderTexture.active = captureRT;
            texClean.ReadPixels(new Rect(0, 0, resolution.x, resolution.y), 0, 0);
            texClean.Apply();
            RenderTexture.active = null;

            string timeID = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string filename = $"Capture_{timeID}_{Random.Range(0, 1000)}";

            // 3. CALCULAR DATOS (Txt) - con limpieza de arrays temporales
            List<BoundingBoxInfo> boxes = GenerateYOLOAnnotations(filename);
            
            // 4. VISUALIZAR EN ESCENA (UI Canvas)
            GameObject canvasObj = CreateVisualizationCanvas(boxes);
            
            // 5. CAPTURA ANOTADA (Annotated)
            targetCamera.Render();
            RenderTexture.active = captureRT;
            texAnnotated.ReadPixels(new Rect(0, 0, resolution.x, resolution.y), 0, 0);
            texAnnotated.Apply();
            RenderTexture.active = null;

            // 6. GUARDAR ARCHIVOS - Mejor manejo de memoria
            byte[] bytesClean = texClean.EncodeToPNG();
            byte[] bytesAnnotated = (boxes.Count > 0) ? texAnnotated.EncodeToPNG() : null;

            // Pre-cargamos la ruta en el hilo principal
            string folderPath = FileHandler.GetCurrentFolderPath();

            // Usar una acción local para capturar solo lo necesario y liberar referencias rápido
            System.Threading.Tasks.Task saveTask = System.Threading.Tasks.Task.Run(() => 
            {
                try
                {
                    string fullPathClean = System.IO.Path.Combine(folderPath, filename + ".png");
                    System.IO.File.WriteAllBytes(fullPathClean, bytesClean);

                    if (bytesAnnotated != null)
                    {
                        string fullPathAnn = System.IO.Path.Combine(folderPath, filename + "_annotated.png");
                        System.IO.File.WriteAllBytes(fullPathAnn, bytesAnnotated);
                    }
                }
                finally
                {
                    // Liberar referencias explícitamente al terminar la tarea
                    bytesClean = null;
                    bytesAnnotated = null;
                }
            });

            // 7. LIMPIEZA INMEDIATA
            Destroy(canvasObj);
            
            // Limpiar boxes list que ya no necesitamos
            boxes.Clear();
            boxes = null;

            // Yield hasta que la tarea de guardado se complete (sin bloquear)
            while (!saveTask.IsCompleted)
            {
                yield return null;
            }

            // Liberar memoria local
            bytesClean = null;
            bytesAnnotated = null;

            // Forzar limpieza cada 3 capturas para evitar acumulación
            if (Random.Range(0, 3) == 0) 
            {
                yield return null;  // Dar un frame para que Unity limpie
                Resources.UnloadUnusedAssets();
                System.GC.Collect(0, System.GCCollectionMode.Optimized);
            }

            Debug.Log($"<color=cyan>Capture Complete: {filename}</color>");
        }
        finally
        {
            // Restaurar cámara original garantizado
            RenderTexture.active = null;
            Graphics.SetRenderTarget(null);
            if (targetCamera != null)
                targetCamera.targetTexture = previousRT;
        }
    }

    // ─── VISUALIZATION HELPERS ───────────────────────────────────────────────

    private GameObject CreateVisualizationCanvas(List<BoundingBoxInfo> boxes)
    {
        if (boxes.Count == 0) return null;

        // 1. Crear Canvas
        GameObject canvasObj = new GameObject("Temp_Vis_Canvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = targetCamera;
        canvas.planeDistance = 1f; // Justo delante de la cámara

        // Ajustar Scaler para que coincida 1:1 con la resolución de captura
        UnityEngine.UI.CanvasScaler scaler = canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(resolution.x, resolution.y);
        scaler.matchWidthOrHeight = 0.5f;

        // GraphicRaycaster para optimización
        GraphicRaycaster raycaster = canvasObj.AddComponent<GraphicRaycaster>();
        
        // 2. Crear Cajas - Optimizadas
        foreach (var box in boxes)
        {
            CreateBoxUI(canvasObj.transform, box);
        }

        return canvasObj;
    }

    private void CreateBoxUI(Transform parent, BoundingBoxInfo box)
    {
        // Contenedor del rect
        GameObject boxObj = new GameObject($"Box_{box.className}");
        boxObj.transform.SetParent(parent, false);
        
        RectTransform rectD = boxObj.AddComponent<RectTransform>();
        
        // CORRECCIÓN DE COORDENADAS:
        // box.screenRect viene en formato Imagen (Origen Top-Left)
        // Unity UI usa formato Cartesiano (Origen Bottom-Left)
        
        // X es igual (Left -> Right)
        float xMin = box.screenRect.x / resolution.x;
        float xMax = (box.screenRect.x + box.screenRect.width) / resolution.x;
        
        // Y hay que invertirlo
        // Top de la imagen (y=0) -> Top del Canvas (anchor=1)
        // Bottom de la imagen (y=res.y) -> Bottom del Canvas (anchor=0)
        
        float yTop_Image = box.screenRect.y;
        float yBottom_Image = box.screenRect.y + box.screenRect.height;
        
        float yMax = 1f - (yTop_Image / resolution.y);      // Top Anchor
        float yMin = 1f - (yBottom_Image / resolution.y);   // Bottom Anchor

        rectD.anchorMin = new Vector2(xMin, yMin);
        rectD.anchorMax = new Vector2(xMax, yMax);
        rectD.offsetMin = Vector2.zero;
        rectD.offsetMax = Vector2.zero;

        // Bordes (4 Imagenes)
        float thickness = 2f; // Grosor en píxeles
        Color c = box.color;

        // Top Border
        CreateLine(boxObj.transform, "Top", 
            new Vector2(0, 1), new Vector2(1, 1), // Anchors: Top Edge
            new Vector2(0, -thickness), new Vector2(0, 0), c); // Height = thickness, downwards

        // Bottom Border
        CreateLine(boxObj.transform, "Bottom", 
            new Vector2(0, 0), new Vector2(1, 0), // Anchors: Bottom Edge
            new Vector2(0, 0), new Vector2(0, thickness), c); // Height = thickness, upwards

        // Left Border
        CreateLine(boxObj.transform, "Left", 
            new Vector2(0, 0), new Vector2(0, 1), // Anchors: Left Edge
            new Vector2(0, 0), new Vector2(thickness, 0), c); // Width = thickness, rightwards

        // Right Border
        CreateLine(boxObj.transform, "Right", 
            new Vector2(1, 0), new Vector2(1, 1), // Anchors: Right Edge
            new Vector2(-thickness, 0), new Vector2(0, 0), c); // Width = thickness, leftwards

        // Etiqueta (Label)
        CreateLabel(boxObj.transform, box.className, c);
    }

    private void CreateLine(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color c)
    {
        GameObject line = new GameObject(name);
        line.transform.SetParent(parent, false);
        RectTransform rt = line.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        
        UnityEngine.UI.Image img = line.AddComponent<UnityEngine.UI.Image>();
        img.color = c;
        img.raycastTarget = false;
    }

    private void CreateLabel(Transform parent, string textStr, Color c)
    {
        // Panel para el texto
        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(parent, false);
        
        RectTransform labelRect = labelObj.AddComponent<RectTransform>();
        // Posicionar arriba-izquierda del box
        labelRect.anchorMin = new Vector2(0, 1); 
        labelRect.anchorMax = new Vector2(0, 1);
        labelRect.pivot = new Vector2(0, 0); // Pivote abajo-izquierda (para crecer hacia arriba)
        labelRect.anchoredPosition = new Vector2(0, 0); // Pegado al borde superior
        
        // Tamaño fijo aproximado para el fondo
        labelRect.sizeDelta = new Vector2(100, 20); 

        // Fondo semi-transparente
        UnityEngine.UI.Image bg = labelObj.AddComponent<UnityEngine.UI.Image>();
        bg.color = new Color(0,0,0, 0.7f);
        bg.raycastTarget = false;

        // Texto
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(labelObj.transform, false);
        UnityEngine.UI.Text text = textObj.AddComponent<UnityEngine.UI.Text>();
        text.text = textStr;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.color = c;
        // text.pixelsPerUnitMultiplier = 1; // Removed: Not available in standard Text
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = new Vector2(5,0); // Padding left
        text.rectTransform.offsetMax = Vector2.zero;
        text.alignment = TextAnchor.MiddleLeft;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 10;
        text.resizeTextMaxSize = 14;
        text.raycastTarget = false;
    }

    private struct BoundingBoxInfo
    {
        public int classId;
        public string className;
        public Rect screenRect;
        public Color color;
    }

    private List<BoundingBoxInfo> GenerateYOLOAnnotations(string filename)
    {
        List<BoundingBoxInfo> boxes = new List<BoundingBoxInfo>();
        
        if (potholeGenerator == null) return boxes;

        List<string> annotations = new List<string>();
        
        // Buscar todos los baches generados en la escena - CACHEAR REFERENCES LOCALES
        GameObject[] potholes = null;
        GameObject[] crocodiles = null;
        GameObject[] rajaduras = null;
        GameObject[] persons = null;
        GameObject[] tagObjs = null;

        try
        {
            potholes = GameObject.FindGameObjectsWithTag("Pothole");
            crocodiles = GameObject.FindGameObjectsWithTag("Crocodile");
            rajaduras = GameObject.FindGameObjectsWithTag("Crack");
            persons = GameObject.FindGameObjectsWithTag("Person");
            tagObjs = GameObject.FindGameObjectsWithTag("Car");

            Debug.Log($"<color=orange>[Capture] Objects Found - Potholes: {potholes.Length}, Crocodiles: {crocodiles.Length}, Cracks: {rajaduras.Length}, Persons: {persons.Length}, Cars: {tagObjs.Length}</color>");

            // Procesar baches normales (clase 0)
            foreach (var pothole in potholes)
            {
                var boxInfo = GetBoundingBoxInfo(pothole, 0, "Bache", colorPothole);
                if (boxInfo.HasValue)
                {
                    boxes.Add(boxInfo.Value);
                }
            }

            // Procesar cocodrilos (clase 1)
            foreach (var croc in crocodiles)
            {
                var boxInfo = GetBoundingBoxInfo(croc, 1, "Cocodrilo", colorCrocodile);
                if (boxInfo.HasValue)
                {
                    boxes.Add(boxInfo.Value);
                }
            }

            // Procesar rajaduras (clase 2)
            foreach (var raj in rajaduras)
            {
                var boxInfo = GetBoundingBoxInfo(raj, 2, "Crack", colorRajadura);
                if (boxInfo.HasValue)
                {
                    boxes.Add(boxInfo.Value);
                }
            }

            // Procesar Personas (clase 3)
            foreach (var p in persons)
            {
                var boxInfo = GetBoundingBoxInfo(p, 3, "Person", colorPerson);
                if (boxInfo.HasValue)
                {
                    boxes.Add(boxInfo.Value);
                }
            }

            // Procesar Tags (clase 4)
            foreach (var t in tagObjs)
            {
                var boxInfo = GetBoundingBoxInfo(t, 4, "Car", colorTag);
                if (boxInfo.HasValue)
                {
                    boxes.Add(boxInfo.Value);
                }
            }

            // Procesar elementos adicionales
            if (additionalElements != null)
            {
                foreach (var element in additionalElements)
                {
                    GameObject[] objs = GameObject.FindGameObjectsWithTag(element.tag);
                    try
                    {
                        foreach (var obj in objs)
                        {
                            var boxInfo = GetBoundingBoxInfo(obj, element.classId, element.className, element.boxColor);
                            if (boxInfo.HasValue)
                            {
                                boxes.Add(boxInfo.Value);
                            }
                        }
                    }
                    finally
                    {
                        objs = null;  // Limpiar reference
                    }
                }
            }
        }
        finally
        {
            // Limpiar todos los arrays de búsqueda
            potholes = null;
            crocodiles = null;
            rajaduras = null;
            persons = null;
            tagObjs = null;
        }

        // ──── FILTRAR OCLUSIONES: Recortar baches que estén parcialmente cubiertos ────
        boxes = FilterAndClipOccludedBoxes(boxes);

        // ──── FILTRAR BOXES MUY PEQUEÑOS ────
        boxes = FilterSmallBoxes(boxes, minAreaPercent: 0.001f);  // Mínimo 0.1% del área de la imagen

        // Guardar anotaciones
        foreach (var box in boxes)
        {
            annotations.Add(FormatYOLOAnnotation(box));
        }

        if (annotations.Count > 0)
        {
            string annotationText = string.Join("\n", annotations);
            FileHandler.SaveAnnotation(annotationText, filename + ".txt");
        }

        annotations.Clear();
        annotations = null;

        return boxes;
    }

    private BoundingBoxInfo? GetBoundingBoxInfo(GameObject obj, int classId, string className, Color color)
    {
        Bounds bounds = new Bounds();
        bool boundsInitialized = false;

        // Priorizar BoxColliders para Personas y Coches si existen
        if (className == "Person" || className == "Car")
        {
            BoxCollider[] colliders = obj.GetComponentsInChildren<BoxCollider>();
            if (colliders.Length > 0)
            {
                bounds = colliders[0].bounds;
                boundsInitialized = true;
                for (int i = 1; i < colliders.Length; i++)
                {
                    bounds.Encapsulate(colliders[i].bounds);
                }
            }
        }

        // Fallback a Renderers si no se inicializaron los bounds (o para otras clases)
        if (!boundsInitialized)
        {
            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return null;

            bounds = renderers[0].bounds;
            boundsInitialized = true;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
        }

        // ─── SMART EDGE FILTERING: Detectar y excluir áreas planas/muertas en bordes ───
        Bounds cleanedBounds = bounds; // Default: usar bounds original
        if (className == "Bache" || className == "Cocodrilo" || className == "Crack")
        {
            // Usar thresholds DIFERENTES según tipo de daño
            // Baches: conservador (flatSurfaceThreshold normal) - preserva daño legítimo
            // Cocodrilos/Cracks: agresivo (0.82) - rechaza superficiales
            float thresholdForThisType = (className == "Bache") ? flatSurfaceThreshold : 0.82f;
            cleanedBounds = DetectAndRemoveDeadAreas(obj, bounds, className, thresholdForThisType);
            if (cleanedBounds.size.magnitude < bounds.size.magnitude * 0.05f) return null; // Si queda MUY poco (<5%), descartar
        }
        
        // Obtener las 8 esquinas del bounding box en espacio mundial (USAR BOUNDS ORIGINAL PARA PROJECTION)
        Vector3[] corners = new Vector3[8];
        corners[0] = bounds.min;
        corners[1] = new Vector3(bounds.min.x, bounds.min.y, bounds.max.z);
        corners[2] = new Vector3(bounds.min.x, bounds.max.y, bounds.min.z);
        corners[3] = new Vector3(bounds.min.x, bounds.max.y, bounds.max.z);
        corners[4] = new Vector3(bounds.max.x, bounds.min.y, bounds.min.z);
        corners[5] = new Vector3(bounds.max.x, bounds.min.y, bounds.max.z);
        corners[6] = new Vector3(bounds.max.x, bounds.max.y, bounds.min.z);
        corners[7] = bounds.max;

        // Proyectar esquinas a coordenadas de pantalla (píxeles)
        float minX = float.MaxValue;
        float minY = float.MaxValue;
        float maxX = float.MinValue;
        float maxY = float.MinValue;
        bool hasVisibleVertex = false;

        foreach (var corner in corners)
        {
            // Convertir de mundo a viewport (0-1)
            Vector3 viewportPoint = targetCamera.WorldToViewportPoint(corner);
            
            // Verificar si está delante de la cámara
            if (viewportPoint.z > 0)
            {
                hasVisibleVertex = true;
                
                // Convertir viewport a píxeles
                float pixelX = viewportPoint.x * resolution.x;
                float pixelY = (1f - viewportPoint.y) * resolution.y; // Invertir Y
                
                minX = Mathf.Min(minX, pixelX);
                minY = Mathf.Min(minY, pixelY);
                maxX = Mathf.Max(maxX, pixelX);
                maxY = Mathf.Max(maxY, pixelY);
            }
        }

        if (!hasVisibleVertex) return null;
        
        // --- OCCLUSION CHECK ---
        // Verificar qué tan visible es el objeto considerando obstrucciones
        // IMPORTANTE: Para baches, usar los bounds LIMPIOS (sin áreas planas)
        Bounds boundsForOcclusion = (className == "Bache" || className == "Cocodrilo" || className == "Crack") ? cleanedBounds : bounds;
        float visibilityFactor = GetVisibilityFactor(obj, boundsForOcclusion, className);
        
        // Umbral MÁS PERMISIVO: Solo rechazar si está MUY occluido
        // Baches legítimos: rechazar solo si <15% visible
        // Cocodrilos/Cracks: rechazar si <20% visible
        float minVisibility = (className == "Crack" || className == "Cocodrilo") ? 0.20f : 0.15f;
        if (visibilityFactor < minVisibility) 
        {
            Debug.Log($"<color=red>[Visibility REJECT] {className}: {visibilityFactor*100:F1}% visible < {minVisibility*100:F0}%</color>");
            return null;
        }

        // --- VISIBILITY CHECK ---
        float rawWidth = maxX - minX;
        float rawHeight = maxY - minY;
        float rawArea = rawWidth * rawHeight;

        // Clampear a los límites de la imagen
        float clampedMinX = Mathf.Clamp(minX, 0, resolution.x);
        float clampedMinY = Mathf.Clamp(minY, 0, resolution.y);
        float clampedMaxX = Mathf.Clamp(maxX, 0, resolution.x);
        float clampedMaxY = Mathf.Clamp(maxY, 0, resolution.y);

        float visibleWidth = clampedMaxX - clampedMinX;
        float visibleHeight = clampedMaxY - clampedMinY;
        
        // Verificar que el bounding box tenga un tamaño mínimo
        if (visibleWidth < 1 || visibleHeight < 1) return null;

        if (rawArea > 0)
        {
            float visibleArea = visibleWidth * visibleHeight;
            float ratio = visibleArea / rawArea;

            // Descartar si se ve menos del % configurado
            // NOTA: Para baches/daños, este threshold se aplica al área TOTAL
            // El área de daño REAL se filtró en DetectAndRemoveDeadAreas
            if (ratio < minVisibilityPercentage) return null;
        }

        // Crear rectángulo en coordenadas de píxeles
        // Para autos y personas, usar escala completa; para daños, aplicar el scale configurado
        float finalScale = (className == "Car" || className == "Person") ? 1.0f : boundingBoxScale;
        float finalWidth = visibleWidth * finalScale;
        float finalHeight = visibleHeight * finalScale;

        float centerX = clampedMinX + (visibleWidth * 0.5f);
        float centerY = clampedMinY + (visibleHeight * 0.5f);

        float finalMinX = centerX - (finalWidth * 0.5f);
        float finalMinY = centerY - (finalHeight * 0.5f);

        Rect screenRect = new Rect(
            finalMinX,
            finalMinY,
            finalWidth,
            finalHeight
        );

        return new BoundingBoxInfo
        {
            classId = classId,
            className = className,
            screenRect = screenRect,
            color = color
        };
    }

    /// <summary>
    /// Detecta y elimina áreas muertas (planas/uniformes) en los bordes del mesh del bache.
    /// Si un área en el borde es plana (sin variación de altura), se considera "basura" y se excluye.
    /// </summary>
    private Bounds DetectAndRemoveDeadAreas(GameObject obj, Bounds originalBounds, string className, float customThreshold = -1f)
    {
        if (!enableDeadAreaFiltering) return originalBounds;

        // Usar threshold personalizado si se proporciona, de lo contrario usar el global
        float thresholdToUse = (customThreshold > 0) ? customThreshold : flatSurfaceThreshold;

        // Usar método avanzado si está habilitado
        if (useAdvancedEdgeAnalysis)
        {
            return DetectAndRemoveDeadAreasAdvanced(obj, originalBounds, className, thresholdToUse);
        }

        MeshFilter[] meshFilters = obj.GetComponentsInChildren<MeshFilter>();
        if (meshFilters.Length == 0) return originalBounds;

        List<Vector3> significantVertices = new List<Vector3>();
        
        // Recolectar vértices del mesh
        foreach (var mf in meshFilters)
        {
            if (mf.mesh == null) continue;
            
            Vector3[] vertices = mf.mesh.vertices;
            Vector3[] normals = mf.mesh.normals;
            
            // Transformar a espacio mundial
            Matrix4x4 localToWorld = mf.transform.localToWorldMatrix;
            
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 worldPos = localToWorld.MultiplyPoint3x4(vertices[i]);
                Vector3 normal = localToWorld.MultiplyVector(normals[i]).normalized;
                
                // Filtrar: Solo incluir vértices que tengan una normal con componente Y significativa
                // (es decir, vértices que no estén en superficies planas horizontales)
                float yNormal = Mathf.Abs(normal.y);
                
                if (yNormal < thresholdToUse)  // Normal no es casi horizontal (usar threshold personalizado)
                {
                    significantVertices.Add(worldPos);
                }
            }
        }

        if (significantVertices.Count == 0) return originalBounds;

        // Calcular bounds solo de vértices significativos (no planos)
        Bounds cleanedBounds = new Bounds(significantVertices[0], Vector3.zero);
        for (int i = 1; i < significantVertices.Count; i++)
        {
            cleanedBounds.Encapsulate(significantVertices[i]);
        }

        // Si el bounds se redujo significativamente, significa que había áreas planas grandes
        float originalVolume = originalBounds.size.x * originalBounds.size.y * originalBounds.size.z;
        float cleanedVolume = cleanedBounds.size.x * cleanedBounds.size.y * cleanedBounds.size.z;
        
        if (originalVolume > 0)
        {
            float reductionPercent = (originalVolume - cleanedVolume) / originalVolume;
            
            if (reductionPercent > minVolumeReduction)  // Si se redujo más del umbral configurado
            {
                Debug.Log($"<color=yellow>[DeadAreas] {className}: Reducción de {reductionPercent*100:F1}% - Áreas planas detectadas y removidas</color>");
                return cleanedBounds;
            }
        }

        return originalBounds;
    }

    /// <summary>
    /// Alternativa avanzada: Analiza el mesh para detectar vértices de borde que están en superficies planas uniformes.
    /// Usa análisis de varianza de altura en zonas de borde.
    /// </summary>
    private Bounds DetectAndRemoveDeadAreasAdvanced(GameObject obj, Bounds originalBounds, string className, float thresholdToUse)
    {
        MeshFilter[] meshFilters = obj.GetComponentsInChildren<MeshFilter>();
        if (meshFilters.Length == 0) return originalBounds;

        List<Vector3> allVertices = new List<Vector3>();
        
        foreach (var mf in meshFilters)
        {
            if (mf.mesh == null) continue;
            
            Vector3[] vertices = mf.mesh.vertices;
            Matrix4x4 localToWorld = mf.transform.localToWorldMatrix;
            
            foreach (var v in vertices)
            {
                allVertices.Add(localToWorld.MultiplyPoint3x4(v));
            }
        }

        if (allVertices.Count < 10) return originalBounds;

        // Detectar vértices en bordes (por coordenada X y Z - horizontales)
        float minX = Mathf.Infinity, maxX = -Mathf.Infinity;
        float minZ = Mathf.Infinity, maxZ = -Mathf.Infinity;
        
        foreach (var v in allVertices)
        {
            minX = Mathf.Min(minX, v.x);
            maxX = Mathf.Max(maxX, v.x);
            minZ = Mathf.Min(minZ, v.z);
            maxZ = Mathf.Max(maxZ, v.z);
        }

        float edgeThreshold = 0.2f;  // 20% desde el borde se considera "zona de borde"
        float edgeDistX = (maxX - minX) * edgeThreshold;
        float edgeDistZ = (maxZ - minZ) * edgeThreshold;

        List<Vector3> coreVertices = new List<Vector3>();
        List<Vector3> edgeVertices = new List<Vector3>();

        foreach (var v in allVertices)
        {
            bool isXEdge = (v.x - minX < edgeDistX) || (maxX - v.x < edgeDistX);
            bool isZEdge = (v.z - minZ < edgeDistZ) || (maxZ - v.z < edgeDistZ);

            if (isXEdge || isZEdge)
            {
                edgeVertices.Add(v);
            }
            else
            {
                coreVertices.Add(v);
            }
        }

        // Analizar varianza de altura en zona de bordes
        if (edgeVertices.Count > 5)
        {
            float edgeHeightMean = 0;
            foreach (var v in edgeVertices) edgeHeightMean += v.y;
            edgeHeightMean /= edgeVertices.Count;

            float edgeHeightVariance = 0;
            foreach (var v in edgeVertices)
            {
                edgeHeightVariance += (v.y - edgeHeightMean) * (v.y - edgeHeightMean);
            }
            edgeHeightVariance /= edgeVertices.Count;
            float edgeHeightStdDev = Mathf.Sqrt(edgeHeightVariance);

            // Si la desviación estándar es muy baja, significa que es un área plana uniforme
            float totalHeight = maxZ - minZ + maxX - minX;  // Aproximación
            if (edgeHeightStdDev < totalHeight * 0.05f)  // Muy poco cambio de altura
            {
                Debug.Log($"<color=yellow>[DeadAreas Advanced] {className}: Bordes uniformes detectados (StdDev: {edgeHeightStdDev:F3})</color>");
                
                // Usar solo vértices del core
                if (coreVertices.Count > 0)
                {
                    Bounds cleanedBounds = new Bounds(coreVertices[0], Vector3.zero);
                    foreach (var v in coreVertices)
                    {
                        cleanedBounds.Encapsulate(v);
                    }
                    return cleanedBounds;
                }
            }
        }

        return originalBounds;
    }

    private string FormatYOLOAnnotation(BoundingBoxInfo boxInfo)
    {
        // Las coordenadas ya están en píxeles, normalizamos a 0-1 para YOLO
        float x_center = (boxInfo.screenRect.x + boxInfo.screenRect.width / 2f) / resolution.x;
        float y_center = (boxInfo.screenRect.y + boxInfo.screenRect.height / 2f) / resolution.y;
        float width = boxInfo.screenRect.width / resolution.x;
        float height = boxInfo.screenRect.height / resolution.y;

        // YOLO usa origen arriba-izquierda, nuestras coords ya están en ese sistema
        return $"{boxInfo.classId} {x_center:F6} {y_center:F6} {width:F6} {height:F6}";
    }

    private Texture2D DrawBoundingBoxes(Texture2D original, List<BoundingBoxInfo> boxes)
    {
        Texture2D result = new Texture2D(original.width, original.height, TextureFormat.RGB24, false);
        result.SetPixels(original.GetPixels());

        foreach (var box in boxes)
        {
            // Dibujar rectángulo
            DrawRect(result, box.screenRect, box.color, 3);
            
            // Dibujar etiqueta con fondo
            DrawLabel(result, box.className, new Vector2(box.screenRect.x, box.screenRect.y - 20), box.color);
        }

        result.Apply();
        return result;
    }

    private void DrawRect(Texture2D tex, Rect rect, Color color, int thickness)
    {
        // Dibujar los 4 lados del rectángulo
        for (int t = 0; t < thickness; t++)
        {
            // Top
            for (int x = (int)rect.x; x < rect.x + rect.width; x++)
                SetPixelSafe(tex, x, (int)rect.y + t, color);
            
            // Bottom
            for (int x = (int)rect.x; x < rect.x + rect.width; x++)
                SetPixelSafe(tex, x, (int)(rect.y + rect.height) - t, color);
            
            // Left
            for (int y = (int)rect.y; y < rect.y + rect.height; y++)
                SetPixelSafe(tex, (int)rect.x + t, y, color);
            
            // Right
            for (int y = (int)rect.y; y < rect.y + rect.height; y++)
                SetPixelSafe(tex, (int)(rect.x + rect.width) - t, y, color);
        }
    }

    private void DrawLabel(Texture2D tex, string text, Vector2 position, Color color)
    {
        // Dibujar fondo negro semi-transparente para la etiqueta
        int labelWidth = text.Length * 8;
        int labelHeight = 16;
        
        for (int x = 0; x < labelWidth; x++)
        {
            for (int y = 0; y < labelHeight; y++)
            {
                SetPixelSafe(tex, (int)position.x + x, (int)position.y + y, new Color(0, 0, 0, 0.7f));
            }
        }
        
        // Nota: Para texto real necesitarías una fuente bitmap o usar GUI.
        // Por ahora dibujamos un rectángulo de color con el nombre en el log
    }

    private void SetPixelSafe(Texture2D tex, int x, int y, Color color)
    {
        if (x >= 0 && x < tex.width && y >= 0 && y < tex.height)
        {
            tex.SetPixel(x, y, color);
        }
    }


    public void ToggleAutoMode()
    {
        isAutoMode = !isAutoMode;
        if (isAutoMode)
        {
            autoCoroutine = StartCoroutine(AutoCaptureLoop());
            Debug.Log("<color=green>Automatic Mode: ENABLED</color>");
        }
        else
        {
            if (autoCoroutine != null) StopCoroutine(autoCoroutine);
            Debug.Log("<color=red>Automatic Mode: DISABLED</color>");
        }
    }

    private bool AreGeneratorsBusy()
    {
        if (prefabGenerators == null) return false;
        foreach (var gen in prefabGenerators)
        {
            if (gen != null && gen.IsGenerating) return true;
        }
        return false;
    }

    private IEnumerator AutoCaptureLoop()
    {
        int captureCount = 0;
        
        while (isAutoMode)
        {
            // Si está habilitada captura multi-altura, generar UNA VEZ y capturar en cada altura
            if (enableMultiHeightCapture && captureHeights.Count > 0)
            {
                // GENERAR UNA SOLA VEZ
                RandomizeAndGenerate();

                // Esperar hasta que todos los generadores de elementos terminen
                yield return new WaitUntil(() => !AreGeneratorsBusy());
                yield return new WaitForSeconds(0.1f);

                // CAPTURAR EN CADA ALTURA (MISMA VERSION)
                foreach (float height in captureHeights)
                {
                    if (!isAutoMode) yield break;  // Salir si se desactiva el modo
                    
                    // Configurar la altura y crear subcarpeta
                    SetupFolderForHeight(height);
                    
                    // Ajustar la altura de la cámara
                    Vector3 camPos = targetCamera.transform.position;
                    camPos.y = height;
                    targetCamera.transform.position = camPos;
                    
                    yield return new WaitForSeconds(0.2f);  // Esperar a que se estabilice
                    
                    CaptureScreenshot();
                    
                    captureCount++;
                    
                    // Limpiar memoria DESPUÉS DE CADA CAPTURA para evitar acumulación
                    if (captureCount % 2 == 0)
                    {
                        Debug.Log($"<color=yellow>[Memory Cleanup] Limpieza después de captura {captureCount}</color>");
                        yield return null;
                        Resources.UnloadUnusedAssets();
                        System.GC.Collect(0, System.GCCollectionMode.Optimized);
                    }
                    
                    yield return new WaitForSeconds(autoInterval);
                }
            }
            else
            {
                // Modo tradicional (altura única)
                RandomizeAndGenerate();

                // Esperar hasta que todos los generadores de elementos terminen
                yield return new WaitUntil(() => !AreGeneratorsBusy());

                yield return new WaitForSeconds(0.1f);
                CaptureScreenshot();
                
                captureCount++;
                
                // Limpiar memoria DESPUÉS DE CADA CAPTURA para evitar acumulación
                if (captureCount % 2 == 0)
                {
                    Debug.Log($"<color=yellow>[Memory Cleanup] Limpieza después de captura {captureCount}</color>");
                    yield return null;
                    Resources.UnloadUnusedAssets();
                    System.GC.Collect(0, System.GCCollectionMode.Optimized);
                }
                
                yield return new WaitForSeconds(autoInterval);
            }
        }
    }

    private float GetVisibilityFactor(GameObject obj, Bounds bounds, string className)
    {
        Vector3 camPos = targetCamera.transform.position;
        Vector3 center = bounds.center;
        Vector3 ext = bounds.extents;

        // Para baches y daños, usar GRID DENSO para mejor detección de oclusión
        // Para autos y personas, usar menos puntos
        int gridSize = (className == "Bache" || className == "Cocodrilo" || className == "Crack") ? 3 : 2;
        
        List<Vector3> points = new List<Vector3>(gridSize * gridSize);
        
        // Generar grid de puntos en el bounds
        for (int x = 0; x < gridSize; x++)
        {
            for (int z = 0; z < gridSize; z++)
            {
                float xFactor = gridSize > 1 ? (float)x / (gridSize - 1) : 0.5f;
                float zFactor = gridSize > 1 ? (float)z / (gridSize - 1) : 0.5f;
                
                float xPos = center.x + (xFactor * 2 - 1) * ext.x * 0.8f;
                float zPos = center.z + (zFactor * 2 - 1) * ext.z * 0.8f;
                
                points.Add(new Vector3(xPos, center.y, zPos));
            }
        }

        int visibleCount = 0;
        
        // CACHEAR los GameObjects buscados para no hacer múltiples FindGameObjectsWithTag
        GameObject[] cars = null;
        GameObject[] persons = null;
        
        try
        {
            cars = GameObject.FindGameObjectsWithTag("Car");
            persons = GameObject.FindGameObjectsWithTag("Person");

            foreach (var p in points)
            {
                Vector3 dir = p - camPos;
                float dist = dir.magnitude;
                bool isVisible = false;

                if (Physics.Raycast(camPos, dir, out RaycastHit hit, dist + 0.1f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    // Visible si golpeamos el objeto o un hijo
                    if (hit.collider.gameObject == obj || hit.collider.transform.IsChildOf(obj.transform))
                    {
                        isVisible = true;
                    }
                    // Si golpea vehículo: revisar si está REALMENTE más cerca (más de 10cm)
                    else if (hit.collider.CompareTag("Car") || hit.collider.CompareTag("Person"))
                    {
                        // PERMISIVO: Solo rechazar si vehículo está CLARAMENTE más cerca (>10cm)
                        if (hit.distance < dist - 0.10f)
                        {
                            isVisible = false;  // Vehículo ocluye CLARAMENTE
                        }
                        else
                        {
                            isVisible = true;  // Al mismo nivel o muy cercano = visible
                        }
                    }
                    // Otros objetos: visible si está al mismo nivel
                    else if (hit.distance >= dist - 0.10f)
                    {
                        isVisible = true;
                    }
                }
                else
                {
                    // Sin obstáculos = visible
                    isVisible = true;
                }

                if (isVisible) visibleCount++;
            }
        }
        finally
        {
            // Limpiar referencias a arrays
            cars = null;
            persons = null;
            points.Clear();
            points = null;
        }

        float visibility = (float)visibleCount / gridSize / gridSize;
        Debug.Log($"<color=cyan>[Visibility] {className}: {visibility*100:F1}% ({visibleCount}/{gridSize*gridSize} puntos)</color>");
        return visibility;
    }

    /// <summary>
    /// Detecta si un bache está completamente occluido por autos o personas.
    /// </summary>
    private bool IsOccludedByVehicleOrPerson(Bounds bacheBounds)
    {
        Vector3 camPos = targetCamera.transform.position;
        Vector3 bacheCenter = bacheBounds.center;
        
        // Raycast hacia el centro del bache
        Vector3 dir = bacheCenter - camPos;
        float dist = dir.magnitude;

        if (Physics.Raycast(camPos, dir, out RaycastHit hit, dist + 0.1f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            // Verificar si el objeto que nos golpea es un auto o persona
            GameObject hitObj = hit.collider.gameObject;
            if (hitObj.CompareTag("Car") || hitObj.CompareTag("Person"))
            {
                // Está occluido por un auto o persona si el impacto es significativamente más cercano
                if (hit.distance < dist - 0.1f)
                {
                    return true;
                }
            }
        }
        
        return false;
    }

    /// <summary>
    /// Filtra y recorta boxes de baches que estén parcialmente occluidos por autos/personas.
    /// Solo recorta la parte que realmente está dentro del box del vehículo/persona.
    /// </summary>
    /// <summary>
    /// Filtra baches basado en cobertura de vehículos.
    /// Solo RECHAZA si está mayormente cubierto (>70%).
    /// Mantiene baches parcialmente cubiertos si la cobertura es <70%.
    /// NO recorta - mantiene el box completo.
    /// </summary>
    private List<BoundingBoxInfo> FilterAndClipOccludedBoxes(List<BoundingBoxInfo> boxes)
    {
        List<BoundingBoxInfo> result = new List<BoundingBoxInfo>();

        // Obtener rectangles de autos y personas
        List<Rect> vehicleRects = new List<Rect>();
        foreach (var box in boxes)
        {
            if (box.className == "Car" || box.className == "Person")
            {
                vehicleRects.Add(box.screenRect);
            }
        }

        // Procesar cada elemento
        foreach (var box in boxes)
        {
            // Autos y personas siempre se incluyen
            if (box.className == "Car" || box.className == "Person")
            {
                result.Add(box);
                continue;
            }

            // Solo procesar baches, rajaduras y cocodrilos
            if (box.className != "Bache" && box.className != "Crack" && box.className != "Cocodrilo")
            {
                result.Add(box);
                continue;
            }

            Rect damageRect = box.screenRect;
            float damageArea = damageRect.width * damageRect.height;

            if (damageArea <= 0)
            {
                continue;
            }

            // ──── CALCULAR ÁREA CUBIERTA POR VEHÍCULOS ────
            float totalCoveredArea = 0f;

            foreach (var vehicleRect in vehicleRects)
            {
                // Calcular intersección
                float x1 = Mathf.Max(damageRect.x, vehicleRect.x);
                float y1 = Mathf.Max(damageRect.y, vehicleRect.y);
                float x2 = Mathf.Min(damageRect.x + damageRect.width, vehicleRect.x + vehicleRect.width);
                float y2 = Mathf.Min(damageRect.y + damageRect.height, vehicleRect.y + vehicleRect.height);

                if (x2 > x1 && y2 > y1)
                {
                    float intersectionArea = (x2 - x1) * (y2 - y1);
                    totalCoveredArea += intersectionArea;
                }
            }

            // Calcular porcentaje de cobertura
            float coveragePercent = (damageArea > 0) ? (totalCoveredArea / damageArea) : 0f;

            // ──── CRITERIO DE FILTRADO ────
            // RECHAZAR SOLO si está CASI COMPLETAMENTE cubierto por vehículos
            // Baches: rechazar si >80% cubierto (casi completamente bajo auto)
            // Cocodrilos/Cracks: rechazar si >70% cubierto
            float rejectThreshold = (box.className == "Bache") ? 0.80f : 0.70f;
            
            if (coveragePercent > rejectThreshold)
            {
                Debug.Log($"<color=red>[Coverage] {box.className} rechazado: {coveragePercent*100:F1}% cubierto por vehículo (umbral: {rejectThreshold*100:F0}%)</color>");
                continue;  // Rechazar
            }

            // Si cobertura > 0%, informar que está parcialmente cubierto pero se mantiene
            if (coveragePercent > 0.001f)
            {
                Debug.Log($"<color=orange>[Coverage] {box.className} parcialmente cubierto: {coveragePercent*100:F1}% - MANTENIDO (umbral: {rejectThreshold*100:F0}%)</color>");
            }

            result.Add(box);
        }

        Debug.Log($"<color=yellow>[Occlusion Filter] Boxes procesados: {boxes.Count} -> {result.Count}</color>");
        return result;
    }

    /// <summary>
    /// Filtra boxes que son demasiado pequeños (ocupan muy poco area de la imagen).
    /// </summary>
    private List<BoundingBoxInfo> FilterSmallBoxes(List<BoundingBoxInfo> boxes, float minAreaPercent)
    {
        float imageArea = resolution.x * resolution.y;
        float minArea = imageArea * minAreaPercent;

        List<BoundingBoxInfo> result = new List<BoundingBoxInfo>(boxes.Count);

        foreach (var box in boxes)
        {
            float boxArea = box.screenRect.width * box.screenRect.height;
            
            if (boxArea >= minArea)
            {
                result.Add(box);
            }
            else
            {
                Debug.Log($"<color=red>[FilterSmall] {box.className} descartado: area={boxArea:F0} < min={minArea:F0}</color>");
            }
        }

        // Limpiar lista original
        boxes.Clear();
        return result;
    }

    /// <summary>
    /// Aplica Non-Maximum Suppression para eliminar boxes superpuestos.
    /// Mantiene los boxes más grandes y elimina los superpuestos más pequeños.
    /// </summary>
    private List<BoundingBoxInfo> ApplyNonMaximumSuppression(List<BoundingBoxInfo> boxes, float iouThreshold)
    {
        if (boxes.Count <= 1) return boxes;

        // Ordenar por área (tamaño) en orden descendente - boxes grandes primero
        List<BoundingBoxInfo> sortedBoxes = new List<BoundingBoxInfo>(boxes);
        sortedBoxes.Sort((a, b) => 
        {
            float areaA = a.screenRect.width * a.screenRect.height;
            float areaB = b.screenRect.width * b.screenRect.height;
            return areaB.CompareTo(areaA);  // Orden descendente
        });

        List<BoundingBoxInfo> result = new List<BoundingBoxInfo>();
        List<bool> suppress = new List<bool>(new bool[sortedBoxes.Count]);

        for (int i = 0; i < sortedBoxes.Count; i++)
        {
            if (suppress[i]) continue;

            result.Add(sortedBoxes[i]);

            // Comparar con los demás boxes
            for (int j = i + 1; j < sortedBoxes.Count; j++)
            {
                if (suppress[j]) continue;

                float iou = CalculateIoU(sortedBoxes[i].screenRect, sortedBoxes[j].screenRect);
                
                if (iou > iouThreshold)
                {
                    suppress[j] = true;  // Marcar como suprimido si el IoU es alto
                }
            }
        }

        Debug.Log($"<color=yellow>[NMS] Boxes reducidos de {boxes.Count} a {result.Count}</color>");
        return result;
    }

    /// <summary>
    /// Calcula la Intersección sobre Unión (IoU) entre dos rectángulos.
    /// </summary>
    private float CalculateIoU(Rect rect1, Rect rect2)
    {
        // Calcular intersección
        float x1 = Mathf.Max(rect1.x, rect2.x);
        float y1 = Mathf.Max(rect1.y, rect2.y);
        float x2 = Mathf.Min(rect1.x + rect1.width, rect2.x + rect2.width);
        float y2 = Mathf.Min(rect1.y + rect1.height, rect2.y + rect2.height);

        if (x2 < x1 || y2 < y1)
            return 0f;  // No hay intersección

        float intersection = (x2 - x1) * (y2 - y1);
        float area1 = rect1.width * rect1.height;
        float area2 = rect2.width * rect2.height;
        float union = area1 + area2 - intersection;

        return union > 0 ? intersection / union : 0f;
    }
}

