using UnityEngine;

[DisallowMultipleComponent]
public class CuboDesaparecer : MonoBehaviour
{
    [Tooltip("Capas donde se encuentran los baches (opcional)")]
    public LayerMask capasObjetivo = ~0;   // Por defecto, todas

    [Tooltip("Porcentaje del tamaño original para el chequeo (0 a 1)")]
    [Range(0.1f, 1f)]
    public float porcentajeTamanoChequeo = 0.95f;

    // Datos internos calculados a partir de la(s) malla(s)
    private Bounds _boundsChequeo;          // Centro y tamaño en WORLD SPACE

    private void Awake() => ActualizarBounds();

#if UNITY_EDITOR
    private void OnValidate() => ActualizarBounds();
#endif

    private void ActualizarBounds()
    {
        Renderer[] renders = GetComponentsInChildren<Renderer>(true);
        if (renders.Length == 0)
        {
            _boundsChequeo = new Bounds(transform.position, Vector3.one);
            return;
        }

        _boundsChequeo = renders[0].bounds;
        for (int i = 1; i < renders.Length; i++)
            _boundsChequeo.Encapsulate(renders[i].bounds);
    }

    private void Update()
    {
        Vector3 extentsReducidos = _boundsChequeo.extents * porcentajeTamanoChequeo;

        // Usamos OverlapBox para detectar objetos en el área
        Collider[] objetosDetectados = Physics.OverlapBox(
            _boundsChequeo.center,
            extentsReducidos,
            Quaternion.identity,
            capasObjetivo);

        foreach (Collider col in objetosDetectados)
        {
            if (col.gameObject == gameObject) continue;

            // Si el objeto detectado tiene "bache" en el nombre, destruye este objeto
            if (col.gameObject.name.ToLower().Contains("bache"))
            {
                Debug.Log($"Bache detectado: {col.gameObject.name}, destruyendo {gameObject.name}");
                Destroy(gameObject);
                return; // Salimos después de la primera detección
            }
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (_boundsChequeo.size == Vector3.zero)
            ActualizarBounds();

        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(_boundsChequeo.center, _boundsChequeo.size);

        Gizmos.color = Color.green;
        Vector3 sizeReducido = _boundsChequeo.size * porcentajeTamanoChequeo;
        Gizmos.DrawWireCube(_boundsChequeo.center, sizeReducido);
    }
#endif
}