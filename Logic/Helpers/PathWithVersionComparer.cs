using System;
using System.Collections.Generic;

namespace Logic.Helpers
{
    public class PathWithVersionComparer : IComparer<string>
    {

        private readonly System.Text.RegularExpressions.Regex PathRegex =
            new System.Text.RegularExpressions.Regex(@"(.*?\D)(\d+\.\d+(\.\d+)?(\.\d+)?)([^\d].*)$");

        private (string Prefix, Version Version, string Suffix)? TryParse(string pString)
        {
            var lMatch = PathRegex.Match(pString);
            if (!lMatch.Success)
                return default;
            string lPrefix = lMatch.Groups[1].Value;
            string lSuffix = lMatch.Groups[5].Value;
            if (!Version.TryParse(lMatch.Groups[2].Value, out Version lVersion))
                return default;
            return (lPrefix, lVersion, lSuffix);
        }

        public int Compare(string x, string y)
        {
            if (x is null & y is null)
                return 0;
            if (x is null)
                return -1;
            if (y is null)
                return 1;

            var lX = TryParse(x);
            var lY = TryParse(y);

            if (lX is null || lY is null)
                return x.CompareTo(y);

            var lPrefixCompare = lX?.Prefix.CompareTo(lY?.Prefix);
            if (lPrefixCompare.HasValue && lPrefixCompare.Value != 0)
                return lPrefixCompare.Value;

            var lVersionCompare = lX?.Version.CompareTo(lY?.Version);
            if (lVersionCompare.HasValue && lVersionCompare.Value != 0)
                return lVersionCompare.Value;

            return (lX?.Suffix.CompareTo(lY?.Suffix)).Value;
        }

    }
}
