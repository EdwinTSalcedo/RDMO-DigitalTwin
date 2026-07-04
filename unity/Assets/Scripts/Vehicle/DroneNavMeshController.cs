using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

/// <summary>
/// Modo de navegacin autnoma del drone.
/// Cambiar con teclas 1-2 durante el Play Mode.
/// </summary>
public enum NavigationMode
{
    Baseline = 1,   // Zigzag + deteccin de baches + retorno (comportamiento original)
    Hover    = 2,   // Igual que Baseline + pausa si hay Person/Car debajo
    Micro    = 3,   // Zigzag errtico dentro del rea para mejor ngulo de captura
    Skip     = 4    // Primera pasada rpida anotando obstaculizados; revisita tras recarga
    // 5 reservado para modos futuros
}

[RequireComponent(typeof(Rigidbody), typeof(NavMeshAgent))]
public class DroneNavMeshController : MonoBehaviour
{
    [Serializable]
    public class ModeLineRoute
    {
        public string routeName = "Line";
        public Vector2 startXZ = Vector2.zero;
        public Vector2 endXZ = new Vector2(10f, 0f);
    }

    public bool apagado = false;
private bool apagando = false;
private float tiempoApagado = 0f;
private float tiempoParaApagarMotores = 2f;
    // private float velocidadBajada = 2f; // Unused
private float targetHeightInicial;
private float minHeightInicial;



    [Header("Height Configuration")]
    [Tooltip("Altura objetivo en metros. 6m = altura estndar de inspeccin con drone real.")]
    public float targetHeight = 6f;
    [Tooltip("Altura mnima permitida en metros")]
    public float minHeight = 3f;
    [Tooltip("Velocidad mxima de ascenso/descenso en m/s")]
    public float maxAscendSpeed = 5f;
    [Tooltip("Ganancia proporcional del PID de altura")]
    public float heightPID_Kp = 50f;
    [Tooltip("Ganancia integral del PID de altura")]
    public float heightPID_Ki = 5f;
    [Tooltip("Ganancia derivativa del PID de altura")]
    public float heightPID_Kd = 10f;
    [Tooltip("Zona muerta del PID de altura")]
    public float heightDeadZone = 0.1f;

    [Header("Movement Configuration")]
    [Tooltip("Velocidad de escaneo (m/s). 10 m/s  36 km/h. CaptureInterval se ajust a 0.5s para no perder baches.")]
    public float moveSpeed = 10f;
    [Tooltip("Velocidad de rotacin (/s). Mayor para curvas rpidas.")]
    public float manualRotationSpeed = 0.5f;
    [Tooltip("Velocidad angular del NavMeshAgent (grados/segundo).")]
    public float navMeshAngularSpeed = 45f;
    [Tooltip("ngulo de inclinacin al moverse ()")]
    public float tiltAngle = 10f;
    [Tooltip("Velocidad de estabilizacin")]
    public float stabilizationSpeed = 5f;
    [Tooltip("Torque mximo de estabilizacin")]
    public float maxStabilizationTorque = 100f;

    [Header("Motor Configuration")]
    [Tooltip("Velocidad de rotacin de los motores. Poner en 0 para detenerlos completamente.")]
    public float motorRotationSpeed = 150f;
    [Tooltip("Si los motores giran en direcciones opuestas (realista)")]
    public bool alternateRotation = true;

    [Header("NavMesh Configuration")]
    [Tooltip("Distancia para considerar waypoint alcanzado (m). 2m para alta velocidad.")]
    public float stoppingDistance = 2.0f;
    [Tooltip("Intervalo entre actualizaciones de ruta NavMesh (segundos). Ms frecuente para alta velocidad.")]
    public float updatePathInterval = 0.2f;

    [Header("References")]
    public Transform[] motors;
    public Transform modelRoot;
    /// <summary>Referencia al EnergyController para que Skip mode consulte el nivel de energa.</summary>
    public EnergyController energyController;

    [Header("Navigation Mode Button (opcional)")]
    [Tooltip("Botn UI que cicla entre los modos de navegacin. Se oculta en modo manual. Asignar en Inspector.")]
    public Button navModeButton;
    [Tooltip("Texto TMP del botn de modo. Si es null se intenta obtener del botn automticamente.")]
    public TMP_Text navModeButtonText;

    [Header("Refuel Position")]
    public Vector3 repostajePosition = new Vector3(0.5f, 0f, 0.5f);

    [Header("Navigation Mode")]
    [Tooltip("Modo activo. Cambiar en Inspector o con teclas 1-2 en Play.")]
    public NavigationMode navigationMode = NavigationMode.Baseline;

    [Header("Hover Mode Settings")]
    [Tooltip("Umbral de visibilidad matemtica (tau_o). 1.0=total, 0.0=ocluido. Si la visibilidad es menor, se activa la poltica.")]
    [Range(0f, 1f)]
    public float visibilityThreshold = 0.5f;
    [Tooltip("Profundidad mxima del rayo de visibilidad (debe ser  targetHeight + 2m)")]
    public float obstacleDetectionHeight = 8f;

    /// <summary>Evento disparado cuando un obstculo se despeja; pasa la posicin XZ donde estaba.</summary>
    public Action<Vector3> onObstacleCleared;

    [Header("Micro Mode Settings")]
    [Tooltip("Segundos entre cada micro-maniobra")]
    public float microManeuverInterval = 2.0f;
    [Tooltip("Desviacin mxima lateral (m). 0.3m = suave y realista.")]
    public float microManeuverRadius = 0.3f;
    [Tooltip("Duracin de cada micro-maniobra (segundos)")]
    public float microManeuverDuration = 1.5f;
    [Tooltip("ngulo mximo de desviacin (). 45 = apenas perceptible, realista.")]
    [Range(15f, 180f)]
    public float microMaxAngle = 45f;

    [Header("Transit Mode Settings")]
    [Tooltip("Altura mxima durante trnsito a la zona (evita obstculos)")]
    public float transitMaxHeightCustom = 20f;
    [Tooltip("Velocidad mxima durante trnsito a la zona (m/s). 30 m/s  108 km/h.")]
    public float transitMaxSpeedCustom = 30f;
    [Tooltip("Distancia mnima para considerar que lleg al rea")]
    public float transitArrivalThresholdCustom = 8f;

    [Header("Skip Mode Settings")]
    [Tooltip("Umbral de energa (%) para considerar recarga completa antes de revisitar")]
    public float skipRechargeThreshold = 95f;
    [Tooltip("Radio de deduplicacin espacial: posiciones ms prximas que esto se ignoran")]
    public float skipDeduplicationRadius = 1.5f;
    [Tooltip("Segundos mnimos entre registros consecutivos de posicin skipped")]
    public float skipRecordCooldown = 2f;
    [Tooltip("Segundos que el drone permanece sobre cada zona anotada durante la revisita")]
    public float revisitHoverTime = 5f;

    // Private variables
    public Rigidbody rb;
    private NavMeshAgent agent;
    private PIDController heightPID;
    private float verticalInput;
    private float currentRotation;
    private Vector2 movementInput;
    private float[] motorRotationAngles;
    private float lastPathUpdateTime;
    public bool manualControl = true;
    private bool hasRoute = false;
    private bool isReturningToBase = false;
    private bool missionComplete = false;

    /// <summary>True cuando el dron termin TODO el trabajo del segmento actual
    /// (incluyendo revisita Skip si aplica) y est listo para el siguiente.
    /// El ExperimentAutomator espera este flag entre segmentos.</summary>
    public bool segmentDone = false;

    // Waypoint system
    private List<Vector3> searchWaypoints = new List<Vector3>();
    private int currentWaypointIndex = 0;
    // private float droneWidth = 0.3f; // Unused
    // private float droneLength = 0.3f; // Unused
    [Tooltip("Separacin entre pasadas del patrn de cortadora (m). 2m = cobertura realista con cmara.")]
    public float searchSpacing = 2.0f;

    [Header("ModeLine Settings")]
    [Tooltip("Si est activo, ignora el rea/segmento recibido y usa rutas rectas configuradas aqu.")]
    public bool ModeLine = false;
    [Tooltip("Cuando termina la ltima lnea, vuelve a empezar desde la primera.")]
    public bool loopModeLineRoutes = true;
    [Tooltip("Lista de lneas rectas XZ que el dron recorrer en orden.")]
    public List<ModeLineRoute> modeLineRoutes = new List<ModeLineRoute>();
    private int currentModeLineRouteIndex = 0;
    private bool modeLineCycleCompleted = false;

    // --- Hover Mode state ---
    private bool isHoveringForObstacle = false;
    private Vector3 hoveredObstaclePosition;
    private float lastObstacleCheckTime = 0f;
    private const float obstacleCheckInterval = 0.3f;

    // --- Micro Mode state ---
    private bool isMicroManeuvering = false;
    private Vector3 searchAreaMin;   // lmite inferior del rea de bsqueda (XZ)
    private Vector3 searchAreaMax;   // lmite superior del rea de bsqueda (XZ)

