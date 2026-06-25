using System;
using System.IO;

namespace BLTFRU_CLI
{
    internal class Program
    {
        // Exit codes
        // 0  PASS
        // 1  Input file not found
        // 2  Config / parse / validation failure
        // 3  Aardvark open failure
        // 4  I2C write or read failure
        // 5  Verify mismatch (FAIL)

        static int Main(string[] args)
        {
            string inputPath    = null;
            string configPath   = null;
            bool   readOnly     = false;
            bool   checkAardvark = false;
            string dumpImagePath = null;

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLower())
                {
                    case "--help":
                    case "-h":
                        PrintHelp();
                        return 0;

                    case "--input":
                        if (i + 1 < args.Length) inputPath = args[++i];
                        break;

                    case "--config":
                        if (i + 1 < args.Length) configPath = args[++i];
                        break;

                    case "--read-only":
                        readOnly = true;
                        break;

                    case "--check-aardvark":
                        checkAardvark = true;
                        break;

                    case "--dump-image":
                        if (i + 1 < args.Length) dumpImagePath = args[++i];
                        break;
                }
            }

            if (args.Length == 0)
            {
                PrintHelp();
                return 0;
            }

            if (checkAardvark)
                return CheckAardvarkConnectivity();

            // --- Resolve and validate config file (required for all modes) ---
            if (string.IsNullOrEmpty(configPath))
                configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BLTFRU.ini");
            if (!File.Exists(configPath))
            {
                Console.Error.WriteLine("error: config file not found: " + configPath);
                return 2;
            }

            BltFruConfig config;
            try
            {
                config = ConfigLoader.Load(configPath);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("error: " + ex.Message);
                return 2;
            }

            // --- Read-only: read and display current EEPROM content from the device ---
            if (readOnly)
                return ReadAndDisplayEeprom(config, dumpImagePath);

            // --- Resolve and validate input file ---
            string resolvedInput = InputLoader.ResolvePath(inputPath);
            if (!File.Exists(resolvedInput))
            {
                Console.Error.WriteLine("error: input file not found: " + resolvedInput);
                return 1;
            }

            BltFruInput input;
            try
            {
                input = InputLoader.Load(resolvedInput);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("error: " + ex.Message);
                return 2;
            }

            if (string.IsNullOrEmpty(input.SerialNumber))
            {
                Console.Error.WriteLine("error: serial number is empty");
                return 2;
            }
            if (input.SerialNumber.Length > 13)
            {
                Console.Error.WriteLine("error: serial number exceeds 13 characters");
                return 2;
            }

            Console.WriteLine("Serial: " + input.SerialNumber);
            Console.WriteLine("I2C device -> 0x{0:X} with {1} byte(s) addressing mode, {2} byte(s) per transfer.",
                              config.DeviceAddress, config.AddressingMode, config.PageSize);

            // --- Build EEPROM image ---
            byte[] image;
            try
            {
                var builder = new BltFruImageBuilder(config, input);
                image = builder.Build();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("error building EEPROM image: " + ex.Message);
                return 2;
            }

            // Print the image
            Console.WriteLine("\nEEPROM image to write:");
            PrintHex(image);

            // Optional raw dump
            if (!string.IsNullOrEmpty(dumpImagePath))
            {
                try
                {
                    File.WriteAllBytes(dumpImagePath, image);
                    Console.WriteLine("image written to: " + dumpImagePath);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("warning: failed to dump image: " + ex.Message);
                }
            }

