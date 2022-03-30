using System.Threading.Tasks;
using System.Diagnostics;
using Pulumi;

class Program
{
        static async Task<int> Main(string[] args)
    {
        // program debugging code
       	// Debugger.Launch();
        
        // run pulumi deployment
        await Deployment.RunAsync<NetworkingStack>();
        return 0;
    }
}