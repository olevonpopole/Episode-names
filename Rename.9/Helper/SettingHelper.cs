using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Episode_Names.Helper
{
    class SettingHelper
    {
        internal static List<string> Formates => GetFormates();
        private static List<string> formates;
        internal static readonly int MaxNumber = 100;
        internal static string Number = "n";
        internal static string Position = "pos";
        internal static string Directory = "dir";
        internal static string SeriesTitle = "series";
        internal static readonly char Separator = '%';
        internal static List<string> FormatesWithoutNumeration = new List<string>
        {
                Number,
                Position,
                Directory,
                SeriesTitle
        };

        private static List<string> GetFormates()
        {
            return formates ?? InitFormates();
        }

        private static List<string> InitFormates()
        {
            formates = new List<string>(FormatesWithoutNumeration);
            
            formates.AddRange(Enumerable.Range(1, MaxNumber -1).Reverse().Select(x => x.ToString()).ToList());
            return formates;
        }
    }
}
