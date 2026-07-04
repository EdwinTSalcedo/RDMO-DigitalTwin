using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace DigitalTwin
{
    /// <summary>
    /// Automatizador del experimento batch.
    ///
    /// RECORRE: Traffic(3) × Altitude(3) × Strategy(4) = 36 episodios
    /// Cada episodio ejecuta EXACTAMENTE 20 segmentos.
    /// Al iniciar cada episodio: resetea TerrainPotholeGenerator + PrefabObjectGenerators.
    /// </summary>
    public class ExperimentAutomator : MonoBehaviour
    {
        [Header("Controllers a Ciclar")]
        public TrafficDensityController densityController;
        public DroneHeightController heightController;
        public DroneNavMeshController droneController;

        [Header("Generadores a resetear por episodio")]
        public TerrainPotholeGenerator potholeGenerator;
        public GeneradorDeCalle calleGenerator;

        [Header("Configuración")]
        [Tooltip("Tiempo máximo (segundos) por segmento antes de forzar el siguiente.")]
        public float maxTimeoutSeconds = 300f;
        [Tooltip("Número de segmentos por episodio")]
        public int segmentsPerEpisode = 20;

        [Header("Estado Inicial (para reanudar o cambiar punto de partida)")]
        [Tooltip("Episodio desde donde empezar (0 = desde el principio)")]
        public int startFromEpisode = 0;
        [Tooltip("Nivel de tráfico inicial: 0=Low, 1=Medium, 2=High")]
        [Range(0, 2)]
        public int initialTrafficLevel = 0;
        [Tooltip("Nivel de altura inicial: 0=Low, 1=Medium, 2=High")]
        [Range(0, 2)]
        public int initialAltitudeLevel = 0;
        [Tooltip("Estrategia de navegación inicial: 1=Baseline, 2=Hover, 3=Micro, 4=Skip")]
        [Range(1, 4)]
        public int initialNavigationMode = 1;

        [Header("Botón de Inicio (opcional)")]
        public Button startButton;

        [Header("Modo Grabación (solo 9 episodios seleccionados)")]
        [Tooltip("Si está activo, solo ejecuta los 9 episodios seleccionados para grabación en lugar de los 36 completos.")]
        public bool recordingMode = false;

        [Header("Grabación de Video por Episodio")]
        [Tooltip("Componente para grabar video de cada episodio (opcional)")]
        public VideoRecorder videoRecorder;

        // ── Estado interno ─────────────────────────────────────────────────────
        private List<string> globalTableData = new List<string>();
        private List<string> paperTableData = new List<string>();
        private string progressCsvPath;
        private string segmentDetailPath;
        private MostrarSoloAlPasarMouse[] allZones;
        private Dictionary<Transform, List<MostrarSoloAlPasarMouse>> callesMap;
        private int episodeCounter = 0;
        private int seedCounter = 0;

        private const string PREFS_EPISODE = "Automator_EpisodeCounter";
        private const string PREFS_EPISODE_DATA = "Automator_EpisodeData";

        [ContextMenu("🔄 Reset Experiment (Start from Episode 1)")]
        public void ResetExperiment()
        {
            episodeCounter = 0;
            seedCounter = 0;
            PlayerPrefs.DeleteKey(PREFS_EPISODE);
            PlayerPrefs.DeleteKey(PREFS_EPISODE_DATA);
            PlayerPrefs.Save();
            Debug.Log("[Automator] 🔄 Experimento reseteado. Episodio 1, Traffic=Low.");
        }

        private void Start()
        {
            // Auto-asignar calleGenerator si está en el mismo GameObject que potholeGenerator
            if (calleGenerator == null && potholeGenerator != null)
                calleGenerator = potholeGenerator.GetComponent<GeneradorDeCalle>();

            if (startButton != null)
                startButton.onClick.AddListener(StartExperimentBatch);
        }

        [ContextMenu("▶ Start Experiment Batch")]
        public void StartExperimentBatch()
        {
            // Si startFromEpisode > 0, usar ese punto de partida (no resetear)
            // startFromEpisode es 1-indexed en el Inspector (episodio #1, #2, ... #28)
            if (startFromEpisode > 0)
            {
                episodeCounter = startFromEpisode - 1;  // Convertir a 0-indexed
                seedCounter = startFromEpisode - 1;
                Debug.Log($"[Automator] 🎯 Iniciando desde episodio #{startFromEpisode} (0-indexed: {episodeCounter}) (sin resetear)");
            }
            else
            {
                ResetExperiment();
            }

            // Buscar DigitalTwinManager (Instance del singleton o Find directo)
            DigitalTwinManager dtm = DigitalTwinManager.Instance;
            if (dtm == null)
            {
                dtm = FindFirstObjectByType<DigitalTwinManager>(FindObjectsInactive.Include);
                if (dtm == null)
                {
                    Debug.LogError("[Automator] DigitalTwinManager no encontrado en la escena.");
                    return;
                }
                // Forzar la asignación singleton si no estaba seteado
                if (DigitalTwinManager.Instance == null)
                {
                    var field = typeof(DigitalTwinManager).GetField("Instance", 
                        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                    if (field != null)
                        field.SetValue(null, dtm);
                }
                Debug.Log("[Automator] DigitalTwinManager encontrado por Find (Instance era null)");
            }

            // Desactivar captura de imágenes para ahorrar memoria
            var captureManager = FindFirstObjectByType<PotholeCaptureManager>();
            if (captureManager != null)
            {
                captureManager.enabled = false;
                Debug.Log("[Automator] 📸 Captura de imágenes DESACTIVADA para ahorrar memoria.");
            }

            // Desactivar cliente de IA (no enviar imágenes a Python)
            var iaClient = FindFirstObjectByType<PythonInferenceClient>();
            if (iaClient != null)
            {
                iaClient.enabled = false;
                Debug.Log("[Automator] 🤖 Cliente IA DESACTIVADO. No se enviarán imágenes a Python.");
            }

            string savedData = PlayerPrefs.GetString(PREFS_EPISODE_DATA, "");

            DiscoverZones();

            Debug.Log($"[Automator] {callesMap.Count} calle(s) | {allZones.Length} segmentos totales:");
            foreach (var kv in callesMap)
                Debug.Log($"  • {kv.Key.name}: {kv.Value.Count} segmento(s)");

            // Aplicar estado inicial (tráfico, altura, navegación)
            SetDensity(initialTrafficLevel + 1);
            SetAltitude(initialAltitudeLevel);
            SetNavigation(initialNavigationMode);

            Debug.Log($"[Automator] 🎮 Estado inicial: Traffic={((TrafficDensity)(initialTrafficLevel + 1)).ToString()}, " +
                      $"Altitude={((DroneHeightController.DroneHeightLevel)initialAltitudeLevel).ToString()}, " +
                      $"Navigation={((NavigationMode)initialNavigationMode).ToString()}");

            if (episodeCounter > 0)
                Debug.Log($"[Automator] ♻ Reanudando desde episodio #{episodeCounter + 1}.");

            StartCoroutine(RunAllPermutations(savedData));
        }

        private IEnumerator ResetGenerators()
        {
            long ticks = System.DateTime.Now.Ticks;

            // Verificar referencias
            Debug.Log($"[Automator] 🔍 potholeGenerator={(potholeGenerator != null ? potholeGenerator.name : "NULL")} | " +
                      $"calleGenerator={(calleGenerator != null ? calleGenerator.name : "NULL")}");

            // 1. Generar Baches (igual que SceneInitializer)
            if (potholeGenerator != null)
            {
                int oldSeed = potholeGenerator.Seed;
                potholeGenerator.Seed = (int)((ticks & 0x7FFFFFFF) + seedCounter);
                seedCounter++;
                Debug.Log($"[Automator] 🌱 Pothole seed: {oldSeed} → {potholeGenerator.Seed}");
                potholeGenerator.gameObject.SetActive(true);
                potholeGenerator.Generate();
            }
            yield return null;

            // 2. Generar Calle (igual que SceneInitializer)
            if (calleGenerator != null)
            {
                calleGenerator.gameObject.SetActive(true);
                calleGenerator.Generate();
            }
            yield return null;
            yield return null;

            // 3. Esperar estabilización de física (igual que SceneInitializer)
            Debug.Log("[Automator] ⏳ Esperando estabilización física...");
            yield return new WaitForSeconds(1.5f);

            Debug.Log("[Automator] ✅ Generadores reseteados y física estabilizada.");
        }

        private void DiscoverZones()
        {
            allZones = FindObjectsByType<MostrarSoloAlPasarMouse>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            callesMap = GroupByParent(allZones ?? new MostrarSoloAlPasarMouse[0]);
        }

        private Dictionary<Transform, List<MostrarSoloAlPasarMouse>> GroupByParent(
            MostrarSoloAlPasarMouse[] zones)
        {
            var map = new Dictionary<Transform, List<MostrarSoloAlPasarMouse>>();
            foreach (var zone in zones)
            {
                Transform parent = zone.transform.parent ?? zone.transform;
                if (!map.ContainsKey(parent))
                    map[parent] = new List<MostrarSoloAlPasarMouse>();
                map[parent].Add(zone);
            }
            foreach (var kv in map)
                kv.Value.Sort((a, b) => string.Compare(
                    a.gameObject.name, b.gameObject.name,
                    System.StringComparison.OrdinalIgnoreCase));
            return map;
        }

        private List<MostrarSoloAlPasarMouse> GetUnvisitedSegmentsShuffled(List<string> visited)
        {
            var all = new List<MostrarSoloAlPasarMouse>();
            foreach (var kv in callesMap)
                foreach (var seg in kv.Value)
                    if (!visited.Contains(seg.gameObject.name))
                        all.Add(seg);
            for (int i = all.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                var tmp = all[i];
                all[i] = all[j];
                all[j] = tmp;
            }
            return all;
        }

        // ── Corrutina principal ───────────────────────────────────────────────
        private IEnumerator RunAllPermutations(string savedData)
        {
            Debug.Log("[Automator] ══════════════════════════════════════════");
            Debug.Log("[Automator]  Batch iniciado — 20 segmentos por episodio");
            Debug.Log("[Automator] ══════════════════════════════════════════");

            globalTableData.Clear();
            paperTableData.Clear();
            paperTableData.Add("Traffic,Altitude,Strategy,Coverage(%),Time(s),Energy(%),Recovery(%)");

            string dirPath = Path.Combine(Application.dataPath, "DigitalTwin_Logs");
            Directory.CreateDirectory(dirPath);
            progressCsvPath = Path.Combine(dirPath, "Progress_Results.csv");

            string globalHeader = BuildGlobalHeader();
            File.WriteAllText(progressCsvPath, globalHeader + System.Environment.NewLine);
            globalTableData.Add(globalHeader);

            var permutations = new List<(int t, int a, int s)>();
            int[] phase1Strategies = { 1, 2, 4 };
            for (int t = 0; t <= 2; t++)
                for (int a = 0; a <= 2; a++)
                    foreach (int s in phase1Strategies)
                        permutations.Add((t, a, s));
            for (int t = 0; t <= 2; t++)
                for (int a = 0; a <= 2; a++)
                    permutations.Add((t, a, 3));

            // Filtrar a solo 9 episodios de grabación si el modo está activo
            if (recordingMode)
            {
                var recordingEpisodes = new HashSet<(int, int, int)>
                {
                    (0, 0, 1),  // Low-Low-Baseline
                    (0, 1, 3),  // Low-Medium-Micro
                    (0, 2, 1),  // Low-High-Baseline
                    (1, 0, 2),  // Medium-Low-Hover
                    (1, 1, 1),  // Medium-Medium-Baseline
                    (1, 2, 3),  // Medium-High-Micro
                    (2, 0, 4),  // High-Low-Skip
                    (2, 1, 3),  // High-Medium-Micro
                    (2, 2, 2)   // High-High-Hover
                };
                permutations = permutations.FindAll(p => recordingEpisodes.Contains(p));
                Debug.Log($"[Automator] 🎬 MODO GRABACIÓN ACTIVO — {permutations.Count} episodios seleccionados");
            }

            for (int ep = 0; ep < permutations.Count; ep++)
            {
                if (ep < episodeCounter)
                {
                    Debug.Log($"[Automator] ⏭ Episodio #{ep + 1} ya completado, saltando.");
                    continue;
                }

                var (t, a, s) = permutations[ep];

                SetAltitude(a);
                SetNavigation(s);

                string trafficStr = ((TrafficDensity)(t + 1)).ToString();
                string altStr     = ((DroneHeightController.DroneHeightLevel)a).ToString();
                string navStr     = ((NavigationMode)s).ToString();

                Debug.Log($"[Automator] ▶▶▶ Episodio #{ep + 1}/{permutations.Count} | {trafficStr} / {altStr} / {navStr}");

                // Iniciar grabación de video si el modo grabación está activo
                if (recordingMode && videoRecorder != null)
                {
                    videoRecorder.StartRecording(ep + 1);
                }

                StreetSummary res = new StreetSummary();
                yield return StartCoroutine(RunTwentySegments(
                    t, a, s, trafficStr, altStr, navStr,
                    summary => res = summary));

                // Detener grabación de video si estaba activa
                if (recordingMode && videoRecorder != null)
                {
                    videoRecorder.StopRecording();
                }

                var ic = System.Globalization.CultureInfo.InvariantCulture;
                string paperRow = $"{trafficStr},{altStr},{navStr}," +
                                  $"{res.avgCoverage.ToString("F2", ic)}," +
                                  $"{res.totalTime.ToString("F2", ic)}," +
                                  $"{res.totalEnergy.ToString("F2", ic)}," +
                                  $"{res.avgRecovery.ToString("F2", ic)}";
                paperTableData.Add(paperRow);

                episodeCounter = ep + 1;
                PlayerPrefs.SetInt(PREFS_EPISODE, episodeCounter);
                PlayerPrefs.SetString(PREFS_EPISODE_DATA, string.Join("\n", paperTableData));
                PlayerPrefs.Save();
            }

            string finalPath = Path.Combine(
                Application.dataPath, "DigitalTwin_Logs", "All_Streets_Results.csv");
            File.WriteAllLines(finalPath, globalTableData);

            string paperPath = Path.Combine(
                Application.dataPath, "DigitalTwin_Logs", "Paper_Table_Results.csv");
            File.WriteAllLines(paperPath, paperTableData);

            PlayerPrefs.DeleteKey(PREFS_EPISODE);
            PlayerPrefs.DeleteKey(PREFS_EPISODE_DATA);
            PlayerPrefs.Save();

            int totalCompleted = recordingMode ? 9 : 36;
            Debug.Log("[Automator] ══════════════════════════════════════════");
            Debug.Log($"[Automator]  ✅ BATCH TERMINADO — {totalCompleted} episodios completados");
            Debug.Log($"[Automator]  📊 CSV: {finalPath}");
            Debug.Log($"[Automator]  🏆 CSV PAPER: {paperPath}");
            Debug.Log("[Automator] ══════════════════════════════════════════");
        }

        // ── Ejecuta EXACTAMENTE 20 segmentos para una combinación ──────────
        private IEnumerator RunTwentySegments(
            int trafficLevel, int altLevel, int navLevel,
            string trafficStr, string altStr, string navStr,
            System.Action<StreetSummary> onComplete)
        {
            // Obtener DigitalTwinManager (con fallback)
            DigitalTwinManager dtm = DigitalTwinManager.Instance ?? FindFirstObjectByType<DigitalTwinManager>(FindObjectsInactive.Include);
            if (dtm == null)
            {
                Debug.LogError("[Automator] DigitalTwinManager no encontrado en RunTwentySegments.");
                yield break;
            }

            var mi = dtm.movementInterface;
            if (mi != null) mi.ResetDetectedPotholes();

            dtm.suppressAutoEndEpisode = true;
            dtm.StartNewEpisode();

            // ─── RESETEAR GENERADORES al inicio de cada episodio ───────────
            Debug.Log($"[Automator] 🌱 Reseteando generadores para nuevo episodio...");
            yield return StartCoroutine(ResetGenerators());

            SetDensity(trafficLevel + 1);
            yield return new WaitForSeconds(0.5f);
            DiscoverZones();

            // Mostrar lista completa de segmentos disponibles
            Debug.Log($"[Automator] 📋 Segmentos disponibles para este episodio:");
            int segIndex = 0;
            foreach (var kv in callesMap)
            {
                foreach (var seg in kv.Value)
                {
                    segIndex++;
                    Debug.Log($"[Automator]   {segIndex}. {seg.gameObject.name} (calle: {kv.Key.name})");
                }
            }

            var segResults = new List<MovementInterface.SegmentResult>();
            int completedSegments = 0;
            var visitedSegments = new List<string>();  // Lista para mantener orden y duplicados

            while (completedSegments < segmentsPerEpisode)
            {
                var availableSegments = GetUnvisitedSegmentsShuffled(visitedSegments);

                if (availableSegments.Count == 0)
                {
                    Debug.Log($"[Automator] 🔄 Todos visitados ({visitedSegments.Count}). Regenerando (2da vez)...");
                    yield return StartCoroutine(ResetGenerators());
                    SetDensity(trafficLevel + 1);
                    yield return new WaitForSeconds(0.5f);
                    DiscoverZones();
                    visitedSegments.Clear();
                    availableSegments = GetUnvisitedSegmentsShuffled(visitedSegments);
                    if (availableSegments.Count == 0)
                    {
                        Debug.LogError("[Automator] ❌ Sin zonas. Abortando.");
                        break;
                    }
                }

                foreach (var seg in availableSegments)
                {
                    if (completedSegments >= segmentsPerEpisode) break;

                    string segName = seg.gameObject.name;
                    visitedSegments.Add(segName);

                    float startEnergy = droneController.energyController != null
                        ? droneController.energyController.energia : 100f;

                    if (mi != null) mi.StartSegment(segName, startEnergy);

                    Debug.Log($"[Automator]   → Seg {completedSegments + 1}/{segmentsPerEpisode}: {segName}");

                    droneController.segmentDone  = false;
                    droneController.apagado       = false;
                    droneController.manualControl = false;
                    droneController.SetSearchArea(seg.primeraPosicion, seg.posicionFinal);

                    float segStart = Time.time;
                    yield return new WaitForSeconds(1f);

                    while (!droneController.segmentDone)
                    {
                        if (Time.time - segStart >= maxTimeoutSeconds)
                        {
                            Debug.LogWarning($"[Automator] ⚠ Timeout '{segName}'. Forzando.");
                            droneController.segmentDone = true;
                            break;
                        }
                        yield return null;
                    }

                    float endEnergy = droneController.energyController != null
                        ? droneController.energyController.energia : 0f;

                    MovementInterface.SegmentResult result = null;
                    if (mi != null) result = mi.EndSegment(endEnergy);

                    if (result != null)
                    {
                        // Guardar el nombre del segmento en el resultado
                        result.name = segName;
                        segResults.Add(result);
                        Debug.Log($"[Automator]   ✓ #{completedSegments + 1}: Coverage={result.Coverage:F1}% | Recovery={result.RecoveryRatio:F1}%");
                        
                        // Guardar CSV después de CADA segmento
                        try
                        {
                            string segmentPath = Path.Combine(Application.dataPath, "DigitalTwin_Logs", $"Ep{episodeCounter + 1}_Seg{completedSegments + 1}_{segName}.csv");
                            Debug.Log($"[Automator]   💾 Guardando CSV: {Path.GetFileName(segmentPath)}");
                            AppendSingleSegmentDetails(segmentPath, episodeCounter + 1, completedSegments + 1, segName, trafficStr, altStr, navStr, result);
                            Debug.Log($"[Automator]   ✅ CSV guardado exitosamente");
                        }
                        catch (System.Exception ex)
                        {
                            Debug.LogError($"[Automator]   ❌ ERROR guardando CSV: {ex.Message}");
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[Automator]   ⚠ result es NULL para segmento {segName}");
                    }

                    completedSegments++;
                    Debug.Log($"[Automator]   📊 Segmentos completados: {completedSegments}/{segmentsPerEpisode}");
                    if (droneController != null &&
                        droneController.ModeLine &&
                        droneController.ConsumeModeLineCycleCompleted() &&
                        completedSegments < segmentsPerEpisode)
                    {
                        Debug.Log("[Automator] ModeLine completo todas sus lineas. Reseteando baches/calle antes de repetir...");
                        yield return StartCoroutine(ResetGenerators());
                        SetDensity(trafficLevel + 1);
                        yield return new WaitForSeconds(0.5f);
                        DiscoverZones();
                        visitedSegments.Clear();
                        break;
                    }

                    yield return new WaitForSeconds(0.3f);
                }
            }

            Debug.Log($"[Automator] ⚡ 20 segmentos. Retornando a base...");
            droneController.ReturnToBase();
            float rechargeStart = Time.time;
            while (true)
            {
                if (droneController.energyController != null &&
                    droneController.energyController.recargaCompleta)
                {
                    droneController.energyController.recargaCompleta = false;
                    break;
                }
                if (Time.time - rechargeStart >= maxTimeoutSeconds)
                {
                    Debug.LogWarning("[Automator] ⚠ Timeout recarga.");
                    break;
                }
                yield return null;
            }

            DigitalTwinManager.Instance.suppressAutoEndEpisode = false;
            DigitalTwinManager.Instance.EndEpisode();

            string row = BuildStreetRow(trafficStr, altStr, navStr, $"Ep_{episodeCounter + 1}", segResults, visitedSegments, out StreetSummary summary);
            globalTableData.Add(row);
            AppendProgressCsv(row);

            // Guardar detalle de cada segmento individual en archivo Episode_N_fecha.csv
            Debug.Log($"[Automator] 💾 Guardando {segResults.Count} segmentos en CSV...");
            string episodeDetailPath = Path.Combine(Application.dataPath, "DigitalTwin_Logs", $"Episode_{episodeCounter + 1}_{System.DateTime.Now:yyyyMMdd_HHmmss}.csv");
            AppendSegmentDetails(episodeDetailPath, episodeCounter + 1, trafficStr, altStr, navStr, segResults);
            Debug.Log($"[Automator] ✅ CSV guardado: {episodeDetailPath}");

            Debug.Log($"[Automator] ✅ Episodio completado. {segResults.Count} segs | Coverage={summary.avgCoverage:F1}%");

            if (onComplete != null)
                onComplete(summary);
        }

        private string BuildGlobalHeader()
        {
            return "Traffic,Altitude,Strategy,Coverage(%),Recovery(%),Segs_with_obstacles,Total_segs,Total_Time(s),Total_Energy(%)";
        }

        private string BuildStreetRow(
            string traffic, string alt, string nav, string label,
            List<MovementInterface.SegmentResult> segs,
            List<string> visitedSegments, out StreetSummary summary)
        {
            var ic = System.Globalization.CultureInfo.InvariantCulture;
            var cols = new List<string> { traffic, alt, nav, label };

            float totalCov = 0f, totalRec = 0f, totalTime = 0f, totalEnergy = 0f;
            int segsWithObst = 0;

            foreach (var seg in segs)
            {
                totalCov    += seg.Coverage;
                totalRec    += seg.RecoveryRatio;
                totalTime   += seg.timeTaken;
                totalEnergy += seg.energyConsumed;
                if (seg.hadObstacles) segsWithObst++;
            }

            int n = segs.Count;
            float avgCov = n > 0 ? totalCov / n : 0f;
            float avgRec = n > 0 ? totalRec / n : 0f;

            cols.Add(avgCov.ToString("F2", ic));
            cols.Add(avgRec.ToString("F2", ic));
            cols.Add(segsWithObst.ToString());
            cols.Add(n.ToString());
            cols.Add(totalTime.ToString("F2", ic));
            cols.Add(totalEnergy.ToString("F2", ic));

            summary = new StreetSummary { avgCoverage = avgCov, avgRecovery = avgRec, totalTime = totalTime, totalEnergy = totalEnergy };
            return string.Join(",", cols);
        }

        private struct StreetSummary
        {
            public float avgCoverage;
            public float avgRecovery;
            public float totalTime;
            public float totalEnergy;
        }

        private void AppendProgressCsv(string row)
        {
            try { File.AppendAllText(progressCsvPath, row + System.Environment.NewLine); }
            catch (System.Exception ex) { Debug.LogWarning($"[Automator] Error CSV: {ex.Message}"); }
        }

        private void AppendSingleSegmentDetails(string filePath, int episode, int segNum, string segName, string traffic, string alt, string nav, MovementInterface.SegmentResult seg)
        {
            try
            {
                var ic = System.Globalization.CultureInfo.InvariantCulture;
                string header = "Episode,Segment_Num,Segment_Name,Traffic,Altitude,Strategy,Coverage(%),Recovery(%),Time(s),Energy(%),Had_Obstacles";
                string line = string.Join(",", new string[]
                {
                    $"Ep_{episode}",
                    segNum.ToString(),
                    segName,
                    traffic,
                    alt,
                    nav,
                    seg.Coverage.ToString("F2", ic),
                    seg.RecoveryRatio.ToString("F2", ic),
                    seg.timeTaken.ToString("F2", ic),
                    seg.energyConsumed.ToString("F2", ic),
                    seg.hadObstacles ? "1" : "0"
                });
                File.WriteAllText(filePath, header + System.Environment.NewLine + line);
            }
            catch (System.Exception ex) { Debug.LogWarning($"[Automator] Error Segment CSV: {ex.Message}"); }
        }

        private void AppendSegmentDetails(string filePath, int episode, string traffic, string alt, string nav, List<MovementInterface.SegmentResult> segs)
        {
            try
            {
                var ic = System.Globalization.CultureInfo.InvariantCulture;
                var lines = new List<string>();

                for (int i = 0; i < segs.Count; i++)
                {
                    var seg = segs[i];
                    string line = string.Join(",", new string[]
                    {
                        $"Ep_{episode}",
                        (i + 1).ToString(),
                        seg.name ?? "Unknown",
                        traffic,
                        alt,
                        nav,
                        seg.Coverage.ToString("F2", ic),
                        seg.RecoveryRatio.ToString("F2", ic),
                        seg.timeTaken.ToString("F2", ic),
                        seg.energyConsumed.ToString("F2", ic),
                        seg.hadObstacles ? "1" : "0"
                    });
                    lines.Add(line);
                }

                File.AppendAllLines(filePath, lines);
            }
            catch (System.Exception ex) { Debug.LogWarning($"[Automator] Error Segment CSV: {ex.Message}"); }
        }

        private void SetDensity(int target)
        {
            if (densityController == null) return;
            for (int i = 0; i < 6 && (int)densityController.currentDensity != target; i++)
                densityController.CycleDensityMode();
        }
        private void SetAltitude(int target)
        {
            if (heightController == null) return;
            for (int i = 0; i < 6 && (int)heightController.currentLevel != target; i++)
                heightController.CycleHeightMode();
        }
        private void SetNavigation(int target)
        {
            if (droneController == null) return;
            for (int i = 0; i < 6 && (int)droneController.navigationMode != target; i++)
                droneController.CycleNavigationMode();
        }
    }
}
