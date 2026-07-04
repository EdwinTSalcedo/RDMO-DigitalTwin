using UnityEngine;

public class Minimapa : MonoBehaviour
{
    public RectTransform objetoUI;
    public GameObject objetoEspecifico; // Referencia al GameObject específico que quieres activar/desactivar

    public Vector2 anchorMinA;
    public Vector2 anchorMaxA;

    public Vector2 anchorMinB;
    public Vector2 anchorMaxB;

    private bool enPosicionA = true;

    void Update()
    {
        AplicarAnchors();
    }

    public void AlternarAnchors()
    {
        enPosicionA = !enPosicionA;
    }

    void AplicarAnchors()
    {
        if (objetoUI == null) return;

        if (enPosicionA)
        {
            objetoUI.anchorMin = anchorMinA;
            objetoUI.anchorMax = anchorMaxA;
            SetHijosActivos(false); // Desactiva hijos en posición A
            
            // Desactivar el objeto específico en posición A
            if (objetoEspecifico != null)
                objetoEspecifico.SetActive(false);
        }
        else
        {
            objetoUI.anchorMin = anchorMinB;
            objetoUI.anchorMax = anchorMaxB;
            SetHijosActivos(true); // Activa hijos en posición B
            
            // Activar el objeto específico en posición B
            if (objetoEspecifico != null)
                objetoEspecifico.SetActive(true);
        }

        objetoUI.offsetMin = Vector2.zero;
        objetoUI.offsetMax = Vector2.zero;
    }

    void SetHijosActivos(bool activo)
    {
        for (int i = 0; i < objetoUI.childCount; i++)
        {
            objetoUI.GetChild(i).gameObject.SetActive(activo);
        }
    }
}