using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ejemplo visual: Mostrar el comportamiento de evitación en tiempo real
/// Útil para entender y debuggear cómo funciona el sistema de evitación
/// </summary>
public class AvoidanceVisualization : MonoBehaviour
{
    [Header("Configuración de Test")]
    [SerializeField] private int numberOfPeople = 4;
    [SerializeField] private Vector3 areaCenter = Vector3.zero;
    [SerializeField] private Vector3 areaSize = new Vector3(15, 2, 15);

    [Header("Parámetros a Probar")]
    [SerializeField] private float detectionRadius = 2.5f;
    [SerializeField] private float pauseDistance = 0.8f;
    [SerializeField] private float avoidanceForce = 0.5f;

    [Header("Visualización")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color avoidingColor = Color.red;
    [SerializeField] private bool showDetectionRadius = true;
    [SerializeField] private bool showInfluenceArea = true;

    private List<PersonController> people = new List<PersonController>();
    private List<Renderer> personRenderers = new List<Renderer>();

    private void Start()
    {
        CreateTestSetup();
    }

    [ContextMenu("Crear Setup de Test")]
    public void CreateTestSetup()
    {
        // Limpiar anteriores
        foreach (var person in people)
        {
            if (person != null)
                DestroyImmediate(person.gameObject);
        }
        people.Clear();
        personRenderers.Clear();

        Debug.Log($"🔬 Creando setup de test con {numberOfPeople} personas");

        // Crear personas en círculo
        float angleStep = 360f / numberOfPeople;
        float circleRadius = 5f;

        for (int i = 0; i < numberOfPeople; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            Vector3 pos = areaCenter + new Vector3(
                Mathf.Cos(angle) * circleRadius,
                0,
                Mathf.Sin(angle) * circleRadius
            );

            // Crear GameObject
            GameObject personGO = new GameObject($"Person_{i}");
            personGO.transform.position = pos;

            // Agregar PersonController
            PersonController controller = personGO.AddComponent<PersonController>();

            // Generar ruta (ir hacia el centro y luego alrededor)
            List<Vector3> route = new List<Vector3>
            {
                pos,
                areaCenter,
                pos + new Vector3(2, 0, 0)
            };
            controller.SetWaypoints(route);

            // Aplicar configuración de test
            controller.SetInfluenceArea(areaCenter, areaSize);
            controller.SetAvoidanceEnabled(true);

            // Asegurar que los parámetros se aplican correctamente
            // (Nota: acceso por reflexión si no hay setters públicos)

            // Agregar renderer (cápsula visual)
            MeshRenderer renderer = personGO.GetComponent<MeshRenderer>();
            if (renderer != null)
                personRenderers.Add(renderer);

            people.Add(controller);
            Debug.Log($"  ✓ Person {i} creado en {pos}");
        }

        Debug.Log($"✅ Setup creado: {numberOfPeople} personas en área {areaSize}");
    }

    private void Update()
    {
        // Actualizar colores basados en estado de evitación
        for (int i = 0; i < people.Count; i++)
        {
            if (i >= personRenderers.Count) break;

            PersonController person = people[i];
            Renderer renderer = personRenderers[i];

            if (person != null && renderer != null)
            {
                Color targetColor = person.IsAvoiding() ? avoidingColor : normalColor;
                
                // Cambiar color de la cápsula (si tiene material)
                var materials = renderer.sharedMaterials;
                foreach (var mat in materials)
                {
                    mat.color = Color.Lerp(mat.color, targetColor, Time.deltaTime * 5f);
                }
            }
        }
    }

    [ContextMenu("Mostrar Estadísticas")]
    public void ShowStatistics()
    {
        Debug.Log("=== 📊 ESTADÍSTICAS DE EVITACIÓN ===");
        int avoidingCount = 0;
        float avgDistance = 0f;

        for (int i = 0; i < people.Count; i++)
        {
            PersonController person = people[i];
            if (person == null) continue;

            bool isAvoiding = person.IsAvoiding();
            if (isAvoiding) avoidingCount++;
            
            float distance = person.GetDistanceToCurrentWaypoint();
            avgDistance += distance;

            Debug.Log($"Person {i}: {(isAvoiding ? "🔴 EVITANDO" : "🟢 NORMAL")} | " +
                     $"WP {person.GetCurrentWaypointIndex()} | " +
                     $"Dist: {distance:F2}m");
        }

        avgDistance /= Mathf.Max(1, people.Count);
        Debug.Log($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Debug.Log($"Total Evitando: {avoidingCount}/{people.Count}");
        Debug.Log($"Distancia promedio: {avgDistance:F2}m");
        Debug.Log($"Parámetros: Detection={detectionRadius}m, " +
                 $"Pause={pauseDistance}m, " +
                 $"Force={avoidanceForce}");
    }

    [ContextMenu("Probar: Aumentar Detection")]
    public void TestIncreaseDetection()
    {
        detectionRadius += 0.5f;
        Debug.Log($"🔍 Detection Radius aumentado a {detectionRadius}m");
    }

    [ContextMenu("Probar: Disminuir Detection")]
    public void TestDecreaseDetection()
    {
        detectionRadius = Mathf.Max(0.5f, detectionRadius - 0.5f);
        Debug.Log($"🔍 Detection Radius disminuido a {detectionRadius}m");
    }

    [ContextMenu("Probar: Aumentar Avoidance Force")]
    public void TestIncreaseForce()
    {
        avoidanceForce = Mathf.Min(1f, avoidanceForce + 0.1f);
        Debug.Log($"💪 Avoidance Force aumentado a {avoidanceForce}");
    }

    [ContextMenu("Probar: Disminuir Avoidance Force")]
    public void TestDecreaseForce()
    {
        avoidanceForce = Mathf.Max(0f, avoidanceForce - 0.1f);
        Debug.Log($"💪 Avoidance Force disminuido a {avoidanceForce}");
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 350, 400));

        GUILayout.Label("🧪 AVOIDANCE VISUALIZATION TEST", 
            new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold });

