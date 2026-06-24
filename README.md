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
- Prints parsed BLT FRU fields from EEPROM readback
- Prints `PASS` or `FAIL`
- Returns a process exit code suitable for automation

## Requirements

- Windows
- .NET Framework 4.8
- Total Phase Aardvark adapter
- Total Phase Aardvark driver/runtime dependencies
- `aardvark_net.dll`
- `aardvark.dll`

The project supports Visual Studio `Any CPU` and `x86` solution platforms, but all build configurations set `PlatformTarget` to `x86` because the Aardvark dependencies are expected to be 32-bit compatible.

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
BLTFRU_CLI.exe --read-only
BLTFRU_CLI.exe --read-only --dump-image C:\path\eeprom_dump.bin
BLTFRU_CLI.exe --input C:\path\input.txt --dump-image C:\path\image.bin
BLTFRU_CLI.exe --check-aardvark
BLTFRU_CLI.exe --help
```

## Options

| Option | Description |
| --- | --- |
| `--input <path>` | Scan/input file containing the serial number. Defaults to `C:\STHI\01.scan`. |
| `--config <path>` | INI configuration file. Defaults to `BLTFRU.ini` next to the executable. |
| `--check-aardvark` | Open/configure the Aardvark adapter to verify connectivity, then exit without programming. |
| `--read-only` | Read the connected EEPROM and display parsed BLT FRU fields without programming hardware. |
| `--dump-image <path>` | Write the generated 128-byte EEPROM image to a binary file, or in `--read-only` mode write the raw EEPROM bytes read from the device. |
| `--help` | Show command-line help. |

## Read-only output

`--read-only` opens the Aardvark adapter, reads the current 128-byte EEPROM contents, and prints the BLT FRU fields one item per line:

```text
BLT FRU content:
Manufacturer ID = INTEL
Assembly Number = AAN53463-100
Serial Number = RMAI53460715
Manufacture Date = 260124
Install Date = 260124
Cycle Counter = 1
Global Counter = 1
Firmware Revision 1 = 0000
Firmware Revision 2 = 0000
Firmware Revision 3 = 0000
Board ID = HOK
Hardware Revision = A100
Firmware Revision 4 = ####
Firmware Revision 5 = ####
Firmware Revision 6 = ####
```

The write/verify flow also prints these parsed fields from the EEPROM readback during verification.

## Input file format

The input parser supports a tolerant key/value format:

```ini
Serial_No=IWEJ02800058
```

If `Serial_No` is not found, the parser falls back to the first meaningful non-empty line.

## Configuration

`BLTFRU.ini` uses the `[BLT]` section and contains EEPROM defaults such as manufacturer ID, assembly number, board ID, hardware revision, firmware/custom fields, counters, EEPROM device address, addressing mode, and page size.

Current configuration keys include:

| Key | Purpose |
| --- | --- |
| `Manufacturer_ID` | Manufacturer text written at EEPROM offset `0x00`. |
| `Assembly_No` | Assembly number written at EEPROM offset `0x10`. |
| `Serial_No` | Default serial value in the INI; normal programming uses the scan/input file serial number instead. |
| `Board_ID` | Board identifier written at EEPROM offset `0x60`. |
| `HW_Revision` | Hardware revision written near EEPROM offset `0x69`. |
| `Manufc_Date` / `Install_Date` | Present for compatibility with the original config format. The CLI currently generates dates during image creation. |
| `FW_Revision_1` through `FW_Revision_5` | Firmware revision fields written into the BLT FRU layout. |
| `Custom` | Custom/legacy field present in the current INI format. |
| `Cycle_Counter` / `Global_Counter` | 3-byte counter values written into the BLT FRU layout. |
| `Device_Address` | EEPROM I2C device address, parsed as hexadecimal. The current default is `56` (`0x56`). |
| `Addressing_Mode` | Number of memory-address bytes sent before read/write operations. The current default is `2`. |
| `Page_Size` | EEPROM page transfer size. The current default is `16`. |
| `Offset`, `I2cFlag`, `Editable` | Compatibility/configuration metadata retained in the INI file. |

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
