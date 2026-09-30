using System;
using UnityEngine;

namespace Redline
{
    public sealed class WorldBuilder : MonoBehaviour
    {
        public RoadSpline Road { get; private set; }

        public void Build()
        {
            ConfigureEnvironment();
            Road = new GameObject("Authoritative Mountain Route").AddComponent<RoadSpline>();
            Road.transform.SetParent(transform, false);
            Road.Build();
            BuildStartPlateau();
            BuildScenery();
            BuildLandmarks();
        }

        void ConfigureEnvironment()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.46f, 0.56f, 0.65f);
            RenderSettings.ambientEquatorColor = new Color(0.34f, 0.37f, 0.37f);
            RenderSettings.ambientGroundColor = new Color(0.17f, 0.15f, 0.12f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 135f;
            RenderSettings.fogEndDistance = 735f;
            RenderSettings.fogColor = new Color(0.5f, 0.57f, 0.63f);
            RenderSettings.reflectionIntensity = 0.62f;
            ConfigureSkybox();

            Light sun = new GameObject("Late Afternoon Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.72f, 0.5f);
            sun.intensity = 1.34f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.46f;
            sun.transform.rotation = Quaternion.Euler(36f, -42f, 0f);

            Light fill = new GameObject("Mountain Sky Fill").AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.34f, 0.52f, 0.68f);
            fill.intensity = 0.24f;
            fill.shadows = LightShadows.None;
            fill.transform.rotation = Quaternion.Euler(48f, 138f, 0f);

            QualitySettings.antiAliasing = 4;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
            QualitySettings.shadowDistance = 105f;
            QualitySettings.shadowResolution = ShadowResolution.High;
            QualitySettings.lodBias = 1.35f;
            QualitySettings.pixelLightCount = 6;
            Physics.defaultSolverIterations = 8;
            Physics.defaultSolverVelocityIterations = 3;
        }

        static void ConfigureSkybox()
        {
            Shader shader = Shader.Find("Skybox/Procedural");
            if (shader == null) return;
            Material sky = new Material(shader) { name = "Redline Late Afternoon Sky" };
            sky.SetColor("_SkyTint", new Color(0.34f, 0.49f, 0.68f));
            sky.SetColor("_GroundColor", new Color(0.38f, 0.23f, 0.13f));
            sky.SetFloat("_AtmosphereThickness", 0.82f);
            sky.SetFloat("_Exposure", 0.92f);
            sky.SetFloat("_SunSize", 0.045f);
            sky.SetFloat("_SunSizeConvergence", 4.5f);
            RenderSettings.skybox = sky;
        }

        void BuildScenery()
        {
            Transform root = new GameObject("Authored Roadside Scenery").transform;
            root.SetParent(transform, false);
            System.Random random = new System.Random(7821);
            Material trunk = RedlineMaterials.Get("tree trunk", new Color(0.24f, 0.15f, 0.09f), 0f, 0.16f);
            Material pineA = RedlineMaterials.Get("pine dark", new Color(0.17f, 0.31f, 0.21f), 0f, 0.12f);
            Material pineB = RedlineMaterials.Get("pine sun", new Color(0.27f, 0.42f, 0.24f), 0f, 0.12f);
            Material rock = RedlineMaterials.Get("sandstone", new Color(0.48f, 0.34f, 0.24f), 0f, 0.18f);
            Material ridge = RedlineMaterials.Get("distant ridge", new Color(0.38f, 0.4f, 0.42f), 0f, 0.12f);

            for (float d = 28f; d < Road.Length - 35f; d += 24f)
            {
                RoadSample sample = Road.Sample(d);
                for (int side = -1; side <= 1; side += 2)
                {
                    float chance = (float)random.NextDouble();
                    float offset = 9f + (float)random.NextDouble() * 17f;
                    Vector3 position = sample.position + sample.right * side * offset - sample.up * 0.25f;
                    if (chance < 0.46f)
                    {
                        float height = 4.8f + (float)random.NextDouble() * 5f;
                        CreatePine(root, position, height, chance < 0.38f ? pineA : pineB, trunk);
                    }
                    else if (chance < 0.82f)
                    {
                        float height = 5.5f + (float)random.NextDouble() * 5.8f;
                        PlaceKenney(root, chance < 0.64f ? "treeLarge" : "treeSmall", position,
                            Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f), height);
                    }
                    else
                    {
                        GameObject stone = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        stone.name = "Weathered Sandstone";
                        stone.transform.SetParent(root, true);
                        stone.transform.position = position + Vector3.up * 0.55f;
                        stone.transform.rotation = Quaternion.Euler((float)random.NextDouble() * 30f, (float)random.NextDouble() * 180f, (float)random.NextDouble() * 20f);
                        float scale = 1.3f + (float)random.NextDouble() * 2.6f;
                        stone.transform.localScale = new Vector3(scale, scale * 0.58f, scale * 0.78f);
                        stone.GetComponent<Renderer>().sharedMaterial = rock;
                        Destroy(stone.GetComponent<Collider>());
                    }
                }
            }

