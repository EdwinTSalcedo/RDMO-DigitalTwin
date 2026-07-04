using UnityEngine;
using System.Collections;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Generador robusto de prefabs evitando intersecciones reales
/// usando BoxColliders + sistema de probabilidades.
/// </summary>
[ExecuteInEditMode]
public class PrefabObjectGenerator : MonoBehaviour
{
    // ─────────────────────────────────────────────
    // CLASE DE PREFAB PONDERADO
    // ─────────────────────────────────────────────

    [System.Serializable]
    public class WeightedPrefab
    {
        public GameObject prefab;

        [Range(0f, 100f)]
        [Tooltip("Probabilidad relativa de aparición")]
        public float porcentaje = 20f;

        [Tooltip("Separación extra individual")]
        public float extraSpacing = 0f;

        [Tooltip("Prioridad de aparición. Grandes primero.")]
        public int prioridad = 0;
    }

    // ─────────────────────────────────────────────
    // PREFABS
    // ─────────────────────────────────────────────

    [Header("Prefabs")]
    public List<WeightedPrefab> prefabs = new();

    // ─────────────────────────────────────────────
    // CONFIGURACIÓN
    // ─────────────────────────────────────────────

    [Header("Configuración")]
    public int cantidad = 20;

    public int seed = 42;

    public bool randomizeSeedOnStart = false;

    public bool autoUpdate = false;

    // ─────────────────────────────────────────────
    // ÁREA
    // ─────────────────────────────────────────────

    [Header("Área")]
    public float ladoArea = 20f;

    public float margenBorde = 1f;

    [Range(0f, 1f)]
    [Tooltip("0 = uniforme | 1 = muy centrado")]
    public float concentracionCentro = 0.65f;

    // ─────────────────────────────────────────────
    // RESTRICCIONES
    // ─────────────────────────────────────────────

    [Header("Restricciones")]
    public float margenExtra = 0.15f;

    public float alturaY = 0f;

    public bool randomRotacionY = true;

    [Tooltip("Intentos máximos por objeto")]
    public int intentosPorObjeto = 400;

    [Tooltip("Distancia mínima global entre objetos")]
    public float distanciaMinimaGlobal = 0.25f;

    // ─────────────────────────────────────────────
    // INTERNO
    // ─────────────────────────────────────────────

    private class PlacedObject
    {
        public Bounds bounds;
        public Vector3 position;
    }

    private readonly List<PlacedObject> placedObjects = new();

    public bool IsGenerating { get; private set; } = false;

    // ─────────────────────────────────────────────
    // UNITY
    // ─────────────────────────────────────────────

    private void Start()
    {
        if (!Application.isPlaying) return;

        if (randomizeSeedOnStart)
            seed = (int)(System.DateTime.Now.Ticks & 0x7FFFFFFF);

        StartCoroutine(GenerateRoutine());
    }

    private void OnDisable()
    {
        ClearChildren();
    }

    private void OnValidate()
    {
#if UNITY_EDITOR
        if (!autoUpdate) return;

        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        if (EditorApplication.isCompiling)
            return;

        EditorApplication.delayCall -= DelayedGenerate;
        EditorApplication.delayCall += DelayedGenerate;
#endif
    }

#if UNITY_EDITOR
    private void DelayedGenerate()
    {
        if (this == null) return;

        Generate();
    }
#endif

    // ─────────────────────────────────────────────
    // GENERACIÓN
    // ─────────────────────────────────────────────

    [ContextMenu("Generar")]
    public void Generate()
    {
        if (Application.isPlaying)
        {
            StopAllCoroutines();
            StartCoroutine(GenerateRoutine());
        }
        else
        {
            GenerateSync();
        }
    }

    private void GenerateSync()
    {
        IsGenerating = true;

        ClearChildren();
        placedObjects.Clear();

        if (prefabs == null || prefabs.Count == 0)
        {
            IsGenerating = false;
            return;
        }

        Random.InitState(seed);

        int generated = 0;

        float usableHalf =
            Mathf.Max(0f, ladoArea * 0.5f - margenBorde);

        int maxAttempts =
            cantidad * intentosPorObjeto;

        int attempts = 0;
        int failedAttemptsInARow = 0;

        while (generated < cantidad && attempts < maxAttempts && failedAttemptsInARow < 10)
        {
            attempts++;

            WeightedPrefab selected =
                GetRandomWeightedPrefab();

            if (selected == null || selected.prefab == null)
            {
                failedAttemptsInARow++;
                continue;
            }

            GameObject prefab = selected.prefab;

            Vector2 posXZ =
                GetCenteredRandomPosition(usableHalf);

            Vector3 spawnPos =
                new Vector3(
                    transform.position.x + posXZ.x,
                    transform.position.y + alturaY,
                    transform.position.z + posXZ.y
                );

            Quaternion rotation =
                randomRotacionY
                ? Quaternion.Euler(0f, Random.Range(0f, 360f), 0f)
                : Quaternion.identity;

            Bounds candidateBounds =
                CalculatePrefabBounds(
                    prefab,
                    spawnPos,
                    rotation,
                    selected.extraSpacing
                );

            if (IntersectsAny(candidateBounds))
            {
                failedAttemptsInARow++;
                continue;
            }

            if (!HasMinimumDistance(spawnPos))
            {
                failedAttemptsInARow++;
                continue;
            }

#if UNITY_EDITOR
            GameObject instance =
                (GameObject)PrefabUtility.InstantiatePrefab(
                    prefab,
                    transform
                );

            instance.transform.SetPositionAndRotation(
                spawnPos,
                rotation
            );
#else
            GameObject instance =
                Instantiate(
                    prefab,
                    spawnPos,
                    rotation,
                    transform
                );
#endif

            instance.name = $"{prefab.name}_{generated}";

            placedObjects.Add(new PlacedObject
            {
                bounds = candidateBounds,
                position = spawnPos
            });

            generated++;
            failedAttemptsInARow = 0;  // Reiniciar contador al lograr generar
        }

        if (failedAttemptsInARow >= 10)
            Debug.Log($"Detenida generación: 10 intentos fallidos. Generados: {generated}/{cantidad}");
        else
            Debug.Log($"Generados: {generated}/{cantidad}");

        IsGenerating = false;
    }

