using System;

namespace MackLib.Encryption.Snow
{
	/// <summary>
	/// Snow2 Mabi en-/decrypter.
	/// </summary>
	/// <remarks>
	/// Reference: https://er.nau.edu.ua/bitstream/NAU/35989/2/diser_Gryshakov.pdf
	/// </remarks>
	public partial class Snow2
	{
		// snow2 box and data
		private uint _s00, _s01, _s02, _s03, _s04, _s05, _s06, _s07, _s08, _s09, _s10, _s11, _s12, _s13, _s14, _s15;
		private uint _r1, _r2;

		// index of uint8_t being encoded -- needed to sync with 16 x uint32_t generated
		// keystream smaller chunks
		private uint _index;

		// keystream buffer
		private uint[] _keyStream = new uint[16];

		/// <summary>
		/// Creates new instance.
		/// </summary>
		private Snow2()
		{
		}

		/// <summary>
		/// Creates new instance with given key.
		/// </summary>
		/// <param name="keyBuffer"></param>
		/// <param name="keyLen"></param>
		public Snow2(byte[] keyBuffer, int keyLen)
		{
			this.GenerateKey(keyBuffer, keyLen);
		}

		/// <summary>
		/// Creates a copy of this instance, based on its current state.
		/// </summary>
		/// <returns></returns>
		public Snow2 Clone()
		{
			var result = new Snow2();

			result._s00 = this._s00;
			result._s01 = this._s01;
			result._s02 = this._s02;
			result._s03 = this._s03;
			result._s04 = this._s04;
			result._s05 = this._s05;
			result._s06 = this._s06;
			result._s07 = this._s07;
			result._s08 = this._s08;
			result._s09 = this._s09;
			result._s10 = this._s10;
			result._s11 = this._s11;
			result._s12 = this._s12;
			result._s13 = this._s13;
			result._s14 = this._s14;
			result._s15 = this._s15;
			result._r1 = this._r1;
			result._r2 = this._r2;

			result._index = this._index;

			Buffer.BlockCopy(this._keyStream, 0, result._keyStream, 0, result._keyStream.Length * sizeof(uint));

			return result;
		}

		/// <summary>
		/// Updates instance's key.
		/// </summary>
		/// <param name="keyBuffer"></param>
		/// <param name="keyLength"></param>
		public void GenerateKey(byte[] keyBuffer, int keyLength)
		{
			_index = 0;

			if (keyLength != 16 && keyLength != 32)
				throw new ArgumentException("Invalid key length.");

			// get keyLen in bits, not byte
			keyLength *= 8;

			// initializes the encoder box with key
			this.LoadKey((sbyte[])(object)keyBuffer, keyLength);

			// load first 16 uint of keystream into buffer
			this.UpdateKeyStream(ref _keyStream);
		}

		/// <summary>
		/// Returns byte from integer at index.
		/// </summary>
		/// <param name="index"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		private static uint GetByte(int index, uint value)
			=> ((value) >> (index * 8)) & 0xff;

		/// <summary>
		/// XORs value with alpha table value.
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		private static uint AMul(uint value)
			=> (value << 8) ^ SnowAlphaMul[value >> 24];

		/// <summary>
		/// XORs value with alpha inv table value.
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		private static uint AInvMul(uint value)
			=> (value >> 8) ^ SnowAlphaInvMul[value & 0xff];

