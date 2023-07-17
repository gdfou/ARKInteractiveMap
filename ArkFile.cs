using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;

namespace ArkFileDecode
{
    public static class MapsName
    {
        static public Dictionary<string, string> Names = new Dictionary<string, string>()
        {
            { "TheIsland", "The Island" },
            { "TheCenter", "The Center" },
            { "ScorchedEarth_P", "Scorched Earth" },
            { "Ragnarok", "Ragnarok" },
            { "Aberration_P", "Aberration" },
            { "Extinction", "Extinction" },
            { "Valguero_P", "Valguero" },
            { "Genesis", "Genesis : Part 1" },
            { "CrystalIsles", "Crystal Isles" },
            { "Gen2", "Genesis: Part 2" },
            { "LostIsland", "Lost Island" },
            { "Fjordur", "Fjordur" }
        };
    }

    public static class Ind
    {
        public static string Build(int level)
        {
            return new string(' ', 3 * level);
        }
    }

    public static class Log
    {
        public static bool Enabled { get; set; } = false;
        public static bool EnabledFile { get; set; } = false;
        public static string Filename { get; set; }

        public static void Init()
        {
            if (Enabled && EnabledFile)
            {
                var logFile = new FileStream(Filename, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite, 4096);
                var listener = new TextWriterTraceListener(logFile);
                Trace.Listeners.Add(listener);
            }
        }

        public static void Write(object value)
        {
            if (Enabled) Trace.Write(value);
        }

        public static void WriteLine(object value)
        {
            if (Enabled) Trace.WriteLine(value);
        }

        public static void Flush()
        {
            if (Enabled) Trace.Flush();
        }
    }

    /// <summary>
    /// extension de BinaryReader
    /// </summary>
    public class BinaryReader2 : BinaryReader
    {
        public BinaryReader2(Stream input) : base(input)
        {
        }

        // Format: <Uint32>[<char>...<0>]  => string, false
        // Format: <Uint32>[<wchar>...<0>] => string, true
        public (string str, bool unicode) ReadI32String()
        {
            int len = ReadInt32();
            // length => si négatif alors Unicode
            if (len > 0)
            {
                char[] str = ReadChars(len - 1);
                _ = ReadByte();
                return (new string(str), false);
            }
            else if (len < 0)
            {
                len = -len - 1;
                byte[] data = ReadBytes(len * 2);
                _ = ReadInt16();
                char[] charBuffer = new char[len];
                Decoder decoder = Encoding.Unicode.GetDecoder();
                _ = decoder.GetChars(data, 0, len * 2, charBuffer, 0);
                return (new string(charBuffer), true);
            }
            return ((string)null, false);
        }
    }

    /// <summary>
    /// extension de BinaryWriter
    /// </summary>
    public class BinaryWriter2 : BinaryWriter
    {
        public BinaryWriter2(Stream input) : base(input)
        {
        }

        // Formart: <Uint32>[<char>...<0>]
        // Formart: <Uint32>[<wchar>...<0>]
        public void WriteI32String(string str, bool unicode=false)
        {
            if (str == null)
            {
                Write(0);
            }
            // length => si négatif alors Unicode
            else if (!unicode)
            {
                Write(str.Length + 1);
                Write(str.ToCharArray());
                Write((byte)0);
            }
            else
            {
                Write(-(str.Length + 1));
                byte[] data = new byte[str.Length * 2];
                Encoder encoder = Encoding.Unicode.GetEncoder();
                _ = encoder.GetBytes(str.ToCharArray(), 0, str.Length, data, 0, false);
                Write(data);
                Write((UInt16)0);
            }
        }
    }

    public class ArkFileFields
    {
        public string Field { get; set; }
        public string Value { get; set; }
    }

    public class ArkFile
    {
        public int StartFlag { get; set; }
        public int FileVersion { get; set; }
        public string FileType { get; set; }
        public int NumberOfHeaderStrings { get; set; }
        public string FileNumber { get; set; }
        public string FileMode { get; set; }
        public string FileLevel { get; set; }
        public string MapName { get; set; }
        public string MapPath { get; set; }
        public int DataOffset { get; set; }

        public int PrimalBufferPersistentDataOffset { get; set; }

        public List<ArkFileFields> Fields { get; set; }

        public ArkStructProperty Root { get; set; }

        public ArkFile()
        {
        }

        public void Load(string inputFilename, bool enableLog = false, bool writeLog=false)
        {
            Log.Enabled = enableLog;
            Log.Filename = inputFilename + ".log";
            Log.EnabledFile = writeLog;
            Log.Init();
            Log.WriteLine("Input Filename: '" + inputFilename + "'");

            using BinaryReader2 binReader = new(File.Open(inputFilename, System.IO.FileMode.Open));
            Root = new ArkStructProperty(true);
            Fields = new List<ArkFileFields>();
            ReadHeader(binReader);
            Root.ReadValue(binReader);
            if (PrimalBufferPersistentDataOffset > 0)
            {
                var PrimalBufferPersistentData = new ArkStructProperty(true);
                PrimalBufferPersistentData.ReadValue(binReader);
            }
        }

        public void Save(string inputFilename)
        {
            using BinaryWriter2 binWriter = new(File.Open(inputFilename, System.IO.FileMode.Create));
            WriteHeader(binWriter);
            Root.Save(binWriter);
        }

