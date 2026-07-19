using UnityEngine;
using Unity.AI.Navigation;
using UnityEngine.AI;

public class NavMeshdrone : MonoBehaviour
{
    void OnEnable()
    {
        // Deshabilitado para ejecución manual desde SceneInitializer.
    }

    public void ManualBake()
    {
        PrepararObjetosBache();
        BakeAllNavMeshes();
    }

    // Marcar objetos con tag "bache" como estáticos y layer 7
    void PrepararObjetosBache()
    {
        GameObject[] baches = GameObject.FindGameObjectsWithTag("bache");

        foreach (var bache in baches)
        {
            bache.isStatic = true;
            bache.layer = 7;
        }

        Debug.Log($"Preparados {baches.Length} objetos con tag 'bache'");
    }

    public void BakeAllNavMeshes()
    {
        // Buscar todos los NavMeshSurface en la escena
        NavMeshSurface[] surfaces = Object.FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None);

        foreach (var surface in surfaces)
        {
            // Forzar el uso de Colliders en lugar de Meshes para evitar el crash de Memoria "Read/Write"
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            
            // Asegurar que la Layer 7 (Obstacles/Baches) esté incluida en el bake
            surface.layerMask = surface.layerMask | (1 << 7);
            
            surface.BuildNavMesh();
        }

        Debug.Log($"Baked {surfaces.Length} NavMesh surfaces");
    }
}
