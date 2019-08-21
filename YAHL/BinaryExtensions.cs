using SoulsFormats;
using System;
using System.Collections.Generic;

namespace YAHL
{
    internal static class BinaryExtensions
    {
        public static uint ReadHKOffset(this BinaryReaderEx br)
        {
            br.BigEndian = true;
            uint v = br.ReadUInt32() & 0x3FFFFFFF;
            br.BigEndian = false;
            return v;
        }

        public static void WriteHKOffset(this BinaryWriterEx bw, uint offset, bool orX40 = true)
        {
            bw.BigEndian = true;
            if (orX40)
                bw.WriteUInt32(offset | 0x40000000);
            else
                bw.WriteUInt32(offset);
            bw.BigEndian = false;
        }

        public static void FillHKOffset(this BinaryWriterEx bw, string name, uint offset, bool orX40 = true)
        {
            bw.BigEndian = true;
            if (orX40)
                bw.FillUInt32(name, offset | 0x40000000);
            else
                bw.FillUInt32(name, offset);
            bw.BigEndian = false;
        }

        public static void WriteHKStringList(this BinaryWriterEx bw, IEnumerable<string> strings)
        {
            foreach (var s in strings)
            {
                bw.WriteASCII(s);
            }
            bw.Pad(4);
        }

        public static List<string> ReadHKStringList(this BinaryReaderEx br)
        {
            List<string> result = new List<string>();
            while (br.Position < br.Length)
            {
                var next = br.ReadASCII();
                if (next.Length > 0)
                    result.Add(next);
            }
            return result;
        }

        public static uint ReadHKPackedInt(this BinaryReaderEx br)
        {
            br.BigEndian = true;

            uint result;

            byte a = br.ReadByte();
            if ((a & 0x80) > 0)
            {
                if ((a & 0x40) > 0)
                {
                    if ((a & 0x20) > 0)
                    {
                        byte b = br.ReadByte();
                        ushort c = br.ReadUInt16();
                        result = (uint)((a << 24 | b | c) & 0x7FFFFFF);
                    }
                    else
                    {
                        ushort b = br.ReadUInt16();
                        result = (uint)((a << 16 | b) & 0x1FFFFF);
                    }
                }
                else
                {
                    byte b = br.ReadByte();
                    result = (uint)((a << 8 | b) & 0x3FFF);
                }
            }
            else
            {
                result = a;
            }

            br.BigEndian = false;

            return result;
        }

        public static void WriteHKPackedInt(this BinaryWriterEx bw, uint v)
        {
            bw.BigEndian = true;

            if (v < 0x80)
            {
                bw.WriteByte((byte)v);
            }
            else if (v < 0x4000)
            {
                bw.WriteUInt16((ushort)v);
            }
            else if (v < 0x200000)
            {
                bw.WriteByte((byte)((v >> 16) | 0xC0));
                bw.WriteUInt16((ushort)(v & 0xFFFF));
            }
            else if (v < 0x8000000)
            {
                bw.WriteUInt32(v | 0xE0000000);
            }
            else
            {
                throw new InvalidOperationException("Packed int value can't be larger than 0x8000000.");
            }

            bw.BigEndian = false;
        }
    }
}