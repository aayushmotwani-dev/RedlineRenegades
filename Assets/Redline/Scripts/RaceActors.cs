using UnityEngine;

namespace Redline
{
    public sealed class RiderCombat : MonoBehaviour
    {
        public bool Attacking => attackSide != 0;
        public float CooldownNormalized => Mathf.Clamp01(cooldown / 0.62f);

        BikeMotor motor;
        int attackSide;
        float attackAge;
        float cooldown;
        bool activeResolved;
        Quaternion leftRest;
        Quaternion rightRest;

        public void Initialize(BikeMotor owner)
        {
            motor = owner;
            leftRest = owner.LeftArm.localRotation;
            rightRest = owner.RightArm.localRotation;
        }

        void Update()
        {
            if (motor == null) return;
            cooldown -= Time.deltaTime;
            bool canFight = RedlineGame.I != null && RedlineGame.I.Phase == RacePhase.Racing && !motor.IsWrecked && !motor.Finished;
            if (motor.IsPlayer && canFight && attackSide == 0)
            {
                if (Input.GetKeyDown(KeyCode.Q)) BeginAttack(-1);
                else if (Input.GetKeyDown(KeyCode.E)) BeginAttack(1);
            }

            if (attackSide == 0)
            {
                motor.LeftArm.localRotation = Quaternion.Slerp(motor.LeftArm.localRotation, leftRest, 14f * Time.deltaTime);
                motor.RightArm.localRotation = Quaternion.Slerp(motor.RightArm.localRotation, rightRest, 14f * Time.deltaTime);
                return;
            }

            attackAge += Time.deltaTime;
            Transform arm = attackSide < 0 ? motor.LeftArm : motor.RightArm;
            Quaternion rest = attackSide < 0 ? leftRest : rightRest;
            float swing;
            if (attackAge < 0.16f) swing = Mathf.Lerp(0f, -58f * attackSide, attackAge / 0.16f);
            else if (attackAge < 0.29f) swing = Mathf.Lerp(-58f * attackSide, 78f * attackSide, (attackAge - 0.16f) / 0.13f);
            else swing = Mathf.Lerp(78f * attackSide, 0f, Mathf.Clamp01((attackAge - 0.29f) / 0.23f));
            arm.localRotation = rest * Quaternion.Euler(0f, swing, -swing * 0.28f);

            if (!activeResolved && attackAge >= 0.19f)
            {
                activeResolved = true;
                ResolveAttack();
            }
            if (attackAge >= 0.54f)
            {
                attackSide = 0;
                attackAge = 0f;
                cooldown = 0.22f;
            }
        }

        public bool BeginAttack(int side)
        {
            if (motor == null || attackSide != 0 || cooldown > 0f || motor.IsWrecked || motor.Finished) return false;
            attackSide = side < 0 ? -1 : 1;
            attackAge = 0f;
            activeResolved = false;
            cooldown = 0.62f;
            RedlineAudio.Play(RedlineSound.Swing, motor.transform.position);
            return true;
        }

        void ResolveAttack()
        {
            RoadSample road = RoadSpline.I.Sample(motor.Progress);
            Vector3 sideDirection = road.right * attackSide;
            Vector3 center = motor.transform.position + sideDirection * 1.28f + road.forward * 0.22f + road.up * 1.15f;
            RedlineFX.AttackArc(center, sideDirection, attackSide < 0 ? new Color(1f, 0.72f, 0.22f) : new Color(0.92f, 0.24f, 0.08f));
            Collider[] hits = Physics.OverlapSphere(center, 0.95f, ~0, QueryTriggerInteraction.Ignore);
            BikeMotor best = null;
            float bestDistance = float.MaxValue;
            foreach (Collider hit in hits)
            {
                BikeMotor target = hit.GetComponentInParent<BikeMotor>();
                if (target == null || target == motor || target.IsWrecked || target.Finished) continue;
                Vector3 toTarget = target.transform.position - motor.transform.position;
                if (Vector3.Dot(toTarget, sideDirection) < 0.25f) continue;
                float distance = toTarget.sqrMagnitude;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = target;
            }

            if (best != null)
            {
                best.TakeHit(18f, sideDirection + road.forward * 0.28f, motor);
                motor.AddBoost(0.09f);
                if (motor.IsPlayer) RedlineGame.I.AddScore(250, "CLEAN HIT");
            }
        }
    }

