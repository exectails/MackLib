namespace MackLib.Compression.NRV
{
	/// <summary>
	/// Result of a UCL compression operation.
	/// </summary>
	public enum UclResult
	{
		/// <summary>
		/// Decompression successful.
		/// </summary>
		Ok,

		/// <summary>
		/// Data to be decompressed is shorter than the given length.
		/// </summary>
		InputOverrun,

		/// <summary>
		/// Data to be decompressed is longer then the output buffer.
		/// </summary>
		OutputOverrun,

		/// <summary>
		/// ?
		/// </summary>
		LookBehindOverrun,

		/// <summary>
		/// Nothing was decompressed from the source buffer.
		/// </summary>
		InputNotConsumed,
	}
}
