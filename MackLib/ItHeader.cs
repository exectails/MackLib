using System.IO;

namespace MackLib
{
	/// <summary>
	/// Represents the header portion of an IT container file.
	/// </summary>
	public class ItHeader
	{
		/// <summary>
		/// Gets or sets the checksum.
		/// </summary>
		public int Checksum { get; set; }

		/// <summary>
		/// Gets or sets the format version.
		/// </summary>
		public byte Version { get; set; }

		/// <summary>
		/// Gets or sets the number of files in the container.
		/// </summary>
		public int FileCount { get; set; }

		/// <summary>
		/// Creates new instance.
		/// </summary>
		private ItHeader()
		{
		}

		/// <summary>
		/// Creates new header from version and file count.
		/// </summary>
		/// <param name="version"></param>
		/// <param name="fileCount"></param>
		public ItHeader(byte version, int fileCount)
		{
			this.Version = version;
			this.FileCount = fileCount;
			this.Checksum = fileCount + version;
		}

		/// <summary>
		/// Reads header from binary reader and returns it.
		/// </summary>
		/// <param name="br"></param>
		/// <returns></returns>
		public static ItHeader ReadFrom(BinaryReader br)
		{
			var result = new ItHeader();

			result.Checksum = br.ReadInt32();
			result.Version = br.ReadByte();
			result.FileCount = br.ReadInt32();

			var valid = result.FileCount + result.Version == result.Checksum;
			if (!valid)
				throw new InvalidDataException("Invalid header data, checksum test failed.");

			return result;
		}
	}
}
