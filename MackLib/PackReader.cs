using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MackLib
{
	/// <summary>
	/// Package folder and pack file reader.
	/// </summary>
	public class PackReader : IDisposable
	{
		private readonly object _syncLock = new object();

		private Dictionary<string, PackedFileEntry> _entries = new Dictionary<string, PackedFileEntry>();
		private Dictionary<string, List<PackedFileEntry>> _entriesNamed = new Dictionary<string, List<PackedFileEntry>>();
		private List<FileStream> _fileStreams = new List<FileStream>();
		private List<BinaryReader> _binaryReaders = new List<BinaryReader>();

		/// <summary>
		/// File path that was used to create this reader.
		/// </summary>
		public string FilePath { get; private set; }

		/// <summary>
		/// Amount of entries in all open pack files.
		/// </summary>
		public int Count { get { return _entries.Count; } }

		/// <summary>
		/// Amount of open pack files.
		/// </summary>
		public int PackCount { get { return _fileStreams.Count; } }

		/// <summary>
		/// Creates new pack reader for given file or folder.
		/// </summary>
		/// <param name="filePath">File or folder path. If it's a folder the reader reads all *.pack files in the top directory.</param>
		public PackReader(string filePath)
		{
			this.FilePath = filePath;

			if (File.Exists(filePath))
			{
				this.Load(filePath);
			}
			else if (Directory.Exists(filePath))
			{
				foreach (var path in Directory.EnumerateFiles(filePath, "*.pack", SearchOption.TopDirectoryOnly).OrderBy(a => a))
					this.Load(path);
			}
			else
				throw new ArgumentException("Path not found.");
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
				foreach (var br in _binaryReaders)
				{
					try { br.Close(); }
					catch { }
				}

				foreach (var fs in _fileStreams)
				{
					try { fs.Close(); }
					catch { }
				}
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
		public PackedFileEntry GetEntry(string filePath)
		{
			filePath = filePath.ToLower();

			PackedFileEntry result;

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
		public List<PackedFileEntry> GetEntriesByFileName(string fileName)
		{
			fileName = fileName.ToLower();

			List<PackedFileEntry> result;

			lock (_syncLock)
				_entriesNamed.TryGetValue(fileName, out result);

			if (result == null)
				return new List<PackedFileEntry>();

			return result.ToList();
		}

		/// <summary>
		/// Returns list of all entries.
		/// </summary>
		/// <returns></returns>
		public List<PackedFileEntry> GetEntries()
		{
			lock (_syncLock)
				return _entries.Values.ToList();
		}

		/// <summary>
		/// Returns list of all entries in the given path.
		/// </summary>
		/// <returns></returns>
		public List<PackedFileEntry> GetEntriesIn(string path)
		{
			lock (_syncLock)
			{
				return _entries.Values.Where(a =>
				{
					var fullPath = (a.Header.BasePath + a.RelativePath);
					if (!fullPath.StartsWith(path))
						return false;

					var anyPathSeperatorsAfterPath = fullPath.IndexOf('\\', path.Length + 1) != -1;

					return !anyPathSeperatorsAfterPath;
				})
				.ToList();
			}
		}

		/// <summary>
		/// Loads entries from the given pack file.
		/// </summary>
		/// <param name="filePath"></param>
		private void Load(string filePath)
		{
			var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
			var br = new BinaryReader(fs, Encoding.ASCII);

			lock (_syncLock)
			{
				_fileStreams.Add(fs);
				_binaryReaders.Add(br);
			}

			var header = PackHeader.ReadFrom(br, filePath);

			for (var i = 0; i < header.ListFileCount; ++i)
			{
				var entry = PackedFileEntry.ReadFrom(header, br);
				var fullPath = entry.FullName.ToLower();

				lock (_syncLock)
				{
					_entries[fullPath] = entry;

					var key = entry.FileName.ToLower();

					if (!_entriesNamed.ContainsKey(key))
						_entriesNamed[key] = new List<PackedFileEntry>();
					_entriesNamed[key].Add(entry);
				}
			}
		}

		/// <summary>
		/// Attempts to return the path to the installed instance of Mabinogi.
		/// Returns null if no Mabinogi folder could be found.
		/// </summary>
		/// <returns></returns>
		public static string GetMabinogiDirectory()
		{
			// TODO: More thorough search.

			var key = Registry.CurrentUser.OpenSubKey(@"Software\Nexon\Mabinogi", false);
			var value = key.GetValue("");
			if (value != null)
				return (string)value;

			if (Directory.Exists(@"C:\Nexon\Library\mabinogi\appdata"))
				return @"C:\Nexon\Library\mabinogi\appdata";

			return null;
		}
	}
}
