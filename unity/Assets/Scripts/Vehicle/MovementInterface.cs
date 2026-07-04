using UnityEngine;
using System.Collections;
using System.IO;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System;
using UnityEngine.AI;

public class MovementInterface : MonoBehaviour
{
    public DroneNavMeshController droneController;
    private NavMeshAgent navAgent; // Se obtiene automáticamente del droneController
    public Rigidbody velocidad;
    public GameObject angulo;
    private float speed;
    private float angle;
    public Direccion direction;
    public List<Camera> captureCameras = new List<Camera>();
    public string outputFolder = "CapturedImages";
    [Tooltip("Intervalo entre capturas de fotos (segundos). 0.6s a 10 m/s captura cada ~6m (misma densidad que antes a 3 m/s).")]
    public float captureInterval = 0.6f;
    
    [Header("Configuración de Rayos")]
    [Tooltip("Distancia base entre rayos. Se actualiza automáticamente según la altura (DroneHeightController).")]
    public float raySpacing = 0.5f;
    [Tooltip("Cantidad de columnas laterales de rayos para el barrido de captura/ground truth.")]
    [Min(1)] public int rayColumns = 8;
    [Tooltip("Cantidad de filas longitudinales de rayos para el barrido de captura/ground truth.")]
    [Min(1)] public int rayRows = 2;
    public bool useDynamicRaySpacing = false;
    // raySpacing es configurado por DroneHeightController según altura:
    // Low (6m): 0.5 | Medium (12m): 1.0 | High (18m): 1.5
    // Esto asegura detección más densa a baja altitud y menos densa a altura elevada.
    
    [Tooltip("Tiempo de espera desde que se detecta el bache hasta que se toma la foto (para centrar la toma).")]
    public float captureDelay = 0.15f;
    
    public TMP_Text buttonText;
    public TMP_Text velocityText;
    public TMP_Text angleText;
    public TMP_Text strategyAltitudeText;
    public TMP_Text trafficText;
    public TMP_Text heightText;
    public DroneHeightController heightController;
    public TrafficDensityController trafficController;

    public bool isCapturing = false;  // Público para que DroneController pueda verificar si está grabando.
    public bool angulo_mando;
    private float baseAngle;
    private bool baseAngleSet = false;
    private float timer = 0f;
    private int count;

    string timestamp;
    string folderPath;
    private float lastAngle;
    private const float TOLERANCIA = 0.1f;
    private bool initialized = false;
    public float toleranciaVelocidad = 0.1f;

    private HashSet<string> detectedPotholes = new HashSet<string>();
    private HashSet<string> groundTruthObjectsSet = new HashSet<string>();  // Objetos tocados por raycast.
    private int groundTruthCount = 0;

    // Skip mode: tracking de primera y segunda pasada.
    private HashSet<string> detectedPotholesFirstPass = new HashSet<string>();
    private HashSet<string> detectedPotholesSecondPass = new HashSet<string>();
    private bool isInSecondPass = false;  // Indica si estamos en revisita.

    // Segmentos: tracking por segmento individual dentro de una calle.
    private string currentSegmentName = "";  // Nombre del segmento activo
    private Dictionary<string, SegmentResult> segmentResultsMap = new Dictionary<string, SegmentResult>();
    private HashSet<string> segmentGroundTruthSet = new HashSet<string>();  // GT exclusivo del segmento
    private HashSet<string> segmentDetectedSet = new HashSet<string>();     // Detectados exclusivo del segmento
    private HashSet<string> segmentDetectedFirstPassSet = new HashSet<string>();
    private HashSet<string> segmentDetectedSecondPassSet = new HashSet<string>();
    private bool segmentHadObstacles = false;  // Indica si se detectó Car/Person durante el segmento.
    private float segmentStartTime = 0f;
    private float segmentStartEnergy = 100f;
    private int testModeDamageSequence = 0;
    