        // Interface de recherche
        public ArkProperty FindSection(string sectionName)
        {
            return Root.FindSection(sectionName);
        }

        // Interface
        public Dictionary<string, List<MapMarker>> ReadMapMarkers()
        {
            var section = FindSection("MapMarkersPerMaps");
            if (section != null)
            {
                return (section as ArkArrayProperty).ReadMapMarkers();
            }
            return null;
        }

        public void WriteMapMarkers(Dictionary<string, List<MapMarker>> markers)
        {
            var section = FindSection("MapMarkersPerMaps");
            if (section != null)
            {
                (section as ArkArrayProperty).WriteMapMarkers(markers);
            }
        }

        protected void AddField(string field, object value)
        {
            Fields.Add(new ArkFileFields() { Field = field, Value = value.ToString() });
        }

        public Dictionary<string, List<int>> ReadFogOfWars()
        {
            var section = FindSection("PerMapFogOfWars");
            if (section != null)
            {
                return (section as ArkArrayProperty).ReadFogOfWars();
            }
            return null;
        }

        protected void ReadHeader(BinaryReader2 binReader)
        {
            // this is probably a either a Start Flag
            StartFlag = binReader.ReadInt32();
            if (StartFlag == 0) 
            {
                throw new ArkException($"ArkFile.StartFlag {StartFlag} non implemented.");
            }
            Log.WriteLine($"Start Flag: {StartFlag:x8}");
            AddField("Start Flag", StartFlag);
            // File Version
            FileVersion = binReader.ReadInt32();
            Log.WriteLine($"File Version: {FileVersion}");
            AddField("File Version", FileVersion);
            // 16 NULL Bytes for padding
            _ = binReader.ReadBytes(16);
            // PrimalLocalProfile
            FileType = binReader.ReadI32String().str;
            Log.WriteLine($"File Type: '{FileType}'");
            AddField("File Type", FileType);
            if (FileType != "PrimalLocalProfile") // Only for PlayerLocalData file
            {
                //throw new ArkException($"ArkFile.FileType {FileType} non implemented.");
            }

            // value is always 0
            _ = binReader.ReadInt32();
            // Strong suspicion that this number refers to the number of NTStrings that will follow
            NumberOfHeaderStrings = binReader.ReadInt32();
            Log.WriteLine($"Number of header strings: {NumberOfHeaderStrings}");
            if (NumberOfHeaderStrings != 5)
            {
                throw new ArkException($"ArkFile.nbHeaderStrings {NumberOfHeaderStrings} non implemented.");
            }

            FileNumber = binReader.ReadI32String().str;
            Log.WriteLine($"File Number: '{FileNumber}'");
            AddField("File Number", FileNumber);
            // PrimalLocalProfile_0 for PlayerLocalData.arkprofile file
            // PrimalTribeData_7 for <id>.arktribe file
            // PrimalPlayerDataBP_C_13 for <id>.arkprofile

            // "ArkGameMode"
            FileMode = binReader.ReadI32String().str;
            Log.WriteLine("File Mode: '" + FileMode + "'");
            AddField("File Mode", FileMode);
            // "PersistentLevel"
            FileLevel = binReader.ReadI32String().str;
            Log.WriteLine("File Level: '" + FileLevel + "'");
            AddField("File Level", FileLevel);

            // current map name ?
            MapName = binReader.ReadI32String().str;
            Log.WriteLine("Map Name: '" + MapName + "'");
            AddField("Map Name", MapName);

            // The asset path for the map this character is on. /Game/Maps/TheIslandSubMaps/TheIsland
            MapPath = binReader.ReadI32String().str;
            Log.WriteLine($"Map path: '{MapPath}'");
            AddField("Map Path", MapPath);

            // 12 NULL Bytes for padding
            _ = binReader.ReadBytes(12);
            // The size of the header in bytes. Also the start position of the container struct
            DataOffset = binReader.ReadInt32();
            Log.WriteLine($" Offset: {DataOffset}");
            AddField("Data Offset", DataOffset);

            // Header extension
            if (FileType == "PrimalPlayerDataBP_C")
            {
                _ = binReader.ReadBytes(20);
                string str = binReader.ReadI32String().str;
                AddField("Primal Buffer Persistent Data", str);
                _ = binReader.ReadInt32();
                _ = binReader.ReadInt32();
                str = binReader.ReadI32String().str;
                AddField("Primal Buffer Persistent Data Number", str);
                _ = binReader.ReadBytes(12);
                PrimalBufferPersistentDataOffset = binReader.ReadInt32();
            }

            Log.Flush();
            // Jump to Data
            binReader.BaseStream.Position = DataOffset;
        }

        protected void WriteHeader(BinaryWriter2 binWriter)
        {
            long start_pos = binWriter.BaseStream.Position;
            // this is probably a either a Start Flag
            binWriter.Write(StartFlag);
            binWriter.Write(FileVersion);
            // 16 NULL Bytes for padding
            binWriter.Write(new byte[16]);
            // PrimalLocalProfile
            binWriter.WriteI32String(FileType);
            // value is always 0
            binWriter.Write(0);
            // Strong suspicion that this number refers to the number of NTStrings that will follow
            binWriter.Write(5);
            // PrimalLocalProfile
            binWriter.WriteI32String(FileNumber);
            // "ArkGameMode"
            binWriter.WriteI32String(FileMode);
            // "PersistentLevel"
            binWriter.WriteI32String(FileLevel);
            // current map name ?
            binWriter.WriteI32String(MapName);
            // The asset path for the map this character is on. /Game/Maps/TheIslandSubMaps/TheIsland
            binWriter.WriteI32String(MapPath);
            // 12 NULL Bytes for padding
            binWriter.Write(new byte[12]);
            // The size of the header in bytes. Also the start position of the container struct => compute new data offset in case of header string changed
            DataOffset = (int)(binWriter.BaseStream.Position - start_pos) + 8;
            binWriter.Write(DataOffset);
            // 4 Null Bytes for padding
            binWriter.Write(0);
        }
    }

