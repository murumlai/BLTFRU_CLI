# 🎯 Create Separate BLTFRU_CLI Solution on .NET Framework 4.8

Create a completely separate standalone CLI solution named `BLTFRU_CLI`, outside the existing `BLTFRU` project folder. Do not add the CLI project to the current `BLTFRU.sln`, and do not require runtime files from the existing `BLTFRU` source/project directory.

Recommended location:

```plaintext
C:\Users\lloganat\source\repos\BLTFRU_CLI\
  BLTFRU_CLI.sln
  BLTFRU_CLI\
    BLTFRU_CLI.csproj
    Program.cs
    BLTFRU.ini
    aardvark_net.dll
```

The new CLI will reproduce the existing WinForms EEPROM write/read/verify flow. It will accept an input text file, load defaults from `BLTFRU.ini`, generate the same 128-byte BLT FRU EEPROM image currently produced by `Form1.BltInit()`, program it through the Total Phase Aardvark adapter, read it back, verify it byte-for-byte, print `PASS` or `FAIL`, and return a meaningful process exit code.

Use `.NET Framework 4.8` rather than modern `.NET` because the existing `aardvark_net.dll` is a .NET Framework-era dependency and may not be compatible with .NET 6/8/9. Keep the CLI as `x86`, matching the existing Release project configuration and reducing risk with native Aardvark dependencies.

Important source references from the existing project:
- `C:\Users\lloganat\source\repos\BLTFRU\Form1.cs`: EEPROM layout constants, Aardvark initialization, EEPROM read/write logic, config parsing, FRU image construction, and verification flow.
- `C:\Users\lloganat\source\repos\BLTFRU\CRCEEPROM.cs`: CRC algorithm used by `BltInit()` through `CalculateCRC()`.
- `C:\Users\lloganat\source\repos\BLTFRU\BLTFRU.ini`: default configuration values.
- `C:\Users\lloganat\source\repos\BLTFRU\aardvark_net.dll`: Total Phase .NET wrapper dependency.

Runtime behavior requirements:
- Input file resolution:
  1. Use `--input <path>` if supplied.
  2. Otherwise use fallback file `C:\STHI\01.scan`.
  3. If the resolved input file does not exist, print an error and return a nonzero exit code.
- Config file resolution:
  1. Use `--config <path>` if supplied.
  2. Otherwise load `BLTFRU.ini` from `AppDomain.CurrentDomain.BaseDirectory`, so the deployed executable is independent from the original source project folder.
- Standalone deployment folder must include at least:
  - `BLTFRU_CLI.exe`
  - `BLTFRU.ini`
  - `aardvark_net.dll`
  - any required Total Phase native DLL or installed driver dependency.

Preserve the existing EEPROM format unless intentionally changed:
- `BLT_SIZE = 0x80`
- `BLT_RFDBOFFSET = 0x0D`
- `BLT_CRCOFFSET = 0x0E`
- Existing `BLT_OFFSET` values.
- CRC append per 16-byte block.
- Existing Aardvark defaults: port `0`, bitrate `100`, bus timeout `150`.

Suggested CLI behavior:

```powershell
BLTFRU_CLI.exe --input C:\STHI\01.scan
BLTFRU_CLI.exe --input C:\path\input.txt --config C:\path\BLTFRU.ini
BLTFRU_CLI.exe --help
```

Input file parsing should initially support tolerant `key=value` format, for example:

```ini
Serial_No=IWEJ02800058
```

If the exact `01.scan` format differs, implement parsing so it can derive the serial number from the first meaningful line or clearly report an unsupported format. Keep parser logic isolated so it is easy to adjust after seeing real scan files.

Open items to resolve during implementation:
- Confirm exact structure of `C:\STHI\01.scan`.
- Existing serial validation in `button1_Click()` treats a hex-convertible value as invalid and non-hex as valid. Preserve that behavior only if required; otherwise replace with explicit serial length and non-empty validation.
- Current GUI code uses UTC date via `GetSystemTime()`. For CLI, use `DateTime.UtcNow` to preserve behavior unless dates should come from input/config.
- Verify whether `aardvark_net.dll` requires an additional native DLL beside the EXE or an installed Total Phase driver package.

**Last Updated**: 2026-06-19 03:37:56

## 📝 Plan Steps
-  **Create new solution directory — create `C:\Users\lloganat\source\repos\BLTFRU_CLI\` separate from `C:\Users\lloganat\source\repos\BLTFRU\`.**
-  **Create new solution — create `BLTFRU_CLI.sln` inside the new directory; do not modify `BLTFRU.sln`.**
-  **Add console project — create `BLTFRU_CLI` Console Application targeting `.NET Framework 4.8`.**
-  **Configure platform — set `BLTFRU_CLI` platform target to `x86` for Debug and Release to match likely `aardvark_net.dll`/native Aardvark compatibility.**
-  **Copy Aardvark dependency — copy `aardvark_net.dll` from the existing project into the new CLI project and configure it to copy to output.**
-  **Copy default config — copy `BLTFRU.ini` from the existing project into the new CLI project and configure it to copy to output.**
-  **Add CRC logic — copy or recreate the necessary CRC logic from `CRCEEPROM.cs` into the new CLI project without referencing the original project folder at runtime.**
-  **Create model classes — add `BltFruConfig` for INI values and EEPROM/Aardvark settings, plus `BltFruInput` for input-file values such as serial number.**
-  **Create config loader — implement a CLI-safe INI/key-value loader that reads `BLTFRU.ini` from `--config` or from the executable directory.**
-  **Create input loader — implement input resolution using `--input`; if missing, fallback to `C:\STHI\01.scan`; parse relevant fields from the file.**
-  **Extract image builder — port the non-GUI logic from `BltInit()`, `PutStringToCharArray()`, `StringToHexArray()`, `FirmwareVersion()`, `HardwareRev()`, and CRC append into a `BltFruImageBuilder` class.**
-  **Replace GUI state — replace `textBox3.Text` with `BltFruInput.SerialNumber`, remove `BackColor`, `MessageBox`, and `ProgressBar` behavior, and use console output/errors instead.**
-  **Implement Aardvark programmer — port `_initI2c()`, `_writeMemory()`, and `_readMemory()` into an `AardvarkEepromProgrammer` class, with `IDisposable` cleanup that always calls `aa_close()` when opened.**
-  **Implement CLI entry point — parse arguments, load config/input, validate data, build EEPROM image, open Aardvark, program EEPROM, read back EEPROM, compare bytes, and print `PASS` or `FAIL`.**
-  **Add exit codes — return distinct nonzero codes for missing input/config, parse/validation failures, Aardvark open failure, write/read failure, and verify mismatch.**
-  **Add optional diagnostics — support `--help`, and if time permits `--read-only` and `--dump-image <path>`.**
-  **Package standalone output — ensure the build output contains `BLTFRU_CLI.exe`, `BLTFRU.ini`, `aardvark_net.dll`, and any required native Total Phase dependency.**
-  **Validate compatibility — build under `.NET Framework 4.8`, verify `aardvark_net.dll` loads successfully, run with `--help`, test missing-file errors, test default fallback to `C:\STHI\01.scan`, then test programming with Aardvark hardware attached.**
-  **Leave existing GUI untouched — do not modify the existing `BLTFRU` project or solution except for reading/copying source logic and dependency files.**

