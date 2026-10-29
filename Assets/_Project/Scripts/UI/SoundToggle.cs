using UnityEngine;
using UnityEngine.UI;

namespace JumpGame
{
    // The sound button: switches every sound on or off and remembers the choice.
    public class SoundToggle : MonoBehaviour
    {
        [SerializeField] Button button;
        [SerializeField] Text label;

        void Start()
        {
            button.onClick.AddListener(() =>
            {
                Sounds.Muted = !Sounds.Muted;
                Refresh();
            });
            Refresh();
        }

        void OnEnable() => Refresh();

        void Refresh()
        {
            if (label != null) label.text = Sounds.Muted ? "Звук: выкл" : "Звук: вкл";
        }
    }
}
