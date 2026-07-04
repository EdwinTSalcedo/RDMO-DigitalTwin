using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Patrulla rectangular alrededor de casas/targets con evasión simple de obstáculos.
/// </summary>
public class RectangularPatrol : MonoBehaviour
{
    [Header("Configuración Básica")]
    [Tooltip("Se asigna automáticamente al inicio: la casa con tag 'Houses' más cercana al agente.")]
    public Transform targetHouse;
    [Tooltip("Ancho del área de patrulla desde la pared hasta la acera.")]
    public float paddingDistance = 2f;
    public float moveSpeed = 5f;
    [Range(0f, 1f)] public float rotationSmoothness = 0.1f;
    public bool clockwise = true;

    [Header("Ajuste de Suelo (Gravedad)")]
    [Tooltip("Desactívalo si el objeto ya usa un Rigidbody. Actívalo para pegarlo al suelo por código.")]
    public bool useArtificialGravity = false;
    [Tooltip("Ajuste de altura si el pivote del modelo 3D está en la cintura y no en los pies.")]
    public float groundOffset = 0.9f;

    [Header("Cambio de Target")]
    [Tooltip("Se rellena automáticamente al inicio buscando objetos con tag 'Houses'. No asignar manualmente.")]
    public Transform[] routeTargets;
    public float switchDistance = 20f;
    public float switchCheckInterval = 0.5f;
    public bool randomTargetSelection = true;
    public float minPatrolTime = 5f;
    public float maxPatrolTime = 10f;

    [Tooltip("Pausa el juego y dibuja líneas hacia las casas evaluadas (Verde=Libre, Rojo=Bloqueada por otra casa).")]
    public bool debugTargetSelection = false;

    [Header("Evasión de Obstáculos")]
    public bool avoidObstacles = true;
    [Tooltip("Distancia a la que detecta obstáculos por delante.")]
    public float avoidanceDistance = 1.2f;
    [Tooltip("Radio del SphereCast (tamaño del cuerpo del personaje).")]
    public float bodyRadius = 0.3f;
    [Tooltip("Altura de la esfera detectora respecto al suelo.")]
    public float sensorHeightOffset = 0.05f;

    // --- Estado interno ---
    private Vector3[] corners = new Vector3[4];
    private int currentCornerIndex = 0;
    private bool isReady = false;
    private bool isTransitioning = false;
    private float distanceThreshold = 0.5f;
    private float lastSwitchCheckTime = 0f;
    private Transform previousTarget = null;
    private float blockTargetSearchTimer = 0f;
    private Vector3 smoothDir = Vector3.zero;
    private float avoidanceBlend = 0f;
    private float yieldTimer = 0f;
    private const float YIELD_TIME = 0.8f;
    private Bounds cachedHouseBounds;

    // --- Atasco ---
    private Vector3 lastPosition;
    private float stuckTimer = 0f;
    private const float STUCK_DIST = 0.05f;
    private const float STUCK_THRESHOLD = 1.5f;
    
    // --- NUEVO: Bloqueo mutuo ---
    private float mutualBlockTimer = 0f;
    private Transform lastBlockedAgent = null;
    private const float MUTUAL_BLOCK_THRESHOLD = 2.0f;
    
    // --- NUEVO: Solución nuclear ---
    private float stuckTime = 0f;
    private const float MAX_STUCK_TIME = 5.0f;

    // Buffer pre-asignado
    private readonly RaycastHit[] raycastBuffer = new RaycastHit[16];
    private readonly Collider[] overlapBuffer = new Collider[20];

    // Debug visual
    private bool dbHit;
    private Vector3 dbHitPoint, dbHitNormal, dbOrigin, dbDir;

    void Start()
    {
        StartCoroutine(WaitForSceneAndInit());
    }

    private void OnEnable()
    {
        // Cuando el objeto se reactiva, buscar houses nuevamente
        if (routeTargets != null && routeTargets.Length > 0)
            return; // Ya inicializado
        
        StartCoroutine(InitializeHouses());
    }

    private IEnumerator WaitForSceneAndInit()
    {
        SceneInitializer sceneInit = FindFirstObjectByType<SceneInitializer>();
        if (sceneInit != null)
        {
            yield return new WaitUntil(() => sceneInit.IsInitializeComplete);
        }
        else
        {
            yield return null;
        }

        // Esperar 10 segundos adicionales antes de reconocer waypoints
        yield return new WaitForSeconds(10f);

        yield return StartCoroutine(InitializeHouses());
    }