    /// <summary>Métricas acumuladas por segmento, accesibles desde ExperimentAutomator.</summary>
    public class SegmentResult
    {
        public string name;
        public int detectedByModel = 0;    // Confirmados por el modelo de IA
        public int detectedByRaycast = 0;  // Ground truth del raycast
        public int recoveredInSecondPass = 0;  // Solo para skip mode.
        public bool isSkipSegment = false;
        public bool hadObstacles = false;      // Indica si hubo Car/Person bloqueando.
        public float timeTaken = 0f;
        public float energyConsumed = 0f;

        /// <summary>Coverage = detecciones de primera pasada / raycast * 100.</summary>
        public float Coverage => (detectedByRaycast > 0) ? Mathf.Min(100f, 100f * detectedByModel / detectedByRaycast) : 0f;
        /// <summary>Recovery = detecciones nuevas de revisita / raycast * 100. No se suma al coverage.</summary>
        public float RecoveryRatio => (isSkipSegment && detectedByRaycast > 0) ? Mathf.Min(100f, 100f * recoveredInSecondPass / detectedByRaycast) : 0f;
    }

    // Compatibilidad legacy.
    private Dictionary<string, object> segmentMetricsMap = new Dictionary<string, object>();
    private bool isRecordingSegment = false;

    // Solo lectura en el inspector
    [SerializeField, TextArea(5, 20)]
    private string visiblePotholes;

    private string currentPothole = null;

    /// <summary>Devuelve la cantidad de baches detectados hasta ahora.</summary>
    public int GetDetectedPotholesCount()
    {
        return detectedPotholes.Count;
    }

    /// <summary>Devuelve el ground truth total.</summary>
    public int GetGroundTruthCount()
    {
        return groundTruthCount;
    }

    /// <summary>Resetea todos los contadores globales y de segmentos (llamar al inicio de cada calle/episodio).</summary>
    public void ResetDetectedPotholes()
    {
        detectedPotholes.Clear();
        groundTruthObjectsSet.Clear();
        groundTruthCount = 0;
        detectedPotholesFirstPass.Clear();
        detectedPotholesSecondPass.Clear();
        isInSecondPass = false;
        segmentResultsMap.Clear();
        segmentGroundTruthSet.Clear();
        segmentDetectedSet.Clear();
        segmentDetectedFirstPassSet.Clear();
        segmentDetectedSecondPassSet.Clear();
        currentSegmentName = "";
        Debug.Log("[MovementInterface] Todos los contadores reiniciados");
    }

    // API de segmentos llamada desde ExperimentAutomator.

    /// <summary>
    /// Inicia el tracking de un segmento. Captura el estado de energía inicial
    /// y limpia los conjuntos exclusivos del segmento.
    /// </summary>
    public void StartSegment(string segName, float currentEnergy)
    {
        currentSegmentName = segName;
        segmentStartTime = Time.time;
        segmentStartEnergy = currentEnergy;
        segmentGroundTruthSet.Clear();
        segmentDetectedSet.Clear();
        segmentDetectedFirstPassSet.Clear();
        segmentDetectedSecondPassSet.Clear();
        segmentHadObstacles = false;
        testModeDamageSequence = 0;
        isInSecondPass = false;
        isRecordingSegment = true;
        Debug.Log($"[Segment] Iniciando segmento: {segName}");
    }

    /// <summary>Indica si hubo Car/Person detectado durante el segmento activo.</summary>
    public bool GetSegmentHadObstacles() => segmentHadObstacles;

