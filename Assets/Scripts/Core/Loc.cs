namespace Bartender
{
    /// <summary>Two languages: English and Slovak. Text is written inline as S("english", "slovensky").</summary>
    public static class Loc
    {
        public static bool En = true;

        public static string S(string en, string sk) => En ? en : sk;
    }
}