    private IEnumerator InitializeHouses()
    {

        // Buscar casas incluyendo desactivadas
        GameObject[] houseObjects = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(go => go.CompareTag("Houses")).ToArray();
        if (houseObjects.Length == 0)
        {
            Debug.LogError("[RectangularPatrol] No se encontraron GameObjects con tag 'Houses'. Asegúrate de que existen y tienen ese tag.");
            yield break;
        }

        routeTargets = new Transform[houseObjects.Length];
        for (int i = 0; i < houseObjects.Length; i++)
            routeTargets[i] = houseObjects[i].transform;

        float closestDist = float.MaxValue;
        foreach (Transform t in routeTargets)
        {
            if (t == null) continue;
            float d = Vector3.Distance(transform.position, t.position);
            if (d < closestDist)
            {
                closestDist = d;
                targetHouse = t;
            }
        }

        if (targetHouse == null)
        {
            Debug.LogError($"[RectangularPatrol] '{gameObject.name}' no pudo determinar una targetHouse inicial.");
            yield break;
        }

        CalculateCorners();
        yield break;
    }

    void Update()
    {
        if (!isReady) return;

        if (blockTargetSearchTimer > 0f)
            blockTargetSearchTimer -= Time.deltaTime;

        if (!isTransitioning && blockTargetSearchTimer <= 0f && routeTargets != null && routeTargets.Length > 0)
        {
            if (Time.time - lastSwitchCheckTime >= switchCheckInterval)
            {
                lastSwitchCheckTime = Time.time;
                TrySelectNextTarget();
            }
        }

        if (isTransitioning && routeTargets != null)
        {
            int hitCount = Physics.OverlapSphereNonAlloc(transform.position + Vector3.up * sensorHeightOffset, avoidanceDistance, overlapBuffer);
            bool intercepted = false;
            for (int i = 0; i < hitCount; i++)
            {
                Collider c = overlapBuffer[i];
                if (c == null) continue;
                Transform hitT = c.transform;

                foreach (Transform rt in routeTargets)
                {
                    if (rt == null || rt == targetHouse || rt == previousTarget) continue;

                    if (hitT == rt || hitT.IsChildOf(rt))
                    {
                        targetHouse = rt;
                        CalculateCorners();
                        intercepted = true;
                        break;
                    }
                }
                if (intercepted) break;
            }
        }

        Vector3 targetPos = new Vector3(corners[currentCornerIndex].x, transform.position.y, corners[currentCornerIndex].z);
        Vector3 desiredDir = (targetPos - transform.position).normalized;
        if (desiredDir == Vector3.zero) desiredDir = transform.forward;

        // --- MEJORADO: Manejo de yieldTimer ---
        if (yieldTimer > 0f)
        {
            yieldTimer -= Time.deltaTime;
            
            smoothDir = Vector3.Lerp(smoothDir, Vector3.zero, 15f * Time.deltaTime);
            
            if (smoothDir.sqrMagnitude < 0.01f)
            {
                smoothDir = Vector3.zero;
                if (isReady && corners != null)
                {
                    Vector3 waitDir = (targetPos - transform.position).normalized;
                    if (waitDir != Vector3.zero)
                    {
                        transform.rotation = Quaternion.Slerp(transform.rotation, 
                            Quaternion.LookRotation(waitDir), 2f * Time.deltaTime);
                    }
                }
            }
            else
            {
                transform.position = Vector3.MoveTowards(transform.position, transform.position + smoothDir, moveSpeed * Time.deltaTime);
                if (smoothDir != Vector3.zero)
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(smoothDir.normalized), rotationSmoothness);
            }
            return;
        }

        Vector3 avoidDir = desiredDir;
        float distToCorner = Vector3.Distance(transform.position, targetPos);
        bool tooCloseToCorner = distToCorner < avoidanceDistance * 2f;
        bool obstacleDetected = avoidObstacles && !tooCloseToCorner && TryGetAvoidanceDir(desiredDir, out avoidDir);

        avoidanceBlend = Mathf.MoveTowards(avoidanceBlend, obstacleDetected ? 1f : 0f, 8f * Time.deltaTime);

