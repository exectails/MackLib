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

				using (var sr = new StreamReader(entry.GetDataAsStream()))
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

				using (var sr = new StreamReader(entry.GetDataAsStream()))
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
