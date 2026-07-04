using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
public class VirtualJoystick : MonoBehaviour, IDragHandler, IPointerUpHandler, IPointerDownHandler
{
    private RectTransform bgImg;
    private RectTransform joystickKnob;
    public Vector3 InputDirection { get; private set; }
    
    // Config
    public float handleRange = 1f;

    private void Start()
    {
        bgImg = GetComponent<RectTransform>();
        // Assume the first child is the Knob
        if (transform.childCount > 0)
            joystickKnob = transform.GetChild(0).GetComponent<RectTransform>();
    }

    public void OnDrag(PointerEventData ped)
    {
        Vector2 pos;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(bgImg, ped.position, ped.pressEventCamera, out pos))
        {
            pos.x = (pos.x / bgImg.sizeDelta.x);
            pos.y = (pos.y / bgImg.sizeDelta.y);

            InputDirection = new Vector3(pos.x * 2, pos.y * 2, 0);
            InputDirection = (InputDirection.magnitude > 1) ? InputDirection.normalized : InputDirection;

            if (joystickKnob != null) 
            {
                joystickKnob.anchoredPosition = new Vector3(InputDirection.x * (bgImg.sizeDelta.x / 3) * handleRange, 
                                                             InputDirection.y * (bgImg.sizeDelta.y / 3) * handleRange);
            }
        }
    }

    public void OnPointerDown(PointerEventData ped)
    {
        OnDrag(ped);
    }

    public void OnPointerUp(PointerEventData ped)
    {
        InputDirection = Vector3.zero;
        if (joystickKnob != null) joystickKnob.anchoredPosition = Vector3.zero;
    }
}