            for (int i = 0; i < 12; i++)
            {
                float d = 130f + i * 152f;
                RoadSample s = Road.Sample(Mathf.Min(Road.Length, d));
                float side = i % 2 == 0 ? -1f : 1f;
                Vector3 basePosition = s.position + s.right * side * (92f + i % 4 * 14f) - Vector3.up * 5f;
                CreateMountainCluster(root, basePosition, 42f + i % 3 * 10f, i % 2 == 0 ? rock : ridge);
            }
        }

        void BuildStartPlateau()
        {
            RoadSample start = Road.Sample(35f);
            Material ground = RedlineMaterials.Get("terrain", new Color(0.25f, 0.29f, 0.17f), 0f, 0.08f);
            RoadSpline.CreateBox(transform, "Start Valley Ground", start.position - Vector3.up * 2.15f - start.forward * 18f,
                Quaternion.LookRotation(start.forward, Vector3.up), new Vector3(240f, 3.4f, 250f), ground, false);
        }

        static void CreatePine(Transform parent, Vector3 position, float height, Material foliage, Material trunkMaterial)
        {
            GameObject tree = new GameObject("Roadside Pine");
            tree.transform.SetParent(parent, true);
            tree.transform.position = position;

            GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.transform.SetParent(tree.transform, false);
            trunk.transform.localPosition = Vector3.up * height * 0.27f;
            trunk.transform.localScale = new Vector3(height * 0.055f, height * 0.27f, height * 0.055f);
            trunk.GetComponent<Renderer>().sharedMaterial = trunkMaterial;
            Destroy(trunk.GetComponent<Collider>());

            for (int tier = 0; tier < 3; tier++)
            {
                float tierHeight = height * (0.42f + tier * 0.19f);
                float radius = height * (0.3f - tier * 0.055f);
                GameObject crown = CreateCone("Pine Crown", 7, radius, height * 0.42f, foliage);
                crown.transform.SetParent(tree.transform, false);
                crown.transform.localPosition = Vector3.up * tierHeight;
            }
        }

        static void CreateMountainCluster(Transform parent, Vector3 position, float height, Material material)
        {
            Transform cluster = new GameObject("Layered Distant Ridge").transform;
            cluster.SetParent(parent, true);
            cluster.position = position;
            cluster.rotation = Quaternion.Euler(0f, position.x * 0.37f, 0f);
            float[] heights = { 0.72f, 1f, 0.62f, 0.82f };
            float[] offsets = { -0.58f, 0f, 0.62f, 1.08f };
            for (int i = 0; i < heights.Length; i++)
            {
                float peak = height * heights[i];
                GameObject mountain = CreateCone("Ridge Peak", 9, peak * 0.9f, peak, material);
                mountain.transform.SetParent(cluster, false);
                mountain.transform.localPosition = new Vector3(offsets[i] * height, 0f, Mathf.Abs(offsets[i]) * 6f);
                mountain.transform.localScale = new Vector3(1.35f, 1f, 0.72f);
            }
        }

        static GameObject PlaceKenney(Transform parent, string resourceName, Vector3 groundPosition, Quaternion rotation, float targetHeight)
        {
            GameObject source = Resources.Load<GameObject>("Kenney/" + resourceName);
            if (source == null) return null;
            GameObject instance = Instantiate(source, parent);
            instance.name = "Kenney " + resourceName;
            instance.transform.SetPositionAndRotation(groundPosition, rotation);
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return instance;
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            float scale = targetHeight / Mathf.Max(0.01f, bounds.size.y);
            instance.transform.localScale = Vector3.one * scale;
            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            instance.transform.position += Vector3.up * (groundPosition.y - bounds.min.y);
            foreach (Collider collider in instance.GetComponentsInChildren<Collider>()) Destroy(collider);
            return instance;
        }

        static GameObject CreateCone(string name, int sides, float radius, float height, Material material)
        {
            GameObject go = new GameObject(name);
            Mesh mesh = new Mesh { name = name + " Mesh" };
            Vector3[] vertices = new Vector3[sides + 2];
            vertices[0] = Vector3.zero;
            vertices[1] = Vector3.up * height;
            for (int i = 0; i < sides; i++)
            {
                float a = i / (float)sides * Mathf.PI * 2f;
                vertices[i + 2] = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
            }
            int[] triangles = new int[sides * 6];
            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                int t = i * 6;
                triangles[t] = 0; triangles[t + 1] = next + 2; triangles[t + 2] = i + 2;
                triangles[t + 3] = 1; triangles[t + 4] = i + 2; triangles[t + 5] = next + 2;
            }
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        void BuildLandmarks()
        {
            Material concrete = RedlineMaterials.Get("concrete", new Color(0.36f, 0.33f, 0.29f), 0f, 0.24f);
            Material red = RedlineMaterials.Get("race red", new Color(0.62f, 0.08f, 0.045f), 0.18f, 0.32f, new Color(0.06f, 0f, 0f));
            Material cream = RedlineMaterials.Get("sign cream", new Color(0.9f, 0.76f, 0.48f), 0.05f, 0.28f);
            Transform root = new GameObject("Race Landmarks").transform;
            root.SetParent(transform, false);

            CreateGate(root, 34f, "START", red, cream);
            CreateGate(root, Road.Length - 20f, "FINISH", cream, red);

            for (float d = 430f; d < Road.Length - 100f; d += 410f)
                CreateRoadSign(root, d, d < 800f ? "HAIRPINS" : d < 1250f ? "SUMMIT" : "FINAL PUSH", cream, red);

            for (float d = 885f; d <= 950f; d += 13f)
            {
                RoadSample s = Road.Sample(d);
                Quaternion rotation = Quaternion.LookRotation(s.forward, s.up);
                RoadSpline.CreateBox(root, "Tunnel Left", s.position - s.right * 6.3f + Vector3.up * 2.2f, rotation, new Vector3(1.6f, 4.4f, 1.2f), concrete, true);
                RoadSpline.CreateBox(root, "Tunnel Right", s.position + s.right * 6.3f + Vector3.up * 2.2f, rotation, new Vector3(1.6f, 4.4f, 1.2f), concrete, true);
                RoadSpline.CreateBox(root, "Tunnel Roof", s.position + Vector3.up * 5f, rotation, new Vector3(14.2f, 1.4f, 1.2f), concrete, true);
            }

            BuildRaceDressing(root);
        }

        void BuildRaceDressing(Transform parent)
        {
            BuildPaddockMotorcycle(parent);
            BuildRoadsideDetail(parent);
            for (int side = -1; side <= 1; side += 2)
            {
                RoadSample start = Road.Sample(45f);
                PlaceKenney(parent, "tentClosed", start.position + start.right * side * 14f, Quaternion.LookRotation(-start.right * side, Vector3.up), 4.1f);
                PlaceKenney(parent, "flagCheckers", start.position + start.right * side * 7.8f, Quaternion.LookRotation(start.forward, Vector3.up), 5.4f);
                RoadSample board = Road.Sample(92f);
                PlaceKenney(parent, "billboard", board.position + board.right * side * 12f, Quaternion.LookRotation(-board.right * side, Vector3.up), 4.4f);

                for (float d = 22f; d < 72f; d += 7.5f)
                {
                    RoadSample barrier = Road.Sample(d);
                    PlaceKenney(parent, ((Mathf.RoundToInt(d / 7.5f) + side) & 1) == 0 ? "barrierRed" : "barrierWhite",
                        barrier.position + barrier.right * side * (Road.RoadHalfWidth + 1.25f), Quaternion.LookRotation(barrier.forward, barrier.up), 0.72f);
                }

                for (float d = 260f; d < Road.Length - 120f; d += 360f)
                {
                    RoadSample light = Road.Sample(d + (side > 0 ? 55f : 0f));
                    PlaceKenney(parent, "lightPostModern", light.position + light.right * side * 8.2f, Quaternion.LookRotation(light.forward, Vector3.up), 7.2f);
                }
            }

            RoadSample radar = Road.Sample(635f);
            PlaceKenney(parent, "radarEquipment", radar.position - radar.right * 11f, Quaternion.LookRotation(radar.forward, Vector3.up), 4.8f);
            RoadSample cone = Road.Sample(1240f);
            for (int i = -3; i <= 3; i++)
                PlaceKenney(parent, "pylon", cone.position + cone.forward * i * 4f + cone.right * (i % 2 == 0 ? -4.3f : 4.3f), Quaternion.LookRotation(cone.forward, Vector3.up), 0.85f);
        }

        void BuildRoadsideDetail(Transform parent)
        {
            Material post = RedlineMaterials.Get("delineator post", new Color(0.82f, 0.8f, 0.7f), 0.05f, 0.32f);
            Material reflector = RedlineMaterials.Get("amber reflector", new Color(1f, 0.48f, 0.08f), 0.12f, 0.72f, new Color(0.34f, 0.08f, 0.005f));
            Material chevron = RedlineMaterials.Get("curve board", new Color(0.67f, 0.075f, 0.035f), 0.08f, 0.38f);

            for (float distance = 105f; distance < Road.Length - 65f; distance += 58f)
            {
                RoadSample sample = Road.Sample(distance);
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 basePosition = sample.position + sample.right * side * (Road.RoadHalfWidth + 2.05f);
                    RoadSpline.CreateBox(parent, "Road Delineator", basePosition + sample.up * 0.52f,
                        Quaternion.LookRotation(sample.forward, sample.up), new Vector3(0.16f, 1.04f, 0.16f), post, false);
                    RoadSpline.CreateBox(parent, "Amber Reflector", basePosition + sample.up * 0.8f - sample.right * side * 0.09f,
                        Quaternion.LookRotation(-sample.right * side, sample.up), new Vector3(0.2f, 0.18f, 0.06f), reflector, false);
                }
            }

            for (float distance = 150f; distance < Road.Length - 90f; distance += 82f)
            {
                RoadSample sample = Road.Sample(distance);
                RoadSample ahead = Road.Sample(Mathf.Min(Road.Length, distance + 24f));
                float turn = Vector3.SignedAngle(sample.forward, ahead.forward, sample.up);
                if (Mathf.Abs(turn) < 3.2f) continue;
                int outside = turn > 0f ? -1 : 1;
                Vector3 position = sample.position + sample.right * outside * (Road.RoadHalfWidth + 3.3f) + sample.up * 1.25f;
                Quaternion facingRoad = Quaternion.LookRotation(-sample.right * outside, sample.up);
                RoadSpline.CreateBox(parent, "Hairpin Chevron", position, facingRoad, new Vector3(1.85f, 1.05f, 0.14f), chevron, false);
                RoadSpline.CreateBox(parent, "Chevron Reflective Face", position - sample.right * outside * 0.09f,
                    facingRoad * Quaternion.Euler(0f, 0f, turn > 0f ? -26f : 26f), new Vector3(1.1f, 0.18f, 0.055f), post, false);
            }
        }

        void BuildPaddockMotorcycle(Transform parent)
        {
            GameObject source = Resources.Load<GameObject>("ThirdParty/OpenGameArt/FancyMotorcycle/bike");
            if (source == null)
            {
                Debug.LogWarning("CC0 paddock motorcycle could not be loaded.");
                return;
            }

            RoadSample display = Road.Sample(26f);
            Vector3 ground = display.position + display.right * 14.2f;
            Material plinth = RedlineMaterials.Get("paddock plinth", new Color(0.12f, 0.105f, 0.09f), 0.18f, 0.42f);
            RoadSpline.CreateBox(parent, "Paddock Motorcycle Plinth", ground + display.up * 0.14f,
                Quaternion.LookRotation(display.forward, display.up), new Vector3(3.4f, 0.28f, 1.55f), plinth, false);

            GameObject instance = Instantiate(source, parent);
            instance.name = "CC0 Fancy Motorcycle Paddock Display";
            instance.transform.SetPositionAndRotation(ground + display.up * 0.3f,
                Quaternion.LookRotation(display.forward, display.up) * Quaternion.Euler(0f, -90f, 0f));

            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            float scale = 2.7f / Mathf.Max(0.01f, Mathf.Max(bounds.size.x, bounds.size.z));
            instance.transform.localScale = Vector3.one * scale;

            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            instance.transform.position += display.up * ((ground + display.up * 0.29f).y - bounds.min.y);

            Material displayPaint = RedlineMaterials.Get("heritage motorcycle", new Color(0.46f, 0.055f, 0.035f), 0.42f, 0.38f);
            foreach (Renderer renderer in renderers) renderer.sharedMaterial = displayPaint;
            foreach (Collider collider in instance.GetComponentsInChildren<Collider>()) Destroy(collider);
        }

        void CreateGate(Transform parent, float distance, string label, Material primary, Material secondary)
        {
            RoadSample s = Road.Sample(distance);
            Quaternion rotation = Quaternion.LookRotation(s.forward, s.up);
            RoadSpline.CreateBox(parent, label + " Left", s.position - s.right * 6.4f + Vector3.up * 2.5f, rotation, new Vector3(0.45f, 5f, 0.45f), primary, false);
            RoadSpline.CreateBox(parent, label + " Right", s.position + s.right * 6.4f + Vector3.up * 2.5f, rotation, new Vector3(0.45f, 5f, 0.45f), primary, false);
            RoadSpline.CreateBox(parent, label + " Header", s.position + Vector3.up * 5f, rotation, new Vector3(13.3f, 0.7f, 0.5f), secondary, false);
        }

        void CreateRoadSign(Transform parent, float distance, string label, Material face, Material accent)
        {
            RoadSample s = Road.Sample(distance);
            Vector3 position = s.position + s.right * (Road.RoadHalfWidth + 3.4f) + Vector3.up * 1.6f;
            Quaternion rotation = Quaternion.LookRotation(-s.right, Vector3.up);
            RoadSpline.CreateBox(parent, label + " Sign", position, rotation, new Vector3(0.25f, 2.4f, 3.6f), face, false);
            RoadSpline.CreateBox(parent, label + " Stripe", position + Vector3.up * 0.55f - s.right * 0.15f, rotation, new Vector3(0.05f, 0.32f, 3.2f), accent, false);
        }
    }
}
