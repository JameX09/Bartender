using UnityEngine;

namespace Bartender
{
    /// <summary>Fixed things on the bar you click on (ice well, garnish tray, glass rack ...).</summary>
    public abstract class Station : MonoBehaviour
    {
        public abstract string Title { get; }
        /// <summary>Hint for what a click would do with the held item (null = nothing).</summary>
        public abstract string Hint(Item held);
        /// <summary>Returns true if the click did something.</summary>
        public abstract bool Use(Player p, Item held);
    }

    public sealed class IceWell : Station
    {
        public override string Title => Loc.S("Ice well", "Nádoba s ľadom");

        public override string Hint(Item held)
        {
            if (held is IceScoop) return Loc.S("LMB  Scoop ice", "ĽTM  Nabrať ľad");
            if (held == null) return Loc.S("Take the ice scoop first", "Najprv si vezmi lopatku na ľad");
            if (held is Vessel) return Loc.S("Use the ice scoop, never the glass", "Použi lopatku, nikdy nie pohár");
            return null;
        }

        public override bool Use(Player p, Item held)
        {
            if (held is IceScoop s)
            {
                if (s.Ice >= IceScoop.Capacity) { p.ShowToast(Loc.S("The scoop is already full", "Lopatka je plná")); return true; }
                p.StartScoop(transform.position);
                return true;
            }
            return false;
        }
    }

    public sealed class GarnishBin : Station
    {
        public Garnish G;
        public override string Title => Recipes.GarnishName(G);

        public override string Hint(Item held)
        {
            if (held == null) return Loc.S("LMB  Take one", "ĽTM  Vziať");
            if (held is GarnishItem) return Loc.S("LMB  Put it back", "ĽTM  Vrátiť");
            return null;
        }

        public override bool Use(Player p, Item held)
        {
            if (held == null)
            {
                var go = Props.GarnishMesh(G);
                var gi = go.AddComponent<GarnishItem>();
                gi.G = G;
                gi.Height = 0.03f;
                go.transform.position = transform.position + Vector3.up * 0.03f;
                p.Grab(gi);
                Sfx.Play("pick", transform.position);
                return true;
            }
            if (held is GarnishItem g)
            {
                p.Release();
                Destroy(g.gameObject);
                return true;
            }
            return false;
        }
    }

    /// <summary>Spot in the glass rack that always offers a fresh clean glass.</summary>
    public sealed class GlassSlot : Station
    {
        public GlassType Type;
        Vessel current;
        float respawn;

        public override string Title => Recipes.GlassName(Type);
        public override string Hint(Item held) => held == null ? Loc.S("LMB  Take a glass", "ĽTM  Vziať pohár") : null;

        void Start() => Spawn();

        void Update()
        {
            if (current != null && (current.Held || Vector3.Distance(current.transform.position, transform.position) > 0.08f))
            {
                current = null;
                respawn = 0.8f;
            }
            if (current == null)
            {
                respawn -= Time.deltaTime;
                if (respawn <= 0) Spawn();
            }
        }

        void Spawn()
        {
            current = Props.Glass(Type);
            current.transform.position = transform.position;
            current.transform.rotation = transform.rotation;
            current.SpawnTime = Time.time;
            Sfx.Play("glassSoft", transform.position, 0.4f);
        }

        public override bool Use(Player p, Item held)
        {
            if (held == null && current != null) { p.Grab(current); return true; }
            return false;
        }
    }

    public sealed class Sink : Station
    {
        public override string Title => Loc.S("Sink", "Drez");
        public override string Hint(Item held) => held is Vessel ? Loc.S("LMB  Empty and rinse", "ĽTM  Vyliať a opláchnuť") : null;

        public override bool Use(Player p, Item held)
        {
            if (held is Vessel v)
            {
                v.Empty();
                Sfx.Play("rinse", transform.position);
                Fx.Splash(transform.position + Vector3.up * 0.05f, new Color(0.8f, 0.9f, 1f, 0.6f), 10);
                return true;
            }
            return false;
        }
    }

    public sealed class BusTub : Station
    {
        public override string Title => Loc.S("Dirty glass tub", "Nádoba na špinavé poháre");
        public override string Hint(Item held) => held is Vessel v && v.VKind == VesselKind.Glass ? Loc.S("LMB  Throw away the glass", "ĽTM  Odložiť pohár") : null;

        public override bool Use(Player p, Item held)
        {
            if (held is Vessel v && v.VKind == VesselKind.Glass)
            {
                p.Release();
                Destroy(v.gameObject);
                Sfx.Play("glassClink", transform.position);
                return true;
            }
            return false;
        }
    }