		/// <summary>
		/// Loads the key material and performs the initial mixing.
		/// </summary>
		/// <remarks>
		/// Assumptions:
		///   keysize is either 128 or 256.
		///   key is of proper length, for keysize=128, key is of lenght 16 uint8_ts
		///      and for keysize=256, key is of length 32 uint8_ts.
		///   key is given in big endian format,
		///   For 128 bit key:
		///        key[0]-> msb of k_3
		///         ...
		///        key[3]-> lsb of k_3
		///         ...
		///        key[12]-> msb of k_0
		///         ...
		///        key[15]-> lsb of k_0
		///
		///   For 256 bit key:
		///        key[0]-> msb of k_7
		///          ...
		///        key[3]-> lsb of k_7
		///          ...
		///        key[28]-> msb of k_0
		///          ...
		///        key[31]-> lsb of k_0
		/// </remarks>
		/// <param name="key"></param>
		/// <param name="keySize"></param>
		private void LoadKey(sbyte[] key, int keySize)
		{
			if (keySize != 128 && keySize != 256)
				throw new ArgumentException("Invalid key size.");

			if (key.Length < keySize / 8)
				throw new ArgumentException("Key array too small for key size.");

			if (keySize == 128)
			{
				_s15 = (uint)((int)key[03] | (((int)key[02] | (((int)key[01] | (key[00] << 8)) << 8)) << 8));
				_s14 = (uint)((int)key[07] | (((int)key[06] | (((int)key[05] | (key[04] << 8)) << 8)) << 8));
				_s13 = (uint)((int)key[11] | (((int)key[10] | (((int)key[09] | (key[08] << 8)) << 8)) << 8));
				_s12 = (uint)((int)key[15] | (((int)key[14] | (((int)key[13] | (key[12] << 8)) << 8)) << 8));

				_s11 = ~_s15; // bitwise inverse
				_s10 = ~_s14;
				_s09 = ~_s13;
				_s08 = ~_s12;
				_s07 = _s15;  // just copy
				_s06 = _s14;
				_s05 = _s13;
				_s04 = _s12;
				_s03 = ~_s15; // bitwise inverse
				_s02 = ~_s14;
				_s01 = ~_s13;
				_s00 = ~_s12;
			}
			else if (keySize == 256)
			{
				_s15 = (uint)(((long)key[00] << 24) | ((long)key[01] << 16) | ((long)key[02] << 8) | (long)key[3]);
				_s14 = (uint)(((long)key[04] << 24) | ((long)key[05] << 16) | ((long)key[06] << 8) | (long)key[7]);
				_s13 = (uint)(((long)key[08] << 24) | ((long)key[09] << 16) | ((long)key[10] << 8) | (long)key[11]);
				_s12 = (uint)(((long)key[12] << 24) | ((long)key[13] << 16) | ((long)key[14] << 8) | (long)key[15]);
				_s11 = (uint)(((long)key[16] << 24) | ((long)key[17] << 16) | ((long)key[18] << 8) | (long)key[19]);
				_s10 = (uint)(((long)key[20] << 24) | ((long)key[21] << 16) | ((long)key[22] << 8) | (long)key[23]);
				_s09 = (uint)(((long)key[24] << 24) | ((long)key[25] << 16) | ((long)key[26] << 8) | (long)key[27]);
				_s08 = (uint)(((long)key[28] << 24) | ((long)key[29] << 16) | ((long)key[30] << 8) | (long)key[31]);
				_s07 = ~_s15; // bitwise inverse
				_s06 = ~_s14;
				_s05 = ~_s13;
				_s04 = ~_s12;
				_s03 = ~_s11;
				_s02 = ~_s10;
				_s01 = ~_s09;
				_s00 = ~_s08;
			}

			_r1 = 0;
			_r2 = 0;

			// Do 32 initial clockings
			for (var i = 0; i < 2; i++)
			{
				uint tmp1, tmp2;

				tmp1 = (_r1 + _s15) ^ _r2;
				_s00 = AMul(_s00) ^ _s02 ^ AInvMul(_s11) ^ tmp1;
				tmp2 = _r2 + _s05;
				_r2 = SnowT0[GetByte(0, _r1)] ^ SnowT1[GetByte(1, _r1)] ^ SnowT2[GetByte(2, _r1)] ^ SnowT3[GetByte(3, _r1)];
				_r1 = tmp2;

				tmp1 = (_r1 + _s00) ^ _r2;
				_s01 = AMul(_s01) ^ _s03 ^ AInvMul(_s12) ^ tmp1;
				tmp2 = _r2 + _s06;
				_r2 = SnowT0[GetByte(0, _r1)] ^ SnowT1[GetByte(1, _r1)] ^ SnowT2[GetByte(2, _r1)] ^ SnowT3[GetByte(3, _r1)];
				_r1 = tmp2;

				tmp1 = (_r1 + _s01) ^ _r2;
				_s02 = AMul(_s02) ^ _s04 ^ AInvMul(_s13) ^ tmp1;
				tmp2 = _r2 + _s07;
				_r2 = SnowT0[GetByte(0, _r1)] ^ SnowT1[GetByte(1, _r1)] ^ SnowT2[GetByte(2, _r1)] ^ SnowT3[GetByte(3, _r1)];
				_r1 = tmp2;

				tmp1 = (_r1 + _s02) ^ _r2;
				_s03 = AMul(_s03) ^ _s05 ^ AInvMul(_s14) ^ tmp1;
				tmp2 = _r2 + _s08;
				_r2 = SnowT0[GetByte(0, _r1)] ^ SnowT1[GetByte(1, _r1)] ^ SnowT2[GetByte(2, _r1)] ^ SnowT3[GetByte(3, _r1)];
				_r1 = tmp2;

				tmp1 = (_r1 + _s03) ^ _r2;
				_s04 = AMul(_s04) ^ _s06 ^ AInvMul(_s15) ^ tmp1;
				tmp2 = _r2 + _s09;
				_r2 = SnowT0[GetByte(0, _r1)] ^ SnowT1[GetByte(1, _r1)] ^ SnowT2[GetByte(2, _r1)] ^ SnowT3[GetByte(3, _r1)];
				_r1 = tmp2;

				tmp1 = (_r1 + _s04) ^ _r2;
				_s05 = AMul(_s05) ^ _s07 ^ AInvMul(_s00) ^ tmp1;
				tmp2 = _r2 + _s10;
				_r2 = SnowT0[GetByte(0, _r1)] ^ SnowT1[GetByte(1, _r1)] ^ SnowT2[GetByte(2, _r1)] ^ SnowT3[GetByte(3, _r1)];
				_r1 = tmp2;

				tmp1 = (_r1 + _s05) ^ _r2;
				_s06 = AMul(_s06) ^ _s08 ^ AInvMul(_s01) ^ tmp1;
				tmp2 = _r2 + _s11;
				_r2 = SnowT0[GetByte(0, _r1)] ^ SnowT1[GetByte(1, _r1)] ^ SnowT2[GetByte(2, _r1)] ^ SnowT3[GetByte(3, _r1)];
				_r1 = tmp2;

				tmp1 = (_r1 + _s06) ^ _r2;
				_s07 = AMul(_s07) ^ _s09 ^ AInvMul(_s02) ^ tmp1;
				tmp2 = _r2 + _s12;
				_r2 = SnowT0[GetByte(0, _r1)] ^ SnowT1[GetByte(1, _r1)] ^ SnowT2[GetByte(2, _r1)] ^ SnowT3[GetByte(3, _r1)];
				_r1 = tmp2;

				tmp1 = (_r1 + _s07) ^ _r2;
				_s08 = AMul(_s08) ^ _s10 ^ AInvMul(_s03) ^ tmp1;
				tmp2 = _r2 + _s13;
				_r2 = SnowT0[GetByte(0, _r1)] ^ SnowT1[GetByte(1, _r1)] ^ SnowT2[GetByte(2, _r1)] ^ SnowT3[GetByte(3, _r1)];
				_r1 = tmp2;

				tmp1 = (_r1 + _s08) ^ _r2;
				_s09 = AMul(_s09) ^ _s11 ^ AInvMul(_s04) ^ tmp1;
				tmp2 = _r2 + _s14;
				_r2 = SnowT0[GetByte(0, _r1)] ^ SnowT1[GetByte(1, _r1)] ^ SnowT2[GetByte(2, _r1)] ^ SnowT3[GetByte(3, _r1)];
				_r1 = tmp2;

				tmp1 = (_r1 + _s09) ^ _r2;
				_s10 = AMul(_s10) ^ _s12 ^ AInvMul(_s05) ^ tmp1;
				tmp2 = _r2 + _s15;
				_r2 = SnowT0[GetByte(0, _r1)] ^ SnowT1[GetByte(1, _r1)] ^ SnowT2[GetByte(2, _r1)] ^ SnowT3[GetByte(3, _r1)];
				_r1 = tmp2;

				tmp1 = (_r1 + _s10) ^ _r2;
				_s11 = AMul(_s11) ^ _s13 ^ AInvMul(_s06) ^ tmp1;
				tmp2 = _r2 + _s00;
				_r2 = SnowT0[GetByte(0, _r1)] ^ SnowT1[GetByte(1, _r1)] ^ SnowT2[GetByte(2, _r1)] ^ SnowT3[GetByte(3, _r1)];
				_r1 = tmp2;

				tmp1 = (_r1 + _s11) ^ _r2;
				_s12 = AMul(_s12) ^ _s14 ^ AInvMul(_s07) ^ tmp1;
				tmp2 = _r2 + _s01;
				_r2 = SnowT0[GetByte(0, _r1)] ^ SnowT1[GetByte(1, _r1)] ^ SnowT2[GetByte(2, _r1)] ^ SnowT3[GetByte(3, _r1)];
				_r1 = tmp2;

				tmp1 = (_r1 + _s12) ^ _r2;
				_s13 = AMul(_s13) ^ _s15 ^ AInvMul(_s08) ^ tmp1;
				tmp2 = _r2 + _s02;
				_r2 = SnowT0[GetByte(0, _r1)] ^ SnowT1[GetByte(1, _r1)] ^ SnowT2[GetByte(2, _r1)] ^ SnowT3[GetByte(3, _r1)];
				_r1 = tmp2;

				tmp1 = (_r1 + _s13) ^ _r2;
				_s14 = AMul(_s14) ^ _s00 ^ AInvMul(_s09) ^ tmp1;
				tmp2 = _r2 + _s03;
				_r2 = SnowT0[GetByte(0, _r1)] ^ SnowT1[GetByte(1, _r1)] ^ SnowT2[GetByte(2, _r1)] ^ SnowT3[GetByte(3, _r1)];
				_r1 = tmp2;

				tmp1 = (_r1 + _s14) ^ _r2;
				_s15 = AMul(_s15) ^ _s01 ^ AInvMul(_s10) ^ tmp1;
				tmp2 = _r2 + _s04;
				_r2 = SnowT0[GetByte(0, _r1)] ^ SnowT1[GetByte(1, _r1)] ^ SnowT2[GetByte(2, _r1)] ^ SnowT3[GetByte(3, _r1)];
				_r1 = tmp2;
			}
		}

