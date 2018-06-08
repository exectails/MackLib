using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

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
	}
}
