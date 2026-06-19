using System;
using System.Linq;

namespace BLTFRU_CLI
{
    /// <summary>
    /// Builds the 128-byte BLT FRU EEPROM image, faithfully porting BltInit() and its
    /// helpers from Form1.cs. No GUI state or console side effects; returns a byte array.
    /// </summary>
    internal class BltFruImageBuilder
    {
        public const int BltSize       = 0x80; // 128 bytes
        public const int BltRfdbOffset = 0x0D; // 13
        public const int BltCrcOffset  = 0x0E; // 14

        private enum BltOffset : byte
        {
            MANID  = 0x00,
            ASM    = 0x10,
            SN     = 0x20,
            MFDATE = 0x30,
            INDATE = 0x37,
            CYCCNT = 0x40,
            GBLCNT = 0x47,
            FW1    = 0x50,
            FW2    = 0x54,
            FW3    = 0x58,
            BID    = 0x60,
            HW     = 0x69,
            FW4    = 0x70,
            FW5    = 0x74,
            FW6    = 0x78,
        }

        private readonly BltFruConfig _config;
        private readonly BltFruInput  _input;
        private readonly CrcEeprom    _crc = new CrcEeprom();
        private byte[] _image;

        public BltFruImageBuilder(BltFruConfig config, BltFruInput input)
        {
            _config = config;
            _input  = input;
        }

        /// <summary>
        /// Constructs and returns the 128-byte EEPROM image.
        /// Throws <see cref="InvalidOperationException"/> if the serial number is invalid.
        /// </summary>
        public byte[] Build()
        {
            _image = Enumerable.Repeat((byte)0xFF, BltSize).ToArray();

            string manid = _config.ManufacturerId;
            string asmno = _config.AssemblyNo;
            string snumb = _input.SerialNumber;
            string brdid = _config.BoardId;
            string hwrev = _config.HwRevision;
            string fwar1 = _config.FwRevision1;
            string fwar2 = _config.FwRevision2;
            string fwar3 = _config.FwRevision3;
            string fwar4 = _config.FwRevision4;
            string fwar5 = _config.FwRevision5;
            string fwar6 = _config.FwRevision6;
            string cycnt = _config.CycleCounter;
            string gycnt = _config.GlobalCounter;
            string mdate = GetDate();
            string idate = GetDate();

            PutStringToCharArray(manid, (int)BltOffset.MANID);

            // Strip to the last letter before the first non-letter character (ported verbatim).
            for (int i = 0; i < asmno.Length; i++)
            {
                if (!asmno.Substring(i, 1).All(char.IsLetter))
                {
                    asmno = asmno.Substring(i - 1);
                    break;
                }
            }
            PutStringToCharArray(asmno, (int)BltOffset.ASM);

            if (snumb.Length > 13)
                throw new InvalidOperationException("Serial number exceeds 13 characters.");
            PutStringToCharArray(snumb, (int)BltOffset.SN);

            PutStringToCharArray(mdate, (int)BltOffset.MFDATE);
            PutStringToCharArray(idate, (int)BltOffset.MFDATE + 7);

            StringToHexArray(cycnt, (int)BltOffset.CYCCNT);
            StringToHexArray(gycnt, (int)BltOffset.GBLCNT);

            FirmwareVersion(fwar1, (int)BltOffset.FW1);
            FirmwareVersion(fwar2, (int)BltOffset.FW1 + 4);
            FirmwareVersion(fwar3, (int)BltOffset.FW1 + 8);

            PutStringToCharArray(brdid, (int)BltOffset.BID);
            HardwareRev(hwrev, (int)BltOffset.BID + 0xd);

            PutStringToCharArray(fwar4, (int)BltOffset.FW4);
            PutStringToCharArray(fwar5, (int)BltOffset.FW4 + 4);
            PutStringToCharArray(fwar6, (int)BltOffset.FW4 + 8);

            _image[(byte)(BltOffset.MANID  + BltRfdbOffset)] = 0x01;
            _image[(byte)(BltOffset.ASM    + BltRfdbOffset)] = 0x01;
            _image[(byte)(BltOffset.SN     + BltRfdbOffset)] = 0x01;
            _image[(byte)(BltOffset.MFDATE + BltRfdbOffset)] = 0x31;
            _image[(byte)(BltOffset.CYCCNT + BltRfdbOffset)] = 0x10;
            _image[(byte)(BltOffset.FW1    + BltRfdbOffset)] = 0x01;
            _image[(byte)(BltOffset.BID    + BltRfdbOffset)] = 0x01;
            _image[(byte)(BltOffset.FW4    + BltRfdbOffset)] = 0x01;

            for (int i = 0; i < BltSize; i += 0x10)
                CrcAppendChecksum((ulong)(i + BltCrcOffset));

            return _image;
        }