    /// <summary>Finaliza el tracking del segmento activo y guarda su SegmentResult.</summary>
    public SegmentResult EndSegment(float currentEnergy)
    {
        if (!isRecordingSegment || string.IsNullOrEmpty(currentSegmentName))
        {
            Debug.LogWarning("[Segment] EndSegment llamado sin segmento activo.");
            return null;
        }

        HashSet<string> recoveredSet = new HashSet<string>(segmentDetectedSecondPassSet);
        recoveredSet.ExceptWith(segmentDetectedFirstPassSet);

        var result = new SegmentResult
        {
            name                  = currentSegmentName,
            detectedByModel       = segmentDetectedFirstPassSet.Count,
            detectedByRaycast     = segmentGroundTruthSet.Count,
            recoveredInSecondPass = recoveredSet.Count,
            isSkipSegment         = droneController != null && droneController.navigationMode == NavigationMode.Skip,
            hadObstacles          = segmentHadObstacles,
            timeTaken             = Time.time - segmentStartTime,
            energyConsumed        = Mathf.Max(0f, segmentStartEnergy - currentEnergy)
        };

        segmentResultsMap[currentSegmentName] = result;
        isRecordingSegment = false;
        Debug.Log($"[Segment] Segmento '{currentSegmentName}' - Coverage={result.Coverage:F1}% " +
                  $"({result.detectedByModel}/{result.detectedByRaycast}) | " +
                  $"Recovery={result.RecoveryRatio:F1}% | Obstáculos={segmentHadObstacles}");
        return result;
    }

    /// <summary>Devuelve todos los resultados de segmento del episodio actual.</summary>
    public List<SegmentResult> GetAllSegmentResults()
    {
        return new List<SegmentResult>(segmentResultsMap.Values);
    }

    /// <summary>Registra un objeto en el ground truth del segmento activo.
    /// Si el tag es Car o Person activa el flag hadObstacles.</summary>
    public void RegisterSegmentGroundTruth(string objectName, string tag = "")
    {
        if (!isRecordingSegment) return;
        segmentGroundTruthSet.Add(objectName);
        if (tag == "Car" || tag == "Person")
            segmentHadObstacles = true;
    }

    /// <summary>Registra un bache CONFIRMADO por el modelo IA en el segmento activo.
    /// Solo debe llamarse desde PythonInferenceClient cuando la IA confirma la detección.</summary>
    public void RegisterSegmentDetection(string objectName)
    {
        // Solo registrar si el segmento sigue activo para evitar que
        // respuestas tardías de Python contaminen el siguiente segmento.
        if (!isRecordingSegment) return;

        segmentDetectedSet.Add(objectName);

        bool belongsToSecondPass = isInSecondPass && !detectedPotholesFirstPass.Contains(objectName);

        if (belongsToSecondPass)
            segmentDetectedSecondPassSet.Add(objectName);
        else
            segmentDetectedFirstPassSet.Add(objectName);
    }

    /// <summary>Marca el inicio de la segunda pasada (Skip revisit).</summary>
    public void MarkSecondPassStart()
    {
        isInSecondPass = true;
        detectedPotholesFirstPass = new HashSet<string>(detectedPotholes);
        detectedPotholesSecondPass.Clear();
        Debug.Log($"[MovementInterface] Segunda pasada iniciada. Baches primera pasada: {detectedPotholesFirstPass.Count}");
    }

    /// <summary>Obtiene baches nuevos detectados en segunda pasada (global del episodio).</summary>
    public int GetRecoveredPotholesCount()
    {
        if (!isInSecondPass) return 0;
        
        HashSet<string> recovered = new HashSet<string>(detectedPotholes);
        recovered.ExceptWith(detectedPotholesFirstPass);
        return recovered.Count;
    }

    public bool IsInSkipSecondPass()
    {
        return isInSecondPass && droneController != null && droneController.navigationMode == NavigationMode.Skip;
    }

    private void Start()
    {
        // 1. Buscar en el propio objeto
        navAgent = GetComponent<NavMeshAgent>();
        
        // 2. Buscar en el padre (por si este script está en la cámara)
        if (navAgent == null) navAgent = GetComponentInParent<NavMeshAgent>();

        if (droneController != null)
        {
            droneController.onObstacleCleared += CheckPotholeAtClearedPosition;
            // 3. Buscar en el droneController asignado
            if (navAgent == null) navAgent = droneController.GetComponent<NavMeshAgent>();
        }

        if (heightController == null)
            heightController = FindFirstObjectByType<DroneHeightController>();

        if (trafficController == null)
            trafficController = FindFirstObjectByType<TrafficDensityController>();
        
        // 4. Buscar en toda la escena (si hay un solo dron)
        if (navAgent == null) navAgent = FindFirstObjectByType<NavMeshAgent>();
        
        Debug.Log($"[MovementInterface Init] navAgent encontrado: {navAgent != null}");
    }

