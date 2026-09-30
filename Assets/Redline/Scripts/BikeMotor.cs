using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Redline
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class BikeMotor : MonoBehaviour
    {
        const float RoadClearance = 0.08f;
        const float WheelRadius = 0.35f;
        public static readonly List<BikeMotor> All = new List<BikeMotor>();

        public string RiderName { get; private set; }
        public bool IsPlayer { get; private set; }
        public float Progress { get; private set; }
        public float LaneOffset { get; private set; }
        public float SpeedKph => speed * 3.6f;
        public float Health { get; private set; } = 100f;
        public float Integrity { get; private set; } = 100f;
        public float Boost { get; private set; } = 1f;
        public bool IsWrecked { get; private set; }
        public bool Finished { get; set; }
        public Transform LeftArm { get; private set; }
        public Transform RightArm { get; private set; }
        public Transform VisualRoot => visualRoot;
        public Color RiderColor { get; private set; }
        public bool AutomationMode { get; private set; }
        public float WheelTravelDegrees { get; private set; }
        public Vector3 FrontWheelAxleWorld => frontWheelSpin != null ? frontWheelSpin.TransformDirection(Vector3.right) : transform.right;

        Rigidbody body;
        Transform visualRoot;
        Transform frontSteerPivot;
        Transform frontWheelSpin;
        Transform rearWheelSpin;
        Transform handlebar;
        Light headlight;
        TrailRenderer boostTrail;
        float speed;
        float throttle;
        float brake;
        float steer;
        bool boostHeld;
        float aiLaneTarget;
        bool hasAILane;
        float stun;
        float impactSlow = 1f;
        float recoverCooldown;
        float visualLean;
        Vector3 lastSafePosition;
        Quaternion lastSafeRotation;

        public static BikeMotor Create(string riderName, Color color, bool player, float startDistance, float laneOffset)
        {
            GameObject root = new GameObject(player ? "Player Motorcycle" : "Rival " + riderName);
            BikeMotor motor = root.AddComponent<BikeMotor>();
            motor.RiderName = riderName;
            motor.RiderColor = color;
            motor.IsPlayer = player;
            motor.Progress = startDistance;
            motor.LaneOffset = laneOffset;
            motor.aiLaneTarget = laneOffset;

            RoadSample start = RoadSpline.I.Sample(startDistance);
            root.transform.SetPositionAndRotation(start.position + start.right * laneOffset + start.up * RoadClearance, Quaternion.LookRotation(start.forward, start.up));

            Rigidbody rb = root.GetComponent<Rigidbody>();
            rb.mass = 280f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = player ? CollisionDetectionMode.ContinuousDynamic : CollisionDetectionMode.Continuous;
            rb.linearDamping = 0.12f;
            rb.angularDamping = 7f;
            rb.useGravity = false;
            rb.centerOfMass = new Vector3(0f, 0.15f, 0f);
            motor.body = rb;

            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.58f, 0f);
            collider.size = new Vector3(1.15f, 1.35f, 2.55f);

            motor.BuildVisual();
            root.AddComponent<RiderCombat>().Initialize(motor);
            if (!player) root.AddComponent<RaceAI>().Initialize(motor);
            RedlineAudio.AttachEngine(root, motor);
            return motor;
        }

        void Awake()
        {
            All.Add(this);
        }

        void OnDestroy()
        {
            All.Remove(this);
        }

        void BuildVisual()
        {
            visualRoot = new GameObject("Authored Bike and Rider").transform;
            visualRoot.SetParent(transform, false);
            Material paint = RedlineMaterials.Get("bike " + RiderName, RiderColor, 0.48f, 0.48f);
            Material dark = RedlineMaterials.Get("bike rubber", new Color(0.035f, 0.035f, 0.032f), 0f, 0.18f);
            Material metal = RedlineMaterials.Get("bike metal", new Color(0.34f, 0.34f, 0.32f), 0.8f, 0.58f);
            Material brakeMetal = RedlineMaterials.Get("brake disc", new Color(0.55f, 0.53f, 0.48f), 0.92f, 0.68f);
            Material leather = RedlineMaterials.Get("bike leather", new Color(0.095f, 0.06f, 0.045f), 0f, 0.18f);
            Material skin = RedlineMaterials.Get("rider skin", new Color(0.5f, 0.28f, 0.17f), 0f, 0.24f);
            Material helmet = RedlineMaterials.Get("helmet " + RiderName, Color.Lerp(RiderColor, Color.white, 0.18f), 0.35f, 0.62f);

            rearWheelSpin = CreateWheelAssembly("Rear Wheel", visualRoot, new Vector3(0f, 0.34f, -0.93f), dark, metal, brakeMetal);
            CreateWheelFender("Rear Fender", visualRoot, new Vector3(0f, 0.34f, -0.93f), paint);
            frontSteerPivot = new GameObject("Front Steering Pivot").transform;
            frontSteerPivot.SetParent(visualRoot, false);
            frontSteerPivot.localPosition = new Vector3(0f, 0.34f, 1.06f);
            frontWheelSpin = CreateWheelAssembly("Front Wheel", frontSteerPivot, Vector3.zero, dark, metal, brakeMetal);
            CreateWheelFender("Front Fender", frontSteerPivot, Vector3.zero, paint);

            CreatePart("Lower Frame", PrimitiveType.Cube, new Vector3(0f, 0.63f, -0.02f), new Vector3(0.24f, 0.18f, 1.42f), Quaternion.Euler(-8f, 0f, 0f), metal);
            CreatePart("Left Swingarm", PrimitiveType.Cube, new Vector3(-0.23f, 0.49f, -0.55f), new Vector3(0.1f, 0.1f, 0.9f), Quaternion.Euler(-13f, 0f, 0f), metal);
            CreatePart("Right Swingarm", PrimitiveType.Cube, new Vector3(0.23f, 0.49f, -0.55f), new Vector3(0.1f, 0.1f, 0.9f), Quaternion.Euler(-13f, 0f, 0f), metal);
            CreatePart("Fuel Tank", PrimitiveType.Sphere, new Vector3(0f, 1.03f, 0.28f), new Vector3(0.72f, 0.5f, 0.88f), Quaternion.identity, paint);
            CreatePart("Engine Block", PrimitiveType.Cube, new Vector3(0f, 0.7f, 0.02f), new Vector3(0.58f, 0.48f, 0.62f), Quaternion.Euler(0f, 0f, 0f), metal);
            CreatePart("Left Side Panel", PrimitiveType.Cube, new Vector3(-0.34f, 0.88f, -0.08f), new Vector3(0.1f, 0.44f, 0.72f), Quaternion.Euler(-8f, 0f, 5f), paint);
            CreatePart("Right Side Panel", PrimitiveType.Cube, new Vector3(0.34f, 0.88f, -0.08f), new Vector3(0.1f, 0.44f, 0.72f), Quaternion.Euler(-8f, 0f, -5f), paint);
            CreatePart("Tail Fairing", PrimitiveType.Cube, new Vector3(0f, 0.93f, -0.7f), new Vector3(0.64f, 0.3f, 0.64f), Quaternion.Euler(-8f, 0f, 0f), paint);
            CreatePart("Seat", PrimitiveType.Cube, new Vector3(0f, 1.14f, -0.48f), new Vector3(0.5f, 0.13f, 0.72f), Quaternion.Euler(-5f, 0f, 0f), leather);
            CreatePart("Left Front Fork", PrimitiveType.Cube, new Vector3(-0.18f, 0.78f, 0.89f), new Vector3(0.09f, 0.09f, 1.04f), Quaternion.Euler(58f, 0f, 0f), metal);
            CreatePart("Right Front Fork", PrimitiveType.Cube, new Vector3(0.18f, 0.78f, 0.89f), new Vector3(0.09f, 0.09f, 1.04f), Quaternion.Euler(58f, 0f, 0f), metal);
            CreatePart("Fork Brace", PrimitiveType.Cube, new Vector3(0f, 1.08f, 0.79f), new Vector3(0.54f, 0.09f, 0.12f), Quaternion.identity, metal);
            CreatePart("Exhaust", PrimitiveType.Cylinder, new Vector3(-0.42f, 0.55f, -0.45f), new Vector3(0.14f, 0.58f, 0.14f), Quaternion.Euler(90f, 0f, 0f), metal);

            handlebar = CreatePart("Handlebar", PrimitiveType.Cube, new Vector3(0f, 1.38f, 0.82f), new Vector3(1.02f, 0.09f, 0.09f), Quaternion.identity, metal).transform;
            CreatePart("Headlamp", PrimitiveType.Sphere, new Vector3(0f, 1.2f, 1.08f), new Vector3(0.42f, 0.42f, 0.28f), Quaternion.identity,
                RedlineMaterials.Get("headlamp", new Color(0.9f, 0.68f, 0.32f), 0.1f, 0.7f, new Color(0.42f, 0.25f, 0.08f)));
            CreatePart("Wind Screen", PrimitiveType.Cube, new Vector3(0f, 1.52f, 0.73f), new Vector3(0.5f, 0.38f, 0.07f), Quaternion.Euler(-24f, 0f, 0f), dark);
            CreatePart("Rear Lamp", PrimitiveType.Cube, new Vector3(0f, 1.02f, -1.04f), new Vector3(0.34f, 0.15f, 0.08f), Quaternion.identity,
                RedlineMaterials.Get("tail lamp", new Color(0.9f, 0.06f, 0.025f), 0.05f, 0.6f, new Color(0.45f, 0.015f, 0.005f)));

            Transform rider = new GameObject("Rider").transform;
            rider.SetParent(visualRoot, false);
            CreateRiderPart(rider, "Jacket Torso", PrimitiveType.Capsule, new Vector3(0f, 1.82f, 0.07f), new Vector3(0.42f, 0.42f, 0.34f), Quaternion.Euler(34f, 0f, 0f), paint);
            CreateRiderPart(rider, "Shoulders", PrimitiveType.Cube, new Vector3(0f, 2.02f, 0.22f), new Vector3(0.82f, 0.18f, 0.32f), Quaternion.Euler(26f, 0f, 0f), leather);
            CreateRiderPart(rider, "Head", PrimitiveType.Sphere, new Vector3(0f, 2.38f, 0.43f), new Vector3(0.48f, 0.52f, 0.5f), Quaternion.Euler(12f, 0f, 0f), helmet);
            CreateRiderPart(rider, "Visor", PrimitiveType.Cube, new Vector3(0f, 2.38f, 0.69f), new Vector3(0.36f, 0.16f, 0.08f), Quaternion.Euler(-3f, 0f, 0f), dark);
            LeftArm = CreateRiderPart(rider, "Left Arm", PrimitiveType.Capsule, new Vector3(-0.42f, 1.91f, 0.44f), new Vector3(0.18f, 0.5f, 0.18f), Quaternion.Euler(65f, 0f, -18f), skin).transform;
            RightArm = CreateRiderPart(rider, "Right Arm", PrimitiveType.Capsule, new Vector3(0.42f, 1.91f, 0.44f), new Vector3(0.18f, 0.5f, 0.18f), Quaternion.Euler(65f, 0f, 18f), skin).transform;
            CreateRiderPart(rider, "Left Glove", PrimitiveType.Sphere, new Vector3(-0.48f, 1.58f, 0.78f), new Vector3(0.18f, 0.16f, 0.2f), Quaternion.identity, leather);
            CreateRiderPart(rider, "Right Glove", PrimitiveType.Sphere, new Vector3(0.48f, 1.58f, 0.78f), new Vector3(0.18f, 0.16f, 0.2f), Quaternion.identity, leather);
            CreateRiderPart(rider, "Left Leg", PrimitiveType.Capsule, new Vector3(-0.31f, 1.3f, -0.12f), new Vector3(0.2f, 0.56f, 0.2f), Quaternion.Euler(-18f, 0f, -8f), leather);
            CreateRiderPart(rider, "Right Leg", PrimitiveType.Capsule, new Vector3(0.31f, 1.3f, -0.12f), new Vector3(0.2f, 0.56f, 0.2f), Quaternion.Euler(-18f, 0f, 8f), leather);
            CreateRiderPart(rider, "Left Boot", PrimitiveType.Cube, new Vector3(-0.36f, 0.96f, -0.3f), new Vector3(0.2f, 0.18f, 0.42f), Quaternion.Euler(-12f, 0f, 0f), dark);
            CreateRiderPart(rider, "Right Boot", PrimitiveType.Cube, new Vector3(0.36f, 0.96f, -0.3f), new Vector3(0.2f, 0.18f, 0.42f), Quaternion.Euler(-12f, 0f, 0f), dark);

            headlight = gameObject.AddComponent<Light>();
            headlight.type = LightType.Spot;
            headlight.color = new Color(1f, 0.72f, 0.4f);
            headlight.range = 28f;
            headlight.spotAngle = 48f;
            headlight.intensity = IsPlayer ? 2.1f : 0.7f;
            headlight.shadows = LightShadows.None;
            headlight.transform.localPosition = new Vector3(0f, 1.2f, 1.2f);
            headlight.transform.localRotation = Quaternion.Euler(6f, 0f, 0f);

            boostTrail = gameObject.AddComponent<TrailRenderer>();
            boostTrail.time = 0.28f;
            boostTrail.startWidth = 0.38f;
            boostTrail.endWidth = 0f;
            boostTrail.minVertexDistance = 0.15f;
            boostTrail.sharedMaterial = RedlineMaterials.Get("boost flame", new Color(1f, 0.27f, 0.04f), 0f, 0.25f, new Color(0.6f, 0.08f, 0.01f));
            boostTrail.emitting = false;
        }

        Transform CreateWheelAssembly(string name, Transform parent, Vector3 position, Material tire, Material hub, Material brake)
        {
            Transform rollPivot = new GameObject(name + " Roll Pivot").transform;
            rollPivot.SetParent(parent, false);
            rollPivot.localPosition = position;

            // Build a hollow tyre ring instead of a solid cylinder. A real gap between tyre,
            // spokes and hub keeps the rolling motion readable from a side camera.
            const int tireSegments = 16;
            float ringRadius = WheelRadius * 0.82f;
            float segmentLength = 2f * ringRadius * Mathf.Sin(Mathf.PI / tireSegments) * 1.12f;
            for (int segment = 0; segment < tireSegments; segment++)
            {
                float angle = segment * (360f / tireSegments);
                float radians = angle * Mathf.Deg2Rad;
                Vector3 segmentPosition = new Vector3(0f, Mathf.Sin(radians) * ringRadius, Mathf.Cos(radians) * ringRadius);
                CreateWheelPart(rollPivot, name + " Tyre Segment " + (segment + 1), PrimitiveType.Cube, segmentPosition,
                    new Vector3(0.18f, 0.12f, segmentLength), Quaternion.Euler(-angle - 90f, 0f, 0f), tire);
            }
            CreateWheelPart(rollPivot, name + " Brake Disc", PrimitiveType.Cylinder, Vector3.zero,
                new Vector3(0.28f, 0.055f, 0.28f), Quaternion.Euler(0f, 0f, 90f), brake);
            CreateWheelPart(rollPivot, name + " Hub", PrimitiveType.Cylinder, Vector3.zero,
                new Vector3(0.17f, 0.23f, 0.17f), Quaternion.Euler(0f, 0f, 90f), hub);

            // The spokes make the wheel's rolling direction readable at a glance.
            // Their long axis lies in the wheel plane (YZ); the roll pivot spins around X.
            for (int spoke = 0; spoke < 3; spoke++)
            {
                CreateWheelPart(rollPivot, name + " Spoke " + (spoke + 1), PrimitiveType.Cube, Vector3.zero,
                    new Vector3(0.075f, 0.045f, 0.56f), Quaternion.Euler(spoke * 60f, 0f, 0f), hub);
            }
            return rollPivot;
        }

        static void CreateWheelFender(string name, Transform parent, Vector3 center, Material material)
        {
            for (int section = 0; section < 7; section++)
            {
                float angle = 25f + section * 21.5f;
                float radians = angle * Mathf.Deg2Rad;
                Vector3 position = center + new Vector3(0f, Mathf.Sin(radians) * 0.42f, Mathf.Cos(radians) * 0.42f);
                CreateWheelPart(parent, name + " Section " + (section + 1), PrimitiveType.Cube, position,
                    new Vector3(0.3f, 0.055f, 0.18f), Quaternion.Euler(-angle - 90f, 0f, 0f), material);
            }
        }

        static GameObject CreateWheelPart(Transform parent, string name, PrimitiveType primitive, Vector3 position,
            Vector3 scale, Quaternion rotation, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(primitive);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.transform.localRotation = rotation;
            part.GetComponent<Renderer>().sharedMaterial = material;
            Destroy(part.GetComponent<Collider>());
            return part;
        }

        GameObject CreatePart(string name, PrimitiveType primitive, Vector3 position, Vector3 scale, Quaternion rotation, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(primitive);
            part.name = name;
            part.transform.SetParent(visualRoot, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.transform.localRotation = rotation;
            part.GetComponent<Renderer>().sharedMaterial = material;
            Destroy(part.GetComponent<Collider>());
            return part;
        }

        static GameObject CreateRiderPart(Transform parent, string name, PrimitiveType primitive, Vector3 position, Vector3 scale, Quaternion rotation, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(primitive);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.transform.localRotation = rotation;
            part.GetComponent<Renderer>().sharedMaterial = material;
            Destroy(part.GetComponent<Collider>());
            return part;
        }

        void Update()
        {
            if (IsPlayer && RedlineGame.I != null && !AutomationMode)
            {
                bool drive = RedlineGame.I.Phase == RacePhase.Racing;
                throttle = drive && (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) ? 1f : 0f;
                brake = drive && (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) ? 1f : 0f;
                steer = drive ? ((Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f)) : 0f;
                boostHeld = drive && Input.GetKey(KeyCode.Space);
                hasAILane = false;
                if (Input.GetKeyDown(KeyCode.R) && recoverCooldown <= 0f) Recover(false);
            }

            recoverCooldown -= Time.deltaTime;
            stun -= Time.deltaTime;
            AnimateVisual();
        }

        void FixedUpdate()
        {
            if (RoadSpline.I == null || body == null) return;
            Progress = RoadSpline.I.NearestDistance(transform.position, Progress);
            RoadSample road = RoadSpline.I.Sample(Mathf.Min(RoadSpline.I.Length, Progress + Mathf.Max(5f, speed * 0.38f)));

            bool canDrive = RedlineGame.I != null && RedlineGame.I.Phase == RacePhase.Racing && !IsWrecked && !Finished;
            if (RedlineGame.I != null && (RedlineGame.I.Phase == RacePhase.Title || RedlineGame.I.Phase == RacePhase.Countdown))
            {
                RoadSample grid = RoadSpline.I.Sample(Progress);
                Vector3 gridPosition = grid.position + grid.right * LaneOffset + grid.up * RoadClearance;
                Quaternion gridRotation = Quaternion.LookRotation(grid.forward, grid.up);
                body.position = gridPosition;
                body.rotation = gridRotation;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                speed = 0f;
                return;
            }
            if (!canDrive)
            {
                speed = Mathf.MoveTowards(speed, 0f, 12f * Time.fixedDeltaTime);
                body.linearVelocity = Vector3.Lerp(body.linearVelocity, road.forward * speed, 0.12f);
                return;
            }

            if (hasAILane) LaneOffset = Mathf.MoveTowards(LaneOffset, aiLaneTarget, 3.3f * Time.fixedDeltaTime);
            else LaneOffset = Mathf.Clamp(LaneOffset + steer * (3.6f + speed * 0.028f) * Time.fixedDeltaTime, -4.3f, 4.3f);

            bool boosting = boostHeld && Boost > 0.025f && throttle > 0.2f && stun <= 0f;
            float maxSpeed = boosting ? 47.5f : 40.5f;
            float desiredSpeed = throttle > 0.05f ? maxSpeed * Mathf.Lerp(0.72f, 1f, throttle) : 8f;
            if (brake > 0.05f) desiredSpeed = Mathf.Lerp(desiredSpeed, 3f, brake);
            if (stun > 0f) desiredSpeed *= 0.45f;
            desiredSpeed *= impactSlow;

            float acceleration = desiredSpeed > speed ? (boosting ? 18f : 11.5f) : (brake > 0.05f ? 23f : 6.5f);
            speed = Mathf.MoveTowards(speed, desiredSpeed, acceleration * Time.fixedDeltaTime);
            impactSlow = Mathf.MoveTowards(impactSlow, 1f, 0.75f * Time.fixedDeltaTime);

            if (boosting) Boost = Mathf.Max(0f, Boost - 0.22f * Time.fixedDeltaTime);
            else Boost = Mathf.Min(1f, Boost + 0.045f * Time.fixedDeltaTime);

            RoadSample exact = RoadSpline.I.Sample(Progress);
            Vector3 targetPosition = exact.position + exact.right * LaneOffset + exact.up * RoadClearance;
            Vector3 error = targetPosition - transform.position;
            float forwardError = Vector3.Dot(error, exact.forward);
            Vector3 correction = exact.right * Vector3.Dot(error, exact.right) * 4.8f + exact.up * Vector3.Dot(error, exact.up) * 7.5f + exact.forward * forwardError * 0.55f;
            correction = Vector3.ClampMagnitude(correction, 12f);
            Vector3 targetVelocity = road.forward * speed + correction;
            body.linearVelocity = Vector3.Lerp(body.linearVelocity, targetVelocity, 1f - Mathf.Exp(-9f * Time.fixedDeltaTime));

            float curveLean = Vector3.SignedAngle(exact.forward, road.forward, exact.up) * 0.8f;
            visualLean = Mathf.Clamp(-steer * Mathf.Lerp(12f, 29f, speed / 45f) - curveLean, -34f, 34f);
            Quaternion targetRotation = Quaternion.LookRotation(road.forward, road.up) * Quaternion.Euler(0f, steer * 2.5f, visualLean * 0.32f);
            body.MoveRotation(Quaternion.Slerp(body.rotation, targetRotation, 1f - Mathf.Exp(-8f * Time.fixedDeltaTime)));

            if (Mathf.Abs(LaneOffset) < RoadSpline.I.RoadHalfWidth - 0.8f && Mathf.Abs(Vector3.Dot(error, exact.up)) < 2f)
            {
                lastSafePosition = targetPosition;
                lastSafeRotation = Quaternion.LookRotation(exact.forward, exact.up);
            }

            if ((transform.position - exact.position).sqrMagnitude > 380f || transform.position.y < exact.position.y - 8f)
                Recover(true);

            boostTrail.emitting = boosting;
        }

        void AnimateVisual()
        {
            if (visualRoot == null) return;
            float targetLean = IsWrecked ? 68f : visualLean;
            visualRoot.localRotation = Quaternion.Lerp(visualRoot.localRotation, Quaternion.Euler(0f, 0f, targetLean), 1f - Mathf.Exp(-11f * Time.deltaTime));
            visualRoot.localPosition = Vector3.up * (Mathf.Sin(Time.time * 8f) * Mathf.Clamp01(speed / 30f) * 0.025f);
            // No-slip rolling: angular distance (radians) = distance travelled / wheel radius.
            // Both pivots use local X as their axle, independent of the cylinder mesh orientation.
            float wheelDegrees = speed * Time.deltaTime / WheelRadius * Mathf.Rad2Deg;
            WheelTravelDegrees += Mathf.Abs(wheelDegrees);
            if (frontWheelSpin != null) frontWheelSpin.Rotate(Vector3.right, wheelDegrees, Space.Self);
            if (rearWheelSpin != null) rearWheelSpin.Rotate(Vector3.right, wheelDegrees, Space.Self);
            if (frontSteerPivot != null)
                frontSteerPivot.localRotation = Quaternion.Lerp(frontSteerPivot.localRotation,
                    Quaternion.Euler(0f, steer * 8f, 0f), 1f - Mathf.Exp(-13f * Time.deltaTime));
            if (handlebar != null) handlebar.localRotation = Quaternion.Euler(0f, steer * 11f, 0f);
            if (headlight != null) headlight.intensity = Mathf.Lerp(headlight.intensity, IsWrecked ? 0.2f : IsPlayer ? 2.1f : 0.7f, 4f * Time.deltaTime);
        }

        public void SetAIControls(float throttleInput, float brakeInput, float steerInput, float targetLane, bool boostInput)
        {
            throttle = Mathf.Clamp01(throttleInput);
            brake = Mathf.Clamp01(brakeInput);
            steer = Mathf.Clamp(steerInput, -1f, 1f);
            aiLaneTarget = Mathf.Clamp(targetLane, -4.1f, 4.1f);
            hasAILane = true;
            boostHeld = boostInput;
        }

        public void EnableAutomation(float targetLane = 0f)
        {
            AutomationMode = true;
            SetAIControls(1f, 0f, 0f, targetLane, false);
        }

        public void TakeHit(float damage, Vector3 pushDirection, BikeMotor attacker)
        {
            if (IsWrecked || Finished || stun > 0.05f) return;
            Health = Mathf.Max(0f, Health - damage);
            stun = 0.32f;
            impactSlow = Mathf.Min(impactSlow, 0.68f);
            float side = Mathf.Sign(Vector3.Dot(pushDirection, RoadSpline.I.Sample(Progress).right));
            LaneOffset = Mathf.Clamp(LaneOffset + side * 0.8f, -4.6f, 4.6f);
            body.AddForce(pushDirection.normalized * 520f + Vector3.up * 150f, ForceMode.Impulse);
            RedlineFX.Impact(transform.position + Vector3.up * 1.4f, RiderColor, 18);
            ChaseCamera.Impact(IsPlayer ? 0.42f : 0.16f);
            RedlineAudio.Play(RedlineSound.Hit, transform.position);
            if (Health <= 0f) Wreck(attacker);
        }

        public void AddBoost(float amount)
        {
            Boost = Mathf.Clamp01(Boost + amount);
        }

        void OnCollisionEnter(Collision collision)
        {
            if (IsWrecked || Finished || collision.relativeVelocity.magnitude < 5.5f) return;
            float severity = Mathf.Clamp(collision.relativeVelocity.magnitude - 4f, 0f, 20f);
            BikeMotor otherRider = collision.collider.GetComponentInParent<BikeMotor>();
            float damageSeverity = otherRider != null ? severity * 0.28f : severity;
            Integrity = Mathf.Max(0f, Integrity - damageSeverity * 1.45f);
            impactSlow = Mathf.Min(impactSlow, Mathf.Lerp(0.82f, 0.38f, damageSeverity / 20f));
            stun = Mathf.Max(stun, Mathf.Lerp(0.08f, 0.55f, damageSeverity / 20f));
            RedlineFX.Impact(collision.GetContact(0).point, new Color(1f, 0.34f, 0.08f), Mathf.RoundToInt(6f + damageSeverity));
            ChaseCamera.Impact(IsPlayer ? damageSeverity * 0.035f : 0.08f);
            RedlineAudio.Play(RedlineSound.Crash, transform.position);
            if (Integrity <= 0f || (otherRider == null && severity > 17f)) Wreck(null);
        }

        public void Wreck(BikeMotor attacker)
        {
            if (IsWrecked) return;
            IsWrecked = true;
            speed *= 0.18f;
            boostTrail.emitting = false;
            RedlineFX.Impact(transform.position + Vector3.up, RiderColor, 34);
            if (attacker != null) attacker.AddBoost(0.22f);
            if (IsPlayer && Health <= 0f)
                RedlineGame.I.LoseRace("YOU WERE KNOCKED OUT");
            else
                StartCoroutine(RecoverAfterDelay(IsPlayer ? 1.55f : 2.2f));
        }

        IEnumerator RecoverAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            Recover(true);
        }

        public void Recover(bool penalize)
        {
            if (RoadSpline.I == null) return;
            recoverCooldown = 1.5f;
            if (penalize) Progress = Mathf.Max(0f, Progress - 12f);
            RoadSample sample = RoadSpline.I.Sample(Progress);
            LaneOffset = Mathf.Clamp(LaneOffset, -3.4f, 3.4f);
            transform.SetPositionAndRotation(sample.position + sample.right * LaneOffset + sample.up * (RoadClearance + 0.02f), Quaternion.LookRotation(sample.forward, sample.up));
            body.position = transform.position;
            body.rotation = transform.rotation;
            body.linearVelocity = sample.forward * Mathf.Max(10f, speed * 0.48f);
            body.angularVelocity = Vector3.zero;
            speed = Mathf.Max(10f, speed * 0.48f);
            Integrity = Mathf.Max(35f, Integrity);
            Health = Mathf.Max(20f, Health);
            IsWrecked = false;
            stun = 0.35f;
            impactSlow = 0.72f;
            boostTrail.Clear();
            RedlineFX.Recovery(transform.position);
        }
    }
}
