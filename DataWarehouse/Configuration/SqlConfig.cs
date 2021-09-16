namespace Ceteris.Configuration
{
	class SqlConfig
	{
		public string SqlDatabaseNameStaging { get; set; } = "SqlDatabaseDefaultName";
		public string SqlDatabaseNameBusiness { get; set; } = "SqlDatabaseDefaultName";
		public string SqlDatabaseNameIntegration { get; set; } = "SqlDatabaseDefaultName";
		public string SqlDatabaseTier { get; set; } = "SqlDatabaseTierDefaultName";
		public string SqlServerAdmin { get; set; } = "SqlServerAdminDefaultName";
		public string? SqlServerPassword { get; set; }
	}
}