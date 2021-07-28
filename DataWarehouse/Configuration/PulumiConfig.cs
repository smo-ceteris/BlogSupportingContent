namespace Ceteris.Configuration
{
    class PulumiConfig
    {
		public SqlConfig Sql { get; set; }
		public StorageConfig Storage { get; set; }
        public AdConfig Ad { get; set; }
        public GeneralConfig General { get; set; }
        public NetworkConfig Network { get; set; }

        public PulumiConfig(
            SqlConfig sql,
            StorageConfig storage,
            AdConfig ad,
            GeneralConfig general,
            NetworkConfig network
        )
        {
            this.Sql = sql;
            this.Storage = storage;
            this.Ad = ad;
            this.General = general;
            this.Network = network;
        }

        public static PulumiConfig LoadConfig()
        {
            var config = new Pulumi.Config();
            var sql = config.RequireObject<SqlConfig>("Sql");
            var storage = config.RequireObject<StorageConfig>("Storage");
            var ad = config.RequireObject<AdConfig>("Ad");
            var general = config.RequireObject<GeneralConfig>("General");
            var network = config.RequireObject<NetworkConfig>("Network");
            
            return new PulumiConfig(
                sql,
                storage,
                ad,
                general,
                network
            );
        }
    }
}