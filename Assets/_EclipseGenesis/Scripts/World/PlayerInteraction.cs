using System;
using EclipseGenesis.Player;
using UnityEngine;

namespace EclipseGenesis.World
{
    public interface IInteractable
    {
        bool CanInteract { get; }
        string Prompt { get; }
        void Interact(GameObject user);
    }

    /// <summary>Looks for an IInteractable under the crosshair and triggers it with the Interact action (F).</summary>
    public sealed class PlayerInteraction : MonoBehaviour
    {
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private Transform viewPoint;
        [SerializeField] private float range = 2.6f;

        private static readonly RaycastHit[] Hits = new RaycastHit[8];

        public IInteractable Focused { get; private set; }

        private void Update()
        {
            Focused = FindFocus();
            if (Focused != null && Focused.CanInteract && input != null && input.InteractPressed) Focused.Interact(gameObject);
        }

        private IInteractable FindFocus()
        {
            if (viewPoint == null) return null;
            int n = Physics.RaycastNonAlloc(viewPoint.position, viewPoint.forward, Hits, range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            float best = float.MaxValue;
            IInteractable found = null;
            for (int i = 0; i < n; i++)
            {
                if (Hits[i].collider.transform.IsChildOf(transform) || Hits[i].distance >= best) continue;
                var it = Hits[i].collider.GetComponentInParent<IInteractable>();
                if (it == null && !Hits[i].collider.isTrigger) { best = Hits[i].distance; found = null; continue; }
                if (it != null) { best = Hits[i].distance; found = it; }
            }
            return found;
        }
    }

}
