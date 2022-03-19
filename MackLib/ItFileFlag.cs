using System;

namespace MackLib
{
	/// <summary>
	/// Used to specify a files encryption and compression properties.
	/// </summary>
	[Flags]
	public enum ItFileFlag
	{
		/// <summary>
		/// Files is compressed.
		/// </summary>
		/// <remarks>
		/// Decompress file as the last step, after it was decrypted
		/// completely.
		/// </remarks>
		Compressed = 1,

		/// <summary>
		/// File is encrypted in its entirety.
		/// </summary>
		/// <remarks>
		/// Encrypted and HeadEncrypted can occur simultaneously. In that
		/// case, decrypt the entire file first, followed by the head.
		/// </remarks>
		Encrypted = 2,

		/// <summary>
		/// File's head is encrypted.
		/// </summary>
		/// <remarks>
		/// The head are the first 1024 bytes of the file, or as many bytes
		/// as the file has.
		/// 
		/// Encrypted and HeadEncrypted can occur simultaneously. In that
		/// case, decrypt the entire file first, followed by the head.
		/// </remarks>
		HeadEncrypted = 4,
	}
}
