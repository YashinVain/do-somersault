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
        [SerializeField] Vector3 toeOffset = new Vector3(0f, -0.9f, 0.14f);

        [Header("Takeoff")]
        [SerializeField] Vector2 upSpeed = new Vector2(2.6f, 5.2f);
        [SerializeField] Vector2 forwardSpeed = new Vector2(0.5f, 1.4f);
        [Tooltip("Extra forward speed when the jump happens at the edge of falling.")]
        [SerializeField] float leanForwardBonus = 3f;
        [Tooltip("Somersault speed given by the legs, deg/s, for a weak and a full push.")]
        [SerializeField] Vector2 takeoffSpin = new Vector2(70f, 130f);

        [Header("Flight")]
        [SerializeField] float gravity = 9.81f;

        [Header("Links")]
        [SerializeField] DiverPose pose;
        [SerializeField] LandingZone zone;
        [SerializeField] Splash splash;

        public State Current { get; private set; }
        public float Charge { get; private set; }
        public float Rotation { get; private set; }
        public float Spin { get; private set; }
        public bool Slipped { get; private set; }
        public float LeanDanger => Current == State.Crouching ? Mathf.Clamp01(lean / fallAngle) : 0f;
        public DiveResult LastResult { get; private set; }

        public event Action<DiveResult> Entered;
        public event Action Restarted;

        Vector3 startPosition;
        Quaternion startRotation;

        // Flight is integrated for the centre of mass; the body is placed around it.
        Vector3 com, prevCom, velocity;
        float angularMomentum;
        float straightInertia = 1f;
        float tuck;
        float lean, leanVel;
        float holdTime;
        bool hitBuoys;
        bool wasHeld;
        bool initialized;
        readonly Vector3[] contacts = new Vector3[DiverPose.ContactCount];

        void Awake() => Init();

        public void Init()
        {
            if (initialized) return;
            startPosition = transform.position;
            startRotation = transform.rotation;
            pose.ResetPose();
            straightInertia = pose.Inertia(pose.LocalCenterOfMass());
            initialized = true;
        }

        void Update() => Step(Time.deltaTime, HoldInput.IsHeld);

        public void Step(float dt, bool held)
        {
            if (dt <= 0f) return;

            bool pressed = held && !wasHeld;
            bool released = !held && wasHeld;
            wasHeld = held;

            switch (Current)
            {
                case State.Ready:
                    pose.Step(dt);
                    if (pressed) BeginCrouch();
                    break;
                case State.Crouching:
                    UpdateCrouch(dt, held, released);
                    break;
                case State.Airborne:
                    UpdateFlight(dt, held);
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
            prevCom = WorldCom();
        }

        void UpdateCrouch(float dt, bool held, bool released)
        {
            holdTime += dt;
            float t = Mathf.Clamp01(holdTime / fullChargeTime);
            Charge = 1f - (1f - t) * (1f - t);
            pose.Crouched = Charge;
            pose.Step(dt);

            // Tipping over the edge speeds up on its own, so a long hold ends in a fall.
            leanVel += (tipPush + tipGain * Mathf.Sin(lean * Mathf.Deg2Rad)) * dt;
            lean += leanVel * dt;
            Rotation = lean;
            PivotOnToes();

            Vector3 c = WorldCom();
            Vector3 comVelocity = (c - prevCom) / dt;
            prevCom = c;

            if (lean >= fallAngle)
                Slip(comVelocity, held);
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
            // takeoffSpin is what a straight body would turn at.
            float spin = Mathf.Lerp(takeoffSpin.x, takeoffSpin.y, Charge) + leanVel;

            tuck = 0f;
            pose.JustLaunched = true;
            Launch(spin * straightInertia);
        }

        // Held too long: the body simply keeps rotating over the toes and drops, in whatever shape it is in.
        void Slip(Vector3 comVelocity, bool held)
        {
            velocity = comVelocity;
            Slipped = true;
            tuck = held ? 1f : 0f;
            Launch(leanVel * pose.Inertia(pose.LocalCenterOfMass()));
        }

        void Launch(float momentum)
        {
            com = WorldCom();
            angularMomentum = momentum;
            pose.Crouched = 0f;
            pose.Airborne = true;
            pose.Tucked = tuck;
            hitBuoys = false;
            Current = State.Airborne;
        }

        void UpdateFlight(float dt, bool held)
        {
            tuck = held ? 1f : 0f;
            pose.Tucked = tuck;
            pose.HeadFirst = IsHeadingDown();
            pose.Step(dt);

            // Angular momentum is conserved: folding up spins faster, opening slows down.
            Vector3 localCom = pose.LocalCenterOfMass();
            Spin = angularMomentum / pose.Inertia(localCom);
            Rotation += Spin * dt;

            velocity += Vector3.down * gravity * dt;
            com += velocity * dt;
            PlaceAroundCom(localCom);

            TouchBuoys();

            if (pose.LowestPoint() <= zone.WaterLevel)
                EnterWater();
        }

        void PlaceAroundCom(Vector3 localCom)
        {
            Quaternion r = startRotation * Quaternion.Euler(Rotation, 0f, 0f);
            transform.SetPositionAndRotation(com - r * localCom, r);
        }

        Vector3 WorldCom() => transform.TransformPoint(pose.LocalCenterOfMass());

        void TouchBuoys()
        {
            pose.ContactPoints(contacts);
            if (zone.Push(contacts, velocity)) hitBuoys = true;
        }

        bool IsHeadingDown()
        {
            float n = Mathf.Repeat(Rotation, 360f);
            return n > 90f && n < 270f;
        }

        void EnterWater()
        {
            Vector3 hit = com;
            hit.y = zone.WaterLevel;

            bool inZone = zone.Contains(hit);
            LastResult = Slipped
                ? DiveScoring.Slip(inZone)
                : DiveScoring.Evaluate(Rotation, pose.Tucked, inZone, hitBuoys, zone.InsideMultiplier);

            Current = State.Underwater;
            pose.Underwater = true;

            float size = Splash.SizeFor(LastResult.entry);
            if (splash != null) splash.Play(hit, size);
            zone.MakeWave(hit, size);
            Entered?.Invoke(LastResult);
        }

        void UpdateUnderwater(float dt)
        {
            pose.Step(dt);
            velocity = Vector3.Lerp(velocity, Vector3.down * 0.5f, 1f - Mathf.Exp(-5f * dt));
            com += velocity * dt;
            angularMomentum = Mathf.MoveTowards(angularMomentum, 0f, Mathf.Abs(angularMomentum) * 4f * dt + 0.01f * dt);

            Vector3 localCom = pose.LocalCenterOfMass();
            Spin = angularMomentum / pose.Inertia(localCom);
            Rotation += Spin * dt;
            PlaceAroundCom(localCom);
            TouchBuoys();
        }

        public void ResetToStart()
        {
            transform.SetPositionAndRotation(startPosition, startRotation);
            velocity = Vector3.zero;
            angularMomentum = Spin = Rotation = tuck = Charge = holdTime = lean = leanVel = 0f;
            Slipped = hitBuoys = false;
            pose.ResetPose();
            wasHeld = true; // ignore the tap that triggered the restart
            Current = State.Ready;
            Restarted?.Invoke();
        }
    }
}
