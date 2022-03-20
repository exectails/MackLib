using System;
using System.IO;
using MackLib;
using MackLib.Compression;
using Xunit;

namespace Tests.MackLib
{
	public class PackFileTests
	{
		[Fact]
		public void OpenReader()
		{
			var path = Path.Combine(Util.GetMabiDir(), "package", "language.pack");
			using (var pf = new PackFile(path))
			{
				Assert.InRange(pf.Count, 1, 100000);
			}
		}

		[Fact]
		public void OpenReaderWithInvalidPath()
		{
			Assert.Throws(typeof(ArgumentException), () =>
			{
				using (var pr = new PackFile("some/path/that/hopefully/doesn't/exist"))
				{
				}
			});
		}

		[Fact]
		public void GetData()
		{
			var path = Path.Combine(Util.GetMabiDir(), "package", "language.pack");
			using (var pf = new PackFile(path))
			{
				var entry = pf.GetEntry(@"data\local\xml\arbeit.english.txt");
				Assert.NotEqual(null, entry);

				using (var ms = new MemoryStream(entry.GetData()))
				using (var sr = new StreamReader(ms))
				{
					Assert.Equal("1\tGeneral", sr.ReadLine());
					Assert.Equal("2\tGrocery Store", sr.ReadLine());
					Assert.Equal("3\tChurch", sr.ReadLine());
				}
			}
		}

		[Fact]
		public void ReadingFileData()
		{
			var path = Path.Combine(Util.GetMabiDir(), "package", "language.pack");
			using (var pf = new PackFile(path))
			{
				var entry = pf.GetEntry(@"data\local\xml\arbeit.english.txt");
				Assert.NotEqual(null, entry);

				using (var ms = new MemoryStream(entry.GetData()))
				using (var sr = new StreamReader(ms))
				{
					Assert.Equal("1\tGeneral", sr.ReadLine());
					Assert.Equal("2\tGrocery Store", sr.ReadLine());
					Assert.Equal("3\tChurch", sr.ReadLine());
				}
			}
		}

		[Fact]
		public void ReadingFileStream()
		{
			var path = Path.Combine(Util.GetMabiDir(), "package", "language.pack");
			using (var pf = new PackFile(path))
			{
				var entry = pf.GetEntry(@"data\local\xml\arbeit.english.txt");
				Assert.NotEqual(null, entry);

				using (var sr = new StreamReader(entry.GetDataAsFileStream()))
				{
					Assert.Equal("1\tGeneral", sr.ReadLine());
					Assert.Equal("2\tGrocery Store", sr.ReadLine());
					Assert.Equal("3\tChurch", sr.ReadLine());
				}
			}
		}

		[Fact]
		public void GetEntry()
		{
			var path = Path.Combine(Util.GetMabiDir(), "package", "language.pack");
			using (var pf = new PackFile(path))
			{
				var entry = pf.GetEntry(@"data\local\xml\arbeit.english.txt");
				Assert.NotEqual(null, entry);
			}
		}

		[Fact]
		public void FullPath()
		{
			var path = Path.Combine(Util.GetMabiDir(), "package", "language.pack");
			using (var pf = new PackFile(path))
			{
				// Previously the base path was ignored, which meant that
				// entries from language.pack were put into the root folder,
				// and would've been found there. This messes up the folder
				// structure, and this file should actually not be found
				// in this location.

				var entry = pf.GetEntry(@"local\world.english.txt");
				Assert.Equal(null, entry);

				entry = pf.GetEntry(@"data\local\world.english.txt");
				Assert.NotEqual(null, entry);
			}
		}

		[Fact]
		public void Save()
		{
			var path = Path.Combine(Util.GetMabiDir(), "package", "language.pack");
			var contents = File.ReadAllBytes(path);
			var tempPath = Path.GetTempFileName();

			using (var pf = new PackFile(path))
			{
				pf.Save(tempPath);
			}

			using (var pf = new PackFile(tempPath))
			{
				var entry = pf.GetEntry(@"data\local\xml\auctioncategory.english.txt");
				Assert.NotEqual(null, entry);

				File.WriteAllBytes("c:/users/exec/desktop/test.txt", entry.GetData());

				using (var sr = new StreamReader(entry.GetDataAsFileStream()))
				{
					Assert.Equal("1\tMelee Weapon", sr.ReadLine());
					Assert.Equal("2\tOne-Handed", sr.ReadLine());
					Assert.Equal("3\tTwo-Handed", sr.ReadLine());
				}
			}

			File.Delete(tempPath);
		}

		[Fact]
		public void Create()
		{
			// Create pack
			var fileTempPath = Path.GetTempFileName();
			var packTempPath = Path.GetTempFileName();

			File.WriteAllText(fileTempPath, "foo1\nbar1");

			var packFile = new PackFile();
			packFile.AddFile(fileTempPath, @"foobar\test1.txt");

			packFile.Save(packTempPath, CompressionStrength.Default);

			// Check pack
			using (var pf = new PackFile(packTempPath))
			{
				var entry = pf.GetEntry(@"data\foobar\test1.txt");
				Assert.NotEqual(null, entry);

				using (var sr = new StreamReader(entry.GetDataAsFileStream()))
				{
					Assert.Equal("foo1", sr.ReadLine());
					Assert.Equal("bar1", sr.ReadLine());
					Assert.Equal(null, sr.ReadLine());
				}
			}

			// Add second file to pack
			File.WriteAllText(fileTempPath, "foo2\nbar2");

			packFile.AddFile(fileTempPath, @"foobar\test2.txt");
			packFile.Save(packTempPath);

			// Check modified pack
			using (var pf = new PackFile(packTempPath))
			{
				var entry = pf.GetEntry(@"data\foobar\test2.txt");
				Assert.NotEqual(null, entry);

				using (var sr = new StreamReader(entry.GetDataAsFileStream()))
				{
					Assert.Equal("foo2", sr.ReadLine());
					Assert.Equal("bar2", sr.ReadLine());
					Assert.Equal(null, sr.ReadLine());
				}
			}

			// Overwrite first file in pack
			File.WriteAllText(fileTempPath, "foo3\nbar3");

			packFile.AddFile(fileTempPath, @"foobar\test1.txt");
			packFile.Save(packTempPath);

			// Check modified pack
			using (var pf = new PackFile(packTempPath))
			{
				var entry = pf.GetEntry(@"data\foobar\test1.txt");
				Assert.NotEqual(null, entry);

				using (var sr = new StreamReader(entry.GetDataAsFileStream()))
				{
					Assert.Equal("foo3", sr.ReadLine());
					Assert.Equal("bar3", sr.ReadLine());
					Assert.Equal(null, sr.ReadLine());
				}
			}

			File.Delete(fileTempPath);
			File.Delete(packTempPath);
		}

		[Fact]
		public void GetEntriesByFileName()
		{
			var path = Path.Combine(Util.GetMabiDir(), "package", "language.pack");
			using (var pf = new PackFile(path))
			{
				var entries = pf.GetEntriesByFileName(@"auctioncategory.english.txt");
				Assert.True(entries.Count > 0);
				Assert.Single(entries, a => a.RelativePath == @"xml\auctioncategory.english.txt");
			}
		}
	}
}
