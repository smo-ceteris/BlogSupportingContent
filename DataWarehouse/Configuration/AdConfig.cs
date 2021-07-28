using System.Collections.Generic;

namespace Ceteris.Configuration
{
	class AdConfig
	{
		public KeyValuePair<string,string> AdminGroup { get; set; } = new KeyValuePair<string, string>("AdmingroupDefaultName", "GUID");
		public KeyValuePair<string,string> UserGroup { get; set; } = new KeyValuePair<string, string>("AdmingroupDefaultName", "GUID");

	}
}