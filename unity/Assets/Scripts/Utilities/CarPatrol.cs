using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Patrulla simple de auto entre waypoints. Sin carriles, sin complejidad extra.
/// </summary>
public class CarPatrol : MonoBehaviour
{
    [Header("Ruta")]
    [Tooltip("Se rellena automáticamente al inicio buscando objetos con tag 'Waypoint'. No necesitas asignar nada aquí.")]
    public Transform[] waypoints;

    [Header("Movimiento")]
    public float moveSpeed = 10f;
    public float rotationSpeed = 8f;
    public float waypointReachThreshold = 1.5f;

    [Header("Comportamiento y Distancias")]
    [Tooltip("Ignorar waypoints cuyo 'forward' apunte en contra de nuestro vehículo. Evita saltar al carril contrario.")]
    public bool ignoreOpposingLanes = true;

    [Tooltip("Distancia mínima (metros) para considerar que un waypoint está 'lejos' y darle prioridad absoluta frente a los demás.")]
    public float minFarWaypointDistance = 30f;

    [Tooltip("Probabilidad (0 a 1) de elegir el camino recto/lejano cuando hay opciones. 1 = Siempre va recto, 0 = Siempre dobla.")]
    [Range(0f, 1f)]
    public float straightPathProbability = 0.75f;

    [Tooltip("Pausa el juego y dibuja líneas de colores a los waypoints evaluados (Rojo=Contra, Amarillo=Bloqueado, Verde=Libre).")]
    public bool debugWaypointSelection = false;

    [Tooltip("Si está activo, elige el próximo waypoint al azar. Si no, va en orden.")]
    public bool useRandomWaypoints = true;

    [Tooltip("Cono visual del auto (-angulo a +angulo). Solo evaluará waypoints dentro de este cono. (ej. 20 grados dictará un ancho de visión de 40 grados en total).")]
    [Range(5f, 180f)]
    public float maxTurnAngle = 100f;

    [Tooltip("Cuántos waypoints recientes evitar al elegir el próximo (evita rebotar).")]
    [Range(1, 20)]
    public int waypointMemorySize = 10;

    [Header("Evasión de Aceras (Anti-Targets)")]
    [Tooltip("Se rellena automáticamente al inicio buscando objetos con tag 'Acera'. No necesitas asignar nada aquí.")]
    public Transform[] antiTargets;
    [Tooltip("Distancia extra que el auto mantiene alejado de la acera (ej. 1 metro).")]
    public float antiTargetMargin = 1.0f;
    [Tooltip("Ajuste manual de altura (desde el pivote del auto) para el radar de aceras. Ajústalo hasta que la línea verde de visión raspe la calle.")]
    public float waypointSensorHeightOffset = 0.15f;

    [Header("Detección de Obstáculos")]
    [Tooltip("¿Detenerse si hay un peatón o auto enfrente?")]
    public bool waitForObstacles = true;

    [Tooltip("Radio del sensor frontal.")]
    public float sensorRadius = 1.2f;

    [Tooltip("Altura de las esferas de detección respecto al suelo (ajustar si la esfera choca la calle).")]
    public float sensorHeightOffset = 0.5f;

    [Tooltip("Distancia de detección frontal.")]
    public float detectionDistance = 5f;

    [Tooltip("Tiempo bloqueado antes de saltar al siguiente waypoint.")]
    public float maxWaitTime = 2f;

    // --- Estado interno ---
    private int currentIndex = -1;
    private Queue<int> recentWaypoints = new Queue<int>();
    private float stuckTimer = 0f;
    private Vector3 smoothDir = Vector3.zero;
    private float reversingTimer = 0f;
    private int rutStuckCount = 0;
    private bool emergencyTurn = false;
    private float currentMoveSpeed = 0f;
    private float currentTurnSpeed = 0f;

    // --- Atasco Físico ---
    private Vector3 lastStuckCheckPos;
    private float stuckCheckTimer = 0f;

    // --- Buffer de Física (Non-Alloc) ---
    private readonly RaycastHit[] raycastBuffer = new RaycastHit[50];
    private readonly RaycastHit[] spherecastBuffer = new RaycastHit[100];
    private readonly Collider[] overlapBuffer = new Collider[50];