    // --- Skip Mode state ---
    private enum SkipPhase { Idle, FirstPass, ReturningAfterFirstPass, Recharging, Revisiting, Done }
    private SkipPhase skipPhase = SkipPhase.Idle;
    private List<Vector3> skippedPositions = new List<Vector3>();
    private int currentSkipIndex = 0;
    private float lastSkipRecordTime = -999f;
    private bool isRevisitHovering = false;
    private float revisitHoverStartTime = 0f;

    // --- Transit Mode (Trnsito rpido a zona) ---
    private bool isInTransitMode = false;
    private float originalTargetHeight = 0f;
    private float originalMoveSpeed = 0f;
    private float baseMoveSpeed = 0f;  // Velocidad de escaneo configurada en el Inspector (nunca cambia)
    // Variables eliminadas - usar transitMaxHeightCustom, transitMaxSpeedCustom, transitArrivalThresholdCustom
    private Vector3 transitTargetCenter = Vector3.zero;  // Centro del rea de bsqueda

    // --- State Persistence (para reanudar tras apagado) ---
    private struct DroneState
    {
        public NavigationMode navigationMode;
        public bool manualControl;
        public bool hasRoute;
        public bool isReturningToBase;
        public bool missionComplete;
        public bool segmentDone;
        public bool isInTransitMode;
        public bool isHoveringForObstacle;
        public bool isMicroManeuvering;
        public Vector3 transitTargetCenter;
        public Vector3 searchAreaMin;
        public Vector3 searchAreaMax;
        public List<Vector3> searchWaypoints;
        public int currentWaypointIndex;
        public float targetHeight;
        public float minHeight;
        public bool isCapturing;  // MovementInterface
        public int heightLevel;  // DroneHeightController (0=Low,1=Medium,2=High)
        // Skip mode
        public SkipPhase skipPhase;
        public List<Vector3> skippedPositions;
        public int currentSkipIndex;
        public bool isRevisitHovering;
        public float revisitHoverStartTime;
    }
#pragma warning disable CS0414
    [System.Obsolete("No utilizado - la persistencia se maneja por PlayerPrefs")]
    private DroneState? savedState = null;
#pragma warning restore CS0414

    // --- Digital Twin Mathematical Variables ---
    public float currentHoverWaitTime = 0f;
    public Vector2 currentMicroDelta = Vector2.zero;

    void Start()
    {
        targetHeightInicial = targetHeight;
        minHeightInicial = minHeight;

        baseMoveSpeed = moveSpeed;  //  velocidad de escaneo (Inspector), nunca cambia
        rb = GetComponent<Rigidbody>();
        agent = GetComponent<NavMeshAgent>();
        heightPID = new PIDController(heightPID_Kp, heightPID_Ki, heightPID_Kd);

        rb.centerOfMass = Vector3.zero;
        rb.inertiaTensorRotation = Quaternion.identity;

        motorRotationAngles = new float[motors.Length];
        transform.position = repostajePosition;

        ConfigureNavAgent(false);
        UpdateNavModeButtonUI();
        
        // Al iniciar, si hay estado guardado en disco (sobrevivi a reinicio de PC),
        // cargarlo para que al presionar Space se reanude exactamente donde iba.
        if (PlayerPrefs.GetInt(PREFS_PREFIX + "saved", 0) == 1)
        {
            Debug.Log("[State]  Estado guardado encontrado en disco (de sesin anterior). Presiona SPACE para reanudar.");
        }
    }


    void Update()
    {
        if (apagado)
{
if (Input.GetKeyDown(KeyCode.Space))
{
    apagado = false;

    // RESTAURAR estado guardado desde disco (PlayerPrefs)
    // Si haba estado guardado, RestoreDroneState() restaura todo automticamente
    // Si no, entra en modo manual por defecto
    bool restored = PlayerPrefs.GetInt(PREFS_PREFIX + "saved", 0) == 1;
    RestoreDroneState();

    if (!restored)
    {
        manualControl = true;
        targetHeight = targetHeightInicial;
        minHeight = minHeightInicial;
        ConfigureNavAgent(false);
        Debug.Log("[State] Sin estado previo. Drone reactivado en modo manual.");
    }
    else
    {
        Debug.Log($"[State] Drone reactivado con estado guardado. manualControl={manualControl}, hasRoute={hasRoute}, modo={navigationMode}");
    }
}


    return;
}

if (apagando)
{
    tiempoApagado += Time.deltaTime;

    // Suavizar motores hasta detenerlos
    for (int i = 0; i < motors.Length; i++)
    {
        float t = Mathf.Clamp01(tiempoApagado / tiempoParaApagarMotores);
        float speedFactor = Mathf.Lerp(1f, 0f, t);

        // Si motorRotationSpeed es 0, no rotar
        if (motorRotationSpeed <= 0f)
        {
            motors[i].localRotation = Quaternion.Euler(0f, motorRotationAngles[i], 0f);
            continue;
        }

        float rotationDirection = alternateRotation ? (i % 2 == 0 ? 1 : -1) : 1;
        motorRotationAngles[i] += motorRotationSpeed * speedFactor * rotationDirection * Time.deltaTime;

        motors[i].localRotation = Quaternion.Euler(0f, motorRotationAngles[i], 0f);
    }

    // Bajar altura objetivo
    targetHeight = Mathf.MoveTowards(targetHeight, 0.04f, Time.deltaTime * 1.5f);

    // Tambin reducir el minHeight para permitir el descenso total
    minHeight = Mathf.MoveTowards(minHeight, 0f, Time.deltaTime * 2f);

    // Detectar si ya toc el suelo
    Ray ray = new Ray(transform.position, Vector3.down);
    if (Physics.Raycast(ray, out RaycastHit hit, 5f))
    {
        float distanceToGround = hit.distance;
        if (distanceToGround <= 0.08f && targetHeight <= 0.05f)
        {
            FinalizarApagado();
        }
    }

    return;
}




        HandleInput();
        HandleModeInput();
        ApplyMotorRotation();

        // ACTUALIZAR velocidad del NavMeshAgent si est en movimiento automtico
        if (!manualControl && agent != null && agent.enabled && hasRoute)
        {
            agent.speed = moveSpeed;  // Aplicar cambios de velocidad en tiempo real
            agent.angularSpeed = navMeshAngularSpeed;
        }

        // --- MODO TRNSITO HABILITADO ---
        if (isInTransitMode && hasRoute && agent != null && agent.enabled)
        {
            float distanceToArea = Vector3.Distance(
                new Vector3(transform.position.x, 0, transform.position.z),
                new Vector3(transitTargetCenter.x, 0, transitTargetCenter.z)
            );
            if (distanceToArea <= transitArrivalThresholdCustom)
            {
                isInTransitMode = false;
                moveSpeed = originalMoveSpeed;  // Restaurar velocidad original
                // Altura ya est en su valor original (no la cambiamos)
                Debug.Log($"[Transit] Llegado a zona! Restaurando velocidad: {moveSpeed}m/s. Iniciando bsqueda.");
                
                // AUTO-ACTIVAR ACDC (Recording) cuando llega a la zona para mapeo
                if (DigitalTwin.DigitalTwinManager.Instance != null)
                {
                    if (DigitalTwin.DigitalTwinManager.Instance.movementInterface != null 
                        && !DigitalTwin.DigitalTwinManager.Instance.movementInterface.isCapturing)
                    {
                        DigitalTwin.DigitalTwinManager.Instance.movementInterface.AcDc();
                        Debug.Log("[Recording]  ACDC ACTIVADO - comenzando mapeo en zona");
                    }
                }
            }
        }

        // Ejecutar lgicas especiales SOLO si el dron ya tiene una ruta activa (evita que arranque solo al cambiar modos)
        if (hasRoute && !manualControl && !isReturningToBase)
        {
            // [Hover] Detectar obstculos debajo antes de avanzar waypoints
            if (navigationMode == NavigationMode.Hover)
                CheckObstaclesBelow();

            // [Micro] Ejecutar micro-maniobras de bsqueda dentro del rea
            if (navigationMode == NavigationMode.Micro)
                CheckMicroManeuver();

            // [Skip] Mquina de estados de dos pasadas
            if (navigationMode == NavigationMode.Skip)
                UpdateSkipMode();
        }

        // No avanzar waypoints mientras el drone est pausado (Hover), en modo Micro
        // (que gestiona su propio destino con offset), o en fases de Skip
        if (!manualControl && hasRoute && Time.time - lastPathUpdateTime > updatePathInterval
            && !isReturningToBase && !isHoveringForObstacle && !isMicroManeuvering
            && navigationMode != NavigationMode.Micro
            && skipPhase != SkipPhase.Recharging
            && skipPhase != SkipPhase.Revisiting
            && skipPhase != SkipPhase.Done)
        {
            if (searchWaypoints.Count > 0 && currentWaypointIndex < searchWaypoints.Count)
            {
                agent.SetDestination(searchWaypoints[currentWaypointIndex]);
                lastPathUpdateTime = Time.time;

                // Check if reached current waypoint
                if (!agent.pathPending && agent.remainingDistance <= stoppingDistance)
                {
                    currentWaypointIndex++;

                    // If completed all waypoints
                    if (currentWaypointIndex >= searchWaypoints.Count)
                    {
                        if (!missionComplete)
                        {
                            //  DESACTIVAR ACDC al terminar el escaneo del segmento
                            var mi = DigitalTwin.DigitalTwinManager.Instance?.movementInterface;
                            if (mi != null && mi.isCapturing)
                            {
                                mi.AcDc();
                                Debug.Log("[Recording]  ACDC DESACTIVADO - escaneo de segmento completado");
                            }

                            // Acelerar para el trnsito
                            moveSpeed = transitMaxSpeedCustom;

                            var dtm = DigitalTwin.DigitalTwinManager.Instance;
                            bool isBatchMode = dtm != null && dtm.suppressAutoEndEpisode && !ModeLine;

                            if (navigationMode == NavigationMode.Skip)
                            {
                                if (skippedPositions.Count > 0)
                                {
                                    BeginSkipRevisit(mi);
                                    return; // An no marcamos segmentDone
                                }
                                else
                                {
                                    skipPhase = SkipPhase.Done;
                                }
                            }

                            if (isBatchMode)
                            {
                                // BATCH MODE: Vuelo continuo, NO ir a base.
                                // missionComplete=true evita que este bloque se re-ejecute cada frame
                                missionComplete = true;
                                segmentDone = true;
                                hasRoute = false;
                                searchWaypoints.Clear();
                                skippedPositions.Clear();
                                Debug.Log($"[SearchPattern]  Segmento completado. Esperando Automator (vuelo continuo).");
                            }
                            else
                            {
                                // NORMAL MODE: Retorna a base
                                missionComplete = true;
                                isReturningToBase = true;
                                GenerateReturnPath();
                                Debug.Log("[SearchPattern]  Zona completada. Retornando a base para recargar.");
                                if (navigationMode == NavigationMode.Skip) skipPhase = SkipPhase.ReturningAfterFirstPass;
                            }
                        }
                        else if (isReturningToBase)
                        {
                            if (navigationMode == NavigationMode.Skip
                                && skipPhase == SkipPhase.ReturningAfterFirstPass)
                            {
                                // Lleg a base tras primera pasada  iniciar recarga
                                isReturningToBase = false;
                                hasRoute = false;
                                searchWaypoints.Clear();
                                skipPhase = SkipPhase.Recharging;
                                energyController?.IniciarRecarga();
                                Debug.Log($"[Skip] En base. Recargando... {skippedPositions.Count} zona(s) anotadas para revisita.");
                            }
                            else
                            {
                                // Comportamiento normal (Baseline / Hover / Micro)
                                isReturningToBase = false;
                                hasRoute = false;
                                searchWaypoints.Clear();
                                Debug.Log("Mission completed and drone returned to base");
                                
                                // INICIAR RECARGA para siguiente zona
                                energyController?.IniciarRecarga();
                                segmentDone = true;  //  Automator puede pasar al siguiente segmento
                                
                                // EndEpisode solo si NO est suprimido (modo manual / sin batch)
                                var dtm = DigitalTwin.DigitalTwinManager.Instance;
                                if (dtm != null && !dtm.suppressAutoEndEpisode)
                                    dtm.EndEpisode();
                            }
                        }
                    }
                }
            }
        }

        // --- INICIO DIAGNSTICO ---
        if (missionComplete || isReturningToBase)
        {
            float dist = Vector3.Distance(transform.position, repostajePosition);
            Debug.Log($"[Diagnstico Fin] isReturning={isReturningToBase} | missionComplete={missionComplete} | Distancia a Base={dist:F2}m | IsAtBase={IsAtBase()} | hasRoute={hasRoute}");
        }
        // --- FIN DIAGNSTICO ---

        // Parche: Validacin absoluta de llegada a base (el bucle de waypoints ignoraba el final por el !isReturningToBase)
        if (IsAtBase() && hasRoute)
        {
            if (isReturningToBase && navigationMode != NavigationMode.Skip)
            {
                isReturningToBase = false;
                hasRoute = false;
                searchWaypoints.Clear();
                Debug.Log("Mission completed and drone returned to base [Unified Check]");
                energyController?.IniciarRecarga();
                segmentDone = true;  //  Automator puede pasar al siguiente segmento
                var dtm = DigitalTwin.DigitalTwinManager.Instance;
                if (dtm != null && dtm.isEpisodeActive && !dtm.suppressAutoEndEpisode)
                    dtm.EndEpisode();
            }
            else if (navigationMode == NavigationMode.Skip && skipPhase == SkipPhase.Done)
            {
                hasRoute = false;
                Debug.Log("Skip Mission fully completed and drone returned to base [Unified Check]");
                energyController?.IniciarRecarga();
                segmentDone = true;  //  Automator puede pasar al siguiente segmento
                var dtm = DigitalTwin.DigitalTwinManager.Instance;
                if (dtm != null && dtm.isEpisodeActive && !dtm.suppressAutoEndEpisode)
                    dtm.EndEpisode();
            }
        }
    }

