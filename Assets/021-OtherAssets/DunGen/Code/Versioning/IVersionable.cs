namespace DunGen.Versioning
{
	public interface IVersionable
	{
		int DataVersion { get; set; }
		int LatestVersion { get; }
		bool RequiresMigration { get; }

		void Migrate();
	}
}