    public sealed class RaceAI : MonoBehaviour
    {
        BikeMotor motor;
        RiderCombat combat;
        float laneTarget;
        float decisionTimer;
        float aggression;
        float pace;
        float attackTimer;
        int personalitySeed;

        public void Initialize(BikeMotor owner)
        {
            motor = owner;
            combat = owner.GetComponent<RiderCombat>();
            personalitySeed = Mathf.Abs(owner.RiderName.GetHashCode());
            aggression = 0.38f + (personalitySeed % 53) / 100f;
            pace = 34.5f + (personalitySeed % 47) * 0.095f;
            laneTarget = owner.LaneOffset;
            decisionTimer = 0.2f + (personalitySeed % 7) * 0.08f;
        }

        void Update()
        {
            if (motor == null || RedlineGame.I == null || RedlineGame.I.Player == null) return;
            if (RedlineGame.I.Phase != RacePhase.Racing || motor.IsWrecked || motor.Finished)
            {
                motor.SetAIControls(0f, 0.25f, 0f, laneTarget, false);
                return;
            }

            decisionTimer -= Time.deltaTime;
            attackTimer -= Time.deltaTime;
            BikeMotor player = RedlineGame.I.Player;
            float playerDelta = player.Progress - motor.Progress;
            float targetSpeed = pace;
            if (playerDelta > 55f) targetSpeed += Mathf.Min(3.2f, playerDelta * 0.018f);
            else if (playerDelta < -80f) targetSpeed -= Mathf.Min(2.4f, -playerDelta * 0.012f);

            BikeMotor nearby = FindNearbyThreat();
            if (decisionTimer <= 0f)
            {
                decisionTimer = 0.65f + (personalitySeed % 5) * 0.11f;
                if (nearby != null)
                {
                    float side = Mathf.Sign(motor.LaneOffset - nearby.LaneOffset);
                    if (Mathf.Abs(motor.LaneOffset - nearby.LaneOffset) < 1.1f)
                        laneTarget = Mathf.Clamp(motor.LaneOffset + (side == 0f ? (personalitySeed % 2 == 0 ? -2.2f : 2.2f) : side * 1.8f), -3.9f, 3.9f);
                }
                else
                {
                    float wave = Mathf.Sin(motor.Progress * 0.017f + personalitySeed * 0.1f);
                    laneTarget = Mathf.Clamp(wave * 3.1f, -3.8f, 3.8f);
                }
            }

            if (nearby != null && attackTimer <= 0f && Mathf.Abs(nearby.Progress - motor.Progress) < 3.2f && aggression > 0.44f)
            {
                float lateral = nearby.LaneOffset - motor.LaneOffset;
                if (Mathf.Abs(lateral) < 2.25f && Mathf.Abs(lateral) > 0.2f)
                {
                    combat.BeginAttack(lateral < 0f ? -1 : 1);
                    attackTimer = Mathf.Lerp(2.4f, 1.15f, aggression);
                }
            }

            float throttle = motor.SpeedKph < targetSpeed * 3.6f ? 1f : 0.45f;
            float brake = motor.SpeedKph > (targetSpeed + 3.5f) * 3.6f ? 0.55f : 0f;
            float steer = Mathf.Clamp((laneTarget - motor.LaneOffset) * 0.7f, -1f, 1f);
            bool boost = playerDelta > 95f && motor.Boost > 0.52f;
            motor.SetAIControls(throttle, brake, steer, laneTarget, boost);
        }

        BikeMotor FindNearbyThreat()
        {
            BikeMotor best = null;
            float bestScore = float.MaxValue;
            foreach (BikeMotor other in BikeMotor.All)
            {
                if (other == null || other == motor || other.IsWrecked || other.Finished) continue;
                float longitudinal = Mathf.Abs(other.Progress - motor.Progress);
                if (longitudinal > 12f) continue;
                float lateral = Mathf.Abs(other.LaneOffset - motor.LaneOffset);
                float score = longitudinal + lateral * 1.8f;
                if (score >= bestScore) continue;
                bestScore = score;
                best = other;
            }
            return best;
        }
    }

