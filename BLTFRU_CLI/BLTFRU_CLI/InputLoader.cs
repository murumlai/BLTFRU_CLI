using System.IO;

namespace BLTFRU_CLI
{
    internal static class InputLoader
    {
        public const string FallbackPath = @"C:\STHI\01.scan";

        /// <summary>
        /// Returns explicitPath if provided, otherwise the default fallback path.
        /// </summary>
        public static string ResolvePath(string explicitPath)
        {
            return string.IsNullOrEmpty(explicitPath) ? FallbackPath : explicitPath;
        }

        /// <summary>
        /// Reads the scan file and extracts the serial number.
        /// Strategy:
        ///   1. Look for a line whose key (before '=') matches "Serial_No" case-insensitively.
        ///   2. If not found, fall back to the value portion of the first key=value line, or the
        ///      first non-empty, non-comment, non-section-header line verbatim.
        /// </summary>
        public static BltFruInput Load(string inputPath)
        {
            string[] lines = File.ReadAllLines(inputPath);
            string serial = null;

            // Pass 1: look for Serial_No=<value>
            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                int eq = trimmed.IndexOf('=');
                if (eq > 0)
                {
                    string key = trimmed.Substring(0, eq).Trim();
                    if (string.Equals(key, "Serial_No", System.StringComparison.OrdinalIgnoreCase))
                    {
                        serial = trimmed.Substring(eq + 1).Trim();
                        break;
                    }
                }
            }

            // Pass 2: first meaningful line (value of any key=value, or bare token)
            if (string.IsNullOrEmpty(serial))
            {
                foreach (string line in lines)
                {
                    string trimmed = line.Trim();
                    if (trimmed.Length == 0 || trimmed[0] == '[' || trimmed[0] == '#' || trimmed[0] == ';')
                        continue;

                    int eq = trimmed.IndexOf('=');
                    if (eq >= 0)
                        serial = eq + 1 < trimmed.Length ? trimmed.Substring(eq + 1).Trim() : string.Empty;
                    else
                        serial = trimmed;
                    break;
                }
            }

            return new BltFruInput { SerialNumber = serial ?? string.Empty };
        }
    }
}
