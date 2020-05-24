using ComponentAce.Compression.Libs.zlib;
using MackLib.UclCompression;
using System;
using System.IO;
using System.Text;

namespace MackLib
{
	/// <summary>
	/// Represents an entry in a pack's file list.
	/// </summary>
	public interface IPackListEntry
	{
		// struct
#pragma warning disable CS1591 // missing XML comments
		PackListNameType NameType { get; set; }
		string RelativePath { get; set; }
		string FileName { get; }
		uint Seed { get; set; } // File Version? Seems to be responsible for deciding which file to use.
		uint Zero { get; set; }
		uint FileSize { get; }
		bool IsCompressed { get; set; }
		DateTime FileTime1 { get; set; }
		DateTime FileTime2 { get; set; }
		DateTime FileTime3 { get; set; }
		DateTime FileTime4 { get; set; }
		DateTime FileTime5 { get; set; }
#pragma warning restore CS1591

		/// <summary>
		/// Returns the file's raw, potentially compressed and encoded data.
		/// </summary>
		/// <returns></returns>
		byte[] GetRawData();

		/// <summary>
		/// Returns the file's uncompressed data.
		/// </summary>
		/// <returns></returns>
		byte[] GetData();

		/// <summary>
		/// Returns a file stream to the uncompressed file. The file might
		/// get extracted to the temp folder to enable this.
		/// </summary>
		/// <returns></returns>
		FileStream GetDataAsFileStream();
	}

	/// <summary>
	/// Represents an entry in a pack's file list, based on an actual
	/// file. On save this is written to the pack and turns into a
	/// PackedFileEntry on loading it.
	/// </summary>
	public class FileEntry : IPackListEntry
	{
#pragma warning disable CS1591 // missing XML comments
		public PackListNameType NameType { get; set; } = PackListNameType.LDyn;
		public string RelativePath { get; set; }
		public string FileName { get; set; }
		public uint Seed { get; set; } = 1;
		public uint Zero { get; set; }
		public uint FileSize { get; }
		public bool IsCompressed { get; set; } = true;
		public DateTime FileTime1 { get; set; } = DateTime.Now;
		public DateTime FileTime2 { get; set; } = DateTime.Now;
		public DateTime FileTime3 { get; set; } = DateTime.Now;
		public DateTime FileTime4 { get; set; } = DateTime.Now;
		public DateTime FileTime5 { get; set; } = DateTime.Now;
#pragma warning restore CS1591

		/// <summary>
		/// Returns the path to the file.
		/// </summary>
		public string FilePath { get; }

		/// <summary>
		/// Creates new instance.
		/// </summary>
		/// <param name="filePath">Path to the file.</param>
		/// <param name="relativePath">Relative path to the file inside the pack file.</param>
		public FileEntry(string filePath, string relativePath)
		{
			var fileInfo = new FileInfo(filePath);

			this.FilePath = filePath;
			this.RelativePath = relativePath;
			this.FileName = Path.GetFileName(relativePath);
			this.FileSize = (uint)fileInfo.Length;
			this.FileTime1 = fileInfo.CreationTime;
			this.FileTime3 = fileInfo.LastAccessTime;
			this.FileTime5 = fileInfo.LastWriteTime;
		}

		/// <summary>
		/// Returns the file's data.
		/// </summary>
		/// <returns></returns>
		public byte[] GetRawData()
		{
			return File.ReadAllBytes(this.FilePath);
		}

		/// <summary>
		/// Returns the file's data.
		/// </summary>
		/// <returns></returns>
		public byte[] GetData()
		{
			return this.GetRawData();
		}

		/// <summary>
		/// Returns file stream for the file, needs to be closed by
		/// the caller.
		/// </summary>
		/// <returns></returns>
		public FileStream GetDataAsFileStream()
		{
			return new FileStream(this.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
		}
	}

	/// <summary>
	/// Represents a file list entry from inside a pack file.
	/// </summary>
	public class PackedFileEntry : IPackListEntry
	{
		private BinaryReader _br;
		private string _tempPath;

#pragma warning disable CS1591 // missing XML comments
		public PackListNameType NameType { get; set; }
		public string RelativePath { get; set; }
		public uint Seed { get; set; }
		public uint Zero { get; set; }
		public uint DataOffset { get; internal set; }
		public uint CompressedSize { get; internal set; }
		public uint FileSize { get; internal set; }
		public bool IsCompressed { get; set; }
		public DateTime FileTime1 { get; set; }
		public DateTime FileTime2 { get; set; }
		public DateTime FileTime3 { get; set; }
		public DateTime FileTime4 { get; set; }
		public DateTime FileTime5 { get; set; }
#pragma warning restore CS1591

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
		public string FileName { get; private set; }

		/// <summary>
		/// Returns the full path to the file, incl. base path.
		/// </summary>
		/// <remarks>
		/// Not part of the struct.
		/// </remarks>
		public string FullName => (this.Header.BasePath + this.RelativePath);

