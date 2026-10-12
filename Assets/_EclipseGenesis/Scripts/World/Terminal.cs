using System;
using EclipseGenesis.Player;
using UnityEngine;

namespace EclipseGenesis.World
{
    /// <summary>Emergency terminal: becomes usable when enabled by the mission and reports activation.</summary>
    public sealed class Terminal : MonoBehaviour, IInteractable
    {
        [SerializeField] private string prompt = "Activar terminal de emergencia";
        [SerializeField] private Renderer statusLight;
        [SerializeField] private Material lockedMaterial;
        [SerializeField] private Material readyMaterial;
        [SerializeField] private Material doneMaterial;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip activateClip;

        private bool _available;

        public bool Activated { get; private set; }
        public bool CanInteract => _available && !Activated;
        public string Prompt => prompt;
        public event Action<Terminal> OnActivated;

        public void SetAvailable(bool available)
        {
            _available = available;
            Refresh();
        }

        public void Interact(GameObject user)
        {
            if (!CanInteract) return;
            Activated = true;
            if (audioSource != null && activateClip != null) audioSource.PlayOneShot(activateClip);
            Refresh();
            OnActivated?.Invoke(this);
        }

        private void Awake() => Refresh();

        private void Refresh()
        {
            if (statusLight == null) return;
            var m = Activated ? doneMaterial : _available ? readyMaterial : lockedMaterial;
            if (m != null) statusLight.sharedMaterial = m;
        }
    }
}
