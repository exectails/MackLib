using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ComponentAce.Compression.Libs.zlib;

namespace MackLib
{
	/// <summary>
	/// Represents a pack file.
	/// </summary>
	public class PackFile : IDisposable
	{
		private readonly object _syncLock = new object();

		private Dictionary<string, IPackListEntry> _entries = new Dictionary<string, IPackListEntry>();
		private Dictionary<string, List<IPackListEntry>> _entriesNamed = new Dictionary<string, List<IPackListEntry>>();
		private Stream _fs;
		private BinaryReader _br;

		/// <summary>
		/// File path that was used to create this reader.
		/// </summary>
		public string FilePath { get; private set; }

		/// <summary>
		/// Returns the pack file's header.
		/// </summary>
		public PackHeader Header { get; private set; }

		/// <summary>
		/// Amount of entries in this pack file.
		/// </summary>
		public int Count { get { lock (_syncLock) return _entries.Count; } }

		/// <summary>
		/// Creates a new pack file.
		/// </summary>
		public PackFile()
		{
			this.Header = new PackHeader();
		}

		/// <summary>
		/// Creates new pack reader for given file.
		/// </summary>
		/// <param name="filePath"></param>
		public PackFile(string filePath)
		{
			if (!File.Exists(filePath))
				throw new ArgumentException("File not found.");

			this.Load(filePath);
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
				try { _br.Close(); }
				catch { }

				try { _fs.Close(); }
				catch { }
			}
		}

		/// <summary>
		/// Adds entry to pack.
		/// </summary>
		/// <param name="entry"></param>
		public void AddEntry(IPackListEntry entry)
		{
			var fullPath = (this.Header.BasePath + entry.RelativePath).ToLower();
			var fileName = Path.GetFileName(fullPath).ToLower();

			lock (_syncLock)
			{
				_entries[fullPath] = entry;

				if (!_entriesNamed.ContainsKey(fileName))
					_entriesNamed[fileName] = new List<IPackListEntry>();
				_entriesNamed[fileName].Add(entry);
			}
		}

		/// <summary>
		/// Adds file to pack.
		/// </summary>
		/// <param name="filePath"></param>
		/// <param name="relativePath"></param>
		public void AddFile(string filePath, string relativePath)
		{
			this.AddEntry(new FileEntry(filePath, relativePath));
		}

