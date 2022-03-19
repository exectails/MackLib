using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Ionic.Zlib;
using MackLib.Encryption.Snow;

namespace MackLib
{
	/// <summary>
	/// Represents an IT container file.
	/// </summary>
	public class ItFile : IDisposable
	{
		private const string Salt = "@6QeTuOaDgJlZcBm#9";
		private const int FileHeadLength = 1024;

		private readonly object _syncLock = new object();

		private FileStream _fs;
		private BinaryReader _br;

		private string _saltyName;
		private long _filesOffset;

		private ItHeader _header;
		private readonly List<ItListEntry> _entries = new List<ItListEntry>();
		private readonly Dictionary<string, ItListEntry> _entriesPath = new Dictionary<string, ItListEntry>();

		/// <summary>
		/// Returns the path to the IT file.
		/// </summary>
		public string FilePath { get; private set; }

		/// <summary>
		/// Returns the IT file's name.
		/// </summary>
		public string FileName { get; private set; }

		/// <summary>
		/// Returns the number of files in this container.
		/// </summary>
		public int FileCount => _entries.Count;

		/// <summary>
		/// Creates new IT file reader from file path.
		/// </summary>
		/// <param name="filePath"></param>
		public ItFile(string filePath)
		{
			this.Load(filePath);
		}

		/// <summary>
		/// Returns true if a file with the given name exists.
		/// (Ignores casing.)
		/// </summary>
		/// <param name="fileName"></param>
		/// <returns></returns>
		public bool FileExists(string fileName)
			=> _entries.Any(a => string.Compare(a.FileName, fileName, true) == 0);

		/// <summary>
		/// Returns a list of all entries in this container.
		/// </summary>
		/// <returns></returns>
		public ItListEntry[] GetEntries()
			=> _entries.ToArray();

		/// <summary>
		/// Returns the given file by its path. Returns null if the file
		/// wasn't found.
		/// </summary>
		/// <param name="filePath"></param>
		/// <returns></returns>
		public ItListEntry Find(string filePath)
		{
			filePath = filePath.ToLowerInvariant();

			if (_entriesPath.TryGetValue(filePath, out var entry))
				return entry;

			return null;
		}

		/// <summary>
		/// Returns file by its path via out, returns whether the file
		/// was found.
		/// </summary>
		/// <param name="filePath"></param>
		/// <param name="entry"></param>
		/// <returns></returns>
		public bool TryFind(string filePath, out ItListEntry entry)
		{
			entry = this.Find(filePath);
			return entry != null;
		}

		/// <summary>
		/// Loads given IT file.
		/// </summary>
		/// <param name="filePath"></param>
		private void Load(string filePath)
		{
			this.FilePath = filePath;
			this.FileName = Path.GetFileName(filePath);

			_saltyName = this.FileName + Salt;

			_fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
			_br = new BinaryReader(_fs, Encoding.ASCII);

			var headerKey = GetHeaderKey(_saltyName);
			var entriesKey = GetEntriesKey(_saltyName);
			var headerOffset = GetHeaderOffset(this.FileName);
			var entriesOffset = GetEntriesOffset(this.FileName);

			var headerSnow = new Snow2(headerKey, 16);
			var entriesSnow = new Snow2(entriesKey, 16);

			// Read header

			using (var itStream = new ItStream(_fs, headerSnow))
			using (var br = new BinaryReader(itStream))
			{
				itStream.Seek(headerOffset, SeekOrigin.Begin);
				_header = ItHeader.ReadFrom(br);
			}

			// Read file list

			using (var itStream = new ItStream(_fs, entriesSnow))
			using (var br = new BinaryReader(itStream))
			{
				itStream.Seek(headerOffset + entriesOffset, SeekOrigin.Begin);

				for (var i = 0; i < _header.FileCount; ++i)
				{
					var entry = ItListEntry.ReadFrom(this, br);
					var fileNameSanitized = entry.FilePath.ToLowerInvariant().Replace("\\", "/");

					_entries.Add(entry);
					_entriesPath.Add(fileNameSanitized, entry);
				}
			}

			// Save files offset, which is relative to the position after
			// the file list
			_filesOffset = (_fs.Position + 1023) / 1024 * 1024;
		}

		/// <summary>
		/// Reads the given file from this container and returns its data.
		/// </summary>
		/// <param name="filePath"></param>
		/// <returns></returns>
		public byte[] GetFile(string filePath)
		{
			var entry = this.Find(filePath);
			return this.GetFile(entry);
		}

