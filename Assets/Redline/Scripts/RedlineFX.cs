using UnityEngine;

namespace Redline
{
    public enum RedlineSound { Swing, Hit, Crash, Boost, Countdown, Finish }

    public static class RedlineFX
    {
        public static void Impact(Vector3 position, Color color, int count)
        {
            GameObject go = new GameObject("Impact Debris");
            go.transform.position = position;
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = ps.main;
            main.duration = 0.45f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.58f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 7.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.045f, 0.16f);
            main.startColor = color;
            main.gravityModifier = 1.2f;
            main.stopAction = ParticleSystemStopAction.Destroy;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Clamp(count, 3, 60)) });
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.2f;
            ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = RedlineMaterials.Get("impact particle", color, 0.1f, 0.25f, color * 0.14f);
            ps.Play();
        }

        public static void Recovery(Vector3 position)
        {
            Impact(position + Vector3.up * 0.4f, new Color(0.92f, 0.68f, 0.24f), 22);
        }

        public static void AttackArc(Vector3 position, Vector3 direction, Color color)
        {
            GameObject go = new GameObject("Directional Attack Arc");
            LineRenderer line = go.AddComponent<LineRenderer>();
            line.positionCount = 11;
            line.widthMultiplier = 0.09f;
            line.sharedMaterial = RedlineMaterials.Get("attack arc " + color, color, 0f, 0.25f, color * 0.22f);
            Vector3 up = Vector3.up;
            Vector3 forward = Vector3.Cross(direction.normalized, up).normalized;
            for (int i = 0; i < line.positionCount; i++)
            {
                float t = i / (float)(line.positionCount - 1);
                float angle = Mathf.Lerp(-48f, 48f, t) * Mathf.Deg2Rad;
                Vector3 arc = direction.normalized * Mathf.Cos(angle) + forward * Mathf.Sin(angle);
                line.SetPosition(i, position + arc * 0.9f + up * Mathf.Sin(t * Mathf.PI) * 0.25f);
            }
            go.AddComponent<TransientLine>().life = 0.18f;
        }
    }

    public sealed class TransientLine : MonoBehaviour
    {
        public float life = 0.2f;
        float age;
        LineRenderer line;

        void Awake() => line = GetComponent<LineRenderer>();

        void Update()
        {
            age += Time.deltaTime;
            if (line != null) line.widthMultiplier = Mathf.Lerp(0.09f, 0f, age / life);
            if (age >= life) Destroy(gameObject);
        }
    }

    public sealed class ChaseCamera : MonoBehaviour
    {
        static ChaseCamera instance;
        BikeMotor target;
        Camera cameraComponent;
        Vector3 velocity;
        float shake;

        public static Camera Create(BikeMotor follow)
        {
            GameObject go = new GameObject("Dynamic Chase Camera");
            Camera camera = go.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.backgroundColor = new Color(0.42f, 0.56f, 0.67f);
            camera.fieldOfView = 59f;
            camera.nearClipPlane = 0.12f;
            camera.farClipPlane = 850f;
            camera.allowHDR = true;
            go.AddComponent<AudioListener>();
            go.AddComponent<PremiumPresentation>();
            ChaseCamera chase = go.AddComponent<ChaseCamera>();
            chase.target = follow;
            chase.cameraComponent = camera;
            instance = chase;
            RoadSample sample = RoadSpline.I.Sample(follow.Progress);
            go.transform.position = follow.transform.position - sample.forward * 5.4f + sample.up * 2.75f;
            go.transform.LookAt(follow.transform.position + sample.up * 1.35f + sample.forward * 4f, sample.up);
            return camera;
        }

        public static void Impact(float amount)
        {
            if (instance != null) instance.shake = Mathf.Max(instance.shake, Mathf.Clamp(amount, 0f, 0.75f));
        }

        void LateUpdate()
        {
            if (target == null || RoadSpline.I == null) return;
            RoadSample road = RoadSpline.I.Sample(Mathf.Min(RoadSpline.I.Length, target.Progress + 7f));
            float speed01 = Mathf.Clamp01(target.SpeedKph / 170f);
            Vector3 lookPoint = target.transform.position + road.up * 1.35f + road.forward * Mathf.Lerp(3.6f, 6.2f, speed01);
            Vector3 desired = target.transform.position - road.forward * Mathf.Lerp(5.1f, 6.5f, speed01) + road.up * Mathf.Lerp(2.55f, 3.05f, speed01) + road.right * target.LaneOffset * 0.03f;
            Vector3 fromLook = desired - lookPoint;
            float distance = fromLook.magnitude;
            if (Physics.SphereCast(lookPoint, 0.3f, fromLook.normalized, out RaycastHit hit, distance, ~0, QueryTriggerInteraction.Ignore))
            {
                BikeMotor hitBike = hit.collider.GetComponentInParent<BikeMotor>();
                if (hitBike == null || hitBike != target) desired = hit.point - fromLook.normalized * 0.4f;
            }

            float smoothTime = Mathf.Lerp(0.12f, 0.075f, speed01);
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime, 100f, Time.unscaledDeltaTime);
            if (shake > 0.001f)
            {
                transform.position += Random.insideUnitSphere * shake;
                shake = Mathf.MoveTowards(shake, 0f, 2.6f * Time.unscaledDeltaTime);
            }
            Quaternion lookRotation = Quaternion.LookRotation(lookPoint - transform.position, road.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime));
            cameraComponent.fieldOfView = Mathf.Lerp(cameraComponent.fieldOfView, Mathf.Lerp(56f, 65f, speed01), 4.2f * Time.unscaledDeltaTime);
        }
    }

    public static class RedlineAudio
    {
        static readonly AudioClip[] clips = new AudioClip[6];
        static AudioClip engineClip;
        static AudioSource oneShot;
        static AudioSource ambience;

        public static void AttachEngine(GameObject root, BikeMotor motor)
        {
            Ensure();
            AudioSource source = root.AddComponent<AudioSource>();
            source.clip = engineClip;
            source.loop = true;
            source.playOnAwake = true;
            source.spatialBlend = motor.IsPlayer ? 0.2f : 0.75f;
            source.minDistance = 4f;
            source.maxDistance = 35f;
            source.volume = motor.IsPlayer ? 0.2f : 0.08f;
            root.AddComponent<BikeEngineAudio>().Initialize(motor, source);
            source.Play();
        }

        public static void StartAmbience()
        {
            Ensure();
            if (ambience != null) return;
            ambience = new GameObject("Mountain Wind Ambience").AddComponent<AudioSource>();
            ambience.clip = MakeAmbience();
            ambience.loop = true;
            ambience.spatialBlend = 0f;
            ambience.volume = 0.07f;
            ambience.Play();
        }

        public static void Play(RedlineSound sound, Vector3 position)
        {
            Ensure();
            oneShot.transform.position = position;
            oneShot.pitch = Random.Range(0.94f, 1.06f);
            oneShot.PlayOneShot(clips[(int)sound], sound == RedlineSound.Crash ? 0.62f : 0.42f);
        }

        static void Ensure()
        {
            if (oneShot != null) return;
            oneShot = new GameObject("Redline One-shot Audio").AddComponent<AudioSource>();
            oneShot.spatialBlend = 0.3f;
            oneShot.playOnAwake = false;
            clips[(int)RedlineSound.Swing] = MakeSweep("Air Cut", 0.16f, 280f, 720f, 0.28f, 0.1f);
            clips[(int)RedlineSound.Hit] = MakeSweep("Body Hit", 0.18f, 110f, 52f, 0.48f, 0.34f);
            clips[(int)RedlineSound.Crash] = MakeSweep("Metal Crash", 0.34f, 88f, 32f, 0.55f, 0.58f);
            clips[(int)RedlineSound.Boost] = MakeSweep("Boost", 0.3f, 160f, 620f, 0.32f, 0.16f);
            clips[(int)RedlineSound.Countdown] = MakeSweep("Count", 0.16f, 440f, 440f, 0.32f, 0.04f);
            clips[(int)RedlineSound.Finish] = MakeSweep("Finish", 0.55f, 380f, 820f, 0.35f, 0.05f);
            engineClip = MakeEngine();
        }

        static AudioClip MakeSweep(string name, float duration, float startFrequency, float endFrequency, float volume, float noiseAmount)
        {
            const int sampleRate = 22050;
            int count = Mathf.CeilToInt(duration * sampleRate);
            float[] data = new float[count];
            float phase = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)count;
                float frequency = Mathf.Lerp(startFrequency, endFrequency, t);
                phase += frequency / sampleRate * Mathf.PI * 2f;
                float envelope = Mathf.Pow(1f - t, 2f) * Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, t * 16f));
                data[i] = (Mathf.Sin(phase) * (1f - noiseAmount) + (Random.value * 2f - 1f) * noiseAmount) * envelope * volume;
            }
            AudioClip clip = AudioClip.Create(name, count, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip MakeEngine()
        {
            const int sampleRate = 22050;
            const int count = sampleRate;
            float[] data = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)sampleRate;
                data[i] = (Mathf.Sin(t * Mathf.PI * 2f * 72f) * 0.34f + Mathf.Sin(t * Mathf.PI * 2f * 144f) * 0.16f + Mathf.Sin(t * Mathf.PI * 2f * 216f) * 0.08f) * 0.32f;
            }
            AudioClip clip = AudioClip.Create("Looping Motorcycle Engine", count, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip MakeAmbience()
        {
            const int sampleRate = 11025;
            const int count = sampleRate * 6;
            float[] data = new float[count];
            for (int i = 0; i < count; i++)
            {
                float cycle = i / (float)count;
                data[i] = (Mathf.Sin(cycle * Mathf.PI * 2f) * 0.18f + Mathf.Sin(cycle * Mathf.PI * 8f) * 0.07f + Mathf.Sin(cycle * Mathf.PI * 18f) * 0.03f) * 0.45f;
            }
            AudioClip clip = AudioClip.Create("Looping Ridge Wind", count, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }

    public sealed class BikeEngineAudio : MonoBehaviour
    {
        BikeMotor motor;
        AudioSource source;

        public void Initialize(BikeMotor owner, AudioSource audioSource)
        {
            motor = owner;
            source = audioSource;
        }

        void Update()
        {
            if (motor == null || source == null) return;
            float speed = Mathf.Clamp01(motor.SpeedKph / 170f);
            source.pitch = Mathf.Lerp(0.58f, 1.78f, speed);
            float targetVolume = motor.IsWrecked ? 0.04f : motor.IsPlayer ? Mathf.Lerp(0.14f, 0.28f, speed) : Mathf.Lerp(0.035f, 0.11f, speed);
            source.volume = Mathf.Lerp(source.volume, targetVolume, 4f * Time.unscaledDeltaTime);
        }
    }
}
