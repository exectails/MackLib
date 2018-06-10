using System;

namespace MackLib.UclCompression
{
	/// <summary>
	/// Compression for KR beta client.
	/// </summary>
	internal static class Ucl
	{
		/// <summary>
		/// Decompresses array to given size and returns it.
		/// </summary>
		/// <param name="src"></param>
		/// <param name="uncompressedSize"></param>
		/// <returns></returns>
		public static byte[] Decompress_NRV2E(byte[] src, uint uncompressedSize)
		{
			var dstLength = uncompressedSize;
			var dst = new byte[dstLength];

			var result = Ucl.Decompress_NRV2E(src, (uint)src.Length, dst, ref dstLength);
			if (result != UclResult.Ok)
				throw new Exception("UCL Decompression failed with code '" + result + "'");

			return dst;
		}

		/// <summary>
		/// Decompresses array.
		/// </summary>
		/// <param name="src"></param>
		/// <param name="src_len"></param>
		/// <param name="dst"></param>
		/// <param name="dst_len"></param>
		/// <returns></returns>
		private static UclResult Decompress_NRV2E(byte[] src, uint src_len, byte[] dst, ref uint dst_len)
		{
			uint bb = 0;
			uint ilen = 0, olen = 0, last_m_off = 1;
			var oend = dst_len;

			uint getbit()
			{
				return (((bb = (bb & 0x7f) != 0 ? bb * 2 : (uint)(src[ilen++] * 2 + 1)) >> 8) & 1);
			}

			while (true)
			{
				uint m_off, m_len;

				while (getbit() != 0)
				{
					if (ilen >= src_len)
					{
						dst_len = olen; return
							UclResult.InputOverrun;
					}

					if (olen >= oend)
					{
						dst_len = olen;
						return UclResult.OutputOverrun;
					}

					dst[olen++] = src[ilen++];
				}
				m_off = 1;

				while (true)
				{
					m_off = m_off * 2 + getbit();

					if (ilen >= src_len)
					{
						dst_len = olen;
						return UclResult.InputOverrun;
					}

					if (m_off > 0xffffff + 3)
					{
						dst_len = olen;
						return UclResult.LookBehindOverrun;
					}

					if (getbit() != 0)
						break;

					m_off = (m_off - 1) * 2 + getbit();
				}

				if (m_off == 2)
				{
					m_off = last_m_off;
					m_len = getbit();
				}
				else
				{
					if (ilen >= src_len)
					{
						dst_len = olen;
						return UclResult.InputOverrun;
					}

					m_off = (m_off - 3) * 256 + src[ilen++];

					if (m_off == 0xffffffff)
						break;

					m_len = (m_off ^ 0xffffffff) & 1;
					m_off >>= 1;
					last_m_off = ++m_off;
				}

				if (m_len != 0)
				{
					m_len = 1 + getbit();
				}
				else if (getbit() != 0)
				{
					m_len = 3 + getbit();
				}
				else
				{
					m_len++;

					do
					{
						m_len = m_len * 2 + getbit();

						if (ilen >= src_len)
						{
							dst_len = olen;
							return UclResult.InputOverrun;
						}

						if (m_len >= oend)
						{
							dst_len = olen;
							return UclResult.OutputOverrun;
						}
					}
					while (getbit() == 0);

					m_len += 3;
				}

				if (m_off > 0x500)
					m_len++;

				if (olen + m_len > oend)
				{
					dst_len = olen;
					return UclResult.OutputOverrun;
				}

				if (m_off > olen)
				{
					dst_len = olen;
					return UclResult.LookBehindOverrun;
				}

				var m_pos = olen - m_off;
				dst[olen++] = dst[m_pos++];
				do
				{
					dst[olen++] = dst[m_pos++];
				}
				while (--m_len > 0);
			}

			dst_len = olen;

			return (ilen == src_len ? UclResult.Ok : (ilen < src_len ? UclResult.InputNotConsumed : UclResult.InputOverrun));
		}
	}
}
