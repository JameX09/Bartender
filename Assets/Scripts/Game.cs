using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Bartender
{
    public enum Mode { Menu, Career, Free }

    /// <summary>Boots the game, owns the state (menu / career / free) and connects all systems.</summary>
    public sealed class Game : MonoBehaviour
    {
        public static Game I;
        public Mode Mode = Mode.Menu;
        public bool Paused;
        public World World;
        public Player Player;
        public Career Career;
        public UI Ui;
        public Recipe Practice;
        public Guest FocusGuest;
        bool ready, shotMode;
        float menuT;
        readonly List<Mannequin> crowd = new List<Mannequin>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindAnyObjectByType<Game>() == null) new GameObject("Game").AddComponent<Game>();
        }

        public Recipe FocusRecipe => Mode == Mode.Career ? (FocusGuest != null && FocusGuest.Waiting ? FocusGuest.Order : null) : Practice;

        IEnumerator Start()
        {
            I = this;
            Application.targetFrameRate = -1;
            Save.Load();
            Ui = gameObject.AddComponent<UI>();
            Ui.ShowLoading();
            yield return null;
            yield return null;
            ApplySettings();
            Sfx.Init(gameObject);
            Fx.Init(transform);
            World = new GameObject("World Builder").AddComponent<World>();
            World.Build();
            World.ApplyQuality(Save.Quality);
            Player = new GameObject("Player").AddComponent<Player>();
            Player.OnPlaced += OnPlaced;
            SetupCamera(Player.Cam);
            Career = gameObject.AddComponent<Career>();
            Career.OnServed += OnServed;
            Career.OnWalkout += g => { Player.ShowToast(Loc.S($"{g.GuestName} got tired of waiting and left", $"{g.GuestName} sa nedočkal a odišiel")); Sfx.Ui("fail", 0.5f); };
            Career.OnNightOver += NightOver;
            Crowd();
            yield return null;
            World.RefreshProbes();
            yield return new WaitForSeconds(0.3f);
            World.RefreshProbes();
            ready = true;
            Ui.HideLoading();
            ToMenu();

            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
                if (args[i] == "-shots" && i + 1 < args.Length) StartCoroutine(Shots(args[i + 1]));
        }

        public static void SetupCamera(Camera cam)
        {
            var cd = cam.GetUniversalAdditionalCameraData();
            cd.renderPostProcessing = true;
            cd.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cd.antialiasingQuality = AntialiasingQuality.High;
            cd.renderShadows = true;
            cd.requiresDepthOption = CameraOverrideOption.On;
            cam.allowHDR = true;
            cam.clearFlags = CameraClearFlags.Skybox;
        }

        public static void ApplySettings()
        {
            Loc.En = Save.English;
            QualitySettings.vSyncCount = Save.VSync ? 1 : 0;
            if (Screen.fullScreen != Save.Fullscreen)
                Screen.fullScreenMode = Save.Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                urp.renderScale = Save.Quality == 0 ? 0.75f : Save.Quality == 1 ? 0.9f : 1f;
                urp.msaaSampleCount = Save.Quality == 0 ? 1 : Save.Quality == 1 ? 2 : 4;
                urp.shadowDistance = Save.Quality == 0 ? 20f : Save.Quality == 1 ? 35f : 50f;
                urp.supportsHDR = true;
            }
            if (World.I != null) World.I.ApplyQuality(Save.Quality);
        }

        // ================================================================== state changes

        public void ToMenu()
        {
            if (Career != null) { Career.Running = false; Career.Clear(); }
            Mode = Mode.Menu;
            Paused = false;
            Time.timeScale = 1f;
            Player.ReleaseAll();
            Player.InputEnabled = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (World.Dof != null) World.Dof.active = true;
            Sfx.MusicDuck(1f);
            Ui.SetHud(false);
            Ui.ShowMenu();
        }

        void EnterPlay()
        {
            Ui.HideMenu();
            Ui.CloseOverlay();
            Ui.SetHud(true);
            if (World.Dof != null) World.Dof.active = false;
            ResetBar();
            Player.SetView(new Vector3(0, 0, -0.95f), 0f, 24f);
            Paused = false;
            Time.timeScale = 1f;
            LockCursor(true);
            Sfx.MusicDuck(0.75f);
        }

        /// <summary>Puts every tool and bottle back and removes loose glasses.</summary>
        void ResetBar()
        {
            Player.ReleaseAll();
            foreach (var v in FindObjectsByType<Vessel>())
            {
                if (v.Locked && !v.Served) continue; // lounge decoration
                if (v.VKind == VesselKind.Jigger && v.Flipped) { v.Flipped = false; v.SetSide(false); }
                if (v.VKind == VesselKind.Cup) { Destroy(v.gameObject); continue; }
                if (v.VKind == VesselKind.Glass)
                {
                    if (!v.HasHome && Time.time - v.SpawnTime > 0.1f && !IsInRack(v)) Destroy(v.gameObject);
                    else v.Empty();
                }
                else v.Empty();
            }
            foreach (var it in FindObjectsByType<Item>())
            {
                if (it is Vessel ve && ve.Cap != null) { ve.Cap = null; }
                if (it is Vessel vs && vs.Strainer != null) { vs.Strainer = null; }
                if (!it.HasHome) continue;
                it.transform.SetParent(null, true);
                it.AttachedTo = null;
                it.Held = false;
                it.transform.SetPositionAndRotation(it.HomePos, it.HomeRot);
                it.SetColliders(true);
                if (it is IceScoop s) s.SetIce(0);
            }
        }

        static bool IsInRack(Vessel v)
        {
            foreach (var s in FindObjectsByType<GlassSlot>())
                if (Vector3.Distance(s.transform.position, v.transform.position) < 0.05f) return true;
            return false;
        }

        public void NewCareer()
        {
            Save.NewCareer();
            StartCareerNight();
        }

        public void ContinueCareer() => StartCareerNight();

        public void StartCareerNight()
        {
            Mode = Mode.Career;
            EnterPlay();
            FocusGuest = null;
            Career.StartNight();
            var fresh = Career.Unlocked(Save.Night).FindAll(r => r.UnlockNight == Save.Night);
            string sub = Loc.S("20:00 · the Skyline Lounge opens", "20:00 · Skyline Lounge otvára");
            if (fresh.Count > 0) sub += "\n<color=#efbd6b>" + Loc.S("New on the menu: ", "Nové v menu: ") + string.Join(", ", fresh.ConvertAll(r => r.Name)) + "</color>";
            Ui.NightIntro(Loc.S($"Night {Save.Night}", $"Noc {Save.Night}"), sub);
            if (!Save.TutorialSeen) { Pause(false); Ui.ShowHelp(true); }
        }

        public void StartFree()
        {
            Mode = Mode.Free;
            Practice = null;
            EnterPlay();
            Ui.NightIntro(Loc.S("Free mixing", "Voľné miešanie"), Loc.S("No guests, no clock. Mix anything.", "Bez hostí a bez času. Miešaj, čo chceš."));
            if (!Save.TutorialSeen) { Pause(false); Ui.ShowHelp(true); }
        }

        void NightOver()
        {
            Player.ReleaseAll();
            Player.InputEnabled = false;
            LockCursor(false);
            if (Career.Earned + Career.Tips > Save.BestEarnings) Save.BestEarnings = Career.Earned + Career.Tips;
            Save.BestNight = Mathf.Max(Save.BestNight, Save.Night);
            Ui.ShowSummary(Career);
            Sfx.Ui("great", 0.7f);
            Save.Store();
        }

        public void AfterSummary()
        {
            Save.Night++;
            Save.Store();
            Ui.ShowShop();
        }

        public void Pause(bool showMenu = true)
        {
            if (Mode == Mode.Menu) return;
            Paused = true;
            Time.timeScale = 0f;
            Player.InputEnabled = false;
            LockCursor(false);
            Sfx.MusicDuck(0.5f);
            if (showMenu) Ui.ShowPause();
        }

        public void Resume()
        {
            Ui.CloseOverlay();
            Paused = false;
            Time.timeScale = 1f;
            LockCursor(true);
            Sfx.MusicDuck(0.75f);
        }

        static void LockCursor(bool on)
        {
            Cursor.lockState = on ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !on;
        }

        // ================================================================== per frame

        void Update()
        {
            Sfx.Tick();
            if (!ready) return;
            if (Mode == Mode.Menu)
            {
                MenuCamera();
                if (Input.GetKeyDown(KeyCode.Escape) && Ui.OverlayOpen) Ui.CloseOverlay();
                return;
            }
            bool overlay = Ui.OverlayOpen;
            if (!shotMode) Player.InputEnabled = !Paused && !overlay && !(Mode == Mode.Career && !Career.Running);
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (overlay && (Ui.OverlayKind == "book" || Ui.OverlayKind == "settings" || Ui.OverlayKind == "help")) { if (Paused) Ui.ShowPause(); else Resume(); }
                else if (Paused) Resume();
                else if (!overlay) Pause();
            }
            if (Input.GetKeyDown(KeyCode.H) && !overlay) { Save.ShowHints = !Save.ShowHints; Save.Store(); Sfx.Ui(); }
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                if (Ui.OverlayKind == "book") { if (Paused) Ui.ShowPause(); else Resume(); }
                else if (!overlay)
                {
                    Paused = true; Time.timeScale = 0f; Player.InputEnabled = false; LockCursor(false);
                    Ui.ShowBook(FocusRecipe);
                }
            }
            // re-lock the cursor when clicking back into the window
            if (!Paused && !overlay && Cursor.lockState != CursorLockMode.Locked && Input.GetMouseButtonDown(0)) LockCursor(true);

            if (Mode == Mode.Career)
            {
                if (FocusGuest == null || !FocusGuest.Waiting) FocusGuest = Career.MostUrgent();
                if (Input.GetKeyDown(KeyCode.F)) CycleFocus();
                // looking at a guest focuses their order
                if (Player.InputEnabled)
                {
                    var ray = new Ray(Player.Cam.transform.position, Player.Cam.transform.forward);
                    foreach (var gst in Career.Seats)
                    {
                        if (gst == null || !gst.Waiting) continue;
                        var head = gst.transform.position + Vector3.up * 1.4f;
                        var to = head - ray.origin;
                        if (Vector3.Cross(ray.direction, to).magnitude < 0.35f && Vector3.Dot(ray.direction, to) > 0) FocusGuest = gst;
                    }
                }
            }
            CrowdIdle();
        }

        void CycleFocus()
        {
            var list = new List<Guest>();
            foreach (var g in Career.Seats) if (g != null && g.Waiting) list.Add(g);
            if (list.Count == 0) return;
            int i = list.IndexOf(FocusGuest);
            FocusGuest = list[(i + 1) % list.Count];
            Sfx.Ui("ui", 0.4f);
        }

        void MenuCamera()
        {
            menuT += Time.unscaledDeltaTime;
            float a = menuT * 0.045f;
            var cam = Player.Cam.transform;
            var focus = new Vector3(0f, 1.25f, -0.6f);
            var pos = focus + new Vector3(Mathf.Sin(a) * 3.6f + 1.2f, 0.55f + Mathf.Sin(a * 0.7f) * 0.15f, 3.4f + Mathf.Cos(a) * 0.6f);
            cam.position = Vector3.Lerp(cam.position, pos, Time.unscaledDeltaTime * 2f);
            cam.rotation = Quaternion.Slerp(cam.rotation, Quaternion.LookRotation(focus - cam.position), Time.unscaledDeltaTime * 2f);
            if (World.Dof != null)
            {
                World.Dof.gaussianStart.value = Vector3.Distance(cam.position, focus) + 0.8f;
                World.Dof.gaussianEnd.value = World.Dof.gaussianStart.value + 9f;
            }
        }

        // ================================================================== serving and tasting

        void OnPlaced(Item it)
        {
            if (!(it is Vessel v) || v.VKind != VesselKind.Glass) return;
            var spot = ServeSpot.Near(v.transform.position);
            if (spot == null) return;
            if (Mode == Mode.Career && spot.Seat >= 0)
            {
                if (!Career.TryServe(v, spot)) Player.ShowToast(Loc.S("Nobody is waiting here", "Tu nikto nečaká"));
            }
            else if (Mode == Mode.Free && spot.Seat < 0)
            {
                var verdict = Practice != null ? Recipes.Evaluate(Practice, v.GType, v.C, v.Ice, v.Garnishes, v.Muddle) : Recipes.BestMatch(v.GType, v.C, v.Ice, v.Garnishes, v.Muddle);
                string extra = Practice == null ? Loc.S("closest match", "najbližší recept") : "";
                Ui.ShowVerdict(verdict, extra);
                Sfx.Ui(verdict.Stars >= 4 ? "great" : verdict.Stars >= 3 ? "success" : "fail", 0.6f);
                if (verdict.Stars >= 4) Fx.Sparkle(v.transform.position + Vector3.up * 0.15f, 30);
            }
        }

        void OnServed(Guest g, Verdict v, float paid, float tip)
        {
            string extra = v.Stars >= 2 ? "<color=#8ce08c>+" + UIKit.Money(paid) + "</color>" + (tip > 0 ? "  <color=#efbd6b>" + Loc.S("tip ", "sprepitné ") + UIKit.Money(tip) + "</color>" : "")
                                        : "<color=#ff7366>" + Loc.S("Sent back, no payment", "Vrátené, bez platby") + "</color>";
            Ui.ShowVerdict(v, extra);
            if (v.Stars >= 2) Sfx.Play("cash", g.CoasterPos, 0.7f);
            Sfx.Ui(v.Stars >= 4 ? "great" : v.Stars >= 3 ? "success" : "fail", 0.55f);
            if (v.Stars >= 4) Fx.Sparkle(g.CoasterPos + Vector3.up * 0.2f, 34);
        }

        // ================================================================== lounge crowd

        void Crowd()
        {
            var spots = new List<(Vector3 pos, float yaw, float hips)>
            {
                (new Vector3(-6f, 0, 5.6f), 90f, 0.52f), (new Vector3(-6f, 0, 6.6f), 90f, 0.52f),
                (new Vector3(-3.6f, 0, 8.8f), 180f, 0.52f),
                (new Vector3(4.2f, 0, 6.0f), -90f, 0.52f),
                (new Vector3(-3.4f, 0, 4.6f), 20f, 0.52f),
                (new Vector3(2.6f, 0, 4.4f), 200f, 0.52f),
                (new Vector3(6.4f, 0, 10.5f), 180f, 0.52f), (new Vector3(7.3f, 0, 10.5f), 180f, 0.52f),
            };
            int seed = 500;
            var root = new GameObject("Crowd").transform;
            foreach (var s in spots)
            {
                var m = Mannequin.Create(seed++, root);
                m.SitHips = s.hips;
                m.Pose = Mannequin.PoseKind.Sit;
                m.Sit = 1f;
                var fwd = Quaternion.Euler(0, s.yaw, 0) * Vector3.forward;
                m.transform.SetPositionAndRotation(s.pos + fwd * 0.12f, Quaternion.Euler(0, s.yaw, 0));
                crowd.Add(m);
            }
        }

        void CrowdIdle()
        {
            float t = Time.time;
            for (int i = 0; i < crowd.Count; i++)
            {
                var m = crowd[i];
                m.LookYaw = Mathf.Sin(t * 0.3f + i * 1.7f) * 35f;
                float cyc = Mathf.Repeat(t + i * 3.1f, 14f);
                m.Drink = Mathf.MoveTowards(m.Drink, cyc > 12f ? 1f : 0f, Time.deltaTime * 1.5f);
            }
        }

        // ================================================================== screenshots for testing (-shots <folder>)

        IEnumerator Shots(string dir)
        {
            Directory.CreateDirectory(dir);
            shotMode = true;
            yield return new WaitForSeconds(1.5f);
            Capture(dir, "0_menu");
            yield return new WaitForSeconds(0.4f);
            Mode = Mode.Free;
            EnterPlay();
            Ui.CloseOverlay();
            Player.InputEnabled = false;
            Cursor.lockState = CursorLockMode.None;
            // set up a few finished drinks so liquids, ice and garnish can be checked
            Demo(GlassType.Highball, "gintonic", new Vector3(-0.35f, Layout.BarTopY, 0.25f), 6, Garnish.LimeWedge, 0f);
            Demo(GlassType.Coupe, "daiquiri", new Vector3(0.0f, Layout.BarTopY + 0.006f, 0.27f), 0, Garnish.LimeWedge, 0f);
            Demo(GlassType.Martini, "cosmopolitan", new Vector3(0.35f, Layout.BarTopY, 0.25f), 0, Garnish.OrangePeel, 0f);
            Demo(GlassType.Rocks, "negroni", new Vector3(0.7f, Layout.BarTopY, 0.25f), 4, Garnish.OrangePeel, 0f);
            Demo(GlassType.Wine, "spritz", new Vector3(-0.7f, Layout.BarTopY, 0.25f), 5, Garnish.OrangePeel, 0f);
            Demo(GlassType.Highball, "bluelagoon", new Vector3(-1.05f, Layout.BarTopY, 0.25f), 6, Garnish.LemonTwist, 0f);
            Demo(GlassType.Highball, "sunrise", new Vector3(1.05f, Layout.BarTopY, 0.25f), 6, Garnish.Cherry, 35f);
            var v = Recipes.Evaluate(Recipes.Get("gintonic"), GlassType.Highball, demoGT.C, demoGT.Ice, demoGT.Garnishes, 0);
            Debug.Log($"[selftest] G&T score {v.Score:0} stars {v.Stars} notes {string.Join("; ", v.Notes)}");
            yield return new WaitForSeconds(0.5f);
            World.RefreshProbes();
            yield return new WaitForSeconds(0.5f);
            yield return Snap(dir, "1_bartender_view", () => Shot(new Vector3(0, 0, -0.95f), 0f, 24f));
            yield return Snap(dir, "2_work_station", () => Shot(new Vector3(0, 0, -0.9f), 0f, 52f));
            yield return Snap(dir, "3_tools", () => Shot(new Vector3(-1.2f, 0, -0.9f), 0f, 48f));
            yield return Snap(dir, "4_back_bar", () => Shot(new Vector3(0, 0, -1.1f), 180f, 12f));
            yield return Snap(dir, "5_bottles_close", () => Shot(new Vector3(0, 0, -1.25f), 180f, 30f));
            var cam = Player.Cam.transform;
            yield return Snap(dir, "6_drinks_close", () => { cam.position = new Vector3(0.1f, 1.32f, 0.85f); cam.rotation = Quaternion.LookRotation(new Vector3(0f, 1.18f, 0.25f) - cam.position); });
            yield return Snap(dir, "7_bar_from_lounge", () => { cam.position = new Vector3(2.5f, 1.6f, 4.5f); cam.rotation = Quaternion.LookRotation(new Vector3(0f, 1.3f, -0.5f) - cam.position); });
            yield return Snap(dir, "8_city", () => { cam.position = new Vector3(0f, 1.7f, -0.9f); cam.rotation = Quaternion.Euler(-2f, 25f, 0); });

            // left hand jigger, right hand bottle: pour into the jigger, then tip it into a glass
            Player.InputEnabled = true;
            Shot(new Vector3(-0.6f, 0, -0.95f), 0f, 40f);
            var target = Props.Glass(GlassType.Highball);
            target.transform.position = new Vector3(-0.45f, Layout.WorkY + 0.008f, -0.3f);
            target.AddIce(6);
            Vessel jig = null;
            foreach (var j in FindObjectsByType<Vessel>()) if (j.VKind == VesselKind.Jigger && j.NominalA > 40f) jig = j;
            Bottle gin = null;
            foreach (var b in FindObjectsByType<Bottle>()) if (b.Ing.Key == "gin") gin = b;
            Player.GrabLeft(jig);
            Player.Grab(gin);
            yield return new WaitForSeconds(0.6f);
            Player.DebugPour(jig);
            yield return new WaitForSeconds(1.4f);
            yield return Snap(dir, "9_pour_into_jigger", null);
            yield return new WaitForSeconds(1.5f);
            Debug.Log($"[selftest] jigger holds {jig.Ml:0.0} ml of {jig.CapacityMl:0} (should stop at the brim)");
            Player.DebugRmb = false;
            yield return new WaitForSeconds(0.5f);
            Player.DebugLeftPour(target);
            yield return new WaitForSeconds(0.45f);
            yield return Snap(dir, "9b_jigger_into_glass", null);
            yield return new WaitForSeconds(1f);
            Debug.Log($"[selftest] glass got {target.C.Get(Ingredients.Get("gin")):0.0} ml gin, jigger left {jig.Ml:0.0} ml");
            jig.Flip();
            yield return new WaitForSeconds(0.5f);
            Debug.Log($"[selftest] flipped jigger side {jig.NominalMl:0} ml");
            yield return Snap(dir, "9c_jigger_flipped", null);
            Player.ReleaseAll();
            gin.transform.SetPositionAndRotation(gin.HomePos, gin.HomeRot); gin.SetColliders(true);
            jig.Flip(false);
            jig.transform.SetPositionAndRotation(jig.HomePos, jig.HomeRot); jig.SetColliders(true);

            // every jigger, both sides, standing on the mat: fill each one to the brim
            var vodka = FindObjectsByType<Bottle>();
            Bottle vb = null;
            foreach (var b in vodka) if (b.Ing.Key == "vodka") vb = b;
            Shot(new Vector3(-1.45f, 0, -0.95f), 0f, 50f);
            int shotN = 0;
            foreach (var jj in FindObjectsByType<Vessel>())
            {
                if (jj.VKind != VesselKind.Jigger) continue;
                for (int side = 0; side < 2; side++)
                {
                    jj.Empty();
                    Player.Grab(vb);
                    yield return new WaitForSeconds(0.3f);
                    Player.DebugPour(jj);
                    float t0 = Time.time;
                    while (Time.time - t0 < 9f && jj.Ml < jj.CapacityMl - 0.1f)
                    {
                        if (shotN == 0 && Time.time - t0 > 1.6f) { Capture(dir, "9d_locked_pour"); shotN++; }
                        yield return null;
                    }
                    yield return new WaitForSeconds(0.4f);
                    Debug.Log($"[selftest] jigger {jj.NominalA:0}/{jj.NominalB:0} side {jj.NominalMl:0}: {jj.Ml:0.0} ml in {Time.time - t0:0.0}s");
                    Player.DebugRmb = false;
                    yield return new WaitForSeconds(0.3f);
                    Player.ReleaseAll();
                    vb.transform.SetPositionAndRotation(vb.HomePos, vb.HomeRot); vb.SetColliders(true);
                    jj.Flip(false);
                }
                jj.Empty();
            }

            // scooping ice into the shaker
            Vessel tin = null;
            foreach (var t in FindObjectsByType<Vessel>()) if (t.VKind == VesselKind.Tin) tin = t;
            var scoop = FindAnyObjectByType<IceScoop>();
            var well = FindAnyObjectByType<IceWell>();
            Shot(new Vector3(-0.9f, 0, -0.95f), 0f, 46f);
            Player.Grab(scoop);
            yield return new WaitForSeconds(0.4f);
            Player.StartScoop(well.transform.position);
            yield return new WaitForSeconds(0.42f);
            yield return Snap(dir, "10a_scooping", null);
            yield return new WaitForSeconds(0.8f);
            Player.DebugDrop(tin);
            yield return new WaitForSeconds(0.55f);
            yield return Snap(dir, "10b_ice_into_tin", null);
            yield return new WaitForSeconds(1.2f);
            Debug.Log($"[selftest] tin ice {tin.Ice}, scoop ice {scoop.Ice}");
            Player.ReleaseAll();
            scoop.transform.SetPositionAndRotation(scoop.HomePos, scoop.HomeRot); scoop.SetColliders(true);

            // shaking with both hands
            var cap = FindAnyObjectByType<ShakerCap>();
            tin.C.Add(Ingredients.Get("rum"), 60); tin.C.Add(Ingredients.Get("lime"), 25);
            Player.AttachCap(tin, cap);
            Player.Grab(tin);
            Player.DebugRmb = true;
            yield return new WaitForSeconds(1.2f);
            yield return Snap(dir, "10_shaking", null);
            Debug.Log($"[selftest] shake {tin.C.Shake:0.00} chill {tin.C.Chill:0.00} water {tin.C.Get(Ingredients.Water):0} ml");
            Player.DebugRmb = false;
            Player.ReleaseAll();
            tin.transform.SetPositionAndRotation(tin.HomePos, tin.HomeRot);

            // espresso machine
            var machine = FindAnyObjectByType<EspressoMachine>();
            Shot(new Vector3(-2.2f, 0, -1.25f), 180f, 38f);
            machine.Use(Player, null);
            yield return new WaitForSeconds(2.6f);
            yield return Snap(dir, "10c_espresso", null);
            yield return new WaitForSeconds(2.4f);
            foreach (var c in FindObjectsByType<Vessel>()) if (c.VKind == VesselKind.Cup) Debug.Log($"[selftest] espresso cup {c.C.Get(Ingredients.Get("espresso")):0.0} ml");
            Save.ShowHints = false;
            yield return Snap(dir, "10d_hints_hidden", () => Shot(new Vector3(0, 0, -0.95f), 0f, 30f));
            Save.ShowHints = true;
            Player.InputEnabled = false;
            // career with guests at the bar
            Mode = Mode.Career;
            Career.StartNight();
            for (int i = 0; i < 4; i++) Career.Seats[i] = Guest.Spawn(i, Recipes.All[i * 3], 300f, 77 + i, transform);
            yield return new WaitForSeconds(17f);
            FocusGuest = Career.MostUrgent();
            yield return Snap(dir, "11_guests_hud", () => Shot(new Vector3(0, 0, -0.95f), 0f, 12f));
            yield return Snap(dir, "12_guests_close", () => { cam.position = new Vector3(-1.8f, 1.5f, 3.0f); cam.rotation = Quaternion.LookRotation(new Vector3(-1.2f, 1.2f, 0.9f) - cam.position); });
            Ui.ShowBook(Recipes.Get("margarita"));
            yield return Snap(dir, "13_recipe_book", null);
            Ui.ShowShop();
            yield return Snap(dir, "14_shop", null);
            Ui.ShowSettings();
            yield return Snap(dir, "15_settings", null);
            Ui.CloseOverlay();
            for (int i = 0; i < 3; i++)
            {
                var gst = Career.Seats[i];
                if (gst == null) continue;
                var head = gst.Body.Head.position;
                yield return Snap(dir, "12f_face" + i, () => { cam.position = head + new Vector3(0.12f, -0.02f, -0.55f); cam.rotation = Quaternion.LookRotation(head - cam.position); });
            }
            Debug.Log("[shots] done");
            Application.Quit();
        }

        IEnumerator Snap(string dir, string name, System.Action setup)
        {
            setup?.Invoke();
            yield return new WaitForSeconds(0.6f);
            Capture(dir, name);
            yield return new WaitForEndOfFrame();
            yield return null;
        }

        Vessel demoGT;

        void Demo(GlassType t, string recipe, Vector3 pos, int ice, Garnish g, float tilt)
        {
            var r = Recipes.Get(recipe);
            var v = Props.Glass(t);
            v.transform.SetPositionAndRotation(pos, Quaternion.Euler(tilt, 160f, 0));
            foreach (var p in r.Parts) v.C.Add(p.ing, p.ml);
            if (r.Method == Method.Shake) { v.C.Shake = 1f; v.C.Chill = 1f; }
            if (r.Method == Method.Stir) { v.C.Stir = 1f; v.C.Chill = 1f; }
            v.AddIce(ice);
            v.Garnishes.Add(g);
            v.Refresh(true);
            if (recipe == "gintonic") demoGT = v;
        }

        void Shot(Vector3 feet, float yaw, float pitch) => Player.SetView(feet, yaw, pitch);

        static void Capture(string dir, string name)
        {
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, name + ".png"));
            Debug.Log("[shots] " + name);
        }
    }
}
