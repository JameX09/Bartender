using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Bartender
{
    public enum Method { Build, Shake, Stir, Muddle }

    public sealed class Recipe
    {
        public string Key, NameEn, NameSk, DescEn, DescSk;
        public GlassType Glass;
        public Method Method;
        public bool Strain;          // shaken/stirred drink poured through a strainer
        public bool Ice;             // served over ice
        public Garnish? Garnish;
        public List<(Ingredient ing, float ml)> Parts = new List<(Ingredient, float)>();
        public float Price;
        public int UnlockNight;
        public Color Accent;

        public string Name => Loc.En ? NameEn : NameSk;
        public string Desc => Loc.En ? DescEn : DescSk;
        public float Volume { get { float v = 0; foreach (var p in Parts) v += p.ml; return v; } }
        public int Complexity => Parts.Count + (Method == Method.Build ? 0 : 2) + (Garnish.HasValue ? 1 : 0);
    }

    public sealed class Verdict
    {
        public Recipe Recipe;
        public float Score;          // 0..100
        public int Stars;            // 1..5
        public float IngredientScore, GlassScore, MethodScore, IceScore, GarnishScore;
        public readonly List<string> Notes = new List<string>();
    }

    public static class Recipes
    {
        public static readonly List<Recipe> All = new List<Recipe>();

        static Recipes()
        {
            R("gintonic", "Gin & Tonic", "Gin s tonikom", "Crisp, bitter and refreshing.", "Svieži, horkastý a osviežujúci.",
                GlassType.Highball, Method.Build, false, true, Garnish.LimeWedge, 8f, 1, new Color(0.6f, 0.85f, 0.95f),
                ("gin", 50), ("tonic", 120));
            R("cubalibre", "Cuba Libre", "Cuba Libre", "Rum and cola with a squeeze of lime.", "Rum s kolou a limetkou.",
                GlassType.Highball, Method.Build, false, true, Garnish.LimeWedge, 8f, 1, new Color(0.6f, 0.3f, 0.15f),
                ("rum", 50), ("lime", 10), ("cola", 120));
            R("screwdriver", "Screwdriver", "Skrutkovač", "Vodka and fresh orange juice.", "Vodka a čerstvý pomarančový džús.",
                GlassType.Highball, Method.Build, false, true, Garnish.OrangePeel, 8f, 1, new Color(1f, 0.6f, 0.1f),
                ("vodka", 50), ("orange", 120));
            R("daiquiri", "Daiquiri", "Daiquiri", "Rum, lime and sugar, shaken hard and served up.", "Rum, limetka a cukor, poriadne pretrepané, bez ľadu.",
                GlassType.Coupe, Method.Shake, true, false, Garnish.LimeWedge, 10f, 1, new Color(0.85f, 0.95f, 0.7f),
                ("rum", 60), ("lime", 25), ("simple", 15));
            R("margarita", "Margarita", "Margarita", "Tequila, orange liqueur and lime.", "Tequila, pomarančový likér a limetka.",
                GlassType.Coupe, Method.Shake, true, false, Garnish.LimeWedge, 11f, 2, new Color(0.8f, 0.95f, 0.55f),
                ("tequila", 50), ("triplesec", 25), ("lime", 25));
            R("mojito", "Mojito", "Mojito", "Muddled mint, rum, lime and soda over ice.", "Rozdrvená mäta, rum, limetka a sóda s ľadom.",
                GlassType.Highball, Method.Muddle, false, true, Garnish.Mint, 11f, 2, new Color(0.5f, 0.9f, 0.5f),
                ("rum", 50), ("lime", 25), ("simple", 20), ("soda", 60));
            R("cosmopolitan", "Cosmopolitan", "Cosmopolitan", "Vodka, orange liqueur, lime and cranberry.", "Vodka, pomarančový likér, limetka a brusnice.",
                GlassType.Martini, Method.Shake, true, false, Garnish.OrangePeel, 12f, 3, new Color(0.95f, 0.3f, 0.45f),
                ("vodka", 40), ("triplesec", 15), ("lime", 15), ("cranberry", 30));
            R("moscowmule", "Moscow Mule", "Moscow Mule", "Vodka, lime and spicy ginger beer in a copper mug.", "Vodka, limetka a pikantné zázvorové pivo v medenom hrnčeku.",
                GlassType.Mug, Method.Build, false, true, Garnish.LimeWedge, 11f, 3, new Color(0.85f, 0.55f, 0.3f),
                ("vodka", 50), ("lime", 15), ("gingerbeer", 120));
            R("negroni", "Negroni", "Negroni", "Equal parts gin, bitter aperitivo and sweet vermouth, stirred.", "Rovnaké diely ginu, bitteru a sladkého vermutu, miešané.",
                GlassType.Rocks, Method.Stir, false, true, Garnish.OrangePeel, 12f, 4, new Color(0.85f, 0.15f, 0.12f),
                ("gin", 30), ("campari", 30), ("sweetvermouth", 30));
            R("oldfashioned", "Old Fashioned", "Old Fashioned", "Bourbon, sugar and bitters, stirred over ice.", "Bourbon, cukor a bitters, miešané s ľadom.",
                GlassType.Rocks, Method.Stir, false, true, Garnish.OrangePeel, 12f, 4, new Color(0.8f, 0.45f, 0.15f),
                ("bourbon", 60), ("simple", 10), ("bitters", 2));
            R("martini", "Dry Martini", "Suché Martini", "Gin and a whisper of dry vermouth, stirred and strained.", "Gin a štipka suchého vermutu, miešané a precedené.",
                GlassType.Martini, Method.Stir, true, false, Garnish.Olive, 13f, 5, new Color(0.85f, 0.92f, 0.8f),
                ("gin", 60), ("dryvermouth", 10));
            R("manhattan", "Manhattan", "Manhattan", "Bourbon, sweet vermouth and bitters, stirred and strained.", "Bourbon, sladký vermut a bitters, miešané a precedené.",
                GlassType.Coupe, Method.Stir, true, false, Garnish.Cherry, 13f, 5, new Color(0.6f, 0.2f, 0.12f),
                ("bourbon", 60), ("sweetvermouth", 30), ("bitters", 2));
            R("whiskeysour", "Whiskey Sour", "Whiskey Sour", "Bourbon, lemon and sugar, shaken and strained over ice.", "Bourbon, citrón a cukor, pretrepané a precedené na ľad.",
                GlassType.Rocks, Method.Shake, true, true, Garnish.Cherry, 12f, 6, new Color(0.95f, 0.75f, 0.35f),
                ("bourbon", 50), ("lemon", 25), ("simple", 20));
            R("spritz", "Orange Spritz", "Pomarančový spritz", "Orange aperitivo, prosecco and a splash of soda.", "Pomarančový aperitív, prosecco a strek sódy.",
                GlassType.Wine, Method.Build, false, true, Garnish.OrangePeel, 11f, 6, new Color(1f, 0.45f, 0.1f),
                ("aperol", 60), ("prosecco", 90), ("soda", 30));
            R("sunrise", "Tequila Sunrise", "Tequila Sunrise", "Tequila and orange juice with a grenadine sunrise.", "Tequila a pomarančový džús s grenadínovým východom slnka.",
                GlassType.Highball, Method.Build, false, true, Garnish.Cherry, 11f, 7, new Color(1f, 0.4f, 0.15f),
                ("tequila", 50), ("orange", 100), ("grenadine", 15));
            R("bluelagoon", "Blue Lagoon", "Modrá lagúna", "Vodka, blue curaçao, lemon and soda.", "Vodka, modré curaçao, citrón a sóda.",
                GlassType.Highball, Method.Build, false, true, Garnish.LemonTwist, 11f, 7, new Color(0.2f, 0.55f, 1f),
                ("vodka", 40), ("curacao", 20), ("lemon", 20), ("simple", 10), ("soda", 80));
            R("pinacolada", "Piña Colada", "Piña Colada", "Rum, coconut cream and pineapple, shaken and strained over ice.", "Rum, kokosový krém a ananás, pretrepané a precedené na ľad.",
                GlassType.Highball, Method.Shake, true, true, Garnish.Cherry, 12f, 8, new Color(1f, 0.92f, 0.65f),
                ("rum", 50), ("coconut", 30), ("pineapple", 90));
            R("espressomartini", "Espresso Martini", "Espresso Martini", "Vodka, coffee liqueur and fresh espresso, shaken hard.", "Vodka, kávový likér a čerstvé espresso, poriadne pretrepané.",
                GlassType.Martini, Method.Shake, true, false, null, 13f, 8, new Color(0.45f, 0.28f, 0.15f),
                ("vodka", 50), ("coffeeliq", 20), ("espresso", 30), ("simple", 10));
            R("longisland", "Long Island Iced Tea", "Long Island Iced Tea", "Five spirits, lemon, sugar and a splash of cola.", "Päť destilátov, citrón, cukor a strek koly.",
                GlassType.Highball, Method.Build, false, true, Garnish.LemonTwist, 14f, 9, new Color(0.7f, 0.42f, 0.2f),
                ("vodka", 15), ("gin", 15), ("rum", 15), ("tequila", 15), ("triplesec", 15), ("lemon", 25), ("simple", 15), ("cola", 30));
            R("tomcollins", "Tom Collins", "Tom Collins", "Gin, lemon and sugar topped with soda.", "Gin, citrón a cukor zaliate sódou.",
                GlassType.Highball, Method.Build, false, true, Garnish.LemonTwist, 10f, 9, new Color(0.95f, 0.95f, 0.7f),
                ("gin", 50), ("lemon", 25), ("simple", 15), ("soda", 60));
        }

        static void R(string key, string en, string sk, string dEn, string dSk, GlassType glass, Method method, bool strain, bool ice,
            Garnish? garnish, float price, int night, Color accent, params (string key, float ml)[] parts)
        {
            var r = new Recipe
            {
                Key = key, NameEn = en, NameSk = sk, DescEn = dEn, DescSk = dSk, Glass = glass, Method = method, Strain = strain,
                Ice = ice, Garnish = garnish, Price = price, UnlockNight = night, Accent = accent,
            };
            foreach (var p in parts)
            {
                var ing = Ingredients.Get(p.key);
                if (ing == null) { Debug.LogError("Unknown ingredient " + p.key); continue; }
                r.Parts.Add((ing, p.ml));
            }
            All.Add(r);
        }

        public static Recipe Get(string key) => All.Find(r => r.Key == key);

        // ---------- names used in UI ----------

        public static string GlassName(GlassType g)
        {
            switch (g)
            {
                case GlassType.Highball: return Loc.S("Highball glass", "Pohár highball");
                case GlassType.Rocks: return Loc.S("Rocks glass", "Pohár rocks");
                case GlassType.Coupe: return Loc.S("Coupe", "Pohár coupe");
                case GlassType.Martini: return Loc.S("Martini glass", "Martini pohár");
                case GlassType.Wine: return Loc.S("Wine glass", "Vínový pohár");
                default: return Loc.S("Copper mug", "Medený hrnček");
            }
        }

        public static string GarnishName(Garnish g)
        {
            switch (g)
            {
                case Garnish.LimeWedge: return Loc.S("Lime wedge", "Mesiačik limetky");
                case Garnish.LemonTwist: return Loc.S("Lemon twist", "Citrónová kôra");
                case Garnish.OrangePeel: return Loc.S("Orange peel", "Pomarančová kôra");
                case Garnish.Cherry: return Loc.S("Cocktail cherry", "Koktailová čerešňa");
                case Garnish.Mint: return Loc.S("Mint sprig", "Vetvička mäty");
                default: return Loc.S("Olive", "Oliva");
            }
        }

        public static string MethodName(Recipe r)
        {
            switch (r.Method)
            {
                case Method.Shake: return r.Ice ? Loc.S("Shake with ice, strain over fresh ice", "Pretrepať s ľadom, preceďiť na čerstvý ľad")
                                                : Loc.S("Shake with ice, strain, serve up", "Pretrepať s ľadom, preceďiť, bez ľadu");
                case Method.Stir: return r.Strain ? Loc.S("Stir with ice, strain, serve up", "Miešať s ľadom, preceďiť, bez ľadu")
                                                  : Loc.S("Stir over ice in the glass", "Miešať s ľadom priamo v pohári");
                case Method.Muddle: return Loc.S("Muddle mint in the glass, add ice, build", "Rozdrviť mätu v pohári, pridať ľad a naliať");
                default: return Loc.S("Build over ice in the glass", "Naliať priamo do pohára s ľadom");
            }
        }

        public static string ShortMethod(Method m)
        {
            switch (m)
            {
                case Method.Shake: return Loc.S("Shaken", "Pretrepané");
                case Method.Stir: return Loc.S("Stirred", "Miešané");
                case Method.Muddle: return Loc.S("Muddled", "Drvené");
                default: return Loc.S("Built", "Liate");
            }
        }

        // ---------- scoring ----------

        /// <summary>How well a single ingredient amount matches (0..1).</summary>
        public static float PartAccuracy(float actual, float expected)
        {
            float tol = Mathf.Max(expected * 0.12f, expected <= 3f ? 1f : 3f);
            float err = Mathf.Abs(actual - expected);
            if (err <= tol) return 1f;
            float span = Mathf.Max(expected * 0.7f, 4f);
            return Mathf.Clamp01(1f - (err - tol) / span);
        }

        public static bool PartOk(float actual, float expected) => PartAccuracy(actual, expected) > 0.92f;

        public static Verdict Evaluate(Recipe r, GlassType glass, Contents c, int ice, List<Garnish> garnishes, float muddle)
        {
            var v = new Verdict { Recipe = r };
            float spirit = c.Spirit;

            // ingredients (50)
            float wsum = 0, acc = 0;
            foreach (var p in r.Parts)
            {
                float w = Mathf.Max(p.ml, 8f);
                float a = PartAccuracy(c.Get(p.ing), p.ml);
                acc += a * w;
                wsum += w;
                float got = c.Get(p.ing);
                if (a < 0.6f)
                {
                    if (got < p.ml) v.Notes.Add(Loc.S($"Not enough {p.ing.NameEn.ToLower()} ({got:0} of {p.ml:0} ml)", $"Málo: {p.ing.NameSk.ToLower()} ({got:0} z {p.ml:0} ml)"));
                    else v.Notes.Add(Loc.S($"Too much {p.ing.NameEn.ToLower()} ({got:0} of {p.ml:0} ml)", $"Priveľa: {p.ing.NameSk.ToLower()} ({got:0} z {p.ml:0} ml)"));
                }
            }
            float extra = 0;
            foreach (var kv in c.Ml)
            {
                if (kv.Key.Kind == Kind.Water) continue;
                bool inRecipe = false;
                foreach (var p in r.Parts) if (p.ing == kv.Key) { inRecipe = true; break; }
                if (!inRecipe) extra += kv.Value;
            }
            float ingAcc = wsum > 0 ? acc / wsum : 0;
            if (extra > 1f)
            {
                ingAcc -= Mathf.Clamp01(extra / Mathf.Max(spirit, 1f)) * 1.6f;
                v.Notes.Add(Loc.S("Contains something that doesn't belong", "Obsahuje niečo, čo tam nepatrí"));
            }
            if (spirit < 1f) { ingAcc = 0; v.Notes.Clear(); v.Notes.Add(Loc.S("The glass is empty!", "Pohár je prázdny!")); }
            v.IngredientScore = 50f * Mathf.Clamp01(ingAcc);

            // glass (15)
            v.GlassScore = glass == r.Glass ? 15f : 0f;
            if (glass != r.Glass) v.Notes.Add(Loc.S("Wrong glass", "Nesprávny pohár"));

            // method (15)
            float m = 0;
            switch (r.Method)
            {
                case Method.Shake:
                    m = Mathf.Clamp01(c.Shake / 0.85f) * 0.6f + Mathf.Clamp01(c.Chill / 0.8f) * 0.4f;
                    if (c.Shake < 0.5f) v.Notes.Add(Loc.S("Should have been shaken", "Malo byť pretrepané"));
                    else if (c.Chill < 0.5f) v.Notes.Add(Loc.S("Shake it with ice", "Treba trepať s ľadom"));
                    if (c.Shake > 2.6f) { m -= 0.3f; v.Notes.Add(Loc.S("Over-shaken and watery", "Pretrepané a vodnaté")); }
                    break;
                case Method.Stir:
                    m = Mathf.Clamp01(c.Stir / 0.85f) * 0.6f + Mathf.Clamp01(c.Chill / 0.8f) * 0.4f;
                    if (c.Shake > 0.3f) { m -= 0.5f; v.Notes.Add(Loc.S("Stirred drinks should not be shaken", "Miešané drinky sa netrasú")); }
                    else if (c.Stir < 0.5f) v.Notes.Add(Loc.S("Should have been stirred", "Malo byť miešané"));
                    break;
                case Method.Muddle:
                    m = Mathf.Clamp01(muddle);
                    if (muddle < 0.5f) v.Notes.Add(Loc.S("Muddle the mint first", "Najprv rozdrv mätu"));
                    if (c.Shake > 0.3f) m -= 0.3f;
                    break;
                default:
                    m = 1f;
                    if (c.Shake > 0.3f) { m = 0.4f; v.Notes.Add(Loc.S("Never shake a fizzy drink", "Perlivý drink sa netrasie")); }
                    break;
            }
            if (r.Strain && c.Unstrained) { m -= 0.45f; v.Notes.Add(Loc.S("Use a strainer when pouring", "Pri nalievaní použi sitko")); }
            v.MethodScore = 15f * Mathf.Clamp01(m);

            // ice (10)
            if (r.Ice)
            {
                v.IceScore = ice >= 3 ? 10f : ice > 0 ? 5f : 0f;
                if (ice < 3) v.Notes.Add(Loc.S("Needs more ice in the glass", "V pohári chýba ľad"));
            }
            else
            {
                v.IceScore = ice == 0 ? 10f : 0f;
                if (ice > 0) v.Notes.Add(Loc.S("Served up: no ice in the glass", "Podáva sa bez ľadu v pohári"));
            }

            // garnish (10)
            int wrong = 0;
            bool has = false;
            foreach (var g in garnishes)
            {
                if (r.Garnish.HasValue && g == r.Garnish.Value) has = true;
                else wrong++;
            }
            if (r.Garnish.HasValue)
            {
                v.GarnishScore = (has ? 10f : 0f) - wrong * 3f;
                if (!has) v.Notes.Add(Loc.S($"Missing garnish: {GarnishName(r.Garnish.Value).ToLower()}", $"Chýba ozdoba: {GarnishName(r.Garnish.Value).ToLower()}"));
            }
            else v.GarnishScore = 10f - wrong * 4f;
            v.GarnishScore = Mathf.Max(0, v.GarnishScore);

            v.Score = Mathf.Clamp(v.IngredientScore + v.GlassScore + v.MethodScore + v.IceScore + v.GarnishScore, 0, 100);
            if (spirit < 1f) v.Score = 0;
            v.Stars = v.Score >= 90 ? 5 : v.Score >= 76 ? 4 : v.Score >= 60 ? 3 : v.Score >= 40 ? 2 : 1;
            return v;
        }

        /// <summary>Finds the recipe the drink most resembles (free mode tasting).</summary>
        public static Verdict BestMatch(GlassType glass, Contents c, int ice, List<Garnish> garnishes, float muddle)
        {
            Verdict best = null;
            foreach (var r in All)
            {
                var v = Evaluate(r, glass, c, ice, garnishes, muddle);
                // pick by ingredients first so the name reflects what was poured
                float key = v.IngredientScore * 2f + v.Score;
                float bestKey = best == null ? -1 : best.IngredientScore * 2f + best.Score;
                if (key > bestKey) best = v;
            }
            return best;
        }

        public static string IngredientList(Recipe r)
        {
            var sb = new StringBuilder();
            foreach (var p in r.Parts)
                sb.Append(p.ing.Kind == Kind.Bitters ? $"{p.ml:0} dash  " : $"{p.ml:0} ml  ").Append(p.ing.Name).Append('\n');
            return sb.ToString();
        }
    }
}
