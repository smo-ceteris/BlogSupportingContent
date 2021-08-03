﻿using System.Threading.Tasks;
using System.Diagnostics;
using Pulumi;

class Program
{
    // static Task<int> Main() => Pulumi.Deployment.RunAsync<PrivateDataWarehouse>();

    static async Task<int> Main(string[] args)
    {
        // program debugging code
       	// Debugger.Launch();
        
        // run pulumi deployment
        await Deployment.RunAsync<PrivateDataWarehouse>();
        return 0;
    }
}