    public IEnumerator GenerateRoutine()
    {
        IsGenerating = true;

        try
        {
            ClearChildren();
            placedObjects.Clear();

            if (prefabs == null || prefabs.Count == 0)
            {
                Debug.LogWarning("No hay prefabs.");
                yield break;
            }

            Random.InitState(seed);

            int generated = 0;

            float usableHalf =
                Mathf.Max(0f, ladoArea * 0.5f - margenBorde);

            int maxAttempts =
                cantidad * intentosPorObjeto;

            int attempts = 0;
            int failedAttemptsInARow = 0;

            while (generated < cantidad && attempts < maxAttempts && failedAttemptsInARow < 10)
            {
                attempts++;

                WeightedPrefab selected =
                    GetRandomWeightedPrefab();

                if (selected == null || selected.prefab == null)
                {
                    failedAttemptsInARow++;
                    continue;
                }

                GameObject prefab = selected.prefab;

                Vector2 posXZ =
                    GetCenteredRandomPosition(usableHalf);

                Vector3 spawnPos =
                    new Vector3(
                        transform.position.x + posXZ.x,
                        transform.position.y + alturaY,
                        transform.position.z + posXZ.y
                    );

                Quaternion rotation =
                    randomRotacionY
                    ? Quaternion.Euler(0f, Random.Range(0f, 360f), 0f)
                    : Quaternion.identity;

                Bounds candidateBounds =
                    CalculatePrefabBounds(
                        prefab,
                        spawnPos,
                        rotation,
                        selected.extraSpacing
                    );

                if (IntersectsAny(candidateBounds))
                {
                    failedAttemptsInARow++;
                    continue;
                }

                if (!HasMinimumDistance(spawnPos))
                {
                    failedAttemptsInARow++;
                    continue;
                }

                GameObject instance =
                    Instantiate(
                        prefab,
                        spawnPos,
                        rotation,
                        transform
                    );

                instance.name = $"{prefab.name}_{generated}";

                var overlapScript =
                    instance.GetComponent<DestroyIfOverlap>();

                if (overlapScript != null)
                    overlapScript.enabled = true;

                yield return new WaitForSeconds(0.05f);

                if (instance != null)
                {
                    if (overlapScript != null)
                        overlapScript.enabled = false;

                    placedObjects.Add(new PlacedObject
                    {
                        bounds = candidateBounds,
                        position = spawnPos
                    });

                    generated++;
                    failedAttemptsInARow = 0;  // Reiniciar contador al lograr generar
                }
                else
                {
                    failedAttemptsInARow++;
                }
            }

            if (failedAttemptsInARow >= 5)
                Debug.Log($"Detenida generación: 10 intentos fallidos. Generados: {generated}/{cantidad}");
            else
                Debug.Log($"Generados: {generated}/{cantidad}");
        }
        finally
        {
            IsGenerating = false;
        }
    }

    // ─────────────────────────────────────────────
    // SELECCIÓN PONDERADA
    // ─────────────────────────────────────────────

    private WeightedPrefab GetRandomWeightedPrefab()
    {
        float total = 0f;

        foreach (var p in prefabs)
        {
            if (p.prefab != null)
                total += Mathf.Max(0f, p.porcentaje);
        }

        if (total <= 0f)
            return null;

        float randomValue =
            Random.Range(0f, total);

        float current = 0f;

        foreach (var p in prefabs)
        {
            if (p.prefab == null)
                continue;

            current += Mathf.Max(0f, p.porcentaje);

            if (randomValue <= current)
                return p;
        }

        return prefabs[0];
    }

    // ─────────────────────────────────────────────
    // POSICIÓN CENTRADA
    // ─────────────────────────────────────────────

