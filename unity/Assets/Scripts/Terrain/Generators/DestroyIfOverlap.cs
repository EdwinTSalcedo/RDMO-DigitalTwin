using UnityEngine;
using System.Collections.Generic;
public class DestroyIfOverlap : MonoBehaviour
{
    public string[] targetTags = { "Car", "Person" };

    private bool destroyed = false;
    private List<GameObject> othersToDestroy = new List<GameObject>();

    private void OnTriggerEnter(Collider other)
    {
        TryDestroy(other);
    }

    private void OnTriggerStay(Collider other)
    {
        // Esto cubre el caso de “atravesados”
        TryDestroy(other);
    }

    private void TryDestroy(Collider other)
    {
        if (!enabled || destroyed) return;

        // Evitar borrar el terreno o el suelo
        if (other.gameObject.layer == LayerMask.NameToLayer("Terrain") || 
            other.GetComponent<Terrain>() != null ||
            other.name.ToLower().Contains("terrain") ||
            other.name.ToLower().Contains("ground"))
        {
            return;
        }

        bool isTarget = false;
        foreach (string tagName in targetTags)
        {
            if (other.CompareTag(tagName))
            {
                isTarget = true;
                break;
            }
        }

        if (isTarget)
        {
            // Si es Person o Car (targetTags), este objeto (el auto u otro) se borra INMEDIATAMENTE
            destroyed = true;
            Debug.Log($"[DestroyIfTouch] {gameObject.name} se destruyó a sí mismo por chocar con {other.name} ({other.tag})");
            Destroy(gameObject);
        }
        else
        {
            // Si es otro objeto, lo agregamos a la lista, pero NO lo borramos todavía
            if (!othersToDestroy.Contains(other.gameObject))
            {
                othersToDestroy.Add(other.gameObject);
            }
        }
    }

    private void OnDisable()
    {
        // Si ya nos destruimos (chocamos con Person o Car), no hacemos nada
        if (destroyed) return;

        // Si el script se deshabilita (terminó la evaluación) y NO fuimos destruidos, 
        // significa que nos vamos a quedar. AHORA sí tenemos potestad de borrar lo que tocamos.
        foreach (var other in othersToDestroy)
        {
            if (other != null)
            {
                Debug.Log($"[DestroyIfTouch] {gameObject.name} (que se quedó) destruyó a {other.name}");
                Destroy(other);
            }
        }
        othersToDestroy.Clear();
    }
}