    private void OnDestroy()
    {
        if (droneController != null)
            droneController.onObstacleCleared -= CheckPotholeAtClearedPosition;
    }

    private void Update()
    {
        if (isCapturing)
        {
            timer += Time.deltaTime;

            if (timer >= captureInterval)
            {
                Capture();
                timer = 0f;
                count++;
            }
        }

        // --- CAPTURA MANUAL PARA PRUEBAS (TECLA G) ---
        if (Input.GetKeyDown(KeyCode.G))
        {
            Debug.Log("[Manual Capture] Forzando captura por tecla G...");
            StartCoroutine(ExecuteDelayedCapture("Manual_G_Test"));
        }

        // Velocidad: si el NavMeshAgent está activo (modo automático), usamos su velocidad.
        // Si está en control manual (Rigidbody activo), usamos la física del Rigidbody.
        float velocidadActual;
        if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
        {
            velocidadActual = navAgent.velocity.magnitude;
        }
        else if (velocidad != null)
        {
            velocidadActual = velocidad.linearVelocity.magnitude;

        }
        else
        {
            velocidadActual = 0f;
        }

        speed = (velocidadActual < toleranciaVelocidad) ? 0f : velocidadActual;

        // Angulo.
        if (!angulo_mando)
        {
            if (angulo != null)
            {
                float currentY = angulo.transform.eulerAngles.y;

                if (!initialized)
                {
                    lastAngle = currentY;
                    baseAngle = currentY;
                    baseAngleSet = true;
                    initialized = true;
                    angle = 0f;
                }
                else if (Mathf.Abs(Mathf.DeltaAngle(currentY, lastAngle)) < TOLERANCIA)
                {
                    if (!baseAngleSet) { baseAngle = currentY; baseAngleSet = true; }
                    angle = 0f;
                }
                else
                {
                    baseAngleSet = false;
                    angle = Mathf.DeltaAngle(baseAngle, currentY);
                }
                lastAngle = currentY;
            }
        }
        else
        {
            angle = direction.anguloactual;
        }

        if (angle >= 134 && angle <= 226) angle -= 180;
        if (angle < 360 && angle >= 314)  angle -= 360;

        // --- ACTUALIZAR UI SIEMPRE ---
        string velocityTextValue = "Velocity : " + speed.ToString("F2") + " Km/H";
        string angleTextValue    = "Angle    : " + angle.ToString("F2") + " º";

        if (velocityText != null)
            velocityText.text = velocityTextValue;

        if (angleText != null)
            angleText.text = angleTextValue;

        // --- STRATEGY / HEIGHT / TRAFFIC ---
        string strategy = (droneController != null) ? droneController.navigationMode.ToString() : "N/A";
        string height = "N/A";
        if (heightController != null)
            height = heightController.currentLevel.ToString();
        else if (droneController != null)
        {
            float h = droneController.targetHeight;
            if (h <= 6f) height = "Low";
            else if (h <= 10f) height = "Medium";
            else height = "High";
        }
        
        string strategyTextValue = "Strategy : " + strategy;
        string heightTextValue   = "Height    : " + height;
        if (strategyAltitudeText != null)
            strategyAltitudeText.text = (heightText != null)
                ? strategyTextValue
                : strategyTextValue + " / " + heightTextValue;

        if (heightText != null)
            heightText.text = heightTextValue;

        string traffic = (trafficController != null) ? trafficController.currentDensity.ToString() : "N/A";
        string trafficValue =      "Traffic    : " + traffic;
        if (trafficText != null)
            trafficText.text = trafficValue;

    }
    [SerializeField] private float offsetDistancia = 0.2f;
    private Vector3 lastMidPoint = Vector3.zero;

