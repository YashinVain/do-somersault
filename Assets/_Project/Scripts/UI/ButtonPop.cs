using UnityEngine;
using UnityEngine.EventSystems;

namespace JumpGame
{
    // Buttons ease up a little under the pointer and sink when pressed, instead of snapping between states.
    public class ButtonPop : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        [SerializeField] float hover = 1.05f;
        [SerializeField] float pressed = 0.94f;
        [SerializeField] float speed = 16f;

        float goal = 1f;
        bool over, down;

        void OnEnable()
        {
            over = down = false;
            goal = 1f;
            transform.localScale = Vector3.one;
        }

        void Update()
        {
            float s = Mathf.Lerp(transform.localScale.x, goal, 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime));
            transform.localScale = new Vector3(s, s, 1f);
        }

        void Aim() => goal = down ? pressed : over ? hover : 1f;

        public void OnPointerEnter(PointerEventData e) { over = true; Aim(); Sounds.Play(Cue.Hover, 0.35f); }
        public void OnPointerClick(PointerEventData e)
        {
            var b = GetComponent<UnityEngine.UI.Button>();
            Sounds.Play(b != null && !b.interactable ? Cue.Error : Cue.Click);
        }
        public void OnPointerExit(PointerEventData e) { over = false; down = false; Aim(); }
        public void OnPointerDown(PointerEventData e) { down = true; Aim(); }
        public void OnPointerUp(PointerEventData e) { down = false; Aim(); }
    }
}
