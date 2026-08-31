using UnityEngine;
using System.Collections;

public class SceneInitializer : MonoBehaviour
{
    [Header("Generadores")]
    public GeneradorDeCalle calleGenerator;
    public TerrainPotholeGenerator bachesGenerator;


    [Header("NavMesh")]
    public NavMeshdrone navMeshDrone;

    [Header("Lógica de Juego")]
    public ToggleActiveExclusive gameLogicToggle;

    // Start es eliminado/vacío porque LoadingScreenController llamará manualmente a BeginInitialization()
    // O simplemente activará el objeto si usamos OnEnable.
    // Para mayor control, usaremos un método público.
    
    public void BeginInitialization()
    {
        StartCoroutine(InitializeSceneSequence());
    }

    private IEnumerator InitializeSceneSequence()
    {
        Debug.Log("[SceneInitializer] Iniciando secuencia de carga...");



        // 2. Generar Baches
        if (bachesGenerator != null)
        {
            Debug.Log("[SceneInitializer] Generando Baches...");
            bachesGenerator.gameObject.SetActive(true);
            bachesGenerator.Generate();
        }
        yield return null;

        // 2. Generar Terreno Base (Calle) - Despues de baches para que pueda recortarlos
        if (calleGenerator != null)
        {
            Debug.Log("[SceneInitializer] Generando Calle...");
            calleGenerator.gameObject.SetActive(true);
            calleGenerator.Generate();
        }
        yield return null;
        yield return null;



        // 4. Esperar a que la física se estabilice y EliminarTriangulos haga su trabajo
        Debug.Log("[SceneInitializer] Esperando estabilización física...");
        yield return new WaitForSeconds(1.5f); // Incrementado para asegurar que la malla se genera completamente

        // 5. Hornear NavMesh
        if (navMeshDrone != null)
        {
            Debug.Log("[SceneInitializer] Horneando NavMesh...");
            navMeshDrone.gameObject.SetActive(true);
            navMeshDrone.ManualBake();
        }
        yield return null;

        // 6. Activar Lógica de Juego (Exclusive Toggles)
        if (gameLogicToggle != null)
        {
            Debug.Log("[SceneInitializer] Activando Lógica de Juego...");
            gameLogicToggle.gameObject.SetActive(true); // Asegurar que el objeto esté activo
            gameLogicToggle.Initialize(); // Llamar a la inicialización manual
        }

        Debug.Log("[SceneInitializer] Secuencia completada.");
        IsInitializeComplete = true;
    }

    public bool IsInitializeComplete { get; private set; }
}