		/// <summary>
		/// Reads file from this container based on the given entry and
		/// returns its data.
		/// </summary>
		/// <param name="entry"></param>
		/// <returns></returns>
		public byte[] GetFile(ItListEntry entry)
		{
			var fileOffset = _filesOffset + entry.Offset * 1024;
			_fs.Seek(fileOffset, SeekOrigin.Begin);

			var size = entry.CompressedSize;
			//size = (size + 1023) / 1024 * 1024;
			//size = entry.CompressedSize;

			var content = new byte[size];

			var fileKey = GetFileKey(entry);
			var fileSnow1 = new Snow2(fileKey, 16);
			var fileSnow2 = fileSnow1.Clone();

			if ((entry.Flags & ItFileFlag.Encrypted) != 0)
			{
				using (var itStream = new ItStream(_fs, fileSnow1))
				using (var br = new BinaryReader(itStream))
					br.Read(content, 0, content.Length);
			}
			else
			{
				using (var br = new BinaryReader(_fs))
					br.Read(content, 0, content.Length);
			}

			if ((entry.Flags & ItFileFlag.HeadEncrypted) != 0)
			{
				using (var ms = new MemoryStream(content))
				using (var itStream = new ItStream(ms, fileSnow2))
				using (var br = new BinaryReader(itStream))
				{
					var len = Math.Min(content.Length, FileHeadLength);

					var head = br.ReadBytes(len);
					Buffer.BlockCopy(head, 0, content, 0, len);
				}
			}

			if ((entry.Flags & ItFileFlag.Compressed) != 0)
			{
				using (var ms = new MemoryStream(content))
				using (var zlib = new ZlibStream(ms, CompressionMode.Decompress))
					content = ZlibBaseStream.UncompressBuffer(content, zlib);
			}

			return content;
		}

		/// <summary>
		/// Closes all file streams.
		/// </summary>
		public void Close()
		{
			this.Dispose();
		}

		/// <summary>
		/// Closes all file streams.
		/// </summary>
		public void Dispose()
		{
			lock (_syncLock)
			{
				try { _br.Close(); } catch { }
				try { _fs.Close(); } catch { }
			}
		}

		/// <summary>
		/// Returns key for decrypting file header.
		/// </summary>
		/// <param name="val"></param>
		/// <returns></returns>
		private static byte[] GetHeaderKey(string val)
		{
			var bytes = Encoding.UTF8.GetBytes(val);
			var result = new byte[16];

			for (var i = 0; i < 16; ++i)
				result[i] = (byte)(bytes[i % bytes.Length] + i);

			return result;
		}

		/// <summary>
		/// Returns key for decrypting list entries.
		/// </summary>
		/// <param name="val"></param>
		/// <returns></returns>
		private static byte[] GetEntriesKey(string val)
		{
			var bytes = Encoding.UTF8.GetBytes(val);
			var result = new byte[16];

			for (var i = 0; i < 16; ++i)
				result[i] = (byte)(i + (i % 3 + 2) * bytes[bytes.Length - i % bytes.Length - 1]);

			return result;
		}

		/// <summary>
		/// Returns key for decrypting entry's file.
		/// </summary>
		/// <param name="entry"></param>
		/// <returns></returns>
		private static byte[] GetFileKey(ItListEntry entry)
		{
			var entryKey = entry.Key;

			var bytes = Encoding.UTF8.GetBytes(entry.FilePath);
			var result = new byte[16];

			for (byte i = 0; i < result.Length; ++i)
				result[i] = (byte)(bytes[i % bytes.Length] * (entryKey[i % 16] - (i / 5 * 5) + 2 + i) + i);

			return result;
		}

		/// <summary>
		/// Returns offset of the header.
		/// </summary>
		/// <param name="val"></param>
		/// <returns></returns>
		private static int GetHeaderOffset(string val)
		{
			var bytes = Encoding.UTF8.GetBytes(val);
			var result = 0;

			for (var i = 0; i < bytes.Length; ++i)
				result += bytes[i];

			return result % 312 + 30;
		}

		/// <summary>
		/// Returns offset of the entries.
		/// </summary>
		/// <param name="val"></param>
		/// <returns></returns>
		private static int GetEntriesOffset(string val)
		{
			var bytes = Encoding.UTF8.GetBytes(val);
			var result = 0;

			for (var i = 0; i < bytes.Length; ++i)
				result += bytes[i] * 3;

			return result % 212 + 42;
		}
	}
}
