using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Colócalo en el hijo que tenga el BoxCollider trigger.
/// Solo detecta qué entra y qué sale. RectangularPatrol lo lee.
/// </summary>
public class PersonObstacleSensor : MonoBehaviour
{
    [HideInInspector] public List<Transform> detected = new List<Transform>();

    private void OnTriggerEnter(Collider other)
    {
        if (!detected.Contains(other.transform))
            detected.Add(other.transform);
    }

    private void OnTriggerExit(Collider other)
    {
        detected.Remove(other.transform);
    }

    private void Update()
    {
        // Limpiar referencias nulas si algo fue destruido
        detected.RemoveAll(t => t == null);
    }
}
