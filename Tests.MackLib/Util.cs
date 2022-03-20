namespace Tests.MackLib
{
	public static class Util
	{
		/// <summary>
		/// Returns the path to the Mabinogi folder to use for the tests.
		/// The client folder needs to include a package folder with .pack
		/// files to run the tests.
		/// </summary>
		/// <returns></returns>
		public static string GetMabiDir()
			=> @"E:\Mabinogi\Clients\Mabinogi NA382";
	}
}
