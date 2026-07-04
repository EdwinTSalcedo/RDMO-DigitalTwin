using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIButtonColorToggle : MonoBehaviour
{
    public Button targetButton;       // Asigne el botón UI en el inspector
    public TMP_Text tmpText;          // Asigne el texto TMP en el inspector (opcional)
    public Text uiText;               // O asigne el texto UI tradicional (opcional)

    private Color originalButtonColor;
    private Color originalTextColor;
    private bool isRed = false;

    void Start()
    {
        if (targetButton == null)
        {
            Debug.LogError("Debe asignar el botón en el inspector.");
            return;
        }

        originalButtonColor = targetButton.image.color;

        if (tmpText != null)
            originalTextColor = tmpText.color;
        else if (uiText != null)
            originalTextColor = uiText.color;
    }

    public void ToggleColor()
    {
        if (targetButton == null)
            return;

        if (!isRed)
        {
            targetButton.image.color = Color.red;

            if (tmpText != null)
                tmpText.color = Color.white;
            else if (uiText != null)
                uiText.color = Color.white;

            isRed = true;
        }
        else
        {
            targetButton.image.color = originalButtonColor;

            if (tmpText != null)
                tmpText.color = originalTextColor;
            else if (uiText != null)
                uiText.color = originalTextColor;

            isRed = false;
        }
    }
}
