using System;

namespace Episode_Names.Renaming
{
    // Stable pairing rule: sorted filenames and their first N metadata rows.
    // No extra file or row is ever included implicitly.
    internal sealed class RenameCountDecision
    {
        internal int PairCount { get; private set; }
        internal int UntouchedFiles { get; private set; }
        internal int IgnoredRows { get; private set; }
        internal bool RequiresConfirmation { get { return UntouchedFiles > 0 || IgnoredRows > 0; } }
        internal bool CanPreview { get { return PairCount > 0; } }

        internal RenameCountDecision(int files, int rows)
        {
            if (files < 0 || rows < 0) throw new ArgumentOutOfRangeException();
            PairCount = Math.Min(files, rows);
            UntouchedFiles = files - PairCount;
            IgnoredRows = rows - PairCount;
        }
    }
}
