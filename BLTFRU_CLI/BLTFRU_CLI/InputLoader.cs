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
        /// Reads the scan file and extracts the serial number from SERIALNUMBER=<value>.
        /// </summary>
        public static BltFruInput Load(string inputPath)
        {
            string[] lines = File.ReadAllLines(inputPath);
            string serial = null;

            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                int eq = trimmed.IndexOf('=');
                if (eq > 0)
                {
                    string key = trimmed.Substring(0, eq).Trim();
                    if (string.Equals(key, "SERIALNUMBER", System.StringComparison.OrdinalIgnoreCase))
                    {
                        serial = trimmed.Substring(eq + 1).Trim();
                        break;
                    }
                }
            }

            return new BltFruInput { SerialNumber = serial ?? string.Empty };
        }
    }
}
