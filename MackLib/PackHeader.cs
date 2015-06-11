using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MackLib
{
	internal class PackHeader
	{
		// 512 B
		public byte[/*8*/] Signature;
		public uint D1;
		public uint Sum;
		public DateTime FileTime1;
		public DateTime FileTime2;
		public string/*char[480]*/ DataPath;

		// 32 B
		public uint FileCount;
		public uint HeaderLength;
		public uint BlankLength;
		public uint DataLength;
		public byte[/*16*/] Zero;

		internal PackHeader()
		{
			this.Signature = new byte[] { (byte)'P', (byte)'A', (byte)'C', (byte)'K', 0x02, 0x01, 0x00, 0x00 };
			this.D1 = 1;
			this.Zero = new byte[16];
		}
	};
}