		/// <summary>
		/// Clocks the cipher 16 times and returns 16 words of keystream
		/// symbols in keystream_block.
		/// </summary>
		/// <param name="keyStreamBlock"></param>
		private void UpdateKeyStream(ref uint[] keyStreamBlock)
		{
			uint tmp;

			_s00 = AMul(_s00) ^ _s02 ^ AInvMul(_s11);
			tmp = _r2 + _s05;
			_r2 = SnowT0[GetByte(0, _r1)] ^ SnowT1[GetByte(1, _r1)] ^ SnowT2[GetByte(2, _r1)] ^ SnowT3[GetByte(3, _r1)];
			_r1 = tmp;
			keyStreamBlock[0] = (_r1 + _s00) ^ _r2 ^ _s01;

			_s01 = AMul(_s01) ^ _s03 ^ AInvMul(_s12);
			tmp = _r2 + _s06;
			_r2 = SnowT0[GetByte(0, _r1)] ^ SnowT1[GetByte(1, _r1)] ^ SnowT2[GetByte(2, _r1)] ^ SnowT3[GetByte(3, _r1)];
			_r1 = tmp;
			keyStreamBlock[1] = (_r1 + _s01) ^ _r2 ^ _s02;

			_s02 = AMul(_s02) ^ _s04 ^ AInvMul(_s13);
			tmp = _r2 + _s07;
			_r2 = SnowT0[GetByte(0, _r1)] ^ SnowT1[GetByte(1, _r1)] ^ SnowT2[GetByte(2, _r1)] ^ SnowT3[GetByte(3, _r1)];
			_r1 = tmp;
			keyStreamBlock[2] = (_r1 + _s02) ^ _r2 ^ _s03;

			_s03 = AMul(_s03) ^ _s05 ^ AInvMul(_s14);
			tmp = _r2 + _s08;
			_r2 = SnowT0[GetByte(0, _r1)] ^ SnowT1[GetByte(1, _r1)] ^ SnowT2[GetByte(2, _r1)] ^ SnowT3[GetByte(3, _r1)];
			_r1 = tmp;
			keyStreamBlock[3] = (_r1 + _s03) ^ _r2 ^ _s04;

			_s04 = AMul(_s04) ^ _s06 ^ AInvMul(_s15);
			tmp = _r2 + _s09;
			_r2 = SnowT0[GetByte(0, _r1)] ^ SnowT1[GetByte(1, _r1)] ^ SnowT2[GetByte(2, _r1)] ^ SnowT3[GetByte(3, _r1)];
			_r1 = tmp;
			keyStreamBlock[4] = (_r1 + _s04) ^ _r2 ^ _s05;

			_s05 = AMul(_s05) ^ _s07 ^ AInvMul(_s00);
			tmp = _r2 + _s10;
			_r2 = SnowT0[GetByte(0, _r1)] ^ SnowT1[GetByte(1, _r1)] ^ SnowT2[GetByte(2, _r1)] ^ SnowT3[GetByte(3, _r1)];
			_r1 = tmp;
			keyStreamBlock[5] = (_r1 + _s05) ^ _r2 ^ _s06;

			_s06 = AMul(_s06) ^ _s08 ^ AInvMul(_s01);
			tmp = _r2 + _s11;
			_r2 = SnowT0[GetByte(0, _r1)] ^ SnowT1[GetByte(1, _r1)] ^ SnowT2[GetByte(2, _r1)] ^ SnowT3[GetByte(3, _r1)];
			_r1 = tmp;
			keyStreamBlock[6] = (_r1 + _s06) ^ _r2 ^ _s07;

			_s07 = AMul(_s07) ^ _s09 ^ AInvMul(_s02);
			tmp = _r2 + _s12;
			_r2 = SnowT0[GetByte(0, _r1)] ^ SnowT1[GetByte(1, _r1)] ^ SnowT2[GetByte(2, _r1)] ^ SnowT3[GetByte(3, _r1)];
			_r1 = tmp;
			keyStreamBlock[7] = (_r1 + _s07) ^ _r2 ^ _s08;

			_s08 = AMul(_s08) ^ _s10 ^ AInvMul(_s03);
			tmp = _r2 + _s13;
			_r2 = SnowT0[GetByte(0, _r1)] ^ SnowT1[GetByte(1, _r1)] ^ SnowT2[GetByte(2, _r1)] ^ SnowT3[GetByte(3, _r1)];
			_r1 = tmp;
			keyStreamBlock[8] = (_r1 + _s08) ^ _r2 ^ _s09;

			_s09 = AMul(_s09) ^ _s11 ^ AInvMul(_s04);
			tmp = _r2 + _s14;
			_r2 = SnowT0[GetByte(0, _r1)] ^ SnowT1[GetByte(1, _r1)] ^ SnowT2[GetByte(2, _r1)] ^ SnowT3[GetByte(3, _r1)];
			_r1 = tmp;
			keyStreamBlock[9] = (_r1 + _s09) ^ _r2 ^ _s10;

			_s10 = AMul(_s10) ^ _s12 ^ AInvMul(_s05);
			tmp = _r2 + _s15;
			_r2 = SnowT0[GetByte(0, _r1)] ^ SnowT1[GetByte(1, _r1)] ^ SnowT2[GetByte(2, _r1)] ^ SnowT3[GetByte(3, _r1)];
			_r1 = tmp;
			keyStreamBlock[10] = (_r1 + _s10) ^ _r2 ^ _s11;

			_s11 = AMul(_s11) ^ _s13 ^ AInvMul(_s06);
			tmp = _r2 + _s00;
			_r2 = SnowT0[GetByte(0, _r1)] ^ SnowT1[GetByte(1, _r1)] ^ SnowT2[GetByte(2, _r1)] ^ SnowT3[GetByte(3, _r1)];
			_r1 = tmp;
			keyStreamBlock[11] = (_r1 + _s11) ^ _r2 ^ _s12;

			_s12 = AMul(_s12) ^ _s14 ^ AInvMul(_s07);
			tmp = _r2 + _s01;
			_r2 = SnowT0[GetByte(0, _r1)] ^ SnowT1[GetByte(1, _r1)] ^ SnowT2[GetByte(2, _r1)] ^ SnowT3[GetByte(3, _r1)];
			_r1 = tmp;
			keyStreamBlock[12] = (_r1 + _s12) ^ _r2 ^ _s13;

			_s13 = AMul(_s13) ^ _s15 ^ AInvMul(_s08);
			tmp = _r2 + _s02;
			_r2 = SnowT0[GetByte(0, _r1)] ^ SnowT1[GetByte(1, _r1)] ^ SnowT2[GetByte(2, _r1)] ^ SnowT3[GetByte(3, _r1)];
			_r1 = tmp;
			keyStreamBlock[13] = (_r1 + _s13) ^ _r2 ^ _s14;

			_s14 = AMul(_s14) ^ _s00 ^ AInvMul(_s09);
			tmp = _r2 + _s03;
			_r2 = SnowT0[GetByte(0, _r1)] ^ SnowT1[GetByte(1, _r1)] ^ SnowT2[GetByte(2, _r1)] ^ SnowT3[GetByte(3, _r1)];
			_r1 = tmp;
			keyStreamBlock[14] = (_r1 + _s14) ^ _r2 ^ _s15;

			_s15 = AMul(_s15) ^ _s01 ^ AInvMul(_s10);
			tmp = _r2 + _s04;
			_r2 = SnowT0[GetByte(0, _r1)] ^ SnowT1[GetByte(1, _r1)] ^ SnowT2[GetByte(2, _r1)] ^ SnowT3[GetByte(3, _r1)];
			_r1 = tmp;
			keyStreamBlock[15] = (_r1 + _s15) ^ _r2 ^ _s00;
		}