    private void Capture()
    {
        if (captureCameras == null || captureCameras.Count == 0)
        {
            Debug.LogError("No capture cameras assigned!");
            return;
        }

        if (speed <= 0.05f && Mathf.Abs(angle) <= 1f)
        {
            Debug.Log("Velocity and angle below thresholds. Image capture skipped.");
            return;
        }

        Vector3 midPoint = Vector3.zero;
        foreach (Camera cam in captureCameras) midPoint += cam.transform.position;
        midPoint /= captureCameras.Count;

        Vector3 direccionMovimiento = (midPoint - lastMidPoint).normalized;

        // Ajustar midPoint en dirección contraria al movimiento.
        midPoint -= direccionMovimiento * offsetDistancia;

        // Guardar el punto medio actual para el siguiente frame.
        lastMidPoint = midPoint;

        Vector3 forwardDir = captureCameras[0].transform.forward;
        Vector3 rightDir = captureCameras[0].transform.right;
        Vector3 upDir = captureCameras[0].transform.up;

        // Usar rayLength fijo para detectar potholes a distancia constante.
        float rayLength = 30f;

        // raySpacing viene configurado desde DroneHeightController según altura.
        float detectionSpacing = raySpacing;

        List<Vector3> rayOrigins = BuildRayGrid(midPoint, rightDir, upDir, detectionSpacing);

        // ════════════════════════════════════════════════════════════════════════════════════
        //  RAYCASTALL: Detecta TODO (Pothole, Crack, Crack_Single, Car, Person)
        // ════════════════════════════════════════════════════════════════════════════════════
        bool hitDetected = false;
        string potholeID = "";
        RaycastHit bestHit = new RaycastHit();
        float closestDistance = float.MaxValue;

        foreach (Vector3 origin in rayOrigins)
        {
            Debug.DrawRay(origin, forwardDir * rayLength, Color.red, 2f);

            // ★ CAMBIO: RaycastAll en lugar de Raycast
            RaycastHit[] hits = Physics.RaycastAll(origin, forwardDir, rayLength);

            foreach (RaycastHit hit in hits)
            {
                string hitTag = hit.collider.tag;
                string hitName = hit.collider.gameObject.name;

                // ═══ GROUND TRUTH: Solo tags permitidos (Crocodile, Pothole, Crack, Car, Person) ═══
                if ((hitTag == "Crocodile" || hitTag == "Pothole" || hitTag == "Crack" || hitTag == "Crack_Single" || hitTag == "Car" || hitTag == "Person") 
                    && !groundTruthObjectsSet.Contains(hitName))
                {
                    groundTruthObjectsSet.Add(hitName);
                    groundTruthCount++;
                    RegisterSegmentGroundTruth(hitName, hitTag);  // ← tracking por segmento + obstacle flag
                    // Debug.Log($"[Ground Truth] {hitName} ({hitTag}) - Total: {groundTruthCount}");
                }

                // ═══ MODELO: Cualquier tag relevante dispara captura de foto → Python ═══
                // Python solo confirmará bache si detecta pothole/crack; Car/Person no suben el Coverage.
                if ((hitTag == "Crocodile" || hitTag == "Pothole" || hitTag == "Crack" || hitTag == "Crack_Single"
                     || hitTag == "Person" || hitTag == "Car") && hit.distance < closestDistance)
                {
                    hitDetected = true;
                    potholeID = hitName;
                    bestHit = hit;
                    closestDistance = hit.distance;
                }
            }
        }

        // ════════════════════════════════════════════════════════════════════════════════════
        // DEBUG: Mostrar TODO lo que el raycast tocó en esta pasada
        // ════════════════════════════════════════════════════════════════════════════════════
        if (groundTruthObjectsSet.Count > 0)
        {
            string allObjects = string.Join(", ", groundTruthObjectsSet);
            // Debug.Log($"🔍 [RAYCAST TOTAL] Tocó: {allObjects} | Total: {groundTruthCount} objetos únicos");
        }
        if (!hitDetected)
        {
            // Debug.Log("No pothole detected by any ray.");
            return;
        }

        if (detectedPotholes.Contains(potholeID))
        {
            // Debug.Log($"Pothole {potholeID} already captured. Skipping.");
            return;
        }

        detectedPotholes.Add(potholeID);
        // NOTA: RegisterSegmentDetection NO se llama aquí.
        // Solo Python puede confirmar una detección del modelo (via PythonInferenceClient).
        // Esto evita que el coverage supere el 100%.
        
        // Si estamos en segunda pasada (Skip revisit), trackear en segundo HashSet
        if (isInSecondPass)
        {
            detectedPotholesSecondPass.Add(potholeID);
        }
        else
        {
            detectedPotholesFirstPass.Add(potholeID);
        }
        
        currentPothole = potholeID;
        
        // Generar marcador visual (Plano 4x4 a 6m de altura)
        SpawnMarker(bestHit.collider.transform.position, bestHit.collider.tag);

        // Actualiza texto visible en Inspector
        visiblePotholes = string.Join("\n", detectedPotholes);

        // --- INICIAR PROCESO DE FOTO CON RETRASO ---
        StartCoroutine(ExecuteDelayedCapture(potholeID, bestHit.collider.transform.position, bestHit.collider.tag));
    }