    // --- Debug ---
    private bool dbAntiTargetHit;
    private Vector3 dbOrigin, dbDir;
    private float dbRadius;

    void Start()
    {
        currentMoveSpeed = moveSpeed;
        StartCoroutine(WaitForSceneAndInit());
    }

    private void OnEnable()
    {
        // Cuando el objeto se reactiva, buscar waypoints nuevamente
        if (waypoints != null && waypoints.Length > 0)
            return; // Ya inicializado
        
        StartCoroutine(InitializeWaypoints());
    }

    private IEnumerator WaitForSceneAndInit()
    {
        // Esperar a que SceneInitializer haya terminado de generar todo el contenido
        SceneInitializer sceneInit = FindFirstObjectByType<SceneInitializer>();
        if (sceneInit != null)
        {
            // Debug.Log($"[CarPatrol] '{gameObject.name}' esperando que SceneInitializer termine...");
            yield return new WaitUntil(() => sceneInit.IsInitializeComplete);
            // Debug.Log($"[CarPatrol] '{gameObject.name}' Scene lista. Iniciando auto-descubrimiento.");
        }
        else
        {
            // Sin SceneInitializer en escena — esperar un frame y continuar
            yield return null;
        }

        // Esperar 10 segundos adicionales antes de reconocer waypoints
        yield return new WaitForSeconds(10f);

        yield return StartCoroutine(InitializeWaypoints());
    }

    private IEnumerator InitializeWaypoints()
    {

        // --- Auto-descubrimiento de Waypoints por tag (incluyendo desactivados) ---
        GameObject[] wpObjects = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(go => go.CompareTag("Waypoint")).ToArray();
        if (wpObjects.Length == 0)
        {
            Debug.LogError("[CarPatrol] No se encontraron GameObjects con tag 'Waypoint' en la escena. Asegúrate de que existen y tienen ese tag.");
            yield break;
        }
        waypoints = new Transform[wpObjects.Length];
        for (int i = 0; i < wpObjects.Length; i++)
            waypoints[i] = wpObjects[i].transform;
        // Debug.Log($"[CarPatrol] '{gameObject.name}' encontró {waypoints.Length} waypoints con tag 'Waypoint'.");

        // --- Auto-descubrimiento de Anti-targets (Acera + Houses, incluyendo desactivados) ---
        GameObject[] acObjects    = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(go => go.CompareTag("Acera")).ToArray();
        GameObject[] houseObjects = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(go => go.CompareTag("Houses")).ToArray();

        if (acObjects.Length == 0 && houseObjects.Length == 0)
        {
            Debug.LogWarning("[CarPatrol] No se encontraron GameObjects con tag 'Acera' ni 'Houses'. El auto no evitará ningún borde.");
            antiTargets = new Transform[0];
        }
        else
        {
            antiTargets = new Transform[acObjects.Length + houseObjects.Length];
            for (int i = 0; i < acObjects.Length; i++)
                antiTargets[i] = acObjects[i].transform;
            for (int i = 0; i < houseObjects.Length; i++)
                antiTargets[acObjects.Length + i] = houseObjects[i].transform;
            // Debug.Log($"[CarPatrol] '{gameObject.name}' encontró {antiTargets.Length} anti-targets ({acObjects.Length} 'Acera' + {houseObjects.Length} 'Houses').");
        }

        SelectNextWaypoint();
        yield break;
    }

