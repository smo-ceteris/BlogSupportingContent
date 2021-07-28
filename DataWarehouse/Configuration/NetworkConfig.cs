using System.Collections.Generic;

namespace Ceteris.Configuration
{
	class NetworkConfig
	{
		public KeyValuePair<string,string> Subnet { get; set; } = new KeyValuePair<string, string>( "SubnetDefaultName", "GUID");
	}
}