            // --- Open Aardvark ---
            using (var programmer = new AardvarkEepromProgrammer(config))
            {
                try
                {
                    programmer.Open();
                    Console.WriteLine("Aardvark device opened successfully.");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("error: " + ex.Message);
                    return 3;
                }

                // Write
                Console.WriteLine("\nwriting EEPROM...");
                try
                {
                    programmer.WriteMemory(image);
                    Console.WriteLine("EEPROM write completed successfully.");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("error: write failed: " + ex.Message);
                    return 4;
                }
                Console.WriteLine("\nwrite done!");

                // Read back
                Console.WriteLine("\nreading back EEPROM...");
                byte[] readBack;
                try
                {
                    readBack = programmer.ReadMemory((short)image.Length);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("error: read failed: " + ex.Message);
                    return 4;
                }
                Console.WriteLine("EEPROM read completed successfully.");

                // Verify
                Console.Write("verifying eeprom content...");
                bool pass = true;
                for (int i = 0; i < image.Length; i++)
                {
                    if (readBack[i] != image[i])
                    {
                        Console.Error.WriteLine(
                            "\noffset: {0}  wrote:{1:X2}  read:{2:X2}",
                            i, image[i], readBack[i]);
                        pass = false;
                        break;
                    }
                }

                PrintBltContent(readBack);

                if (pass)
                {
                    Console.WriteLine("done!");
                    Console.WriteLine("PASS");
                    return 0;
                }
                else
                {
                    Console.WriteLine("FAIL");
                    return 5;
                }
            }
        }

        private static void PrintHex(byte[] data)
        {
            for (int i = 0; i < data.Length; i++)
            {
                if ((i & 0x0f) == 0) Console.Write("\n{0:X4}:  ", i);
                Console.Write("{0:X2} ", data[i]);
                if (((i + 1) & 0x07) == 0) Console.Write(" ");
            }
            Console.WriteLine();
        }

        private static int ReadAndDisplayEeprom(BltFruConfig config, string dumpPath)
        {
            Console.WriteLine("I2C device -> 0x{0:X} with {1} byte(s) addressing mode.",
                              config.DeviceAddress, config.AddressingMode);

            using (var programmer = new AardvarkEepromProgrammer(config))
            {
                try
                {
                    programmer.Open();
                    Console.WriteLine("Aardvark device opened successfully.");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("error: " + ex.Message);
                    return 3;
                }

                Console.WriteLine("\nreading EEPROM...");
                byte[] data;
                try
                {
                    data = programmer.ReadMemory((short)BltFruImageBuilder.BltSize, false);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("error: read failed: " + ex.Message);
                    return 4;
                }
                Console.WriteLine("EEPROM read completed successfully.");
                PrintBltContent(data);

                if (!string.IsNullOrEmpty(dumpPath))
                {
                    try
                    {
                        File.WriteAllBytes(dumpPath, data);
                        Console.WriteLine("image written to: " + dumpPath);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine("warning: failed to dump image: " + ex.Message);
                    }
                }

                return 0;
            }
        }

        private static void PrintBltContent(byte[] data)
        {
            Console.WriteLine();
            Console.WriteLine("BLT FRU content:");
            PrintField("Manufacturer ID", ReadText(data, 0x00, 13));
            PrintField("Assembly Number", ReadText(data, 0x10, 13));
            PrintField("Serial Number", ReadText(data, 0x20, 13));
            PrintField("Manufacture Date", ReadText(data, 0x30, 6));
            PrintField("Install Date", ReadText(data, 0x37, 6));
            PrintField("Cycle Counter", ReadCounter(data, 0x40));
            PrintField("Global Counter", ReadCounter(data, 0x47));
            PrintField("Firmware Revision 1", ReadText(data, 0x50, 4));
            PrintField("Firmware Revision 2", ReadText(data, 0x54, 4));
            PrintField("Firmware Revision 3", ReadText(data, 0x58, 4));
            PrintField("Board ID", ReadText(data, 0x60, 9));
            PrintField("Hardware Revision", ReadText(data, 0x69, 4));
            PrintField("Firmware Revision 4", ReadText(data, 0x70, 4));
            PrintField("Firmware Revision 5", ReadText(data, 0x74, 4));
            PrintField("Firmware Revision 6", ReadText(data, 0x78, 4));
        }

        private static void PrintField(string name, string value)
        {
            Console.WriteLine("{0} = {1}", name, value);
        }

        private static string ReadText(byte[] data, int offset, int length)
        {
            char[] value = new char[length];
            int count = 0;
            for (int i = 0; i < length && offset + i < data.Length; i++)
            {
                byte b = data[offset + i];
                if (b == 0x00 || b == 0xFF)
                    continue;

                value[count++] = b >= 0x20 && b <= 0x7E ? (char)b : '.';
            }

            return new string(value, 0, count).Trim();
        }

        private static string ReadCounter(byte[] data, int offset)
        {
            if (offset + 2 >= data.Length)
                return string.Empty;

            int value = (data[offset] << 16) | (data[offset + 1] << 8) | data[offset + 2];
            return value.ToString();
        }

        private static int CheckAardvarkConnectivity()
        {
            Console.WriteLine("Checking Aardvark connectivity...");
            using (var programmer = new AardvarkEepromProgrammer(new BltFruConfig()))
            {
                try
                {
                    programmer.Open();
                    Console.WriteLine("Aardvark connectivity: PASS");
                    return 0;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("Aardvark connectivity: FAIL");
                    Console.Error.WriteLine("error: " + ex.Message);
                    if (ex.InnerException != null)
                        Console.Error.WriteLine("detail: " + ex.InnerException.Message);
                    return 3;
                }
            }
        }

        private static void PrintHelp()
        {
            Console.WriteLine("BLTFRU_CLI  -  BLT FRU EEPROM programmer");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  BLTFRU_CLI.exe --input <path> [options]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --input <path>       Scan file containing Serial_No (default: C:\\STHI\\01.scan)");
            Console.WriteLine("  --config <path>      INI config file (default: BLTFRU.ini next to the exe)");
            Console.WriteLine("  --check-aardvark     Check Aardvark connectivity and exit without programming");
            Console.WriteLine("  --read-only          Read and display the current EEPROM content from the device");
            Console.WriteLine("  --dump-image <path>  Write raw binary EEPROM image to file");
            Console.WriteLine("  --help               Show this help");
            Console.WriteLine();
            Console.WriteLine("Exit codes:");
            Console.WriteLine("  0  PASS");
            Console.WriteLine("  1  Input file not found");
            Console.WriteLine("  2  Config / parse / validation failure");
            Console.WriteLine("  3  Aardvark device open failure");
            Console.WriteLine("  4  I2C write or read failure");
            Console.WriteLine("  5  Verify mismatch (FAIL)");
        }
    }
}

