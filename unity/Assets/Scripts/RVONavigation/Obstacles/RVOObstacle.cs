using UnityEngine;
using System.Collections.Generic;
using RVO;

public class RVOObstacle : MonoBehaviour
{
    // isConvex eliminado — se asignaba pero nunca se leía en la lógica
    [SerializeField] private bool isClockwise = false; // true para obstáculos negativos
    
    private int rvoObstacleId = -1;
    private List<RVO.Vector2> vertices = new List<RVO.Vector2>();
    
    private void Start()
    {
        // Extraer vértices del collider
        if (!ExtractVertices())
        {
            Debug.LogError($"[RVO] No se pudieron extraer vértices de '{gameObject.name}'");
            return;
        }
        
        // Registrar en manager y en el simulador RVO de inmediato.
        // processObstacles() será llamado por RVOSceneSetup después.
        RVOSimulationManager.Instance.RegisterObstacle(this);
        RegisterInRVO();
        
        Debug.Log($"[RVO] Obstáculo '{gameObject.name}' preparado con {vertices.Count} vértices");
    }
    
    private bool ExtractVertices()
    {
        vertices.Clear();
        
        // Opción 1: Usar BoxCollider
        BoxCollider boxCollider = GetComponent<BoxCollider>();
        if (boxCollider != null)
        {
            Vector3 size = boxCollider.size;
            Vector3 center = boxCollider.center;
            
            Vector3[] corners = new Vector3[4]
            {
                transform.TransformPoint(center + new Vector3(-size.x/2, 0, -size.z/2)),
                transform.TransformPoint(center + new Vector3(size.x/2, 0, -size.z/2)),
                transform.TransformPoint(center + new Vector3(size.x/2, 0, size.z/2)),
                transform.TransformPoint(center + new Vector3(-size.x/2, 0, size.z/2))
            };
            
            foreach (Vector3 corner in corners)
            {
                vertices.Add(new RVO.Vector2(corner.x, corner.z));
            }
            
            return true;
        }
        
        // Opción 2: Usar MeshCollider
        MeshCollider meshCollider = GetComponent<MeshCollider>();
        if (meshCollider != null && meshCollider.convex)
        {
            Mesh mesh = meshCollider.sharedMesh;
            if (mesh != null)
            {
                // Projectar vértices a plano XZ
                foreach (Vector3 vert in mesh.vertices)
                {
                    Vector3 worldVert = transform.TransformPoint(vert);
                    vertices.Add(new RVO.Vector2(worldVert.x, worldVert.z));
                }
                return true;
            }
        }
        
        // Opción 3: Usar PolygonCollider2D (en 3D, ignorar Y)
        PolygonCollider2D polyCollider = GetComponent<PolygonCollider2D>();
        if (polyCollider != null)
        {
            UnityEngine.Vector2[] points = polyCollider.points;
            foreach (UnityEngine.Vector2 point in points)
            {
                Vector3 worldPoint = transform.TransformPoint(new Vector3(point.x, 0, point.y));
                vertices.Add(new RVO.Vector2(worldPoint.x, worldPoint.z));
            }
            return true;
        }
        
        return false;
    }
    
    public void RegisterInRVO()
    {
        if (vertices.Count < 2)
        {
            Debug.LogError($"[RVO] Obstáculo '{gameObject.name}' requiere al menos 2 vértices");
            return;
        }
        
        // Invertir orden si es clockwise (para obstáculos negativos)
        if (isClockwise)
        {
            vertices.Reverse();
        }
        
        // Añadir a simulador RVO
        rvoObstacleId = Simulator.Instance.addObstacle(vertices);
        
        if (rvoObstacleId >= 0)
        {
            Debug.Log($"[RVO] Obstáculo '{gameObject.name}' registrado con ID: {rvoObstacleId}");
        }
        else
        {
            Debug.LogError($"[RVO] Error registrando obstáculo '{gameObject.name}'");
        }
    }
    
    public int GetRVOObstacleId() => rvoObstacleId;
    
    private void OnDestroy()
    {
        if (RVOSimulationManager.Instance != null)
        {
            RVOSimulationManager.Instance.UnregisterObstacle(this);
        }
    }
}
