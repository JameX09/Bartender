using UnityEngine;

namespace Bartender
{
    /// <summary>Small particle effects: bubbles in fizzy drinks, splashes, sparkles.</summary>
    public static class Fx
    {
        static ParticleSystem bubbles, splash, sparkle;

        public static void Init(Transform parent)
        {
            bubbles = Make("bubbles", parent, 0f, Mats.Particle(new Color(1, 1, 1, 0.55f), Mats.SoftDot));
            splash = Make("splash", parent, 1f, Mats.Particle(Color.white, Mats.SoftDot));
            sparkle = Make("sparkle", parent, -0.05f, Mats.Additive(new Color(1f, 0.85f, 0.5f, 1f), Mats.SoftDot));
        }

        static ParticleSystem Make(string name, Transform parent, float gravity, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 3000;
            main.gravityModifier = gravity;
            main.startLifetime = 1f;
            main.startSpeed = 0f;
            var em = ps.emission; em.enabled = false;
            var sh = ps.shape; sh.enabled = false;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                      new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.12f), new GradientAlphaKey(1, 0.75f), new GradientAlphaKey(0, 1) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sortingFudge = -10;
            ps.Play();
            return ps;
        }

        public static void Bubble(Vessel v)
        {
            if (bubbles == null || v.Solid == null) return;
            float bottom = v.Solid.BottomY + 0.004f;
            float top = v.Level / Mathf.Max(0.5f, v.LevelN.y);
            if (top - bottom < 0.01f) return;
            float r = v.Solid.RadiusAt(bottom + 0.01f) * 0.75f;
            float a = Random.value * Mathf.PI * 2f;
            float d = Mathf.Sqrt(Random.value) * r;
            var local = new Vector3(Mathf.Cos(a) * d, bottom, Mathf.Sin(a) * d);
            float speed = Random.Range(0.025f, 0.05f);
            var ep = new ParticleSystem.EmitParams
            {
                position = v.transform.TransformPoint(local),
                velocity = Vector3.up * speed,
                startSize = Random.Range(0.0012f, 0.0024f),
                startLifetime = (top - bottom) / speed,
                startColor = new Color(1, 1, 1, 0.7f),
            };
            bubbles.Emit(ep, 1);
        }

        public static void Splash(Vector3 p, Color c, int n)
        {
            if (splash == null) return;
            for (int i = 0; i < n; i++)
            {
                var ep = new ParticleSystem.EmitParams
                {
                    position = p,
                    velocity = new Vector3(Random.Range(-0.35f, 0.35f), Random.Range(0.2f, 0.7f), Random.Range(-0.35f, 0.35f)),
                    startSize = Random.Range(0.003f, 0.007f),
                    startLifetime = Random.Range(0.25f, 0.5f),
                    startColor = c,
                };
                splash.Emit(ep, 1);
            }
        }

        public static void Sparkle(Vector3 p, int n)
        {
            if (sparkle == null) return;
            for (int i = 0; i < n; i++)
            {
                var ep = new ParticleSystem.EmitParams
                {
                    position = p + Random.insideUnitSphere * 0.05f,
                    velocity = Random.insideUnitSphere * 0.25f + Vector3.up * 0.2f,
                    startSize = Random.Range(0.008f, 0.02f),
                    startLifetime = Random.Range(0.5f, 1.1f),
                    startColor = new Color(1f, 0.85f, 0.5f, 1f),
                };
                sparkle.Emit(ep, 1);
            }
        }
    }
}
