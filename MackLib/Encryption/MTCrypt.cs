using MackLib.Random;

namespace MackLib.Encryption
{
	/// <summary>
	/// Encryption using an MersenneTwister RNG, as used to encrypt data
	/// files in Mabinogi's PACK data container.
	/// </summary>
	public class MTCrypt
	{
		/// <summary>
		/// Encrypts the buffer using the given seed.
		/// </summary>
		/// <param name="buffer"></param>
		/// <param name="seed"></param>
		public static void Encrypt(ref byte[] buffer, uint seed)
		{
			var mt = new MTRandom(((uint)seed << 7) ^ 0xA9C36DE1);

			for (var i = 0; i < buffer.Length; ++i)
				buffer[i] = (byte)(buffer[i] ^ mt.GetUInt32());
		}

		/// <summary>
		/// Decrypts the buffer using the given seed.
		/// </summary>
		/// <param name="buffer"></param>
		/// <param name="seed"></param>
		public static void Decrypt(ref byte[] buffer, uint seed)
			=> Encrypt(ref buffer, seed);
	}
}
