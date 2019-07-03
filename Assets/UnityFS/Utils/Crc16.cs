using System.IO;

namespace UnityFS.Utils
{
    public class Crc16
    {
        private const ushort polynomial = 0xA001;
        private static readonly ushort[] table = new ushort[256];
        private byte[] buffer = new byte[256];

        private ushort _checksum = 0;

        public ushort value { get { return _checksum; } }

        public string hex { get { return ToString(_checksum); } }

        public Crc16()
        {
        }

        public void Clear()
        {
            _checksum = 0;
        }

        public static string ToString(ushort value)
        {
            return value.ToString("x").PadLeft(4, '0');
        }

        public ushort Update(Stream stream)
        {
            var count = buffer.Length;
            var read = stream.Read(buffer, 0, count);
            while (read > 0)
            {
                _checksum = ComputeChecksum(buffer, 0, read, _checksum);
                read = stream.Read(buffer, 0, count);
            }
            return _checksum;
        }

        public ushort Update(byte[] bytes)
        {
            return _checksum = ComputeChecksum(bytes, 0, bytes.Length, _checksum);
        }

        public ushort Update(byte[] bytes, int offset, int count)
        {
            return _checksum = ComputeChecksum(bytes, offset, count, _checksum);
        }

        public static ushort ComputeChecksum(byte[] bytes)
        {
            return ComputeChecksum(bytes, 0, bytes.Length, 0);
        }

        public static ushort ComputeChecksum(byte[] bytes, int offset, int count, ushort checksum)
        {
            for (int i = 0, size = count; i < size; ++i)
            {
                byte index = (byte)(checksum ^ bytes[i + offset]);
                checksum = (ushort)((checksum >> 8) ^ table[index]);
            }
            return checksum;
        }

        static Crc16()
        {
            ushort value;
            ushort temp;
            for (ushort i = 0; i < table.Length; ++i)
            {
                value = 0;
                temp = i;
                for (byte j = 0; j < 8; ++j)
                {
                    if (((value ^ temp) & 0x0001) != 0)
                    {
                        value = (ushort)((value >> 1) ^ polynomial);
                    }
                    else
                    {
                        value >>= 1;
                    }
                    temp >>= 1;
                }
                table[i] = value;
            }
        }
    }
}