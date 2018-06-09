using ComponentAce.Compression.Libs.zlib;

namespace MackLib
{
	/// <summary>
	/// Defines how strong the applied compression is.
	/// </summary>
	public enum CompressionStrength : int
	{
		/// <summary>
		/// Data is not compressed.
		/// </summary>
		NoCompression = zlibConst.Z_NO_COMPRESSION,

		/// <summary>
		/// Data is compressed only slightly, for fast (de)compression.
		/// </summary>
		Fast = zlibConst.Z_BEST_SPEED,

		/// <summary>
		/// Data is compressed strongly to save space. Takes longer to
		/// (de)compress.
		/// </summary>
		Strong = zlibConst.Z_BEST_COMPRESSION,

		/// <summary>
		/// Default compression level, a mix of speed and space savings.
		/// </summary>
		Default = zlibConst.Z_DEFAULT_COMPRESSION,
	}
}
