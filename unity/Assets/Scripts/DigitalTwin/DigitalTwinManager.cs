using UnityEngine;
using System.Collections.Generic;
using System.IO;

namespace DigitalTwin
{
    public class DigitalTwinManager : MonoBehaviour
    {
        public static DigitalTwinManager Instance { get; private set; }

        [Header("1. Estado Global del DT (D_t)")]
        [Tooltip("Viaje o entrenamiento actual")]
        public int currentEpisode = 0;
        
        [Tooltip("El episodio de entrenamiento est en curso?")]
        public bool isEpisodeActive = false;

        [Tooltip("Paso de tiempo discreto (t)")]
        public int timeStep = 0; 

        [Header("2. Segmentos de Carretera (S)")]
        public List<RoadSegment> roadNetwork = new List<RoadSegment>();
        public RoadSegment currentTarget; // s*

        [Header("3. Estado del UAV (x_t)")]
        public UAVState currentUAVState;

        [Header("4. Agentes Dinmicos (A_t)")]
        public List<TrafficAgent> activeAgents = new List<TrafficAgent>();

        [Header("5. Visibilidad y Memoria (M_t)")]
        [Tooltip("Umbral de activacin (\tau_o)")]
        public float visibilityThreshold = 0.7f; 
        public Dictionary<RoadSegment, SegmentStatus> inspectionMemory = new Dictionary<RoadSegment, SegmentStatus>();
        public List<RoadSegment> revisitQueue = new List<RoadSegment>();

        [Header("Modo Prueba")]
        [Tooltip("Si est activo, no se envan capturas al servidor Python y las detecciones se simulan con raycast.")]
        public bool testModeNoPython = true;
        [Tooltip("Ventana de baches simulados en modo prueba. 4 significa evaluar grupos de 4.")]
        public int testModeConfirmEvery = 4;
        [Tooltip("Cuantos baches se confirman dentro de cada ventana. 3 de 4 deja 1 fallido para probar Skip.")]
        public int testModeConfirmCount = 3;

        [Header("Referencias a Sistemas Reales")]
        [Tooltip("El controlador de movimiento fsico real")]
        private DroneNavMeshController droneController;
        private TrafficDensityController densityController;
        private float currentVisibilityScore = 1.0f; 
        [Tooltip("La interfaz encargada de los raycasts de visibilidad")]
        public MovementInterface movementInterface;

        // --- Clculo de Velocidad universal ---
        private Dictionary<int, Vector3> previousAgentPositions = new Dictionary<int, Vector3>();

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void FixedUpdate()
        {
            if (!isEpisodeActive) return; // Solo avanza si hay un viaje en curso

            // 1. Avanzar tiempo discreto t (como exige el paper)
            timeStep++;

            // 2. Traffic/Env (A_t)
            UpdateAgentsState();

            // 3. UAV State (x_t) - Mapea variables reales a la formalizacin
            UpdateUAVState();

            // 4 y 5. Visibilidad (o_{s,t}) y Memoria (M_t)
            UpdateVisibilityState();

            // 6. Action (\Pi) - Leer la poltica de DroneController.cs
            SyncRecoveryPolicy();

        }


        private void UpdateUAVState()
        {
            droneController = FindFirstObjectByType<DroneNavMeshController>();
            densityController = FindFirstObjectByType<TrafficDensityController>();
            if (droneController == null) return;

            Vector3 pos = droneController.transform.position;
            currentUAVState.p = new Vector2(pos.x, pos.z);
            
            float groundHeight = Terrain.activeTerrain != null ? Terrain.activeTerrain.SampleHeight(pos) : 0f;
            currentUAVState.h = pos.y - groundHeight;

            // La velocidad fsica en un dron controlado por NavMesh no est en el Rigidbody, est en el NavMeshAgent
            UnityEngine.AI.NavMeshAgent nav = droneController.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (nav != null)
                currentUAVState.v = nav.velocity.magnitude;
            
            // Usamos GetComponent por si el droneController.energyController no se asign en el Inspector
            EnergyController eController = droneController.energyController != null ? droneController.energyController : droneController.GetComponent<EnergyController>();
            if (eController != null)
                currentUAVState.e = eController.energia / 100f;
        }