    void FixedUpdate()
    {
        HandleAltitude();

        if (manualControl)
        {
            HandleManualMovement();
            HandleRotation();
            ForceStabilization();
        }
    }

    public void SetSearchArea(Vector3 startPos, Vector3 endPos)
    {
        startPos.y = targetHeight;
        endPos.y = targetHeight;
        bool usingModeLineRoute = TryApplyModeLineRoute(ref startPos, ref endPos);

        missionComplete = false;
        isReturningToBase = false;
        hasRoute = true;
        segmentDone = false;  // reset al iniciar nuevo segmento
        
        // RESETEAR flags de EnergyController para nueva zona
        if (energyController != null)
        {
            energyController.ResetReturnFlags();  // Nuevo mtodo
        }

        // --- MODO TRNSITO HABILITADO: acelerar (sin cambiar altura) ---
        isInTransitMode = true;
        originalTargetHeight = targetHeight;
        // Siempre guardar la velocidad LENTA de escaneo (baseMoveSpeed = valor del Inspector).
        // Nunca usar moveSpeed aqu porque puede estar en 25m/s de un trnsito anterior.
        originalMoveSpeed = baseMoveSpeed;
        moveSpeed = transitMaxSpeedCustom;  // Acelerar a 25m/s para el trnsito
        transitTargetCenter = startPos;
        transitTargetCenter.y = targetHeight;
        Debug.Log($"[Transit] Iniciando trnsito. Velocidad escaneo: {originalMoveSpeed}m/s  trnsito: {moveSpeed}m/s");

        // Guardar lmites del rea para acotar el modo Micro
        searchAreaMin = new Vector3(Mathf.Min(startPos.x, endPos.x), 0f, Mathf.Min(startPos.z, endPos.z));
        searchAreaMax = new Vector3(Mathf.Max(startPos.x, endPos.x), 0f, Mathf.Max(startPos.z, endPos.z));

        // Inicializar estado del modo Skip
        if (navigationMode == NavigationMode.Skip)
        {
            skippedPositions.Clear();
            currentSkipIndex = 0;
            isRevisitHovering = false;
            lastSkipRecordTime = -999f;
            skipPhase = SkipPhase.FirstPass;
        }

        if (usingModeLineRoute)
        {
            GenerateStraightLinePattern(startPos, endPos);
        }
        else
        {
            GenerateSearchPattern(startPos, endPos);
            RebuildSearchPatternAsZigzag(
                new Vector3(Mathf.Min(startPos.x, endPos.x), targetHeight, Mathf.Min(startPos.z, endPos.z)),
                new Vector3(Mathf.Max(startPos.x, endPos.x), targetHeight, Mathf.Max(startPos.z, endPos.z)),
                Mathf.Abs(endPos.x - startPos.x) >= Mathf.Abs(endPos.z - startPos.z),
                Mathf.Abs(endPos.x - startPos.x) >= Mathf.Abs(endPos.z - startPos.z)
                    ? Mathf.Abs(endPos.z - startPos.z)
                    : Mathf.Abs(endPos.x - startPos.x),
                Mathf.Max(1, Mathf.CeilToInt((Mathf.Abs(endPos.x - startPos.x) >= Mathf.Abs(endPos.z - startPos.z)
                    ? Mathf.Abs(endPos.z - startPos.z)
                    : Mathf.Abs(endPos.x - startPos.x)) / (searchSpacing * 0.8f))));
        }

        if (!manualControl && searchWaypoints.Count > 0)
        {
            // NO insertar waypoint de trnsito ficticio (estara fuera del NavMesh a 30m)
            // Transit mode se maneja va targetHeight/moveSpeed en Update(), no con waypoints
            
            currentWaypointIndex = 0;
            agent.SetDestination(searchWaypoints[currentWaypointIndex]);
            Debug.Log($"[SearchPattern] Iniciando patrn con {searchWaypoints.Count} waypoints (transit mode: altura {targetHeight}m  {transitMaxHeightCustom}m)");
        }
    }

