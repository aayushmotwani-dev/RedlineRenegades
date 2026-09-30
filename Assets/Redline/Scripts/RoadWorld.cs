using System.Collections.Generic;
using UnityEngine;

namespace Redline
{
    public struct RoadSample
    {
        public Vector3 position;
        public Vector3 forward;
        public Vector3 right;
        public Vector3 up;
        public float distance;
    }

    public sealed class RoadSpline : MonoBehaviour
    {
        public static RoadSpline I { get; private set; }
        public float Length { get; private set; }
        public float RoadHalfWidth => 5.4f;
        public IReadOnlyList<RoadSample> Samples => samples;

        readonly List<Vector3> controls = new List<Vector3>();
        readonly List<RoadSample> samples = new List<RoadSample>();

        public void Build()
        {
            I = this;
            CreateControlPoints();
            BakeSamples(20);
            BuildRibbon("Mountain Ground", 46f, -0.42f, RedlineMaterials.Get("terrain", new Color(0.25f, 0.29f, 0.17f), 0f, 0.08f), true, 0.6f);
            BuildRibbon("Gravel Shoulders", RoadHalfWidth + 2.2f, -0.06f, RedlineMaterials.Get("shoulder", new Color(0.43f, 0.34f, 0.22f), 0f, 0.12f), true, 0f);
            BuildRibbon("Asphalt", RoadHalfWidth, 0f, RedlineMaterials.Get("asphalt", new Color(0.105f, 0.115f, 0.12f), 0.05f, 0.28f), true, 0f);
            BuildMarkings();
            BuildGuardrails();
        }

        void CreateControlPoints()
        {
            controls.Clear();
            controls.Add(new Vector3(0f, 2f, 0f));
            controls.Add(new Vector3(8f, 2f, 90f));
            controls.Add(new Vector3(-18f, 5f, 190f));
            controls.Add(new Vector3(-62f, 10f, 285f));
            controls.Add(new Vector3(-38f, 16f, 385f));
            controls.Add(new Vector3(24f, 20f, 470f));
            controls.Add(new Vector3(78f, 27f, 560f));
            controls.Add(new Vector3(64f, 31f, 655f));
            controls.Add(new Vector3(5f, 37f, 742f));
            controls.Add(new Vector3(-72f, 44f, 820f));
            controls.Add(new Vector3(-92f, 50f, 915f));
            controls.Add(new Vector3(-42f, 55f, 1005f));
            controls.Add(new Vector3(40f, 59f, 1080f));
            controls.Add(new Vector3(104f, 64f, 1160f));
            controls.Add(new Vector3(92f, 69f, 1260f));
            controls.Add(new Vector3(26f, 74f, 1348f));
            controls.Add(new Vector3(-54f, 80f, 1430f));
            controls.Add(new Vector3(-72f, 85f, 1525f));
            controls.Add(new Vector3(-20f, 89f, 1615f));
            controls.Add(new Vector3(54f, 94f, 1705f));
            controls.Add(new Vector3(70f, 98f, 1810f));
        }

        void BakeSamples(int resolution)
        {
            samples.Clear();
            float cumulative = 0f;
            Vector3 previous = controls[0];
            int segmentCount = controls.Count - 1;
            for (int segment = 0; segment < segmentCount; segment++)
            {
                Vector3 p0 = controls[Mathf.Max(0, segment - 1)];
                Vector3 p1 = controls[segment];
                Vector3 p2 = controls[segment + 1];
                Vector3 p3 = controls[Mathf.Min(controls.Count - 1, segment + 2)];
                for (int step = 0; step < resolution; step++)
                {
                    float t = step / (float)resolution;
                    Vector3 position = Catmull(p0, p1, p2, p3, t);
                    Vector3 next = Catmull(p0, p1, p2, p3, Mathf.Min(1f, t + 0.015f));
                    Vector3 forward = (next - position).normalized;
                    if (forward.sqrMagnitude < 0.5f) forward = Vector3.forward;
                    if (samples.Count > 0) cumulative += Vector3.Distance(previous, position);
                    Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
                    Vector3 up = Vector3.Cross(forward, right).normalized;
                    samples.Add(new RoadSample { position = position, forward = forward, right = right, up = up, distance = cumulative });
                    previous = position;
                }
            }
            Vector3 last = controls[controls.Count - 1];
            cumulative += Vector3.Distance(previous, last);
            Vector3 lastForward = (last - previous).normalized;
            Vector3 lastRight = Vector3.Cross(Vector3.up, lastForward).normalized;
            samples.Add(new RoadSample { position = last, forward = lastForward, right = lastRight, up = Vector3.Cross(lastForward, lastRight).normalized, distance = cumulative });
            Length = cumulative;
        }

