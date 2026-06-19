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

                    case "--dump-image":
                        if (i + 1 < args.Length) dumpImagePath = args[++i];
                        break;
                }
            }

            // --- Resolve and validate input file ---
            string resolvedInput = InputLoader.ResolvePath(inputPath);
            if (!File.Exists(resolvedInput))
            {
                Console.Error.WriteLine("error: input file not found: " + resolvedInput);
                return 1;
            }

            // --- Resolve and validate config file ---
            if (string.IsNullOrEmpty(configPath))
                configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BLTFRU.ini");
            if (!File.Exists(configPath))
            {
                Console.Error.WriteLine("error: config file not found: " + configPath);
                return 2;
            }

            // --- Load config and input ---
            BltFruConfig config;
            BltFruInput  input;
            try
            {
                config = ConfigLoader.Load(configPath);
                input  = InputLoader.Load(resolvedInput);
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

            if (readOnly)
            {
                Console.WriteLine("\n--read-only: skipping programming.");
                return 0;
            }

            // --- Open Aardvark ---
            using (var programmer = new AardvarkEepromProgrammer(config))
            {
                try
                {
                    programmer.Open();
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
                Console.WriteLine("read done!");

                // Verify
                Console.Write("verifying...");
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
            Console.WriteLine("  --read-only          Build and display EEPROM image; skip programming");
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