    private void GenerateSearchPattern(Vector3 start, Vector3 end)
    {
        searchWaypoints.Clear();
        currentWaypointIndex = 0;

        // Calcular el rea real que debe cubrir (incluyendo el tamao del drone)
        Vector3 realStart = new Vector3(
            Mathf.Min(start.x, end.x),
            targetHeight,
            Mathf.Min(start.z, end.z));

        Vector3 realEnd = new Vector3(
            Mathf.Max(start.x, end.x),
            targetHeight,
            Mathf.Max(start.z, end.z));

        // Asignar variables globales de rea de bsqueda (vital para clamplear en Micro mode)
        searchAreaMin = realStart;
        searchAreaMax = realEnd;

        // Dimensiones reales del rea a cubrir
        float width = realEnd.x - realStart.x;
        float length = realEnd.z - realStart.z;

        // Determinar direccin principal (mejor cobertura)
        bool searchAlongWidth = width >= length;
        float mainAxisLength = searchAlongWidth ? width : length;
        float crossAxisLength = searchAlongWidth ? length : width;

        // Calcular nmero de pasadas necesarias para cobertura completa
        // Asegurar al menos 1 pasada incluso para reas pequeas
        int passes = Mathf.Max(1, Mathf.CeilToInt(crossAxisLength / (searchSpacing * 0.8f)));

        // Generar waypoints en patrn de cortadora de csped (LAWNMOWER):
        // Todas las filas van en la MISMA direccin (inicio  fin),
        // con un retorno vaco (dead-head) entre cada fila para posicionarse
        // al inicio de la siguiente.
        Vector3 rowStart, rowEnd;
        for (int i = 0; i <= passes; i++)
        {
            float crossAxisPos = Mathf.Lerp(0, crossAxisLength, (float)i / passes);

            // Calcular waypoints de inicio y fin de la fila actual
            if (searchAlongWidth)
            {
                rowStart = new Vector3(realStart.x, 0f, realStart.z + crossAxisPos);
                rowEnd   = new Vector3(realEnd.x,   0f, realStart.z + crossAxisPos);
            }
            else
            {
                rowStart = new Vector3(realStart.x + crossAxisPos, 0f, realStart.z);
                rowEnd   = new Vector3(realStart.x + crossAxisPos, 0f, realEnd.z);
            }

            // Agregar fila de barrido (siempre en la misma direccin: rowStart  rowEnd)
            searchWaypoints.Add(rowStart);
            searchWaypoints.Add(rowEnd);

            // Si NO es la ltima fila, agregar retorno vaco (dead-head) al inicio de la siguiente fila
            if (i < passes)
            {
                float nextCrossAxisPos = Mathf.Lerp(0, crossAxisLength, (float)(i + 1) / passes);
                Vector3 nextRowStart;
                if (searchAlongWidth)
                    nextRowStart = new Vector3(realStart.x, 0f, realStart.z + nextCrossAxisPos);
                else
                    nextRowStart = new Vector3(realStart.x + nextCrossAxisPos, 0f, realStart.z);

                searchWaypoints.Add(nextRowStart);
            }
        }

        Debug.Log($"Patrn cortadora generado con {searchWaypoints.Count} waypoints. rea: {width}x{length}m. Pasadas: {passes}");
    }
    private bool TryApplyModeLineRoute(ref Vector3 startPos, ref Vector3 endPos)
    {
        if (!ModeLine) return false;

        if (modeLineRoutes == null || modeLineRoutes.Count == 0)
        {
            Debug.LogWarning("[ModeLine] Activo, pero no hay lneas configuradas. Usando el patrn normal.");
            return false;
        }

        if (currentModeLineRouteIndex < 0 || currentModeLineRouteIndex >= modeLineRoutes.Count)
            currentModeLineRouteIndex = 0;

        int selectedIndex = currentModeLineRouteIndex;
        ModeLineRoute route = modeLineRoutes[selectedIndex];

        startPos = new Vector3(route.startXZ.x, targetHeight, route.startXZ.y);
        endPos = new Vector3(route.endXZ.x, targetHeight, route.endXZ.y);

        currentModeLineRouteIndex++;
        if (currentModeLineRouteIndex >= modeLineRoutes.Count)
        {
            if (loopModeLineRoutes)
            {
                currentModeLineRouteIndex = 0;
                modeLineCycleCompleted = true;
                Debug.Log("[ModeLine] Ciclo de lneas completado. Reiniciando desde la primera.");
            }
            else
            {
                currentModeLineRouteIndex = modeLineRoutes.Count - 1;
            }
        }

        Debug.Log($"[ModeLine] Ruta {selectedIndex + 1}/{modeLineRoutes.Count} '{route.routeName}': ({startPos.x:F1}, {startPos.z:F1}) -> ({endPos.x:F1}, {endPos.z:F1})");
        return true;
    }

    public bool ConsumeModeLineCycleCompleted()
    {
        if (!modeLineCycleCompleted) return false;
        modeLineCycleCompleted = false;
        return true;
    }

    private void GenerateStraightLinePattern(Vector3 start, Vector3 end)
    {
        searchWaypoints.Clear();
        currentWaypointIndex = 0;

        start.y = 0f;
        end.y = 0f;

        searchAreaMin = new Vector3(Mathf.Min(start.x, end.x), 0f, Mathf.Min(start.z, end.z));
        searchAreaMax = new Vector3(Mathf.Max(start.x, end.x), 0f, Mathf.Max(start.z, end.z));

        searchWaypoints.Add(start);
        searchWaypoints.Add(end);

        Debug.Log($"[ModeLine] Lnea recta generada con {searchWaypoints.Count} waypoints.");
    }

    private void RebuildSearchPatternAsZigzag(Vector3 realStart, Vector3 realEnd, bool searchAlongWidth, float crossAxisLength, int passes)
    {
        searchWaypoints.Clear();

        for (int i = 0; i <= passes; i++)
        {
            float crossAxisPos = Mathf.Lerp(0, crossAxisLength, (float)i / passes);
            Vector3 rowStart;
            Vector3 rowEnd;

            if (searchAlongWidth)
            {
                rowStart = new Vector3(realStart.x, 0f, realStart.z + crossAxisPos);
                rowEnd = new Vector3(realEnd.x, 0f, realStart.z + crossAxisPos);
            }
            else
            {
                rowStart = new Vector3(realStart.x + crossAxisPos, 0f, realStart.z);
                rowEnd = new Vector3(realStart.x + crossAxisPos, 0f, realEnd.z);
            }

            if (i % 2 == 0)
            {
                searchWaypoints.Add(rowStart);
                searchWaypoints.Add(rowEnd);
            }
            else
            {
                searchWaypoints.Add(rowEnd);
                searchWaypoints.Add(rowStart);
            }
        }
    }

    private void GenerateReturnPath()
    {
        searchWaypoints.Clear();
        currentWaypointIndex = 0;
        searchWaypoints.Add(repostajePosition);
        if (agent.enabled && agent.isOnNavMesh)
            agent.SetDestination(searchWaypoints[currentWaypointIndex]);
        Debug.Log("Generated return path to base");
    }


