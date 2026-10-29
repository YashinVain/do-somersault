using System;
using UnityEngine;

namespace JumpGame
{
    public class Diver : MonoBehaviour
    {
        public enum State { Ready, Crouching, Airborne, Underwater }

        [Header("Crouch and lean")]
        [Tooltip("Seconds of holding until the crouch is at full depth.")]
        [SerializeField] float fullChargeTime = 0.55f;
        [Tooltip("Constant forward push while leaning, deg/s².")]
        [SerializeField] float tipPush = 22f;
        [Tooltip("How strongly the lean feeds itself, like a falling pole.")]
        [SerializeField] float tipGain = 320f;
        [SerializeField] float fallAngle = 50f;
        [SerializeField] Vector3 toeOffset = new Vector3(0f, -0.9f, 0.12f);

        [Header("Takeoff")]
        [SerializeField] Vector2 upSpeed = new Vector2(2.6f, 5.2f);
        [SerializeField] Vector2 forwardSpeed = new Vector2(0.5f, 1.4f);
        [Tooltip("Extra forward speed when the jump happens at the edge of falling.")]
        [SerializeField] float leanForwardBonus = 3f;
        [SerializeField] Vector2 takeoffSpin = new Vector2(30f, 80f);

        [Header("Flight")]
        [SerializeField] float gravity = 9.81f;
        [SerializeField] float tuckSpin = 560f;
        [SerializeField] float openSpin = 20f;
        [SerializeField] float spinUpRate = 900f;
        [SerializeField] float spinDownRate = 3200f;
        [SerializeField] float tuckInTime = 0.12f;
        [SerializeField] float tuckOutTime = 0.09f;

        [Header("Body")]
        [SerializeField] float halfHeight = 0.9f;
        [SerializeField] float tuckedHalfHeight = 0.45f;

        [Header("Links")]
        [SerializeField] DiverPose pose;
        [SerializeField] LandingZone zone;
        [SerializeField] Splash splash;

        public State Current { get; private set; }
        public float Charge { get; private set; }
        public float Rotation { get; private set; }
        public bool Slipped { get; private set; }
        public float LeanDanger => Current == State.Crouching ? Mathf.Clamp01(lean / fallAngle) : 0f;
        public DiveResult LastResult { get; private set; }

        public event Action<DiveResult> Entered;
        public event Action Restarted;

        Vector3 startPosition;
        Quaternion startRotation;
        Vector3 velocity;
        float spin;
        float tuck;
        float lean, leanVel;
        float holdTime;
        bool wasHeld;
        bool initialized;

        void Awake() => Init();

        public void Init()
        {
            if (initialized) return;
            startPosition = transform.position;
            startRotation = transform.rotation;
            initialized = true;
        }

        void Update() => Step(Time.deltaTime, HoldInput.IsHeld);

        public void Step(float dt, bool held)
        {
            bool pressed = held && !wasHeld;
            bool released = !held && wasHeld;
            wasHeld = held;

            switch (Current)
            {
                case State.Ready:
                    if (pressed) BeginCrouch();
                    break;
                case State.Crouching:
                    UpdateCrouch(dt, released);
                    break;
                case State.Airborne:
                    UpdateFlight(dt, held && !Slipped);
                    break;
                case State.Underwater:
                    UpdateUnderwater(dt);
                    break;
            }
        }

        void BeginCrouch()
        {
            Current = State.Crouching;
            holdTime = lean = leanVel = 0f;
            Charge = 0f;
        }

        void UpdateCrouch(float dt, bool released)
        {
            holdTime += dt;
            float t = Mathf.Clamp01(holdTime / fullChargeTime);
            Charge = 1f - (1f - t) * (1f - t);
            pose.Crouched = Charge;

            // Tipping over the edge speeds up on its own, so a long hold ends in a fall.
            leanVel += (tipPush + tipGain * Mathf.Sin(lean * Mathf.Deg2Rad)) * dt;
            lean += leanVel * dt;
            Rotation = lean;
            PivotOnToes();

            if (lean >= fallAngle)
                Slip();
            else if (released)
                TakeOff();
        }

