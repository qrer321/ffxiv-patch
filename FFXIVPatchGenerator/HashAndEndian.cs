using System;
using System.IO;
using System.Text;

namespace FfxivKoreanPatch.FFXIVPatchGenerator
{
    internal static class SqPackHash
    {
        private static readonly uint[] Table = CreateTable();

        public static ulong GetIndexHash(string path)
        {
            path = path.Replace('\\', '/').ToLowerInvariant().Trim();
            int slash = path.LastIndexOf('/');
            if (slash < 0)
            {
                return Crc32(path);
            }

            string folder = path.Substring(0, slash);
            string file = path.Substring(slash + 1);
            return ((ulong)Crc32(folder) << 32) | Crc32(file);
        }

        public static uint Crc32(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            uint crc = 0xFFFFFFFFu;

            for (int i = 0; i < bytes.Length; i++)
            {
                crc = Table[(byte)(crc ^ bytes[i])] ^ (crc >> 8);
            }

            return crc;
        }

        private static uint[] CreateTable()
        {
            uint[] table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint value = i;
                for (int bit = 0; bit < 8; bit++)
                {
                    value = (value & 1) == 1 ? 0xEDB88320u ^ (value >> 1) : value >> 1;
                }

                table[i] = value;
            }

            return table;
        }
    }

    internal static class Endian
    {
        public static ushort ReadUInt16BE(byte[] data, int offset)
        {
            return (ushort)((data[offset] << 8) | data[offset + 1]);
        }

        public static uint ReadUInt32BE(byte[] data, int offset)
        {
            return ((uint)data[offset] << 24) |
                   ((uint)data[offset + 1] << 16) |
                   ((uint)data[offset + 2] << 8) |
                   data[offset + 3];
        }

        public static uint ReadUInt32LE(byte[] data, int offset)
        {
            return data[offset] |
                   ((uint)data[offset + 1] << 8) |
                   ((uint)data[offset + 2] << 16) |
                   ((uint)data[offset + 3] << 24);
        }

        public static ulong ReadUInt64LE(byte[] data, int offset)
        {
            uint low = ReadUInt32LE(data, offset);
            uint high = ReadUInt32LE(data, offset + 4);
            return ((ulong)high << 32) | low;
        }

        public static void WriteUInt16BE(byte[] data, int offset, ushort value)
        {
            data[offset] = (byte)(value >> 8);
            data[offset + 1] = (byte)value;
        }

        public static void WriteUInt32BE(byte[] data, int offset, uint value)
        {
            data[offset] = (byte)(value >> 24);
            data[offset + 1] = (byte)(value >> 16);
            data[offset + 2] = (byte)(value >> 8);
            data[offset + 3] = (byte)value;
        }

        public static void WriteUInt32LE(byte[] data, int offset, uint value)
        {
            data[offset] = (byte)value;
            data[offset + 1] = (byte)(value >> 8);
            data[offset + 2] = (byte)(value >> 16);
            data[offset + 3] = (byte)(value >> 24);
        }

        public static void WriteUInt16BE(Stream stream, ushort value)
        {
            stream.WriteByte((byte)(value >> 8));
            stream.WriteByte((byte)value);
        }

        public static void WriteUInt32BE(Stream stream, uint value)
        {
            stream.WriteByte((byte)(value >> 24));
            stream.WriteByte((byte)(value >> 16));
            stream.WriteByte((byte)(value >> 8));
            stream.WriteByte((byte)value);
        }
    }
}