		/// <summary>
		/// Returns header of the pack file this entry belongs to.
		/// </summary>
		public PackHeader Header { get; private set; }

		/// <summary>
		/// Creates new list entry.
		/// </summary>
		/// <param name="packHeader"></param>
		/// <param name="binaryReader"></param>
		internal PackedFileEntry(PackHeader packHeader, BinaryReader binaryReader)
		{
			_br = binaryReader;

			this.Seed = 1;
			this.Header = packHeader;
		}

		/// <summary>
		/// Reads pack list entry from reader and returns it.
		/// </summary>
		/// <param name="packHeader"></param>
		/// <param name="br"></param>
		/// <returns></returns>
		public static PackedFileEntry ReadFrom(PackHeader packHeader, BinaryReader br)
		{
			int len;
			byte[] strBuffer;

			var entry = new PackedFileEntry(packHeader, br);

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
				var size = (int)br.ReadUInt32();
				strBuffer = br.ReadBytes(size);
			}
			else
				throw new Exception("Unknown entry name type '" + entry.NameType + "'.");

			len = Array.IndexOf(strBuffer, (byte)0);
			entry.RelativePath = Encoding.UTF8.GetString(strBuffer, 0, len);

			entry.Seed = br.ReadUInt32();
			entry.Zero = br.ReadUInt32();
			entry.DataOffset = br.ReadUInt32();
			entry.CompressedSize = br.ReadUInt32();
			entry.FileSize = br.ReadUInt32();
			entry.IsCompressed = (br.ReadUInt32() != 0); // compression strength?
			entry.FileTime1 = DateTime.FromFileTimeUtc(br.ReadInt64());
			entry.FileTime2 = DateTime.FromFileTimeUtc(br.ReadInt64());
			entry.FileTime3 = DateTime.FromFileTimeUtc(br.ReadInt64());
			entry.FileTime4 = DateTime.FromFileTimeUtc(br.ReadInt64());
			entry.FileTime5 = DateTime.FromFileTimeUtc(br.ReadInt64());

			entry.FileName = Path.GetFileName(entry.RelativePath);

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
				if (_tempPath == null)
					_tempPath = Path.GetTempFileName();

				outPath = _tempPath;
			}

			using (var fs = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.None))
				this.WriteData(fs);

			return outPath;
		}

		/// <summary>
		/// Returns the entry's raw, potentially compressed and encoded data.
		/// </summary>
		/// <returns></returns>
		public byte[] GetRawData()
		{
			var dataListOffset = PackHeader.HeaderLength + this.Header.ListLength;

			byte[] data;
			lock (_br)
			{
				_br.BaseStream.Seek(dataListOffset + this.DataOffset, SeekOrigin.Begin);
				data = _br.ReadBytes((int)this.CompressedSize);
			}

			return data;
		}

		/// <summary>
		/// Returns entry's uncompressed data.
		/// </summary>
		/// <returns></returns>
		public byte[] GetData()
		{
			var data = this.GetRawData();

			if (!this.IsCompressed)
				return data;

			this.Decode(ref data);

			using (var ms = new MemoryStream())
			{
				this.Decompress(data, ms);
				return ms.ToArray();
			}
		}

		/// <summary>
		/// Extracts the file in memory and returns a stream to read it.
		/// </summary>
		/// <returns></returns>
		public Stream GetDataAsStream()
		{
			var data = this.GetData();
			return new MemoryStream(data);
		}

		/// <summary>
		/// Extracts the uncompressed file to the temp folder and returns
		/// a file stream for it.
		/// </summary>
		/// <returns>Stream with the data, has to be closed by the caller.</returns>
		public FileStream GetDataAsFileStream()
		{
			return new FileStream(this.ExtractFile(), FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.DeleteOnClose);
		}

		/// <summary>
		/// Writes decompressed data into given stream.
		/// </summary>
		/// <param name="stream"></param>
		public void WriteData(Stream stream)
		{
			var data = this.GetData();
			stream.Write(data, 0, data.Length);
		}

		/// <summary>
		/// Decodes buffer.
		/// </summary>
		/// <param name="buffer"></param>
		private void Decode(ref byte[] buffer)
		{
			// KR beta (v1) didn't have encoding yet
			if (this.Header.FormatVersion == 1)
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
			// Use UCL for old KR packs
			// v1: KR72 (Beta Client)
			// v257 from 2006: KR281
			if (this.Header.FormatVersion == 1 || (this.Header.FormatVersion == 257 && this.Header.FileTime1.Year == 2006))
			{
				var uncompressed = Ucl.Decompress_NRV2E(buffer, this.FileSize);

				using (var ms = new MemoryStream(uncompressed))
					ms.CopyTo(outStream);
			}
			else
			{
				using (var zlib = new ZOutputStream(outStream))
					zlib.Write(buffer, 0, buffer.Length);
			}
		}
	}
}