    /// <summary>Coaster in front of a seat (career) or the tasting mat (free mode).</summary>
    public sealed class ServeSpot : MonoBehaviour
    {
        public int Seat = -1;   // -1 = tasting mat
        public static readonly System.Collections.Generic.List<ServeSpot> All = new System.Collections.Generic.List<ServeSpot>();
        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public static ServeSpot Near(Vector3 p, float max = 0.14f)
        {
            ServeSpot best = null;
            float bd = max;
            foreach (var s in All)
            {
                var d = new Vector2(s.transform.position.x - p.x, s.transform.position.z - p.z).magnitude;
                if (d < bd && Mathf.Abs(s.transform.position.y - p.y) < 0.05f) { bd = d; best = s; }
            }
            return best;
        }
    }

    /// <summary>Marks colliders you can put things down on.</summary>
    public sealed class Surface : MonoBehaviour { }

    /// <summary>Espresso machine: pulls a 30 ml shot into the cup under the group head.</summary>
    public sealed class EspressoMachine : Station
    {
        public Vector3 CupSpot;      // where the cup stands under the group head
        public Vector3 Spout;        // where the coffee runs out
        Vessel cup;
        float brew = -1f;
        LineRenderer stream;
        Material streamMat;
        const float ShotMl = 30f, BrewTime = 4.5f;

        public bool Brewing => brew >= 0f;
        public override string Title => Loc.S("Espresso machine", "Kávovar");

        public override string Hint(Item held)
        {
            if (Brewing) return Loc.S("Pulling a shot…", "Pripravuje sa espresso…");
            if (held == null) return Loc.S("LMB  Pull an espresso shot (30 ml)", "ĽTM  Pripraviť espresso (30 ml)");
            if (held is Vessel v && v.VKind == VesselKind.Cup) return Loc.S("LMB  Put the cup under and brew", "ĽTM  Položiť šálku a pripraviť espresso");
            return null;
        }

        void Awake()
        {
            var g = new GameObject("coffee stream");
            g.transform.SetParent(transform, false);
            stream = g.AddComponent<LineRenderer>();
            stream.useWorldSpace = true;
            stream.positionCount = 0;
            stream.widthMultiplier = 0.0028f;
            stream.numCapVertices = 3;
            stream.generateLightingData = true;
            streamMat = Mats.Liquid(new Color(0.2f, 0.1f, 0.04f, 0.95f));
            streamMat.renderQueue = 3020;
            stream.sharedMaterial = streamMat;
        }

        bool CupInPlace => cup != null && !cup.Held && Vector3.Distance(cup.transform.position, CupSpot) < 0.06f;

        public override bool Use(Player p, Item held)
        {
            if (Brewing) { p.ShowToast(Loc.S("Wait for the shot to finish", "Počkaj, kým dotečie")); return true; }
            if (held is Vessel hv && hv.VKind == VesselKind.Cup)
            {
                if (CupInPlace) { p.ShowToast(Loc.S("There is already a cup under the machine", "Pod kávovarom už šálka je")); return true; }
                p.Release();
                hv.transform.SetPositionAndRotation(CupSpot, Quaternion.Euler(0, 200f, 0));
                hv.SetColliders(true);
                cup = hv;
                Sfx.Play("glassSoft", CupSpot);
                StartBrew();
                return true;
            }
            if (held != null) return false;
            if (!CupInPlace)
            {
                cup = Props.EspressoCup();
                cup.transform.SetPositionAndRotation(CupSpot, Quaternion.Euler(0, 200f, 0));
                Sfx.Play("glassSoft", CupSpot);
            }
            if (cup.Ml > cup.CapacityMl - ShotMl) { p.ShowToast(Loc.S("The cup is full: take it", "Šálka je plná: vezmi si ju")); return true; }
            StartBrew();
            return true;
        }

        void StartBrew()
        {
            brew = 0f;
            Sfx.Play("grind", transform.position, 0.8f);
        }

        void Update()
        {
            if (!Brewing) { stream.positionCount = 0; return; }
            brew += Time.deltaTime;
            if (!CupInPlace) { brew = -1f; stream.positionCount = 0; return; }
            // grind first, then the pump pushes the shot through
            if (brew > 1.2f)
            {
                float ml = ShotMl / (BrewTime - 1.2f) * Time.deltaTime;
                cup.C.Add(Ingredients.Get("espresso"), ml);
                float wobble = Mathf.Sin(Time.time * 40f) * 0.0004f;
                stream.positionCount = 2;
                stream.SetPosition(0, Spout);
                var top = cup.transform.TransformPoint(new Vector3(0, cup.Level / Mathf.Max(0.5f, cup.LevelN.y), 0));
                stream.SetPosition(1, new Vector3(Spout.x + wobble, Mathf.Max(top.y, CupSpot.y + 0.01f), Spout.z));
                if (Mathf.Repeat(brew, 0.9f) < Time.deltaTime) Sfx.Play("brew", transform.position, 0.5f);
            }
            if (brew >= BrewTime)
            {
                brew = -1f;
                stream.positionCount = 0;
                Sfx.Play("bell", transform.position, 0.15f, 1.6f);
            }
        }
    }
}