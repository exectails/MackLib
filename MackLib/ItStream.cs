using System;
using System.IO;
using MackLib.Encryption.Snow;

namespace MackLib
{
	/// <summary>
	/// A stream to read encrypted data from an IT file.
	/// </summary>
	public class ItStream : Stream
	{
		private readonly Stream _stream;
		private readonly Snow2 _crypter;

		private byte[] _backBuffer = new byte[1024 * 1024];
		private int _bbOffset;

		/// <summary>
		/// Returns whether data can  be read from this stream.
		/// </summary>
		public override bool CanRead => true;

		/// <summary>
		/// Returns whether data can  be written to this stream.
		/// </summary>
		/// public override bool CanSeek => false;
		public override bool CanWrite => false;

		/// <summary>
		/// Returns whether a new position can be sought.
		/// </summary>
		public override bool CanSeek => true;

		/// <summary>
		/// Returns the stream's length.
		/// </summary>
		public override long Length => _stream.Length;

		/// <summary>
		/// Returns the current position in the stream.
		/// </summary>
		public override long Position { get => _stream.Position; set => throw new NotImplementedException(); }

		/// <summary>
		/// Creates new stream that reads/writes from/to the base stream
		/// and en-/decrypts the data with the given crypter.
		/// </summary>
		/// <param name="baseStream"></param>
		/// <param name="crypter"></param>
		public ItStream(Stream baseStream, Snow2 crypter)
		{
			_stream = baseStream;
			_crypter = crypter;
		}

		/// <summary>
		/// Reads the given amount of bytes into the buffer at the offset.
		/// </summary>
		/// <param name="buffer"></param>
		/// <param name="offset"></param>
		/// <param name="count"></param>
		/// <returns>
		/// Returns the number of bytes written, but it always writes the
		/// requested amount of bytes. If the return value is lower, an
		/// error occurred.
		/// </returns>
		public override int Read(byte[] buffer, int offset, int count)
		{
			if (count <= _bbOffset)
			{
				_bbOffset -= count;

				Buffer.BlockCopy(_backBuffer, 0, buffer, offset, count);
				Buffer.BlockCopy(_backBuffer, count, _backBuffer, 0, _bbOffset);

				return count;
			}

			// Resize backbuffer if it's is too small for the requested
			// amount of bytes
			if (count > _backBuffer.Length)
			{
				var newSize = (count + 1023) / 1024 * 1024;
				Array.Resize(ref _backBuffer, newSize);
			}

			var readCount = count - _bbOffset;
			var blockLen = (readCount + 3) / 4 * 4;

			var result = _stream.Read(_backBuffer, _bbOffset, blockLen);
			if (result < readCount)
				throw new ArgumentException();

			_crypter.Decrypt(ref _backBuffer, _bbOffset, blockLen);

			_bbOffset = blockLen - readCount;

			Buffer.BlockCopy(_backBuffer, 0, buffer, offset, count);
			Buffer.BlockCopy(_backBuffer, count, _backBuffer, 0, _bbOffset);

			return count;
		}

		/// <summary>
		/// Sets the position in the stream.
		/// </summary>
		/// <param name="offset"></param>
		/// <param name="origin"></param>
		/// <returns></returns>
		public override long Seek(long offset, SeekOrigin origin)
		{
			return _stream.Seek(offset, origin);
		}

		/// <summary>
		/// Not implemented yet.
		/// </summary>
		/// <param name="buffer"></param>
		/// <param name="offset"></param>
		/// <param name="count"></param>
		public override void Write(byte[] buffer, int offset, int count)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Not implemented yet.
		/// </summary>
		public override void Flush()
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Not implemented yet.
		/// </summary>
		/// <param name="value"></param>
		public override void SetLength(long value)
		{
			throw new NotImplementedException();
		}
	}
}