    private List<Vector3> BuildRayGrid(Vector3 center, Vector3 lateralDir, Vector3 longitudinalDir, float spacing)
    {
        int columns = Mathf.Max(1, rayColumns);
        int rows = Mathf.Max(1, rayRows);
        float safeSpacing = Mathf.Max(0.01f, spacing);

        float lateralWidth = safeSpacing * Mathf.Max(0, columns - 1);
        float longitudinalDepth = safeSpacing * Mathf.Max(1, rows);

        List<Vector3> origins = new List<Vector3>(columns * rows);
        for (int row = 0; row < rows; row++)
        {
            float rowT = rows == 1 ? 0.5f : row / (float)(rows - 1);
            float longitudinalOffset = Mathf.Lerp(-longitudinalDepth * 0.5f, longitudinalDepth * 0.5f, rowT);

            for (int col = 0; col < columns; col++)
            {
                float colT = columns == 1 ? 0.5f : col / (float)(columns - 1);
                float lateralOffset = Mathf.Lerp(-lateralWidth * 0.5f, lateralWidth * 0.5f, colT);
                origins.Add(center + lateralDir * lateralOffset + longitudinalDir * longitudinalOffset);
            }
        }

        return origins;
    }

    private IEnumerator ExecuteDelayedCapture(string potholeID)
    {
        return ExecuteDelayedCapture(potholeID, Vector3.zero, "");
    }

    private IEnumerator ExecuteDelayedCapture(string potholeID, Vector3 candidateWorldPosition, string candidateTag)
    {
        if (captureDelay > 0)
            yield return new WaitForSeconds(captureDelay);

        foreach (Camera cam in captureCameras)
        {
            string filename = $"Image{count}_{cam.name}_{potholeID}_{System.DateTime.Now:yyyyMMdd_HHmmss}.png";

            bool convertToGrayscale = cam.name.Contains("L") || cam.name.Contains("R");
            RenderTexture renderTexture = new RenderTexture(1270, 950, 24);

            cam.targetTexture = renderTexture;
            Texture2D screenshot = new Texture2D(1270, 950, TextureFormat.RGB24, false);
            cam.Render();

            RenderTexture.active = renderTexture;
            screenshot.ReadPixels(new Rect(0, 0, 1270, 950), 0, 0);

            if (convertToGrayscale)
            {
                Color[] pixels = screenshot.GetPixels();
                for (int i = 0; i < pixels.Length; i++)
                {
                    float gray = pixels[i].grayscale;
                    pixels[i] = new Color(gray, gray, gray);
                }
                screenshot.SetPixels(pixels);
            }

            screenshot.Apply();
            
            // Use FileHandler for cross-platform compatibility
            byte[] pngBytes = screenshot.EncodeToPNG();
            // FileHandler.SaveImage(pngBytes, filename);  // ← DESACTIVADO en batch (ahorra I/O en disco)

            // ---> ENVIAR IMAGEN A LA API DE PYTHON <---
            var dtm = DigitalTwin.DigitalTwinManager.Instance;
            if (dtm != null && dtm.testModeNoPython)
            {
                RegisterTestModeDetection(potholeID, candidateWorldPosition, candidateTag);
            }
            else if (PythonInferenceClient.Instance != null)
            {
                PythonInferenceClient.Instance.AnalyzeImageBytes(
                    pngBytes,
                    filename,
                    potholeID,
                    candidateWorldPosition,
                    candidateTag);
            }

            cam.targetTexture = null;
            RenderTexture.active = null;
            Destroy(renderTexture);
            Destroy(screenshot);

            // Debug.Log($"Captured image: {filename}");
        }

        count++;
    }

