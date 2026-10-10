using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VehicleSystem
{
    /// <summary>
    /// A UI button that reports "Held" while a finger / mouse is down on it.
    /// Idle colour = the Image's colour in the scene, so you restyle it just by editing the Image.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class TouchHoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [Tooltip("Image that changes colour while pressed. Found automatically if empty.")]
        public Image targetImage;
        [Tooltip("Colour of the Image while the button is held. The idle colour is the Image's own colour.")]
        public Color heldColor = new Color(1f, 1f, 1f, 0.6f);

        public bool Held { get; private set; }

        Color idleColor = Color.white;
        bool ready;

        void Awake() => Setup();

        void Setup()
        {
            if (ready) return;
            if (targetImage == null) targetImage = GetComponent<Image>();
            if (targetImage != null) idleColor = targetImage.color;
            ready = true;
        }

        /// <summary>Used by MobileControls when it builds the UI at runtime.</summary>
        public void Init(Image img, Color idle, Color held)
        {
            targetImage = img;
            idleColor = idle;
            heldColor = held;
            ready = true;
        }

        public void OnPointerDown(PointerEventData e) => Set(true);
        public void OnPointerUp(PointerEventData e) => Set(false);
        public void OnPointerExit(PointerEventData e) => Set(false);
        void OnDisable() => Set(false);

        void Set(bool value)
        {
            Setup();
            Held = value;
            if (targetImage != null) targetImage.color = value ? heldColor : idleColor;
        }
    }
}
