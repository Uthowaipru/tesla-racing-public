using UnityEngine;
using UnityEngine.EventSystems;

namespace VehicleSystem
{
    /// <summary>
    /// Put on the preview picture (RawImage). Dragging it with a finger or the mouse turns the 3D vehicle by hand;
    /// the automatic spin resumes a moment after you let go. VehicleSelectMenu wires the stage in for you.
    /// </summary>
    public class VehiclePreviewDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public VehiclePreviewStage stage;

        public void OnBeginDrag(PointerEventData e) { if (stage != null) stage.BeginDrag(); }
        public void OnDrag(PointerEventData e) { if (stage != null) stage.AddDrag(e.delta.x); }
        public void OnEndDrag(PointerEventData e) { if (stage != null) stage.EndDrag(); }
    }
}