    void Update()
    {
        if (waypoints == null || waypoints.Length == 0) return;
        if (currentIndex < 0 || currentIndex >= waypoints.Length) return;

        Vector3 targetPos = waypoints[currentIndex].position;
        targetPos.y = transform.position.y;

        Vector3 desiredDir = (targetPos - transform.position).normalized;
        if (desiredDir == Vector3.zero) desiredDir = transform.forward;

        // Si por culpa de una evasión agresiva el waypoint quedó casi en nuestras espaldas,
        // lo abandonamos y buscamos uno nuevo enfrente para no dar vueltas en U (vueltas locas).
        // (Ignoramos esta regla si el auto entró en modo de vuelta en U de emergencia).
        if (!emergencyTurn && Vector3.Angle(transform.forward, desiredDir) > maxTurnAngle + 20f)
        {
            SelectNextWaypoint();
            return;
        }

        // Si está atascado dando la vuelta, retrocedemos un poco haciendo espacio
        if (reversingTimer > 0f)
        {
            reversingTimer -= Time.deltaTime;
            transform.position += -transform.forward * (moveSpeed * 0.4f) * Time.deltaTime;
            currentMoveSpeed = 0f; // Reiniciar velocidad para acelerar suave al salir
            return; // Al terminar de retroceder, el radar vuelve a operar instantáneamente
        }

        if (smoothDir == Vector3.zero) smoothDir = desiredDir;

        // 1. Analizar proximidad a aceras (Anti-targets)
        float distToAntiTarget = float.MaxValue;
        Vector3 wallNormal = Vector3.zero;
        Vector3 targetSteerDir = desiredDir; // Por defecto quiere ir a su destino original

        distToAntiTarget = GetDistanceToAntiTarget(smoothDir, out wallNormal);

        Collider myCol = GetComponent<Collider>();
        if (myCol == null) myCol = GetComponentInChildren<Collider>();
        float carNoseDist = (myCol != null) ? myCol.bounds.extents.z : 2.0f;
            
        // Distancia de evasión preventiva (parachoques + 1.2m)
        float evasionThreshold = carNoseDist + 1.2f;
            // Distancia de impacto frontal (parachoques tocando muro)
            float crashThreshold = carNoseDist + 0.15f;

            // Wall-Hugging / Reflexión de evasión suave antes de chocar
            if (distToAntiTarget < evasionThreshold && wallNormal != Vector3.zero)
            {
                Vector3 reflectDir = Vector3.Reflect(smoothDir, wallNormal);
                reflectDir.y = 0;
                
                // Mientras más se presiona contra el muro, más violento/obligatorio es el volante lateral (de 0 a 1)
                float avoidanceWeight = Mathf.InverseLerp(evasionThreshold, crashThreshold, distToAntiTarget);
                targetSteerDir = Vector3.Lerp(desiredDir, reflectDir.normalized, avoidanceWeight).normalized;
            }

            if (distToAntiTarget <= crashThreshold) // Ya estrelló las llantas
            {
                rutStuckCount++;
                if (rutStuckCount >= 2) // Si tras 2 maniobras falla, cambia ruta
                {
                    SelectNextWaypoint();
                    rutStuckCount = 0;
                }
                
                reversingTimer = 1.0f; // Dar un segundo entero y completo de retroceso puro 
                return;
            }

        // 2. Parada de emergencia si hay autos o peatones cruzando en nuestra trayectoria
        bool blocked = waitForObstacles && IsObstacleAhead(smoothDir);

        if (!blocked)
        {
            stuckTimer = 0f;

            float targetSpeed = moveSpeed;

            // Freno gradual al acercarse a una acera (toma de curvas o bordes)
            if (distToAntiTarget < detectionDistance)
            {
                float antiTargetSlowFactor = Mathf.Clamp01(distToAntiTarget / detectionDistance);
                targetSpeed = Mathf.Lerp(moveSpeed * 0.2f, targetSpeed, antiTargetSlowFactor);
            }

            float angleToTarget = Vector3.Angle(transform.forward, targetSteerDir);

            // Prioridad a la rotación: Si está muy desalineado, frena para girar (simulación realista de curvas)
            if (angleToTarget > 20f)
            {
                targetSpeed = moveSpeed * 0.15f; // Reduce muchísimo la velocidad en esquinas cerradas
            }
            else if (angleToTarget > 5f)
            {
                targetSpeed = Mathf.Min(targetSpeed, moveSpeed * 0.5f); // Mitad de velocidad en curvas suaves
            }

            // Inercia de Aceleración y Frenado
            // Si targetSpeed es mayor (acelerar), lo hace gradualmente simulando peso.
            // Si es menor (frenar), lo hace más rápido simulando pisar el freno.
            float accelRate = (targetSpeed > currentMoveSpeed) ? (moveSpeed * 0.35f) : (moveSpeed * 1.5f);
            currentMoveSpeed = Mathf.MoveTowards(currentMoveSpeed, targetSpeed, accelRate * Time.deltaTime);

            transform.position = Vector3.MoveTowards(transform.position, transform.position + smoothDir, currentMoveSpeed * Time.deltaTime);

            if (Vector3.Distance(transform.position, targetPos) < waypointReachThreshold)
            {
                SelectNextWaypoint();
            }
        }
        else
        {
            stuckTimer += Time.deltaTime;
            
            // Freno de disco duro si detecta peatón/obstáculo
            currentMoveSpeed = Mathf.MoveTowards(currentMoveSpeed, 0f, (moveSpeed * 3f) * Time.deltaTime);

            if (stuckTimer >= maxWaitTime)
            {
                stuckTimer = 0f;
                SelectNextWaypoint();
            }
        }

        // --- Inercia del Volante ---
        float targetTurnRate = rotationSpeed * 15f;
        float angleDiff = Vector3.Angle(smoothDir, targetSteerDir);
        
        // El conductor gira el volante poco a poco (empieza lento, luego acelera el giro de las llantas)
        if (angleDiff > 2f) 
        {
            currentTurnSpeed = Mathf.MoveTowards(currentTurnSpeed, targetTurnRate, (targetTurnRate * 0.6f) * Time.deltaTime);
        }
        else 
        {
            currentTurnSpeed = Mathf.MoveTowards(currentTurnSpeed, rotationSpeed * 2f, (targetTurnRate * 1.5f) * Time.deltaTime);
        }

        smoothDir = Vector3.RotateTowards(smoothDir, targetSteerDir, currentTurnSpeed * Mathf.Deg2Rad * Time.deltaTime, 10f);

        // Aplicar rotación suave del chasis a donde apunta la dirección de las llantas (smoothDir)
        if (smoothDir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(smoothDir);
            // Reducimos el factor Slerp (0.8f) para que el peso del chasis reaccione más lento y parezca pesado
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSpeed * 0.8f);
        }