    /// <summary>
    /// Property = Name (I32String) + Type (I32String) + Size (I32) + Index (I32)
    /// </summary>
    public class ArkProperty : TreeViewItemBase
    {
        public ArkProperty() : base()
        {
        }
        public ArkProperty(string type) : base()
        {
            Type = type;
        }
        public ArkProperty(ArkProperty p) : base()
        {
            Set(p);
        }
        // File properties
        public string Name { get; set; }
        public string Type { get; set; }
        public int Size { get; set; }
        public int Index { get; set; }
        public object Value { get; set; }
        // Interface
        public string Label { get; set; }
        public int Level { get; set; }
        // For writer
        protected long sizePos_;
        protected long valuePos_;

        public void Set(ArkProperty p)
        {
            Name = p.Name;
            Type = p.Type;
            Size = p.Size;
            Index = p.Index;
            Label = Name;
            Level = p.Level;
        }

        // Interface TreeView
        public ObservableCollection<ArkProperty> Childrens { get; set; }

        // Interface ListView
        public string ValueString => GetValueString();
        public virtual string GetValueString()
        {
            return Value?.ToString();
        }
        public virtual List<ArkProperty> GetItemList()
        {
            List<ArkProperty> list = new();
            list.Add(this);
            return list;
        }

        public override string ToString()
        {
            return $"{Name}: {Type}, {Size}, {Index}";
        }

        // Property Header = Name (I32String) + Type (I32String) + Size (I32) + Index (I32)
        public ArkProperty ReadPropertyHeader(BinaryReader2 binReader)
        {
            try
            {
                ArkProperty result = new()
                {
                    Name = binReader.ReadI32String().str
                };
                if (result.Name == null) // end of file
                {
                    return null;
                }
                if (result.Name == "None")  // struct end tag
                {
                    return result;
                }
                result.Type = binReader.ReadI32String().str;
                result.Size = binReader.ReadInt32();
                result.Index = binReader.ReadInt32();
                return result;
            }
            catch (Exception ex)
            {
                throw new ArkException(ex.Message);
            }
        }

        public virtual void ReadValue(BinaryReader2 binReader, int arrayIndex=-1)
        {
            Value = binReader.ReadBytes(Size);
        }

        public ArkProperty ReadProperty(BinaryReader2 binReader)
        {
            try
            {
                ArkProperty property = ReadPropertyHeader(binReader);
                if ((property == null) || (property.Type == null)) // end of file or struct end tag
                {
                    return null;
                }
                property.Level = Level;
                Log.Write($"{Ind.Build(Level)}{property.Name}:{property.Type}({property.Size}).{property.Index}");
                Log.Flush();
                switch (property.Type)
                {
                    case "ArrayProperty":
                        {
                            ArkArrayProperty p = new(property);
                            p.ReadHeader(binReader);
                            p.ReadValue(binReader);
                            property = p;
                            break;
                        }

                    case "ObjectProperty":
                        {
                            ArkObjectProperty p = new(property);
                            p.ReadValue(binReader);
                            property = p;
                            break;
                        }

                    case "StructProperty":
                        {
                            ArkStructProperty p = new(property);
                            p.ReadHeader(binReader);
                            p.ReadValue(binReader);
                            property = p;
                            break;
                        }

                    default:
                        {
                            ArkValueProperty p = new(property);
                            p.ReadValue(binReader);
                            property = p;
                            break;
                        }
                }
                Log.Flush();
                return property;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                Application.Current.Shutdown();
            }
            return null;
        }

        public virtual void WritePropertyHeader(BinaryWriter2 binWriter)
        {
            if (Name != null)
            {
                binWriter.WriteI32String(Name);
                binWriter.WriteI32String(Type);
                sizePos_ = binWriter.BaseStream.Position;
                binWriter.Write(Size);
                binWriter.Write(Index);
                valuePos_ = binWriter.BaseStream.Position;
            }
        }

        public virtual void WriteValue(BinaryWriter2 binWriter)
        {
            throw new ArkException("WriteValue non implemented.");
        }

        public virtual void PatchSize(BinaryWriter2 binWriter)
        {
            long currentPos = binWriter.BaseStream.Position;
            binWriter.BaseStream.Position = sizePos_;
            binWriter.Write(Size);
            binWriter.BaseStream.Position = currentPos;
        }

        public virtual void WriteEnd(BinaryWriter2 binWriter)
        {
        }

        public virtual void WriteProperty(BinaryWriter2 binWriter)
        {
            WritePropertyHeader(binWriter);
            WriteValue(binWriter);
            WriteEnd(binWriter);
        }

        public virtual ArkProperty Remove(ArkProperty property)
        {
            return (property == this) ? this : null;
        }
    }

