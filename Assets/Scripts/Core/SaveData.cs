using UnityEngine;

namespace Bartender
{
    public enum Upgrade { SpeedPourers, ProShaker, LoungeDJ, PremiumMenu, TipJar, ExtraSeat }

    /// <summary>Career progress and settings, stored in PlayerPrefs.</summary>
    public static class Save
    {
        // career
        public static int Night = 1;
        public static float Money;
        public static float Reputation = 3f;   // 0..5 stars
        public static int Upgrades;            // bit mask
        public static int Served, Perfect, BestNight;
        public static float BestEarnings;
        public static bool CareerStarted;
        public static bool TutorialSeen;

        // settings
        public static float Sensitivity = 1f;
        public static bool InvertY;
        public static float Fov = 68f;
        public static float Master = 0.9f, MusicVol = 0.55f, SfxVol = 0.9f;
        public static int Quality = 2;           // 0 low, 1 medium, 2 high
        public static bool Fullscreen = true;
        public static bool VSync = true;
        public static bool English = true;
        public static bool ShowHints = true;
        public static bool PourAssist = true;   // aim lock on jiggers and stop at the brim
        public static float PourSpeed = 0.7f;   // multiplies how fast bottles pour (Shift pours at a third of this)

        public static bool Has(Upgrade u) => (Upgrades & (1 << (int)u)) != 0;
        public static void Give(Upgrade u) => Upgrades |= 1 << (int)u;

        public static void Load()
        {
            Night = PlayerPrefs.GetInt("night", 1);
            Money = PlayerPrefs.GetFloat("money", 0f);
            Reputation = PlayerPrefs.GetFloat("rep", 3f);
            Upgrades = PlayerPrefs.GetInt("upgrades", 0);
            Served = PlayerPrefs.GetInt("served", 0);
            Perfect = PlayerPrefs.GetInt("perfect", 0);
            BestNight = PlayerPrefs.GetInt("bestNight", 0);
            BestEarnings = PlayerPrefs.GetFloat("bestEarn", 0f);
            CareerStarted = PlayerPrefs.GetInt("career", 0) == 1;
            TutorialSeen = PlayerPrefs.GetInt("tutorial", 0) == 1;

            Sensitivity = PlayerPrefs.GetFloat("sens", 1f);
            InvertY = PlayerPrefs.GetInt("invY", 0) == 1;
            Fov = PlayerPrefs.GetFloat("fov", 68f);
            Master = PlayerPrefs.GetFloat("master", 0.9f);
            MusicVol = PlayerPrefs.GetFloat("music", 0.55f);
            SfxVol = PlayerPrefs.GetFloat("sfx", 0.9f);
            Quality = PlayerPrefs.GetInt("quality", 2);
            Fullscreen = PlayerPrefs.GetInt("fullscreen", 1) == 1;
            VSync = PlayerPrefs.GetInt("vsync", 1) == 1;
            English = PlayerPrefs.GetString("lang2", "en") == "en";
            ShowHints = PlayerPrefs.GetInt("hints", 1) == 1;
            PourAssist = PlayerPrefs.GetInt("assist", 1) == 1;
            PourSpeed = PlayerPrefs.GetFloat("pourSpeed", 0.7f);
            Loc.En = English;
        }

        public static void Store()
        {
            PlayerPrefs.SetInt("night", Night);
            PlayerPrefs.SetFloat("money", Money);
            PlayerPrefs.SetFloat("rep", Reputation);
            PlayerPrefs.SetInt("upgrades", Upgrades);
            PlayerPrefs.SetInt("served", Served);
            PlayerPrefs.SetInt("perfect", Perfect);
            PlayerPrefs.SetInt("bestNight", BestNight);
            PlayerPrefs.SetFloat("bestEarn", BestEarnings);
            PlayerPrefs.SetInt("career", CareerStarted ? 1 : 0);
            PlayerPrefs.SetInt("tutorial", TutorialSeen ? 1 : 0);

            PlayerPrefs.SetFloat("sens", Sensitivity);
            PlayerPrefs.SetInt("invY", InvertY ? 1 : 0);
            PlayerPrefs.SetFloat("fov", Fov);
            PlayerPrefs.SetFloat("master", Master);
            PlayerPrefs.SetFloat("music", MusicVol);
            PlayerPrefs.SetFloat("sfx", SfxVol);
            PlayerPrefs.SetInt("quality", Quality);
            PlayerPrefs.SetInt("fullscreen", Fullscreen ? 1 : 0);
            PlayerPrefs.SetInt("vsync", VSync ? 1 : 0);
            PlayerPrefs.SetString("lang2", English ? "en" : "sk");
            PlayerPrefs.SetInt("hints", ShowHints ? 1 : 0);
            PlayerPrefs.SetInt("assist", PourAssist ? 1 : 0);
            PlayerPrefs.SetFloat("pourSpeed", PourSpeed);
            PlayerPrefs.Save();
        }

        public static void NewCareer()
        {
            Night = 1; Money = 0; Reputation = 3f; Upgrades = 0; Served = 0; Perfect = 0; BestNight = 0; BestEarnings = 0;
            CareerStarted = true;
            Store();
        }

        public static int Seats => Has(Upgrade.ExtraSeat) ? 5 : 4;

        public static string UpgradeName(Upgrade u)
        {
            switch (u)
            {
                case Upgrade.SpeedPourers: return Loc.S("Speed pourers", "Rýchle nálevky");
                case Upgrade.ProShaker: return Loc.S("Pro shaker tins", "Profi šejker");
                case Upgrade.LoungeDJ: return Loc.S("Lounge DJ", "Lounge DJ");
                case Upgrade.PremiumMenu: return Loc.S("Premium menu", "Prémiové menu");
                case Upgrade.TipJar: return Loc.S("Golden tip jar", "Zlatá pokladnička");
                default: return Loc.S("Extra bar stool", "Ďalšia barová stolička");
            }
        }

        public static string UpgradeDesc(Upgrade u)
        {
            switch (u)
            {
                case Upgrade.SpeedPourers: return Loc.S("Bottles pour 35% faster.", "Fľaše nalievajú o 35 % rýchlejšie.");
                case Upgrade.ProShaker: return Loc.S("Shaking takes 40% less time.", "Trasenie trvá o 40 % kratšie.");
                case Upgrade.LoungeDJ: return Loc.S("Guests wait 30% longer.", "Hostia čakajú o 30 % dlhšie.");
                case Upgrade.PremiumMenu: return Loc.S("Drinks cost 20% more.", "Drinky stoja o 20 % viac.");
                case Upgrade.TipJar: return Loc.S("Tips are 40% bigger.", "Sprepitné je o 40 % vyššie.");
                default: return Loc.S("A fifth guest can sit at the bar.", "Pri bare môže sedieť piaty hosť.");
            }
        }

        public static float UpgradeCost(Upgrade u)
        {
            switch (u)
            {
                case Upgrade.SpeedPourers: return 120;
                case Upgrade.ProShaker: return 160;
                case Upgrade.LoungeDJ: return 220;
                case Upgrade.PremiumMenu: return 300;
                case Upgrade.TipJar: return 260;
                default: return 380;
            }
        }
    }
}
