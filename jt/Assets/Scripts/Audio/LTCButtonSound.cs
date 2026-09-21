using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LTC.Audio
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class LTCButtonSound : MonoBehaviour, IPointerDownHandler, ISubmitHandler
    {
        Button button;
        void Awake() { button = GetComponent<Button>(); }
        void Play()
        {
            if (button && button.IsActive() && button.IsInteractable() && LTCGameAudio.Instance)
                LTCGameAudio.Instance.PlayTap();
        }
        // Feedback on press survives scene changes and gameplay RemoveAllListeners calls.
        public void OnPointerDown(PointerEventData data)
        {
            if (data.button == PointerEventData.InputButton.Left) Play();
        }
        public void OnSubmit(BaseEventData data) { Play(); }
    }
}
