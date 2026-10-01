using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PoeClone.UI
{
    /// <summary>
    /// Forwards one uGUI element's press/drag/release to code, for the touch controls. Deliberately
    /// not a Button: pressing a Selectable selects it, and any selected UI object counts as
    /// "typing" to the player scripts (PlayerController.IsUiFocused), which would freeze movement.
    /// Each finger is its own pointer to the EventSystem, so the joystick and the attack button
    /// can be held at the same time.
    /// </summary>
    public class TouchPointerRelay : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler, IInitializePotentialDragHandler
    {
        public event Action<PointerEventData> Down;
        public event Action<PointerEventData> Dragged;
        public event Action<PointerEventData> Up;

        // A joystick must follow the finger from the first pixel, not after the drag threshold.
        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            eventData.useDragThreshold = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            Down?.Invoke(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            Dragged?.Invoke(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Up?.Invoke(eventData);
        }
    }
}
