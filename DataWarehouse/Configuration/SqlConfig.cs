namespace Ceteris.Configuration
{
	class SqlConfig
	{
		public string SqlDatabaseName { get; set; } = "SqlDatabaseDefaultName";
		public string SqlDatabaseTier { get; set; } = "SqlDatabaseTierDefaultName";
		public string SqlServerAdmin { get; set; } = "SqlServerAdminDefaultName";
		public string? SqlServerPassword { get; set; }
	}
}