using MackLib;
using System;
using System.IO;
using Xunit;

namespace Tests.MackLib
{
	public class PackFileTests
	{
		[Fact]
		public void OpenReader()
		{
			var path = Path.Combine(PackReader.GetMabinogiDirectory(), "package", "language.pack");
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
			var path = Path.Combine(PackReader.GetMabinogiDirectory(), "package", "language.pack");
			using (var pf = new PackFile(path))
			{
				var entry = pf.GetEntry(@"data\local\xml\arbeit.english.txt");
				Assert.NotEqual(null, entry);

				using (var ms = new MemoryStream(entry.GetData()))
				using (var sr = new StreamReader(ms))
				{
					Assert.Equal(sr.ReadLine(), "1\tGeneral");
					Assert.Equal(sr.ReadLine(), "2\tGrocery Store");
					Assert.Equal(sr.ReadLine(), "3\tChurch");
				}
			}
		}

		[Fact]
		public void ReadingFileData()
		{
			var path = Path.Combine(PackReader.GetMabinogiDirectory(), "package", "language.pack");
			using (var pf = new PackFile(path))
			{
				var entry = pf.GetEntry(@"data\local\xml\arbeit.english.txt");
				Assert.NotEqual(null, entry);

				using (var ms = new MemoryStream(entry.GetData()))
				using (var sr = new StreamReader(ms))
				{
					Assert.Equal(sr.ReadLine(), "1\tGeneral");
					Assert.Equal(sr.ReadLine(), "2\tGrocery Store");
					Assert.Equal(sr.ReadLine(), "3\tChurch");
				}
			}
		}

		[Fact]
		public void ReadingFileStream()
		{
			var path = Path.Combine(PackReader.GetMabinogiDirectory(), "package", "language.pack");
			using (var pf = new PackFile(path))
			{
				var entry = pf.GetEntry(@"data\local\xml\arbeit.english.txt");
				Assert.NotEqual(null, entry);

				using (var sr = new StreamReader(entry.GetDataAsFileStream()))
				{
					Assert.Equal(sr.ReadLine(), "1\tGeneral");
					Assert.Equal(sr.ReadLine(), "2\tGrocery Store");
					Assert.Equal(sr.ReadLine(), "3\tChurch");
				}
			}
		}

		[Fact]
		public void GetEntry()
		{
			var path = Path.Combine(PackReader.GetMabinogiDirectory(), "package", "language.pack");
			using (var pf = new PackFile(path))
			{
				var entry = pf.GetEntry(@"data\local\xml\arbeit.english.txt");
				Assert.NotEqual(null, entry);
			}
		}

		[Fact]
		public void FullPath()
		{
			var path = Path.Combine(PackReader.GetMabinogiDirectory(), "package", "language.pack");
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
			var path = Path.Combine(PackReader.GetMabinogiDirectory(), "package", "language.pack");
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

				using (var sr = new StreamReader(entry.GetDataAsFileStream()))
				{
					Assert.Equal(sr.ReadLine(), "1\tMelee Weapon");
					Assert.Equal(sr.ReadLine(), "2\tOne-Handed");
					Assert.Equal(sr.ReadLine(), "3\tTwo-Handed");
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
			packFile.AddFolder("c:/users/exec/desktop/Neuer Ordner");

			packFile.Save(packTempPath);
			//packFile.Save("c:/users/exec/desktop/test.pack");

			// Check pack
			using (var pf = new PackFile(packTempPath))
			{
				var entry = pf.GetEntry(@"data\foobar\test1.txt");
				Assert.NotEqual(null, entry);

				using (var sr = new StreamReader(entry.GetDataAsFileStream()))
				{
					Assert.Equal(sr.ReadLine(), "foo1");
					Assert.Equal(sr.ReadLine(), "bar1");
					Assert.Equal(sr.ReadLine(), null);
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
					Assert.Equal(sr.ReadLine(), "foo2");
					Assert.Equal(sr.ReadLine(), "bar2");
					Assert.Equal(sr.ReadLine(), null);
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
					Assert.Equal(sr.ReadLine(), "foo3");
					Assert.Equal(sr.ReadLine(), "bar3");
					Assert.Equal(sr.ReadLine(), null);
				}
			}

			File.Delete(fileTempPath);
			File.Delete(packTempPath);
		}

		[Fact]
		public void GetEntriesByFileName()
		{
			var path = Path.Combine(PackReader.GetMabinogiDirectory(), "package", "language.pack");
			using (var pf = new PackFile(path))
			{
				var entries = pf.GetEntriesByFileName(@"auctioncategory.english.txt");
				Assert.True(entries.Count > 0);
				Assert.Single(entries, a => a.RelativePath == @"xml\auctioncategory.english.txt");
			}
		}
	}
}