    public sealed class TrafficVehicle : MonoBehaviour
    {
        float progress;
        float lane;
        float speed;
        Rigidbody body;
        readonly Transform[] wheelPivots = new Transform[4];

        public static TrafficVehicle Create(int index, float startDistance, float laneOffset, float driveSpeed)
        {
            GameObject root = new GameObject("Civilian Traffic " + (index + 1));
            TrafficVehicle traffic = root.AddComponent<TrafficVehicle>();
            traffic.progress = startDistance;
            traffic.lane = laneOffset;
            traffic.speed = driveSpeed;
            traffic.BuildVisual(index);

            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.75f, 0f);
            collider.size = new Vector3(1.85f, 1.5f, 3.9f);
            Rigidbody rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            traffic.body = rb;
            traffic.PlaceImmediate();
            return traffic;
        }

        void BuildVisual(int index)
        {
            Material paint = RedlineMaterials.Get("traffic " + index, Color.HSVToRGB((index * 0.17f + 0.08f) % 1f, 0.5f, 0.66f), 0.35f, 0.42f);
            Material dark = RedlineMaterials.Get("traffic glass", new Color(0.045f, 0.065f, 0.07f), 0.25f, 0.62f);
            Material tire = RedlineMaterials.Get("traffic tire", new Color(0.03f, 0.03f, 0.028f), 0f, 0.14f);
            Transform visual = new GameObject("Low-poly Civilian Car").transform;
            visual.SetParent(transform, false);
            RoadSpline.CreateBox(visual, "Body", new Vector3(0f, 0.62f, 0f), Quaternion.identity, new Vector3(1.75f, 0.62f, 3.65f), paint, false);
            RoadSpline.CreateBox(visual, "Cabin", new Vector3(0f, 1.18f, -0.18f), Quaternion.identity, new Vector3(1.48f, 0.65f, 1.85f), dark, false);
            Vector3[] wheelPositions = { new Vector3(-0.92f, 0.22f, -1.15f), new Vector3(0.92f, 0.22f, -1.15f), new Vector3(-0.92f, 0.22f, 1.15f), new Vector3(0.92f, 0.22f, 1.15f) };
            for (int wheelIndex = 0; wheelIndex < wheelPositions.Length; wheelIndex++)
            {
                Vector3 wheelPosition = wheelPositions[wheelIndex];
                Transform pivot = new GameObject("Traffic Wheel Roll Pivot").transform;
                pivot.SetParent(visual, false);
                pivot.localPosition = wheelPosition;
                wheelPivots[wheelIndex] = pivot;
                GameObject wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                wheel.name = "Traffic Wheel";
                wheel.transform.SetParent(pivot, false);
                wheel.transform.localPosition = Vector3.zero;
                wheel.transform.localScale = new Vector3(0.42f, 0.16f, 0.42f);
                wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                wheel.GetComponent<Renderer>().sharedMaterial = tire;
                Destroy(wheel.GetComponent<Collider>());
            }
        }

        void FixedUpdate()
        {
            if (RoadSpline.I == null || body == null || RedlineGame.I == null) return;
            if (RedlineGame.I.Phase == RacePhase.Racing) progress += speed * Time.fixedDeltaTime;
            if (RedlineGame.I.Phase == RacePhase.Racing)
            {
                float wheelDegrees = speed * Time.fixedDeltaTime / 0.21f * Mathf.Rad2Deg;
                foreach (Transform pivot in wheelPivots)
                    if (pivot != null) pivot.Rotate(Vector3.right, wheelDegrees, Space.Self);
            }
            if (progress > RoadSpline.I.Length - 5f)
            {
                gameObject.SetActive(false);
                return;
            }
            RoadSample sample = RoadSpline.I.Sample(progress);
            Vector3 position = sample.position + sample.right * lane + sample.up * 0.04f;
            body.MovePosition(position);
            body.MoveRotation(Quaternion.LookRotation(sample.forward, sample.up));
        }

        void PlaceImmediate()
        {
            RoadSample sample = RoadSpline.I.Sample(progress);
            transform.SetPositionAndRotation(sample.position + sample.right * lane + sample.up * 0.04f, Quaternion.LookRotation(sample.forward, sample.up));
        }
    }
}
