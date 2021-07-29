namespace Ceteris.Configuration
{
	class SecurityConfig
	{
		public string SubnetDevAddressPrefix { get; set; } = "10.0.0.0/27";
		public string SubnetQaAddressPrefix { get; set; } = "10.0.0.0/27";
		public string SubnetProdAddressPrefix { get; set; } = "10.0.0.0/27";
		public string SubnetBastionAddressPrefix { get; set; } = "10.0.0.0/27";
		public string SubnetMgmtAddressPrefix { get; set; } = "10.0.0.0/27";
		public string SubnetDevName { get; set; } = "SubnetDefaultName";
    	public string SubnetQaName { get; set; } = "SubnetDefaultName";
    	public string SubnetProdName { get; set; } = "SubnetDefaultName";
    	public string SubnetBastionName { get; set; } = "SubnetDefaultName";
		public string SubnetMgmtName { get; set; } = "SubnetDefaultName";
	}
}