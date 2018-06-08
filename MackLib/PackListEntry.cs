using ComponentAce.Compression.Libs.zlib;
using MackLib.UclCompression;
using System;
using System.IO;
using System.Text;

namespace MackLib
{
	/// <summary>
	/// Represents entry in pack list.
	/// </summary>
	public class PackListEntry
	{
		private BinaryReader _br;
		private string _tempPath;

		public PackListNameType NameType { get; set; }
		public string FullName { get; set; }
		public uint Seed { get; set; }
		public uint Zero { get; set; }
		public uint DataOffset { get; internal set; }
		public uint CompressedSize { get; internal set; }
		public uint DecompressedSize { get; internal set; }
		public bool IsCompressed { get; set; }
		public DateTime FileTime1 { get; set; }
		public DateTime FileTime2 { get; set; }
		public DateTime FileTime3 { get; set; }
		public DateTime FileTime4 { get; set; }
		public DateTime FileTime5 { get; set; }

		/// <summary>
		/// Time the file was created.
		/// </summary>
		public DateTime CreationTime { get { return this.FileTime1; } }

		/// <summary>
		/// Time the file was accessed last.
		/// </summary>
		public DateTime LastAccessTime { get { return this.FileTime3; } }

		/// <summary>
		/// Time the file was written to last.
		/// </summary>
		public DateTime LastWriteTime { get { return this.FileTime5; } }

		/// <summary>
		/// Returns the name of the file.
		/// </summary>
		/// <remarks>
		/// Not part of the struct.
		/// </remarks>
		public string FileName { get; set; }

		/// <summary>
		/// Returns header of the pack file this entry belongs to.
		/// </summary>
		public PackHeader Header { get; private set; }

		/// <summary>
		/// Creates new list entry.
		/// </summary>
		/// <param name="packFilePath"></param>
		/// <param name="packHeader"></param>
		/// <param name="binaryReader"></param>
		internal PackListEntry(PackHeader packHeader, BinaryReader binaryReader)
		{
			_br = binaryReader;

			this.Seed = 1;
			this.Header = packHeader;
		}

		/// <summary>
		/// Reads pack list entry from reader and returns it.
		/// </summary>
		/// <param name="packFilePath"></param>
		/// <param name="packHeader"></param>
		/// <param name="br"></param>
		/// <returns></returns>
		public static PackListEntry ReadFrom(PackHeader packHeader, BinaryReader br)
		{
			int len;
			byte[] strBuffer;

			var entry = new PackListEntry(packHeader, br);

			entry.NameType = (PackListNameType)br.ReadByte();

			if (entry.NameType <= PackListNameType.L64)
			{
				var size = (0x10 * ((byte)entry.NameType + 1));
				strBuffer = br.ReadBytes(size - 1);
			}
			else if (entry.NameType == PackListNameType.L96)
			{
				var size = 0x60;
				strBuffer = br.ReadBytes(size - 1);
			}
			else if (entry.NameType == PackListNameType.LDyn)
			{
				var size = (int)br.ReadUInt32() + 5;
				strBuffer = br.ReadBytes(size - 1 - 4);
			}
			else
				throw new Exception("Unknown entry name type '" + entry.NameType + "'.");

			len = Array.IndexOf(strBuffer, (byte)0);
			entry.FullName = Encoding.UTF8.GetString(strBuffer, 0, len);
			entry.FileName = Path.GetFileName(entry.FullName);

			entry.Seed = br.ReadUInt32();
			entry.Zero = br.ReadUInt32();
			entry.DataOffset = br.ReadUInt32();
			entry.CompressedSize = br.ReadUInt32();
			entry.DecompressedSize = br.ReadUInt32();
			entry.IsCompressed = (br.ReadUInt32() != 0);
			entry.FileTime1 = DateTime.FromFileTimeUtc(br.ReadInt64());
			entry.FileTime2 = DateTime.FromFileTimeUtc(br.ReadInt64());
			entry.FileTime3 = DateTime.FromFileTimeUtc(br.ReadInt64());
			entry.FileTime4 = DateTime.FromFileTimeUtc(br.ReadInt64());
			entry.FileTime5 = DateTime.FromFileTimeUtc(br.ReadInt64());

			return entry;
		}

		/// <summary>
		/// Extracts file to the given location.
		/// </summary>
		/// <param name="outPath">If this is null, a temp path will be generated.</param>
		/// <returns>The full path the file was extracted to.</returns>
		public string ExtractFile(string outPath = null)
		{
			if (outPath == null)
			{
				if (_tempPath == null || File.Exists(_tempPath))
					_tempPath = Path.GetTempFileName() + Path.GetExtension(this.FileName);

				outPath = _tempPath;
			}

			using (var fsOut = new FileStream(outPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None))
				this.WriteData(fsOut);

			return outPath;
		}

		/// <summary>
		/// Returns raw decompressed file data.
		/// </summary>
		/// <returns></returns>
		public byte[] GetData()
		{
			using (var ms = new MemoryStream())
			{
				this.WriteData(ms);
				return ms.ToArray();
			}
		}

		/// <summary>
		/// Returns raw decompressed data as memory stream.
		/// </summary>
		/// <returns>Stream with the data, has to be closed by the user.</returns>
		public MemoryStream GetDataAsStream()
		{
			return new MemoryStream(this.GetData());
		}

		/// <summary>
		/// Extracts the file to the temp folder and returns a file stream for it.
		/// </summary>
		/// <returns>Stream with the data, has to be closed by the user.</returns>
		public FileStream GetDataAsFileStream()
		{
			return new FileStream(this.ExtractFile(), FileMode.Open, FileAccess.Read, FileShare.Read);
		}

		/// <summary>
		/// Writes decompressed data into given stream.
		/// </summary>
		/// <param name="stream"></param>
		public void WriteData(Stream stream)
		{
			var start = PackHeader.HeaderLength + this.Header.ListLength;
			// sizeof(PackageHeader) + sizeof(PackageListHeader) + headerLength

			byte[] buffer;
			lock (_br)
			{
				_br.BaseStream.Seek(start + this.DataOffset, SeekOrigin.Begin);
				buffer = _br.ReadBytes((int)this.CompressedSize);
			}

			if (this.IsCompressed)
			{
				this.Decode(ref buffer);
				this.Decompress(buffer, stream);
			}
			else
			{
				using (var ms = new MemoryStream(buffer))
					ms.CopyTo(stream);
			}
		}

		/// <summary>
		/// Decodes buffer.
		/// </summary>
		/// <param name="buffer"></param>
		/// <param name="seed"></param>
		private void Decode(ref byte[] buffer)
		{
			// KR beta (v1) didn't have encoding yet
			if (this.Header.Version == 1)
				return;

			var mt = new MTRandom((this.Seed << 7) ^ 0xA9C36DE1);

			for (var i = 0; i < buffer.Length; ++i)
				buffer[i] = (byte)(buffer[i] ^ mt.GetUInt32());
		}

		/// <summary>
		/// Decompresses buffer into stream.
		/// </summary>
		/// <param name="buffer"></param>
		/// <param name="outStream"></param>
		private void Decompress(byte[] buffer, Stream outStream)
		{
			// Use zlib for modern packs and UCL for KR beta (v1)
			if (this.Header.Version > 1)
			{
				using (var zlib = new ZOutputStream(outStream))
					zlib.Write(buffer, 0, buffer.Length);
			}
			else
			{
				var uncompressed = Ucl.Decompress_NRV2E(buffer, this.DecompressedSize);

				using (var ms = new MemoryStream(uncompressed))
					ms.CopyTo(outStream);
			}
		}
	}
}
