using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[ExecuteAlways]
public class desac: MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        // Desactivar todos los GameObjects de la escena recursivamente, excepto el objeto que contiene este script
        DesactivarRecursivamente(SceneManager.GetActiveScene().GetRootGameObjects());
    }

    // Función recursiva para desactivar GameObject y sus hijos
    private void DesactivarRecursivamente(GameObject[] rootObjects)
    {
        foreach (var rootObject in rootObjects)
        {
            if (rootObject != gameObject) // Saltar el objeto que contiene este script
            {
                rootObject.SetActive(false);
                DesactivarRecursivamente(GetChildren(rootObject.transform));
            }
        }
    }

    // Función para obtener todos los hijos de un transform
    private GameObject[] GetChildren(Transform parent)
    {
        List<GameObject> children = new List<GameObject>();
        foreach (Transform child in parent)
        {
            children.Add(child.gameObject);
        }
        return children.ToArray();
    }

    // Update is called once per frame
    void Update()
    {
        // No es necesario hacer nada en el Update para esta funcionalidad
    }
}
