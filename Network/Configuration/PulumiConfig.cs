using System;

namespace Ceteris.Configuration
{
    class PulumiConfig
    {
		public ResourceGroupConfig ResourceGroup { get; set; }
        public SecurityConfig Security { get; set; }
        public NetworkConfig Network { get; set; }
        public GeneralConfig General { get; set; }

        public PulumiConfig(
            ResourceGroupConfig resourceGroup,
            SecurityConfig security,
            NetworkConfig network,
            GeneralConfig general
        )
        {
            this.ResourceGroup = resourceGroup;
            this.Security = security;
            this.Network = network;
            this.General = general;
        }

        public static PulumiConfig LoadConfig()
        {
            var config = new Pulumi.Config();
            var resourceGroup = config.RequireObject<ResourceGroupConfig>("ResourceGroup");
            var security = config.RequireObject<SecurityConfig>("Security");
            var network = config.RequireObject<NetworkConfig>("Network");
            var general = config.RequireObject<GeneralConfig>("General");
            
            return new PulumiConfig(
                resourceGroup,
                security,
                network,
                general
            );
        }
    }
}