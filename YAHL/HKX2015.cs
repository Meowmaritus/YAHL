using System;
using System.Collections.Generic;
using System.Xml;

namespace YAHL
{
    public class HKX2015
    {
        public List<HKType> Types = new List<HKType>();
        public List<HKItem> Items = new List<HKItem>();
        public List<HKPatch> Patches = new List<HKPatch>();

        public byte[] DATA;

        [Flags]
        public enum HKTagFlag : uint
        {
            SubType = 0x1,
            Pointer = 0x2,
            Version = 0x4,
            ByteSize = 0x8,
            AbstractValue = 0x10,
            Members = 0x20,
            Interfaces = 0x40,
            Unknown = 0x80,
        }

        public class HKTypeTemplate
        {
            public string Name;
            public uint Value;
            public bool IsValueATypeIndex => Name == "t";

            public override string ToString()
            {
                return $"HKTypeTemplate ['{Name}', '{Value}']";
            }
        }

        public class HKInterface
        {
            public uint TypeIndex;
            public HKType Type;
            public uint Value;

            public override string ToString()
            {
                return $"HKInterface {(Type?.ToString() ?? "<Null>")}, {Value}";
            }
        }

        public class HKMember
        {
            public string Name;
            public uint Flags;
            public uint ByteOffset;
            public uint TypeIndex;
            public HKType Type;

            public override string ToString()
            {
                return $"HKMember [{(Type?.ToString() ?? "<Null>")}, '{Name}']";
            }
        }

        public class HKPatch
        {
            public uint TypeIndex;
            public HKType Type;
            public List<uint> Offsets = new List<uint>();

            public override string ToString()
            {
                return $"HKPatch [{(Type?.ToString() ?? "<Null>")}]";
            }
        }

        public class HKItem
        {
            public uint TypeIndex;
            public HKType Type;
            public bool IsPointer;
            public uint OffsetPastDataStart;
            public uint Count;

            public override string ToString()
            {
                return $"HKItem [{(Type?.ToString() ?? "<Null>")}]";
            }
        }

        public class HKType
        {
            public string Name;
            public List<string> TypeStrings = new List<string>();
            public List<string> FieldStrings = new List<string>();
            public List<HKTypeTemplate> Templates = new List<HKTypeTemplate>();
            public uint ParentTypeIndex;
            public HKType Parent;
            public HKTagFlag Flags;

            public uint Hash;

            //Shit Flags controls:
            public uint SubTypeFlags;
            public uint PointerTypeIndex;
            public HKType Pointer;
            public uint Version;
            public uint ByteSize;
            public uint Alignment;
            public uint AbstractValue;

            public List<HKMember> Members = new List<HKMember>();
            public List<HKInterface> Interfaces = new List<HKInterface>();

            public override string ToString()
            {
                return $"HKType ['{Name}', Parent={(Parent?.ToString() ?? "<Null>")}]";
            }
        }