    // 
    //  CAMBIO DE MODO   Teclas numricas (fila + teclado num.) + botn UI
    // 
    private void HandleModeInput()
    {
        // Fila numrica superior
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) SwitchMode(NavigationMode.Baseline);
        if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) SwitchMode(NavigationMode.Hover);
        if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) SwitchMode(NavigationMode.Micro);
        if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) SwitchMode(NavigationMode.Skip);
        // Alpha5 / Keypad5 reservados para modo futuro
    }

    private void SwitchMode(NavigationMode newMode)
    {
        if (navigationMode == newMode) return;
        navigationMode = newMode;

        // Limpiar estado Hover si estaba activo
        if (isHoveringForObstacle)
        {
            isHoveringForObstacle = false;
            if (agent.enabled && agent.isOnNavMesh)
                agent.isStopped = false;
        }

        // Limpiar estado Micro si estaba activo
        isMicroManeuvering = false;

        // Limpiar estado Skip
        skipPhase = SkipPhase.Idle;
        skippedPositions.Clear();
        currentSkipIndex = 0;
        isRevisitHovering = false;

        Debug.Log($"[NavMode] Modo activo: {newMode} (tecla {(int)newMode})");
        UpdateNavModeButtonUI();
    }

    /// <summary>
    /// Cicla al siguiente modo de navegacin en orden: BaselineHoverMicroSkipBaseline.
    /// Llamar desde el botn UI de OnClick().
    /// </summary>
    public void CycleNavigationMode()
    {
        // Los valores del enum son 1..4; hacemos mdulo 4 y sumamos 1
        int next = (int)navigationMode % 4 + 1;
        SwitchMode((NavigationMode)next);
    }

    /// <summary>
    /// Actualiza la visibilidad y el texto del botn de navegacin.
    /// El botn solo aparece cuando el drone est en modo autnomo (manualControl == false).
    /// </summary>
    private void UpdateNavModeButtonUI()
    {
        if (navModeButton == null) return;

        // Mostrar solo en modo navegacin
        navModeButton.gameObject.SetActive(!manualControl);

        // Resolver texto: campo propio o GetComponentInChildren como fallback
        TMP_Text label = navModeButtonText
            ?? navModeButton.GetComponentInChildren<TMP_Text>();

        if (label != null)
            label.text = navigationMode.ToString();   // "Baseline", "Hover", "Micro", "Skip"
    }

    // 
    //  HOVER MODE & VISIBILITY  deteccin matemtica del paper
    // 
    
    public float CalculateVisibilityScore()
    {
        // Frmula o_{s,t} del paper: 5 raycasts
        int totalRays = 5;
        int hitsCount = 0;
        float spread = 1.0f; 

        Vector3[] rayOffsets = new Vector3[]
        {
            Vector3.zero,
            new Vector3(spread, 0, 0),
            new Vector3(-spread, 0, 0),
            new Vector3(0, 0, spread),
            new Vector3(0, 0, -spread)
        };

        foreach (Vector3 offset in rayOffsets)
        {
            Vector3 origin = transform.position + offset;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, obstacleDetectionHeight))
            {
                if (!hit.collider.CompareTag("Person") && !hit.collider.CompareTag("Car"))
                    hitsCount++;
            }
            else
            {
                // Sin impacto = rea libre
                hitsCount++;
            }
        }

        return Mathf.Clamp01((float)hitsCount / totalRays);
    }

    private void CheckObstaclesBelow()
    {
        if (Time.time - lastObstacleCheckTime < obstacleCheckInterval) return;
        lastObstacleCheckTime = Time.time;

        float o_st = CalculateVisibilityScore();
        bool obstacleFound = o_st < visibilityThreshold; // o_{s*,t} < tau_o
        Vector3 groundPos = new Vector3(transform.position.x, 0f, transform.position.z);

        if (obstacleFound && !isHoveringForObstacle)
        {
            // Oclusin detectada -> Activar poltica Hover
            isHoveringForObstacle = true;
            currentHoverWaitTime = 0f; // Reiniciar timer
            hoveredObstaclePosition = groundPos;
            if (agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
            Debug.Log($"[Hover] Visibilidad ({o_st:F2}) < umbral ({visibilityThreshold:F2})  Drone en estacin (wait)...");
        }
        else if (obstacleFound && isHoveringForObstacle)
        {
            // Sumar tiempo a Delta t_wait mientras sigue estacionario
            currentHoverWaitTime += Time.deltaTime;
        }
        else if (!obstacleFound && isHoveringForObstacle)
        {
            // Zona despejada -> Reanudar
            isHoveringForObstacle = false;
            if (agent.enabled && agent.isOnNavMesh) agent.isStopped = false;
            Debug.Log($"[Hover] Visibilidad recuperada ({o_st:F2})  Reanudando. Verificando bache...");
            onObstacleCleared?.Invoke(hoveredObstaclePosition);
        }
        // Sin obstculo y sin hover previo  marcha normal (no-op)
    }

    // 
    //  SKIP MODE  dos pasadas con recarga de energa real
    // 

    /// <summary>Dispatcher de fases del modo Skip. Se llama en Update() independientemente de manualControl.</summary>
    private void UpdateSkipMode()
    {
        switch (skipPhase)
        {
            case SkipPhase.FirstPass:
                // No escanear mientras est volviendo a base al final de la 1 pasada
                if (!isReturningToBase) ScanForSkipPositions();
                break;

            case SkipPhase.ReturningAfterFirstPass:
                // El waypoint-loop no puede detectar la llegada porque tiene &&!isReturningToBase.
                // Usamos IsAtBase() para hacer el polling de llegada.
                if (IsAtBase())
                {
                    isReturningToBase = false;
                    hasRoute = false;
                    searchWaypoints.Clear();
                    skipPhase = SkipPhase.Recharging;
                    energyController?.IniciarRecarga();
                    Debug.Log($"[Skip] En base. Recargando energa... {skippedPositions.Count} zona(s) anotadas para revisita.");
                }
                break;

            case SkipPhase.Recharging:
                UpdateRecharging();
                break;

            case SkipPhase.Revisiting:
                UpdateRevisiting();
                break;
        }
    }

    /// <summary>1 pasada: anota posiciones que no cumplen con tau_o.</summary>
    private void ScanForSkipPositions()
    {
        if (Time.time - lastSkipRecordTime < skipRecordCooldown) return;

        float o_st = CalculateVisibilityScore();
        if (o_st >= visibilityThreshold) return; // Si o_st >= tau_o, el segmento es "inspected" en tiempo real

        Vector3 pos = new Vector3(transform.position.x, 0f, transform.position.z);

        // Deduplicacin espacial: ignorar si ya hay una posicin anotada cercana
        foreach (var sp in skippedPositions)
            if (Vector3.Distance(new Vector3(sp.x, 0, sp.z), pos) < skipDeduplicationRadius)
                return;

        skippedPositions.Add(pos);
        lastSkipRecordTime = Time.time;
        Debug.Log($"[Skip] Posicin #{skippedPositions.Count} marcada como Pending: {pos} (o_st={o_st:F2} < {visibilityThreshold:F2})");
    }

    /// <summary>Espera a que EnergyController indique que la recarga est completa (~95%).</summary>
    public void QueueSkipRevisitPosition(Vector3 worldPosition, string reason)
    {
        if (navigationMode != NavigationMode.Skip) return;
        if (skipPhase == SkipPhase.Done || skipPhase == SkipPhase.Idle) return;

        Vector3 pos = new Vector3(worldPosition.x, 0f, worldPosition.z);

        foreach (var sp in skippedPositions)
        {
            if (Vector3.Distance(new Vector3(sp.x, 0f, sp.z), pos) < skipDeduplicationRadius)
                return;
        }

        skippedPositions.Add(pos);
        Debug.Log($"[Skip] Posicin agregada para revisita por {reason}: {pos}");
    }

    private void UpdateRecharging()
    {
        bool cargada = (energyController != null)
            ? energyController.EstaCompleta(skipRechargeThreshold)
            : true;  // si no hay referencia, pasar directamente

        if (!cargada) return;

        if (skippedPositions.Count == 0)
        {
            Debug.Log("[Skip] Sin zonas que revisar. Misin completa.");
            skipPhase = SkipPhase.Done;
            return;
        }

        Debug.Log($"[Skip] Recarga completa ({energyController?.energia:F1}%). Iniciando revisita de {skippedPositions.Count} zona(s)...");
        skipPhase = SkipPhase.Revisiting;
        currentSkipIndex = 0;
        isRevisitHovering = false;

        //  MARCAR INICIO DE SEGUNDA PASADA para rastreo de baches
        if (DigitalTwin.DigitalTwinManager.Instance != null && 
            DigitalTwin.DigitalTwinManager.Instance.movementInterface != null)
        {
            DigitalTwin.DigitalTwinManager.Instance.movementInterface.MarkSecondPassStart();
        }

        // Enviar al primer punto de revisita
        if (agent.enabled && agent.isOnNavMesh)
        {
            Vector3 first = new Vector3(skippedPositions[0].x, 0f, skippedPositions[0].z);
            agent.SetDestination(first);
        }
    }

    /// <summary>Visita cada posicin anotada, verifica baches y retorna a base al terminar.</summary>
    private void BeginSkipRevisit(MovementInterface movementInterface)
    {
        skipPhase = SkipPhase.Revisiting;
        currentSkipIndex = 0;
        isRevisitHovering = false;

        movementInterface?.MarkSecondPassStart();

        if (agent.enabled && agent.isOnNavMesh && skippedPositions.Count > 0)
        {
            Vector3 first = new Vector3(skippedPositions[0].x, 0f, skippedPositions[0].z);
            agent.isStopped = false;
            agent.SetDestination(first);
            lastPathUpdateTime = Time.time;
        }

        Debug.Log($"[Skip] Iniciando revisita de {skippedPositions.Count} zonas al final de la pasada.");
    }

    private void UpdateRevisiting()
    {
        // Ya terminamos todas?
        if (currentSkipIndex >= skippedPositions.Count)
        {
            var dtm = DigitalTwin.DigitalTwinManager.Instance;
            bool isBatchMode = dtm != null && dtm.suppressAutoEndEpisode && !ModeLine;

            if (isBatchMode)
            {
                if (!segmentDone)
                {
                    skipPhase = SkipPhase.Done;
                    segmentDone = true;
                    hasRoute = false;
                    searchWaypoints.Clear();
                    skippedPositions.Clear();
                    Debug.Log($"[Skip]  Revisitas del segmento completadas. Esperando Automator (vuelo continuo).");
                }
            }
            else if (!isReturningToBase)
            {
                skipPhase = SkipPhase.Done;
                isReturningToBase = true;
                missionComplete = true;
                Debug.Log("[Skip] Todas las zonas revisitadas. Retornando a base.");
                GenerateReturnPath();
            }
            return;
        }

        // Hovering sobre la posicin actual
        if (isRevisitHovering)
        {
            if (Time.time - revisitHoverStartTime >= revisitHoverTime)
            {
                isRevisitHovering = false;
                currentSkipIndex++;
                Debug.Log($"[Skip] Revisita {currentSkipIndex}/{skippedPositions.Count} completada.");

                // Navegar al siguiente punto (si hay)
                if (currentSkipIndex < skippedPositions.Count && agent.enabled && agent.isOnNavMesh)
                {
                    Vector3 next = new Vector3(
                        skippedPositions[currentSkipIndex].x, 0f,
                        skippedPositions[currentSkipIndex].z);
                    agent.SetDestination(next);
                }
            }
            return;
        }

        // Navegando hacia el punto: llegamos?
        if (!agent.pathPending && agent.remainingDistance <= stoppingDistance)
        {
            // Llegamos: iniciar hover y verificar bache
            isRevisitHovering = true;
            revisitHoverStartTime = Time.time;
            Debug.Log($"[Skip] Llegado a zona #{currentSkipIndex + 1}. Verificando bache ({revisitHoverTime}s)...");
            onObstacleCleared?.Invoke(skippedPositions[currentSkipIndex]);
        }
        else if (Time.time - lastPathUpdateTime > updatePathInterval)
        {
            // Refrescar destino peridicamente
            Vector3 target = new Vector3(
                skippedPositions[currentSkipIndex].x, 0f,
                skippedPositions[currentSkipIndex].z);
            if (agent.enabled && agent.isOnNavMesh) agent.SetDestination(target);
            lastPathUpdateTime = Time.time;
        }
    }

    // 
    //  MICRO MODE  perturbacin errtica aleatoria
    //  El dron navega hacia el waypoint pero se desva constantemente con
    //  offsets aleatorios que cambian cada ~0.3s, produciendo un patrn
    //  irregular e impredecible (errtico).
    // 
    private float lastMicroWaypointCheck = 0f;
    private float microNoiseTimer = 0f;
    private Vector2 microNoiseOffset = Vector2.zero;  // Offset aleatorio actual

    private void CheckMicroManeuver()
    {
        if (!manualControl && hasRoute && searchWaypoints.Count > 0 
            && currentWaypointIndex < searchWaypoints.Count)
        {
            // Waypoint actual
            Vector3 waypointTarget = searchWaypoints[currentWaypointIndex];
            
            // Verificar si llegamos al waypoint (solo cada 0.5s para evitar mltiples avances)
            Vector3 currentXZ = new Vector3(transform.position.x, 0f, transform.position.z);
            Vector3 waypointXZ = new Vector3(waypointTarget.x, 0f, waypointTarget.z);

            if (Time.time - lastMicroWaypointCheck > 0.5f && Vector3.Distance(currentXZ, waypointXZ) <= stoppingDistance)
            {
                lastMicroWaypointCheck = Time.time;
                currentWaypointIndex++;
                
                if (currentWaypointIndex >= searchWaypoints.Count)
                {
                    // Segmento completado
                    if (!missionComplete)
                    {
                        var mi = DigitalTwin.DigitalTwinManager.Instance?.movementInterface;
                        if (mi != null && mi.isCapturing) mi.AcDc();
                        moveSpeed = transitMaxSpeedCustom;
                        var dtm = DigitalTwin.DigitalTwinManager.Instance;
                        bool isBatchMode = dtm != null && dtm.suppressAutoEndEpisode && !ModeLine;
                        if (isBatchMode)
                        {
                            missionComplete = true;
                            segmentDone = true;
                            hasRoute = false;
                            searchWaypoints.Clear();
                            Debug.Log($"[SearchPattern]  Segmento (Micro) completado. Esperando Automator.");
                        }
                        else
                        {
                            missionComplete = true;
                            isReturningToBase = true;
                            GenerateReturnPath();
                            Debug.Log("[SearchPattern]  Zona (Micro) completada. Retornando a base.");
                        }
                    }
                    return;
                }
                
                waypointTarget = searchWaypoints[currentWaypointIndex];
            }

            // Generar nuevo offset aleatorio cada ~0.3s para comportamiento errtico
            microNoiseTimer -= Time.deltaTime;
            if (microNoiseTimer <= 0f)
            {
                microNoiseTimer = UnityEngine.Random.Range(0.2f, 0.5f);
                // Offset aleatorio en X y Z, con radio entre 1m y 3m
                float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                float radius = UnityEngine.Random.Range(1f, 3f);
                microNoiseOffset = new Vector2(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius
                );
                Debug.Log($"[Micro] Nueva perturbacin: dir={angle*Mathf.Rad2Deg:F0} radio={radius:F1}m");
            }
            
            // Aplicar el offset aleatorio al waypoint (en espacio global XZ)
            Vector3 microTarget = waypointTarget + new Vector3(microNoiseOffset.x, 0f, microNoiseOffset.y);
            
            // Clampeo al bounding box del rea de bsqueda
            microTarget = new Vector3(
                Mathf.Clamp(microTarget.x, searchAreaMin.x, searchAreaMax.x),
                0f,
                Mathf.Clamp(microTarget.z, searchAreaMin.z, searchAreaMax.z)
            );

            agent.SetDestination(microTarget);
            currentMicroDelta = microNoiseOffset;
        }
    }

    private void SmoothWaypoints()
    {
        if (searchWaypoints.Count < 3) return;

        List<Vector3> smoothed = new List<Vector3>();
        smoothed.Add(searchWaypoints[0]);

        for (int i = 1; i < searchWaypoints.Count - 1; i++)
        {
            // Average between previous, current and next waypoint
            Vector3 smoothedPoint = (searchWaypoints[i - 1] + searchWaypoints[i] + searchWaypoints[i + 1]) / 3f;
            smoothedPoint.y = targetHeight;
            smoothed.Add(smoothedPoint);
        }

        smoothed.Add(searchWaypoints[searchWaypoints.Count - 1]);
        searchWaypoints = smoothed;
    }

    public void ToggleControlMode()
    {
        manualControl = !manualControl;
        ConfigureNavAgent(!manualControl);

        if (!manualControl && hasRoute && searchWaypoints.Count > 0)
        {
            currentWaypointIndex = Mathf.Clamp(currentWaypointIndex, 0, searchWaypoints.Count - 1);
            agent.Warp(transform.position);
            agent.SetDestination(searchWaypoints[currentWaypointIndex]);
        }
        else if (manualControl)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        UpdateNavModeButtonUI();
    }

    private void ConfigureNavAgent(bool active)
    {
        if (active)
        {
            agent.enabled = true;
            agent.updatePosition = true;
            agent.updateRotation = true;
            agent.stoppingDistance = stoppingDistance;
            agent.speed = moveSpeed;
            agent.angularSpeed = navMeshAngularSpeed;
            agent.acceleration = moveSpeed * 5;
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.None;
        }
        else
        {
            // Reset path only if agent is active and on a NavMesh to avoid errors
            if (agent.enabled && agent.isOnNavMesh)
            {
                agent.ResetPath();
            }
            
            agent.enabled = false;
            rb.isKinematic = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }
    }

    [Header("Mobile Controls")]
    public VirtualJoystick virtualJoystick;
    public VirtualJoystick heightJoystick; // Optional second joystick for height/rot

    private void HandleInput()
    {
        //  Ajuste de altura: I (subir) / K (bajar)  funciona en AMBOS modos 
        if (Input.GetKey(KeyCode.I))
            targetHeight += 5f * Time.deltaTime;
        if (Input.GetKey(KeyCode.K))
        {
            targetHeight -= 5f * Time.deltaTime;
            targetHeight = Mathf.Max(targetHeight, minHeight);
        }

        if (manualControl)
        {
            // Keyboard Input
            float vKeyboard = Input.GetKey(KeyCode.Space) ? 1 : Input.GetKey(KeyCode.LeftControl) ? -1 : 0;
            Vector2 mKeyboard = new Vector2(
                Input.GetKey(KeyCode.Q) ? -1 : Input.GetKey(KeyCode.E) ? 1 : 0,
                Input.GetKey(KeyCode.W) ? 1 : Input.GetKey(KeyCode.S) ? -1 : 0
            );
            float rKeyboard = Input.GetKey(KeyCode.A) ? -1 : Input.GetKey(KeyCode.D) ? 1 : 0;

            // Touch Input (Joystick)
            Vector3 joyInput = (virtualJoystick != null) ? virtualJoystick.InputDirection : Vector3.zero;
            Vector3 heightJoyInput = (heightJoystick != null) ? heightJoystick.InputDirection : Vector3.zero;

            // Combine inputs (Keyboard + Joystick)
            verticalInput = vKeyboard + heightJoyInput.y;
            movementInput.x = mKeyboard.x + joyInput.x;
            movementInput.y = mKeyboard.y + joyInput.y;
            currentRotation = rKeyboard + heightJoyInput.x;
            
            // Clamp to ensure we don't exceed 1.0 force if using both
            verticalInput = Mathf.Clamp(verticalInput, -1f, 1f);
            movementInput = Vector2.ClampMagnitude(movementInput, 1f);
            currentRotation = Mathf.Clamp(currentRotation, -1f, 1f);
        }
        else
        {
            verticalInput = 0;
            movementInput = Vector2.zero;
            currentRotation = 0;
        }
    }

    private void HandleAltitude()
    {
        float currentHeight = transform.position.y;

        if (currentHeight < minHeight)
        {
            rb.position = new Vector3(rb.position.x, minHeight, rb.position.z);
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, Mathf.Max(0, rb.linearVelocity.y), rb.linearVelocity.z);
            currentHeight = minHeight;
        }

        float heightError = targetHeight - currentHeight;
        float pidOutput = 0f;

        if (heightPID != null)
        {
            if (Mathf.Abs(heightError) < heightDeadZone)
            {
                heightPID.ResetIntegral();
            }

            pidOutput = heightPID.Update(heightError, Time.fixedDeltaTime);

            if (pidOutput < 0 && currentHeight >= targetHeight)
            {
                pidOutput *= 0.3f;
            }

            if (heightError > 0.5f)
            {
                pidOutput *= 1.5f;
            }
        }

        Vector3 verticalForce = Vector3.up * (pidOutput + verticalInput * maxAscendSpeed);
        rb.AddForce(verticalForce, ForceMode.Acceleration);
    }

    private void HandleManualMovement()
    {
        if (movementInput.magnitude > 0)
        {
            Vector3 moveDirection = new Vector3(movementInput.x, 0, movementInput.y);
            moveDirection = transform.TransformDirection(moveDirection);
            rb.AddForce(moveDirection * moveSpeed, ForceMode.Acceleration);
        }
    }

    private void HandleRotation()
    {
        if (currentRotation != 0)
        {
            float rotationAmount = currentRotation * manualRotationSpeed * Time.fixedDeltaTime;
            rb.AddTorque(Vector3.up * rotationAmount, ForceMode.VelocityChange);
        }
    }

    private void ApplyMotorRotation()
    {
        float speed = motorRotationSpeed;  // Usar la nica variable de velocidad

        for (int i = 0; i < motors.Length; i++)
        {
            // Si motorRotationSpeed es 0, mantener la rotacion actual.
            if (speed > 0f)
            {
                float rotationDirection = alternateRotation ? (i % 2 == 0 ? 1 : -1) : 1;
                motorRotationAngles[i] += speed * rotationDirection * Time.deltaTime;

                if (motorRotationAngles[i] > 360f) motorRotationAngles[i] -= 360f;
                if (motorRotationAngles[i] < -360f) motorRotationAngles[i] += 360f;
            }

            motors[i].localRotation = Quaternion.Euler(0f, motorRotationAngles[i], 0f);
        }
    }

    private void ForceStabilization()
    {
        Quaternion currentRot = transform.rotation;
        float currentYRotation = currentRot.eulerAngles.y;
        Quaternion targetRotation = Quaternion.Euler(0, currentYRotation, 0);

        Quaternion rotationDiff = targetRotation * Quaternion.Inverse(currentRot);
        Vector3 rotationDiffEuler = rotationDiff.eulerAngles;

        rotationDiffEuler.x = (rotationDiffEuler.x > 180) ? rotationDiffEuler.x - 360 : rotationDiffEuler.x;
        rotationDiffEuler.z = (rotationDiffEuler.z > 180) ? rotationDiffEuler.z - 360 : rotationDiffEuler.z;

        Vector3 stabilizationTorque = new Vector3(
            rotationDiffEuler.x * stabilizationSpeed,
            0,
            rotationDiffEuler.z * stabilizationSpeed
        );

        stabilizationTorque = Vector3.ClampMagnitude(stabilizationTorque, maxStabilizationTorque);
        rb.AddTorque(stabilizationTorque, ForceMode.Acceleration);

        Vector3 angularVelocity = rb.angularVelocity;
        if (currentRotation == 0)
        {
            angularVelocity.y = Mathf.Lerp(angularVelocity.y, 0, Time.fixedDeltaTime * stabilizationSpeed);
        }
        angularVelocity.x = Mathf.Lerp(angularVelocity.x, 0, Time.fixedDeltaTime * stabilizationSpeed * 2);
        angularVelocity.z = Mathf.Lerp(angularVelocity.z, 0, Time.fixedDeltaTime * stabilizationSpeed * 2);

        rb.angularVelocity = angularVelocity;
    }

    // Visualize waypoints in editor
    void OnDrawGizmosSelected()
    {
        if (searchWaypoints != null && searchWaypoints.Count > 0)
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < searchWaypoints.Count - 1; i++)
            {
                Gizmos.DrawLine(searchWaypoints[i], searchWaypoints[i + 1]);
                Gizmos.DrawSphere(searchWaypoints[i], 0.1f);
            }
            Gizmos.DrawSphere(searchWaypoints[searchWaypoints.Count - 1], 0.1f);

            // Draw current target
            if (currentWaypointIndex < searchWaypoints.Count)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(searchWaypoints[currentWaypointIndex], 0.2f);
            }
        }
    }
        public void ReturnToBase()
    {
        missionComplete = true;
        isReturningToBase = true;
        hasRoute = true;
        moveSpeed = transitMaxSpeedCustom;  // Acelerar para el regreso

        // Apagar ACDC si estaba grabando
        var mi = DigitalTwin.DigitalTwinManager.Instance?.movementInterface;
        if (mi != null && mi.isCapturing) mi.AcDc();

        searchWaypoints.Clear();
        currentWaypointIndex = 0;

        if (agent.enabled && agent.isOnNavMesh)
        {
            agent.ResetPath();
            agent.SetDestination(repostajePosition);
        }

        Debug.Log("[DroneController]  Retornando a base.");
    }

