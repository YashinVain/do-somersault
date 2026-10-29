using UnityEngine;

namespace JumpGame
{
    // A life buoy floating over the location with the dive number and its medals,
    // tied by a thin pole to the spot on the water where that dive lands.
    public class MapMarker : MonoBehaviour
    {
        [SerializeField] Transform face;
        [SerializeField] Renderer ring;
        [SerializeField] TextMesh number;
        [SerializeField] GameObject padlock;
        [SerializeField] Renderer[] medals;
        [SerializeField] Transform pole;

        public int Level { get; private set; }
        public Transform Face => face;

        public void Set(int level, int shownNumber, bool open, bool current, int stars,
                        Material ringOpen, Material ringCurrent, Material ringLocked, Material[] medalOn, Material medalOff)
        {
            Level = level;
            ring.sharedMaterial = !open ? ringLocked : current ? ringCurrent : ringOpen;
            number.text = shownNumber.ToString();
            number.gameObject.SetActive(open);
            padlock.SetActive(!open);
            for (int i = 0; i < medals.Length; i++)
            {
                medals[i].gameObject.SetActive(open);
                medals[i].sharedMaterial = i < stars ? medalOn[i] : medalOff;
            }
        }

        // Grows with distance so it reads the same on every map; the tap area grows with it.
        public void SetSize(float size)
        {
            face.localScale = Vector3.one * size;
            var hit = GetComponent<SphereCollider>();
            if (hit != null) hit.radius = 0.85f * size;
        }

        // Stretch the pole from just under the buoy to the given point on the water.
        public void PointTo(Vector3 water, float thickness)
        {
            Vector3 from = transform.position;
            Vector3 d = water - from;
            pole.position = from + d * 0.5f;
            pole.rotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
            pole.localScale = new Vector3(thickness, d.magnitude, thickness);
        }
    }
}
