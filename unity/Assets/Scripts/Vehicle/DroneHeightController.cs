using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.AI.Navigation;

/// <summary>
/// Cicla la altura de vuelo del dron (y su NavMesh) entre tres niveles:
///   Low    → 3 m
///   Medium → 6 m
///   High   → 9 m
///
/// Conectar CycleHeightMode() al OnClick() del botón en el Inspector.
/// </summary>
public class DroneHeightController : MonoBehaviour
{
    // ─── Niveles de altura ───────────────────────────────────────────────────
    // En Unity, 1 unidad = 1 metro. baseOffset y targetHeight deben ser iguales.
    // Alturas realistas para inspección con drone: Low (6m), Medium (10m), High (15m)
    private static readonly float[] Heights = { 6f, 10f, 15f };
    // Espaciado fijo de rayos: 0.5m para todas las alturas (el barrido del drone cubre los bordes)
    private static readonly float[] RaySpacings = { 0.5f, 0.5f, 0.5f };

    public enum DroneHeightLevel
    {
        Low    = 0,
        Medium = 1,
        High   = 2
    }

    // ─── UI ──────────────────────────────────────────────────────────────────
    [Header("UI Configuration")]
    [Tooltip("Botón que ciclará la altura. Asignar en el Inspector.")]
    public Button heightButton;

    [Tooltip("Texto (TMP) del botón. Si es nulo se busca automáticamente en los hijos.")]
    public TMP_Text heightButtonText;

    // ─── Referencias ─────────────────────────────────────────────────────────
    [Header("Referencias")]
    [Tooltip("Controlador del dron (DroneNavMeshController).")]
    public DroneNavMeshController droneController;

    [Tooltip("El NavMeshAgent del dron.")]
    public UnityEngine.AI.NavMeshAgent navAgent;

    [Tooltip("Interfaz de movimiento para ajustar el espaciado de rayos.")]
    public MovementInterface movementInterface;

    // ─── Estado ───────────────────────────────────────────────────────────────
    [Header("Estado Actual")]
    public DroneHeightLevel currentLevel = DroneHeightLevel.Low;

    // ─── Unity ───────────────────────────────────────────────────────────────
    private void Start()
    {
        if (droneController == null)
            droneController = FindFirstObjectByType<DroneNavMeshController>();

        if (navAgent == null && droneController != null)
            navAgent = droneController.GetComponent<UnityEngine.AI.NavMeshAgent>();

        if (movementInterface == null)
            movementInterface = FindFirstObjectByType<MovementInterface>();

        UpdateUIButton();
    }

    // ─── Público (conectar al botón) ─────────────────────────────────────────
    public void CycleHeightMode()
    {
        int next = ((int)currentLevel + 1) % 3;
        currentLevel = (DroneHeightLevel)next;

        ApplyHeight();
        UpdateUIButton();

        Debug.Log($"[DroneHeight] Nivel: {currentLevel} | Altura configurada: {Heights[(int)currentLevel]}m | RaySpacing: {RaySpacings[(int)currentLevel]}");
    }

    // ─── Privado ──────────────────────────────────────────────────────────────
    private void ApplyHeight()
    {
        float targetH = Heights[(int)currentLevel]; // Altura real deseada en metros (6, 12, 18)
        float rayS = RaySpacings[(int)currentLevel];

        // 1) Actualizar altura objetivo del dron (física PID - usa coordenadas globales)
        if (droneController != null)
        {
            droneController.targetHeight = targetH;
            
            // Sincronizar minHeight para evitar que el dron se bloquee
            droneController.minHeight = targetH * 0.5f; 
        }

        // 2) Actualizar baseOffset y height del NavMeshAgent
        if (navAgent != null)
        {
            float worldScaleY = navAgent.transform.lossyScale.y;
            
            if (worldScaleY > 0.001f)
            {
                float baseVal = targetH / worldScaleY; 
                navAgent.baseOffset = baseVal;
                navAgent.height = baseVal; 
            }
            else
            {
                navAgent.baseOffset = targetH;
                navAgent.height = targetH;
            }
        }

        // 3) Actualizar raySpacing en MovementInterface
        if (movementInterface != null)
        {
            movementInterface.raySpacing = rayS;
        }
    }

    /// <summary>Actualiza el texto del botón para mostrar el nivel actual.</summary>
    private void UpdateUIButton()
    {
        if (heightButton == null) return;

        TMP_Text label = heightButtonText != null
            ? heightButtonText
            : heightButton.GetComponentInChildren<TMP_Text>();

        if (label != null)
            label.text = "Height:" + currentLevel.ToString(); // "Height:Low", "Height:Medium" o "Height:High"
    }
}