    /// <summary>
    /// Int, float or string value
    /// </summary>
    public class ArkValueProperty : ArkProperty
    {
        protected bool unicode_; // to specify unicode string
        protected byte dataAux_;

        public ArkValueProperty(string type, int level=0) : base()
        {
            Level = level;
            Type = type;
        }

        public ArkValueProperty(ArkProperty p) : base(p)
        {
        }

        public ArkValueProperty(string type, string name, object value) : base()
        {
            Type = type;
            Name = name;
            Value = value;
            ComputeSize();
        }

        public override void ReadValue(BinaryReader2 binReader, int arrayIndex=-1)
        {
            try
            {

                switch (Type)
                {
                    case "StrProperty":
                    case "NameProperty":
                        {
                            (Value, unicode_) = binReader.ReadI32String();
                            break;
                        }

                    case "Int16Property":
                        {
                            Value = binReader.ReadInt16();
                            break;
                        }

                    case "UInt16Property":
                        {
                            Value = binReader.ReadUInt16();
                            break;
                        }

                    case "IntProperty":
                        {
                            Value = binReader.ReadInt32();
                            break;
                        }

                    case "UInt32Property":
                        {
                            Value = binReader.ReadUInt32();
                            break;
                        }

                    case "Int64Property":
                        {
                            Value = binReader.ReadInt64();
                            break;
                        }

                    case "UInt64Property":
                        {
                            Value = binReader.ReadUInt64();
                            break;
                        }

                    case "ByteProperty":
                        {
                            if (Size > 1)
                            {
                                // jamais eu encore !!
                                Value = binReader.ReadBytes(Size);
                            }
                            else
                            {
                                // WARNING actually in 'PlayerLocalData.arkprofile' file, these property type is not a single byte but a string ('None') followed by 1 byte equal to 0
                                // Probably it's a definition like an 'Ark Class Object' and the last byte is propably an number and the string a object name.
                                (Value, unicode_) = binReader.ReadI32String();
                                dataAux_ = binReader.ReadByte();
                                string str = Value as string;
                                if (str != "None")
                                {
                                    str += $"_{dataAux_}";
                                }
                            }
                            break;
                        }

                    case "BoolProperty":
                        {
                            Value = binReader.ReadBoolean();
                            break;
                        }

                    case "FloatProperty":
                        {
                            Value = binReader.ReadSingle();
                            break;
                        }

                    case "DoubleProperty":
                        {
                            Value = binReader.ReadDouble();
                            break;
                        }

                    default:
                        {
                            throw new ArkException($"ReadValue() for ArkProperty.Type {Type} non implemented.");
                        }
                }
                if (arrayIndex >= 0)
                {
                    if ((Value?.GetType() == typeof(string)) || (Value == null))
                        Log.WriteLine($"{Ind.Build(Level)}#{arrayIndex,3}.{{ '{Value}' }}");
                    else if (Value.GetType() == typeof(UInt32))
                        Log.WriteLine($"{Ind.Build(Level)}#{arrayIndex,3}.{{ '{Value:X8}' }}");
                    else if (Value.GetType() == typeof(byte[]))
                        Log.WriteLine($"{Ind.Build(Level)}#{arrayIndex,3}.{{ [{Size} bytes] }}");
                    else
                        Log.WriteLine($"{Ind.Build(Level)}#{arrayIndex,3}.{{ {Value} }}");
                }
                else
                {
                    if (Value == null)
                        Log.WriteLine($" = ''");
                    else if (Value.GetType() == typeof(string))
                        Log.WriteLine($" = '{Value}'");
                    else if (Value.GetType() == typeof(byte[]))
                        Log.WriteLine($" = [{Size} bytes]");
                    else
                        Log.WriteLine($" = {Value}");
                }
                Log.Flush();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                Application.Current.Shutdown();
            }
        }

        public override string GetValueString()
        {
            if (Name == "UploadTime")
            {
                DateTime dt = new(1970, 1, 1);
                dt = dt.AddSeconds((int)Value).ToLocalTime();
                return dt.ToString("yyyy/MM/dd HH:mm:ss");
            }
            else
            {
                return Value?.ToString();
            }
        }

        public override List<ArkProperty> GetItemList()
        {
            return base.GetItemList();
        }

        public bool ContainsUnicodeCharacter(string input)
        {
            return input.Any(c => c > 127);
        }

        public void ComputeSize()
        {
            switch (Type)
            {
                case "StrProperty":
                case "NameProperty":
                    {
                        unicode_ = ContainsUnicodeCharacter(Value as string);
                        Size = ((Value as string).Length + 1) * (unicode_ ? 2 : 1) + 4; // + 4 pour size + 1 pour le zéro de fin
                        break;
                    }
                case "Int16Property": Size = 2;break;
                case "UInt16Property": Size = 2; break;
                case "IntProperty": Size = 4; break;
                case "UInt32Property": Size = 4; break;
                case "Int64Property": Size = 8; break;
                case "UInt64Property": Size = 8; break;
                case "BoolProperty": Size = 1; break;
                case "FloatProperty": Size = 4; break;
                case "DoubleProperty": Size = 8; break;
            }
        }