		/// <summary>
		/// Adds all files in folder to pack.
		/// </summary>
		/// <param name="path"></param>
		/// <exception cref="ArgumentException">
		/// Thrown if folder doesn't exist.
		/// </exception>
		public void AddFolder(string path)
		{
			if (!Directory.Exists(path))
				throw new ArgumentException("Directory not found");

			var rootPath = Path.GetFullPath(path).Replace("/", "\\").TrimEnd('\\') + "\\";

			foreach (var filePath in Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories))
			{
				var relativePath = filePath.Replace(rootPath, "");
				this.AddFile(filePath, relativePath);
			}
		}

		/// <summary>
		/// Returns true if a file with the given full name exists.
		/// </summary>
		/// <param name="fullName"></param>
		/// <returns></returns>
		public bool Exists(string fullName)
		{
			fullName = fullName.ToLower();

			lock (_syncLock)
				return _entries.ContainsKey(fullName);
		}

		/// <summary>
		/// Returns the entry with the given full name, or null if it
		/// doesn't exist.
		/// </summary>
		/// <param name="filePath"></param>
		/// <returns></returns>
		public IPackListEntry GetEntry(string filePath)
		{
			filePath = filePath.ToLower();

			IPackListEntry result;

			lock (_syncLock)
				_entries.TryGetValue(filePath, out result);

			return result;
		}

		/// <summary>
		/// Returns list of all files with the given file name.
		/// List will be empty if none were found.
		/// </summary>
		/// <param name="fileName"></param>
		/// <returns></returns>
		public List<IPackListEntry> GetEntriesByFileName(string fileName)
		{
			fileName = fileName.ToLower();

			List<IPackListEntry> result;

			lock (_syncLock)
				_entriesNamed.TryGetValue(fileName, out result);

			if (result == null)
				return new List<IPackListEntry>();

			return result.ToList();
		}

		/// <summary>
		/// Returns list of all entries.
		/// </summary>
		/// <returns></returns>
		public List<IPackListEntry> GetEntries()
		{
			lock (_syncLock)
				return _entries.Values.ToList();
		}

		/// <summary>
		/// Loads entries from the given pack file.
		/// </summary>
		/// <param name="filePath"></param>
		private void Load(string filePath)
		{
			_fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
			_br = new BinaryReader(_fs, Encoding.ASCII);

			this.Header = PackHeader.ReadFrom(_br, filePath);

			for (var i = 0; i < this.Header.ListFileCount; ++i)
			{
				var entry = PackedFileEntry.ReadFrom(this.Header, _br);
				this.AddEntry(entry);
			}
		}

		/// <summary>
		/// Writes pack file to given location.
		/// </summary>
		/// <param name="filePath"></param>
		/// <param name="compression"></param>
		public void Save(string filePath, CompressionStrength compression = CompressionStrength.Default)
		{
			var blankLength = this.Header.BlankLength;
			var fileCount = this.Count;

			using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
			using (var bw = new BinaryWriter(fs))
			{
				var entryStarts = new Dictionary<IPackListEntry, long>();

				// Header
				bw.Write(this.Header.Signature);
				bw.Write(this.Header.FormatVersion);
				bw.Write(this.Header.PackVersion);
				bw.Write(fileCount);
				bw.Write(this.Header.FileTime1.ToFileTimeUtc());
				bw.Write(this.Header.FileTime2.ToFileTimeUtc());
				bw.Write(Encoding.UTF8.GetBytes(this.Header.BasePath.PadRight(480, '\0')));
				bw.Write(fileCount);

				var headerLengthsStart = bw.BaseStream.Position;
				bw.Write(this.Header.ListLength);
				bw.Write(blankLength);
				bw.Write(this.Header.DataLength);
				bw.Write(this.Header.Zero);

				// List
				var entryListStart = bw.BaseStream.Position;
				foreach (var entry in _entries.Values)
				{
					bw.Write((byte)entry.NameType);

					if (entry.NameType <= PackListNameType.L64)
					{
						var size = (0x10 * ((byte)entry.NameType + 1));
						var bytes = Encoding.UTF8.GetBytes(entry.RelativePath.PadRight(size - 1, '\0'));

						bw.Write(bytes);
					}
					else if (entry.NameType == PackListNameType.L96)
					{
						var size = 0x60;
						var bytes = Encoding.UTF8.GetBytes(entry.RelativePath.PadRight(size - 1, '\0'));

						bw.Write(bytes);
					}
					else if (entry.NameType == PackListNameType.LDyn)
					{
						var bytes = Encoding.UTF8.GetBytes(entry.RelativePath + '\0');

						bw.Write(bytes.Length);
						bw.Write(bytes);
					}
					else
						throw new Exception("Unknown entry name type '" + entry.NameType + "'.");

					bw.Write(entry.Seed);
					bw.Write(entry.Zero);

					entryStarts[entry] = bw.BaseStream.Position;
					bw.Write(0); // DataOffset
					bw.Write(0); // CompressedSize
					bw.Write(0); // DecompressedSize

					bw.Write(entry.IsCompressed ? 1 : 0);
					bw.Write(entry.FileTime1.ToFileTimeUtc());
					bw.Write(entry.FileTime2.ToFileTimeUtc());
					bw.Write(entry.FileTime3.ToFileTimeUtc());
					bw.Write(entry.FileTime4.ToFileTimeUtc());
					bw.Write(entry.FileTime5.ToFileTimeUtc());
				}

				var entryListEnd = bw.BaseStream.Position;
				var entryListLength = (int)(entryListEnd - entryListStart);

				bw.Write(new byte[blankLength]);

				// Data
				var dataOffset = 0;
				var dataListStart = bw.BaseStream.Position;
				foreach (var entry in _entries.Values)
				{
					// Get data
					var data = entry.GetData();
					var uncompressedSize = data.Length;

					if (entry.IsCompressed)
					{
						// Compress data
						var dataStart = bw.BaseStream.Position;
						byte[] compressed;

						using (var ms = new MemoryStream())
						{
							var zlib = new ZOutputStream(ms, (int)compression);
							zlib.Write(data, 0, data.Length);
							zlib.finish();

							compressed = ms.ToArray();
						}

						var mt = new MTRandom((entry.Seed << 7) ^ 0xA9C36DE1);
						for (var i = 0; i < compressed.Length; ++i)
							compressed[i] = (byte)(compressed[i] ^ mt.GetUInt32());

						data = compressed;
					}

					bw.Write(data);

					var dataEnd = bw.BaseStream.Position;
					var compressedSize = data.Length;
					var listPos = entryStarts[entry];

					// Overwrite entry information
					bw.BaseStream.Seek(listPos, SeekOrigin.Begin);
					bw.Write(dataOffset);
					bw.Write(compressedSize);
					bw.Write(uncompressedSize);
					bw.BaseStream.Seek(dataEnd, SeekOrigin.Begin);

					dataOffset += compressedSize;
				}

				var dataListEnd = bw.BaseStream.Position;
				var dataListLength = (int)(dataListEnd - dataListStart);

				bw.BaseStream.Seek(headerLengthsStart, SeekOrigin.Begin);
				bw.Write(entryListLength + blankLength);
				bw.Seek(4, SeekOrigin.Current);
				bw.Write(dataListLength);
				bw.BaseStream.Seek(dataListEnd, SeekOrigin.Begin);
			}
		}
	}
}
