using System;
using System.Linq;
using TotalPhase;

namespace BLTFRU_CLI
{
    /// <summary>
    /// Wraps the Total Phase Aardvark I2C adapter for EEPROM programming.
    /// Ports _initI2c(), _writeMemory(), and _readMemory() from Form1.cs.
    /// Always calls aa_close() on Dispose when the handle was successfully opened.
    /// </summary>
    internal sealed class AardvarkEepromProgrammer : IDisposable
    {
        private const int DefaultPort       = 0;
        private const int DefaultBitRate    = 100;
        private const int DefaultBusTimeout = 150;

        private readonly BltFruConfig _config;
        private int  _handle  = -1;
        private bool _opened;

        public AardvarkEepromProgrammer(BltFruConfig config)
        {
            _config = config;
        }

        /// <summary>
        /// Opens the Aardvark device and configures it for I2C.
        /// Throws <see cref="Exception"/> if the device cannot be opened.
        /// </summary>
        public void Open()
        {
            try
            {
                _handle = AardvarkApi.aa_open(DefaultPort);
                if (_handle <= 0)
                {
                    throw new Exception(
                        "Unable to open Aardvark device on port " + DefaultPort +
                        "\nerror: " + AardvarkApi.aa_status_string(_handle));
                }

                AardvarkApi.aa_configure(_handle, AardvarkConfig.AA_CONFIG_SPI_I2C);
                AardvarkApi.aa_i2c_pullup(_handle, AardvarkApi.AA_I2C_PULLUP_BOTH);
                AardvarkApi.aa_target_power(_handle, AardvarkApi.AA_TARGET_POWER_BOTH);
                AardvarkApi.aa_i2c_bitrate(_handle, DefaultBitRate);
                AardvarkApi.aa_i2c_bus_timeout(_handle, DefaultBusTimeout);
                _opened = true;
            }
#pragma warning disable 0168
            catch (Exception ex) when (!_opened)
#pragma warning restore 0168
            {
                throw new Exception("Unable to open Aardvark device!", ex);
            }
        }

        /// <summary>
        /// Writes <paramref name="data"/> to EEPROM using paged I2C writes.
        /// Ports _writeMemory() from Form1.cs (minus ProgressBar updates).
        /// </summary>
        public void WriteMemory(byte[] data)
        {
            byte device = ResolvedDevice();
            byte addr   = 0;
            byte mode   = _config.AddressingMode;
            short page  = _config.PageSize;
            short length = (short)data.Length;
            int size     = page + mode;

            byte[] dataOut = Enumerable.Repeat((byte)0x00, size).ToArray();

            for (int p = 0; p < length; p += page)
            {
                for (int i = 0; i < mode; i++)
                    dataOut[mode - i - 1] = (byte)(((addr + p) >> (8 * i)) & 0xff);

                for (int i = 0; i < page; i++)
                {
                    dataOut[i + mode] = data[i + p];
                    if (((i + 1) & 0x07) == 0) Console.Write(" ");
                }

                int written = AardvarkApi.aa_i2c_write(_handle, device, AardvarkI2cFlags.AA_I2C_NO_FLAGS, (ushort)size, dataOut);
                if (written < 0)
                    throw new Exception(string.Format(
                        "I2C write error at page offset 0x{0:X2}: {1}",
                        p, AardvarkApi.aa_status_string(written)));
                if (written == 0)
                    throw new Exception(string.Format(
                        "I2C write error at page offset 0x{0:X2}: no bytes written — NACK or device not responding.", p));
                if (written != size)
                    throw new Exception(string.Format(
                        "I2C write error at page offset 0x{0:X2}: wrote {1} byte(s), expected {2}.",
                        p, written, size));
                AardvarkApi.aa_sleep_ms(10);
            }
        }

        /// <summary>
        /// Reads <paramref name="length"/> bytes from EEPROM and returns them.
        /// Ports _readMemory() from Form1.cs.
        /// Throws <see cref="Exception"/> on I2C read errors.
        /// </summary>
        public byte[] ReadMemory(short length)
        {
            return ReadMemory(length, true);
        }

        public byte[] ReadMemory(short length, bool printHex)
        {
            byte device = ResolvedDevice();
            byte addr   = 0;
            byte mode   = _config.AddressingMode;

            byte[] dataOut = Enumerable.Repeat((byte)0x00, mode).ToArray();
            byte[] dataIn  = Enumerable.Repeat((byte)0x00, length).ToArray();

            for (int ij = 0; ij < mode; ij++)
                dataOut[mode - ij - 1] = (byte)((addr >> (8 * ij)) & 0xff);

            int addressed = AardvarkApi.aa_i2c_write(_handle, device, AardvarkI2cFlags.AA_I2C_NO_STOP, (ushort)mode, dataOut);
            if (addressed < 0)
                throw new Exception("I2C read address error: " + AardvarkApi.aa_status_string(addressed));
            if (addressed == 0)
                throw new Exception("I2C read address error: no address bytes written — check device address.");
            if (addressed != mode)
                throw new Exception(string.Format(
                    "I2C read address error: wrote {0} address byte(s), expected {1}.",
                    addressed, mode));

            int count = AardvarkApi.aa_i2c_read(_handle, device, AardvarkI2cFlags.AA_I2C_NO_FLAGS, (ushort)length, dataIn);

            if (count < 0)
                throw new Exception("I2C read error: " + AardvarkApi.aa_status_string(count));
            if (count == 0)
                throw new Exception("I2C read error: no bytes read — check device address.");
            if (count != length)
                Console.Error.WriteLine("warning: read {0} bytes (expected {1})", count, length);

            byte[] result = Enumerable.Repeat((byte)0xFF, length).ToArray();
            if (printHex)
                Console.Write("\nread:");

            for (int i = 0; i < count; i++)
            {
                result[i] = dataIn[i];
                if (printHex)
                {
                    if ((i & 0x0f) == 0) Console.Write("\n{0:X4}:  ", addr + i);
                    Console.Write("{0:X2} ", dataIn[i]);
                    if (((i + 1) & 0x07) == 0) Console.Write(" ");
                }
            }
            if (printHex)
                Console.WriteLine();

            return result;
        }

        public void Dispose()
        {
            if (_opened && _handle > 0)
            {
                AardvarkApi.aa_close(_handle);
                _opened = false;
            }
        }

        // Mirrors the shift in button1_Click: if address includes R/W bit, strip it.
        private byte ResolvedDevice()
        {
            byte device = _config.DeviceAddress;
            if (device > 0x7F) device = (byte)(device >> 1);
            return device;
        }
    }
}
