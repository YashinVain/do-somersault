using System;
using UnityEngine;

namespace JumpGame
{
    public class Diver : MonoBehaviour
    {
        public enum State { Ready, Crouching, Airborne, Underwater }

        [Header("Takeoff")]
        [SerializeField] float chargePeriod = 1.1f;
        [SerializeField] Vector2 upSpeed = new Vector2(4f, 9f);
        [SerializeField] Vector2 forwardSpeed = new Vector2(1.5f, 4f);
        [SerializeField] float takeoffSpin = 90f;

        [Header("Flight")]
        [SerializeField] float gravity = 20f;
        [SerializeField] float tuckSpin = 720f;
        [SerializeField] float openSpin = 70f;
        [SerializeField] float spinUpRate = 1400f;
        [SerializeField] float spinDownRate = 2400f;
        [SerializeField] float tuckSpeed = 10f;

        [Header("Body")]
        [SerializeField] float halfHeight = 0.9f;
        [SerializeField] float tuckedHalfHeight = 0.45f;

        [Header("Links")]
        [SerializeField] DiverPose pose;
        [SerializeField] WaterTarget target;
        [SerializeField] Splash splash;

        public State Current { get; private set; }
        public float Charge { get; private set; }
        public float Rotation { get; private set; }
        public DiveResult LastResult { get; private set; }

        public event Action<DiveResult> Entered;

        Vector3 startPosition;
        Quaternion startRotation;
        Vector3 velocity;
        float spin;
        float tuck;
        float chargeTime;
        bool wasHeld;

        void Awake()
        {
            startPosition = transform.position;
            startRotation = transform.rotation;
        }

        void Update()
        {
            bool held = HoldInput.IsHeld;
            bool pressed = held && !wasHeld;
            bool released = !held && wasHeld;
            wasHeld = held;

            switch (Current)
            {
                case State.Ready:
                    if (pressed) BeginCrouch();
                    break;
                case State.Crouching:
                    UpdateCrouch(released);
                    break;
                case State.Airborne:
                    UpdateFlight(held);
                    break;
                case State.Underwater:
                    UpdateUnderwater();
                    break;
            }
        }

        void BeginCrouch()
        {
            Current = State.Crouching;
            chargeTime = 0f;
        }

        void UpdateCrouch(bool released)
        {
            chargeTime += Time.deltaTime;
            // Ping-pong so that holding longer is not automatically better.
            Charge = Mathf.PingPong(chargeTime * 2f / chargePeriod, 1f);
            pose.Crouched = Mathf.Clamp01(chargeTime * 6f);

            if (released) TakeOff();
        }

        void TakeOff()
        {
            velocity = transform.forward * Mathf.Lerp(forwardSpeed.x, forwardSpeed.y, Charge)
                     + Vector3.up * Mathf.Lerp(upSpeed.x, upSpeed.y, Charge);
            spin = takeoffSpin * Charge;
            Rotation = 0f;
            tuck = 0f;
            pose.Crouched = 0f;
            pose.Airborne = true;
            Current = State.Airborne;
        }

        void UpdateFlight(bool held)
        {
            float dt = Time.deltaTime;

            tuck = Mathf.MoveTowards(tuck, held ? 1f : 0f, tuckSpeed * dt);
            float targetSpin = Mathf.Lerp(openSpin, tuckSpin, tuck);
            float rate = targetSpin > spin ? spinUpRate : spinDownRate;
            spin = Mathf.MoveTowards(spin, targetSpin, rate * dt);

            velocity += Vector3.down * gravity * dt;
            transform.position += velocity * dt;

            Rotation += spin * dt;
            transform.rotation = startRotation * Quaternion.Euler(Rotation, 0f, 0f);
            pose.Tucked = tuck;

            if (LowestPoint() <= target.WaterLevel)
                EnterWater();
        }

        // Rough vertical extent of the body around the hip pivot.
        float LowestPoint()
        {
            float extent = Mathf.Lerp(halfHeight, tuckedHalfHeight, tuck);
            float tilt = Mathf.Abs(Mathf.Cos(Rotation * Mathf.Deg2Rad));
            return transform.position.y - extent * Mathf.Max(tilt, 0.35f);
        }

        void EnterWater()
        {
            Vector3 hit = transform.position;
            hit.y = target.WaterLevel;

            LastResult = DiveScoring.Evaluate(Rotation, tuck, target.MultiplierAt(hit));
            Current = State.Underwater;

            if (splash != null) splash.Play(hit, LastResult.entry);
            Entered?.Invoke(LastResult);
        }

        void UpdateUnderwater()
        {
            float dt = Time.deltaTime;
            velocity = Vector3.Lerp(velocity, Vector3.down * 0.6f, 1f - Mathf.Exp(-4f * dt));
            transform.position += velocity * dt;
            spin = Mathf.MoveTowards(spin, 0f, 1500f * dt);
            Rotation += spin * dt;
            transform.rotation = startRotation * Quaternion.Euler(Rotation, 0f, 0f);
        }

        public void ResetToStart()
        {
            transform.SetPositionAndRotation(startPosition, startRotation);
            velocity = Vector3.zero;
            spin = Rotation = tuck = Charge = 0f;
            pose.Airborne = false;
            pose.Crouched = pose.Tucked = 0f;
            pose.Snap();
            wasHeld = HoldInput.IsHeld;
            Current = State.Ready;
        }
    }
}
