using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Il2CppDumper
{
    public class BinaryStream : IDisposable
    {
        public double Version;
        public bool Is32Bit;
        public ulong ImageBase;
        private readonly Stream stream;
        private readonly BinaryReader reader;
        private readonly BinaryWriter writer;
        private readonly Dictionary<FieldInfo, VersionAttribute[]> attributeCache;

        public BinaryStream(Stream input)
        {
            stream = input;
            reader = new BinaryReader(stream, Encoding.UTF8, true);
            writer = new BinaryWriter(stream, Encoding.UTF8, true);
            attributeCache = new();
        }

        public bool ReadBoolean() => reader.ReadBoolean();

        public byte ReadByte() => reader.ReadByte();

        public byte[] ReadBytes(int count) => reader.ReadBytes(count);

        public sbyte ReadSByte() => reader.ReadSByte();

        public short ReadInt16() => reader.ReadInt16();

        public ushort ReadUInt16() => reader.ReadUInt16();

        public int ReadInt32() => reader.ReadInt32();

        public uint ReadUInt32() => reader.ReadUInt32();

        public long ReadInt64() => reader.ReadInt64();

        public ulong ReadUInt64() => reader.ReadUInt64();

        public float ReadSingle() => reader.ReadSingle();

        public double ReadDouble() => reader.ReadDouble();

        public uint ReadCompressedUInt32() => reader.ReadCompressedUInt32();

        public int ReadCompressedInt32() => reader.ReadCompressedInt32();

        public uint ReadULeb128() => reader.ReadULeb128();

        public void Write(bool value) => writer.Write(value);

        public void Write(byte value) => writer.Write(value);

        public void Write(sbyte value) => writer.Write(value);

        public void Write(short value) => writer.Write(value);

        public void Write(ushort value) => writer.Write(value);

        public void Write(int value) => writer.Write(value);

        public void Write(uint value) => writer.Write(value);

        public void Write(long value) => writer.Write(value);

        public void Write(ulong value) => writer.Write(value);

        public void Write(float value) => writer.Write(value);

        public void Write(double value) => writer.Write(value);

        public ulong Position
        {
            get => (ulong)stream.Position;
            set => stream.Position = (long)value;
        }

        public ulong Length => (ulong)stream.Length;

        private object ReadPrimitive(Type type)
        {
            return type.Name switch
            {
                "Int32" => ReadInt32(),
                "UInt32" => ReadUInt32(),
                "Int16" => ReadInt16(),
                "UInt16" => ReadUInt16(),
                "Byte" => ReadByte(),
                "Int64" => ReadIntPtr(),
                "UInt64" => ReadUIntPtr(),
                _ => throw new NotSupportedException()
            };
        }

        public T ReadClass<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] T>(ulong addr) where T : new()
        {
            Position = addr;
            return ReadClass<T>();
        }

        public T ReadClass<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] T>() where T : new()
        {
            var type = typeof(T);
            if (type.IsPrimitive)
            {
                return (T)ReadPrimitive(type);
            }
            else
            {
                object t = new T();
                foreach (var i in type.GetFields())
                {
                    if (!attributeCache.TryGetValue(i, out var versionAttributes))
                    {
                        if (Attribute.IsDefined(i, typeof(VersionAttribute)))
                        {
                            versionAttributes = i.GetCustomAttributes<VersionAttribute>().ToArray();
                            attributeCache.Add(i, versionAttributes);
                        }
                    }
                    if (versionAttributes?.Length > 0)
                    {
                        var read = false;
                        foreach (var versionAttribute in versionAttributes)
                        {
                            if (Version >= versionAttribute.Min && Version <= versionAttribute.Max)
                            {
                                read = true;
                                break;
                            }
                        }
                        if (!read)
                        {
                            continue;
                        }
                    }
                    var fieldType = i.FieldType;
                    if (fieldType.IsPrimitive)
                    {
                        i.SetValue(t, ReadPrimitive(fieldType));
                    }
                    else if (fieldType.IsEnum)
                    {
                        i.SetValue(t, Enum.ToObject(fieldType, ReadPrimitive(Enum.GetUnderlyingType(fieldType))));
                    }
                    else if (fieldType.IsArray)
                    {
                        var length = i.GetCustomAttribute<ArrayLengthAttribute>().Length;
                        i.SetValue(t, ReadInlineArray(fieldType, length));
                    }
                    else
                    {
                        i.SetValue(t, ReadNestedClass(fieldType));
                    }
                }
                return (T)t;
            }
        }

        // These layouts appear as inline fields of the on-disk structures.
        private object ReadNestedClass(Type type)
        {
            if (type == typeof(Il2CppAssemblyNameDefinition)) return ReadClass<Il2CppAssemblyNameDefinition>();
            if (type == typeof(Il2CppGenericContext)) return ReadClass<Il2CppGenericContext>();
            if (type == typeof(Il2CppGenericMethodIndices)) return ReadClass<Il2CppGenericMethodIndices>();
            if (type == typeof(Il2CppRange)) return ReadClass<Il2CppRange>();
            if (type == typeof(Il2CppRGCTXDefinitionData)) return ReadClass<Il2CppRGCTXDefinitionData>();
            if (type == typeof(Il2CppSectionMetadata)) return ReadClass<Il2CppSectionMetadata>();
            if (type == typeof(NSORelativeExtent)) return ReadClass<NSORelativeExtent>();
            if (type == typeof(NSOSegmentHeader)) return ReadClass<NSOSegmentHeader>();
            throw new NotSupportedException($"Unsupported inline structure: {type}.");
        }

        private Array ReadInlineArray(Type type, int length)
        {
            if (type == typeof(byte[])) return ReadClassArray<byte>(length);
            if (type == typeof(ushort[])) return ReadClassArray<ushort>(length);
            throw new NotSupportedException($"Unsupported inline array: {type}.");
        }

        public T[] ReadClassArray<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] T>(long count) where T : new()
        {
            if (count < 0 || count > int.MaxValue)
            {
                throw new InvalidDataException($"Invalid array element count: {count}.");
            }

            var length = (int)count;
            var t = new T[length];
            for (var i = 0; i < length; i++)
            {
                t[i] = ReadClass<T>();
            }
            return t;
        }

        public T[] ReadClassArray<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] T>(ulong addr, ulong count) where T : new()
        {
            if (count > int.MaxValue)
            {
                throw new InvalidDataException($"Invalid array element count: {count}.");
            }

            return ReadClassArray<T>(addr, (long)count);
        }

        public T[] ReadClassArray<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] T>(ulong addr, long count) where T : new()
        {
            Position = addr;
            return ReadClassArray<T>(count);
        }

        public string ReadStringToNull(ulong addr)
        {
            Position = addr;
            var bytes = new List<byte>();
            byte b;
            while ((b = ReadByte()) != 0)
                bytes.Add(b);
            return Encoding.UTF8.GetString(bytes.ToArray());
        }

        public long ReadIntPtr()
        {
            return Is32Bit ? ReadInt32() : ReadInt64();
        }

        public virtual ulong ReadUIntPtr()
        {
            return Is32Bit ? ReadUInt32() : ReadUInt64();
        }

        public ulong PointerSize
        {
            get => Is32Bit ? 4ul : 8ul;
        }

        public BinaryReader Reader => reader;

        public BinaryWriter Writer => writer;

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                reader.Dispose();
                writer.Dispose();
                stream.Close();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
