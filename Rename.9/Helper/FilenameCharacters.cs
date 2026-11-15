namespace Episode_Names.Helper
{
    /// <summary>
    /// Windows reserves the ASCII ':' in file names. U+2236 (ratio) is a
    /// narrower, readable replacement instead of fullwidth U+FF1A.
    /// </summary>
    internal static class FilenameCharacters
    {
        internal const string Colon = "\u2236";

        internal static string ReplaceColon(string text)
        {
            return (text ?? "").Replace(" : ", " " + Colon + " ")
                .Replace(":", Colon);
        }
    }
}
