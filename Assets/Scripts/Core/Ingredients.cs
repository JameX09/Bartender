using System.Collections.Generic;
using UnityEngine;

namespace Bartender
{
    public enum Kind { Spirit, Liqueur, Wine, Syrup, Juice, Mixer, Bitters, Water }

    public enum BottleStyle { Classic, Shoulder, Slim, Flask, Wine, Bitters, Syrup, Carafe, Soda, Square }

    public sealed class Ingredient
    {
        public int Id;
        public string Key;
        public string NameEn, NameSk;
        public string Brand;          // fictional label brand
        public Color Color;           // liquid colour
        public float Alpha;           // how opaque the liquid is (0 = clear, 1 = opaque)
        public Kind Kind;
        public bool Fizzy;
        public float PourRate;        // ml per second at full tilt
        public BottleStyle Style;
        public Color GlassTint;       // bottle glass colour
        public Color LabelColor, LabelInk;

        public string Name => Loc.En ? NameEn : NameSk;
        public bool IsSmall => Kind == Kind.Bitters;
    }

    public static class Ingredients
    {
        public static readonly List<Ingredient> All = new List<Ingredient>();
        static readonly Dictionary<string, Ingredient> byKey = new Dictionary<string, Ingredient>();
        public static Ingredient Water { get; private set; }

        static readonly Color Clear = new Color(0.93f, 0.96f, 1f);
        static readonly Color GlassClear = new Color(0.92f, 0.96f, 0.98f, 0.08f);
        static readonly Color GlassGreen = new Color(0.25f, 0.55f, 0.38f, 0.55f);
        static readonly Color GlassBrown = new Color(0.45f, 0.25f, 0.1f, 0.6f);
        static readonly Color GlassBlue = new Color(0.35f, 0.55f, 0.85f, 0.35f);
        static readonly Color GlassFrost = new Color(0.95f, 0.97f, 1f, 0.22f);