        Vector3 targetDir = Vector3.Slerp(desiredDir, avoidDir, avoidanceBlend).normalized;
        
        // --- NUEVO: Jitter aleatorio para romper oscilaciones ---
        if (avoidanceBlend > 0.5f)
        {
            float jitter = Random.Range(-0.3f, 0.3f);
            targetDir = Quaternion.Euler(0, jitter, 0) * targetDir;
        }

        if (smoothDir == Vector3.zero) smoothDir = targetDir;
        smoothDir = Vector3.Slerp(smoothDir, targetDir, 10f * Time.deltaTime);

        transform.position = Vector3.MoveTowards(transform.position, transform.position + smoothDir, moveSpeed * Time.deltaTime);

        if (smoothDir != Vector3.zero)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(smoothDir), rotationSmoothness);

        if (Vector3.Distance(transform.position, targetPos) < distanceThreshold)
        {
            if (isTransitioning)
            {
                isTransitioning = false;
                blockTargetSearchTimer = Random.Range(minPatrolTime, maxPatrolTime);
            }
            currentCornerIndex = (currentCornerIndex + 1) % corners.Length;
        }

        // --- Detección de atasco físico ---
        if (Vector3.Distance(transform.position, lastPosition) < STUCK_DIST)
        {
            stuckTimer += Time.deltaTime;
            
            // --- NUEVO: Contador de tiempo atascado total ---
            stuckTime += Time.deltaTime;
            
            if (stuckTimer >= STUCK_THRESHOLD)
            {
                stuckTimer = 0f;
                previousTarget = null;
                currentCornerIndex = (currentCornerIndex + 1) % corners.Length;
                
                // --- NUEVO: Solución nuclear ---
                if (stuckTime > MAX_STUCK_TIME)
                {
                    Vector3 escapeDir = (targetHouse.position - transform.position).normalized;
                    escapeDir.y = 0;
                    if (escapeDir == Vector3.zero) escapeDir = transform.forward;
                    
                    transform.position += escapeDir * 3f;
                    
                    stuckTime = 0f;
                    stuckTimer = 0f;
                    mutualBlockTimer = 0f;
                    yieldTimer = 0f;
                    lastBlockedAgent = null;
                    
                    Debug.LogWarning($"[{gameObject.name}] Teletransporte de emergencia por bloqueo infinito");
                }
            }
        }
        else
        {
            stuckTimer = 0f;
            stuckTime = 0f; // Resetear solo si se mueve
            lastPosition = transform.position;
        }

        if (useArtificialGravity)
        {
            RaycastHit groundHit;
            if (Physics.Raycast(transform.position + Vector3.up * 1f, Vector3.down, out groundHit, 5f))
            {
                bool hitHouse = false;
                if (routeTargets != null)
                    foreach (Transform rt in routeTargets)
                        if (rt != null && groundHit.transform.IsChildOf(rt)) { hitHouse = true; break; }

                if (!hitHouse && groundHit.transform != transform && !groundHit.collider.isTrigger)
                {
                    Vector3 p = transform.position;
                    p.y = groundHit.point.y + groundOffset;
                    transform.position = p;
                }
            }
        }
    }

    private void TrySelectNextTarget()
    {
        Transform nextTarget = null;
        float closestDist = float.MaxValue;
        bool didEvaluate = false;

        if (randomTargetSelection)
        {
            List<Transform> valid = new List<Transform>();
            foreach (Transform t in routeTargets)
            {
                if (t == null || t == targetHouse || t == previousTarget) continue;
                
                float distToT = Vector3.Distance(transform.position, t.position);
                if (distToT <= switchDistance)
                {
                    didEvaluate = true;
                    bool hasLoS = HasLineOfSightToTarget(t);
                    
                    if (debugTargetSelection)
                    {
                        Vector3 origin = transform.position + Vector3.up * 0.4f;
                        Vector3 dest = t.position + Vector3.up * 0.4f;
                        Debug.DrawLine(origin, dest, hasLoS ? Color.green : Color.red, 5f);
                    }

                    if (hasLoS) valid.Add(t);
                }
            }
            if (valid.Count > 0)
            {
                nextTarget = valid[Random.Range(0, valid.Count)];
                closestDist = Vector3.Distance(transform.position, nextTarget.position);
            }
        }
        else
        {
            foreach (Transform t in routeTargets)
            {
                if (t == null || t == targetHouse) continue;
                float d = Vector3.Distance(transform.position, t.position);
                if (d <= switchDistance)
                {
                    didEvaluate = true;
                    bool hasLoS = HasLineOfSightToTarget(t);

                    if (debugTargetSelection)
                    {
                        Vector3 origin = transform.position + Vector3.up * 1.5f;
                        Vector3 dest = t.position + Vector3.up * 1.5f;
                        Debug.DrawLine(origin, dest, hasLoS ? Color.green : Color.red, 5f);
                    }

                    if (hasLoS && d < closestDist)
                    {
                        closestDist = d; 
                        nextTarget = t;
                    }
                }
            }
        }

        if (debugTargetSelection && didEvaluate)
        {
            Debug.Break();
        }

        if (nextTarget != null && (randomTargetSelection || closestDist <= switchDistance))
        {
            previousTarget = targetHouse;
            targetHouse = nextTarget;
            if (randomTargetSelection) clockwise = Random.value > 0.5f;
            isTransitioning = true;
            CalculateCorners();
        }
    }

    private bool TryGetAvoidanceDir(Vector3 desiredDir, out Vector3 avoidDir)
    {
        avoidDir = desiredDir;
        Vector3 origin = transform.position + Vector3.up * sensorHeightOffset;
        dbOrigin = origin;
        dbDir = desiredDir;
        dbHit = false;

        RaycastHit hit;
        if (!Physics.SphereCast(origin, bodyRadius, desiredDir, out hit, avoidanceDistance))
        {
            // Resetear bloqueo si no hay obstáculo
            if (lastBlockedAgent != null)
            {
                lastBlockedAgent = null;
                mutualBlockTimer = 0f;
            }
            return false;
        }

        Transform t = hit.transform;
        if (t == transform || t.IsChildOf(transform)) return false;

        if (cachedHouseBounds.Contains(hit.point)) return false;
        if (t.IsChildOf(targetHouse) || t == targetHouse) return false;

        if (routeTargets != null)
            foreach (Transform rt in routeTargets)
                if (rt != null && (t == rt || t.IsChildOf(rt))) return false;

        // --- Caso especial: otro peatón ---
        RectangularPatrol otherPerson = t.GetComponentInParent<RectangularPatrol>();
        if (otherPerson != null)
        {
            Vector3 toOther = (t.position - transform.position);
            toOther.y = 0;
            if (toOther == Vector3.zero) toOther = transform.right;

            Vector3 lateral = Vector3.Cross(Vector3.up, toOther.normalized);

            // --- NUEVO: DETECCIÓN DE BLOQUEO MUTUO ---
            if (lastBlockedAgent == t.transform)
            {
                mutualBlockTimer += Time.deltaTime;
                
                if (mutualBlockTimer > MUTUAL_BLOCK_THRESHOLD)
                {
                    // El de MENOR ID cede (invertido para forzar decisión)
                    if (gameObject.GetInstanceID() < otherPerson.gameObject.GetInstanceID())
                    {
                        yieldTimer = YIELD_TIME * 2f;
                        mutualBlockTimer = 0f;
                        lastBlockedAgent = null;
                        avoidDir = Vector3.zero;
                        return false;
                    }
                    else
                    {
                        // El de mayor ID se mueve con determinación
                        avoidDir = lateral.normalized;
                        dbHit = true; dbHitPoint = hit.point; dbHitNormal = hit.normal;
                        mutualBlockTimer = 0f;
                        lastBlockedAgent = null;
                        return true;
                    }
                }
            }
            else
            {
                lastBlockedAgent = t.transform;
                mutualBlockTimer = 0f;
            }

            if (gameObject.GetInstanceID() > otherPerson.gameObject.GetInstanceID()
                && avoidanceBlend > 0.9f)
            {
                yieldTimer = YIELD_TIME;
                return false;
            }

            avoidDir = lateral.normalized;
            dbHit = true; dbHitPoint = hit.point; dbHitNormal = hit.normal;
            return true;
        }

        return false;
    }

    private bool HasLineOfSightToTarget(Transform newTarget)
    {
        Vector3 origin = transform.position + Vector3.up * 0.4f;
        Vector3 dest = new Vector3(newTarget.position.x, origin.y, newTarget.position.z);
        Vector3 dir = (dest - origin).normalized;
        float dist = Vector3.Distance(origin, dest);

        int n = Physics.RaycastNonAlloc(origin, dir, raycastBuffer, dist);
        for (int i = 0; i < n; i++)
        {
            Transform t = raycastBuffer[i].transform;
            if (t == transform || t.IsChildOf(transform)) continue;
            if (t == newTarget || t.IsChildOf(newTarget)) continue;
            
            if (t == targetHouse || t.IsChildOf(targetHouse)) return false; 
            
            if (routeTargets != null)
            {
                foreach (Transform rt in routeTargets)
                {
                    if (rt != null && (t == rt || t.IsChildOf(rt))) 
                    {
                        return false;
                    }
                }
            }
        }
        return true;
    }

    private static Dictionary<Transform, Collider[]> houseCollidersCache = new Dictionary<Transform, Collider[]>();

    private void CalculateCorners()
    {
        Collider[] cols;
        if (!houseCollidersCache.TryGetValue(targetHouse, out cols))
        {
            cols = targetHouse.GetComponentsInChildren<Collider>();
            if (cols != null && cols.Length > 0)
            {
                houseCollidersCache[targetHouse] = cols;
            }
        }

        if (cols == null || cols.Length == 0)
        {
            Debug.LogError($"[RectangularPatrol] '{targetHouse.name}' no tiene Colliders!");
            return;
        }

        Bounds bounds = cols[0].bounds;
        for (int i = 1; i < cols.Length; i++) bounds.Encapsulate(cols[i].bounds);

        cachedHouseBounds = bounds;
        cachedHouseBounds.Expand(paddingDistance);

        float pd = paddingDistance;
        float yPos = transform.position.y;

        corners[0] = new Vector3(bounds.max.x + pd, yPos, bounds.max.z + pd);
        corners[1] = new Vector3(bounds.max.x + pd, yPos, bounds.min.z - pd);
        corners[2] = new Vector3(bounds.min.x - pd, yPos, bounds.min.z - pd);
        corners[3] = new Vector3(bounds.min.x - pd, yPos, bounds.max.z + pd);

        if (!clockwise) System.Array.Reverse(corners);

        float closestDist = Mathf.Infinity;
        for (int i = 0; i < corners.Length; i++)
        {
            float d = Vector3.Distance(transform.position, corners[i]);
            if (d < closestDist) { closestDist = d; currentCornerIndex = i; }
        }

        smoothDir = Vector3.zero;
        isReady = true;
    }

    private void OnDrawGizmos()
    {
        if (!isReady || corners == null) return;

        Gizmos.color = Color.green;
        for (int i = 0; i < corners.Length; i++)
        {
            Gizmos.DrawSphere(corners[i], 0.3f);
            Gizmos.DrawLine(corners[i], corners[(i + 1) % corners.Length]);
        }

        if (!Application.isPlaying || !avoidObstacles) return;

        Gizmos.color = dbHit ? Color.red : Color.cyan;
        Gizmos.DrawWireSphere(dbOrigin, bodyRadius);
        Gizmos.DrawLine(dbOrigin, dbOrigin + dbDir * avoidanceDistance);
        Gizmos.DrawWireSphere(dbOrigin + dbDir * avoidanceDistance, bodyRadius);

        if (dbHit)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(dbHitPoint, 0.12f);
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(dbHitPoint, dbHitPoint + dbHitNormal);
        }

        Gizmos.color = Color.blue;
        Gizmos.DrawLine(transform.position + Vector3.up * sensorHeightOffset, transform.position + Vector3.up * sensorHeightOffset + smoothDir * 1.5f);
        
        // --- NUEVO: Debug de bloqueo mutuo ---
        if (mutualBlockTimer > 0f)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 2f, 0.5f);
            Vector3 labelPos = transform.position + Vector3.up * 2.5f;
#if UNITY_EDITOR
            Handles.Label(labelPos, $"Bloqueo: {mutualBlockTimer:F1}s");
#endif
        }
        
        // --- NUEVO: Debug de teletransporte ---
        if (stuckTime > 0f)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 3f, 0.3f);
            Vector3 labelPos = transform.position + Vector3.up * 3.5f;
#if UNITY_EDITOR
            Handles.Label(labelPos, $"Atasco: {stuckTime:F1}/{MAX_STUCK_TIME}s");
#endif
        }
    }
}