        // Uses DateTime.UtcNow instead of GetSystemTime() – same format YYMMDD.
        private static string GetDate()
        {
            DateTime now = DateTime.UtcNow;
            string yy = now.Year.ToString().Substring(2, 2);
            string mm = now.Month.ToString().PadLeft(2, '0');
            string dd = now.Day.ToString().PadLeft(2, '0');
            return yy + mm + dd;
        }

        private void PutStringToCharArray(string str, int off)
        {
            char[] tmp = str.ToCharArray();
            for (int i = 0; i < str.Length; i++)
            {
                byte saved = _image[i + off];
                try   { _image[i + off] = (byte)tmp[i]; }
#pragma warning disable 0168
                catch (Exception) { _image[i + off] = saved; }
#pragma warning restore 0168
            }
        }

        private void StringToHexArray(string num, int off)
        {
            const int len = 3;
            string hex = string.Format("{0:X6}", Convert.ToUInt32(num));
            for (int i = 0; i < len; i++)
            {
                try   { _image[i + off] = Convert.ToByte(hex.Substring(i * 2, 2), 16); }
#pragma warning disable 0168
                catch (Exception) { _image[i + off] = 0x00; }
#pragma warning restore 0168
            }
        }

        private void FirmwareVersion(string str, int off)
        {
            char[] tmp;
            if (str.Contains('.'))
            {
                string[] s0 = str.Split('.');
                string s1 = "00" + s0[0];
                string s2 = "00" + s0[1];
                string s3 = s1.Substring(s1.Length - 2) + s2.Substring(s2.Length - 2);
                tmp = s3.ToCharArray();
            }
            else
            {
                tmp = str.ToCharArray();
            }

            for (int i = 0; i < str.Length; i++)
            {
                byte saved = _image[i + off];
                try   { _image[i + off] = (byte)tmp[i]; }
#pragma warning disable 0168
                catch (Exception) { _image[i + off] = saved; }
#pragma warning restore 0168
            }
        }

        private void HardwareRev(string str, int off)
        {
            char[] tmp = str.ToCharArray();
            for (int i = 0; i < str.Length; i++)
            {
                byte saved = _image[i + off - str.Length];
                try   { _image[i + off - str.Length] = (byte)tmp[i]; }
#pragma warning disable 0168
                catch (Exception) { _image[i + off - str.Length] = saved; }
#pragma warning restore 0168
            }
        }

        private ushort CrcChecksum(ulong length)
        {
            byte[] data = new byte[0x0E];
            ulong j = 0;
            for (ulong i = length - 0x0E; i < length; i++)
            {
                data[j] = _image[i];
                j++;
            }
            byte[] val = _crc.CalculateCRC(data);
            ushort crc = (ushort)(((ulong)val[0]) << 8);
            crc += (ushort)val[1];
            return crc;
        }

        private void CrcAppendChecksum(ulong length)
        {
            ulong crc = CrcChecksum(length);
            _image[length + 1] = (byte)(crc & 0xff);
            _image[length]     = (byte)((crc >> 8) & 0xff);
        }
    }
}
