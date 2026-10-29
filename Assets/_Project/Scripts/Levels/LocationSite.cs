using UnityEngine;

namespace JumpGame
{
    // One place to dive from: its scenery, its tower and the light and air that go with it.
    public class LocationSite : MonoBehaviour
    {
        [SerializeField] DivingTower tower;

        [Header("Sun")]
        [SerializeField] Vector3 sunAngles = new Vector3(44f, 160f, 0f);
        [SerializeField] Color sunColor = Color.white;
        [SerializeField] float sunIntensity = 1f;

        [Header("Ambient")]
        [SerializeField] Color ambientSky = Color.gray;
        [SerializeField] Color ambientEquator = Color.gray;
        [SerializeField] Color ambientGround = Color.gray;

        [Header("Fog")]
        [SerializeField] Color fogColor = Color.white;
        [SerializeField] float fogStart = 30f;
        [SerializeField] float fogEnd = 120f;

        [Header("Sky")]
        [SerializeField] Color skyTint = Color.gray;
        [SerializeField] Color skyGround = Color.gray;
        [SerializeField] float skyExposure = 1f;
        [SerializeField] float atmosphere = 0.8f;

        [Header("Water")]
        [Tooltip("Height of the swell, metres. 0 for still water.")]
        [SerializeField] float swell;
        [SerializeField] float swellPeriod = 4f;
        [SerializeField] float swellLength = 14f;

        [Header("Level map view")]
        [SerializeField] Vector3 mapCenter = new Vector3(0f, 3f, 3f);
        [SerializeField] float mapDistance = 26f;
        [Tooltip("Direction the map camera looks in.")]
        [SerializeField] Vector3 mapDirection = new Vector3(-0.45f, -0.75f, -1f);

        public DivingTower Tower => tower;
        public float MapDistance => mapDistance;

        // Where the map camera sits, swung sideways by yaw degrees around the location's centre.
        public void MapPose(float yaw, out Vector3 position, out Vector3 lookAt)
        {
            lookAt = transform.TransformPoint(mapCenter);
            Vector3 dir = Quaternion.AngleAxis(yaw, Vector3.up) * mapDirection.normalized;
            position = lookAt - dir * mapDistance;
        }

        public void Apply(Light sun, Material sky) => Blend(this, this, 1f, sun, sky);

        // Light, air and water somewhere between two locations, for flying from one to the other.
        public static void Blend(LocationSite a, LocationSite b, float t, Light sun, Material sky)
        {
            WaterMotion.Set(b.swell, b.swellPeriod, b.swellLength);

            sun.transform.rotation = Quaternion.Slerp(Quaternion.Euler(a.sunAngles), Quaternion.Euler(b.sunAngles), t);
            sun.color = Color.Lerp(a.sunColor, b.sunColor, t);
            sun.intensity = Mathf.Lerp(a.sunIntensity, b.sunIntensity, t);

            RenderSettings.ambientSkyColor = Color.Lerp(a.ambientSky, b.ambientSky, t);
            RenderSettings.ambientEquatorColor = Color.Lerp(a.ambientEquator, b.ambientEquator, t);
            RenderSettings.ambientGroundColor = Color.Lerp(a.ambientGround, b.ambientGround, t);
            RenderSettings.fogColor = Color.Lerp(a.fogColor, b.fogColor, t);
            RenderSettings.fogStartDistance = Mathf.Lerp(a.fogStart, b.fogStart, t);
            RenderSettings.fogEndDistance = Mathf.Lerp(a.fogEnd, b.fogEnd, t);

            sky.SetColor("_SkyTint", Color.Lerp(a.skyTint, b.skyTint, t));
            sky.SetColor("_GroundColor", Color.Lerp(a.skyGround, b.skyGround, t));
            sky.SetFloat("_Exposure", Mathf.Lerp(a.skyExposure, b.skyExposure, t));
            sky.SetFloat("_AtmosphereThickness", Mathf.Lerp(a.atmosphere, b.atmosphere, t));
        }
    }
}
