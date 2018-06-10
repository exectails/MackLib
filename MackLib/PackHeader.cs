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
		/// <summary>
		/// The size of the header in bytes.
		/// </summary>
		public const int HeaderLength = 512 + 32;

		// 512 B
#pragma warning disable CS1591 // missing XML comments
		public byte[/*4*/] Signature { get; internal set; }
		public int FormatVersion { get; set; }
		public int PackVersion { get; set; }
		public int FileCount { get; internal set; }
		public DateTime FileTime1 { get; set; }
		public DateTime FileTime2 { get; set; }
		public string/*char[480]*/ BasePath { get; set; }

		// 32 B
		public int ListFileCount { get; internal set; }
		public int ListLength { get; internal set; } // includes blank
		public int BlankLength { get; set; }
		public int DataLength { get; set; }
		public byte[/*16*/] Zero { get; set; }
#pragma warning restore CS1591

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
			this.FormatVersion = 258;
			this.PackVersion = 1;
			this.FileTime1 = DateTime.Now;
			this.FileTime2 = DateTime.Now;
			this.BasePath = @"data\";
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
			header.FormatVersion = br.ReadInt32();
			header.PackVersion = br.ReadInt32();
			header.FileCount = br.ReadInt32();
			header.FileTime1 = DateTime.FromFileTimeUtc(br.ReadInt64());
			header.FileTime2 = DateTime.FromFileTimeUtc(br.ReadInt64());

			strBuffer = br.ReadBytes(480);
			len = Array.IndexOf(strBuffer, (byte)0);
			header.BasePath = Encoding.UTF8.GetString(strBuffer, 0, len);

			header.ListFileCount = br.ReadInt32();
			header.ListLength = br.ReadInt32();
			header.BlankLength = br.ReadInt32();
			header.DataLength = br.ReadInt32();
			header.Zero = br.ReadBytes(16);

			return header;
		}
	}
}