        private void UpdateAgentsState()
        {
            activeAgents.Clear();

            GameObject[] cars = GameObject.FindGameObjectsWithTag("Car");
            GameObject[] persons = GameObject.FindGameObjectsWithTag("Person");

            List<GameObject> allWorldAgents = new List<GameObject>();
            allWorldAgents.AddRange(cars);
            allWorldAgents.AddRange(persons);

            // Lista temporal para limpiar el diccionario de memoria muerta
            List<int> currentFrameIds = new List<int>();

            foreach (GameObject obj in allWorldAgents)
            {
                TrafficAgent agent = new TrafficAgent();
                agent.position = obj.transform.position;
                
                int id = obj.GetInstanceID();
                currentFrameIds.Add(id);

                // Clculo universal de velocidad (delta posicin / delta tiempo)
                if (previousAgentPositions.TryGetValue(id, out Vector3 prevPos))
                {
                    // Solo calculamos velocidad horizontal en plano (X, Z) que es lo relevante
                    Vector3 delta = new Vector3(agent.position.x - prevPos.x, 0f, agent.position.z - prevPos.z);
                    agent.velocity = delta / Time.fixedDeltaTime;
                }
                else
                {
                    agent.velocity = Vector3.zero;
                }
                
                previousAgentPositions[id] = agent.position;
                agent.type = obj.CompareTag("Car") ? AgentType.Vehicle : AgentType.Pedestrian;
                
                activeAgents.Add(agent);
            }

            // Limpiar agentes que fueron destruidos (garbage collection del diccionario)
            var keys = new List<int>(previousAgentPositions.Keys);
            foreach (int k in keys)
            {
                if (!currentFrameIds.Contains(k))
                    previousAgentPositions.Remove(k);
            }
        }

        private void UpdateVisibilityState()
        {
            if (droneController == null) return;
            
            // Frmula o_{s,t} del paper: 1.0 = totalmente visible, 0.0 = totalmente ocluido
            // Usamos 5 raycasts hacia abajo (centro + 4 bordes)
            int totalRays = 5;
            int hitsCount = 0;
            float spread = 1.0f; // Dispersin de los rayos en metros

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
                Vector3 origin = droneController.transform.position + offset;
                // Si NO choca con Person o Car, el rayo llega al suelo y est visible
                if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 10f))
                {
                    if (!hit.collider.CompareTag("Person") && !hit.collider.CompareTag("Car"))
                    {
                        hitsCount++;
                    }
                }
                else
                {
                    // Si no choca con nada asume visible
                    hitsCount++;
                }
            }

            currentVisibilityScore = (float)hitsCount / totalRays;
            currentVisibilityScore = Mathf.Clamp01(currentVisibilityScore);
        }

        private void SyncRecoveryPolicy()
        {
            if (droneController == null) return;

            RecoveryPolicy currentPolicyLocal = RecoveryPolicy.None;
            
            if (droneController.navigationMode == NavigationMode.Hover) 
                currentPolicyLocal = RecoveryPolicy.Hover;
            else if (droneController.navigationMode == NavigationMode.Micro) 
                currentPolicyLocal = RecoveryPolicy.Micro;
            else if (droneController.navigationMode == NavigationMode.Skip) 
                currentPolicyLocal = RecoveryPolicy.Skip;

            this.currentPolicy = currentPolicyLocal;
        }

        // Se declara la variable para guardar en memoria
        public RecoveryPolicy currentPolicy = RecoveryPolicy.None;

        /// <summary>
        /// Cuando es true, DroneController NO llama EndEpisode() automticamente
        /// al volver a base. El ExperimentAutomator lo llama manualmente al terminar
        /// todos los segmentos de la calle.
        /// </summary>
        public bool suppressAutoEndEpisode = false;

        public void StartNewEpisode()
        {
            currentEpisode++;
            timeStep = 0; 
            isEpisodeActive = true;
            
            inspectionMemory.Clear();
            revisitQueue.Clear();
            previousAgentPositions.Clear();
            
            // Resetear contadores de mtricas
            if (movementInterface != null)
            {
                movementInterface.ResetDetectedPotholes();  // Resetea TODO (ground truth + detected)
            }

            Debug.Log($"[Digital Twin] Iniciando Viaje / Episodio de Entrenamiento #{currentEpisode}");
        }

        public void EndEpisode()
        {
            isEpisodeActive = false;
            
            // AUTO-DESACTIVAR ACDC al terminar episodio
            if (movementInterface != null && movementInterface.isCapturing)
            {
                movementInterface.AcDc();
                Debug.Log("[Digital Twin]  ACDC DESACTIVADO - finalizando captura");
            }
            
            Debug.Log($"[Digital Twin] Viaje/Episodio finalizado.");
        }
    }
}

