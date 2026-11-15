using System;
using System.Collections.Generic;
using System.Linq;

namespace Episode_Names.Renaming
{
    /// <summary>
    /// Converts Windows/Unix/mixed editor line endings into stable episode rows.
    /// Empty lines inside the data are intentionally preserved so validation can flag them.
    /// Only empty lines at the end (typically inserted by the editor) are removed.
    /// </summary>
    internal static class MetadataRows
    {
        internal static List<string> Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            string normalized = text.TrimStart('\uFEFF').Replace("\r\n", "\n").Replace('\r', '\n');
            var rows = normalized.Split('\n').ToList();
            while (rows.Count > 0 && string.IsNullOrWhiteSpace(rows[rows.Count - 1]))
                rows.RemoveAt(rows.Count - 1);
            return rows.Count == 0 ? null : rows;
        }
    }
}