        static Ingredients()
        {
            // spirits
            Add("vodka", "Vodka", "Vodka", "NORTHERN STAR", Clear, 0.06f, Kind.Spirit, BottleStyle.Classic, GlassFrost, new Color(0.08f, 0.12f, 0.22f), new Color(0.92f, 0.86f, 0.7f));
            Add("gin", "Gin", "Gin", "ROYAL BOTANIC", Clear, 0.06f, Kind.Spirit, BottleStyle.Square, GlassBlue, new Color(0.94f, 0.92f, 0.86f), new Color(0.1f, 0.25f, 0.3f));
            Add("rum", "White Rum", "Biely rum", "ISLA BLANCA", Clear, 0.06f, Kind.Spirit, BottleStyle.Classic, GlassClear, new Color(0.96f, 0.93f, 0.85f), new Color(0.55f, 0.15f, 0.12f));
            Add("darkrum", "Dark Rum", "Tmavý rum", "CAPTAIN'S RESERVE", new Color(0.42f, 0.18f, 0.05f), 0.86f, Kind.Spirit, BottleStyle.Shoulder, GlassClear, new Color(0.12f, 0.08f, 0.05f), new Color(0.86f, 0.68f, 0.35f));
            Add("tequila", "Tequila", "Tequila", "SOL DE AGAVE", new Color(0.98f, 0.95f, 0.82f), 0.1f, Kind.Spirit, BottleStyle.Flask, GlassClear, new Color(0.9f, 0.82f, 0.62f), new Color(0.18f, 0.35f, 0.25f));
            Add("bourbon", "Bourbon", "Bourbon", "OLD HICKORY", new Color(0.72f, 0.36f, 0.08f), 0.72f, Kind.Spirit, BottleStyle.Shoulder, GlassClear, new Color(0.08f, 0.07f, 0.06f), new Color(0.85f, 0.7f, 0.42f));
            // liqueurs and wines
            Add("triplesec", "Triple Sec", "Triple Sec", "MAISON D'ORANGE", Clear, 0.05f, Kind.Liqueur, BottleStyle.Slim, GlassFrost, new Color(0.95f, 0.55f, 0.12f), new Color(0.15f, 0.08f, 0.04f));
            Add("curacao", "Blue Curaçao", "Modré Curaçao", "LAGUNA AZUL", new Color(0.02f, 0.42f, 0.95f), 0.72f, Kind.Liqueur, BottleStyle.Slim, GlassClear, new Color(0.95f, 0.95f, 0.98f), new Color(0.05f, 0.25f, 0.6f));
            Add("campari", "Bitter Aperitivo", "Bitter aperitív", "ROSSO AMARO", new Color(0.85f, 0.04f, 0.09f), 0.82f, Kind.Liqueur, BottleStyle.Classic, GlassClear, new Color(0.96f, 0.94f, 0.88f), new Color(0.7f, 0.05f, 0.08f));
            Add("aperol", "Orange Aperitivo", "Pomarančový aperitív", "TRAMONTO", new Color(1f, 0.36f, 0.04f), 0.72f, Kind.Liqueur, BottleStyle.Classic, GlassClear, new Color(0.98f, 0.92f, 0.78f), new Color(0.85f, 0.3f, 0.05f));
            Add("sweetvermouth", "Sweet Vermouth", "Sladký vermut", "TORINO ROSSO", new Color(0.42f, 0.09f, 0.05f), 0.85f, Kind.Wine, BottleStyle.Wine, GlassClear, new Color(0.55f, 0.08f, 0.1f), new Color(0.95f, 0.85f, 0.6f));
            Add("dryvermouth", "Dry Vermouth", "Suchý vermut", "BLANC SEC", new Color(0.95f, 0.93f, 0.72f), 0.14f, Kind.Wine, BottleStyle.Wine, GlassGreen, new Color(0.95f, 0.93f, 0.85f), new Color(0.2f, 0.35f, 0.2f));
            Add("coffeeliq", "Coffee Liqueur", "Kávový likér", "CAFÉ NOIR", new Color(0.13f, 0.06f, 0.02f), 0.97f, Kind.Liqueur, BottleStyle.Flask, GlassClear, new Color(0.92f, 0.85f, 0.7f), new Color(0.2f, 0.1f, 0.05f));
            Add("prosecco", "Prosecco", "Prosecco", "BOLLICINE", new Color(0.98f, 0.92f, 0.58f), 0.18f, Kind.Wine, BottleStyle.Wine, GlassGreen, new Color(0.08f, 0.1f, 0.12f), new Color(0.88f, 0.75f, 0.45f), fizzy: true, rate: 45f);
            Add("bitters", "Aromatic Bitters", "Aromatické bitters", "DR. ALDER'S", new Color(0.5f, 0.07f, 0.04f), 0.95f, Kind.Bitters, BottleStyle.Bitters, GlassBrown, new Color(0.96f, 0.93f, 0.82f), new Color(0.65f, 0.12f, 0.08f), rate: 2.2f);
            // syrups
            Add("simple", "Simple Syrup", "Cukrový sirup", "BAR SYRUP", new Color(1f, 0.99f, 0.95f), 0.12f, Kind.Syrup, BottleStyle.Syrup, GlassClear, new Color(0.97f, 0.97f, 0.97f), new Color(0.3f, 0.3f, 0.32f), rate: 20f);
            Add("grenadine", "Grenadine", "Grenadína", "BAR SYRUP", new Color(0.72f, 0.01f, 0.08f), 0.9f, Kind.Syrup, BottleStyle.Syrup, GlassClear, new Color(0.97f, 0.97f, 0.97f), new Color(0.65f, 0.05f, 0.1f), rate: 20f);
            Add("agave", "Agave Syrup", "Agávový sirup", "BAR SYRUP", new Color(0.92f, 0.68f, 0.25f), 0.55f, Kind.Syrup, BottleStyle.Syrup, GlassClear, new Color(0.97f, 0.97f, 0.97f), new Color(0.75f, 0.5f, 0.12f), rate: 18f);
            Add("coconut", "Coconut Cream", "Kokosový krém", "COCO CREMA", new Color(0.97f, 0.96f, 0.92f), 0.97f, Kind.Syrup, BottleStyle.Syrup, GlassClear, new Color(0.97f, 0.97f, 0.97f), new Color(0.45f, 0.3f, 0.2f), rate: 18f);
            // juices
            Add("lime", "Lime Juice", "Limetková šťava", "FRESH", new Color(0.78f, 0.9f, 0.5f), 0.45f, Kind.Juice, BottleStyle.Carafe, GlassClear, Color.white, Color.white, rate: 40f);
            Add("lemon", "Lemon Juice", "Citrónová šťava", "FRESH", new Color(0.97f, 0.94f, 0.55f), 0.45f, Kind.Juice, BottleStyle.Carafe, GlassClear, Color.white, Color.white, rate: 40f);
            Add("orange", "Orange Juice", "Pomarančový džús", "FRESH", new Color(1f, 0.58f, 0.06f), 0.86f, Kind.Juice, BottleStyle.Carafe, GlassClear, Color.white, Color.white, rate: 45f);
            Add("cranberry", "Cranberry Juice", "Brusnicový džús", "FRESH", new Color(0.68f, 0.02f, 0.12f), 0.8f, Kind.Juice, BottleStyle.Carafe, GlassClear, Color.white, Color.white, rate: 45f);
            Add("pineapple", "Pineapple Juice", "Ananásový džús", "FRESH", new Color(1f, 0.84f, 0.32f), 0.82f, Kind.Juice, BottleStyle.Carafe, GlassClear, Color.white, Color.white, rate: 45f);
            Add("espresso", "Espresso", "Espresso", "FRESH", new Color(0.12f, 0.06f, 0.02f), 0.98f, Kind.Juice, BottleStyle.Carafe, GlassClear, Color.white, Color.white, rate: 35f);
            // mixers
            Add("cola", "Cola", "Kola", "SKY COLA", new Color(0.2f, 0.07f, 0.02f), 0.9f, Kind.Mixer, BottleStyle.Soda, GlassClear, new Color(0.75f, 0.06f, 0.08f), Color.white, fizzy: true, rate: 55f);
            Add("tonic", "Tonic Water", "Tonik", "ALPINE TONIC", Clear, 0.05f, Kind.Mixer, BottleStyle.Soda, GlassClear, new Color(0.95f, 0.85f, 0.45f), new Color(0.12f, 0.12f, 0.15f), fizzy: true, rate: 55f);
            Add("soda", "Soda Water", "Sóda", "CLUB SODA", Clear, 0.03f, Kind.Mixer, BottleStyle.Soda, GlassClear, new Color(0.35f, 0.65f, 0.9f), Color.white, fizzy: true, rate: 55f);
            Add("gingerbeer", "Ginger Beer", "Zázvorové pivo", "FIERY GINGER", new Color(0.95f, 0.84f, 0.58f), 0.38f, Kind.Mixer, BottleStyle.Soda, GlassBrown, new Color(0.95f, 0.9f, 0.75f), new Color(0.6f, 0.3f, 0.05f), fizzy: true, rate: 55f);
            // dilution from melting ice (never poured directly)
            Water = Add("water", "Water", "Voda", "", Clear, 0f, Kind.Water, BottleStyle.Carafe, GlassClear, Color.white, Color.white);
        }

        static Ingredient Add(string key, string en, string sk, string brand, Color color, float alpha, Kind kind, BottleStyle style,
            Color glass, Color label, Color ink, bool fizzy = false, float rate = 30f)
        {
            var ing = new Ingredient
            {
                Id = All.Count, Key = key, NameEn = en, NameSk = sk, Brand = brand, Color = color, Alpha = alpha, Kind = kind,
                Style = style, GlassTint = glass, LabelColor = label, LabelInk = ink, Fizzy = fizzy, PourRate = rate,
            };
            All.Add(ing);
            byKey[key] = ing;
            return ing;
        }

        public static Ingredient Get(string key) => byKey.TryGetValue(key, out var i) ? i : null;
    }
}
