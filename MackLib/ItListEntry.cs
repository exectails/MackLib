using System;
using System.IO;
using System.Linq;
using System.Text;

namespace MackLib
{
	/// <summary>
	/// Represents an entry in the list of files inside an IT container.
	/// </summary>
	public class ItListEntry
	{
		// struct
#pragma warning disable CS1591 // missing XML comments
		public string FilePath { get; set; }
		public int Checksum { get; set; }
		public ItFileFlag Flags { get; set; }
		public int Offset { get; set; }
		public int Size { get; set; }
		public int CompressedSize { get; set; }
		public byte[] Key { get; set; }
#pragma warning restore CS1591

		/// <summary>
		/// Returns the name of the entry's file, based on its path.
		/// </summary>
		public string FileName => Path.GetFileName(this.FilePath);

		/// <summary>
		/// Returns the IT container this entry belongs to.
		/// </summary>
		public ItFile ItFile { get; private set; }

		/// <summary>
		/// Reads entry from binary reader.
		/// </summary>
		/// <param name="itFile"></param>
		/// <param name="br"></param>
		/// <returns></returns>
		public static ItListEntry ReadFrom(ItFile itFile, BinaryReader br)
		{
			var result = new ItListEntry();
			result.ItFile = itFile;

			var nameLen = br.ReadInt32();
			result.FilePath = Encoding.Unicode.GetString(br.ReadBytes(nameLen * 2));

			result.Checksum = br.ReadInt32();
			result.Flags = (ItFileFlag)br.ReadInt32();
			result.Offset = br.ReadInt32();
			result.Size = br.ReadInt32();
			result.CompressedSize = br.ReadInt32();
			result.Key = br.ReadBytes(16);

			var keysSum = result.Key.Sum(a => a);
			var valid = (int)result.Flags + result.Offset + result.Size + result.CompressedSize + keysSum == result.Checksum;

			if (!valid)
				throw new InvalidDataException("Invalid data, checksum test failed.");

			return result;
		}
	}
}
