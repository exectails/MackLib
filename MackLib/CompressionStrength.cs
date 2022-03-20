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
		NoCompression,

		/// <summary>
		/// Data is compressed only slightly, for fast (de)compression.
		/// </summary>
		Fast,

		/// <summary>
		/// Data is compressed strongly to save space. Takes longer to
		/// (de)compress.
		/// </summary>
		Strong,

		/// <summary>
		/// Default compression level, a mix of speed and space savings.
		/// </summary>
		Default,
	}
}
