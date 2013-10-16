using System;
using System.IO;
using System.Collections.Generic;
using System.Collections;
using System.Text;

/// <summary>
/// Dex.NET - Mario Kosmiskas
/// 
/// Provided under the Apache 2.0 License: http://www.apache.org/licenses/LICENSE-2.0
/// Commercial use requires attribution
/// </summary>
namespace dex.net
{
	/// <summary>
	/// Dex
	/// </summary>
	/// <exception cref='ArgumentException'>
	/// Is thrown when an argument passed to a method is invalid.
	/// </exception>
	/// <exception cref='OverflowException'>
	/// Is thrown when the result of an arithmetic operation is too large to be represented by the destination type.
	/// </exception>
	public class Dex : IDisposable
	{
		private Dictionary<TypeCode,MapItem> SectionsMap;
		internal Stream DexStream;
		internal DexHeader DexHeader;
		internal BinaryReader DexReader;

		public Dex (Stream dexStream)
		{
			if (!dexStream.CanSeek) {
				throw new ArgumentException ("Must be able to seek the DEX stream");
			}

			DexStream = dexStream;
			DexHeader = DexHeader.Parse(DexStream);
			DexReader = new BinaryReader (dexStream);
			SectionsMap = ReadSectionsMap();
		}

		#region IDisposable implementation

		public void Dispose ()
		{
			DexStream.Close();
			SectionsMap.Clear();
			DexHeader = null;
		}

		#endregion
		
		/// <summary>
		/// Return a String given an index
		/// </summary>
		/// <returns>
		/// The string
		/// </returns>
		/// <param name='index'>
		/// Index in the Strings table
		/// </param>
		public string GetString (uint index)
		{
			// Sanity check
			if (index >= DexHeader.StringIdsCount)
				throw new ArgumentException(String.Format("String id {0} outside the range {1}", index, DexHeader.StringIdsCount));

			// Find the offset of the String ID in the Strings table 
			// and position the stream at the entry
			var offset = DexHeader.StringIdsOffset + (index*4);
			DexStream.Seek(offset, SeekOrigin.Begin);

			return ReadString(DexReader.ReadUInt32());
		}

		/// <summary>
		/// Iterate over all Strings
		/// </summary>
		/// <returns>
		/// All Strings in the DEX
		/// </returns>
		public IEnumerable<string> GetStrings ()
		{
			for (uint i=0; i<DexHeader.StringIdsCount; i++) {
				yield return GetString (i);
			}
		}

		/// <summary>
		/// Read a string at the specified offset in the DEX file. The layout of a String is:
		/// 	length: length of the decoded UTF16 string
		/// 	data: string encoded in MUTF8 ending in a \0
		/// </summary>
		/// <returns>
		/// The string
		/// </returns>
		/// <param name='offset'>
		/// Offset of the string from the beginning of the file
		/// </param>
		internal string ReadString (uint offset)
		{
			DexStream.Seek(offset, SeekOrigin.Begin);

			// find out the length of the decoded string
			var stringLength = (int)Leb128.ReadUleb(DexReader);

			// strings are encoded in MUTF-8 format
			char[] chararr = new char[stringLength];
			int c, char2, char3;
			int chararr_count=0;

			while (chararr_count < stringLength) {
				c = DexReader.ReadByte();

				switch (c >> 4) {
					/* 0xxxxxxx */
					case 0: 
					case 1: 
					case 2: 
					case 3: 
					case 4: 
					case 5: 
					case 6: 
					case 7:
						chararr[chararr_count++]=(char)c;
						break;

					/* 110x xxxx || 10xx xxxx */
					case 12: 
					case 13:
						char2 = DexReader.ReadByte();
						if ((char2 & 0xC0) != 0x80) {
							throw new InvalidDataException("MUTF-8 parsing error. 2nd byte must be 10xxxxxx. Dex offset:" + (DexStream.Position-2));
						}
						chararr[chararr_count++]=(char)(((c & 0x1F) << 6) | (char2 & 0x3F));
						break;

					/* 1110 xxxx || 10xx xxxx || 10xx xxxx */
					case 14:
						char2 = DexReader.ReadByte();
						char3 = DexReader.ReadByte();
						if (((char2 & 0xC0) ^ (char3 & 0xC0)) != 0) {
							throw new InvalidDataException("MUTF-8 parsing error. Both bytes must be 10xxxxxx. Dex offset:" + (DexStream.Position-3));
						}
						chararr[chararr_count++]=(char)(((c     & 0x0F) << 12) |
						                                ((char2 & 0x3F) << 6)  |
						                                ((char3 & 0x3F) << 0));
						break;

					default:
						throw new InvalidDataException("Invalid MUTF-8 encoding. Dex offset: " + (DexStream.Position-1));
				}
			}

			// The number of chars produced may be less than utflen
			return new string(chararr, 0, chararr_count);
		}


