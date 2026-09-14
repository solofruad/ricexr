using System;
using System.Text.RegularExpressions;

namespace RiceXR.Core
{
    public static class LeafSeverity
    {
        public static bool TryParseMeshName(string name, out int severity)
        {
            severity = -1;
            if (string.IsNullOrWhiteSpace(name) || name.EndsWith(".base", StringComparison.OrdinalIgnoreCase))
                return false;
            Match match = Regex.Match(name, @"^(\d+)[^\d\s]");
            return match.Success && int.TryParse(match.Groups[1].Value, out severity)
                && severity >= 0 && severity <= 9;
        }
    }
}