        void PivotOnToes()
        {
            Vector3 toe = startPosition + startRotation * toeOffset;
            Quaternion r = startRotation * Quaternion.Euler(Rotation, 0f, 0f);
            transform.SetPositionAndRotation(toe + r * -toeOffset, r);
        }

        void TakeOff()
        {
            float edge = lean / fallAngle;
            float forward = Mathf.Lerp(forwardSpeed.x, forwardSpeed.y, Charge) + leanForwardBonus * edge;
            float up = Mathf.Lerp(upSpeed.x, upSpeed.y, Charge) * (1f - 0.3f * edge);

            velocity = startRotation * Vector3.forward * forward + Vector3.up * up;
            spin = Mathf.Lerp(takeoffSpin.x, takeoffSpin.y, Charge) + leanVel;
            Launch();
        }

        void Slip()
        {
            velocity = startRotation * Vector3.forward * 0.7f;
            spin = Mathf.Max(leanVel, 120f);
            Slipped = true;
            pose.Flailing = true;
            Launch();
        }

        void Launch()
        {
            tuck = 0f;
            pose.Crouched = 0f;
            pose.Airborne = true;
            Current = State.Airborne;
        }

        void UpdateFlight(float dt, bool held)
        {
            float tuckRate = 1f / (held ? tuckInTime : tuckOutTime);
            tuck = Mathf.MoveTowards(tuck, held ? 1f : 0f, tuckRate * dt);

            if (!Slipped)
            {
                float targetSpin = Mathf.Lerp(openSpin, tuckSpin, tuck);
                float rate = targetSpin > spin ? spinUpRate : spinDownRate;
                spin = Mathf.MoveTowards(spin, targetSpin, rate * dt);
            }

            velocity += Vector3.down * gravity * dt;
            transform.position += velocity * dt;

            Rotation += spin * dt;
            ApplyRotation();
            pose.Tucked = tuck;
            pose.HeadFirst = IsHeadingDown();

            if (LowestPoint() <= zone.WaterLevel)
                EnterWater();
        }

        bool IsHeadingDown()
        {
            float n = Mathf.Repeat(Rotation, 360f);
            return n > 90f && n < 270f;
        }

        // Rough vertical extent of the body below the hip pivot.
        float LowestPoint()
        {
            float extent = Mathf.Lerp(halfHeight, tuckedHalfHeight, tuck);
            float tilt = Mathf.Abs(Mathf.Cos(Rotation * Mathf.Deg2Rad));
            return transform.position.y - extent * Mathf.Max(tilt, 0.3f);
        }

        void EnterWater()
        {
            Vector3 hit = transform.position;
            hit.y = zone.WaterLevel;

            bool inZone = zone.Contains(hit);
            LastResult = Slipped
                ? DiveScoring.Slip(inZone)
                : DiveScoring.Evaluate(Rotation, tuck, inZone, zone.InsideMultiplier);

            Current = State.Underwater;
            pose.Underwater = true;

            if (splash != null) splash.Play(hit, LastResult.entry);
            Entered?.Invoke(LastResult);
        }

        void UpdateUnderwater(float dt)
        {
            velocity = Vector3.Lerp(velocity, Vector3.down * 0.5f, 1f - Mathf.Exp(-5f * dt));
            transform.position += velocity * dt;
            spin = Mathf.MoveTowards(spin, 0f, 1200f * dt);
            Rotation += spin * dt;
            ApplyRotation();
        }

        void ApplyRotation()
        {
            transform.rotation = startRotation * Quaternion.Euler(Rotation, 0f, 0f);
        }

        public void ResetToStart()
        {
            transform.SetPositionAndRotation(startPosition, startRotation);
            velocity = Vector3.zero;
            spin = Rotation = tuck = Charge = holdTime = lean = leanVel = 0f;
            Slipped = false;
            pose.ResetPose();
            wasHeld = true; // ignore the tap that triggered the restart
            Current = State.Ready;
            Restarted?.Invoke();
        }
    }
}