        // --- Detección Universal de Atasco Físico (Vibración o encallamiento) ---
        // Ignoramos el chequeo si estamos frenados intencionalmente por un obstáculo o retrocediendo
        if (!blocked && reversingTimer <= 0f)
        {
            stuckCheckTimer += Time.deltaTime;
            if (stuckCheckTimer >= 2.0f)
            {
                // Si en 2 segundos nos hemos movido menos de 0.5 metros, estamos vibrando contra una esquina
                if (Vector3.Distance(transform.position, lastStuckCheckPos) < 0.5f)
                {
                    SelectNextWaypoint();
                    reversingTimer = 1.0f; // Dar marcha atrás fuerte para desengancharse
                }
                
                lastStuckCheckPos = transform.position;
                stuckCheckTimer = 0f;
            }
        }
        else
        {
            // Si nos bloquea un peatón, reiniciamos el comprobador físico
            stuckCheckTimer = 0f;
            lastStuckCheckPos = transform.position;
        }
    }

    private float GetDistanceToAntiTarget(Vector3 testDir, out Vector3 wallNormal)
    {
        wallNormal = Vector3.zero;
        if (antiTargets == null || antiTargets.Length == 0) return float.MaxValue;

        // Radar de escudo y evasión ensanchado para evitar puntos ciegos
        float radius = 0.4f;
        Vector3 origin = new Vector3(transform.position.x, GetRaycastBaseY() + waypointSensorHeightOffset, transform.position.z);

        dbOrigin = origin;
        dbDir = testDir;
        dbRadius = radius;
        dbAntiTargetHit = false;

        // 1. Chequeo de solapamiento inicial
        int overlapCount = Physics.OverlapSphereNonAlloc(origin, radius, overlapBuffer);
        for (int i = 0; i < overlapCount; i++)
        {
            Transform t = overlapBuffer[i].transform;
            foreach (Transform at in antiTargets)
            {
                if (at != null && (t == at || t.IsChildOf(at)))
                {
                    dbAntiTargetHit = true;
                    // Deducción aproximada de la normal para Overlap interior
                    wallNormal = (origin - overlapBuffer[i].ClosestPoint(origin)).normalized;
                    if (wallNormal == Vector3.zero) wallNormal = -testDir; // Fallback
                    return 0f;
                }
            }
        }

        // 2. Chequeo hacia adelante
        RaycastHit hit;
        if (Physics.SphereCast(origin, radius, testDir, out hit, detectionDistance))
        {
            Transform t = hit.transform;
            foreach (Transform at in antiTargets)
            {
                if (at != null && (t == at || t.IsChildOf(at)))
                {
                    dbAntiTargetHit = true;
                    wallNormal = hit.normal;
                    return hit.distance;
                }
            }
        }
        return float.MaxValue;
    }

    private void SelectNextWaypoint()
    {
        if (waypoints == null || waypoints.Length == 0) return;

        int next = -1;
        string debugLogMsg = "";

        if (useRandomWaypoints)
        {
            List<int> perfectFarOptions = new List<int>(); // Waypoints lejanos (Ideales Verdes)
            List<int> perfectCloseOptions = new List<int>(); // Waypoints cercanos (Cercanos Cyan, salvavidas temporal)
            List<int> fallbackOptions = new List<int>(); // Naranjas: Seguros físicamente, pero rompen el ángulo o carril

            Vector3 origin = new Vector3(transform.position.x, GetRaycastBaseY() + waypointSensorHeightOffset, transform.position.z);

            for (int i = 0; i < waypoints.Length; i++)
            {
                if (i == currentIndex || waypoints[i] == null) continue;
                if (recentWaypoints.Contains(i)) continue;

                // REGLA 1 (INQUEBRANTABLE): NO CHOCAR CON ACERAS O EDIFICIOS (ANTITARGETS)
                if (!IsPathClearToWaypoint(waypoints[i].position))
                {
                    if (debugWaypointSelection) Debug.DrawLine(origin, waypoints[i].position, Color.yellow, 10f);
                    continue; // Muerte súbita, no entra ni a Perfectos ni a Fallback.
                }

                // --- Si llegó aquí, el camino es FISICAMENTE SEGURO. Evaluamos los otros 4 filtros ---
                bool isPerfect = true;

                // 2. Ángulo Máximo de Visión (Cono único dictado por maxTurnAngle: [-Max, +Max])
                // Esto controla perfectamente cuánto hacia atrás o a los lados puede mirar el coche sin usar viejos cortes de eje Z.
                Vector3 flatWpPos = new Vector3(waypoints[i].position.x, origin.y, waypoints[i].position.z);
                Vector3 flatForward = new Vector3(transform.forward.x, 0, transform.forward.z).normalized;
                Vector3 dirToWp = (flatWpPos - origin).normalized;
                float angleToWp = Vector3.Angle(flatForward, dirToWp);

                if (angleToWp > maxTurnAngle) isPerfect = false;

                // 4. Carril Contrario (Sentido OPUESTO)
                if (ignoreOpposingLanes)
                {
                    float dotDir = Vector3.Dot(transform.forward, waypoints[i].forward);
                    if (dotDir < -0.6f) isPerfect = false; // Oculto estructuralmente a la vista
                }

                // Clasificación Minuciosa del Waypoint
                float distToWp = Vector3.Distance(transform.position, waypoints[i].position);

                if (isPerfect)
                {
                    if (distToWp >= minFarWaypointDistance)
                    {
                        if (debugWaypointSelection) Debug.DrawLine(origin, flatWpPos, Color.green, 10f);
                        perfectFarOptions.Add(i);
                    }
                    else
                    {
                        if (debugWaypointSelection) Debug.DrawLine(origin, flatWpPos, Color.cyan, 10f); // Demasiado cerca, plan B
                        perfectCloseOptions.Add(i);
                    }
                }
                else
                {
                    if (debugWaypointSelection) Debug.DrawLine(origin, flatWpPos, new Color(1f, 0.5f, 0f), 10f); // Naranja para fallback
                    fallbackOptions.Add(i);
                }
            }

            // MÁQUINA DE DECISIÓN
            if (perfectFarOptions.Count > 0 || perfectCloseOptions.Count > 0)
            {
                emergencyTurn = false;
                
                // 1. Agrupar físicamente por Ángulo (Rectos <= 30° vs Curvas > 30°)
                List<int> straightFar = new List<int>();
                List<int> turnFar = new List<int>();
                List<int> straightClose = new List<int>();
                List<int> turnClose = new List<int>();

                Vector3 flatForward = new Vector3(transform.forward.x, 0, transform.forward.z).normalized;
                Vector3 originFlat = new Vector3(transform.position.x, transform.position.y, transform.position.z);

                foreach (int w in perfectFarOptions)
                {
                    Vector3 dirWp = (new Vector3(waypoints[w].position.x, originFlat.y, waypoints[w].position.z) - originFlat).normalized;
                    if (Vector3.Angle(flatForward, dirWp) <= 30f) straightFar.Add(w);
                    else turnFar.Add(w);
                }
                foreach (int w in perfectCloseOptions)
                {
                    Vector3 dirWp = (new Vector3(waypoints[w].position.x, originFlat.y, waypoints[w].position.z) - originFlat).normalized;
                    if (Vector3.Angle(flatForward, dirWp) <= 30f) straightClose.Add(w);
                    else turnClose.Add(w);
                }

                // 2. Aplicar Probabilidad del Inspector
                bool hasStraightOptions = (straightFar.Count + straightClose.Count) > 0;
                bool hasTurnOptions = (turnFar.Count + turnClose.Count) > 0;
                bool wantsStraight = hasStraightOptions; // por defecto

                if (hasStraightOptions && hasTurnOptions)
                {
                    wantsStraight = Random.value <= straightPathProbability;
                }
                else if (hasTurnOptions)
                {
                    wantsStraight = false;
                }

                // 3. Respetar la prioridad de Lejanía (Far > Close) sobre el grupo ganador
                List<int> targetFar = wantsStraight ? straightFar : turnFar;
                List<int> targetClose = wantsStraight ? straightClose : turnClose;

                List<int> finalWorkingOptions = targetFar.Count > 0 ? targetFar : targetClose;
                bool isFarGroup = targetFar.Count > 0;

                // 4. Ordenar por distancia (Ascendente si es Lejano, Descendente si es Cercano)
                finalWorkingOptions.Sort((a, b) => 
                {
                    float distA = Vector3.Distance(transform.position, waypoints[a].position);
                    float distB = Vector3.Distance(transform.position, waypoints[b].position);
                    return isFarGroup ? distA.CompareTo(distB) : distB.CompareTo(distA); 
                });

                // 5. Extraer la Banda de Campeones
                float bestDist = Vector3.Distance(transform.position, waypoints[finalWorkingOptions[0]].position);
                List<int> distanceChampions = new List<int>();
                
                foreach (int w in finalWorkingOptions)
                {
                    float dist = Vector3.Distance(transform.position, waypoints[w].position);
                    if (Mathf.Abs(dist - bestDist) <= 5.0f) 
                    {
                        distanceChampions.Add(w);
                    }
                }

                next = distanceChampions[Random.Range(0, distanceChampions.Count)];
                
                Vector3 fwWpPos = new Vector3(waypoints[next].position.x, transform.position.y, waypoints[next].position.z);
                float finalAng = Vector3.Angle(flatForward, (fwWpPos - transform.position).normalized);
                float finalDist = Vector3.Distance(transform.position, waypoints[next].position);
                
                string dirTag = wantsStraight ? "RECTO" : "CURVA";
                string tag = isFarGroup ? "LEJANO (Campeón Cercano)" : "CERCANO (Estirado Lejano)";
                debugLogMsg = $"[{dirTag} | {tag}] Elegido WP '{waypoints[next].name}'. Dist. {finalDist:F1}m, Ang. {finalAng:F1}°.";
            }
            else if (fallbackOptions.Count > 0)
            {
                emergencyTurn = true; 
                
                // Fallback: Eliminado el favoristimo de Ángulo. Solo importa cumplir TU regla de distancias salvavidas.
                fallbackOptions.Sort((a, b) => 
                {
                    float distA = Vector3.Distance(transform.position, waypoints[a].position);
                    float distB = Vector3.Distance(transform.position, waypoints[b].position);
                    
                    bool aIsFar = distA >= minFarWaypointDistance;
                    bool bIsFar = distB >= minFarWaypointDistance;

                    if (aIsFar && bIsFar)
                        return distA.CompareTo(distB); // Ambos lejanos: Queremos al MÁS CERCANO de los lejanos (Ascendente)
                    else if (!aIsFar && !bIsFar)
                        return distB.CompareTo(distA); // Ambos cercanos: Queremos al MÁS LEJANO de los cercanos (Descendente)
                    else if (aIsFar && !bIsFar)
                        return -1; // 'a' cumple la regla de lejanía, 'b' es cobarde cercano. Gana 'a'.
                    else
                        return 1;  // 'b' cumple la regla, 'a' no. Gana 'b'.
                });
                
                next = fallbackOptions[0];
                float finalDist = Vector3.Distance(transform.position, waypoints[next].position);
                debugLogMsg = $"[EMERGENCIA FLEXIBLE] Elegido WP '{waypoints[next].name}' por ser el salvavidas mejor balanceado ({finalDist:F1}m).";
            }
            else
            {
                emergencyTurn = true;
                // Fallback Total (Estamos totalmente encerrados físicamente por todas partes)
                next = (currentIndex + 1) % waypoints.Length;
                debugLogMsg = $"[DESESPERACIÓN CIEGA] Atrapado en AntiTargets 360°. Forzando índice sumado WP '{waypoints[next].name}'.";
            }

            // Siempre imprimimos el Log en consola para ayudarte a diagnosticar, sin importar la palanca
            // Debug.Log($"[CarPatrol] RESULTADO: {debugLogMsg}");
            
            if (debugWaypointSelection)
            {
                Debug.Break(); // Pausa el editor de Unity inmediatamente SOLO si tienes activado 'debugWaypointSelection' en el CarPatrol
            }
        }
        else
        {
            next = (currentIndex + 1) % waypoints.Length;
            // Debug.Log($"[CarPatrol] ADVERTENCIA: Tienes 'Use Random Waypoints' desactivado en el Inspector. El auto está ignorando toda la inteligencia artificial y yendo a lo ciego al Array[{next}].");
        }

        if (next < 0) next = 0;

        currentIndex = next;
        recentWaypoints.Enqueue(next);
        if (recentWaypoints.Count > waypointMemorySize)
            recentWaypoints.Dequeue();
    }

    private bool IsPathClearToWaypoint(Vector3 wpPosition)
    {
        if (antiTargets == null || antiTargets.Length == 0) return true;

        // Origin fijado directamente a la base del collider más el offset manual del usuario
        Vector3 lowOrigin = new Vector3(transform.position.x, GetRaycastBaseY() + waypointSensorHeightOffset, transform.position.z); 
        
        // Radar esférico voluminoso para emular las proporciones del auto y no volar por encima de aceras chatas
        float sweepRadius = 0.4f; 
        
        // 1. Check Ciego Incial: Si la esfera empieza adentro de un objeto (como orillándose), SphereCast NO lo detectará más adelante.
        int overlapCount = Physics.OverlapSphereNonAlloc(lowOrigin, sweepRadius, overlapBuffer);
        for (int i = 0; i < overlapCount; i++)
        {
            Transform t = overlapBuffer[i].transform;
            foreach (Transform at in antiTargets)
            {
                if (at != null && (t == at || t.IsChildOf(at)))
                {
                    return false; // El auto ya está rosando físicamente este antitarget al inicio
                }
            }
        }

        // Aplanar el vector destino para que la línea no apunte al cielo si las esferas están flotando
        Vector3 flatWpPos = new Vector3(wpPosition.x, lowOrigin.y, wpPosition.z);
        Vector3 direction = (flatWpPos - lowOrigin).normalized;
        float dist = Vector3.Distance(lowOrigin, flatWpPos);

        // 2. Barrido a futuro sin límite de memoria (SphereCastAll atrapa todos los impactos)
        RaycastHit[] hits = Physics.SphereCastAll(lowOrigin, sweepRadius, direction, dist);
        
        foreach (RaycastHit hit in hits)
        {
            Transform t = hit.transform;
            foreach (Transform at in antiTargets)
            {
                if (at != null && (t == at || t.IsChildOf(at)))
                {
                    return false; // El camino atraviesa un anti-target
                }
            }
        }

        // 3. Trazado directo (RaycastAll) para garantizar que si la acera es un plano delgado sin volumen, la intercepte como un filo
        RaycastHit[] lineHits = Physics.RaycastAll(lowOrigin, direction, dist);
        foreach (RaycastHit lineHit in lineHits)
        {
            Transform t = lineHit.transform;
            foreach (Transform at in antiTargets)
            {
                if (at != null && (t == at || t.IsChildOf(at)))
                {
                    return false; // Bloqueo directo por cara plana de antitarget
                }
            }
        }
        
        return true;
    }

    private bool IsObstacleAhead(Vector3 direction)
    {
        // Asegurarnos de usar el offset especificado visualmente por el usuario para obstáculos
        Vector3 origin = new Vector3(transform.position.x, GetRaycastBaseY() + sensorHeightOffset, transform.position.z);

        // 1. Solapamiento inicial
        int overlapCount = Physics.OverlapSphereNonAlloc(origin, sensorRadius, overlapBuffer);
        for (int i = 0; i < overlapCount; i++)
        {
            if (overlapBuffer[i].gameObject == gameObject) continue;
            Transform t = overlapBuffer[i].transform;
            if (t.GetComponentInParent<CarPatrol>() != null) return true;
            if (t.GetComponentInParent<RectangularPatrol>() != null) return true;
        }

        // 2. SphereCast hacia adelante
        int hitCount = Physics.SphereCastNonAlloc(
            origin,
            sensorRadius,
            direction,
            spherecastBuffer,
            detectionDistance
        );

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = spherecastBuffer[i];
            if (hit.collider.gameObject == gameObject) continue;

            Transform t = hit.transform;
            if (t.GetComponentInParent<CarPatrol>() != null) return true;
            if (t.GetComponentInParent<RectangularPatrol>() != null) return true;
        }

        return false;
    }

    private float GetRaycastBaseY()
    {
        Collider myCol = GetComponent<Collider>();
        if (myCol == null) myCol = GetComponentInChildren<Collider>();
        
        // Base justo por debajo del nivel del box collider
        if (myCol != null)
        {
            return myCol.bounds.min.y - 0.05f;
        }
        return transform.position.y - 0.5f; // Fallback si no hay collider
    }

    private void OnDrawGizmos()
    {
        if (waypoints == null) return;

        Gizmos.color = new Color(0f, 1f, 1f, 0.5f);
        foreach (Transform wp in waypoints)
        {
            if (wp != null) Gizmos.DrawSphere(wp.position, 0.4f);
        }

        // Waypoint actual en verde
        if (Application.isPlaying && currentIndex >= 0 && currentIndex < waypoints.Length && waypoints[currentIndex] != null)
        {
            Vector3 floorOrigin = new Vector3(transform.position.x, GetRaycastBaseY() + waypointSensorHeightOffset, transform.position.z);
            Vector3 floorTarget = new Vector3(waypoints[currentIndex].position.x, GetRaycastBaseY() + waypointSensorHeightOffset, waypoints[currentIndex].position.z);

            Gizmos.color = Color.green;
            Gizmos.DrawSphere(floorTarget, 0.6f);
            Gizmos.DrawLine(floorOrigin, floorTarget);
        }

        if (Application.isPlaying)
        {
            // --- Radar Periférico (Aceras / AntiTargets) ---
            if (antiTargets != null && antiTargets.Length > 0)
            {
                Gizmos.color = dbAntiTargetHit ? new Color(1f, 0.5f, 0f, 0.6f) : new Color(0f, 1f, 1f, 0.3f); // Naranja (peligro), Cyan (libre)
                Gizmos.DrawWireSphere(dbOrigin, dbRadius);
                Gizmos.DrawWireSphere(dbOrigin + dbDir * detectionDistance, dbRadius);
                Gizmos.DrawLine(dbOrigin, dbOrigin + dbDir * detectionDistance);
            }

            // --- Radar Frontal de Bloqueo (Peatones / Tráfico) ---
            if (waitForObstacles)
            {
                Gizmos.color = new Color(1f, 0f, 0f, 0.4f); // Rojo
                
                Vector3 frontOrigin = new Vector3(transform.position.x, GetRaycastBaseY() + sensorHeightOffset, transform.position.z);
                Vector3 lookDir = smoothDir != Vector3.zero ? smoothDir : transform.forward;
                Gizmos.DrawWireSphere(frontOrigin + lookDir * detectionDistance, sensorRadius);
                Gizmos.DrawLine(frontOrigin, frontOrigin + lookDir * detectionDistance);
            }
        }
    }
}
