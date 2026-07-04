using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(VerticalLayoutGroup))]
public class UIButtonScaler : MonoBehaviour
{
    [Range(0f, 1f)]
    public float widthScreenPercent = 0.3f;   // Ejemplo: 30% del ancho de la pantalla
    [Range(0f, 1f)]
    public float heightScreenPercent = 0.1f;  // Ejemplo: 10% del alto de la pantalla

    private VerticalLayoutGroup layoutGroup;
    private RectTransform rectTransform;

    void Awake()
    {
        layoutGroup = GetComponent<VerticalLayoutGroup>();
        rectTransform = GetComponent<RectTransform>();

        // Pivote arriba para que el contenedor crezca hacia abajo
        rectTransform.pivot = new Vector2(0.5f, 1f);

        layoutGroup.childControlHeight = false;
        layoutGroup.childControlWidth = false;
        layoutGroup.childForceExpandHeight = false;
        layoutGroup.childForceExpandWidth = false;
    }

    void LateUpdate()
    {
        int childCount = transform.childCount;

        float screenWidth = Screen.width;
        float screenHeight = Screen.height;

        float buttonWidth = screenWidth * widthScreenPercent;
        float buttonHeight = screenHeight * heightScreenPercent;

        float totalHeight = layoutGroup.padding.top + layoutGroup.padding.bottom;
        
        for (int i = 0; i < childCount; i++)
        {
            RectTransform rt = transform.GetChild(i) as RectTransform;
            if (rt != null)
            {
                rt.sizeDelta = new Vector2(buttonWidth, buttonHeight);
                totalHeight += buttonHeight;
            }
        }

        totalHeight += layoutGroup.spacing * Mathf.Max(0, childCount - 1);

        rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, totalHeight);
    }
}