        public override void WriteValue(BinaryWriter2 binWriter)
        {
            switch (Type)
            {
                case "StrProperty":
                case "NameProperty":
                    {
                        binWriter.WriteI32String((string)Value, unicode_);
                        break;
                    }

                case "Int16Property":
                    {
                        binWriter.Write((Int16)Value);
                        break;
                    }

                case "UInt16Property":
                    {
                        binWriter.Write((UInt16)Value);
                        break;
                    }

                case "IntProperty":
                    {
                        binWriter.Write((Int32)Value);
                        break;
                    }

                case "UInt32Property":
                    {
                        binWriter.Write((UInt32)Value);
                        break;
                    }

                case "Int64Property":
                    {
                        binWriter.Write((Int64)Value);
                        break;
                    }

                case "UInt64Property":
                    {
                        binWriter.Write((UInt64)Value);
                        break;
                    }

                case "ByteProperty":
                    {
                        if (Size > 1)
                        {
                            binWriter.Write((byte[])Value);
                        }
                        else
                        {
                            // WARNING actually in 'PlayerLocalData.arkprofile' file, these property type is not a single byte but a string ('None') followed by 1 byte equal to 0
                            // Probably it's a definition like an 'Ark Class Object' and the last byte is propably an number and the string a object name.
                            binWriter.WriteI32String(Value as string);
                            binWriter.Write(dataAux_);
                        }
                        break;
                    }

                case "BoolProperty":
                    {
                        binWriter.Write((bool)Value);
                        break;
                    }

                case "FloatProperty":
                    {
                        binWriter.Write((Single)Value);
                        break;
                    }

                case "DoubleProperty":
                    {
                        binWriter.Write((Double)Value);
                        break;
                    }

                default:
                    {
                        throw new ArkException($"WriteValue() for ArkProperty.Type {Type} non implemented.");
                    }
            }
        }
    }

    /// <summary>
    /// Ark property with child values
    /// </summary>
    public class ArkValuesProperty : ArkProperty
    {
        protected ObservableCollection<ArkProperty> Values_ = new();

        public ArkValuesProperty() : base()
        {
            Value = Values_;
        }

        public ArkValuesProperty(ArkProperty p) : base(p)
        {
            Value = Values_;
        }

        // Interface ListView
        public override List<ArkProperty> GetItemList()
        {
            List<ArkProperty> list = new();
            foreach (ArkProperty item in Values_)
            {
                list.Add(item);
            }
            return list;
        }

        public override string GetValueString()
        {
            return (Values_.Count > 1) ? $"<{Values_.Count} items>" : "1 item";
        }

        public override ArkProperty Remove(ArkProperty property)
        {
            // this property
            if (property == this)
            {
                return this;
            }
            // childs properties
            foreach (ArkProperty item in Values_)
            {
                if (item == property)
                {
                    Values_.Remove(item);
                    Childrens?.Remove(item);
                    return item;
                }
            }
            // inside childs properties
            foreach (ArkProperty item in Values_)
            {
                ArkProperty p = item.Remove(property);
                if (p != null)
                {
                    return p;
                }
            }
            return null;
        }
    }

    public class LinearColor
    {
        public float r { get; set; }
        public float g { get; set; }
        public float b { get; set; }
        public float a { get; set; }
    }

    /// <summary>
    /// StructProperty = <Property> + Struct Type (I32String) + [Values (N<Propery>)] + 'None' (I32String)
    /// </summary>
    public class ArkStructProperty : ArkValuesProperty
    {
        public ArkStructProperty(bool root) : base()
        {
            Childrens = new ObservableCollection<ArkProperty>();
        }

        public ArkStructProperty(string type, int level=0) : base()
        {
            Level = level;
            Childrens = new ObservableCollection<ArkProperty>();
            Type = type ?? "StructProperty";
            Label = "{" + Type + "}";
        }

        public ArkStructProperty(ArkProperty p) : base(p)
        {
            Childrens = new ObservableCollection<ArkProperty>();
        }

        public ArkStructProperty(string type, string stype, string name) : base()
        {
            Type = type;
            StructType = stype;
            Name = name;
        }
        public ArkStructProperty(string type, string stype, string name, object value) : base()
        {
            Type = type;
            StructType = stype;
            Name = name;
            Value = value;
            ComputeSize();
        }

        public string StructType { get; set; }

        private readonly List<string> StructTypeExtension = new() { "Color", "Vector", "UniqueNetIdRepl", "LinearColor" };

        // Interface de recherche
        public ArkProperty FindSection(string sectionName)
        {
            return Values_.FirstOrDefault(x => x.Name == sectionName);
        }

        public void ReadHeader(BinaryReader2 binReader)
        {
            StructType = binReader.ReadI32String().str;
            if (!StructTypeExtension.Contains(StructType))
                Log.WriteLine($".{StructType} =");
        }

        public void Add (ArkProperty pc)
        {
            if (pc != null)
            {
                Values_.Add(pc);
            }
            else
            {
                Values_.Add(new ArkProperty("None"));
            }
        }