        GUILayout.Space(10);

        GUILayout.Label("Estado Actual:", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
        int avoidingCount = 0;
        foreach (var person in people)
        {
            if (person != null && person.IsAvoiding())
                avoidingCount++;
        }
        GUILayout.Label($"Personas evitando: {avoidingCount}/{people.Count}");
        GUILayout.Label($"Área: {areaSize.x:F0} × {areaSize.z:F0}m");

        GUILayout.Space(10);

        GUILayout.Label("Parámetros de Test:", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
        
        GUILayout.BeginHorizontal();
        GUILayout.Label($"Detection: {detectionRadius:F1}m", GUILayout.Width(150));
        if (GUILayout.Button("+", GUILayout.Width(30)))
            TestIncreaseDetection();
        if (GUILayout.Button("-", GUILayout.Width(30)))
            TestDecreaseDetection();
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label($"Force: {avoidanceForce:F2}", GUILayout.Width(150));
        if (GUILayout.Button("+", GUILayout.Width(30)))
            TestIncreaseForce();
        if (GUILayout.Button("-", GUILayout.Width(30)))
            TestDecreaseForce();
        GUILayout.EndHorizontal();

        GUILayout.Label($"Pause Distance: {pauseDistance:F1}m");

        GUILayout.Space(10);

        if (GUILayout.Button("📊 Mostrar Estadísticas", GUILayout.Height(30)))
            ShowStatistics();

        if (GUILayout.Button("🔬 Recrear Setup", GUILayout.Height(30)))
            CreateTestSetup();

        GUILayout.Space(10);

        GUILayout.Label("Visualización:", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
        showDetectionRadius = GUILayout.Toggle(showDetectionRadius, "Mostrar Radios de Detección");
        showInfluenceArea = GUILayout.Toggle(showInfluenceArea, "Mostrar Área");

        GUILayout.Space(10);

        GUILayout.Label("Leyenda:", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
        GUILayout.Label("🟢 Verde = Normal");
        GUILayout.Label("🔴 Rojo = Evitando");
        GUILayout.Label("🟡 Amarillo = Círculo de detección");

        GUILayout.EndArea();
    }
}