        public void DumpTypesToXml(string xmlFile)
        {
            XmlWriterSettings xws = new XmlWriterSettings();
            xws.Indent = true;
            XmlWriter xw = XmlWriter.Create(xmlFile, xws);
            xw.WriteStartElement("types");
            {
                for (int i = 1; i < Types.Count; i++)
                {
                    var t = Types[i];
                    xw.WriteStartElement("type");
                    {
                        xw.WriteAttributeString("alignment", $"{t.Alignment}");
                        xw.WriteAttributeString("byteSize", $"{t.ByteSize}");
                        xw.WriteAttributeString("flags", $"{(uint)t.Flags}");

                        if (t.Hash != 0)
                            xw.WriteAttributeString("hash", $"{t.Hash}");

                        xw.WriteAttributeString("id", $"{i}");
                        xw.WriteAttributeString("name", $"{t.Name}");

                        if (t.ParentTypeIndex > 0)
                            xw.WriteAttributeString("parent", $"{t.ParentTypeIndex}");

                        if ((t.Flags & HKTagFlag.Pointer) != 0)
                            xw.WriteAttributeString("pointer", $"{t.PointerTypeIndex}");

                        if ((t.Flags & HKTagFlag.SubType) != 0)
                            xw.WriteAttributeString("subTypeFlags", $"{t.SubTypeFlags}");

                        if ((t.Flags & HKTagFlag.Version) != 0)
                            xw.WriteAttributeString("version", $"{t.Version}");


                        if (t.Templates.Count > 0)
                        {
                            foreach (var temp in t.Templates)
                            {
                                xw.WriteStartElement("template");
                                {
                                    xw.WriteAttributeString("name", $"{temp.Name}");
                                    xw.WriteAttributeString("value", $"{temp.Value}");
                                }
                                xw.WriteEndElement();
                            }
                        }

                        if ((t.Flags & HKTagFlag.Members) != 0)
                        {
                            foreach (var m in t.Members)
                            {
                                xw.WriteStartElement("member");
                                {
                                    xw.WriteAttributeString("flags", $"{m.Flags}");
                                    xw.WriteAttributeString("name", $"{m.Name}");
                                    xw.WriteAttributeString("offset", $"{m.ByteOffset}");
                                    xw.WriteAttributeString("type", $"{m.TypeIndex}");
                                }
                                xw.WriteEndElement();
                            }
                        }

                        if ((t.Flags & HKTagFlag.Interfaces) != 0)
                        {
                            foreach (var inter in t.Interfaces)
                            {
                                xw.WriteStartElement("interface");
                                {
                                    xw.WriteAttributeString("flags", $"{inter.Value}");
                                    xw.WriteAttributeString("type", $"{inter.TypeIndex}");
                                }
                                xw.WriteEndElement();
                            }
                        }
                    }
                    xw.WriteEndElement();
                }
            }
            xw.WriteEndElement();
            xw.Close();
        }

        public void Read(string fileName)
        {
            Read(new BinaryReaderEx(false, System.IO.File.ReadAllBytes(fileName)));
        }

        public void Read(BinaryReaderEx br)
        {
            uint fileLength = br.ReadHKOffset();

            if (fileLength != br.Length)
                throw new System.IO.InvalidDataException($"TAG0 section size does not match entire file size.");

            br.AssertASCII("TAG0");

            Dictionary<string, BinaryReaderEx> sections = new Dictionary<string, BinaryReaderEx>();
            while (br.Position < br.Length)
            {
                var sLength = br.ReadHKOffset();
                var sName = br.ReadASCII(4);
                byte[] data = br.ReadBytes((int)(sLength - 8));
                sections.Add(sName, new BinaryReaderEx(false, data));
            }

            br = sections["SDKV"];
            var sdkVersion = br.ReadASCII((int)br.Length);
            if (sdkVersion != "20150100")
                throw new System.IO.InvalidDataException($"Invalid SDKV SDK Version: '{sdkVersion}'. Expected '20150100'.");

            br = sections["DATA"];
            DATA = br.ReadBytes((int)br.Length);

            br = sections["TYPE"];
            ReadSectionTYPE(br);

            br = sections["INDX"];
            ReadSectionINDX(br);
        }