        public override void ReadValue(BinaryReader2 binReader, int arrayIndex=-1)
        {
            if (StructType == "Color") 
            {
                Childrens = null; // cas particulier
                Value = binReader.ReadUInt32();
                Log.WriteLine($".{StructType} = {Value:x8}");
            }
            else if (StructType == "Vector")
            {
                Childrens = null; // cas particulier
                Value = binReader.ReadBytes(Size);
                var str = BitConverter.ToString(Value as byte[]).Replace("-", "");
                Log.WriteLine($".{StructType} = {str}");
            }
            else if (StructType == "UniqueNetIdRepl")
            {
                Childrens = null; // cas particulier
                var value = binReader.ReadInt32();
                Value = binReader.ReadI32String().str;
                Log.WriteLine($".{StructType} = ({value}, {Value})");
            }
            else if (StructType == "LinearColor")
            {
                Childrens = null; // cas particulier
                LinearColor color = new();
                color.r = binReader.ReadSingle();
                color.g = binReader.ReadSingle();
                color.b = binReader.ReadSingle();
                color.a = binReader.ReadSingle();
                Value = color;
                Log.WriteLine($".{StructType} = ({color.r},{color.g},{color.b},{color.a})");
            }
            else
            {
                if (arrayIndex >= 0)
                {
                    Log.WriteLine($"{Ind.Build(Level)}#{arrayIndex}.{{");
                }
                else
                {
                    Log.WriteLine($"{Ind.Build(Level)}{{");
                }
                Level++;
                bool computeSize = Size == 0;
                ArkProperty pc;
                do
                {
                    long before_read = binReader.BaseStream.Position;
                    pc = ReadProperty(binReader);
                    if (pc != null)
                    {
                        if (computeSize)
                        {
                            Size += (int)(binReader.BaseStream.Position - before_read);
                        }
                        Values_.Add(pc);
                        if (pc.Childrens != null || Type == null) // Type = null if root
                        {
                            Childrens.Add(pc);
                        }
                    }
                    else
                    {
                        Log.WriteLine($"{Ind.Build(Level)}<None>");
                    }
                    Log.Flush();
                }
                while (pc != null);
                Level--;
                Log.WriteLine($"{Ind.Build(Level)}}}");
                Log.Flush();
            }
        }

        public override string GetValueString()
        {
            if (StructType == "Color") 
            {
                return $"Color {Value:x8}";
            }
            else 
            {
                return base.GetValueString();
            }
        }

        public override string ToString()
        {
            if (Name != null) 
            {
                return $"{Name}: {Type}, {Size}, {Index}, {StructType}";
            }
            else 
            {
                return $"{Label}: {Type}, {Size}, {Index}, {StructType}";
            }
        }

        public override void WritePropertyHeader(BinaryWriter2 binWriter)
        {
            base.WritePropertyHeader(binWriter);
            if (StructType != null) 
            {
                binWriter.WriteI32String(StructType);
            }
        }

        public void ComputeSize()
        {
            if (StructType == "Color")
            {
                Size = 4;
            }
            else if (StructType == "UniqueNetIdRepl")
            {
                Size = 4 + (Value as string).Length + 5;
            }
            else if (StructType == "LinearColor")
            {
                Size = 4 + 4;
            }
        }

        public override void WriteValue(BinaryWriter2 binWriter)
        {
            if (StructType == "Color")
            {
                binWriter.Write((UInt32)Value);
            }
            else if (StructType == "Vector")
            {
                binWriter.Write((byte[])Value);
            }
            else 
            {
                foreach (ArkProperty item in Values_)
                {
                    item.WriteProperty(binWriter);
                }
                binWriter.WriteI32String("None"); // structure end tag
            }
        }

        public override void WriteEnd(BinaryWriter2 binWriter)
        {
            long current_pos = binWriter.BaseStream.Position;
            if (Size < 0)
            {
                Size = (int)(current_pos - valuePos_);
                PatchSize(binWriter);
            }
        }

        public void Save(BinaryWriter2 binWriter)
        {
            if (Name == null) // root
            {
                WriteValue(binWriter);
                binWriter.Write(0);
                binWriter.Flush();
            }
        }
    }

    /// <summary>
    /// ArrayProperty = Child Property Type (I32String) + Length (I32) + [Values]
    /// </summary>
    public class ArkArrayProperty : ArkValuesProperty
    {
        public ArkArrayProperty(ArkProperty p) : base(p)
        {
            Childrens = new ObservableCollection<ArkProperty>();
        }

        public string ChildPropertyType { get; set; }
        public int Length { get; set; }

        public void ReadHeader(BinaryReader2 binReader)
        {
            ChildPropertyType = binReader.ReadI32String().str;
            Length = binReader.ReadInt32();
            if (Length == 0)
            {
                Childrens = null; // cas particulier => bytes array
                Log.WriteLine($".{ChildPropertyType}[{Length}] = []");
            }
            else
            {
                Log.WriteLine($".{ChildPropertyType}[{Length}] =");
            }
        }

