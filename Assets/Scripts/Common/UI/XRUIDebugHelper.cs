using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SoloBandStudio.Common.UI
{
    /// <summary>
    /// Helps debug XR UI interaction issues.
    /// Attach to a UI Button to see why it's not being clicked.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class XRUIDebugHelper : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        private Button button;

        private void Awake()
        {
            button = GetComponent<Button>();
        }

        private void Start()
        {
            Debug.Log($"[XRUIDebug] Button '{gameObject.name}' initialized");
            Debug.Log($"[XRUIDebug] Button interactable: {button.interactable}");
            Debug.Log($"[XRUIDebug] Canvas: {GetComponentInParent<Canvas>()?.name}");
            Debug.Log($"[XRUIDebug] Canvas RenderMode: {GetComponentInParent<Canvas>()?.renderMode}");

            var raycaster = GetComponentInParent<GraphicRaycaster>();
            Debug.Log($"[XRUIDebug] Has GraphicRaycaster: {raycaster != null}");

            var eventSystem = FindFirstObjectByType<EventSystem>();
            Debug.Log($"[XRUIDebug] EventSystem exists: {eventSystem != null}");
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            Debug.Log($"[XRUIDebug] ✓ POINTER ENTER on '{gameObject.name}'");
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Debug.Log($"[XRUIDebug] POINTER EXIT from '{gameObject.name}'");
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            Debug.Log($"[XRUIDebug] ✓✓ POINTER DOWN on '{gameObject.name}'");
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Debug.Log($"[XRUIDebug] POINTER UP on '{gameObject.name}'");
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Debug.Log($"[XRUIDebug] ✓✓✓ POINTER CLICK on '{gameObject.name}' - SUCCESS!");
        }
    }
}
