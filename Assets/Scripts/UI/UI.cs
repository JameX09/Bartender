using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Bartender
{
    /// <summary>All screens: loading, main menu, HUD, recipe book, pause, settings, summary, shop.</summary>
    public sealed class UI : MonoBehaviour
    {
        Canvas canvas;
        RectTransform root;
        GameObject loading, menu, hud, overlay;
        CanvasGroup introGroup, verdictGroup;
        float introTime = -99, verdictTime = -99;
        public bool OverlayOpen => overlay != null;
        public string OverlayKind;

        // HUD parts
        Text clock, money, hoverTitle, hoverInfo, hints, toast, pourText, card, progressText, ticketsText, introTitle, introSub, verdictTitle, verdictBody, cardArrow;
        Image crosshair, progressBar, progressBg, cardBg, ticketsBg;
        GameObject progressRoot;

        void Awake()
        {
            TextTex.Init();
            var cgo = new GameObject("UI", typeof(RectTransform));
            cgo.transform.SetParent(transform, false);
            canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = cgo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            cgo.AddComponent<GraphicRaycaster>();
            root = cgo.GetComponent<RectTransform>();
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
                es.transform.SetParent(transform, false);
            }
        }

        static void Kill(ref GameObject g) { if (g != null) Destroy(g); g = null; }

        RectTransform Layer(string name, Color bg)
        {
            var go = UIKit.Node(name, root);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            if (bg.a > 0)
            {
                var img = go.AddComponent<Image>();
                img.color = bg;
                img.raycastTarget = true;
            }
            return rt;
        }

        // ================================================================== loading

        public void ShowLoading()
        {
            Kill(ref loading);
            var l = Layer("loading", new Color(0.02f, 0.02f, 0.03f, 1f));
            loading = l.gameObject;
            var t = UIKit.Label(l, "Bartender 3D", 96, UIKit.Gold, TextAnchor.MiddleCenter, TextTex.Serif, FontStyle.Italic);
            UIKit.Rect(t.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(1200, 140));
            var s = UIKit.Label(l, Loc.S("Polishing the glasses…", "Leštím poháre…"), 26, UIKit.Muted, TextAnchor.MiddleCenter);
            UIKit.Rect(s.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -50), new Vector2(800, 40));
            loading.transform.SetAsLastSibling();
        }

        public void HideLoading() => Kill(ref loading);

        // ================================================================== main menu

        public void ShowMenu()
        {
            Kill(ref menu);
            CloseOverlay();
            SetHud(false);
            var m = Layer("menu", new Color(0, 0, 0, 0));
            menu = m.gameObject;
            var grad = UIKit.Box(m, "shade", new Color(0.02f, 0.015f, 0.03f, 0.92f), UIKit.FadeRight);
            grad.type = Image.Type.Simple;
            UIKit.Rect(grad.gameObject, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), Vector2.zero, new Vector2(1150, 0));
            var title = UIKit.Label(m, "Bartender 3D", 104, UIKit.Gold, TextAnchor.MiddleLeft, TextTex.Serif, FontStyle.Italic);
            UIKit.Rect(title.gameObject, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(110, -130), new Vector2(900, 140));
            UIKit.Shadow(title, 3);
            var sub = UIKit.Label(m, Loc.S("SKYLINE  ROOFTOP  LOUNGE", "SKYLINE  STREŠNÝ  LOUNGE"), 24, UIKit.Muted, TextAnchor.MiddleLeft);
            UIKit.Rect(sub.gameObject, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(116, -262), new Vector2(800, 40));

            var col = UIKit.Node("buttons", m);
            UIKit.Rect(col, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(110, -40), new Vector2(420, 520));
            UIKit.VStack(col, 16, TextAnchor.UpperLeft);
            if (Save.CareerStarted)
                UIKit.Button(col.transform, Loc.S($"Continue · Night {Save.Night}", $"Pokračovať · Noc {Save.Night}"), () => Game.I.ContinueCareer(), 28, 420, 66, true);
            UIKit.Button(col.transform, Save.CareerStarted ? Loc.S("New career", "Nová kariéra") : Loc.S("Start career", "Začať kariéru"), () =>
            {
                if (Save.CareerStarted) Confirm(Loc.S("Start over? Your progress will be lost.", "Začať odznova? Postup sa stratí."), () => Game.I.NewCareer());
                else Game.I.NewCareer();
            }, 28, 420, 66, !Save.CareerStarted);
            UIKit.Button(col.transform, Loc.S("Free mixing", "Voľné miešanie"), () => Game.I.StartFree(), 28, 420, 66);
            UIKit.Button(col.transform, Loc.S("Recipe book", "Kniha receptov"), () => ShowBook(), 28, 420, 66);
            UIKit.Button(col.transform, Loc.S("Settings", "Nastavenia"), () => ShowSettings(), 28, 420, 66);
            UIKit.Button(col.transform, Loc.S("Quit", "Ukončiť"), () => Application.Quit(), 28, 420, 66);

            if (Save.CareerStarted)
            {
                var stats = UIKit.Label(m, Loc.S($"Money {UIKit.Money(Save.Money)}    Reputation {UIKit.Stars(Mathf.RoundToInt(Save.Reputation))}    Drinks served {Save.Served}",
                                                $"Peniaze {UIKit.Money(Save.Money)}    Reputácia {UIKit.Stars(Mathf.RoundToInt(Save.Reputation))}    Podaných drinkov {Save.Served}"), 22, UIKit.Muted);
                UIKit.Rect(stats.gameObject, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(116, 70), new Vector2(1100, 40));
            }
            var lang = UIKit.Button(m, Loc.En ? "English  ·  Slovensky" : "Slovensky  ·  English", () =>
            {
                Save.English = !Save.English; Loc.En = Save.English; Save.Store(); ShowMenu();
            }, 20, 260, 44);
            var lrt = lang.GetComponent<RectTransform>();
            lrt.anchorMin = lrt.anchorMax = lrt.pivot = new Vector2(1, 0);
            lrt.anchoredPosition = new Vector2(-40, 40);
            var ver = UIKit.Label(m, "v" + Application.version, 18, new Color(1, 1, 1, 0.3f), TextAnchor.LowerRight);
            UIKit.Rect(ver.gameObject, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-40, 96), new Vector2(300, 30));
        }

        public void HideMenu() => Kill(ref menu);

        // ================================================================== overlays (pause, book, settings ...)

        RectTransform Overlay(string kind, float w, float h, string title)
        {
            CloseOverlay();
            OverlayKind = kind;
            var l = Layer("overlay " + kind, new Color(0.01f, 0.01f, 0.02f, 0.55f));
            overlay = l.gameObject;
            var p = UIKit.Box(l, "panel", UIKit.Panel);
            UIKit.Rect(p.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w, h));
            var line = UIKit.Box(p.transform, "accent", new Color(UIKit.Gold.r, UIKit.Gold.g, UIKit.Gold.b, 0.8f), UIKit.RoundedSmall);
            UIKit.Rect(line.gameObject, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -2), new Vector2(w - 80, 3));
            if (!string.IsNullOrEmpty(title))
            {
                var t = UIKit.Label(p.transform, title, 52, UIKit.Gold, TextAnchor.MiddleCenter, TextTex.Serif, FontStyle.Italic);
                UIKit.Rect(t.gameObject, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -28), new Vector2(0, 70));
            }
            if (menu != null) menu.SetActive(false);
            return p.rectTransform;
        }

        public void CloseOverlay()
        {
            Kill(ref overlay);
            OverlayKind = null;
            if (menu != null) menu.SetActive(true);
        }

        void Confirm(string text, Action yes)
        {
            var p = Overlay("confirm", 720, 300, null);
            var t = UIKit.Label(p, text, 28, UIKit.Ink, TextAnchor.MiddleCenter);
            UIKit.Rect(t.gameObject, new Vector2(0, 0.45f), new Vector2(1, 1), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-80, 0));
            var a = UIKit.Button(p, Loc.S("Yes", "Áno"), () => { CloseOverlay(); yes(); }, 26, 240, 60, true);
            var b = UIKit.Button(p, Loc.S("No", "Nie"), CloseOverlay, 26, 240, 60);
            Place(a, new Vector2(0.5f, 0), new Vector2(-135, 50));
            Place(b, new Vector2(0.5f, 0), new Vector2(135, 50));
        }

        static void Place(Component c, Vector2 anchor, Vector2 pos)
        {
            var rt = c.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0);
            rt.anchoredPosition = pos;
        }

        public void ShowPause()
        {
            var p = Overlay("pause", 520, 640, Loc.S("Paused", "Pauza"));
            var col = UIKit.Node("col", p);
            UIKit.Rect(col, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -130), new Vector2(400, 480));
            UIKit.VStack(col, 14);
            UIKit.Button(col.transform, Loc.S("Resume", "Pokračovať"), () => Game.I.Resume(), 26, 400, 62, true);
            UIKit.Button(col.transform, Loc.S("Recipe book", "Kniha receptov"), () => ShowBook(), 26, 400, 62);
            UIKit.Button(col.transform, Loc.S("How to play", "Ako hrať"), () => ShowHelp(), 26, 400, 62);
            UIKit.Button(col.transform, Loc.S("Settings", "Nastavenia"), () => ShowSettings(), 26, 400, 62);
            UIKit.Button(col.transform, Loc.S("Main menu", "Hlavné menu"), () =>
            {
                if (Game.I.Mode == Mode.Career) Confirm(Loc.S("Leave the night? Tonight's progress is lost.", "Odísť z noci? Dnešný postup sa stratí."), () => Game.I.ToMenu());
                else Game.I.ToMenu();
            }, 26, 400, 62);
        }

        public void ShowHelp(bool fromStart = false)
        {
            var p = Overlay("help", 1260, 960, Loc.S("How to play", "Ako hrať"));
            var sb = new StringBuilder();
            if (Loc.En)
            {
                sb.Append("<color=#efbd6b><b>Controls</b></color>\n");
                sb.Append("<b>Mouse</b> look around   ·   <b>WASD</b> walk behind the bar\n");
                sb.Append("<b>LMB</b> pick up / put down / use (ice scoop, garnish, cap, strainer)\n");
                sb.Append("<b>Hold RMB</b> pour (aim at the glass) · shake a capped tin · stir with the spoon · muddle\n");
                sb.Append("<b>Shift + RMB</b> pour slowly   ·   <b>Mouse wheel</b> fine tilt while pouring\n");
                sb.Append("<b>E</b> on a held tin: cap → strainer → open   ·   <b>Q</b> put back   ·   <b>X</b> empty\n");
                sb.Append("<b>R</b> flip the jigger (60/30, 50/25, 40/20, 30/15, 20/10 ml)   ·   <b>H</b> hide or show the guides\n");
                sb.Append("<b>Tab</b> recipe book   ·   <b>F</b> next order   ·   <b>Esc</b> pause\n\n");
                sb.Append("<color=#efbd6b><b>Measuring</b></color>\n");
                sb.Append("Click a jigger: it goes into your left hand. Take a bottle with the right hand and hold RMB: the bottle pours into the jigger and stops at the brim. Then click the tin or glass to tip the jigger in.\n");
                sb.Append("Espresso: click the espresso machine on the back bar and it pulls a 30 ml shot into a cup.\n\n");
                sb.Append("<color=#efbd6b><b>Making a shaken drink</b></color>\n");
                sb.Append("1. Take the shaker tin, put it down and pour the ingredients in (watch the ml on screen; a jigger helps).\n");
                sb.Append("2. Scoop ice into the tin, put the small tin on as a cap, pick it up and hold RMB to shake.\n");
                sb.Append("3. Press E to swap the cap for the strainer, then pour into the right glass.\n");
                sb.Append("4. Add the garnish from the tray and set the glass on the guest's coaster.\n\n");
                sb.Append("<color=#efbd6b><b>Built and stirred drinks</b></color>\n");
                sb.Append("Built drinks go straight into a glass full of ice. Stirred drinks are stirred with ice in the mixing glass (or in the glass itself for drinks on the rocks).\n");
                sb.Append("The recipe card on the right ticks off every step as you go.");
            }
            else
            {
                sb.Append("<color=#efbd6b><b>Ovládanie</b></color>\n");
                sb.Append("<b>Myš</b> rozhliadanie   ·   <b>WASD</b> chôdza za barom\n");
                sb.Append("<b>ĽTM</b> vziať / položiť / použiť (lopatka na ľad, ozdoba, viečko, sitko)\n");
                sb.Append("<b>Drž PTM</b> nalievať (zamier na pohár) · trepať šejker s viečkom · miešať lyžicou · drviť\n");
                sb.Append("<b>Shift + PTM</b> nalievať pomaly   ·   <b>Koliesko</b> jemný náklon pri nalievaní\n");
                sb.Append("<b>E</b> pri šejkri v ruke: viečko → sitko → otvorené   ·   <b>Q</b> vrátiť   ·   <b>X</b> vyliať\n");
                sb.Append("<b>R</b> otočiť džiger (60/30, 50/25, 40/20, 30/15, 20/10 ml)   ·   <b>H</b> skryť alebo zobraziť nápovedy\n");
                sb.Append("<b>Tab</b> kniha receptov   ·   <b>F</b> ďalšia objednávka   ·   <b>Esc</b> pauza\n\n");
                sb.Append("<color=#efbd6b><b>Odmeriavanie</b></color>\n");
                sb.Append("Klikni na džiger: vezmeš ho do ľavej ruky. Pravou rukou vezmi fľašu a drž PTM: fľaša naleje do džigra a zastaví sa pri okraji. Potom klikni na šejker alebo pohár a džiger sa vyleje.\n");
                sb.Append("Espresso: klikni na kávovar na zadnom bare a pripraví 30 ml do šálky.\n\n");
                sb.Append("<color=#efbd6b><b>Trasený drink</b></color>\n");
                sb.Append("1. Vezmi šejker, polož ho a nalej doň suroviny (sleduj ml na obrazovke, pomôže džiger).\n");
                sb.Append("2. Naber lopatkou ľad, nasaď malú plechovku ako viečko, zdvihni šejker a drž PTM.\n");
                sb.Append("3. Stlač E, vymeň viečko za sitko a nalej do správneho pohára.\n");
                sb.Append("4. Pridaj ozdobu z tácky a polož pohár na podložku pred hosťa.\n\n");
                sb.Append("<color=#efbd6b><b>Liate a miešané drinky</b></color>\n");
                sb.Append("Liate drinky sa nalievajú priamo do pohára plného ľadu. Miešané sa miešajú s ľadom v miešacom pohári (alebo priamo v pohári, ak sa podávajú s ľadom).\n");
                sb.Append("Karta receptu vpravo odškrtáva každý krok.");
            }
            var t = UIKit.Label(p, sb.ToString(), 21, UIKit.Ink, TextAnchor.UpperLeft);
            UIKit.Rect(t.gameObject, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(-120, -240));
            var ok = UIKit.Button(p, Loc.S("Got it", "Rozumiem"), () =>
            {
                Save.TutorialSeen = true; Save.Store();
                if (fromStart) { CloseOverlay(); Game.I.Resume(); } else BackFromOverlay();
            }, 26, 300, 60, true);
            Place(ok, new Vector2(0.5f, 0), new Vector2(0, 40));
        }

        void BackFromOverlay()
        {
            if (Game.I.Mode == Mode.Menu) CloseOverlay();
            else if (Game.I.Paused) ShowPause();
            else { CloseOverlay(); Game.I.Resume(); }
        }

        // ------------------------------------------------------------------ settings

        public void ShowSettings()
        {
            var p = Overlay("settings", 1340, 800, Loc.S("Settings", "Nastavenia"));
            var colL = UIKit.Node("col left", p);
            UIKit.Rect(colL, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(1, 1), new Vector2(-30, -130), new Vector2(580, 520));
            UIKit.VStack(colL, 10, TextAnchor.UpperLeft);
            var colR = UIKit.Node("col right", p);
            UIKit.Rect(colR, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, 1), new Vector2(30, -130), new Vector2(580, 520));
            UIKit.VStack(colR, 10, TextAnchor.UpperLeft);
            Section(colL.transform, Loc.S("Controls and sound", "Ovládanie a zvuk"));
            var col = colL;
            string OnOff(bool b) => b ? Loc.S("On", "Zap.") : Loc.S("Off", "Vyp.");
            UIKit.Slider(col.transform, Loc.S("Mouse sensitivity", "Citlivosť myši"), 0.2f, 3f, Save.Sensitivity, v => Save.Sensitivity = v, v => v.ToString("0.00"), 580);
            UIKit.Toggle(col.transform, Loc.S("Invert mouse Y", "Invertovať myš Y"), () => OnOff(Save.InvertY), () => Save.InvertY = !Save.InvertY, 580);
            UIKit.Slider(col.transform, Loc.S("Master volume", "Hlasitosť"), 0f, 1f, Save.Master, v => Save.Master = v, v => Mathf.RoundToInt(v * 100) + "%", 580);
            UIKit.Slider(col.transform, Loc.S("Music", "Hudba"), 0f, 1f, Save.MusicVol, v => Save.MusicVol = v, v => Mathf.RoundToInt(v * 100) + "%", 580);
            UIKit.Slider(col.transform, Loc.S("Effects", "Efekty"), 0f, 1f, Save.SfxVol, v => Save.SfxVol = v, v => Mathf.RoundToInt(v * 100) + "%", 580);
            Section(col.transform, Loc.S("Gameplay", "Hranie"));
            UIKit.Slider(col.transform, Loc.S("Pour speed", "Rýchlosť nalievania"), 0.25f, 1.5f, Save.PourSpeed, v => Save.PourSpeed = v, v => Mathf.RoundToInt(v * 100) + "%", 580);
            UIKit.Toggle(col.transform, Loc.S("Pour assist (jigger lock)", "Pomoc pri nalievaní"), () => OnOff(Save.PourAssist), () => Save.PourAssist = !Save.PourAssist, 580);
            UIKit.Toggle(col.transform, Loc.S("Hints and recipe card (H)", "Nápovedy a karta (H)"), () => OnOff(Save.ShowHints), () => Save.ShowHints = !Save.ShowHints, 580);
            col = colR;
            Section(col.transform, Loc.S("Display", "Obraz"));
            UIKit.Slider(col.transform, Loc.S("Field of view", "Zorné pole"), 55f, 90f, Save.Fov, v => Save.Fov = v, v => v.ToString("0") + "°", 580);
            UIKit.Toggle(col.transform, Loc.S("Graphics quality", "Kvalita grafiky"), () => Save.Quality == 0 ? Loc.S("Low", "Nízka") : Save.Quality == 1 ? Loc.S("Medium", "Stredná") : Loc.S("High", "Vysoká"),
                () => { Save.Quality = (Save.Quality + 1) % 3; Game.ApplySettings(); }, 580);
            UIKit.Toggle(col.transform, Loc.S("Fullscreen", "Celá obrazovka"), () => OnOff(Save.Fullscreen), () => { Save.Fullscreen = !Save.Fullscreen; Game.ApplySettings(); }, 580);
            UIKit.Toggle(col.transform, "V-Sync", () => OnOff(Save.VSync), () => { Save.VSync = !Save.VSync; Game.ApplySettings(); }, 580);
            Section(col.transform, Loc.S("Language", "Jazyk"));
            UIKit.Toggle(col.transform, Loc.S("Language", "Jazyk"), () => Loc.En ? "English" : "Slovensky", () => { Save.English = !Save.English; Loc.En = Save.English; Save.Store(); ShowSettings(); }, 580);
            var back = UIKit.Button(p, Loc.S("Back", "Späť"), () => { Save.Store(); BackFromOverlay(); if (Game.I.Mode == Mode.Menu) ShowMenu(); }, 26, 300, 60, true);
            Place(back, new Vector2(0.5f, 0), new Vector2(0, 40));
        }

        static void Section(Transform parent, string title)
        {
            var row = UIKit.Node("section", parent);
            row.GetComponent<RectTransform>().sizeDelta = new Vector2(580, 40);
            var t = UIKit.Label(row.transform, title.ToUpper(), 18, UIKit.Gold, TextAnchor.LowerLeft);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            UIKit.Fill(t);
            var line = UIKit.Box(row.transform, "line", new Color(UIKit.Gold.r, UIKit.Gold.g, UIKit.Gold.b, 0.3f), UIKit.RoundedSmall);
            UIKit.Rect(line.gameObject, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 1), new Vector2(0, -4), new Vector2(0, 2));
        }

        // ------------------------------------------------------------------ recipe book

        Recipe bookSel;

        public void ShowBook(Recipe select = null)
        {
            var p = Overlay("book", 1500, 880, Loc.S("Recipe book", "Kniha receptov"));
            if (select != null) bookSel = select;
            if (bookSel == null) bookSel = Game.I.FocusRecipe ?? Recipes.All[0];
            int night = Game.I.Mode == Mode.Career ? Save.Night : 99;
            var list = UIKit.Node("list", p);
            UIKit.Rect(list, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(50, -120), new Vector2(560, 700));
            var grid = list.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(275, 54);
            grid.spacing = new Vector2(10, 8);
            foreach (var r in Recipes.All)
            {
                bool locked = r.UnlockNight > night;
                var rr = r;
                var b = UIKit.Button(list.transform, r.Name, () => ShowBook(rr), 20, 275, 54, r == bookSel);
                if (locked) b.GetComponentInChildren<Text>().color = new Color(1, 1, 1, r == bookSel ? 0.8f : 0.4f);
            }
            // detail
            var d = UIKit.Box(p, "detail", UIKit.PanelLight);
            UIKit.Rect(d.gameObject, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-50, -120), new Vector2(820, 700));
            var r0 = bookSel;
            var accent = UIKit.Box(d.transform, "accent", r0.Accent, UIKit.RoundedSmall);
            UIKit.Rect(accent.gameObject, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -44), new Vector2(10, 64));
            var name = UIKit.Label(d.transform, r0.Name, 54, UIKit.Ink, TextAnchor.UpperLeft, TextTex.Serif, FontStyle.Bold);
            UIKit.Rect(name.gameObject, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(66, -36), new Vector2(-100, 80));
            var desc = UIKit.Label(d.transform, r0.Desc, 24, UIKit.Muted, TextAnchor.UpperLeft, TextTex.Serif, FontStyle.Italic);
            UIKit.Rect(desc.gameObject, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(66, -112), new Vector2(-100, 70));
            var sb = new StringBuilder();
            sb.Append("<color=#efbd6b>").Append(Loc.S("Glass", "Pohár")).Append("</color>   ").Append(Recipes.GlassName(r0.Glass)).Append('\n');
            sb.Append("<color=#efbd6b>").Append(Loc.S("Method", "Postup")).Append("</color>   ").Append(Recipes.MethodName(r0)).Append('\n');
            sb.Append("<color=#efbd6b>").Append(Loc.S("Garnish", "Ozdoba")).Append("</color>   ").Append(r0.Garnish.HasValue ? Recipes.GarnishName(r0.Garnish.Value) : Loc.S("none", "bez ozdoby")).Append("\n\n");
            foreach (var part in r0.Parts)
                sb.Append("<b>").Append(part.ing.Kind == Kind.Bitters ? $"{part.ml:0} dash" : $"{part.ml:0} ml").Append("</b>   ").Append(part.ing.Name).Append('\n');
            sb.Append('\n').Append("<color=#efbd6b>").Append(Loc.S("Price", "Cena")).Append("</color>   ").Append(UIKit.Money(r0.Price));
            if (r0.UnlockNight > night) sb.Append("\n<color=#ff8a80>").Append(Loc.S($"Unlocks on night {r0.UnlockNight}", $"Odomkne sa v noci {r0.UnlockNight}")).Append("</color>");
            var body = UIKit.Label(d.transform, sb.ToString(), 26, UIKit.Ink, TextAnchor.UpperLeft);
            body.lineSpacing = 1.15f;
            UIKit.Rect(body.gameObject, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(66, -200), new Vector2(-110, 480));
            if (Game.I.Mode == Mode.Free)
            {
                var prac = UIKit.Button(d.transform, Loc.S("Make this drink", "Pripraviť tento drink"), () => { Game.I.Practice = r0; CloseOverlay(); Game.I.Resume(); }, 24, 360, 58, true);
                var prt = prac.GetComponent<RectTransform>();
                prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(1, 0);
                prt.anchoredPosition = new Vector2(-30, 30);
            }
            var back = UIKit.Button(p, Loc.S("Close (Tab)", "Zavrieť (Tab)"), () => BackFromOverlay(), 22, 260, 54);
            var brt = back.GetComponent<RectTransform>();
            brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0, 0);
            brt.anchoredPosition = new Vector2(50, 36);
        }

        // ------------------------------------------------------------------ night end and shop

        public void ShowSummary(Career c)
        {
            var p = Overlay("summary", 900, 760, Loc.S($"Night {Save.Night} is over", $"Noc {Save.Night} skončila"));
            float avg = c.ServedCount > 0 ? c.StarSum / c.ServedCount : 0;
            var sb = new StringBuilder();
            void Row(string a, string b) => sb.Append("<color=#c9c0b2>").Append(a).Append("</color>\t\t").Append(b).Append('\n');
            Row(Loc.S("Drinks served", "Podané drinky"), c.ServedCount.ToString());
            Row(Loc.S("Perfect drinks", "Dokonalé drinky"), c.PerfectCount.ToString());
            Row(Loc.S("Guests who walked out", "Odišlo bez drinku"), c.Walkouts.ToString());
            Row(Loc.S("Average rating", "Priemerné hodnotenie"), c.ServedCount > 0 ? UIKit.Stars(Mathf.RoundToInt(avg)) + "  " + avg.ToString("0.0") : "-");
            Row(Loc.S("Takings", "Tržba"), UIKit.Money(c.Earned));
            Row(Loc.S("Tips", "Sprepitné"), UIKit.Money(c.Tips));
            Row(Loc.S("Wallet", "Peňaženka"), "<b>" + UIKit.Money(Save.Money) + "</b>");
            Row(Loc.S("Reputation", "Reputácia"), UIKit.Stars(Mathf.RoundToInt(Save.Reputation)));
            var t = UIKit.Label(p, sb.ToString(), 30, UIKit.Ink, TextAnchor.UpperLeft);
            t.lineSpacing = 1.25f;
            UIKit.Rect(t.gameObject, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(40, -140), new Vector2(-200, 460));
            var next = Career.Unlocked(Save.Night + 1).FindAll(r => r.UnlockNight == Save.Night + 1);
            if (next.Count > 0)
            {
                var n = UIKit.Label(p, Loc.S("New on the menu tomorrow: ", "Zajtra nové v menu: ") + string.Join(", ", next.ConvertAll(r => r.Name)), 24, UIKit.Gold, TextAnchor.MiddleCenter, TextTex.Serif, FontStyle.Italic);
                UIKit.Rect(n.gameObject, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 120), new Vector2(-80, 40));
            }
            var b = UIKit.Button(p, Loc.S("Continue", "Pokračovať"), () => Game.I.AfterSummary(), 26, 320, 62, true);
            Place(b, new Vector2(0.5f, 0), new Vector2(0, 36));
        }

        public void ShowShop()
        {
            var p = Overlay("shop", 1300, 860, Loc.S("Upgrades", "Vylepšenia"));
            var money = UIKit.Label(p, Loc.S("Wallet  ", "Peňaženka  ") + "<b>" + UIKit.Money(Save.Money) + "</b>", 28, UIKit.Ink, TextAnchor.MiddleCenter);
            UIKit.Rect(money.gameObject, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -100), new Vector2(0, 40));
            var grid = UIKit.Node("grid", p);
            UIKit.Rect(grid, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -160), new Vector2(1200, 520));
            var g = grid.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(380, 240);
            g.spacing = new Vector2(20, 20);
            foreach (Upgrade u in Enum.GetValues(typeof(Upgrade)))
            {
                var card = UIKit.Box(grid.transform, "card", UIKit.PanelLight);
                var title = UIKit.Label(card.transform, Save.UpgradeName(u), 27, UIKit.Ink, TextAnchor.UpperLeft, TextTex.Serif, FontStyle.Bold);
                UIKit.Rect(title.gameObject, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(24, -20), new Vector2(-48, 44));
                var desc = UIKit.Label(card.transform, Save.UpgradeDesc(u), 22, UIKit.Muted, TextAnchor.UpperLeft);
                UIKit.Rect(desc.gameObject, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(24, -96), new Vector2(-48, 80));
                bool owned = Save.Has(u);
                float cost = Save.UpgradeCost(u);
                var uu = u;
                var b = UIKit.Button(card.transform, owned ? Loc.S("Owned", "Kúpené") : UIKit.Money(cost), () =>
                {
                    if (Save.Has(uu)) return;
                    if (Save.Money < cost) { Sfx.Ui("fail"); return; }
                    Save.Money -= cost; Save.Give(uu); Save.Store();
                    Sfx.Ui("cash", 0.8f);
                    ShowShop();
                }, 22, 200, 50, !owned && Save.Money >= cost);
                var brt = b.GetComponent<RectTransform>();
                brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(1, 0);
                brt.anchoredPosition = new Vector2(-20, 20);
            }
            var go = UIKit.Button(p, Loc.S($"Open the bar · Night {Save.Night}", $"Otvoriť bar · Noc {Save.Night}"), () => Game.I.StartCareerNight(), 26, 420, 62, true);
            Place(go, new Vector2(0.5f, 0), new Vector2(160, 36));
            var menuB = UIKit.Button(p, Loc.S("Main menu", "Hlavné menu"), () => Game.I.ToMenu(), 24, 260, 62);
            Place(menuB, new Vector2(0.5f, 0), new Vector2(-230, 36));
        }

        // ================================================================== HUD

        public void SetHud(bool on)
        {
            if (!on) { Kill(ref hud); return; }
            if (hud != null) return;
            var h = Layer("hud", new Color(0, 0, 0, 0));
            hud = h.gameObject;
            hud.transform.SetAsFirstSibling();
            // top-left: clock and money
            var tl = UIKit.Box(h, "status", UIKit.Panel);
            UIKit.Rect(tl.gameObject, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -26), new Vector2(380, 92));
            clock = UIKit.Label(tl.transform, "", 34, UIKit.Ink, TextAnchor.MiddleLeft, TextTex.Serif, FontStyle.Bold);
            UIKit.Rect(clock.gameObject, new Vector2(0, 0.5f), new Vector2(1, 1), new Vector2(0, 0.5f), new Vector2(22, 0), new Vector2(-30, 0));
            money = UIKit.Label(tl.transform, "", 21, UIKit.Muted, TextAnchor.MiddleLeft);
            UIKit.Rect(money.gameObject, new Vector2(0, 0), new Vector2(1, 0.5f), new Vector2(0, 0.5f), new Vector2(22, 4), new Vector2(-30, 0));
            // tickets (left)
            ticketsBg = UIKit.Box(h, "tickets", UIKit.Panel);
            UIKit.Rect(ticketsBg.gameObject, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -134), new Vector2(380, 300));
            ticketsText = UIKit.Label(ticketsBg.transform, "", 22, UIKit.Ink, TextAnchor.UpperLeft);
            UIKit.Fill(ticketsText, 22, 18, 16, 16);
            // recipe card (right)
            cardBg = UIKit.Box(h, "card", UIKit.Panel);
            UIKit.Rect(cardBg.gameObject, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-30, -26), new Vector2(430, 520));
            card = UIKit.Label(cardBg.transform, "", 22, UIKit.Ink, TextAnchor.UpperLeft);
            card.lineSpacing = 1.12f;
            UIKit.Fill(card, 26, 20, 20, 16);
            // arrow tab: hides / shows the guides (also the H key)
            var tab = UIKit.Box(cardBg.transform, "tab", UIKit.Panel, UIKit.RoundedSmall);
            UIKit.Rect(tab.gameObject, new Vector2(0, 1), new Vector2(0, 1), new Vector2(1, 1), new Vector2(-6, 0), new Vector2(54, 54));
            tab.raycastTarget = true;
            tab.gameObject.AddComponent<Button>().onClick.AddListener(() => { Save.ShowHints = !Save.ShowHints; Save.Store(); Sfx.Ui(); });
            cardArrow = UIKit.Label(tab.transform, "", 22, UIKit.Gold, TextAnchor.MiddleCenter);
            UIKit.Fill(cardArrow);
            // crosshair
            crosshair = UIKit.Box(h, "crosshair", new Color(1, 1, 1, 0.85f), UIKit.Circle);
            UIKit.Rect(crosshair.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6, 6));
            hoverTitle = UIKit.Label(h, "", 26, UIKit.Ink, TextAnchor.UpperCenter, TextTex.Serif, FontStyle.Bold);
            UIKit.Rect(hoverTitle.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1), new Vector2(0, -26), new Vector2(900, 40));
            UIKit.Shadow(hoverTitle);
            hoverInfo = UIKit.Label(h, "", 20, UIKit.Muted, TextAnchor.UpperCenter);
            UIKit.Rect(hoverInfo.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1), new Vector2(0, -62), new Vector2(900, 120));
            UIKit.Shadow(hoverInfo);
            pourText = UIKit.Label(h, "", 30, UIKit.Gold, TextAnchor.MiddleCenter, TextTex.Sans, FontStyle.Bold);
            UIKit.Rect(pourText.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 90), new Vector2(1100, 50));
            UIKit.Shadow(pourText, 2);
            // progress bar (shake/stir/muddle)
            progressRoot = UIKit.Node("progress", h);
            UIKit.Rect(progressRoot, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 170), new Vector2(420, 60));
            progressText = UIKit.Label(progressRoot.transform, "", 22, UIKit.Ink, TextAnchor.MiddleCenter);
            UIKit.Rect(progressText.gameObject, new Vector2(0, 0.5f), new Vector2(1, 1), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            progressBg = UIKit.Box(progressRoot.transform, "bg", new Color(1, 1, 1, 0.15f), UIKit.RoundedSmall);
            UIKit.Rect(progressBg.gameObject, new Vector2(0, 0), new Vector2(1, 0.4f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            progressBar = UIKit.Box(progressBg.transform, "bar", UIKit.Gold, UIKit.RoundedSmall);
            UIKit.Fill(progressBar);
            // hints (bottom)
            hints = UIKit.Label(h, "", 22, UIKit.Ink, TextAnchor.MiddleCenter);
            UIKit.Rect(hints.gameObject, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(1600, 60));
            UIKit.Shadow(hints);
            toast = UIKit.Label(h, "", 26, UIKit.Ink, TextAnchor.MiddleCenter, TextTex.Serif, FontStyle.Italic);
            UIKit.Rect(toast.gameObject, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(1000, 44));
            UIKit.Shadow(toast);
            // night intro
            var ig = UIKit.Node("intro", h);
            UIKit.Rect(ig, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 170), new Vector2(1400, 260));
            introGroup = ig.AddComponent<CanvasGroup>();
            introGroup.alpha = 0;
            introTitle = UIKit.Label(ig.transform, "", 92, UIKit.Gold, TextAnchor.MiddleCenter, TextTex.Serif, FontStyle.Italic);
            UIKit.Rect(introTitle.gameObject, new Vector2(0, 0.45f), new Vector2(1, 1), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            UIKit.Shadow(introTitle, 3);
            introSub = UIKit.Label(ig.transform, "", 28, UIKit.Ink, TextAnchor.MiddleCenter);
            UIKit.Rect(introSub.gameObject, new Vector2(0, 0), new Vector2(1, 0.45f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            UIKit.Shadow(introSub);
            // verdict popup
            var vg = UIKit.Box(h, "verdict", new Color(0.05f, 0.045f, 0.06f, 0.92f));
            UIKit.Rect(vg.gameObject, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -100), new Vector2(640, 250));
            verdictGroup = vg.gameObject.AddComponent<CanvasGroup>();
            verdictGroup.alpha = 0;
            verdictTitle = UIKit.Label(vg.transform, "", 36, UIKit.Ink, TextAnchor.UpperCenter, TextTex.Serif, FontStyle.Bold);
            UIKit.Rect(verdictTitle.gameObject, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -16), new Vector2(-40, 96));
            verdictBody = UIKit.Label(vg.transform, "", 21, UIKit.Muted, TextAnchor.UpperCenter);
            UIKit.Rect(verdictBody.gameObject, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -112), new Vector2(-50, 130));
        }

        public void NightIntro(string title, string sub)
        {
            if (hud == null) return;
            introTitle.text = title;
            introSub.text = sub;
            introTime = Time.unscaledTime;
        }

        public void ShowVerdict(Verdict v, string extra)
        {
            if (hud == null || v == null) return;
            verdictTitle.text = v.Recipe.Name + "\n<size=34><color=#efbd6b>" + UIKit.Stars(v.Stars) + "</color></size>";
            var sb = new StringBuilder();
            sb.Append(Loc.S("Score ", "Skóre ")).Append(Mathf.RoundToInt(v.Score)).Append(" / 100");
            if (!string.IsNullOrEmpty(extra)) sb.Append("    ").Append(extra);
            int n = 0;
            foreach (var note in v.Notes) { if (n++ >= 3) break; sb.Append('\n').Append("· ").Append(note); }
            if (v.Notes.Count == 0) sb.Append('\n').Append(Loc.S("Flawless!", "Bezchybné!"));
            verdictBody.text = sb.ToString();
            verdictTime = Time.unscaledTime;
        }

        void Update()
        {
            if (hud == null || Game.I == null) return;
            var g = Game.I;
            var p = Player.I;
            bool play = !g.Paused && !OverlayOpen;
            hud.SetActive(true);
            // intro / verdict fades
            float it = Time.unscaledTime - introTime;
            introGroup.alpha = it < 0.5f ? it / 0.5f : it < 4f ? 1f : Mathf.Clamp01(1f - (it - 4f) / 0.8f);
            float vt = Time.unscaledTime - verdictTime;
            verdictGroup.alpha = vt < 0.25f ? vt / 0.25f : vt < 5.5f ? 1f : Mathf.Clamp01(1f - (vt - 5.5f) / 0.6f);

            if (g.Mode == Mode.Career)
            {
                clock.text = $"{g.Career.Clock}   <size=22><color=#c9c0b2>{Loc.S("Night", "Noc")} {Save.Night}</color></size>";
                money.text = UIKit.Money(Save.Money) + "    " + UIKit.Stars(Mathf.RoundToInt(Save.Reputation)) + (g.Career.Closing ? "    <color=#efbd6b>" + Loc.S("Last call", "Posledná objednávka") + "</color>" : "");
            }
            else
            {
                clock.text = Loc.S("Free mixing", "Voľné miešanie");
                money.text = Loc.S("Put a drink on the gold mat to taste it", "Polož drink na zlatú podložku a ochutnaj");
            }
            // tickets
            if (g.Mode == Mode.Career)
            {
                ticketsBg.gameObject.SetActive(true);
                var sb = new StringBuilder();
                sb.Append("<color=#efbd6b><b>").Append(Loc.S("Orders", "Objednávky")).Append("</b></color>   <size=18><color=#c9c0b2>F ").Append(Loc.S("switch", "prepnúť")).Append("</color></size>\n");
                int count = 0;
                foreach (var guest in g.Career.Seats)
                {
                    if (guest == null || !guest.Waiting) continue;
                    count++;
                    bool focus = guest == g.FocusGuest;
                    float f = guest.PatienceFrac;
                    string col = f > 0.5f ? "#8ce08c" : f > 0.25f ? "#ffcc59" : "#ff7366";
                    int bars = Mathf.CeilToInt(f * 10);
                    sb.Append(focus ? "<color=#efbd6b>▶ </color>" : "   ").Append("<b>").Append(guest.Order.Name).Append("</b>  <size=17><color=#c9c0b2>").Append(guest.GuestName).Append(" · ").Append(Loc.S("seat", "miesto")).Append(' ').Append(guest.Seat + 1).Append("</color></size>\n");
                    sb.Append("     <color=").Append(col).Append(">").Append(new string('▮', bars)).Append("</color><color=#ffffff30>").Append(new string('▮', 10 - bars)).Append("</color>\n");
                }
                if (count == 0) sb.Append("<color=#c9c0b2>").Append(g.Career.Closing ? Loc.S("Closing time…", "Zatvárame…") : Loc.S("Waiting for guests…", "Čakáme na hostí…")).Append("</color>");
                ticketsText.text = sb.ToString();
                ticketsBg.rectTransform.sizeDelta = new Vector2(380, 70 + Mathf.Max(1, count) * 62);
            }
            else ticketsBg.gameObject.SetActive(false);

            // recipe card
            var r = g.FocusRecipe;
            if (Save.ShowHints)
            {
                if (r != null) card.text = RecipeCard(r, p != null ? p.Focus : null);
                else card.text = Loc.S("<color=#efbd6b><b>Free mixing</b></color>\n\nPress <b>Tab</b> and pick a drink to practise, or mix anything you like and put it on the gold tasting mat.",
                                       "<color=#efbd6b><b>Voľné miešanie</b></color>\n\nStlač <b>Tab</b> a vyber drink na tréning, alebo namiešaj čokoľvek a polož to na zlatú podložku.");
                cardBg.color = UIKit.Panel;
                cardBg.rectTransform.sizeDelta = new Vector2(430, Mathf.Clamp(card.preferredHeight + 40, 160, 760));
                cardArrow.text = "▶";
            }
            else
            {
                // collapsed: only the arrow tab stays at the screen edge
                card.text = "";
                cardBg.color = new Color(0, 0, 0, 0);
                cardBg.rectTransform.sizeDelta = new Vector2(0, 54);
                cardArrow.text = "◀ <size=16>H</size>";
            }

            // hover and hints
            if (p != null && play)
            {
                string title = null, info = null;
                if (p.Held != null && p.HoverItem == null && p.HoverStation == null)
                {
                    title = p.Held.Title;
                    info = p.Held.Info;
                }
                if (p.HoverItem != null)
                {
                    title = p.HoverItem.Locked ? Loc.S("Guest's drink", "Drink hosťa") : p.HoverItem.Title;
                    info = p.HoverItem.Locked ? "" : p.HoverItem.Info;
                }
                else if (p.HoverStation != null) { title = p.HoverStation.Title; info = ""; }
                hoverTitle.text = title ?? "";
                hoverInfo.text = info ?? "";
                bool hot = p.HoverItem != null || p.HoverStation != null;
                crosshair.rectTransform.sizeDelta = Vector2.Lerp(crosshair.rectTransform.sizeDelta, hot ? new Vector2(14, 14) : new Vector2(6, 6), Time.unscaledDeltaTime * 14f);
                crosshair.color = hot ? UIKit.Gold : new Color(1, 1, 1, 0.85f);
                hints.text = Save.ShowHints ? p.ActionHints() : "<color=#ffffff60><b>H</b>  " + Loc.S("show hints", "zobraziť nápovedy") + "</color>";
                pourText.text = p.PourText ?? "";
                toast.text = Time.time - p.ToastTime < 2.6f ? p.Toast : "";
                // progress
                Vessel pv = p.Shaking ? p.Held as Vessel : (p.Stirring || p.Muddling) ? p.Focus : null;
                progressRoot.SetActive(pv != null);
                if (pv != null)
                {
                    float val; string label;
                    if (p.Shaking) { val = pv.C.Shake; label = pv.Ice > 0 ? Loc.S("Shaking", "Trasenie") : Loc.S("Dry shake: add ice!", "Bez ľadu: pridaj ľad!"); }
                    else if (p.Stirring) { val = pv.C.Stir; label = pv.Ice > 0 ? Loc.S("Stirring", "Miešanie") : Loc.S("Stirring without ice", "Miešanie bez ľadu"); }
                    else { val = pv.Muddle; label = pv.Garnishes.Contains(Garnish.Mint) ? Loc.S("Muddling", "Drvenie") : Loc.S("Add mint to muddle", "Pridaj mätu"); }
                    progressText.text = label + "  " + Mathf.RoundToInt(Mathf.Min(val, 3f) * 100) + "%" + (val >= 1f && val < 2.4f ? "  <color=#8ce08c>✓</color>" : val >= 2.4f ? "  <color=#ff7366>" + Loc.S("too much", "priveľa") + "</color>" : "");
                    progressBar.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(val), 1);
                    progressBar.color = val < 1f ? UIKit.Gold : val < 2.4f ? UIKit.Good : UIKit.Bad;
                }
            }
            else
            {
                hoverTitle.text = hoverInfo.text = hints.text = pourText.text = toast.text = "";
                progressRoot.SetActive(false);
            }
            crosshair.enabled = play;
        }

        static string Check(bool ok) => ok ? "<color=#8ce08c>✓</color> " : "<color=#ffffff40>○</color> ";

        /// <summary>Recipe card that ticks off steps against the vessel you are working with.</summary>
        static string RecipeCard(Recipe r, Vessel v)
        {
            var sb = new StringBuilder();
            sb.Append("<size=34><b>").Append(r.Name).Append("</b></size>\n");
            sb.Append("<color=#c9c0b2><size=19>").Append(Recipes.MethodName(r)).Append("</size></color>\n\n");
            bool glassOk = v != null && v.VKind == VesselKind.Glass && v.GType == r.Glass;
            sb.Append(Check(glassOk)).Append(Recipes.GlassName(r.Glass)).Append('\n');
            foreach (var part in r.Parts)
            {
                float have = v != null ? v.C.Get(part.ing) : 0;
                bool ok = Recipes.PartOk(have, part.ml);
                bool over = have > part.ml && !ok;
                sb.Append(Check(ok)).Append(part.ing.Kind == Kind.Bitters ? $"{part.ml:0} dash  " : $"{part.ml:0} ml  ").Append(part.ing.Name);
                if (v != null && have > 0.05f)
                    sb.Append("  <size=18><color=").Append(ok ? "#8ce08c" : over ? "#ff7366" : "#efbd6b").Append(">").Append(have.ToString(have < 5 ? "0.0" : "0")).Append("</color></size>");
                sb.Append('\n');
            }
            if (r.Method == Method.Shake) sb.Append(Check(v != null && v.C.Shake >= 0.85f && v.C.Chill > 0.5f)).Append(Loc.S("Shake with ice", "Pretrepať s ľadom")).Append('\n');
            if (r.Method == Method.Stir) sb.Append(Check(v != null && v.C.Stir >= 0.85f && v.C.Chill > 0.5f)).Append(Loc.S("Stir with ice", "Miešať s ľadom")).Append('\n');
            if (r.Method == Method.Muddle) sb.Append(Check(v != null && v.Muddle >= 0.9f)).Append(Loc.S("Muddle the mint", "Rozdrviť mätu")).Append('\n');
            if (r.Strain) sb.Append(Check(v != null && v.VKind == VesselKind.Glass && v.Ml > 10 && !v.C.Unstrained)).Append(Loc.S("Strain into the glass", "Preceďiť do pohára")).Append('\n');
            if (r.Ice) sb.Append(Check(v != null && v.VKind == VesselKind.Glass && v.Ice >= 3)).Append(Loc.S("Fresh ice in the glass", "Čerstvý ľad v pohári")).Append('\n');
            else sb.Append(Check(v != null && v.VKind == VesselKind.Glass && v.Ice == 0 && v.Ml > 10)).Append(Loc.S("No ice in the glass", "Bez ľadu v pohári")).Append('\n');
            if (r.Garnish.HasValue) sb.Append(Check(v != null && v.Garnishes.Contains(r.Garnish.Value))).Append(Recipes.GarnishName(r.Garnish.Value)).Append('\n');
            sb.Append("\n<size=18><color=#c9c0b2>").Append(v != null ? Loc.S("Checking: ", "Kontrolujem: ") + v.Title + "  ·  " + v.Ml.ToString("0") + " ml" : Loc.S("Pick up a glass or the shaker", "Vezmi pohár alebo šejker")).Append("</color></size>");
            return sb.ToString();
        }
    }
}
