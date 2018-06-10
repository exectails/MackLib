namespace MackLib
{
	/// <summary>
	/// Specifies how a pack list name is read and written.
	/// </summary>
	public enum PackListNameType : byte
	{
		/// <summary>
		/// 16 byte length.
		/// </summary>
		L16 = 0,

		/// <summary>
		/// 32 byte length.
		/// </summary>
		L32 = 1,

		/// <summary>
		/// 48 byte length.
		/// </summary>
		L48 = 2,

		/// <summary>
		/// 64 byte length.
		/// </summary>
		L64 = 3,

		/// <summary>
		/// 96 byte length.
		/// </summary>
		L96 = 4,

		/// <summary>
		/// Prefixed with length as int.
		/// </summary>
		LDyn = 5,
	}
}