		private Dictionary<TypeCode,MapItem> ReadSectionsMap ()
		{
			// Position the stream at the beginning of the map
			DexStream.Seek (DexHeader.MapOffset, SeekOrigin.Begin);

			// Number of entries in the map
			var count = DexReader.ReadUInt32();

			// Read all entries from the DEX and add to the dictionary
			var map = new Dictionary<TypeCode,MapItem> ();
			for (int i=0; i<count; i++) {
				var itemType = (TypeCode)DexReader.ReadUInt16();
				// Skip the unused field
				DexReader.ReadUInt16();

				MapItem item = new MapItem();
				item.Count = DexReader.ReadUInt32();
				item.Offset = DexReader.ReadUInt32();

				map.Add(itemType, item);
			}

			return map;
		}

		public Prototype GetPrototype (uint id)
		{
			if (id >= DexHeader.PrototypeIdsCount)
				throw new ArgumentException (string.Format ("Prototype Id {0} out of range 0-{1}", id, DexHeader.PrototypeIdsCount));

			DexStream.Position = DexHeader.PrototypeIdsOffset + (id * 12);

			return new Prototype (this, DexReader);
		}

		internal List<ushort> ReadTypeList (uint offset)
		{
			// Offset 0 means the type list is empty
			if (offset == 0) {
				return new List<ushort>();
			}

			DexStream.Seek (offset, SeekOrigin.Begin);

			var count = DexReader.ReadUInt32();
			var types = new List<ushort> ((int)count);

			while (count-- > 0) {
				types.Add(DexReader.ReadUInt16());
			}

			return types;
		}

		public IEnumerable<Prototype> GetPrototypes ()
		{
			for (uint i=0; i<DexHeader.PrototypeIdsCount; i++) {
				yield return GetPrototype (i);
			}
		}

		public Field GetField (uint id)
		{
			if (id > DexHeader.FieldIdsCount)
				throw new ArgumentException (string.Format ("Field Id {0} out of range 0-{1}", id, DexHeader.FieldIdsCount));

			DexStream.Position = DexHeader.FieldIdsOffset + (id * 8);

			return new Field (id, this, DexReader);
		}

		public IEnumerable<Field> GetFields ()
		{
			for (uint i=0; i<DexHeader.FieldIdsCount; i++) {
				yield return GetField (i);
			}
		}

		public Method GetMethod (uint id, uint codeOffset = 0)
		{
			if (id >= DexHeader.MethodIdsCount)
				throw new ArgumentException (string.Format ("Method Id {0} out of range 0-{1}", id, DexHeader.MethodIdsCount));

			DexStream.Seek (DexHeader.MethodIdsOffset + (id * 8), SeekOrigin.Begin);

			return new Method (id, this, DexReader, codeOffset);
		}

		public IEnumerable<Method> GetMethods ()
		{
			for (uint i=0; i<DexHeader.MethodIdsCount; i++) {
				yield return GetMethod (i);
			}
		}

		public int ClassCount
		{
			get { return (int)DexHeader.ClassDefinitionsCount; }
		}

		public Class GetClass (uint id)
		{
			if (id >= DexHeader.ClassDefinitionsCount)
				throw new ArgumentException (string.Format ("Class Id {0} out of range 0-{1}", id, DexHeader.ClassDefinitionsCount-1));

			DexStream.Seek (DexHeader.ClassDefinitionsOffset + (id * 32), SeekOrigin.Begin);

			return new Class (this, DexReader);
		}

		public IEnumerable<Class> GetClasses ()
		{
			for (uint i=0; i<DexHeader.ClassDefinitionsCount; i++) {
				yield return GetClass (i);
			}
		}

		internal OpCode Decode (ref long offset) {
			DexStream.Position = offset;

			var opcode = Disassembler.Decode(DexReader);
			opcode.OpCodeOffset = offset;

			offset = DexStream.Position;

			return opcode;
		}

		public string GetTypeName (uint id)
		{
			if (id >= DexHeader.TypeIdsCount)
				throw new ArgumentException(string.Format("Type Id {0} out of range 0-{1}", id, DexHeader.TypeIdsCount));

			DexStream.Seek (DexHeader.TypeIdsOffset + (id*4), SeekOrigin.Begin);

			return TypeToString(GetString(DexReader.ReadUInt32()));
		}

		public IEnumerable<string> GetTypeNames ()
		{
			for (uint i=0; i<DexHeader.TypeIdsCount; i++) {
				yield return GetTypeName (i);
			}
		}

		internal string TypeToString (string typeDescriptor)
		{
			if (String.IsNullOrWhiteSpace(typeDescriptor))
				return "";

			switch (typeDescriptor [0]) {
				case 'V':
				return "void";

				case 'Z':
				return "boolean";

				case 'B':
				return "byte";

				case 'S':
				return "short";

				case 'C':
				return "char";

				case 'I':
				return "int";

				case 'J':
				return "long";

				case 'F':
				return "float";

				case 'D':
				return "double";

				case 'L':
				return typeDescriptor.Replace('/', '.').Substring(1, typeDescriptor.Length-2);

				case '[':
				return TypeToString(typeDescriptor.Substring(1)) + "[]";

				default:
				return "unknown";
			}
		}
	}

	struct MapItem
	{
		// Number of items at the offset
		internal uint Count;

		// Offset from the start of the file
		internal uint Offset;

		public override string ToString ()
		{
			return string.Format("Offset:{0} Count:{1}", Offset, Count);
		}
	}
}