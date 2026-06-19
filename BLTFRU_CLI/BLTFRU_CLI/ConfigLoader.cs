using System;
using System.IO;

namespace BLTFRU_CLI
{
    internal static class ConfigLoader
    {
        public static BltFruConfig Load(string iniPath)
        {
            string[] lines = File.ReadAllLines(iniPath);
            return new BltFruConfig
            {
                ManufacturerId = ReadValue(lines, "Manufacturer_ID"),
                AssemblyNo     = ReadValue(lines, "Assembly_No"),
                BoardId        = ReadValue(lines, "Board_ID"),
                HwRevision     = ReadValue(lines, "HW_Revision"),
                FwRevision1    = ReadValue(lines, "FW_Revision_1"),
                FwRevision2    = ReadValue(lines, "FW_Revision_2"),
                FwRevision3    = ReadValue(lines, "FW_Revision_3"),
                FwRevision4    = ReadValue(lines, "FW_Revision_4"),
                FwRevision5    = ReadValue(lines, "FW_Revision_5"),
                FwRevision6    = ReadValue(lines, "FW_Revision_6"),
                CycleCounter   = ReadValue(lines, "Cycle_Counter"),
                GlobalCounter  = ReadValue(lines, "Global_Counter"),
                DeviceAddress  = Convert.ToByte(ReadValue(lines, "Device_Address"),  16),
                AddressingMode = Convert.ToByte(ReadValue(lines, "Addressing_Mode"), 10),
                PageSize       = Convert.ToByte(ReadValue(lines, "Page_Size"),       10),
            };
        }

        // Mirrors original Form1.ReadConfigFile: case-insensitive Contains match on param name,
        // then returns everything after the first '='.
        private static string ReadValue(string[] lines, string param)
        {
            string key = param.Trim().ToUpper();
            foreach (string line in lines)
            {
                if (line.Trim().ToUpper().Contains(key))
                {
                    int eq = line.IndexOf('=');
                    if (eq >= 0 && eq + 1 < line.Length)
                        return line.Substring(eq + 1);
                }
            }
            return "0";
        }
    }
}
