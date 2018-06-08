using System;
using System.IO;
using System.Text;

namespace MackLib
{
	/// <summary>
	/// Represents the header of a pack file.
	/// </summary>
	public class PackHeader
	{
		public const int HeaderLength = 512 + 32;

		// 512 B
		public byte[/*4*/] Signature { get; internal set; }
		public int Version { get; set; }
		public uint ClientVersion { get; set; }
		public uint FileCount1 { get; internal set; }
		public DateTime FileTime1 { get; set; }
		public DateTime FileTime2 { get; set; }
		public string/*char[480]*/ BasePath { get; set; }

		// 32 B
		public uint FileCount2 { get; internal set; }
		public uint ListLength { get; internal set; } // incl blank
		public uint BlankLength { get; set; }
		public uint DataLength { get; set; }
		public byte[/*16*/] Zero { get; set; }

		/// <summary>
		/// Returns path to the pack file that this header is part of.
		/// </summary>
		/// <remarks>
		/// Not part of the struct.
		/// </remarks>
		public string PackFilePath { get; private set; }

		/// <summary>
		/// Creates new instance.
		/// </summary>
		public PackHeader()
		{
			this.Signature = new byte[] { (byte)'P', (byte)'A', (byte)'C', (byte)'K' };
			this.Version = 258;
			this.ClientVersion = 1;
			this.Zero = new byte[16];
		}

		/// <summary>
		/// Reads header from reader and returns it.
		/// </summary>
		/// <param name="br"></param>
		/// <param name="packFilePath"></param>
		/// <returns></returns>
		public static PackHeader ReadFrom(BinaryReader br, string packFilePath)
		{
			int len;
			byte[] strBuffer;

			var header = new PackHeader();
			header.PackFilePath = packFilePath;

			header.Signature = br.ReadBytes(4);
			header.Version = br.ReadInt32();
			header.ClientVersion = br.ReadUInt32();
			header.FileCount1 = br.ReadUInt32();
			header.FileTime1 = DateTime.FromFileTimeUtc(br.ReadInt64());
			header.FileTime2 = DateTime.FromFileTimeUtc(br.ReadInt64());

			strBuffer = br.ReadBytes(480);
			len = Array.IndexOf(strBuffer, (byte)0);
			header.BasePath = Encoding.UTF8.GetString(strBuffer, 0, len);

			header.FileCount2 = br.ReadUInt32();
			header.ListLength = br.ReadUInt32();
			header.BlankLength = br.ReadUInt32();
			header.DataLength = br.ReadUInt32();
			header.Zero = br.ReadBytes(16);

			return header;
		}
	}
}
