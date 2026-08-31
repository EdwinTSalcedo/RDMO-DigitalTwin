using UnityEngine;

public class CanvasElementPositionToggle : MonoBehaviour
{
    [Tooltip("Elementos que se muestran en el estado por defecto")]
    public GameObject[] defaultElements;

    [Tooltip("Elementos que se muestran en el estado alternativo")]
    public GameObject[] alternateElements;

    private bool isAlternate = false;

    void Start()
    {
        // Inicializar los estados al comenzar
        ApplyState(!isAlternate, defaultElements);
        ApplyState(isAlternate, alternateElements);
    }

    public void ToggleElements()
    {
        isAlternate = !isAlternate;

        // Aplicar el nuevo estado
        ApplyState(!isAlternate, defaultElements);
        ApplyState(isAlternate, alternateElements);
    }

    private void ApplyState(bool shouldBeActive, GameObject[] elements)
    {
        if (elements == null) return;

        foreach (GameObject element in elements)
        {
            // Solo modificar elementos que no son null
            if (element != null)
            {
                element.SetActive(shouldBeActive);
            }
            // Los elementos null se ignoran (no se activa/desactiva nada para ellos)
        }
    }
    public void VerElements()
    {
        ApplyState(!isAlternate, defaultElements);
        ApplyState(isAlternate, alternateElements);
    }
}