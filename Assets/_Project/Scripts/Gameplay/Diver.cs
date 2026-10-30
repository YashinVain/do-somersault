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

        [Header("Takeoff")]
        [SerializeField] Vector2 upSpeed = new Vector2(2.6f, 5.2f);
        [SerializeField] Vector2 forwardSpeed = new Vector2(0.5f, 1.4f);
        [Tooltip("Extra forward speed when the jump happens at the edge of falling.")]
        [SerializeField] float leanForwardBonus = 3f;
        [Tooltip("Somersault speed given by the legs, deg/s, for a weak and a full push.")]
        [SerializeField] Vector2 takeoffSpin = new Vector2(70f, 130f);

        [Header("Quick tap")]
        [Tooltip("A press shorter than this, in seconds, pushes off hard instead of crouching.")]
        [SerializeField] float tapTime = 0.22f;
        [Tooltip("Speeds of the push, m/s: forward and up.")]
        [SerializeField] Vector2 pushSpeed = new Vector2(3.6f, 3.4f);
        [Tooltip("Somersault speed given by a push, deg/s.")]
        [SerializeField] float pushSpin = 45f;

        [Header("Stranded")]
        [Tooltip("Seconds to get off a platform after landing on it, before the dive counts as failed.")]
        [SerializeField] float strandedTime = 3f;
        [Tooltip("Speeds of the hop that gets the diver off, m/s: forward and up.")]
        [SerializeField] Vector2 hopSpeed = new Vector2(3f, 4f);

        [Header("Flight")]
        [SerializeField] float gravity = 9.81f;
        [Tooltip("Seconds to fold into a full tuck and to open from it.")]
        [SerializeField] float tuckInTime = 0.16f;
        [SerializeField] float tuckOutTime = 0.12f;
        [Tooltip("After a slip the body keeps curling slowly from the crouch while the button is held.")]
        [SerializeField] float slipCurlTime = 0.7f;

        [Header("Contact with the tower")]
        [SerializeField] float restitution = 0.2f;
        [SerializeField] float friction = 0.6f;
        [Tooltip("Below this speed while touching the tower the dive counts as stuck on the platform.")]
        [SerializeField] float restSpeed = 0.6f;
        [SerializeField] float restTime = 0.8f;
        [Tooltip("Velocity change, m/s, that counts as a real knock rather than the toes brushing the edge.")]
        [SerializeField] float knockImpulse = 0.8f;
        [Tooltip("Fastest the body is pushed back out of the tower, m/s. Keeps any overlap from snapping.")]
        [SerializeField] float pushOutSpeed = 2.5f;

        [Header("Links")]
        [SerializeField] DiverPose pose;
        [SerializeField] LandingZone zone;
        [SerializeField] DivingTower tower;
        [SerializeField] Splash splash;

        public State Current { get; private set; }
        public float Charge { get; private set; }
        public float Rotation { get; private set; }
        public float Spin { get; private set; }
        public bool Slipped { get; private set; }
        public float LeanDanger => Current == State.Crouching ? Mathf.Clamp01(lean / fallAngle) : 0f;
        public DiveResult LastResult { get; private set; }
        public Vector3 EntryPoint { get; private set; }

        public bool UsedTuck { get; private set; }
        // Lying on a platform, a deck or a hull instead of falling into the water: the player has a few seconds to hop off.
        public bool Stranded { get; private set; }
        public float StrandedLeft { get; private set; }
        // The character's own twist on gravity, spin, splash and scoring; set by GameFlow.
        public Perk Perk { get; set; } = Perk.None;
        // A robot that short-circuited in the water.
        public bool Broken { get; private set; }
        // Off while menus are open.
        public bool AcceptInput { get; set; } = true;

        public event Action<DiveResult> Entered;
        public event Action Restarted;
        // The first press on the platform, and the moment the legs push off.
        public event Action Crouched;
        public event Action TookOff;
        // Number of full somersaults done so far in this dive.
        public event Action<int> TurnCompleted;
        // A real knock against the tower, with its strength in m/s.
        public event Action<float> Knocked;

        int turnsAnnounced;

        Vector3 startPosition;
        Quaternion startRotation;

        // Flight is integrated for the centre of mass; the body is placed around it.
        Vector3 com, prevCom, velocity;
        Vector3 toePivot;
        float angularMomentum;
        float straightInertia = 1f;
        float tuck;
        float lean, leanVel;
        float holdTime;
        bool hitBuoys;
        bool hitTower, stuck;
        float restTimer;
        float freeTime;
        bool pressedNow;
        float faultTime, faultSparkTimer;
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

        void Update() => Step(Time.deltaTime, AcceptInput && HoldInput.IsHeld);

        public void Step(float dt, bool held)
        {
            if (dt <= 0f) return;

            bool pressed = held && !wasHeld;
            pressedNow = pressed;
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
            Crouched?.Invoke();
            holdTime = lean = leanVel = 0f;
            Charge = 0f;
            toePivot = transform.TransformPoint(pose.LocalToes());
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
            leanVel += (tipPush + tipGain * Mathf.Sin(lean * Mathf.Deg2Rad)) * Perk.edge * dt;
            lean += leanVel * dt;
            Rotation = lean;
            PivotOnToes();

            Vector3 c = WorldCom();
            Vector3 comVelocity = (c - prevCom) / dt;
            prevCom = c;

            if (lean >= fallAngle)
                Slip(comVelocity, held);
            else if (released)
            {
                // A quick tap is a strong push straight off the edge; holding is the careful, charged jump.
                if (holdTime < tapTime) Push();
                else TakeOff();
            }
        }

        // The real toe tips stay where they first stood, whatever the crouch does to the legs.
        void PivotOnToes()
        {
            Quaternion r = startRotation * Quaternion.Euler(Rotation, 0f, 0f);
            transform.SetPositionAndRotation(toePivot - r * pose.LocalToes(), r);
        }

        void TakeOff()
        {
            float edge = lean / fallAngle;
            float forward = Mathf.Lerp(forwardSpeed.x, forwardSpeed.y, Charge) + leanForwardBonus * edge;
            float up = Mathf.Lerp(upSpeed.x, upSpeed.y, Charge) * (1f - 0.3f * edge);

            forward *= Perk.jump;
            up *= Perk.jump;
            velocity = startRotation * Vector3.forward * forward + Vector3.up * up;
            // takeoffSpin is what a straight body would turn at.
            float spin = (Mathf.Lerp(takeoffSpin.x, takeoffSpin.y, Charge) + leanVel) * Perk.spin;

            tuck = 0f;
            pose.JustLaunched = true;
            Launch(spin * straightInertia);
            TookOff?.Invoke();
        }

        // A hop off whatever the diver landed on, towards the water.
        void Hop()
        {
            velocity = startRotation * Vector3.forward * (hopSpeed.x * Perk.jump) + Vector3.up * (hopSpeed.y * Perk.jump);
            angularMomentum = 0f;
            restTimer = 0f;
            Stranded = false;
            TookOff?.Invoke();
        }

        // The legs kick off hard while the body is still upright: well away from the platform, with a gentle turn.
        void Push()
        {
            velocity = startRotation * Vector3.forward * (pushSpeed.x * Perk.jump) + Vector3.up * (pushSpeed.y * Perk.jump);
            tuck = 0f;
            pose.JustLaunched = true;
            Launch(pushSpin * Perk.spin * straightInertia);
            TookOff?.Invoke();
        }

        // Held too long: the body simply keeps rotating over the toes and drops, in whatever shape it is in.
        void Slip(Vector3 comVelocity, bool held)
        {
            velocity = comVelocity;
            Slipped = true;
            tuck = 0f;
            pose.Slipping = held;
            Launch(leanVel * pose.Inertia(pose.LocalCenterOfMass()));
        }

        void Launch(float momentum)
        {
            com = WorldCom();
            angularMomentum = momentum;
            pose.Crouched = 0f;
            pose.Airborne = true;
            pose.Tucked = tuck;
            hitBuoys = hitTower = stuck = false;
            Stranded = false;
            freeTime = 0f;
            UsedTuck = false;
            turnsAnnounced = 0;
            restTimer = 0f;
            Current = State.Airborne;
        }

        void UpdateFlight(float dt, bool held)
        {
            // Releasing after a slip is the player's call to open up; until then the crouch just curls in.
            if (!held) pose.Slipping = false;
            float time = !held ? tuckOutTime : pose.Slipping ? slipCurlTime : tuckInTime;
            tuck = Mathf.MoveTowards(tuck, held ? 1f : 0f, dt / time);
            if (tuck > 0.5f) UsedTuck = true;

            pose.Tucked = tuck * tuck * (3f - 2f * tuck);
            pose.HeadFirst = IsHeadingDown();
            pose.Step(dt);

            // Angular momentum is conserved: folding up spins faster, opening slows down.
            Vector3 localCom = pose.LocalCenterOfMass();
            Spin = angularMomentum / pose.Inertia(localCom);
            Rotation += Spin * dt;

            int turns = Mathf.FloorToInt(Rotation / 360f);
            if (turns > turnsAnnounced && !Slipped)
            {
                turnsAnnounced = turns;
                TurnCompleted?.Invoke(turns);
            }

            velocity += Vector3.down * gravity * Perk.gravity * dt;
            com += velocity * dt;
            PlaceAroundCom(localCom);

            CollideWithTower(dt, localCom);
            TouchBuoys();

            if (Stranded)
            {
                StrandedLeft -= dt;
                if (pressedNow) Hop();
                else if (StrandedLeft <= 0f) stuck = true;
            }

            if (stuck)
                StopOnTower();
            else if (pose.LowestPoint() <= zone.WaterLevel)
                EnterWater();
        }

        // Body points against the tower boxes: push out, then an impulse with friction that changes both
        // the flight and the spin, as a knock on the board would.
        void CollideWithTower(float dt, Vector3 localCom)
        {
            if (tower == null) return;

            pose.ContactPoints(contacts);
            float inertia = pose.Inertia(localCom);
            float omega = Spin * Mathf.Deg2Rad;
            // The somersault turns round the platform's own sideways axis, whichever way the platform faces.
            Vector3 axis = startRotation * Vector3.right;
            bool touching = false;

            foreach (var p in contacts)
            {
                if (!tower.Penetration(p, out var n, out float depth)) continue;
                touching = true;
                com += n * Mathf.Min(depth, pushOutSpeed * dt);

                // Velocity of this point: the centre of mass plus the spin around the somersault (x) axis.
                Vector3 r = p - com;
                Vector3 arm = Vector3.Cross(axis, r);
                Vector3 vp = velocity + arm * omega;
                float vn = Vector3.Dot(vp, n);
                if (vn >= 0f) continue;

                float an = Vector3.Dot(arm, n);
                float j = -(1f + restitution) * vn / (1f + an * an / inertia);
                velocity += n * j;
                omega += j * an / inertia;

                Vector3 vt = vp - n * vn;
                float speed = vt.magnitude;
                if (speed > 0.001f)
                {
                    Vector3 t = vt / speed;
                    float at = Vector3.Dot(arm, t);
                    float jt = Mathf.Max(-speed / (1f + at * at / inertia), -friction * j);
                    velocity += t * jt;
                    omega += jt * at / inertia;
                }

                if (j > knockImpulse)
                {
                    hitTower = true;
                    Knocked?.Invoke(j);
                }
            }

            if (!touching)
            {
                restTimer = 0f;
                // Off the surface for a moment: no longer stranded.
                freeTime += dt;
                if (Stranded && freeTime > 0.25f) Stranded = false;
                return;
            }

            angularMomentum = omega * Mathf.Rad2Deg * inertia;
            PlaceAroundCom(localCom);

            freeTime = 0f;
            restTimer = velocity.magnitude < restSpeed ? restTimer + dt : 0f;
            if (restTimer > restTime && !Stranded)
            {
                Stranded = true;
                StrandedLeft = strandedTime;
            }
        }

        void StopOnTower()
        {
            LastResult = DiveScoring.Fail(EntryKind.OnTower, false);
            EntryPoint = com;
            Current = State.Underwater;
            Entered?.Invoke(LastResult);
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
            EntryPoint = hit;

            bool inZone = zone.Contains(hit);
            DiveResult result;
            if (Slipped) result = DiveScoring.Fail(EntryKind.Slipped, inZone);
            else if (hitTower) result = DiveScoring.Fail(EntryKind.HitTower, inZone);
            else result = DiveScoring.Evaluate(Rotation, tuck, inZone, zone.InCenter(hit), hitBuoys, zone.InsideMultiplier);
            result.usedTuck = UsedTuck;
            DiveScoring.ApplyPerk(ref result, Perk);
            LastResult = result;
            Broken = Perk.breaks;
            faultTime = faultSparkTimer = 0f;

            Current = State.Underwater;
            pose.Underwater = true;

            float size = Splash.SizeFor(LastResult.entry) * Perk.splash;
            if (splash != null) splash.Play(hit, size);
            zone.MakeWave(hit, size);
            Entered?.Invoke(LastResult);
        }

        void UpdateUnderwater(float dt)
        {
            pose.Step(dt);
            if (stuck) return;

            // A short-circuited robot sputters for a moment and then hangs almost still.
            Vector3 sinkTo = Vector3.down * (Broken ? 0.12f : 0.5f);
            velocity = Vector3.Lerp(velocity, sinkTo, 1f - Mathf.Exp(-(Broken ? 9f : 5f) * dt));
            if (Broken) SparkFault(dt);
            com += velocity * dt;
            angularMomentum = Mathf.MoveTowards(angularMomentum, 0f, Mathf.Abs(angularMomentum) * 4f * dt + 0.01f * dt);

            Vector3 localCom = pose.LocalCenterOfMass();
            Spin = angularMomentum / pose.Inertia(localCom);
            Rotation += Spin * dt;
            PlaceAroundCom(localCom);
            TouchBuoys();
        }

        void SparkFault(float dt)
        {
            faultTime += dt;
            faultSparkTimer -= dt;
            if (faultTime > 1.8f || faultSparkTimer > 0f || splash == null) return;
            faultSparkTimer = 0.1f;
            splash.Sparks(EntryPoint + new Vector3(UnityEngine.Random.Range(-0.35f, 0.35f), 0.05f, UnityEngine.Random.Range(-0.35f, 0.35f)));
        }

        public void SetTower(DivingTower t) => tower = t;

        public void SetStart(Vector3 position, Quaternion rotation)
        {
            startPosition = position;
            startRotation = rotation;
            ResetToStart();
        }

        public void ResetToStart()
        {
            transform.SetPositionAndRotation(startPosition, startRotation);
            velocity = Vector3.zero;
            angularMomentum = Spin = Rotation = tuck = Charge = holdTime = lean = leanVel = 0f;
            Slipped = hitBuoys = hitTower = stuck = Broken = Stranded = false;
            restTimer = 0f;
            pose.ResetPose();
            wasHeld = true; // ignore the tap that triggered the restart
            Current = State.Ready;
            Restarted?.Invoke();
        }
    }
}