        static Vector3 Catmull(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        public RoadSample Sample(float distance)
        {
            distance = Mathf.Clamp(distance, 0f, Length);
            int low = 0;
            int high = samples.Count - 1;
            while (low < high)
            {
                int mid = (low + high) / 2;
                if (samples[mid].distance < distance) low = mid + 1;
                else high = mid;
            }
            int b = Mathf.Clamp(low, 1, samples.Count - 1);
            int a = b - 1;
            float span = Mathf.Max(0.001f, samples[b].distance - samples[a].distance);
            float t = (distance - samples[a].distance) / span;
            RoadSample result = new RoadSample
            {
                position = Vector3.Lerp(samples[a].position, samples[b].position, t),
                forward = Vector3.Slerp(samples[a].forward, samples[b].forward, t).normalized,
                right = Vector3.Slerp(samples[a].right, samples[b].right, t).normalized,
                up = Vector3.Slerp(samples[a].up, samples[b].up, t).normalized,
                distance = distance
            };
            return result;
        }

        public float NearestDistance(Vector3 world, float hint = -1f)
        {
            int start = 0;
            int end = samples.Count;
            if (hint >= 0f)
            {
                int center = Mathf.RoundToInt(hint / Mathf.Max(0.1f, Length) * (samples.Count - 1));
                start = Mathf.Max(0, center - 34);
                end = Mathf.Min(samples.Count, center + 35);
            }
            float best = float.MaxValue;
            int bestIndex = start;
            for (int i = start; i < end; i++)
            {
                float sqr = (samples[i].position - world).sqrMagnitude;
                if (sqr >= best) continue;
                best = sqr;
                bestIndex = i;
            }
            return samples[bestIndex].distance;
        }

        void BuildRibbon(string name, float halfWidth, float yOffset, Material material, bool collider, float edgeNoise)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            Mesh mesh = new Mesh { name = name + " Mesh", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            Vector3[] vertices = new Vector3[samples.Count * 2];
            Vector2[] uv = new Vector2[vertices.Length];
            int[] triangles = new int[(samples.Count - 1) * 6];
            for (int i = 0; i < samples.Count; i++)
            {
                RoadSample s = samples[i];
                float noise = edgeNoise > 0f ? Mathf.Sin(i * 1.73f) * edgeNoise : 0f;
                vertices[i * 2] = s.position - s.right * (halfWidth + noise) + s.up * yOffset;
                vertices[i * 2 + 1] = s.position + s.right * (halfWidth - noise) + s.up * yOffset;
                uv[i * 2] = new Vector2(0f, s.distance / 12f);
                uv[i * 2 + 1] = new Vector2(halfWidth * 2f / 12f, s.distance / 12f);
                if (i >= samples.Count - 1) continue;
                int t = i * 6;
                int v = i * 2;
                triangles[t] = v; triangles[t + 1] = v + 2; triangles[t + 2] = v + 1;
                triangles[t + 3] = v + 1; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
            }
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            if (collider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        void BuildMarkings()
        {
            Material cream = RedlineMaterials.Get("lane paint", new Color(0.92f, 0.83f, 0.57f), 0f, 0.25f, new Color(0.1f, 0.06f, 0.015f));
            Material edge = RedlineMaterials.Get("edge paint", new Color(0.78f, 0.72f, 0.58f), 0f, 0.22f);
            Transform root = new GameObject("Road Markings").transform;
            root.SetParent(transform, false);
            for (float d = 12f; d < Length - 10f; d += 22f)
            {
                RoadSample s = Sample(d);
                CreateBox(root, "Centre Dash", s.position + s.up * 0.055f, Quaternion.LookRotation(s.forward, s.up), new Vector3(0.18f, 0.035f, 8f), cream, false);
            }
            BuildEdgeLine(root, -RoadHalfWidth + 0.3f, edge);
            BuildEdgeLine(root, RoadHalfWidth - 0.3f, edge);
        }

        void BuildEdgeLine(Transform root, float offset, Material material)
        {
            LineRenderer line = new GameObject(offset < 0f ? "Left Edge Line" : "Right Edge Line").AddComponent<LineRenderer>();
            line.transform.SetParent(root, false);
            line.positionCount = samples.Count;
            line.widthMultiplier = 0.16f;
            line.sharedMaterial = material;
            line.textureMode = LineTextureMode.Stretch;
            for (int i = 0; i < samples.Count; i++) line.SetPosition(i, samples[i].position + samples[i].right * offset + samples[i].up * 0.06f);
        }

        void BuildGuardrails()
        {
            Material rail = RedlineMaterials.Get("guardrail", new Color(0.42f, 0.45f, 0.43f), 0.75f, 0.5f);
            Transform root = new GameObject("Guardrails").transform;
            root.SetParent(transform, false);
            for (float d = 30f; d < Length - 15f; d += 18f)
            {
                RoadSample s = Sample(d);
                RoadSample n = Sample(Mathf.Min(Length, d + 18f));
                float length = Vector3.Distance(s.position, n.position);
                Vector3 midpoint = (s.position + n.position) * 0.5f;
                Vector3 forward = (n.position - s.position).normalized;
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 position = midpoint + s.right * side * (RoadHalfWidth + 0.95f) + Vector3.up * 0.58f;
                    CreateBox(root, side < 0 ? "Left Rail" : "Right Rail", position, Quaternion.LookRotation(forward, Vector3.up), new Vector3(0.18f, 0.28f, length + 0.6f), rail, true);
                }
            }
        }

        public static GameObject CreateBox(Transform parent, string name, Vector3 position, Quaternion rotation, Vector3 scale, Material material, bool collider)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, true);
            box.transform.SetPositionAndRotation(position, rotation);
            box.transform.localScale = scale;
            box.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider) Object.Destroy(box.GetComponent<Collider>());
            return box;
        }
    }

    public static class RedlineMaterials
    {
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();

        public static Material Get(string key, Color color, float metallic, float smoothness, Color emission = default)
        {
            if (cache.TryGetValue(key, out Material existing) && existing != null) return existing;
            Shader shader = Resources.Load<Shader>("RedlineSurface");
            if (shader == null) shader = Shader.Find("Standard");
            Material material = new Material(shader) { name = "RR " + key };
            material.SetColor("_Color", color);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", emission);
            if (material.HasProperty("_MainTex") && (key == "terrain" || key == "shoulder" || key == "asphalt"))
            {
                material.SetTexture("_MainTex", CreateSurfaceTexture(key));
                if (material.HasProperty("_BumpMap")) material.SetTexture("_BumpMap", CreateSurfaceNormal(key));
                if (material.HasProperty("_BumpStrength")) material.SetFloat("_BumpStrength", key == "asphalt" ? 0.42f : 0.68f);
            }
            cache[key] = material;
            return material;
        }

        static Texture2D CreateSurfaceTexture(string key)
        {
            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGB24, true)
            {
                name = "Hand-tuned " + key + " variation",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4
            };
            int seed = key == "asphalt" ? 17 : key == "shoulder" ? 43 : 71;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float value = SurfaceValue(key, x, y, seed);
                    texture.SetPixel(x, y, new Color(value, value, value));
                }
            }
            texture.Apply(true, false);
            return texture;
        }

        static Texture2D CreateSurfaceNormal(string key)
        {
            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGB24, true)
            {
                name = "Authored " + key + " micro-normal",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4
            };
            int seed = key == "asphalt" ? 17 : key == "shoulder" ? 43 : 71;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float left = SurfaceValue(key, x - 1, y, seed);
                    float right = SurfaceValue(key, x + 1, y, seed);
                    float down = SurfaceValue(key, x, y - 1, seed);
                    float up = SurfaceValue(key, x, y + 1, seed);
                    Vector3 normal = new Vector3(left - right, down - up, 1.5f).normalized;
                    texture.SetPixel(x, y, new Color(normal.x * 0.5f + 0.5f, normal.y * 0.5f + 0.5f, normal.z * 0.5f + 0.5f));
                }
            }
            texture.Apply(true, false);
            return texture;
        }

        static float SurfaceValue(string key, int x, int y, int seed)
        {
            float broad = Mathf.PerlinNoise((x + seed) * 0.075f, (y + seed * 0.37f) * 0.075f);
            float grain = Mathf.PerlinNoise((x + seed * 2f) * 0.43f, (y - seed) * 0.43f);
            float value = key == "asphalt"
                ? Mathf.Lerp(0.68f, 1.18f, broad * 0.64f + grain * 0.36f)
                : Mathf.Lerp(0.7f, 1.25f, broad * 0.78f + grain * 0.22f);
            if (key == "shoulder" && ((x * 13 + y * 7 + seed) % 47 == 0)) value *= 1.32f;
            return value;
        }
    }
}