public bool IsManualControl()
{
    return manualControl;
}

public bool IsReturningToBase()
{
    return isReturningToBase;
}

/// <summary>Devuelve la cantidad de posiciones marcadas para revisita en Skip mode.</summary>
public int GetSkippedPositionsCount()
{
    return skippedPositions.Count;
}

    public bool IsAtBase()
    {
        // Ignorar la altura (Y) para calcular la distancia, ya que el dron vuela a varios metros del suelo.
        Vector3 pos2D = new Vector3(transform.position.x, 0f, transform.position.z);
        Vector3 base2D = new Vector3(repostajePosition.x, 0f, repostajePosition.z);
        
        return Vector3.Distance(pos2D, base2D) < 2.5f;
    }
    private const string PREFS_PREFIX = "DroneState_";

    /// <summary>Guarda el estado actual del drone en PlayerPrefs (persiste reinicios de PC).</summary>
    private void SaveDroneState()
    {
        var mi = DigitalTwin.DigitalTwinManager.Instance?.movementInterface;
        var hc = FindFirstObjectByType<DroneHeightController>();

        // Guardar en PlayerPrefs (disco) para que sobreviva a reinicio de PC
        PlayerPrefs.SetInt(PREFS_PREFIX + "navigationMode", (int)navigationMode);
        PlayerPrefs.SetInt(PREFS_PREFIX + "manualControl", manualControl ? 1 : 0);
        PlayerPrefs.SetInt(PREFS_PREFIX + "hasRoute", hasRoute ? 1 : 0);
        PlayerPrefs.SetInt(PREFS_PREFIX + "isReturningToBase", isReturningToBase ? 1 : 0);
        PlayerPrefs.SetInt(PREFS_PREFIX + "missionComplete", missionComplete ? 1 : 0);
        PlayerPrefs.SetInt(PREFS_PREFIX + "segmentDone", segmentDone ? 1 : 0);
        PlayerPrefs.SetInt(PREFS_PREFIX + "isInTransitMode", isInTransitMode ? 1 : 0);
        PlayerPrefs.SetInt(PREFS_PREFIX + "isHoveringForObstacle", isHoveringForObstacle ? 1 : 0);
        PlayerPrefs.SetInt(PREFS_PREFIX + "isMicroManeuvering", isMicroManeuvering ? 1 : 0);
        PlayerPrefs.SetFloat(PREFS_PREFIX + "transitTargetCenterX", transitTargetCenter.x);
        PlayerPrefs.SetFloat(PREFS_PREFIX + "transitTargetCenterZ", transitTargetCenter.z);
        PlayerPrefs.SetFloat(PREFS_PREFIX + "searchAreaMinX", searchAreaMin.x);
        PlayerPrefs.SetFloat(PREFS_PREFIX + "searchAreaMinZ", searchAreaMin.z);
        PlayerPrefs.SetFloat(PREFS_PREFIX + "searchAreaMaxX", searchAreaMax.x);
        PlayerPrefs.SetFloat(PREFS_PREFIX + "searchAreaMaxZ", searchAreaMax.z);
        PlayerPrefs.SetInt(PREFS_PREFIX + "currentWaypointIndex", currentWaypointIndex);
        PlayerPrefs.SetFloat(PREFS_PREFIX + "targetHeight", targetHeight);
        PlayerPrefs.SetFloat(PREFS_PREFIX + "minHeight", minHeight);
        PlayerPrefs.SetInt(PREFS_PREFIX + "isCapturing", (mi != null && mi.isCapturing) ? 1 : 0);
        PlayerPrefs.SetInt(PREFS_PREFIX + "heightLevel", hc != null ? (int)hc.currentLevel : 0);
        PlayerPrefs.SetInt(PREFS_PREFIX + "skipPhase", (int)skipPhase);
        PlayerPrefs.SetInt(PREFS_PREFIX + "currentSkipIndex", currentSkipIndex);
        PlayerPrefs.SetInt(PREFS_PREFIX + "isRevisitHovering", isRevisitHovering ? 1 : 0);
        PlayerPrefs.SetFloat(PREFS_PREFIX + "revisitHoverStartTime", revisitHoverStartTime);

        // Guardar waypoints como string (cantidad + coordenadas separadas por |)
        string wpStr = searchWaypoints.Count.ToString();
        foreach (var wp in searchWaypoints)
            wpStr += "|" + wp.x + "," + wp.z;
        PlayerPrefs.SetString(PREFS_PREFIX + "searchWaypoints", wpStr);

        // Guardar skippedPositions
        string spStr = skippedPositions.Count.ToString();
        foreach (var sp in skippedPositions)
            spStr += "|" + sp.x + "," + sp.z;
        PlayerPrefs.SetString(PREFS_PREFIX + "skippedPositions", spStr);

        PlayerPrefs.SetInt(PREFS_PREFIX + "saved", 1);  // Flag de que hay estado guardado
        PlayerPrefs.Save();

        Debug.Log($"[State] Estado GUARDADO EN DISCO: modo={navigationMode}, waypoint={currentWaypointIndex}/{searchWaypoints.Count}, altura={targetHeight}m");
    }

    /// <summary>Restaura el estado guardado del drone desde PlayerPrefs (tras reinicio de PC).</summary>
    private void RestoreDroneState()
    {
        if (PlayerPrefs.GetInt(PREFS_PREFIX + "saved", 0) == 0)
        {
            Debug.Log("[State] No hay estado guardado en disco.");
            return;
        }

        navigationMode = (NavigationMode)PlayerPrefs.GetInt(PREFS_PREFIX + "navigationMode", (int)NavigationMode.Baseline);
        manualControl = PlayerPrefs.GetInt(PREFS_PREFIX + "manualControl", 1) == 1;
        hasRoute = PlayerPrefs.GetInt(PREFS_PREFIX + "hasRoute", 0) == 1;
        isReturningToBase = PlayerPrefs.GetInt(PREFS_PREFIX + "isReturningToBase", 0) == 1;
        missionComplete = PlayerPrefs.GetInt(PREFS_PREFIX + "missionComplete", 0) == 1;
        segmentDone = PlayerPrefs.GetInt(PREFS_PREFIX + "segmentDone", 0) == 1;
        isInTransitMode = PlayerPrefs.GetInt(PREFS_PREFIX + "isInTransitMode", 0) == 1;
        isHoveringForObstacle = PlayerPrefs.GetInt(PREFS_PREFIX + "isHoveringForObstacle", 0) == 1;
        isMicroManeuvering = PlayerPrefs.GetInt(PREFS_PREFIX + "isMicroManeuvering", 0) == 1;
        transitTargetCenter = new Vector3(
            PlayerPrefs.GetFloat(PREFS_PREFIX + "transitTargetCenterX", 0f),
            0f,
            PlayerPrefs.GetFloat(PREFS_PREFIX + "transitTargetCenterZ", 0f));
        searchAreaMin = new Vector3(
            PlayerPrefs.GetFloat(PREFS_PREFIX + "searchAreaMinX", 0f),
            0f,
            PlayerPrefs.GetFloat(PREFS_PREFIX + "searchAreaMinZ", 0f));
        searchAreaMax = new Vector3(
            PlayerPrefs.GetFloat(PREFS_PREFIX + "searchAreaMaxX", 0f),
            0f,
            PlayerPrefs.GetFloat(PREFS_PREFIX + "searchAreaMaxZ", 0f));
        currentWaypointIndex = PlayerPrefs.GetInt(PREFS_PREFIX + "currentWaypointIndex", 0);
        targetHeight = PlayerPrefs.GetFloat(PREFS_PREFIX + "targetHeight", 6f);
        minHeight = PlayerPrefs.GetFloat(PREFS_PREFIX + "minHeight", 3f);
        skipPhase = (SkipPhase)PlayerPrefs.GetInt(PREFS_PREFIX + "skipPhase", 0);
        currentSkipIndex = PlayerPrefs.GetInt(PREFS_PREFIX + "currentSkipIndex", 0);
        isRevisitHovering = PlayerPrefs.GetInt(PREFS_PREFIX + "isRevisitHovering", 0) == 1;
        revisitHoverStartTime = PlayerPrefs.GetFloat(PREFS_PREFIX + "revisitHoverStartTime", 0f);

        // Restaurar waypoints
        searchWaypoints.Clear();
        string wpStr = PlayerPrefs.GetString(PREFS_PREFIX + "searchWaypoints", "");
        if (!string.IsNullOrEmpty(wpStr))
        {
            string[] parts = wpStr.Split('|');
            if (parts.Length > 0 && int.TryParse(parts[0], out int wpCount))
            {
                for (int i = 1; i <= wpCount && i < parts.Length; i++)
                {
                    string[] coords = parts[i].Split(',');
                    if (coords.Length == 2 && float.TryParse(coords[0], out float wx) && float.TryParse(coords[1], out float wz))
                        searchWaypoints.Add(new Vector3(wx, 0f, wz));
                }
            }
        }

        // Restaurar skippedPositions
        skippedPositions.Clear();
        string spStr = PlayerPrefs.GetString(PREFS_PREFIX + "skippedPositions", "");
        if (!string.IsNullOrEmpty(spStr))
        {
            string[] parts = spStr.Split('|');
            if (parts.Length > 0 && int.TryParse(parts[0], out int spCount))
            {
                for (int i = 1; i <= spCount && i < parts.Length; i++)
                {
                    string[] coords = parts[i].Split(',');
                    if (coords.Length == 2 && float.TryParse(coords[0], out float sx) && float.TryParse(coords[1], out float sz))
                        skippedPositions.Add(new Vector3(sx, 0f, sz));
                }
            }
        }

        // Restaurar MovementInterface (ACDC)
        bool shouldBeCapturing = PlayerPrefs.GetInt(PREFS_PREFIX + "isCapturing", 0) == 1;
        var mi = DigitalTwin.DigitalTwinManager.Instance?.movementInterface;
        if (mi != null && shouldBeCapturing != mi.isCapturing)
            mi.AcDc();

        // Restaurar DroneHeightController
        int targetLevel = PlayerPrefs.GetInt(PREFS_PREFIX + "heightLevel", 0);
        var hc = FindFirstObjectByType<DroneHeightController>();
        if (hc != null)
        {
            while ((int)hc.currentLevel != targetLevel)
                hc.CycleHeightMode();
        }

        // Si no est en manual, reactivar el NavMeshAgent y reanudar ruta
        if (!manualControl && hasRoute && searchWaypoints.Count > 0)
        {
            ConfigureNavAgent(true);
            currentWaypointIndex = Mathf.Clamp(currentWaypointIndex, 0, searchWaypoints.Count - 1);
            agent.Warp(transform.position);
            agent.SetDestination(searchWaypoints[currentWaypointIndex]);
            Debug.Log($"[State] Ruta reanudada: waypoint {currentWaypointIndex}/{searchWaypoints.Count}");
        }

        // Limpiar flag para no restaurar dos veces
        PlayerPrefs.DeleteKey(PREFS_PREFIX + "saved");
        PlayerPrefs.Save();

        Debug.Log($"[State] Estado RESTAURADO DESDE DISCO: modo={navigationMode}, altura={targetHeight}m, waypoints={searchWaypoints.Count}");
    }

    public void ApagarDrone()
    {
        if (apagado || apagando) return;

        Debug.Log("Iniciando apagado del dron...");

        // GUARDAR ESTADO antes de apagar
        SaveDroneState();

        apagando = true;
        manualControl = false;
        hasRoute = false;
        isReturningToBase = false;

        agent.enabled = false;
        rb.isKinematic = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        tiempoApagado = 0f;
    }
private void FinalizarApagado()
{
    apagado = true;
    apagando = false;

    rb.linearVelocity = Vector3.zero;
    rb.angularVelocity = Vector3.zero;
    rb.isKinematic = true;

    Debug.Log("Dron apagado completamente.");
}


public bool EstaApagado()
{
    return apagado;
}
public bool IsFullyShutdown()
{
    return apagado;
}
}

public class PIDController
{
    private float Kp, Ki, Kd;
    private float integral;
    private float lastError;
    private float maxIntegral = 10f;

    public PIDController(float Kp, float Ki, float Kd)
    {
        this.Kp = Kp;
        this.Ki = Ki;
        this.Kd = Kd;
    }

    public float Update(float error, float deltaTime)
    {
        integral += error * deltaTime;
        integral = Mathf.Clamp(integral, -maxIntegral, maxIntegral);

        float derivative = (error - lastError) / deltaTime;
        lastError = error;

        return Kp * error + Ki * integral + Kd * derivative;
    }

    public void ResetIntegral()
    {
        integral = 0;
    }


}