        // Interface
        public Dictionary<string, List<MapMarker>> ReadMapMarkers() 
        {
            var dict = new Dictionary<string, List<MapMarker>>();
            foreach (ArkProperty item in Values_)
            {
                if (item != null && item.Type == "StructProperty")
                {
                    string map_name = "";
                    var markers = new List<MapMarker>();
                    foreach (ArkProperty sub_item in (item as ArkStructProperty).GetItemList())
                    {
                        if (sub_item.Name == "MapName")
                        {
                            map_name = sub_item.Value as string;
                            if (MapsName.Names.TryGetValue(map_name, out var new_map_name))
                            {
                                map_name = new_map_name;
                            }
                            else
                            {
                                MessageBox.Show($"Le nom de carte '{map_name}' n'a pas été trouvé dans le dictionnaire MapsName.Names.");
                            }
                        }
                        else if (sub_item.Name == "MapMarkers")
                        {
                            if (sub_item != null && sub_item.Type == "ArrayProperty" && (sub_item as ArkArrayProperty).ChildPropertyType == "StructProperty")
                            {
                                foreach (ArkStructProperty poi in (sub_item as ArkArrayProperty).Values_)
                                {
                                    var marker = new MapMarker();
                                    foreach (ArkProperty poi_item in poi.GetItemList())
                                    {
                                        switch (poi_item.Name)
                                        {
                                            case "Name":
                                                marker.Name = poi_item.Value as string;
                                                break;

                                            case "OverrideMarkerTextColor":
                                                marker.Color = (uint)poi_item.Value;
                                                break;

                                            case "coord1f":
                                                marker.Lon = (float)poi_item.Value;
                                                break;

                                            case "coord2f":
                                                marker.Lat = (float)poi_item.Value;
                                                break;

                                            case "coord1":
                                                marker.Coord1 = (int)poi_item.Value;
                                                break;

                                            case "coord2":
                                                marker.Coord2 = (int)poi_item.Value;
                                                break;
                                        }
                                    }
                                    markers.Add(marker);
                                }
                            }
                        }
                    }
                    dict[map_name] = markers;
                }
            }
            return dict;
        }

        public void WriteMapMarkers(Dictionary<string, List<MapMarker>> markersMap)
        {
            Size = -1;
            Length = -1;
            foreach (ArkProperty item in Values_)
            {
                if (item != null && item.Type == "StructProperty")
                {
                    List<MapMarker> markers = null;
                    foreach (ArkProperty sub_item in (item as ArkStructProperty).GetItemList())
                    {
                        if (sub_item.Name == "MapName")
                        {
                            string map_name = sub_item.Value as string;
                            if (MapsName.Names.TryGetValue(map_name, out var new_map_name))
                            {
                                markersMap.TryGetValue(new_map_name, out markers);
                            }
                            else
                            {
                                MessageBox.Show($"Le nom de carte '{map_name}' n'a pas été trouvé dans le dictionnaire MapsName.Names.");
                            }
                        }
                        else if (sub_item.Name == "MapMarkers")
                        {
                            if (sub_item != null && sub_item.Type == "ArrayProperty")
                            {
                                var sub_item_array = sub_item as ArkArrayProperty;
                                sub_item_array.Size = -1;
                                sub_item_array.Length = -1;
                                if (markers != null)
                                {
                                    sub_item_array.Values_.Clear();
                                }
                                for (int i = 0; i < markers?.Count; i++)
                                {
                                    var marker = markers[i];
                                    var ark_marker = new ArkStructProperty("StructProperty", null, null);
                                    ark_marker.Add(new ArkValueProperty("IntProperty", "coord1", marker.Coord1));
                                    ark_marker.Add(new ArkValueProperty("IntProperty", "coord2", marker.Coord2));
                                    ark_marker.Add(new ArkValueProperty("StrProperty", "Name", marker.Name));
                                    ark_marker.Add(new ArkStructProperty("StructProperty", "Color", "OverrideMarkerTextColor", marker.Color));
                                    ark_marker.Add(new ArkValueProperty("FloatProperty", "coord1f", marker.Lon));
                                    ark_marker.Add(new ArkValueProperty("FloatProperty", "coord2f", marker.Lat));
                                    sub_item_array.Add(ark_marker);
                                }
                                markers = null;
                            }
                        }
                    }
                }
            }
        }

        public void Add(ArkProperty item)
        {
            Values_.Add(item);
        }

        public Dictionary<string, List<int>> ReadFogOfWars()
        {
            var dict = new Dictionary<string, List<int>>();
            foreach (ArkProperty item in Values_)
            {
                if (item != null && item.Type == "StructProperty")
                {
                    string map_name = "";
                    var fogs = new List<int>();
                    foreach (ArkProperty sub_item in (item as ArkStructProperty).GetItemList())
                    {
                        if (sub_item.Name == "MapName")
                        {
                            map_name = sub_item.Value as string;
                            if (MapsName.Names.TryGetValue(map_name, out var new_map_name))
                            {
                                map_name = new_map_name;
                            }
                            else
                            {
                                MessageBox.Show($"Le nom de carte '{map_name}' n'a pas été trouvé dans le dictionnaire MapsName.Names.");
                            }
                        }
                        else if (sub_item.Name == "UnlockMask")
                        {
                            if (sub_item != null && sub_item.Type == "ArrayProperty" && (sub_item as ArkArrayProperty).ChildPropertyType == "BoolProperty")
                            {
                                foreach (ArkValueProperty poi in (sub_item as ArkArrayProperty).Values_)
                                {
                                    fogs.Add(((bool)poi.Value) ? 1: 0);
                                }
                            }
                        }
                    }
                    dict[map_name] = fogs;
                }
            }
            return dict;
        }