    private void RegisterTestModeDetection(string objectID, Vector3 objectWorldPosition, string objectTag)
    {
        var dtm = DigitalTwin.DigitalTwinManager.Instance;
        if (dtm == null) return;

        if (IsObstacleTag(objectTag))
        {
            ForgetUnconfirmedCandidate(objectID);
            droneController?.QueueSkipRevisitPosition(
                objectWorldPosition,
                $"TestMode detectó obstáculo {objectTag} {objectID}");
            Debug.Log($"[TestMode] Obstáculo detectado por raycast: {objectID} ({objectTag})");
            return;
        }

        if (!IsDamageTag(objectTag)) return;

        int window = Mathf.Max(1, dtm.testModeConfirmEvery);
        int confirmCount = Mathf.Clamp(dtm.testModeConfirmCount, 0, window);
        int slot = testModeDamageSequence % window;
        testModeDamageSequence++;

        if (slot >= confirmCount)
        {
            ForgetUnconfirmedCandidate(objectID);
            droneController?.QueueSkipRevisitPosition(
                objectWorldPosition,
                $"TestMode fallo detección de {objectTag} {objectID}");
            Debug.Log($"[TestMode] Detección simulada FALLIDA: {objectID} ({objectTag}) slot {slot + 1}/{window}");
            return;
        }

        RegisterSegmentDetection(objectID);
        Debug.Log($"[TestMode] Detección simulada confirmada: {objectID} ({objectTag}) slot {slot + 1}/{window}");
    }

    private bool IsDamageTag(string tag)
    {
        return tag == "Pothole" || tag == "Crack" || tag == "Crack_Single" || tag == "Crocodile";
    }

    private bool IsObstacleTag(string tag)
    {
        return tag == "Car" || tag == "Person";
    }

    private void ForgetUnconfirmedCandidate(string objectID)
    {
        detectedPotholes.Remove(objectID);
        detectedPotholesFirstPass.Remove(objectID);
        detectedPotholesSecondPass.Remove(objectID);
        visiblePotholes = string.Join("\n", detectedPotholes);
    }

    private void SpawnMarker(Vector3 position, string tag)
    {
        // Crear plano primitivo
        GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        
        // Posición: 18.91 metros arriba del objeto detectado
        plane.transform.position = position + Vector3.up * 18.91f;
        
        // Escala: 8x8 metros (El plano por defecto es 10x10, así que 0.8f lo hace 8x8)
        plane.transform.localScale = new Vector3(0.8f, 1f, 0.8f);
        
        // Rotación: Plano mirando arriba (por defecto ya está así, pero aseguramos)
        plane.transform.rotation = Quaternion.identity;

        // Configurar material/color
        Renderer rend = plane.GetComponent<Renderer>();
        if (rend != null)
        {
            // Usamos material standard temporal o cambiamos color directo si el shader lo permite
            rend.sharedMaterial.shader = Shader.Find("Standard"); 
            if (tag == "Crocodile")
            {
                rend.material.color = new Color(1f, 1f, 0f); // Amarillo puro chillón
            }
            else // Pothole
            {
                rend.material.color = new Color(0f, 1f, 1f); // Cyan/Azul eléctrico chillón
            }
        }

        // Eliminar collider para no interferir con futuros raycasts del dron
        Destroy(plane.GetComponent<Collider>());
        
        // Asignar un nombre informativo
        plane.name = $"Marker_{tag}_{System.DateTime.Now.Ticks}";
    }

