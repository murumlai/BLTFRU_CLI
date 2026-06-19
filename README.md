# BLTFRU_CLI

Standalone command-line utility for programming and verifying BLT FRU EEPROM data through a Total Phase Aardvark adapter.

## Overview

`BLTFRU_CLI` reproduces the EEPROM write/read/verify flow from the original BLTFRU WinForms application as a .NET Framework 4.8 console application.

The tool:

- Loads BLT FRU defaults from `BLTFRU.ini`
- Reads a serial number from an input scan file
- Builds the 128-byte BLT FRU EEPROM image
- Appends CRC values per 16-byte block
- Programs the EEPROM through the Aardvark I2C adapter
- Reads the EEPROM back
- Verifies the readback byte-for-byte
- Prints `PASS` or `FAIL`
- Returns a process exit code suitable for automation

## Requirements

- Windows
- .NET Framework 4.8
- Total Phase Aardvark adapter
- Total Phase Aardvark driver/runtime dependencies
- `aardvark_net.dll`
- `aardvark.dll`

The project is configured for `x86` because the Aardvark dependencies are expected to be 32-bit compatible.

## Build

Open `BLTFRU_CLI.slnx` in Visual Studio and build the solution.

Expected Debug output includes:

- `BLTFRU_CLI.exe`
- `BLTFRU.ini`
- `aardvark.dll`
- `aardvark_net.dll`

## Usage

```powershell
BLTFRU_CLI.exe --input C:\STHI\01.scan
BLTFRU_CLI.exe --input C:\path\input.txt --config C:\path\BLTFRU.ini
BLTFRU_CLI.exe --read-only --input C:\path\input.txt
BLTFRU_CLI.exe --check-aardvark
BLTFRU_CLI.exe --help
```

## Options

| Option | Description |
| --- | --- |
| `--input <path>` | Scan/input file containing the serial number. Defaults to `C:\STHI\01.scan`. |
| `--config <path>` | INI configuration file. Defaults to `BLTFRU.ini` next to the executable. |
| `--check-aardvark` | Open/configure the Aardvark adapter to verify connectivity, then exit without programming. |
| `--read-only` | Build and display the EEPROM image without programming hardware. |
| `--dump-image <path>` | Write the generated 128-byte EEPROM image to a binary file. |
| `--help` | Show command-line help. |

## Input file format

The input parser supports a tolerant key/value format:

```ini
Serial_No=IWEJ02800058
```

If `Serial_No` is not found, the parser falls back to the first meaningful non-empty line.

## Configuration

`BLTFRU.ini` contains EEPROM defaults such as manufacturer ID, assembly number, board ID, firmware revisions, counters, EEPROM device address, addressing mode, and page size.

By default the executable loads `BLTFRU.ini` from the application directory so deployment is independent of the original GUI project folder.

## Exit codes

| Code | Meaning |
| --- | --- |
| `0` | PASS |
| `1` | Input file not found |
| `2` | Config, parse, or validation failure |
| `3` | Aardvark open failure |
| `4` | I2C write or read failure |
| `5` | Verify mismatch / FAIL |

## Notes

- The EEPROM image size is 128 bytes.
- The implementation preserves the original BLT FRU layout and CRC behavior.
- Hardware programming requires an attached and accessible Aardvark adapter.