        public override void ReadValue(BinaryReader2 binReader, int arrayIndex=-1)
        {
            if (Length == 0) 
                return;
            Log.WriteLine($"{Ind.Build(Level)}[");
            Level++;
            switch (ChildPropertyType)
            {
                case "ByteProperty":
                    {
                        ArkValueProperty po = new(ChildPropertyType, Level);
                        po.Size = Length; // warning: patch with length !
                        po.ReadValue(binReader, 0);
                        Add(po);
                        Childrens = null; // cas particulier => bytes array
                        break;
                    }

                case "StructProperty":
                    {
                        for (int i = 0; i < Length; i++)
                        {
                            ArkStructProperty po = new(ChildPropertyType, Level);
                            po.ReadValue(binReader, i);
                            Add(po);
                            Childrens.Add(po);
                        }
                        break;
                    }

                case "ObjectProperty":
                    {
                        // ObjectProperty = Quantity (I32) + Value (I32String)
                        for (int i = 0; i < Length; i++)
                        {
                            ArkObjectProperty po = new(ChildPropertyType, Level);
                            // cas particulier => bidouille !!!
                            if (Length == 1)
                            {
                                po.Size = Size;
                            }
                            po.ReadValue(binReader, i);
                            Add(po);
                        }
                        break;
                    }

                case "BoolProperty":
                case "Int16Property":
                case "IntProperty":
                case "UInt32Property":
                case "Int64Property":
                case "UInt64Property":
                case "FloatProperty":
                case "DoubleProperty":
                case "StrProperty":
                case "NameProperty":
                    {
                        for (int i = 0; i < Length; i++)
                        {
                            ArkValueProperty po = new(ChildPropertyType, Level);
                            po.ReadValue(binReader, i);
                            Add(po);
                        }
                        break;
                    }

                default:
                    throw new ArkException($"ArkProperty.ChildPropertyType {ChildPropertyType} non implemented.");
            }
            Level--;
            Log.WriteLine($"{Ind.Build(Level)}]");
        }

        public override string GetValueString()
        {
            if (ChildPropertyType == "ByteProperty")
            {
                return "[bytes]";
            }
            else
            {
                return base.GetValueString();
            }
        }

        public override string ToString()
        {
            return $"{Name}: {Type}, {Size}, {Index}, {ChildPropertyType}, {Length}";
        }

        public override void WritePropertyHeader(BinaryWriter2 binWriter)
        {
            base.WritePropertyHeader(binWriter);
            binWriter.WriteI32String(ChildPropertyType);
            valuePos_ = binWriter.BaseStream.Position;
            if (Length < 0)
            {
                Length = Values_.Count;
            }
            binWriter.Write(Length);
        }

        public override void WriteValue(BinaryWriter2 binWriter)
        {
            foreach (ArkProperty item in Values_)
            {
                item.WriteProperty(binWriter);
            }
        }

        public override void WriteEnd(BinaryWriter2 binWriter)
        {
            long current_pos = binWriter.BaseStream.Position;
            if (Size < 0)
            {
                Size = (int)(current_pos - valuePos_);
                PatchSize(binWriter);
            }
        }
    }

    /// <summary>
    /// ObjectProperty = Quantity (I32) + Value (I32String)
    /// </summary>
    public class ArkObjectProperty : ArkProperty
    {
        public ArkObjectProperty(string type, int level) : base()
        {
            Level = level;
            Type = type ?? "ObjectProperty";
        }

        public ArkObjectProperty(ArkProperty p) : base(p)
        {
        }

        public int Quantity { get; set; }

        public override void ReadValue(BinaryReader2 binReader, int arrayIndex=-1)
        {
            Quantity = binReader.ReadInt32();
            // WARNING if Size is 8 then Value is not a string but an Int32 !
            // If Size is 4 then there is no value
            if (Size == 8) 
            {
                Value = binReader.ReadInt32();
                Log.WriteLine($" = {Quantity}, {Value}");
            }
            else if (Size == 4) 
            {
                Log.WriteLine($" = {Quantity}");
            }
            else
            {
                // Attention bidouille : si Size = 12 et que string_size = 0 alors value = 2 int32 !
                Value = binReader.ReadI32String().str;
                if ((Value as string).Length == 0 && Size > 8)
                {
                    Value = binReader.ReadBytes(Size - 4);
                }
                if (arrayIndex >= 0)
                {
                    Log.WriteLine($"{Ind.Build(Level)}#{arrayIndex}.{{ {Quantity}, '{Value}' }}");
                }
                else
                {
                    Log.WriteLine($" = {Quantity}, '{Value}'");
                }
            }
            Log.Flush();
        }

        public override void WriteValue(BinaryWriter2 binWriter)
        {
            binWriter.Write(Quantity);
            if (Size == 8)
            {
                binWriter.Write((int)Value);
            }
            else if (Size != 4)
            {
                binWriter.WriteI32String((string)Value);
            }
        }
    }

    /// <summary>
    /// INotifyPropertyChanged
    /// </summary>
    public class TreeViewItemBase : INotifyPropertyChanged
    {
        private bool isSelected;
        public bool IsSelected
        {
            get => isSelected;
            set
            {
                if (value != isSelected)
                {
                    isSelected = value;
                    NotifyPropertyChanged("IsSelected");

                }
            }
        }

        private bool isExpanded;
        public bool IsExpanded
        {
            get => isExpanded;
            set
            {
                if (value != isExpanded)
                {
                    isExpanded = value;
                    NotifyPropertyChanged("IsExpanded");
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void NotifyPropertyChanged(string propName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
    }

    public class ArkException : Exception
    {
        public ArkException(string message) : base(message)
        {
        }
    }

    public class MapMarker
    {
        public int Coord1 { get; set; }
        public int Coord2 { get; set; }
        public float Lat { get; set; }
        public float Lon { get; set; }
        public string Name { get; set; }
        public UInt32 Color { get; set; }
    }
}