    private Vector2 GetCenteredRandomPosition(
        float usableHalf)
    {
        float power =
            Mathf.Lerp(1f, 3.5f, concentracionCentro);

        float tx = Mathf.Pow(Random.value, power);
        float tz = Mathf.Pow(Random.value, power);

        float sx = Random.value > 0.5f ? 1f : -1f;
        float sz = Random.value > 0.5f ? 1f : -1f;

        float x = tx * usableHalf * sx;
        float z = tz * usableHalf * sz;

        return new Vector2(x, z);
    }

    // ─────────────────────────────────────────────
    // DISTANCIA MÍNIMA
    // ─────────────────────────────────────────────

    private bool HasMinimumDistance(Vector3 position)
    {
        foreach (var p in placedObjects)
        {
            float dist =
                Vector3.Distance(position, p.position);

            if (dist < distanciaMinimaGlobal)
                return false;
        }

        return true;
    }

    // ─────────────────────────────────────────────
    // INTERSECCIONES
    // ─────────────────────────────────────────────

    private bool IntersectsAny(Bounds candidate)
    {
        foreach (var p in placedObjects)
        {
            if (candidate.Intersects(p.bounds))
                return true;
        }

        return false;
    }

    // ─────────────────────────────────────────────
    // CALCULAR BOUNDS
    // ─────────────────────────────────────────────

    private Bounds CalculatePrefabBounds(
        GameObject prefab,
        Vector3 position,
        Quaternion rotation,
        float extraSpacing)
    {
        BoxCollider[] colliders =
            prefab.GetComponentsInChildren<BoxCollider>(true);

        if (colliders.Length == 0)
        {
            Bounds fallback =
                new Bounds(position, Vector3.one);

            fallback.Expand(
                margenExtra + extraSpacing
            );

            return fallback;
        }

        bool initialized = false;

        Bounds combined = new Bounds();

        foreach (BoxCollider box in colliders)
        {
            Transform t = box.transform;

            Vector3 scale = t.lossyScale;

            Vector3 scaledCenter =
                Vector3.Scale(box.center, scale);

            Vector3 worldCenter =
                position +
                rotation * (t.localPosition + scaledCenter);

            Vector3 scaledSize =
                Vector3.Scale(box.size, scale);

            Matrix4x4 matrix =
                Matrix4x4.TRS(
                    worldCenter,
                    rotation * t.localRotation,
                    Vector3.one
                );

            Vector3 ext = scaledSize * 0.5f;

            Vector3[] corners = new Vector3[8]
            {
                matrix.MultiplyPoint3x4(new Vector3(-ext.x,-ext.y,-ext.z)),
                matrix.MultiplyPoint3x4(new Vector3(ext.x,-ext.y,-ext.z)),
                matrix.MultiplyPoint3x4(new Vector3(-ext.x,-ext.y,ext.z)),
                matrix.MultiplyPoint3x4(new Vector3(ext.x,-ext.y,ext.z)),
                matrix.MultiplyPoint3x4(new Vector3(-ext.x,ext.y,-ext.z)),
                matrix.MultiplyPoint3x4(new Vector3(ext.x,ext.y,-ext.z)),
                matrix.MultiplyPoint3x4(new Vector3(-ext.x,ext.y,ext.z)),
                matrix.MultiplyPoint3x4(new Vector3(ext.x,ext.y,ext.z))
            };

            Bounds colliderBounds =
                new Bounds(corners[0], Vector3.zero);

            for (int i = 1; i < corners.Length; i++)
            {
                colliderBounds.Encapsulate(corners[i]);
            }

            if (!initialized)
            {
                combined = colliderBounds;
                initialized = true;
            }
            else
            {
                combined.Encapsulate(colliderBounds);
            }
        }

        combined.Expand(
            margenExtra + extraSpacing
        );

        return combined;
    }

    // ─────────────────────────────────────────────
    // LIMPIAR
    // ─────────────────────────────────────────────

    [ContextMenu("Limpiar")]
    public void ClearChildren()
    {
        List<GameObject> children = new();

        foreach (Transform child in transform)
        {
            if (child != null)
                children.Add(child.gameObject);
        }

        foreach (GameObject go in children)
        {
            if (Application.isPlaying)
                Destroy(go);
            else
                DestroyImmediate(go);
        }

        placedObjects.Clear();
    }

    // ─────────────────────────────────────────────
    // GIZMOS
    // ─────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        Gizmos.color =
            new Color(0f, 0.8f, 1f, 0.15f);

        Gizmos.DrawCube(
            transform.position,
            new Vector3(ladoArea, 0.05f, ladoArea)
        );

        Gizmos.color = Color.cyan;

        Gizmos.DrawWireCube(
            transform.position,
            new Vector3(ladoArea, 0.05f, ladoArea)
        );

        Gizmos.color =
            new Color(1f, 0.4f, 0f, 0.75f);

        foreach (var p in placedObjects)
        {
            Gizmos.DrawWireCube(
                p.bounds.center,
                p.bounds.size
            );
        }
    }
}