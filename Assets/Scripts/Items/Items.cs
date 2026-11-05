using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Bartender
{
    /// <summary>Anything the bartender can pick up.</summary>
    public abstract class Item : MonoBehaviour
    {
        public Vector3 HomePos;
        public Quaternion HomeRot = Quaternion.identity;
        public bool HasHome;
        public bool Held;
        public bool Locked;          // a served glass belongs to the guest
        public float Height = 0.1f;  // pivot is at the bottom centre
        public float Radius = 0.04f;
        public Item AttachedTo;      // cap / strainer sitting on a vessel

        public virtual string Title => name;
        public virtual string Info => "";
        public virtual bool Pourable => false;
        /// <summary>Where the held item sits in view (camera space offsets).</summary>
        public virtual Vector3 HoldOffset => new Vector3(0.17f, -0.2f, 0.45f);
        public virtual Quaternion HoldRotation => Quaternion.identity;

        public void SetHome()
        {
            HomePos = transform.position;
            HomeRot = transform.rotation;
            HasHome = true;
        }

        public void SetColliders(bool on)
        {
            foreach (var c in GetComponentsInChildren<Collider>(true)) c.enabled = on;
        }
    }

    // ------------------------------------------------------------------ bottles

    public sealed class Bottle : Item
    {
        public Ingredient Ing;
        public Vector3 SpoutLocal;     // tip of the pourer, local space
        public override string Title => Ing.Name;
        public override string Info => Ing.Kind == Kind.Bitters ? Loc.S("Dashes", "Kvapky") : Ing.Kind switch
        {
            Kind.Juice => Loc.S("Fresh juice", "Čerstvá šťava"),
            Kind.Syrup => Loc.S("Syrup", "Sirup"),
            Kind.Mixer => Loc.S("Mixer", "Miešačka"),
            Kind.Wine => Loc.S("Wine", "Víno"),
            Kind.Liqueur => Loc.S("Liqueur", "Likér"),
            _ => Loc.S("Spirit", "Destilát"),
        };
        public override bool Pourable => true;
        public override Vector3 HoldOffset => new Vector3(0.16f, -0.28f, 0.45f);

        /// <summary>ml per second for a tilt angle (degrees from upright).</summary>
        public float FlowAt(float tilt)
        {
            float start = Ing.Style == BottleStyle.Carafe ? 55f : 75f;
            float k = Mathf.Clamp01((tilt - start) / 55f);
            k = k * k * (3f - 2f * k);
            float rate = Ing.PourRate;
            if (Save.Has(Upgrade.SpeedPourers) && Ing.Kind != Kind.Bitters) rate *= 1.35f;
            if (Ing.Kind != Kind.Bitters) rate *= Save.PourSpeed;
            return rate * k;
        }
    }

    // ------------------------------------------------------------------ vessels

    public enum VesselKind { Glass, Tin, MixingGlass, Jigger, Cup }

    /// <summary>Trigger at the opening of a vessel; pour streams that hit it go inside.</summary>
    public sealed class Mouth : MonoBehaviour
    {
        public Vessel Owner;
    }

    public sealed class Vessel : Item
    {
        public VesselKind VKind;
        public GlassType GType;
        public readonly Contents C = new Contents();
        public int Ice;
        public int MaxIce = 6;
        public float IceSize = 0.026f;
        public readonly List<Garnish> Garnishes = new List<Garnish>();
        public float Muddle;
        public LiquidSolid Solid;
        public ShakerCap Cap;
        public StrainerItem Strainer;
        public bool Served;
        public float Level;           // current plane offset along LevelN (local)
        public Vector3 LevelN = Vector3.up;
        public Transform Mouth;
        public float SpawnTime;

        // double-sided jigger: two cups, flip with R
        public float NominalMl;              // exact brim volume (0 = from geometry)
        public bool Flipped;
        public LiquidSolid SolidA, SolidB;   // A = big side up, B = small side up
        public float NominalA, NominalB, JiggerHeight, WaistA, WaistB;
        public Transform Body;
        float flipAnim = 1f;
        public float WaistY => Flipped ? WaistB : WaistA;
        public bool Flipping => flipAnim < 1f;

        MeshFilter liquidF;
        MeshRenderer liquidR;
        Mesh liquidMesh;
        Material liquidMat;
        readonly List<Transform> iceObjs = new List<Transform>();
        Transform garnishRoot;
        readonly List<Vector3> bV = new List<Vector3>(4096), bN = new List<Vector3>(4096);
        readonly List<int> bT = new List<int>(8192);
        int lastVersion = -1, lastIce = -1, lastGarnish = -1;
        Vector3 lastUp = Vector3.zero;
        float bubbleTimer;

        public static readonly List<Vessel> All = new List<Vessel>();
        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public float CapacityMl => NominalMl > 0 ? NominalMl : Solid.Volume * 1e6f;
        float GeoScale => NominalMl > 0 ? Solid.Volume * 1e6f / NominalMl : 1f;
        public float Ml => C.Total;
        public override bool Pourable => Cap == null;
        public override Vector3 HoldOffset => VKind == VesselKind.Jigger ? new Vector3(0.12f, -0.16f, 0.4f) : VKind == VesselKind.Cup ? new Vector3(0.13f, -0.17f, 0.4f) : new Vector3(0.16f, -0.24f, 0.45f);

        public override string Title
        {
            get
            {
                switch (VKind)
                {
                    case VesselKind.Tin: return Loc.S("Shaker tin", "Šejker");
                    case VesselKind.MixingGlass: return Loc.S("Mixing glass", "Miešací pohár");
                    case VesselKind.Jigger: return Loc.S($"Jigger {NominalA:0}/{NominalB:0} ml · {NominalMl:0} ml side up", $"Džiger {NominalA:0}/{NominalB:0} ml · hore {NominalMl:0} ml");
                    case VesselKind.Cup: return Loc.S("Espresso cup", "Šálka na espresso");
                    default: return Recipes.GlassName(GType);
                }
            }
        }

        public override string Info
        {
            get
            {
                var sb = new StringBuilder();
                if (C.Total < 0.5f && Ice == 0 && Garnishes.Count == 0) return Loc.S("Empty", "Prázdne");
                foreach (var kv in C.Ml)
                {
                    if (kv.Key.Kind == Kind.Water) continue;
                    if (sb.Length > 0) sb.Append("  ·  ");
                    sb.Append(kv.Key.Name).Append(' ').Append(kv.Value.ToString(kv.Value < 5 ? "0.0" : "0")).Append(" ml");
                }
                if (Ice > 0) { if (sb.Length > 0) sb.Append("  ·  "); sb.Append(Loc.S("Ice", "Ľad")).Append(" ×").Append(Ice); }
                foreach (var g in Garnishes) { if (sb.Length > 0) sb.Append("  ·  "); sb.Append(Recipes.GarnishName(g)); }
                if (C.Shake > 0.05f) sb.Append("\n").Append(Loc.S("Shaken ", "Pretrepané ")).Append(Mathf.RoundToInt(Mathf.Min(C.Shake, 3f) * 100)).Append('%');
                if (C.Stir > 0.05f) sb.Append("\n").Append(Loc.S("Stirred ", "Miešané ")).Append(Mathf.RoundToInt(Mathf.Min(C.Stir, 3f) * 100)).Append('%');
                if (Muddle > 0.05f) sb.Append("\n").Append(Loc.S("Muddled ", "Rozdrvené ")).Append(Mathf.RoundToInt(Mathf.Min(Muddle, 1f) * 100)).Append('%');
                if (Cap != null) sb.Append("\n").Append(Loc.S("Capped", "Zatvorené"));
                if (Strainer != null) sb.Append("\n").Append(Loc.S("Strainer on", "So sitkom"));
                return sb.ToString();
            }
        }

        public void InitVisuals(LiquidSolid solid)
        {
            Solid = solid;
            var lg = new GameObject("liquid");
            lg.transform.SetParent(transform, false);
            liquidF = lg.AddComponent<MeshFilter>();
            liquidR = lg.AddComponent<MeshRenderer>();
            liquidR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            liquidMesh = new Mesh { name = "liquid" };
            liquidMesh.MarkDynamic();
            liquidF.sharedMesh = liquidMesh;
            liquidMat = Mats.Liquid(Color.clear);
            liquidR.sharedMaterial = liquidMat;
            liquidR.enabled = false;
            garnishRoot = new GameObject("garnish").transform;
            garnishRoot.SetParent(transform, false);
        }

        public float DisplayVolumeMl
        {
            get
            {
                float ml = C.Total;
                if (ml <= 0.01f) return 0;
                float iceDisp = Ice * IceSize * IceSize * IceSize * 1e6f * 0.8f * Mathf.Clamp01(ml / 40f);
                return Mathf.Min(ml + iceDisp, CapacityMl * 0.999f);
            }
        }

        void LateUpdate()
        {
            if (flipAnim < 1f && Body != null)
            {
                flipAnim = Mathf.MoveTowards(flipAnim, 1f, Time.deltaTime / 0.28f);
                float s = flipAnim * flipAnim * (3f - 2f * flipAnim);
                float from = Flipped ? 0f : 180f, to = Flipped ? 180f : 0f;
                float a = Mathf.Lerp(from, to, s);
                Body.localRotation = Quaternion.Euler(a, 0, 0);
                // rotate around the middle so it turns in place
                Body.localPosition = Quaternion.Euler(a, 0, 0) * new Vector3(0, -JiggerHeight * 0.5f, 0) + new Vector3(0, JiggerHeight * 0.5f, 0);
            }
            Refresh(false);
        }

        /// <summary>Turns a double jigger over. Whatever was inside is lost.</summary>
        public void Flip(bool animate = true)
        {
            if (VKind != VesselKind.Jigger || SolidB == null) return;
            Flipped = !Flipped;
            SetSide(animate);
        }

        public void SetSide(bool animate)
        {
            Solid = Flipped ? SolidB : SolidA;
            NominalMl = Flipped ? NominalB : NominalA;
            C.Clear();
            if (Mouth != null)
            {
                Mouth.localPosition = new Vector3(0, Solid.RimY + 0.012f, 0);
                var bc = Mouth.GetComponent<BoxCollider>();
                bc.size = new Vector3(Solid.RimR * 1.9f, 0.028f, Solid.RimR * 1.9f);
            }
            flipAnim = animate ? 0f : 0.999f;
            if (!animate && Body != null)
            {
                Body.localRotation = Quaternion.Euler(Flipped ? 180f : 0f, 0, 0);
                Body.localPosition = Flipped ? new Vector3(0, JiggerHeight, 0) : Vector3.zero;
            }
            lastVersion = -1;
        }

        public void Refresh(bool force)
        {
            if (Solid == null) return;
            Vector3 up = transform.InverseTransformDirection(Vector3.up);
            bool moved = Vector3.Angle(up, lastUp) > 0.4f;
            bool changed = force || C.Version != lastVersion || moved;
            if (changed)
            {
                lastVersion = C.Version;
                lastUp = up;
                float vol = DisplayVolumeMl * 1e-6f * GeoScale;
                LevelN = up;
                if (vol <= 1e-7f)
                {
                    liquidR.enabled = false;
                    Level = Solid.SolveLevel(up, 0);
                }
                else
                {
                    Level = Solid.SolveLevel(up, vol);
                    Solid.Build(liquidMesh, up, Level, bV, bN, bT);
                    liquidR.enabled = true;
                    var col = C.MixColor();
                    Mats.SetColor(liquidMat, col);
                }
            }
            if (Ice != lastIce || (changed && Ice > 0)) LayoutIce();
            if (Garnishes.Count != lastGarnish) LayoutGarnish();

            // a few bubbles in fizzy drinks
            if (C.IsFizzy && liquidR.enabled && !Held)
            {
                bubbleTimer -= Time.deltaTime;
                if (bubbleTimer <= 0)
                {
                    bubbleTimer = Random.Range(0.08f, 0.25f);
                    Fx.Bubble(this);
                }
            }
        }

        /// <summary>Upright liquid surface height in local space (for floating ice).</summary>
        float UprightLevel => Vector3.Angle(LevelN, Vector3.up) < 25f ? Level / Mathf.Max(LevelN.y, 0.5f) : -1f;

        void LayoutIce()
        {
            lastIce = Ice;
            while (iceObjs.Count < Ice)
            {
                var go = Props.IceCube(IceSize);
                go.transform.SetParent(transform, false);
                iceObjs.Add(go.transform);
            }
            while (iceObjs.Count > Ice)
            {
                var t = iceObjs[iceObjs.Count - 1];
                iceObjs.RemoveAt(iceObjs.Count - 1);
                Destroy(t.gameObject);
            }
            if (Ice == 0) return;
            float s = IceSize;
            float bottom = Solid.BottomY;
            float rBottom = Solid.RadiusAt(bottom + s);
            int per = rBottom < 0.022f ? 1 : rBottom < 0.034f ? 2 : rBottom < 0.044f ? 3 : 4;
            float stackTop = bottom + s * 0.5f + ((Ice - 1) / per) * s * 0.9f + s * 0.5f;
            float lift = 0;
            float lvl = UprightLevel;
            if (lvl > 0 && C.Total > 10f) lift = Mathf.Clamp(lvl - stackTop - s * 0.1f, 0, Solid.RimY - stackTop - s * 0.3f);
            for (int i = 0; i < Ice; i++)
            {
                int layer = i / per, k = i % per;
                float y = bottom + s * 0.5f + layer * s * 0.9f + lift;
                float r = Solid.RadiusAt(y);
                float ang = Mathf.PI * 2f * k / per + layer * 0.9f;
                float rad = per == 1 ? 0 : Mathf.Max(0, r - s * 0.62f) * 0.85f;
                var t = iceObjs[i];
                t.localPosition = new Vector3(Mathf.Cos(ang) * rad, y, Mathf.Sin(ang) * rad);
                var rnd = new System.Random(i * 31 + 7);
                t.localRotation = Quaternion.Euler((float)rnd.NextDouble() * 30f - 15f, (float)rnd.NextDouble() * 360f, (float)rnd.NextDouble() * 30f - 15f);
            }
        }

        void LayoutGarnish()
        {
            lastGarnish = Garnishes.Count;
            for (int i = garnishRoot.childCount - 1; i >= 0; i--) Destroy(garnishRoot.GetChild(i).gameObject);
            for (int i = 0; i < Garnishes.Count; i++)
            {
                var g = Garnishes[i];
                var go = Props.GarnishMesh(g);
                go.transform.SetParent(garnishRoot, false);
                float a = (35f + i * 70f) * Mathf.Deg2Rad;
                var rim = new Vector3(Mathf.Cos(a) * Solid.RimR, Solid.RimY, Mathf.Sin(a) * Solid.RimR);
                switch (g)
                {
                    case Garnish.LimeWedge:
                        go.transform.localPosition = rim + Vector3.down * 0.006f;
                        go.transform.localRotation = Quaternion.LookRotation(new Vector3(rim.x, 0, rim.z).normalized) * Quaternion.Euler(0, -20f, 90f);
                        break;
                    case Garnish.LemonTwist:
                    case Garnish.OrangePeel:
                        go.transform.localPosition = rim * 0.85f + Vector3.up * 0.004f;
                        go.transform.localRotation = Quaternion.LookRotation(new Vector3(-rim.x, 0, -rim.z).normalized) * Quaternion.Euler(-35f, 0, 0);
                        break;
                    case Garnish.Cherry:
                        {
                            float y = UprightLevel > 0 && C.Total > 20 && Ice > 0 ? Mathf.Min(UprightLevel, Solid.RimY - 0.01f) : Solid.BottomY + 0.012f;
                            go.transform.localPosition = new Vector3(0.006f, y, 0.004f);
                            go.transform.localRotation = Quaternion.Euler(0, i * 50, 10);
                            break;
                        }
                    case Garnish.Mint:
                        go.transform.localPosition = new Vector3(0, Solid.RimY - 0.035f, 0);
                        go.transform.localRotation = Quaternion.Euler(0, 40f * i, 0);
                        break;
                    case Garnish.Olive:
                        go.transform.localPosition = rim * 0.9f + Vector3.up * 0.02f;
                        go.transform.localRotation = Quaternion.LookRotation((new Vector3(0, Solid.BottomY + 0.02f, 0) - (rim + Vector3.up * 0.02f)).normalized);
                        break;
                }
            }
        }

        public void AddIce(int n)
        {
            Ice = Mathf.Clamp(Ice + n, 0, MaxIce);
            C.Version++;
        }

        public void Empty()
        {
            C.Clear();
            Ice = 0;
            Garnishes.Clear();
            Muddle = 0;
        }

        /// <summary>
        /// Outflow when tilted: ml/s over the lowest point of the rim (weir flow).
        /// spout/dir are in world space.
        /// </summary>
        public float Outflow(out Vector3 spout, out Vector3 dir)
        {
            spout = transform.position; dir = Vector3.down;
            if (Cap != null || Solid == null) return 0;
            Vector3 n = transform.InverseTransformDirection(Vector3.up);
            Vector3 low = Solid.RimLow(n, out float rimH);
            Vector3 outward = new Vector3(low.x, 0, low.z).normalized;
            spout = transform.TransformPoint(low + outward * 0.002f);
            dir = (transform.TransformDirection(outward) * 0.8f + Vector3.down * 0.4f).normalized;
            if (C.Total < 0.05f) return 0;
            float excess = Level - rimH;
            if (excess <= 0) return 0;
            float width = Solid.RimR * 1.4f;
            float q = 1.7f * width * Mathf.Pow(excess, 1.5f) * 1e6f;
            if (Strainer != null) q *= 0.6f;
            return Mathf.Min(q, 260f);
        }
    }

    // ------------------------------------------------------------------ attachments and tools

    public sealed class ShakerCap : Item
    {
        public override string Title => Loc.S("Shaker cap (small tin)", "Viečko šejkra (malá plechovka)");
        public override string Info => Loc.S("Put it on the shaker tin to shake", "Nasaď na šejker a môžeš trepať");
    }

    public sealed class StrainerItem : Item
    {
        public override string Title => Loc.S("Hawthorne strainer", "Barové sitko (Hawthorne)");
        public override string Info => Loc.S("Put it on a tin or mixing glass, then pour", "Nasaď na šejker alebo miešací pohár a nalievaj");
        public override Quaternion HoldRotation => Quaternion.Euler(-60f, 0, 0);
    }

    public sealed class BarSpoon : Item
    {
        public override string Title => Loc.S("Bar spoon", "Barová lyžica");
        public override string Info => Loc.S("Hold RMB over a glass to stir", "Drž PTM nad pohárom a miešaj");
        public override Vector3 HoldOffset => new Vector3(0.16f, -0.3f, 0.45f);
        public override Quaternion HoldRotation => Quaternion.Euler(20f, 0, -10f);
    }

    public sealed class Muddler : Item
    {
        public override string Title => Loc.S("Muddler", "Drvič");
        public override string Info => Loc.S("Hold RMB over a glass with mint", "Drž PTM nad pohárom s mätou");
        public override Vector3 HoldOffset => new Vector3(0.16f, -0.28f, 0.45f);
        public override Quaternion HoldRotation => Quaternion.Euler(15f, 0, -10f);
    }

    public sealed class IceScoop : Item
    {
        public int Ice;
        public const int Capacity = 5;
        public Transform IceRoot;
        public override string Title => Loc.S("Ice scoop", "Lopatka na ľad");
        public override string Info => Ice > 0 ? Loc.S($"Ice ×{Ice}: click a glass or tin", $"Ľad ×{Ice}: klikni na pohár alebo šejker")
                                               : Loc.S("Click the ice well to scoop", "Klikni na ľad a naber");
        public override Vector3 HoldOffset => new Vector3(0.14f, -0.2f, 0.42f);
        public override Quaternion HoldRotation => Quaternion.Euler(-12f, 0, 0);

        public void SetIce(int n)
        {
            Ice = n;
            for (int i = IceRoot.childCount - 1; i >= 0; i--) Destroy(IceRoot.GetChild(i).gameObject);
            for (int i = 0; i < n; i++)
            {
                var c = Props.IceCube(0.024f);
                c.transform.SetParent(IceRoot, false);
                c.transform.localPosition = new Vector3((i % 2 - 0.5f) * 0.026f, 0.014f + (i / 2) * 0.006f, 0.02f + (i / 2) * 0.024f - 0.03f);
                c.transform.localRotation = Quaternion.Euler(i * 23, i * 47, i * 11);
            }
        }
    }

    public sealed class GarnishItem : Item
    {
        public Garnish G;
        public override string Title => Recipes.GarnishName(G);
        public override string Info => Loc.S("Click a glass to garnish it", "Klikni na pohár a ozdob ho");
        public override Vector3 HoldOffset => new Vector3(0.12f, -0.14f, 0.36f);
    }
}
