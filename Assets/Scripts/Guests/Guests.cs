using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Bartender
{
    /// <summary>A guest at the bar: walks in, sits, orders, waits, drinks, leaves.</summary>
    public sealed class Guest : MonoBehaviour
    {
        public enum St { Arrive, Settle, Think, Wait, React, Drink, Rise, Leave }
        public St State;
        public int Seat;
        public Recipe Order;
        public float Patience, PatienceMax;
        public Mannequin Body;
        public Vessel Glass;
        public Verdict Result;
        public string GuestName;
        public bool Angry, Served;
        public float Paid, Tip;
        public float OrderTime;

        readonly List<Vector3> path = new List<Vector3>();
        int pi;
        float t;
        float sipTimer;
        Canvas bubble;
        Text bubbleTitle, bubbleSub;
        Image bubbleBar, bubbleBg;

        static readonly string[] Names = { "Emma", "Lukas", "Sofia", "Noah", "Mia", "Leon", "Ella", "Adam", "Nina", "Oliver", "Zoe", "Hugo", "Lea", "Max", "Ivy", "Theo", "Ava", "Jakub", "Lily", "Marek", "Clara", "Samuel", "Eva", "Daniel" };

        public static Guest Spawn(int seat, Recipe order, float patience, int seed, Transform parent)
        {
            var body = Mannequin.Create(seed, parent);
            var g = body.gameObject.AddComponent<Guest>();
            g.Body = body;
            g.Seat = seat;
            g.Order = order;
            g.PatienceMax = g.Patience = patience;
            g.GuestName = Names[Mathf.Abs(seed) % Names.Length];
            float x = Layout.SeatX[seat];
            g.transform.position = Layout.Entrance;
            g.path.Add(new Vector3(-9f, 0, Layout.AisleZ));
            g.path.Add(new Vector3(x, 0, Layout.AisleZ));
            g.path.Add(new Vector3(x, 0, Layout.StoolZ + 0.32f));
            g.State = St.Arrive;
            body.Pose = Mannequin.PoseKind.Walk;
            g.MakeBubble();
            return g;
        }

        void MakeBubble()
        {
            var go = new GameObject("bubble", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            bubble = go.AddComponent<Canvas>();
            bubble.renderMode = RenderMode.WorldSpace;
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(300, 112);
            go.transform.localScale = Vector3.one * 0.0016f;
            bubbleBg = UIKit.Box(go.transform, "bg", new Color(0.05f, 0.045f, 0.06f, 0.88f));
            UIKit.Fill(bubbleBg);
            bubbleTitle = UIKit.Label(go.transform, "", 34, UIKit.Ink, TextAnchor.MiddleCenter, TextTex.Serif, FontStyle.Bold);
            UIKit.Rect(bubbleTitle.gameObject, new Vector2(0, 0.45f), new Vector2(1, 1), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-20, 0));
            bubbleSub = UIKit.Label(go.transform, "", 20, UIKit.Muted, TextAnchor.MiddleCenter);
            UIKit.Rect(bubbleSub.gameObject, new Vector2(0, 0.18f), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-20, 0));
            var barBg = UIKit.Box(go.transform, "bar bg", new Color(1, 1, 1, 0.12f), UIKit.RoundedSmall);
            UIKit.Rect(barBg.gameObject, new Vector2(0.08f, 0.06f), new Vector2(0.92f, 0.14f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            bubbleBar = UIKit.Box(barBg.transform, "bar", UIKit.Good, UIKit.RoundedSmall);
            UIKit.Fill(bubbleBar);
            bubble.gameObject.SetActive(false);
        }

        public float PatienceFrac => Mathf.Clamp01(Patience / Mathf.Max(1f, PatienceMax));
        public bool Waiting => State == St.Wait;
        public Vector3 CoasterPos => new Vector3(Layout.SeatX[Seat], Layout.BarTopY, Layout.CoasterZ);

        void Update()
        {
            float dt = Time.deltaTime;
            t += dt;
            switch (State)
            {
                case St.Arrive:
                case St.Leave:
                    Walk(dt);
                    break;
                case St.Settle:
                    {
                        // slide onto the stool, facing the bar
                        var seatPos = new Vector3(Layout.SeatX[Seat], 0, Layout.StoolZ - 0.04f);
                        transform.position = Vector3.Lerp(transform.position, seatPos, dt * 3f);
                        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0, 180f, 0), dt * 5f);
                        Body.Pose = Mannequin.PoseKind.Sit;
                        if (t > 1.2f) { State = St.Think; t = 0; }
                        break;
                    }
                case St.Think:
                    Body.LookYaw = Mathf.Sin(t * 1.3f) * 25f;
                    if (t > 1.8f)
                    {
                        State = St.Wait; t = 0;
                        OrderTime = Time.time;
                        Body.LookYaw = 0;
                        bubble.gameObject.SetActive(true);
                        Sfx.Play("bell", transform.position + Vector3.up * 1.2f, 0.35f);
                    }
                    break;
                case St.Wait:
                    Patience -= dt;
                    Body.Wave = Mathf.MoveTowards(Body.Wave, PatienceFrac < 0.35f && Mathf.Repeat(t, 6f) < 1.4f ? 1f : 0f, dt * 3f);
                    Body.LookYaw = Mathf.Sin(t * 0.4f) * 10f;
                    if (Patience <= 0)
                    {
                        Angry = true;
                        Leave();
                    }
                    break;
                case St.React:
                    Body.Wave = Mathf.MoveTowards(Body.Wave, 0, dt * 3f);
                    if (t > 1.6f)
                    {
                        t = 0;
                        if (Result != null && Result.Stars >= 2 && Glass != null)
                        {
                            State = St.Drink;
                            Glass.transform.SetParent(Body.RightHand, true);
                            sipTimer = 0.5f;
                        }
                        else Leave();
                    }
                    break;
                case St.Drink:
                    {
                        sipTimer -= dt;
                        float cycle = Mathf.Repeat(t, 4.2f);
                        float target = cycle > 2.6f ? 1f : 0f;
                        Body.Drink = Mathf.MoveTowards(Body.Drink, target, dt * 1.8f);
                        if (Glass != null)
                        {
                            Glass.transform.localPosition = Vector3.Lerp(Glass.transform.localPosition, new Vector3(0, -0.05f, 0.02f), dt * 6f);
                            Glass.transform.rotation = Quaternion.Slerp(Glass.transform.rotation, Quaternion.Euler(Body.Drink * -38f, transform.eulerAngles.y, 0), dt * 6f);
                            if (Body.Drink > 0.9f && Glass.Ml > 1f) Glass.C.Take(dt * 9f);
                        }
                        if (t > 11f) { State = St.Rise; t = 0; }
                        break;
                    }
                case St.Rise:
                    Body.Drink = Mathf.MoveTowards(Body.Drink, 0, dt * 2f);
                    if (t > 0.6f) Leave();
                    break;
            }
            UpdateBubble();
        }

        public void Leave()
        {
            bubble.gameObject.SetActive(false);
            if (Glass != null && State == St.Drink) { Destroy(Glass.gameObject); Glass = null; }
            Body.Pose = Mannequin.PoseKind.Sit;
            State = St.Leave;
            t = 0;
            path.Clear();
            float x = Layout.SeatX[Seat];
            path.Add(new Vector3(x, 0, Layout.StoolZ + 0.4f));
            path.Add(new Vector3(x, 0, Layout.AisleZ));
            path.Add(new Vector3(-9f, 0, Layout.AisleZ));
            path.Add(Layout.Entrance + new Vector3(-0.6f, 0, -0.4f));
            pi = 0;
        }

        void Walk(float dt)
        {
            if (State == St.Leave && t < 0.6f) return; // stand up first
            Body.Pose = Mannequin.PoseKind.Walk;
            if (pi >= path.Count)
            {
                if (State == St.Arrive) { State = St.Settle; t = 0; Body.Pose = Mannequin.PoseKind.Sit; }
                else Destroy(gameObject);
                return;
            }
            var target = path[pi];
            var to = target - transform.position;
            to.y = 0;
            float step = Body.WalkSpeed * dt;
            if (to.magnitude <= step) { transform.position = target; pi++; return; }
            transform.position += to.normalized * step;
            var look = Quaternion.LookRotation(to.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, dt * 8f);
        }

        void UpdateBubble()
        {
            if (!bubble.gameObject.activeSelf) return;
            var cam = Player.I != null ? Player.I.Cam : Camera.main;
            if (cam == null) return;
            bubble.transform.position = transform.position + Vector3.up * 1.95f;
            bubble.transform.rotation = Quaternion.LookRotation(bubble.transform.position - cam.transform.position);
            bubbleTitle.text = Order.Name;
            bubbleSub.text = Recipes.GlassName(Order.Glass);
            float f = PatienceFrac;
            var rt = bubbleBar.rectTransform;
            rt.anchorMax = new Vector2(Mathf.Max(0.02f, f), 1);
            bubbleBar.color = f > 0.5f ? UIKit.Good : f > 0.25f ? new Color(1f, 0.8f, 0.35f) : UIKit.Bad;
        }

        /// <summary>The bartender put a glass on this guest's coaster.</summary>
        public void Serve(Vessel glass, Verdict v, float paid, float tip)
        {
            Served = true;
            Glass = glass;
            Result = v;
            Paid = paid; Tip = tip;
            glass.Locked = true;
            glass.Served = true;
            bubble.gameObject.SetActive(false);
            State = St.React;
            t = 0;
            if (v.Stars <= 1) { Angry = true; glass.Locked = false; glass.Served = false; Glass = null; }
        }
    }

    /// <summary>Runs a career night: spawns guests, checks served drinks, pays out.</summary>
    public sealed class Career : MonoBehaviour
    {
        public const float NightLength = 480f;
        public float Elapsed;
        public bool Running, Closing;
        public readonly Guest[] Seats = new Guest[5];
        public int ServedCount, PerfectCount, Walkouts;
        public float Earned, Tips, StarSum;
        public System.Action<Guest, Verdict, float, float> OnServed;
        public System.Action<Guest> OnWalkout;
        public System.Action OnNightOver;
        float spawnTimer;
        int seedCounter;
        Transform guestRoot;

        public string Clock
        {
            get
            {
                float h = 20f + Mathf.Clamp01(Elapsed / NightLength) * 6f;
                int hh = Mathf.FloorToInt(h) % 24, mm = Mathf.FloorToInt((h - Mathf.Floor(h)) * 60f);
                return $"{hh:00}:{mm:00}";
            }
        }

        public static List<Recipe> Unlocked(int night)
        {
            var l = new List<Recipe>();
            foreach (var r in Recipes.All) if (r.UnlockNight <= night) l.Add(r);
            return l;
        }

        public void StartNight()
        {
            if (guestRoot == null) guestRoot = new GameObject("Guests").transform;
            Clear();
            Elapsed = 0;
            Running = true;
            Closing = false;
            spawnTimer = 4f;
            ServedCount = PerfectCount = Walkouts = 0;
            Earned = Tips = StarSum = 0;
            seedCounter = Save.Night * 1000 + Random.Range(0, 999);
        }

        public void Clear()
        {
            for (int i = 0; i < Seats.Length; i++)
            {
                if (Seats[i] != null)
                {
                    if (Seats[i].Glass != null) Destroy(Seats[i].Glass.gameObject);
                    Destroy(Seats[i].gameObject);
                }
                Seats[i] = null;
            }
            if (guestRoot != null)
                for (int i = guestRoot.childCount - 1; i >= 0; i--) Destroy(guestRoot.GetChild(i).gameObject);
        }

        void Update()
        {
            if (!Running) return;
            float dt = Time.deltaTime;
            Elapsed += dt;
            // free seats whose guest has left
            for (int i = 0; i < Seats.Length; i++)
            {
                var g = Seats[i];
                if (g == null) continue;
                if (g.State == Guest.St.Leave && !g.Served && g.Angry && !walkoutCounted.Contains(g))
                {
                    walkoutCounted.Add(g);
                    Walkouts++;
                    Save.Reputation = Mathf.Clamp(Save.Reputation - 0.15f, 0, 5);
                    OnWalkout?.Invoke(g);
                }
                if (g.State == Guest.St.Leave) Seats[i] = null;
            }
            Closing = Elapsed > NightLength * 0.88f;
            if (!Closing)
            {
                spawnTimer -= dt;
                if (spawnTimer <= 0) TrySpawn();
            }
            if (Elapsed >= NightLength && AllGone())
            {
                Running = false;
                OnNightOver?.Invoke();
            }
        }

        readonly HashSet<Guest> walkoutCounted = new HashSet<Guest>();

        bool AllGone()
        {
            for (int i = 0; i < Seats.Length; i++) if (Seats[i] != null) return false;
            return guestRoot == null || guestRoot.childCount == 0;
        }

        void TrySpawn()
        {
            var free = new List<int>();
            for (int i = 0; i < Save.Seats; i++) if (Seats[i] == null && !GlassOnCoaster(i)) free.Add(i);
            float busy = 1f - free.Count / (float)Save.Seats;
            if (free.Count == 0) { spawnTimer = 3f; return; }
            int seat = free[Random.Range(0, free.Count)];
            var menu = Unlocked(Save.Night);
            // newer recipes a little more often
            Recipe r = menu[Random.Range(0, menu.Count)];
            if (Random.value < 0.35f)
            {
                var fresh = menu.FindAll(x => x.UnlockNight >= Save.Night - 1);
                if (fresh.Count > 0) r = fresh[Random.Range(0, fresh.Count)];
            }
            // generous waiting time: about 2.5 to 4.5 minutes depending on the drink (more on the first nights)
            float patience = 140f + r.Complexity * 16f + (Save.Night <= 3 ? 60f : 0f);
            if (Save.Has(Upgrade.LoungeDJ)) patience *= 1.3f;
            Seats[seat] = Guest.Spawn(seat, r, patience, seedCounter++, guestRoot);
            float pace = Mathf.Lerp(34f, 16f, Mathf.Clamp01((Save.Night - 1) / 8f));
            spawnTimer = Random.Range(pace * 0.6f, pace * 1.2f) * (0.6f + busy * 0.8f);
        }

        static bool GlassOnCoaster(int seat)
        {
            var p = new Vector3(Layout.SeatX[seat], Layout.BarTopY, Layout.CoasterZ);
            foreach (var c in Physics.OverlapSphere(p + Vector3.up * 0.05f, 0.08f))
            {
                var v = c.GetComponentInParent<Vessel>();
                if (v != null && !v.Held) return true;
            }
            return false;
        }

        /// <summary>Returns true if the placed glass was handed to a waiting guest.</summary>
        public bool TryServe(Vessel glass, ServeSpot spot)
        {
            if (!Running || spot == null || spot.Seat < 0 || glass.VKind != VesselKind.Glass) return false;
            var g = Seats[spot.Seat];
            if (g == null || !g.Waiting) return false;
            var v = Recipes.Evaluate(g.Order, glass.GType, glass.C, glass.Ice, glass.Garnishes, glass.Muddle);
            float price = g.Order.Price * (Save.Has(Upgrade.PremiumMenu) ? 1.2f : 1f);
            float paid = v.Stars >= 2 ? price * Mathf.Lerp(0.6f, 1f, v.Score / 100f) : 0f;
            float speed = 1f + g.PatienceFrac * 0.6f;
            float tip = v.Stars >= 3 ? price * 0.1f * (v.Stars - 2) * speed * (Save.Has(Upgrade.TipJar) ? 1.4f : 1f) : 0f;
            paid = Mathf.Round(paid * 100f) / 100f;
            tip = Mathf.Round(tip * 100f) / 100f;
            g.Serve(glass, v, paid, tip);
            if (v.Stars >= 2)
            {
                ServedCount++;
                StarSum += v.Stars;
                if (v.Stars == 5) PerfectCount++;
                Earned += paid;
                Tips += tip;
                Save.Money += paid + tip;
                Save.Served++;
                if (v.Stars == 5) Save.Perfect++;
            }
            Save.Reputation = Mathf.Clamp(Save.Reputation + (v.Stars - 3) * 0.06f, 0, 5);
            OnServed?.Invoke(g, v, paid, tip);
            return true;
        }

        public Guest MostUrgent()
        {
            Guest best = null;
            foreach (var g in Seats)
                if (g != null && g.Waiting && (best == null || g.Patience < best.Patience)) best = g;
            return best;
        }
    }
}
