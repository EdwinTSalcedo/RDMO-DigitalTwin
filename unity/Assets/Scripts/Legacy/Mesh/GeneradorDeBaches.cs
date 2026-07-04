using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Instancia prefabs de "baches" en un área cuadrada centrada en el pivote,
/// evitando posiciones donde ya exista otro objeto en exactamente el mismo volumen
/// que ocupa el bache. Al terminar, marca <see cref="generacionTerminada"/> en TRUE
/// para que otros sistemas puedan reaccionar.
/// </summary>
public class GeneradorDeBaches : MonoBehaviour
{
    #region Parámetros públicos ────────────────

    [Header("Configuración de Baches")]
    public List<GameObject> baches;
    [Min(1)] public int cantidadBaches = 30;
    public GameObject objetoPadre;
    public Material waterMaterial;
    [Range(0f, 1f)]
    public float probabilidadAgua = 0.5f;

    [Header("Área centrada en el pivote")]
    [Min(0.1f)] public float ladoArea = 4f;
    [Min(0f)]   public float margenBorde = 0.5f;
    [Min(0.01f)]public float pasoCuadricula = 0.25f;
    public float alturaGeneracion = 0.01f;

    [Header("Detección de obstáculos")]
    public LayerMask capasObstaculos = ~0;     // "Everything" por defecto

    [Header("Estado de generación")]
    [Tooltip("Se pone en TRUE cuando el algoritmo terminó de intentar colocar los baches.")]
    public bool generacionTerminada = false;

    #endregion

    #region Ciclo de vida ──────────────────────

    private void OnEnable()
    {
        // Deshabilitado para ejecución manual desde SceneInitializer
    }

    public void Generate()
    {
#if UNITY_EDITOR
        if (UnityEditor.EditorApplication.isPaused) return;
#endif
        generacionTerminada = false;

        if (!EntradasValidas()) return;

        GenerarBaches();

        generacionTerminada = true;
    }

    #endregion

    #region Generación ─────────────────────────

    private void GenerarBaches()
    {
        float medioLado = ladoArea * 0.5f;
        float xmin = -medioLado + margenBorde;
        float xmax =  medioLado - margenBorde;
        float zmin = xmin;
        float zmax = xmax;
        Vector3 centro = transform.position;

        int generados = 0;
        int intentos = 0;
        int maxIntentos = cantidadBaches * 10;

        while (generados < cantidadBaches && intentos < maxIntentos)
        {
            intentos++;

            float xL = Random.Range(xmin, xmax);
            float zL = Random.Range(zmin, zmax);
            xL = Mathf.Round(xL / pasoCuadricula) * pasoCuadricula;
            zL = Mathf.Round(zL / pasoCuadricula) * pasoCuadricula;
            Vector3 posicion = centro + new Vector3(xL, alturaGeneracion, zL);

            GameObject prefab = baches[generados % baches.Count];
            if (!EspacioDisponible(posicion, prefab, out _))
                continue;

            GameObject bache = Instantiate(prefab, posicion, Quaternion.identity);
            bache.isStatic = true; // ← Generado estático para NavMesh
            bache.layer = 7; // ← Capa Obstacles
            bache.name = $"bache_{generados + 1}"; // ← Asignar nombre único

            // Asegurar que todos los sub-objetos con malla tengan collider y capa correcta
            MeshFilter[] meshFilters = bache.GetComponentsInChildren<MeshFilter>();
            foreach (var mf in meshFilters)
            {
                mf.gameObject.layer = 7; // Asegurar Layer 7 en hijos
                
                if (mf.GetComponent<Collider>() == null)
                {
                    MeshCollider mc = mf.gameObject.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                }
            }

            Renderer rend = bache.GetComponentInChildren<Renderer>();
            if (rend == null)
            {
                Debug.LogWarning("El prefab de bache no tiene Renderer asignado.");
                Destroy(bache);
                continue;
            }

            Bounds bounds = rend.bounds;

            float minX = centro.x - medioLado;
            float maxX = centro.x + medioLado;
            float minZ = centro.z - medioLado;
            float maxZ = centro.z + medioLado;

            if (bounds.min.x < minX || bounds.max.x > maxX || bounds.min.z < minZ || bounds.max.z > maxZ)
            {
                Destroy(bache);
                continue;
            }

            CrearPlanoDeAgua(bache);
            ConfigurarFisica(bache);

            if (objetoPadre != null)
                bache.transform.SetParent(objetoPadre.transform);

            generados++;
        }

        if (generados < cantidadBaches)
            Debug.LogWarning($"Solo se generaron {generados} de {cantidadBaches} baches; el resto quedó sin espacio libre.");
    }

    #endregion

    #region Utilidades ─────────────────────────

    private bool EspacioDisponible(Vector3 centro, GameObject prefab, out Vector3 halfExtents)
    {
        halfExtents = Vector3.zero;

        GameObject temp = Instantiate(prefab, centro, Quaternion.identity);
        temp.SetActive(false);

        Renderer rend = temp.GetComponentInChildren<Renderer>();
        if (rend == null)
        {
            Debug.LogWarning("El prefab no tiene Renderer.");
            DestroyImmediate(temp);
            return false;
        }

        Bounds bounds = rend.bounds;
        halfExtents = bounds.extents;

        DestroyImmediate(temp);

        Collider[] hits = Physics.OverlapBox(
            centro,
            halfExtents,
            Quaternion.identity,
            capasObstaculos,
            QueryTriggerInteraction.Ignore);

        return hits.Length == 0;
    }

    private void CrearPlanoDeAgua(GameObject bache)
    {
        if (Random.value > probabilidadAgua) return;

        Renderer rend = bache.GetComponentInChildren<Renderer>();
        if (rend == null) return;

        Vector3 boundsSize = rend.bounds.size;
        Vector3 bacheScale = bache.transform.lossyScale;

        float width = boundsSize.x / bacheScale.x;
        float depth = boundsSize.z / bacheScale.z;

        GameObject plano = GameObject.CreatePrimitive(PrimitiveType.Plane);
        DestroyImmediate(plano.GetComponent<MeshCollider>());

        plano.transform.SetParent(bache.transform);
        plano.transform.localPosition = Vector3.zero;
        plano.transform.localRotation = Quaternion.identity;
        plano.transform.localScale = new Vector3(width / 10f, 1, depth / 10f);

        float offsetY = Random.Range(-0.02f, -0.06f);
        plano.transform.position += Vector3.up * offsetY;

        if (waterMaterial != null)
            plano.GetComponent<Renderer>().material = waterMaterial;
    }

    private void ConfigurarFisica(GameObject bache)
    {
        var rb = bache.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = bache.AddComponent<Rigidbody>();
        }
        rb.useGravity = false;
        rb.isKinematic = true;
        rb.constraints = RigidbodyConstraints.FreezeAll;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
    }

    private bool EntradasValidas()
    {
        if (baches == null || baches.Count == 0)
        {
            Debug.LogWarning("La lista de prefabs de baches está vacía."); return false;
        }
        if (ladoArea <= 0f)
        {
            Debug.LogWarning("El lado del área debe ser mayor que cero.");  return false;
        }
        if (margenBorde * 2 >= ladoArea)
        {
            Debug.LogWarning("El margen al borde es demasiado grande.");     return false;
        }
        return true;
    }

    #endregion

    #region Gizmos (opcional) ──────────────────

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector3 size = new Vector3(ladoArea, 0.01f, ladoArea);
        Gizmos.DrawWireCube(transform.position + Vector3.up * alturaGeneracion, size);
    }
#endif

    #endregion
}
