namespace MackLib
{
	/// <summary>
	/// Specifies how a pack list name is read and written.
	/// </summary>
	public enum PackListNameType : byte
	{
		L16 = 0,
		L32 = 1,
		L48 = 2,
		L64 = 3,
		L96 = 4,
		LDyn = 5,
	}
}
