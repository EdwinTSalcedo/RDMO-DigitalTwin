using UnityEngine;
using System.Collections.Generic;

public class ToggleActiveExclusive : MonoBehaviour
{
    public List<GameObject> groupA;
    public List<GameObject> groupB;

    public bool energia; // false = grupo A activo, true = grupo B activo

    // Referencia al controlador de energía
    public EnergyController energyController;

    public void Initialize()
    {
        if (energia)
            ActivateGroupB();
        else
            ActivateGroupA();
    }

    public void ActivateGroupA()
    {
        foreach (GameObject obj in groupA)
            ActivateRecursive(obj);

        foreach (GameObject obj in groupB)
            obj.SetActive(false);

        energia = false;

        // Notificar a EnergyController
        if (energyController != null)
            energyController.energiaActivo = energia;
    }

    public void ActivateGroupB()
    {
        foreach (GameObject obj in groupB)
             ActivateRecursive(obj);

        foreach (GameObject obj in groupA)
            obj.SetActive(false);

        energia = true;

        // Notificar a EnergyController
        if (energyController != null)
            energyController.energiaActivo = energia;
    }

    public List<GameObject> exceptions; // Objetos que NO deben activarse, pero sus hijos sí (si se recorren)

    private void ActivateRecursive(GameObject obj)
    {
        if (obj != null)
        {
            // Solo activar si NO está en la lista de excepciones
            if (exceptions == null || !exceptions.Contains(obj))
            {
                obj.SetActive(true);
            }

            foreach (Transform child in obj.transform)
            {
                ActivateRecursive(child.gameObject);
            }
        }
    }
}
