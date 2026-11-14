using UnityEngine;

namespace MissionDesign.UI.Visual3D
{
    /// <summary>Llama de motor y humo creados por código (sin assets de partículas externos).</summary>
    public static class ExhaustFx
    {
        private static Texture2D softDot;

        public static void Create(Transform parent, Vector3 localPosition, float radius, Material flameMaterial, Material smokeMaterial)
        {
            if (flameMaterial != null)
                Build(parent, localPosition, "Exhaust", radius, flameMaterial, lifetime: 0.5f, speed: radius * 40f, size: radius * 2.4f,
                    rate: 160, start: new Color(1f, 0.85f, 0.45f, 1f), end: new Color(1f, 0.3f, 0.05f, 0f), world: false);
            if (smokeMaterial != null)
                Build(parent, localPosition, "Smoke", radius * 2.5f, smokeMaterial, lifetime: 3.5f, speed: radius * 10f, size: radius * 7f,
                    rate: 60, start: new Color(0.85f, 0.85f, 0.85f, 0.55f), end: new Color(0.6f, 0.6f, 0.6f, 0f), world: true);
        }

        private static void Build(Transform parent, Vector3 localPosition, string name, float radius, Material material,
            float lifetime, float speed, float size, float rate, Color start, Color end, bool world)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);    // emite hacia abajo (-Y)
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.startLifetime = lifetime;
            main.startSpeed = speed;
            main.startSize = size;
            main.maxParticles = 600;
            main.simulationSpace = world ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;

            var emission = ps.emission;
            emission.rateOverTime = rate;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = world ? 25f : 6f;
            shape.radius = radius;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(start, 0f), new GradientColorKey(end, 1f) },
                new[] { new GradientAlphaKey(start.a, 0f), new GradientAlphaKey(end.a, 1f) });
            col.color = gradient;
            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, world ? 0.5f : 1f, 1f, world ? 2.5f : 0.3f));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            if (material.mainTexture == null && material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", SoftDot());
            ps.Play();
        }

        /// <summary>Textura radial suave (se genera una sola vez).</summary>
        public static Texture2D SoftDot()
        {
            if (softDot != null) return softDot;
            const int n = 64;
            softDot = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "SoftDot" };
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                float a = Mathf.Clamp01(1f - d);
                softDot.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
            softDot.Apply();
            return softDot;
        }
    }
}
