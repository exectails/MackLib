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

		private Dictionary<string, PackListEntry> _entries = new Dictionary<string, PackListEntry>();
		private Dictionary<string, List<PackListEntry>> _entriesNamed = new Dictionary<string, List<PackListEntry>>();
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
		public int Count { get { return this.Header.FileCount2; } }

		/// <summary>
		/// Creates new pack reader for given file or folder.
		/// </summary>
		/// <param name="filePath">File or folder path. If it's a folder the reader reads all *.pack files in the top directory.</param>
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
		public PackListEntry GetEntry(string filePath)
		{
			filePath = filePath.ToLower();

			PackListEntry result;

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
		public List<PackListEntry> GetEntriesByFileName(string fileName)
		{
			fileName = fileName.ToLower();

			List<PackListEntry> result;

			lock (_syncLock)
				_entriesNamed.TryGetValue(fileName, out result);

			if (result == null)
				return new List<PackListEntry>();

			return result.ToList();
		}

		/// <summary>
		/// Returns list of all entries.
		/// </summary>
		/// <returns></returns>
		public List<PackListEntry> GetEntries()
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

			for (var i = 0; i < this.Header.FileCount2; ++i)
			{
				var entry = PackListEntry.ReadFrom(this.Header, _br);
				var fullPath = entry.FullName.ToLower();

				lock (_syncLock)
				{
					_entries[fullPath] = entry;

					var key = entry.FileName.ToLower();

					if (!_entriesNamed.ContainsKey(key))
						_entriesNamed[key] = new List<PackListEntry>();
					_entriesNamed[key].Add(entry);
				}
			}
		}

		/// <summary>
		/// Writes pack file to given location.
		/// </summary>
		/// <param name="filePath"></param>
		public void Save(string filePath)
		{
			_entries["test.txt"] = _entries.Values.First();

			var blankLength = this.Header.BlankLength;

			using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
			using (var bw = new BinaryWriter(fs))
			{
				var entryStarts = new Dictionary<PackListEntry, long>();

				// Header
				bw.Write(this.Header.Signature);
				bw.Write(this.Header.Version);
				bw.Write(this.Header.ClientVersion);
				bw.Write(this.Header.FileCount1);
				bw.Write(this.Header.FileTime1.ToFileTimeUtc());
				bw.Write(this.Header.FileTime2.ToFileTimeUtc());
				bw.Write(Encoding.UTF8.GetBytes(this.Header.BasePath.PadRight(480, '\0')));
				bw.Write(this.Header.FileCount2);

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
					bw.Write(entry.DataOffset);
					bw.Write(entry.CompressedSize);
					bw.Write(entry.DecompressedSize);

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
							var zlib = new ZOutputStream(ms, zlibConst.Z_DEFAULT_COMPRESSION);
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