		/// <summary>
		/// Decrypts data in buffer.
		/// </summary>
		/// <param name="buffer"></param>
		/// <param name="offset"></param>
		/// <param name="length"></param>
		public void Decrypt(ref byte[] buffer, int offset, int length)
		{
			for (var i = 0; i < length / 4; ++i)
			{
				if (_index >= 16)
				{
					this.UpdateKeyStream(ref _keyStream);
					_index = 0;
				}

				var sub = _keyStream[_index];
				_index++;

				// Subtract integer from key stream from integer in buffer,
				// by converting the bytes to a uint, subtracting, and
				// then converting back to bytes.
				uint b1 = buffer[offset + i * 4 + 0];
				uint b2 = buffer[offset + i * 4 + 1];
				uint b3 = buffer[offset + i * 4 + 2];
				uint b4 = buffer[offset + i * 4 + 3];

				var bt = (b1 << 0) | (b2 << 8) | (b3 << 16) | (b4 << 24);
				bt -= sub;

				buffer[offset + i * 4 + 0] = (byte)((bt >> 00) & 0xFF);
				buffer[offset + i * 4 + 1] = (byte)((bt >> 08) & 0xFF);
				buffer[offset + i * 4 + 2] = (byte)((bt >> 16) & 0xFF);
				buffer[offset + i * 4 + 3] = (byte)((bt >> 24) & 0xFF);
			}
		}

		/// <summary>
		/// Encrypts data in buffer.
		/// </summary>
		/// <param name="buffer"></param>
		/// <param name="offset"></param>
		/// <param name="length"></param>
		public void Encrypt(ref byte[] buffer, int offset, int length)
		{
			throw new NotImplementedException();
		}
	}
}