        private void ReadSectionTYPE(BinaryReaderEx br)
        {
            Types.Clear();

            Dictionary<string, BinaryReaderEx> sections = new Dictionary<string, BinaryReaderEx>();
            while (br.Position < br.Length)
            {
                var sLength = br.ReadHKOffset();
                var sName = br.ReadASCII(4);
                byte[] data = br.ReadBytes((int)(sLength - 8));
                sections.Add(sName, new BinaryReaderEx(false, data));
            }

            var typeStrings = sections["TSTR"].ReadHKStringList();
            var fieldStrings = sections["FSTR"].ReadHKStringList();

            //TNAM
            br = sections["TNAM"];
            var typeCount = br.ReadHKPackedInt();
            Types = new List<HKType>();
            Types.Add(null);

            for (int i = 1; i < typeCount; i++)
            {
                var t = new HKType();
                Types.Add(t);
            }

            for (int i = 1; i < typeCount; i++)
            {
                Types[i].Name = typeStrings[(int)br.ReadHKPackedInt()];
                var templateCount = br.ReadHKPackedInt();
                for (int j = 0; j < templateCount; j++)
                {
                    var template = new HKTypeTemplate();
                    template.Name = typeStrings[(int)br.ReadHKPackedInt()];
                    template.Value = br.ReadHKPackedInt();
                    Types[i].Templates.Add(template);
                }
            }

            //TBOD
            br = sections["TBOD"];
            while (br.Position < br.Length)
            {
                var typeIndex = br.ReadHKPackedInt();

                if (typeIndex == 0)
                    continue;

                var t = Types[(int)typeIndex];
                t.ParentTypeIndex = br.ReadHKPackedInt();
                t.Parent = Types[(int)t.ParentTypeIndex];
                t.Flags = (HKTagFlag)br.ReadHKPackedInt();

                if ((t.Flags & HKTagFlag.SubType) != 0)
                {
                    t.SubTypeFlags = br.ReadHKPackedInt();
                }

                if (((t.Flags & HKTagFlag.Pointer) != 0) && ((t.SubTypeFlags & 0xF) >= 6))
                {
                    t.PointerTypeIndex = br.ReadHKPackedInt();
                    t.Pointer = Types[(int)t.PointerTypeIndex];
                }

                if ((t.Flags & HKTagFlag.Version) != 0)
                {
                    t.Version = br.ReadHKPackedInt();
                }

                if ((t.Flags & HKTagFlag.ByteSize) != 0)
                {
                    t.ByteSize = br.ReadHKPackedInt();
                    t.Alignment = br.ReadHKPackedInt();
                }

                if ((t.Flags & HKTagFlag.AbstractValue) != 0)
                {
                    t.AbstractValue = br.ReadHKPackedInt();
                }

                if ((t.Flags & HKTagFlag.Members) != 0)
                {
                    uint memberCount = br.ReadHKPackedInt();
                    for (int i = 0; i < memberCount; i++)
                    {
                        var member = new HKMember();
                        member.Name = fieldStrings[(int)br.ReadHKPackedInt()];
                        member.Flags = br.ReadHKPackedInt();
                        member.ByteOffset = br.ReadHKPackedInt();
                        member.TypeIndex = br.ReadHKPackedInt();
                        member.Type = Types[(int)member.TypeIndex];
                        t.Members.Add(member);
                    }
                }

                if ((t.Flags & HKTagFlag.Interfaces) != 0)
                {
                    uint interfaceCount = br.ReadHKPackedInt();
                    for (int i = 0; i < interfaceCount; i++)
                    {
                        var inter = new HKInterface();
                        inter.TypeIndex = br.ReadHKPackedInt();
                        inter.Type = Types[(int)inter.TypeIndex];
                        inter.Value = br.ReadHKPackedInt();
                        t.Interfaces.Add(inter);
                    }
                }

                if ((t.Flags & HKTagFlag.Unknown) != 0)
                {
                    throw new NotImplementedException("Didn't know 'HKTagFlag.Unknown' was used.");
                }
            }

            //THSH
            br = sections["THSH"];
            var hashCount = br.ReadHKPackedInt();
            for (int i = 0; i < hashCount; i++)
            {
                var hashTypeIndex = br.ReadHKPackedInt();
                var hashValue = br.ReadUInt32();
                Types[(int)hashTypeIndex].Hash = hashValue;
            }
        }

        private void ReadSectionINDX(BinaryReaderEx br)
        {
            Items.Clear();
            Patches.Clear();

            Dictionary<string, BinaryReaderEx> sections = new Dictionary<string, BinaryReaderEx>();
            while (br.Position < br.Length)
            {
                var sLength = br.ReadHKOffset();
                var sName = br.ReadASCII(4);
                byte[] data = br.ReadBytes((int)(sLength - 8));
                sections.Add(sName, new BinaryReaderEx(false, data));
            }

            //ITEM
            br = sections["ITEM"];
            while (br.Position < br.Length)
            {
                var item = new HKItem();
                var flag = br.ReadUInt32();
                item.TypeIndex = (flag & 0xFFFFFF);
                item.Type = Types[(int)item.TypeIndex];
                item.IsPointer = ((flag & 0x10000000) != 0);
                item.OffsetPastDataStart = br.ReadUInt32();
                item.Count = br.ReadUInt32();
                Items.Add(item);
            }

            //PTCH
            br = sections["PTCH"];
            while (br.Position < br.Length)
            {
                var patch = new HKPatch();
                patch.TypeIndex = br.ReadUInt32();
                patch.Type = Types[(int)patch.TypeIndex];
                int offsetCount = br.ReadInt32();
                for (int j = 0; j < offsetCount; j++)
                {
                    patch.Offsets.Add(br.ReadUInt32());
                }
                Patches.Add(patch);
            }
        }
    }
}