    /// <summary>
    /// [Hover Mode] Verifica si hay bache en la posición donde estaba el obstáculo.
    /// La marcha ya fue reanudada por DroneNavMeshController antes de llamar a este método;
    /// si no se encuentra nada el drone simplemente continúa su ruta.
    /// </summary>
    private void CheckPotholeAtClearedPosition(Vector3 worldXZPos)
    {
        float scanHeight = (droneController != null) ? droneController.targetHeight : 5f;
        Vector3 origin   = new Vector3(worldXZPos.x, scanHeight + 1f, worldXZPos.z);
        float   scanDepth = scanHeight + 3f;

        // ★ CAMBIO: RaycastAll para detectar TODO
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, scanDepth);

        bool potholeFound = false;
        foreach (RaycastHit hit in hits)
        {
            string hitTag  = hit.collider.tag;
            string hitName = hit.collider.gameObject.name;

            // ═══ GROUND TRUTH: Solo tags permitidos ═══
            if ((hitTag == "Crocodile" || hitTag == "Pothole" || hitTag == "Crack" || hitTag == "Crack_Single" || hitTag == "Car" || hitTag == "Person") 
                && !groundTruthObjectsSet.Contains(hitName))
            {
                groundTruthObjectsSet.Add(hitName);
                groundTruthCount++;
                RegisterSegmentGroundTruth(hitName, hitTag);  // ← tracking por segmento + obstacle flag
                Debug.Log($"[Ground Truth] {hitName} ({hitTag}) - Total: {groundTruthCount}");
            }

            // ═══ MODELO: Baches ═══
            if ((hitTag == "Pothole" || hitTag == "Crack" || hitTag == "Crack_Single") && !detectedPotholes.Contains(hitName))
            {
                detectedPotholes.Add(hitName);
                if (isInSecondPass) detectedPotholesSecondPass.Add(hitName);
                else detectedPotholesFirstPass.Add(hitName);

                var dtm = DigitalTwin.DigitalTwinManager.Instance;
                if (dtm != null && dtm.testModeNoPython)
                {
                    RegisterTestModeDetection(hitName, hit.collider.transform.position, hitTag);
                }
                else
                {
                    RegisterSegmentDetection(hitName);  // tracking por segmento
                }
                
                currentPothole = hitName;
                SpawnMarker(hit.collider.transform.position, hitTag);
                visiblePotholes = string.Join("\n", detectedPotholes);
                potholeFound = true;
                Debug.Log($"[Hover] Bache descubierto bajo obstáculo: {hitName}");
            }
        }

        if (!potholeFound)
        {
            Debug.Log("[Hover] Zona despejada sin bache detectado. Marcha en curso.");
        }

        // ════════════════════════════════════════════════════════════════════════════════════
        // DEBUG: Mostrar TODO lo que el raycast tocó en esta verificación Hover
        // ════════════════════════════════════════════════════════════════════════════════════
        if (groundTruthObjectsSet.Count > 0)
        {
            string allObjects = string.Join(", ", groundTruthObjectsSet);
            // Debug.Log($"🔍 [HOVER RAYCAST] Tocó: {allObjects} | Total: {groundTruthCount} objetos únicos");
        }
    }

    public void AcDc()
    {
        if (isCapturing)
        {
            isCapturing = false;
            // container = false;
            buttonText.text = "Record";
        }
        else
        {
            isCapturing = true;
            // container = true;
            buttonText.text = "Stop";
        }
    }

    public void restarted()
    {
        SceneManager.LoadScene("Mode_Load");
    }
}
