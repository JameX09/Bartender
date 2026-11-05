using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Bartender
{
    /// <summary>
    /// First-person bartender with two hands: the right hand holds bottles, tins and tools,
    /// the left hand holds the jigger. Look, walk, pick up, pour, shake, stir, muddle, scoop ice.
    /// </summary>
    public sealed class Player : MonoBehaviour
    {
        public static Player I;
        public Camera Cam;
        public bool InputEnabled;
        public Item Held;               // right hand
        public Vessel Left;             // left hand (jigger)
        public Vessel Focus;            // vessel the recipe card compares against
        public System.Action<Item> OnPlaced;

        // walk area behind the bar
        public static readonly Rect Walk = new Rect(-2.85f, -1.32f, 5.7f, 0.6f);
        const float Eye = 1.68f;
        const float Reach = 1.75f;

        float yaw, pitch = 28f;
        Vector3 feet = new Vector3(0, 0, -0.95f);

        // hover state (read by the HUD)
        public Item HoverItem;
        public Station HoverStation;
        public Vessel HoverVessel;
        public bool HoverSurface;
        public Vector3 HoverPoint;
        public bool PlaceOk;
        bool hasHit;
        RaycastHit hit;

        // right hand pose
        Vector3 holdPos;
        Quaternion holdRot;
        float tilt;
        bool pourHeld, jiggerFull, pourAligned;
        Vector3 lockSpout;

        /// <summary>Pour assist on a jigger: the bottle locks over its centre and every drop goes in.</summary>
        bool JiggerLock => Save.PourAssist && pourHeld && pourTarget != null && pourTarget.VKind == VesselKind.Jigger;
        Vessel pourTarget;
        Vector3 pourPoint;
        float shakePhase, stirPhase, mudPhase;
        Vessel toolTarget;
        public bool Shaking, Stirring, Muddling, Pouring;
        public float FlowRate;
        float iceCarry, iceDumpTimer;

        // left hand pose
        Vector3 leftPos;
        Quaternion leftRot;
        Vessel leftTarget;
        float leftTilt, leftDone;
        public bool LeftPouring => leftTarget != null;

        // short animations (scooping ice, dropping ice)
        enum Anim { None, Scoop, DropIce }
        Anim anim;
        float animT, nextDrop;
        Vector3 animPoint;
        Vessel animTarget;
        struct Falling { public Transform T; public Vector3 P0, V; public float Age, Dur; public Vessel Dest; }
        readonly List<Falling> falling = new List<Falling>();

        // readout
        public string PourText;
        float pourTextTimer;
        Ingredient sessionIng;
        float sessionMl;
        Vessel sessionDest;

        public string Toast;
        public float ToastTime;

        // lets automated tests hold the right mouse button
        public bool DebugRmb;
        bool Rmb => DebugRmb || Input.GetMouseButton(1);
        public void DebugPour(Vessel target) { DebugRmb = true; pourHeld = true; jiggerFull = false; pourAligned = false; pourTarget = target; toolTarget = target; }
        public void DebugLeftPour(Vessel target) { leftTarget = target; leftTilt = 0; leftDone = 0; }
        public void DebugDrop(Vessel target) => StartDrop(target);

        LineRenderer stream, leftStream;
        Material streamMat, leftStreamMat;
        Transform marker;
        Material markerMat;
        Hands hands;

        void Awake()
        {
            I = this;
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            Cam = camGo.AddComponent<Camera>();
            Cam.nearClipPlane = 0.02f;
            Cam.farClipPlane = 2000f;
            Cam.fieldOfView = Save.Fov;
            camGo.AddComponent<AudioListener>();
            camGo.transform.SetParent(transform, false);

            stream = MakeStream("pour stream", out streamMat);
            leftStream = MakeStream("jigger stream", out leftStreamMat);

            var mg = Props.Part("place marker", transform, MeshGen.Disc(0.05f, 0.042f, 40), null, Vector3.zero, null, null, false);
            markerMat = Mats.Additive(new Color(1f, 0.85f, 0.55f, 0.5f));
            mg.GetComponent<MeshRenderer>().sharedMaterial = markerMat;
            marker = mg.transform;
            marker.gameObject.SetActive(false);

            hands = new GameObject("Hands").AddComponent<Hands>();
            hands.transform.SetParent(transform, false);
            hands.Init(Cam);
        }

        LineRenderer MakeStream(string name, out Material mat)
        {
            var sg = new GameObject(name);
            sg.transform.SetParent(transform, false);
            var lr = sg.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 0;
            lr.numCapVertices = 4;
            lr.numCornerVertices = 2;
            lr.alignment = LineAlignment.View;
            lr.generateLightingData = true;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mat = Mats.Liquid(Color.white);
            mat.renderQueue = 3020;
            lr.sharedMaterial = mat;
            return lr;
        }

        public void SetView(Vector3 feetPos, float yawDeg, float pitchDeg)
        {
            feet = feetPos; yaw = yawDeg; pitch = pitchDeg;
            ApplyCamera();
        }

        void ApplyCamera()
        {
            Cam.transform.position = feet + Vector3.up * Eye;
            Cam.transform.rotation = Quaternion.Euler(pitch, yaw, 0);
        }

        public void ShowToast(string s)
        {
            Toast = s;
            ToastTime = Time.time;
        }

        // ================================================================== frame

        void Update()
        {
            Cam.fieldOfView = Mathf.Lerp(Cam.fieldOfView, Save.Fov, Time.unscaledDeltaTime * 6f);
            if (!InputEnabled)
            {
                StopLoops();
                stream.positionCount = 0;
                leftStream.positionCount = 0;
                marker.gameObject.SetActive(false);
                Pouring = Shaking = Stirring = Muddling = false;
                hands.SetRight(false, Vector3.zero, Quaternion.identity, 0);
                hands.SetLeft(false, Vector3.zero, Quaternion.identity, 0);
                return;
            }
            float dt = Time.deltaTime;
            Look();
            Move(dt);
            ApplyCamera();
            Physics.SyncTransforms();
            UpdateHover();
            if (anim == Anim.None) Buttons();
            else if (!Rmb) { pourHeld = false; toolTarget = null; }
            UpdateHeld(dt);
            UpdateLeft(dt);
            Physics.SyncTransforms();
            Flow(dt);
            LeftFlow(dt);
            UpdateFalling(dt);
            UpdateMarker();
            UpdateHands();
            if (Time.time - pourTextTimer > 1.6f) PourText = null;
        }

        void Look()
        {
            if (Cursor.lockState != CursorLockMode.Locked) return;
            float s = 2.1f * Save.Sensitivity;
            float mx = Input.GetAxisRaw("Mouse X") * s;
            float my = Input.GetAxisRaw("Mouse Y") * s * (Save.InvertY ? -1 : 1);
            // steadier aim while pouring or stirring
            if (Pouring || Stirring || Muddling) { mx *= 0.45f; my *= 0.45f; }
            yaw += mx;
            pitch = Mathf.Clamp(pitch - my, -70f, 82f);
        }

        void Move(float dt)
        {
            var f = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
            var r = Quaternion.Euler(0, yaw, 0) * Vector3.right;
            Vector3 v = Vector3.zero;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) v += f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) v -= f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) v += r;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) v -= r;
            if (v.sqrMagnitude > 1) v.Normalize();
            feet += v * 2.1f * dt;
            feet.x = Mathf.Clamp(feet.x, Walk.xMin, Walk.xMax);
            feet.z = Mathf.Clamp(feet.z, Walk.yMin, Walk.yMax);
        }

        // ================================================================== hover

        void UpdateHover()
        {
            HoverItem = null; HoverStation = null; HoverVessel = null; HoverSurface = false; PlaceOk = false;
            var ray = new Ray(Cam.transform.position, Cam.transform.forward);
            hasHit = Physics.Raycast(ray, out hit, Reach, ~0, QueryTriggerInteraction.Ignore);
            if (!hasHit) return;
            HoverPoint = hit.point;
            var item = hit.collider.GetComponentInParent<Item>();
            if (item != null && !item.Held)
            {
                HoverItem = item;
                HoverVessel = item as Vessel;
                if (HoverVessel == null && item.AttachedTo is Vessel av) HoverVessel = av;
                return;
            }
            var st = hit.collider.GetComponentInParent<Station>();
            if (st != null) { HoverStation = st; return; }
            if (hit.collider.GetComponentInParent<Surface>() != null && hit.normal.y > 0.75f)
            {
                HoverSurface = true;
                var it = Held ?? (Item)Left;
                if (it != null) PlaceOk = CanPlace(it, hit.point);
            }
        }

        bool CanPlace(Item it, Vector3 p)
        {
            if (it is GarnishItem) return false;
            float r = Mathf.Max(0.012f, it.Radius * 0.85f);
            float h = (it is BarSpoon || it is Muddler) ? 0.03f : Mathf.Max(it.Height, r * 2.2f);
            var a = p + Vector3.up * (r + 0.004f);
            var b = p + Vector3.up * Mathf.Max(r + 0.005f, h - r);
            var hits = Physics.OverlapCapsule(a, b, r, ~0, QueryTriggerInteraction.Ignore);
            foreach (var c in hits)
            {
                if (c.GetComponentInParent<Surface>() != null && c.GetComponentInParent<Item>() == null) continue;
                return false;
            }
            return true;
        }

        void UpdateMarker()
        {
            var it = Held ?? (Item)Left;
            bool show = it != null && HoverSurface && !Pouring && !(it is GarnishItem) && anim == Anim.None;
            marker.gameObject.SetActive(show);
            if (!show) return;
            marker.position = HoverPoint + Vector3.up * 0.002f;
            float s = Mathf.Max(0.6f, it.Radius / 0.045f * 1.1f);
            marker.localScale = new Vector3(s, 1, s);
            Mats.SetColor(markerMat, PlaceOk ? new Color(1f, 0.85f, 0.55f, 0.55f) : new Color(1f, 0.25f, 0.2f, 0.55f));
        }

        // ================================================================== buttons

        void Buttons()
        {
            if (Input.GetMouseButtonDown(0)) Primary();
            if (Input.GetKeyDown(KeyCode.Q)) PutBack();
            if (Input.GetKeyDown(KeyCode.R)) FlipJigger();
            if (Input.GetKeyDown(KeyCode.X))
            {
                var v = Held as Vessel ?? (Held == null ? Left : null);
                if (v != null && (v.Ml > 0.5f || v.Ice > 0 || v.Garnishes.Count > 0))
                {
                    v.Empty();
                    Sfx.Play("rinse", v.transform.position, 0.6f);
                    ShowToast(Loc.S("Emptied", "Vyliate"));
                }
            }
            if (Input.GetKeyDown(KeyCode.E)) QuickSwap();
            if (Input.GetMouseButtonDown(1)) BeginSecondary();
            if (!Rmb) { pourHeld = false; toolTarget = null; jiggerFull = false; }
        }

        static bool IsJigger(Item it) => it is Vessel v && v.VKind == VesselKind.Jigger;

        void Primary()
        {
            if (LeftPouring) return;
            // jiggers always go to the left hand
            if (HoverItem != null && IsJigger(HoverItem) && Left == null && !HoverItem.Locked)
            {
                GrabLeft((Vessel)HoverItem);
                return;
            }
            // a full jigger in the left hand empties into whatever you click
            var target = HoverVessel;
            bool toolUse = Held is GarnishItem || Held is IceScoop || Held is ShakerCap || Held is StrainerItem;
            if (Left != null && Left.Ml > 0.5f && target != null && target != Left && !target.Held && !target.Locked && !toolUse)
            {
                if (target.Cap != null) { ShowToast(Loc.S("Take the cap off first", "Najprv daj dole viečko")); return; }
                leftTarget = target;
                leftTilt = 0;
                leftDone = 0;
                return;
            }
            if (Held == null)
            {
                if (HoverItem != null)
                {
                    if (HoverItem.Locked) { ShowToast(Loc.S("That drink belongs to a guest", "Tento drink patrí hosťovi")); return; }
                    Grab(HoverItem);
                }
                else if (HoverStation != null) HoverStation.Use(this, null);
                else if (Left != null && HoverSurface)
                {
                    if (PlaceOk) PlaceLeft(HoverPoint);
                    else ShowToast(Loc.S("No room there", "Tam nie je miesto"));
                }
                return;
            }
            if (HoverStation != null && HoverStation.Use(this, Held)) return;
            if (target != null && !target.Held && !target.Locked)
            {
                switch (Held)
                {
                    case GarnishItem g:
                        if (target.VKind != VesselKind.Glass) { ShowToast(Loc.S("Garnish goes on the serving glass", "Ozdoba patrí na pohár pre hosťa")); return; }
                        if (target.Garnishes.Count >= 3) { ShowToast(Loc.S("That's enough garnish", "Ozdôb je dosť")); return; }
                        target.Garnishes.Add(g.G);
                        target.C.Version++;
                        Focus = target;
                        Release();
                        Destroy(g.gameObject);
                        Sfx.Play("pick", target.transform.position, 0.7f);
                        return;
                    case IceScoop s:
                        if (s.Ice <= 0) { ShowToast(Loc.S("The scoop is empty: click the ice well", "Lopatka je prázdna: klikni na ľad")); return; }
                        if (target.MaxIce <= 0) { ShowToast(Loc.S("Ice doesn't go in there", "Tam ľad nepatrí")); return; }
                        if (target.Cap != null) { ShowToast(Loc.S("Take the cap off first", "Najprv daj dole viečko")); return; }
                        if (target.MaxIce - target.Ice <= 0) { ShowToast(Loc.S("It's full of ice", "Ľadu je už dosť")); return; }
                        StartDrop(target);
                        return;
                    case ShakerCap cap:
                        if (target.VKind != VesselKind.Tin) { ShowToast(Loc.S("The cap fits the shaker tin", "Viečko patrí na šejker")); return; }
                        if (target.Cap != null || target.Strainer != null) { ShowToast(Loc.S("Something is already on it", "Niečo už je nasadené")); return; }
                        AttachCap(target, cap);
                        return;
                    case StrainerItem str:
                        if (target.VKind != VesselKind.Tin && target.VKind != VesselKind.MixingGlass) { ShowToast(Loc.S("Strainers go on a shaker tin or mixing glass", "Sitko patrí na šejker alebo miešací pohár")); return; }
                        if (target.Cap != null || target.Strainer != null) { ShowToast(Loc.S("Something is already on it", "Niečo už je nasadené")); return; }
                        AttachStrainer(target, str);
                        return;
                }
            }
            if (HoverSurface)
            {
                if (PlaceOk) Place(Held, HoverPoint);
                else if (Held is GarnishItem) ShowToast(Loc.S("Click a glass to add the garnish", "Klikni na pohár a pridaj ozdobu"));
                else ShowToast(Loc.S("No room there", "Tam nie je miesto"));
            }
        }

        public void Grab(Item it)
        {
            if (it.AttachedTo is Vessel v)
            {
                if (v.Cap == it) v.Cap = null;
                if (v.Strainer == it) v.Strainer = null;
                it.AttachedTo = null;
                Sfx.Play("capOn", it.transform.position, 0.6f);
            }
            it.transform.SetParent(null, true);
            Held = it;
            it.Held = true;
            it.SetColliders(false);
            holdPos = it.transform.position;
            holdRot = it.transform.rotation;
            tilt = 0;
            if (it is Vessel hv) Focus = hv;
            if (!(it is GarnishItem)) Sfx.Play("pick", it.transform.position, 0.6f);
        }

        public void GrabLeft(Vessel jig)
        {
            jig.transform.SetParent(null, true);
            Left = jig;
            jig.Held = true;
            jig.SetColliders(false);
            // keep the opening "catchable" so you can pour into the jigger in your hand
            jig.Mouth.GetComponent<Collider>().enabled = true;
            leftPos = jig.transform.position;
            leftRot = jig.transform.rotation;
            Focus = jig;
            Sfx.Play("pick", jig.transform.position, 0.6f);
        }

        public void Release()
        {
            if (Held == null) return;
            Held.Held = false;
            Held = null;
            stream.positionCount = 0;
        }

        public void ReleaseAll()
        {
            Release();
            if (Left != null) { Left.Held = false; Left.SetColliders(true); Left = null; }
            leftTarget = null;
            leftStream.positionCount = 0;
            anim = Anim.None;
            foreach (var f in falling) if (f.T != null) Destroy(f.T.gameObject);
            falling.Clear();
        }

        void Place(Item it, Vector3 p)
        {
            Quaternion rot = Quaternion.Euler(0, yaw + 180f, 0);
            Vector3 pos = p;
            if (it is BarSpoon || it is Muddler)
            {
                rot = Quaternion.Euler(0, yaw + 90f, 0) * Quaternion.Euler(90f, 0, 0);
                pos += Vector3.up * it.Radius * 0.9f - rot * Vector3.up * (it.Height * 0.5f);
            }
            it.transform.SetPositionAndRotation(pos, rot);
            Release();
            it.SetColliders(true);
            PlaceSound(it);
            OnPlaced?.Invoke(it);
        }

        void PlaceLeft(Vector3 p)
        {
            var j = Left;
            j.transform.SetPositionAndRotation(p, Quaternion.Euler(0, yaw + 180f, 0));
            j.Held = false;
            j.SetColliders(true);
            Left = null;
            PlaceSound(j);
        }

        static void PlaceSound(Item it)
        {
            var p = it.transform.position;
            if (it is Bottle) Sfx.Play("bottlePlace", p);
            else if (it is Vessel v) Sfx.Play(v.VKind == VesselKind.Tin || v.VKind == VesselKind.Jigger || v.GType == GlassType.Mug && v.VKind == VesselKind.Glass ? "metalPlace" : "glassPlace", p);
            else Sfx.Play("metalPlace", p, 0.7f);
        }

        void PutBack()
        {
            if (Held == null)
            {
                if (Left != null && !LeftPouring)
                {
                    var j = Left;
                    if (j.HasHome) j.transform.SetPositionAndRotation(j.HomePos, j.HomeRot);
                    j.Held = false; j.SetColliders(true); Left = null;
                    PlaceSound(j);
                }
                return;
            }
            var it = Held;
            if (it is GarnishItem) { Release(); Destroy(it.gameObject); return; }
            if (it.HasHome)
            {
                it.transform.SetPositionAndRotation(it.HomePos, it.HomeRot);
                Release();
                it.SetColliders(true);
                PlaceSound(it);
                return;
            }
            if (it is Vessel v && (v.VKind == VesselKind.Glass || v.VKind == VesselKind.Cup))
            {
                if (v.Ml > 0.5f || v.Ice > 0) { ShowToast(Loc.S("Put the drink down on the bar first", "Najprv polož drink na bar")); return; }
                Release();
                Destroy(v.gameObject);
                Sfx.Play("glassSoft", transform.position);
            }
        }

        void FlipJigger()
        {
            Vessel j = Left ?? (IsJigger(Held) ? (Vessel)Held : IsJigger(HoverItem) ? (Vessel)HoverItem : null);
            if (j == null || j.Flipping || j == leftTarget) return;
            bool spilled = j.Ml > 0.5f;
            if (spilled) Fx.Splash(j.transform.TransformPoint(new Vector3(0, j.Solid.RimY, 0)), j.C.MixColor(), 8);
            j.Flip();
            Sfx.Play("flip", j.transform.position, 0.7f);
            ShowToast((spilled ? Loc.S("Spilled! ", "Vyliate! ") : "") + Loc.S($"{j.NominalMl:0} ml side up", $"Hore strana {j.NominalMl:0} ml"));
        }

        public void AttachCap(Vessel tin, ShakerCap cap)
        {
            Release();
            cap.transform.SetParent(tin.transform, false);
            cap.transform.localPosition = new Vector3(0, tin.Solid.RimY - 0.03f + cap.Height, 0.004f);
            cap.transform.localRotation = Quaternion.Euler(184f, 0, 0);
            cap.AttachedTo = tin;
            cap.SetColliders(true);
            tin.Cap = cap;
            Focus = tin;
            Sfx.Play("capOn", tin.transform.position);
        }

        void AttachStrainer(Vessel v, StrainerItem s)
        {
            Release();
            s.transform.SetParent(v.transform, false);
            s.transform.localPosition = new Vector3(0, v.Solid.RimY - 0.0045f, 0);
            s.transform.localRotation = Quaternion.identity;
            s.AttachedTo = v;
            s.SetColliders(true);
            v.Strainer = s;
            Focus = v;
            Sfx.Play("metalPlace", v.transform.position, 0.6f);
        }

        /// <summary>E while holding the tin: cap → strainer → open.</summary>
        void QuickSwap()
        {
            if (!(Held is Vessel v) || (v.VKind != VesselKind.Tin && v.VKind != VesselKind.MixingGlass)) return;
            var cap = FindAnyObjectByType<ShakerCap>();
            var str = FindAnyObjectByType<StrainerItem>();
            if (v.Cap != null)
            {
                var c = v.Cap;
                v.Cap = null; c.AttachedTo = null;
                c.transform.SetParent(null, true);
                if (c.HasHome) c.transform.SetPositionAndRotation(c.HomePos, c.HomeRot);
                Sfx.Play("capOn", v.transform.position, 0.6f);
                if (str != null && str.AttachedTo == null && !str.Held) AttachStrainer(v, str);
                Held = v; v.Held = true; v.SetColliders(false);
                ShowToast(Loc.S("Cap off, strainer on", "Viečko dole, sitko hore"));
                return;
            }
            if (v.Strainer != null)
            {
                var s = v.Strainer;
                v.Strainer = null; s.AttachedTo = null;
                s.transform.SetParent(null, true);
                if (s.HasHome) s.transform.SetPositionAndRotation(s.HomePos, s.HomeRot);
                Sfx.Play("metalPlace", v.transform.position, 0.6f);
                ShowToast(Loc.S("Strainer off", "Sitko dole"));
                return;
            }
            if (v.VKind == VesselKind.Tin && cap != null && cap.AttachedTo == null && !cap.Held)
            {
                AttachCap(v, cap);
                Held = v; v.Held = true; v.SetColliders(false);
                ShowToast(Loc.S("Cap on: hold RMB to shake", "Viečko hore: drž PTM a trepaj"));
            }
            else if (str != null && str.AttachedTo == null && !str.Held)
            {
                AttachStrainer(v, str);
                Held = v; v.Held = true; v.SetColliders(false);
            }
        }

        /// <summary>Pour assist: the open vessel whose mouth is closest to where you look.</summary>
        Vessel MagnetTarget()
        {
            var ray = new Ray(Cam.transform.position, Cam.transform.forward);
            Vessel best = null;
            float bd = float.MaxValue;
            foreach (var v in Vessel.All)
            {
                if (v == null || v == Held || v.Locked || v.Cap != null || v.Solid == null) continue;
                var m = v.transform.TransformPoint(new Vector3(0, v.Solid.RimY, 0));
                var to = m - ray.origin;
                float along = Vector3.Dot(to, ray.direction);
                if (along < 0.1f || along > Reach) continue;
                float off = Vector3.Cross(ray.direction, to).magnitude;
                float limit = v.VKind == VesselKind.Jigger ? 0.14f : 0.08f;
                if (off < limit && off < bd) { bd = off; best = v; }
            }
            return best;
        }

        void BeginSecondary()
        {
            if (Held == null) return;
            if (Held is BarSpoon || Held is Muddler)
            {
                toolTarget = HoverVessel != null && !HoverVessel.Held && HoverVessel.Cap == null ? HoverVessel : null;
                if (toolTarget == null && Save.PourAssist) toolTarget = MagnetTarget();
                if (toolTarget == Left) toolTarget = null;
                if (toolTarget == null) ShowToast(Held is BarSpoon ? Loc.S("Aim at a glass or mixing glass to stir", "Zamier na pohár alebo miešací pohár") : Loc.S("Aim at a glass to muddle", "Zamier na pohár"));
                return;
            }
            if (Held is Vessel v && v.Cap != null) return; // shaking, handled in UpdateHeld
            if (!Held.Pourable) return;
            pourHeld = true;
            jiggerFull = false;
            pourAligned = false;
            pourTarget = HoverVessel != null && HoverVessel != Held && !HoverVessel.Locked ? HoverVessel : null;
            if (pourTarget == null && Save.PourAssist) pourTarget = MagnetTarget();
            // nothing in sight but a jigger in the left hand: pour into the jigger
            if (pourTarget == null && Left != null && !LeftPouring) pourTarget = Left;
            pourPoint = hasHit ? hit.point : Cam.transform.position + Cam.transform.forward * 0.6f;
            sessionIng = null; sessionMl = 0; sessionDest = null;
        }

        // ================================================================== animations

        public void StartScoop(Vector3 wellPoint)
        {
            if (anim != Anim.None) return;
            anim = Anim.Scoop;
            animT = 0;
            animPoint = wellPoint + new Vector3(Random.Range(-0.25f, 0.25f), 0, Random.Range(-0.06f, 0.06f));
        }

        void StartDrop(Vessel target)
        {
            if (anim != Anim.None) return;
            anim = Anim.DropIce;
            animT = 0;
            animTarget = target;
            nextDrop = 0.32f;
            Focus = target;
        }

        void UpdateFalling(float dt)
        {
            for (int i = falling.Count - 1; i >= 0; i--)
            {
                var f = falling[i];
                f.Age += dt;
                if (f.T != null) f.T.position = f.P0 + f.V * f.Age + 0.5f * Physics.gravity * f.Age * f.Age;
                if (f.T != null) f.T.Rotate(260f * dt, 410f * dt, 0);
                if (f.Age >= f.Dur)
                {
                    if (f.Dest != null && f.Dest.Ice < f.Dest.MaxIce) f.Dest.AddIce(1);
                    if (f.Dest != null) Sfx.Play("clink", f.Dest.transform.position, 0.5f, Random.Range(0.9f, 1.15f));
                    if (f.T != null) Destroy(f.T.gameObject);
                    falling.RemoveAt(i);
                    continue;
                }
                falling[i] = f;
            }
        }

        // ================================================================== right hand motion

        void UpdateHeld(float dt)
        {
            Shaking = Stirring = Muddling = false;
            if (Held == null) { tilt = 0; anim = Anim.None; return; }
            var it = Held;
            var camT = Cam.transform;
            Quaternion yawQ = Quaternion.Euler(0, yaw, 0);
            Vector3 rightH = yawQ * Vector3.right;
            Vector3 fwdH = yawQ * Vector3.forward;
            Vector3 desiredPos;
            Quaternion desiredRot = yawQ * it.HoldRotation;
            bool rmb = Rmb;
            float follow = 16f;

            var vessel = it as Vessel;
            if (anim != Anim.None && it is IceScoop scoop)
            {
                animT += dt;
                Vector3 mouthLocal = new Vector3(0, 0.037f, 0.06f);
                if (anim == Anim.Scoop)
                {
                    // reach in, dig, lift
                    float dig = animT < 0.3f ? animT / 0.3f : animT < 0.55f ? 1f : Mathf.Max(0, 1f - (animT - 0.55f) / 0.3f);
                    desiredRot = yawQ * Quaternion.Euler(Mathf.Lerp(-10f, 35f, dig) - (animT > 0.42f && animT < 0.6f ? 30f : 0f), 0, 0);
                    desiredPos = Vector3.Lerp(camT.position + rightH * 0.14f - Vector3.up * 0.2f + fwdH * 0.42f, animPoint + Vector3.up * 0.015f, dig) - desiredRot * mouthLocal * dig;
                    follow = 18f;
                    if (animT >= 0.45f && scoop.Ice < IceScoop.Capacity)
                    {
                        scoop.SetIce(IceScoop.Capacity);
                        Sfx.Play("scoop", animPoint);
                    }
                    if (animT > 0.9f) anim = Anim.None;
                }
                else
                {
                    var t = animTarget;
                    if (t == null || t.Held) { anim = Anim.None; return; }
                    var mouth = t.transform.TransformPoint(new Vector3(0, t.Solid.RimY, 0));
                    float tip = Mathf.Clamp01((animT - 0.15f) / 0.2f);
                    desiredRot = yawQ * Quaternion.Euler(Mathf.Lerp(-10f, 62f, tip), 0, 0);
                    desiredPos = mouth + Vector3.up * 0.075f - fwdH * 0.01f - desiredRot * mouthLocal;
                    follow = 14f;
                    if (animT >= nextDrop && scoop.Ice > 0 && t.Ice + CountFalling(t) < t.MaxIce)
                    {
                        nextDrop = animT + 0.075f;
                        scoop.SetIce(scoop.Ice - 1);
                        var cube = Props.IceCube(0.024f).transform;
                        var p0 = scoop.transform.TransformPoint(mouthLocal);
                        var p1 = mouth + new Vector3(Random.Range(-0.01f, 0.01f), -0.03f, Random.Range(-0.01f, 0.01f));
                        const float dur = 0.2f;
                        var v0 = (p1 - p0) / dur - 0.5f * Physics.gravity * dur;
                        cube.position = p0;
                        falling.Add(new Falling { T = cube, P0 = p0, V = v0, Age = 0, Dur = dur, Dest = t });
                    }
                    bool done = scoop.Ice == 0 || t.Ice + CountFalling(t) >= t.MaxIce;
                    if (done && animT > nextDrop + 0.25f) anim = Anim.None;
                }
            }
            else if (vessel != null && vessel.Cap != null && rmb)
            {
                // shaking over the shoulder with both hands
                Shaking = true;
                float rate = 6.45f;
                shakePhase += dt * rate * Mathf.PI * 2f;
                float s = Mathf.Sin(shakePhase);
                desiredPos = camT.position + rightH * 0.06f + Vector3.up * (-0.2f + s * 0.06f) + fwdH * (0.4f + Mathf.Cos(shakePhase) * 0.03f);
                desiredRot = yawQ * Quaternion.Euler(-70f + s * 16f, 0, 6f);
                follow = 40f;
                float k = dt / (Save.Has(Upgrade.ProShaker) ? 1.8f : 3f);
                if (vessel.Ml > 0.5f)
                {
                    float before = vessel.C.Shake;
                    vessel.C.Shake += k;
                    if (vessel.Ice > 0)
                    {
                        vessel.C.Chill = Mathf.Min(1f, vessel.C.Chill + k * 1.25f);
                        vessel.C.Dilute(vessel.C.Spirit * (before < 1.2f ? 0.2f : 0.05f) * k);
                    }
                    vessel.C.Version++;
                }
                tilt = 0;
                Focus = vessel;
            }
            else if ((it is BarSpoon || it is Muddler) && rmb && toolTarget != null && Vector3.Distance(toolTarget.transform.position, camT.position) < Reach + 0.2f)
            {
                var tv = toolTarget;
                float r = tv.Solid.RadiusAt(tv.Solid.BottomY + 0.02f);
                if (it is BarSpoon)
                {
                    Stirring = true;
                    stirPhase += dt * Mathf.PI * 2f * 2.1f;
                    var local = new Vector3(Mathf.Cos(stirPhase) * r * 0.45f, tv.Solid.BottomY + 0.008f, Mathf.Sin(stirPhase) * r * 0.45f);
                    desiredPos = tv.transform.TransformPoint(local);
                    desiredRot = Quaternion.LookRotation(fwdH, Vector3.up) * Quaternion.Euler(-8f, 0, 6f);
                    follow = 30f;
                    if (tv.Ml > 0.5f)
                    {
                        float k = dt / 4f;
                        float before = tv.C.Stir;
                        tv.C.Stir += k;
                        if (tv.Ice > 0)
                        {
                            tv.C.Chill = Mathf.Min(1f, tv.C.Chill + k * 1.15f);
                            tv.C.Dilute(tv.C.Spirit * (before < 1.2f ? 0.14f : 0.04f) * k);
                        }
                        tv.C.Version++;
                    }
                }
                else
                {
                    Muddling = true;
                    float prev = mudPhase;
                    mudPhase += dt * Mathf.PI * 2f * 1.6f;
                    float lift = Mathf.Abs(Mathf.Sin(mudPhase)) * 0.035f;
                    desiredPos = tv.transform.TransformPoint(new Vector3(0, tv.Solid.BottomY + 0.004f + lift, 0));
                    desiredRot = Quaternion.LookRotation(fwdH, Vector3.up);
                    follow = 30f;
                    if (Mathf.Floor(prev / Mathf.PI) != Mathf.Floor(mudPhase / Mathf.PI)) Sfx.Play("muddle", tv.transform.position, 0.7f);
                    if (tv.Garnishes.Contains(Garnish.Mint)) tv.Muddle = Mathf.Min(1.5f, tv.Muddle + dt / 1.6f);
                }
                Focus = tv;
            }
            else if (it.Pourable && pourHeld && rmb && !jiggerFull)
            {
                // pouring: move the spout over the target and tilt forward
                bool slow = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                // pouring into a jigger is naturally a little more careful
                bool careful = slow || (pourTarget != null && pourTarget.VKind == VesselKind.Jigger);
                bool jigLock = JiggerLock;
                float maxTilt = slow ? (it is Bottle ? 110f : 100f) : careful ? (it is Bottle ? 128f : 112f) : (it is Bottle ? 150f : 140f);
                float speed = slow ? 45f : careful ? 80f : 115f;
                if (jigLock && !pourAligned)
                {
                    // first line the spout up over the jigger, only then start tipping
                    float preTilt = it is Bottle b0 && b0.Ing.Style == BottleStyle.Carafe ? 40f : 60f;
                    tilt = Mathf.MoveTowards(tilt, Mathf.Min(maxTilt, preTilt), speed * dt);
                }
                else tilt = Mathf.MoveTowards(tilt, maxTilt, speed * dt);
                tilt = Mathf.Clamp(tilt + Input.GetAxis("Mouse ScrollWheel") * 60f, 0, 165f);
                Vector3 aim;
                float clearance;
                if (pourTarget != null)
                {
                    aim = pourTarget.transform.TransformPoint(new Vector3(0, pourTarget.Solid.RimY, 0));
                    clearance = it is Bottle ? (pourTarget.VKind == VesselKind.Jigger ? 0.045f : 0.075f) : 0.05f;
                }
                else { aim = pourPoint; clearance = 0.16f; }
                // keep it within arm's reach
                var toAim = aim - camT.position;
                if (toAim.magnitude > 1.25f) aim = camT.position + toAim.normalized * 1.25f;
                // pour from the right side: the spout points left, the item tips around the view axis
                Quaternion pq = yawQ * Quaternion.Euler(0, -90f, 0);
                Vector3 pourDir = pq * Vector3.forward;   // = to the left
                Vector3 pourAxis = pq * Vector3.right;    // = forward
                desiredRot = Quaternion.AngleAxis(tilt, pourAxis) * pq * it.HoldRotation;
                Vector3 spoutLocal = it is Bottle b ? b.SpoutLocal : new Vector3(0, vessel.Solid.RimY, vessel.Solid.RimR);
                // the stream leaves with some sideways speed; aim a little short so it lands in the middle
                float comp = jigLock ? 0f : it is Bottle ? 0.032f : 0.012f;
                Vector3 spoutTarget = aim + Vector3.up * clearance - pourDir * comp * Mathf.Clamp01((tilt - 60f) / 60f);
                desiredPos = spoutTarget - desiredRot * spoutLocal;
                follow = jigLock ? 26f : 12f;
                lockSpout = spoutTarget;
                spoutLocalNow = spoutLocal;
            }
            else
            {
                tilt = Mathf.MoveTowards(tilt, 0, 300f * dt);
                Vector3 off = it.HoldOffset;
                desiredPos = camT.position + rightH * off.x + Vector3.up * off.y + fwdH * off.z;
                // follow the view a little when looking up or down
                desiredPos += Vector3.up * Mathf.Clamp(-pitch * 0.004f, -0.12f, 0.1f);
                if (tilt > 0.5f) desiredRot = Quaternion.AngleAxis(tilt, rightH) * desiredRot;
            }
            float a = 1f - Mathf.Exp(-dt * follow);
            holdPos = Vector3.Lerp(holdPos, desiredPos, a);
            holdRot = Quaternion.Slerp(holdRot, desiredRot, a);
            it.transform.SetPositionAndRotation(holdPos, holdRot);
            if (vessel != null) vessel.Refresh(false);
            // locked once the spout sits right over the jigger; stays locked for the rest of the pour
            if (JiggerLock) { if (!pourAligned && Vector3.Distance(it.transform.TransformPoint(spoutLocalNow), lockSpout) < 0.012f) pourAligned = true; }
            else pourAligned = false;
        }

        Vector3 spoutLocalNow;

        int CountFalling(Vessel v)
        {
            int n = 0;
            foreach (var f in falling) if (f.Dest == v) n++;
            return n;
        }

        // ================================================================== left hand motion

        void UpdateLeft(float dt)
        {
            if (Left == null) { leftTarget = null; return; }
            var camT = Cam.transform;
            Quaternion yawQ = Quaternion.Euler(0, yaw, 0);
            Vector3 rightH = yawQ * Vector3.right;
            Vector3 fwdH = yawQ * Vector3.forward;
            Vector3 desiredPos;
            Quaternion desiredRot = yawQ;
            float follow = 14f;
            if (leftTarget != null)
            {
                if (leftTarget.Held && leftTarget != Held) { leftTarget = null; return; }
                // tip the jigger over the target in one smooth motion
                leftTilt = Mathf.MoveTowards(leftTilt, 150f, 260f * dt);
                var mouth = leftTarget.transform.TransformPoint(new Vector3(0, leftTarget.Solid.RimY, 0));
                // the left hand tips the jigger in from the left side
                Quaternion lq = yawQ * Quaternion.Euler(0, 90f, 0);
                desiredRot = Quaternion.AngleAxis(leftTilt, lq * Vector3.right) * lq;
                var rim = new Vector3(0, Left.Solid.RimY, Left.Solid.RimR);
                desiredPos = mouth + Vector3.up * 0.03f - (lq * Vector3.forward) * 0.004f - desiredRot * rim;
                follow = 16f;
                if (Left.Ml < 0.2f) leftDone += dt;
                if (leftDone > 0.3f) { leftTarget = null; leftTilt = 0; }
            }
            else if (pourHeld && pourTarget == Left && Rmb)
            {
                // hold the jigger out in front so the bottle can pour into it
                desiredPos = camT.position + fwdH * 0.42f - rightH * 0.05f + Vector3.up * -0.27f;
                follow = 10f;
            }
            else
            {
                desiredPos = camT.position + rightH * -0.15f + Vector3.up * (-0.2f + Mathf.Clamp(-pitch * 0.004f, -0.12f, 0.1f)) + fwdH * 0.4f;
            }
            float a = 1f - Mathf.Exp(-dt * follow);
            leftPos = Vector3.Lerp(leftPos, desiredPos, a);
            leftRot = Quaternion.Slerp(leftRot, desiredRot, a);
            Left.transform.SetPositionAndRotation(leftPos, leftRot);
            Left.Refresh(false);
        }

        void LeftFlow(float dt)
        {
            if (Left == null || leftTarget == null) { leftStream.positionCount = 0; return; }
            float flow = Left.Outflow(out var spout, out var dir);
            if (flow <= 0.01f) { leftStream.positionCount = 0; return; }
            var col = Left.C.MixColor();
            Mats.SetColor(leftStreamMat, new Color(col.r, col.g, col.b, Mathf.Max(0.35f, col.a)));
            leftStream.widthMultiplier = Mathf.Lerp(0.003f, 0.008f, Mathf.Clamp01(flow / 60f));
            var dest = TraceStream(leftStream, Left, spout, dir * Mathf.Min(0.6f, 0.2f + flow * 0.004f), out var end, out bool spilled);
            // pour assist: a jigger tipped right over the glass always lands in it
            if (dest == null && Save.PourAssist && leftTarget != null)
            {
                dest = leftTarget;
                spilled = false;
                var m = leftTarget.transform.TransformPoint(new Vector3(0, Mathf.Max(leftTarget.Solid.BottomY, leftTarget.Level / Mathf.Max(0.5f, leftTarget.LevelN.y)), 0));
                leftStream.positionCount = 2;
                leftStream.SetPosition(0, spout);
                leftStream.SetPosition(1, new Vector3(spout.x, m.y, spout.z));
                end = m;
            }
            var part = Left.C.Take(Mathf.Min(flow * dt, Left.Ml));
            if (dest != null) { dest.C.Merge(part, false); Overflow(dest); Focus = dest; }
            if (spilled && Random.value < dt * 30f) Fx.Splash(end, col, 1);
            PourText = "+" + (dest != null ? dest.Ml.ToString("0") + " ml  →  " + dest.Title : Loc.S("spilling", "rozlievaš"));
            pourTextTimer = Time.time;
            Sfx.SetLoop(Sfx.Pour, 0.6f, 1.1f, end);
        }

        // ================================================================== hands

        void UpdateHands()
        {
            if (Held != null)
            {
                Hands.Grip(Held, out var local, out var lrot, out float r);
                var t = Held.transform;
                var rot = Held is Bottle || Held is Vessel ? FaceHand(t.rotation, false) : t.rotation * lrot;
                hands.SetRight(true, t.TransformPoint(local), rot, r);
            }
            else hands.SetRight(false, Vector3.zero, Quaternion.identity, 0);

            if (Left != null)
            {
                Hands.Grip(Left, out var local, out var lrot, out float r);
                var t = Left.transform;
                hands.SetLeft(true, t.TransformPoint(local), FaceHand(t.rotation, true), r);
            }
            else if (Shaking && Held is Vessel tin && tin.Cap != null)
            {
                // the left hand caps the top while shaking
                var t = tin.transform;
                hands.SetLeft(true, t.TransformPoint(new Vector3(0, tin.Solid.RimY + 0.07f, 0)), t.rotation * Quaternion.Euler(0, 180f, 0), 0.04f);
            }
            else hands.SetLeft(false, Vector3.zero, Quaternion.identity, 0);
        }

        /// <summary>
        /// For round items: keep the item axis but turn the hand around it so the palm sits on the side
        /// the arm comes from (right side for the right hand, left for the left), slightly towards the camera.
        /// </summary>
        Quaternion FaceHand(Quaternion itemRot, bool left)
        {
            var ct = Cam.transform;
            Vector3 axis = itemRot * Vector3.up;
            Vector3 want = (left ? -ct.right : ct.right) * 0.85f - ct.forward * 0.5f;
            want = Vector3.ProjectOnPlane(want, axis);
            if (want.sqrMagnitude < 1e-4f) return itemRot;
            want.Normalize();
            // the left hand is mirrored, so its palm is on local -X: point local +X the other way
            Vector3 right = left ? -want : want;
            return Quaternion.LookRotation(Vector3.Cross(right, axis), axis);
        }

        // ================================================================== liquid flow (right hand)

        void Flow(float dt)
        {
            Pouring = false;
            FlowRate = 0;
            float flow = 0;
            Vector3 spout = Vector3.zero, dir = Vector3.down;
            Color col = Color.white;
            Bottle bottle = Held as Bottle;
            Vessel src = Held as Vessel;
            if (bottle != null)
            {
                float t = Vector3.Angle(bottle.transform.up, Vector3.up);
                flow = bottle.FlowAt(t);
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) flow *= 0.33f;
                spout = bottle.transform.TransformPoint(bottle.SpoutLocal);
                dir = bottle.transform.TransformDirection(new Vector3(0, 1f, 0.35f).normalized);
                col = bottle.Ing.Color;
                col.a = Mathf.Lerp(0.25f, 0.95f, bottle.Ing.Alpha);
            }
            else if (src != null)
            {
                flow = src.Outflow(out spout, out dir) * Mathf.Lerp(0.6f, 1f, Mathf.Clamp01(Save.PourSpeed));
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) flow *= 0.4f;
                col = src.C.MixColor();
                // ice tumbles out of an unstrained tin once the liquid is gone
                if (src.Ice > 0 && src.Strainer == null && src.Ml < 1f && Vector3.Angle(src.transform.up, Vector3.up) > 105f)
                {
                    iceDumpTimer -= dt;
                    if (iceDumpTimer <= 0)
                    {
                        iceDumpTimer = 0.18f;
                        var dest0 = TraceStream(stream, src, spout, dir * 0.3f, out _, out _);
                        src.AddIce(-1);
                        if (dest0 != null && dest0.MaxIce > dest0.Ice) dest0.AddIce(1);
                        Sfx.Play("iceDrop", spout, 0.6f);
                    }
                }
            }
            if (flow <= 0.01f)
            {
                stream.positionCount = 0;
                if (!LeftPouring) StopLoops();
                return;
            }
            Pouring = true;
            FlowRate = flow;
            Vector3 v0 = dir * Mathf.Min(0.95f, 0.22f + flow * 0.006f);
            Vessel dest;
            Vector3 end;
            bool spilled;
            if (JiggerLock && pourAligned)
            {
                // locked on a jigger: a clean stream straight into the cup, nothing spills
                dest = pourTarget;
                spilled = false;
                end = LockedStream(stream, spout, v0, dest);
                // ease off near the brim so it stops neatly
                float room0 = dest.CapacityMl - dest.Ml;
                flow *= Mathf.Clamp(room0 / Mathf.Max(6f, dest.CapacityMl * 0.25f), 0.22f, 1f);
            }
            else if (JiggerLock)
            {
                stream.positionCount = 0;
                return;
            }
            else dest = TraceStream(stream, Held, spout, v0, out end, out spilled);
            float ml = flow * dt;
            // pour assist: stop exactly at the brim of a jigger
            if (dest != null && dest.VKind == VesselKind.Jigger && Save.PourAssist)
            {
                float room = dest.CapacityMl - dest.Ml;
                if (room <= 0.05f && !jiggerFull)
                {
                    jiggerFull = true;
                    Sfx.Play("clink", dest.transform.position, 0.4f, 1.6f);
                    ShowToast(Loc.S($"Jigger full: {dest.CapacityMl:0} ml. Click a glass or tin to pour it in", $"Džiger plný: {dest.CapacityMl:0} ml. Klikni na pohár alebo šejker a vylej ho"));
                }
                ml = Mathf.Min(ml, Mathf.Max(0, room));
            }
            stream.widthMultiplier = Mathf.Lerp(0.0025f, 0.009f, Mathf.Clamp01(flow / 60f));
            Mats.SetColor(streamMat, new Color(col.r, col.g, col.b, Mathf.Max(0.35f, col.a)));

            if (bottle != null)
            {
                if (dest != null) { dest.C.Add(bottle.Ing, ml); Overflow(dest); Focus = dest; }
                if (sessionIng != bottle.Ing || sessionDest != dest) { sessionIng = bottle.Ing; sessionMl = 0; sessionDest = dest; }
                sessionMl += ml;
                PourText = (dest != null ? "+" : Loc.S("Spilling ", "Rozlievaš ")) + sessionMl.ToString("0") + " ml  " + bottle.Ing.Name +
                           (dest != null ? "  →  " + dest.Title + "  (" + dest.Ml.ToString("0") + " ml)" : "");
                pourTextTimer = Time.time;
            }
            else if (src != null)
            {
                bool unstrained = src.Strainer == null && src.Ice > 0 && (src.VKind == VesselKind.Tin || src.VKind == VesselKind.MixingGlass);
                var part = src.C.Take(Mathf.Min(ml, src.Ml));
                if (dest != null) { dest.C.Merge(part, unstrained); Overflow(dest); Focus = dest; }
                if (unstrained)
                {
                    iceCarry += ml;
                    while (iceCarry > 28f && src.Ice > 0)
                    {
                        iceCarry -= 28f;
                        src.AddIce(-1);
                        if (dest != null && dest.MaxIce > dest.Ice) dest.AddIce(1);
                        Sfx.Play("iceDrop", spout, 0.5f);
                    }
                }
                if (sessionDest != dest) { sessionMl = 0; sessionDest = dest; }
                sessionMl += ml;
                PourText = (dest != null ? "+" : Loc.S("Spilling ", "Rozlievaš ")) + sessionMl.ToString("0") + " ml" +
                           (dest != null ? "  →  " + dest.Title + "  (" + dest.Ml.ToString("0") + " ml)" : "");
                pourTextTimer = Time.time;
            }
            if (spilled && Random.value < dt * 30f) Fx.Splash(end, new Color(col.r, col.g, col.b, 0.8f), 1);
            // sound: pitch rises as the glass fills
            float fill = dest != null ? Mathf.Clamp01(dest.Ml / Mathf.Max(30f, dest.CapacityMl)) : 0.2f;
            Sfx.SetLoop(Sfx.Pour, Mathf.Clamp01(flow / 35f) * 0.85f, 0.8f + fill * 0.7f, end);
            bool fizzy = bottle != null ? bottle.Ing.Fizzy : src != null && src.C.IsFizzy;
            Sfx.SetLoop(Sfx.Fizz, fizzy ? 0.6f : 0f, 1f, end);
        }

        void Overflow(Vessel v)
        {
            float iceMl = v.Ice * v.IceSize * v.IceSize * v.IceSize * 0.8e6f;
            float cap = v.CapacityMl;
            if (v.Ml + iceMl > cap)
            {
                v.C.Take(v.Ml + iceMl - cap);
                if (Random.value < 0.3f) Fx.Splash(v.transform.TransformPoint(new Vector3(0, v.Solid.RimY, v.Solid.RimR)), v.C.MixColor(), 2);
                if (Time.time - ToastTime > 2f && v.VKind != VesselKind.Jigger) ShowToast(Loc.S("It's overflowing!", "Pretečie to!"));
            }
        }

        readonly List<Vector3> streamPts = new List<Vector3>(48);

        /// <summary>Smooth arc from the spout down to the liquid surface in the centre of the vessel.</summary>
        Vector3 LockedStream(LineRenderer lr, Vector3 spout, Vector3 v0, Vessel dest)
        {
            float level = Mathf.Max(dest.Solid.BottomY, dest.Level / Mathf.Max(0.5f, dest.LevelN.y));
            var surface = dest.transform.TransformPoint(new Vector3(0, level, 0));
            var mouth = dest.transform.TransformPoint(new Vector3(0, dest.Solid.RimY, 0));
            // control point: a little along the spout direction, then it falls into the centre
            var ctrl = spout + v0.normalized * 0.012f + Vector3.down * 0.01f;
            ctrl = new Vector3(Mathf.Lerp(ctrl.x, mouth.x, 0.5f), ctrl.y, Mathf.Lerp(ctrl.z, mouth.z, 0.5f));
            const int n = 14;
            lr.positionCount = n + 2;
            for (int i = 0; i <= n; i++)
            {
                float t = (float)i / n;
                var p = (1 - t) * (1 - t) * spout + 2 * (1 - t) * t * ctrl + t * t * mouth;
                lr.SetPosition(i, p);
            }
            lr.SetPosition(n + 1, new Vector3(mouth.x, Mathf.Min(mouth.y, surface.y + 0.002f), mouth.z));
            return surface;
        }

        /// <summary>Simulates the falling stream and returns the vessel it lands in (if any).</summary>
        Vessel TraceStream(LineRenderer lr, Item src, Vector3 p, Vector3 v, out Vector3 end, out bool spilled)
        {
            streamPts.Clear();
            streamPts.Add(p);
            const float step = 0.01f;
            Vessel dest = null;
            spilled = false;
            end = p;
            for (int i = 0; i < 60; i++)
            {
                Vector3 nv = v + Physics.gravity * step;
                Vector3 np = p + (v + nv) * 0.5f * step;
                Vector3 seg = np - p;
                if (Physics.Raycast(p, seg.normalized, out var h, seg.magnitude, ~0, QueryTriggerInteraction.Collide))
                {
                    var mouth = h.collider.GetComponent<Mouth>();
                    if (mouth != null && mouth.Owner != src && mouth.Owner.Cap == null)
                    {
                        dest = mouth.Owner;
                        // let the stream continue down to the liquid surface visually
                        var bottomW = dest.transform.TransformPoint(new Vector3(0, Mathf.Max(dest.Solid.BottomY, dest.Level / Mathf.Max(0.5f, dest.LevelN.y)), 0));
                        streamPts.Add(h.point);
                        streamPts.Add(new Vector3(h.point.x, Mathf.Min(h.point.y, bottomW.y + 0.002f), h.point.z));
                        end = streamPts[streamPts.Count - 1];
                        break;
                    }
                    if (h.collider.isTrigger) { p = np; v = nv; streamPts.Add(p); continue; }
                    streamPts.Add(h.point);
                    end = h.point;
                    spilled = true;
                    break;
                }
                p = np; v = nv;
                streamPts.Add(p);
                end = p;
                if (i == 59) spilled = true;
            }
            lr.positionCount = streamPts.Count;
            for (int i = 0; i < streamPts.Count; i++) lr.SetPosition(i, streamPts[i]);
            return dest;
        }

        void StopLoops()
        {
            Sfx.SetLoop(Sfx.Pour, 0, 1, transform.position);
            Sfx.SetLoop(Sfx.Fizz, 0, 1, transform.position);
            Sfx.SetLoop(Sfx.Shake, Shaking ? 1f : 0f, 1, Cam != null ? Cam.transform.position : transform.position);
            Sfx.SetLoop(Sfx.Stir, Stirring ? 0.8f : 0f, 1, transform.position);
        }

        void LateUpdate()
        {
            if (!InputEnabled) return;
            var p = Cam.transform.position;
            Sfx.SetLoop(Sfx.Shake, Shaking ? (Held is Vessel v && v.Ice > 0 ? 1f : 0.45f) : 0f, 1f, p);
            Sfx.SetLoop(Sfx.Stir, Stirring ? 0.8f : 0f, 1f, p);
        }

        // ================================================================== HUD helpers

        /// <summary>What the mouse buttons and keys would do right now.</summary>
        public string ActionHints()
        {
            var sb = new StringBuilder();
            void Add(string key, string what) { if (sb.Length > 0) sb.Append("     "); sb.Append("<b>").Append(key).Append("</b>  ").Append(what); }
            string lmb = Loc.S("LMB", "ĽTM"), rmb = Loc.S("Hold RMB", "Drž PTM");
            bool leftFull = Left != null && Left.Ml > 0.5f;
            if (HoverItem != null && IsJigger(HoverItem) && Left == null) Add(lmb, Loc.S("Take the jigger (left hand)", "Vziať džiger (ľavá ruka)"));
            else if (leftFull && HoverVessel != null && !(Held is GarnishItem || Held is IceScoop || Held is ShakerCap || Held is StrainerItem))
                Add(lmb, Loc.S($"Pour the jigger in ({Left.Ml:0} ml)", $"Vyliať džiger ({Left.Ml:0} ml)"));
            else if (Held == null)
            {
                if (HoverItem != null && !HoverItem.Locked) Add(lmb, HoverItem.AttachedTo != null ? Loc.S("Take off", "Zložiť") : Loc.S("Pick up", "Vziať"));
                else if (HoverStation != null) { var h = HoverStation.Hint(null); if (h != null) sb.Append(h); }
                else if (Left != null && HoverSurface) Add(lmb, Loc.S("Put the jigger down", "Položiť džiger"));
            }
            else if (HoverStation != null) { var h = HoverStation.Hint(Held); if (h != null) sb.Append(h); }
            else if (HoverVessel != null && !HoverVessel.Held)
            {
                if (Held is GarnishItem) Add(lmb, Loc.S("Add garnish", "Pridať ozdobu"));
                else if (Held is IceScoop sc && sc.Ice > 0) Add(lmb, Loc.S("Add ice", "Pridať ľad"));
                else if (Held is ShakerCap && HoverVessel.VKind == VesselKind.Tin) Add(lmb, Loc.S("Put the cap on", "Nasadiť viečko"));
                else if (Held is StrainerItem) Add(lmb, Loc.S("Put the strainer on", "Nasadiť sitko"));
            }
            else if (HoverSurface && !(Held is GarnishItem)) Add(lmb, Loc.S("Put down", "Položiť"));

            if (Held is Vessel v)
            {
                if (v.Cap != null) Add(rmb, Loc.S("Shake", "Trepať"));
                else Add(rmb, Loc.S("Pour (Shift = slowly)", "Naliať (Shift = pomaly)"));
                if (v.VKind == VesselKind.Tin) Add("E", v.Cap != null ? Loc.S("Cap → strainer", "Viečko → sitko") : v.Strainer != null ? Loc.S("Strainer off", "Sitko dole") : Loc.S("Cap on", "Viečko hore"));
                else if (v.VKind == VesselKind.MixingGlass) Add("E", v.Strainer != null ? Loc.S("Strainer off", "Sitko dole") : Loc.S("Strainer on", "Sitko hore"));
                if (v.Ml > 0.5f || v.Ice > 0) Add("X", Loc.S("Empty", "Vyliať"));
            }
            else if (Held is Bottle) Add(rmb, Left != null && HoverVessel == null ? Loc.S("Pour into the jigger", "Naliať do džigra") : Loc.S("Pour (Shift = slowly)", "Naliať (Shift = pomaly)"));
            else if (Held is BarSpoon) Add(rmb, Loc.S("Stir", "Miešať"));
            else if (Held is Muddler) Add(rmb, Loc.S("Muddle", "Drviť"));
            if (Left != null || IsJigger(Held) || IsJigger(HoverItem)) Add("R", Loc.S("Flip the jigger", "Otočiť džiger"));
            if (Held != null || Left != null) Add("Q", Held is GarnishItem ? Loc.S("Drop", "Zahodiť") : Loc.S("Put back", "Vrátiť"));
            else Add("Tab", Loc.S("Recipes", "Recepty"));
            return sb.ToString();
        }
    }
}
