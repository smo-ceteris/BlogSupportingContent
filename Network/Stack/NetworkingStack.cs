using System.Collections.Generic;

using Pulumi;
using Pulumi.AzureNative.Resources;
using Network = Pulumi.AzureNative.Network;
using Pulumi.AzureNative.Compute;
using NetworkInputs = Pulumi.AzureNative.Network.Inputs;
using Ceteris.Configuration;
using Ceteris.Extensions;
using System;

class NetworkingStack : Stack
{
    public NetworkingStack() : base(
            new StackOptions {
                ResourceTransformations = {
                    StackExtensions.RegisterAutoTags(
                        new Dictionary<string, string> {
                            { "Project", "GHD Cloud Datawarehouse" },
                            { "Environment", Pulumi.Deployment.Instance.StackName},
                            { "Owner", "g.janke@gesundheitsgmbh.de"}
                        }
                    ),
                    StackExtensions.ProtectProdResources(Pulumi.Deployment.Instance.StackName)
                }
            }
        )
    {
        // create config
        var config = PulumiConfig.LoadConfig();

        // retrieve stack environment and other relevant variables
        var stackName = Pulumi.Deployment.Instance.StackName.ToLower();
        var companyName = config.General.CompanyName ?? throw new ArgumentNullException("Provide a company in your config!");
        var projectName = config.General.ProjectName ?? throw new ArgumentNullException("Provide a project name in your config!");
        var subscription = config.General.SubscriptionId ?? throw new ArgumentNullException("Provide a subscriptionId in your config!");

        // configure naming conventions
        var nsgBastionName = companyName.ToLower() + "-" + projectName.ToLower() + "-" + stackName.ToLower() + "-nsg-bastion";
        var nsgDwhName = companyName.ToLower() + "-" + projectName.ToLower() + "-" + stackName.ToLower() + "-nsg-dwh";
        var nsgMgmtName = companyName.ToLower() + "-" + projectName.ToLower() + "-" + stackName.ToLower() + "-nsg-mgmt";
        var bastionName = companyName.ToLower() + "-" + projectName.ToLower() + "-" + stackName.ToLower() + "-bst";
        var bastionIpName = companyName.ToLower() + "-" + projectName.ToLower() + "-" + stackName.ToLower() + "-ip-bastion";

        // retrieve network and origin resource group
        var vnetRef = Output.Create(Network.GetVirtualNetwork.InvokeAsync(new Network.GetVirtualNetworkArgs
        {
            VirtualNetworkName = config.Network.VnetName,
            ResourceGroupName = config.ResourceGroup.ResourceGroupName,
        }));
        var virtualNetworkName = vnetRef.Apply(example => example.Name);

        var resourceGroupOriginRef = Output.Create(GetResourceGroup.InvokeAsync(new GetResourceGroupArgs
        {
             ResourceGroupName = config.ResourceGroup.ResourceGroupName
        }));
        var resourceGroupOriginName = resourceGroupOriginRef.Apply(example => example.Name);

        // create relevant network security groups
        var nsgBastion = new Network.NetworkSecurityGroup(nsgBastionName, new Network.NetworkSecurityGroupArgs
        {
            NetworkSecurityGroupName = nsgBastionName,
            ResourceGroupName = resourceGroupOriginName,
            SecurityRules =
            {
                // inbound rules
                new NetworkInputs.SecurityRuleArgs
                {
                    Access = "Allow",
                    DestinationAddressPrefix = "*",
                    DestinationPortRange = "443",
                    Direction = "Inbound",
                    Name = "AllowHttpsInbound",
                    Priority = 200,
                    Protocol = "TCP",
                    SourceAddressPrefix = "Internet",
                    SourcePortRange = "*"
                },
                new NetworkInputs.SecurityRuleArgs
                {
                    Access = "Allow",
                    DestinationAddressPrefix = "*",
                    DestinationPortRange = "443",
                    Direction = "Inbound",
                    Name = "AllowGatewayManagerInbound",
                    Priority = 210,
                    Protocol = "TCP",
                    SourceAddressPrefix = "GatewayManager",
                    SourcePortRange = "*"
                },
                new NetworkInputs.SecurityRuleArgs
                {
                    Access = "Allow",
                    DestinationAddressPrefix = "*",
                    DestinationPortRange = "443",
                    Direction = "Inbound",
                    Name = "AllowAzureLoadbalancerInbound",
                    Priority = 220,
                    Protocol = "TCP",
                    SourceAddressPrefix = "AzureLoadBalancer",
                    SourcePortRange = "*"
                },
                new NetworkInputs.SecurityRuleArgs
                {
                    Access = "Allow",
                    DestinationAddressPrefix = "VirtualNetwork",
                    DestinationPortRanges = new List<string>{"8080", "5701"},
                    Direction = "Inbound",
                    Name = "AllowBastionHostCommunicationInbound",
                    Priority = 230,
                    Protocol = "*",
                    SourceAddressPrefix = "VirtualNetwork",
                    SourcePortRange = "*"
                },
                new NetworkInputs.SecurityRuleArgs
                {
                    Access = "Deny",
                    DestinationAddressPrefix = "*",
                    DestinationPortRanges = "*",
                    Direction = "Inbound",
                    Name = "DenyAllInBound",
                    Priority = 4050,
                    Protocol = "*",
                    SourceAddressPrefix = "*",
                    SourcePortRange = "*"
                },
                
                // outbound rules
                new NetworkInputs.SecurityRuleArgs
                {
                    Access = "Allow",
                    DestinationAddressPrefix = "VirtualNetwork",
                    DestinationPortRanges = new List<string>{"22", "3389"},
                    Direction = "Outbound",
                    Name = "AllowSshRdpOutbound",
                    Priority = 200,
                    Protocol = "*",
                    SourceAddressPrefix = "*",
                    SourcePortRange = "*"
                },
                new NetworkInputs.SecurityRuleArgs
                {
                    Access = "Allow",
                    DestinationAddressPrefix = "VirtualNetwork",
                    DestinationPortRanges = new List<string>{"8080", "5701"},
                    Direction = "Outbound",
                    Name = "AllowBastionHostCommunicationOutbound",
                    Priority = 220,
                    Protocol = "*",
                    SourceAddressPrefix = "VirtualNetwork",
                    SourcePortRange = "*"
                },
                new NetworkInputs.SecurityRuleArgs
                {
                    Access = "Allow",
                    DestinationAddressPrefix = "AzureCloud",
                    DestinationPortRange = "443",
                    Direction = "Outbound",
                    Name = "AllowDiagnosticsOutbound",
                    Priority = 210,
                    Protocol = "TCP",
                    SourceAddressPrefix = "*",
                    SourcePortRange = "*"
                },
                new NetworkInputs.SecurityRuleArgs
                {
                    Access = "Allow",
                    DestinationAddressPrefix = "Internet",
                    DestinationPortRange = "80",
                    Direction = "Outbound",
                    Name = "AllowInternetOutbound",
                    Priority = 230,
                    Protocol = "TCP",
                    SourceAddressPrefix = "*",
                    SourcePortRange = "*"
                },
                new NetworkInputs.SecurityRuleArgs
                {
                    Access = "Deny",
                    DestinationAddressPrefix = "*",
                    DestinationPortRange = "*",
                    Direction = "Outbound",
                    Name = "DenyAllOutBound",
                    Priority = 4050,
                    Protocol = "*",
                    SourceAddressPrefix = "*",
                    SourcePortRange = "*"
                }
            }
        });
        
        var nsgDwh = new Network.NetworkSecurityGroup(nsgDwhName, new Network.NetworkSecurityGroupArgs
        {
            NetworkSecurityGroupName = nsgDwhName,
            ResourceGroupName = resourceGroupOriginName,
            SecurityRules =
            {
                // inbound rules
                new NetworkInputs.SecurityRuleArgs
                {
                    Access = "Allow",
                    DestinationAddressPrefix = "10.5.1.0/24",
                    DestinationPortRange = "*",
                    Direction = "Inbound",
                    Name = "AllowSelectedVnetInBound",
                    Priority = 2000,
                    Protocol = "*",
                    SourceAddressPrefixes = new List<string>{"10.5.1.0/24","10.5.4.0/22","10.20.19.0/24","10.20.20.0/24"},
                    SourcePortRange = "*"
                }, 
                new NetworkInputs.SecurityRuleArgs
                {
                    Access = "Deny",
                    DestinationAddressPrefix = "*",
                    DestinationPortRange = "*",
                    Direction = "Inbound",
                    Name = "DenyAllInBound",
                    Priority = 4050,
                    Protocol = "*",
                    SourceAddressPrefix = "*",
                    SourcePortRange = "*"
                },               
                // outbound rules
                new NetworkInputs.SecurityRuleArgs
                {
                    Access = "Deny",
                    DestinationAddressPrefix = "Internet",
                    DestinationPortRange = "*",
                    Direction = "Outbound",
                    Name = "DenyInternetOutBound",
                    Priority = 200,
                    Protocol = "*",
                    SourceAddressPrefix = "*",
                    SourcePortRange = "*"
                },
                new NetworkInputs.SecurityRuleArgs
                {
                    Access = "Allow",
                    DestinationAddressPrefixes = new List<string>{"10.5.1.0/24","10.5.4.0/22","10.20.19.0/24","10.20.20.0/24"},
                    DestinationPortRange = "*",
                    Direction = "Outbound",
                    Name = "AllowSelectedVnetOutBound",
                    Priority = 2000,
                    Protocol = "*",
                    SourceAddressPrefix = "10.5.1.0/24",
                    SourcePortRange = "*"
                },
                new NetworkInputs.SecurityRuleArgs
                {
                    Access = "Deny",
                    DestinationAddressPrefix = "*",
                    DestinationPortRange = "*",
                    Direction = "Outbound",
                    Name = "DenyAllOutBound",
                    Priority = 4050,
                    Protocol = "*",
                    SourceAddressPrefix = "*",
                    SourcePortRange = "*"
                }
            }
        });

        var nsgMgmt = new Network.NetworkSecurityGroup(nsgMgmtName, new Network.NetworkSecurityGroupArgs
        {
            NetworkSecurityGroupName = nsgMgmtName,
            ResourceGroupName = resourceGroupOriginName,
            SecurityRules =
            {
                // inbound rules
                new NetworkInputs.SecurityRuleArgs
                {
                    Access = "Allow",
                    DestinationAddressPrefix = "10.5.1.0/24",
                    DestinationPortRange = "*",
                    Direction = "Inbound",
                    Name = "AllowSelectedVnetInBound",
                    Priority = 2000,
                    Protocol = "*",
                    SourcePortRange = "*",
                    SourceAddressPrefixes = new List<string>{"10.5.1.0/24","10.5.4.0/22","10.20.19.0/24","10.20.20.0/24"}
                }, 
                new NetworkInputs.SecurityRuleArgs
                {
                    Access = "Deny",
                    DestinationAddressPrefix = "*",
                    DestinationPortRange = "*",
                    Direction = "Inbound",
                    Name = "DenyAllInBound",
                    Priority = 4050,
                    Protocol = "*",
                    SourceAddressPrefix = "*",
                    SourcePortRange = "*"
                },               
                // outbound rules
                new NetworkInputs.SecurityRuleArgs
                {
                    Access = "Allow",
                    Description = "allows Azure Ad auth on SQL Server",
                    DestinationAddressPrefix = "AzureActiveDirectory",
                    DestinationPortRange = "*",
                    Direction = "Outbound",
                    Name = "AllowAzureAdOutbound",
                    Priority = 1890,
                    Protocol = "*",
                    SourceAddressPrefix = "*",
                    SourcePortRange = "*"
                },
                new NetworkInputs.SecurityRuleArgs
                {
                    Access = "Allow",
                    Description = "used for static content needed at aad login with MFA",
                    DestinationAddressPrefix = "13.107.0.0/16",
                    DestinationPortRanges = new List<string>{"80","443"},
                    Direction = "Outbound",
                    Name = "AllowAzureAdCdnOutbound",
                    Priority = 1880,
                    Protocol = "TCP",
                    SourceAddressPrefix = "*",
                    SourcePortRange = "*"
                },
                new NetworkInputs.SecurityRuleArgs
                {
                    Access = "Allow",
                    Description = "allows management vm to communicate with Azure cloud",
                    DestinationAddressPrefix = "AzureCloud",
                    DestinationPortRange = "*",
                    Direction = "Outbound",
                    Name = "AllowAzureCloudOutbound",
                    Priority = 1900,
                    Protocol = "*",
                    SourceAddressPrefix = "*",
                    SourcePortRange = "*"
                },
                new NetworkInputs.SecurityRuleArgs
                {
                    Access = "Allow",
                    DestinationAddressPrefixes = new List<string>{"10.5.1.0/24","10.5.4.0/22","10.20.19.0/24","10.20.20.0/24"},
                    DestinationPortRange = "*",
                    Direction = "Outbound",
                    Name = "AllowSelectedVnetOutBound",
                    Priority = 2000,
                    Protocol = "*",
                    SourceAddressPrefix = "10.5.1.0/24",
                    SourcePortRange = "*"
                },
                new NetworkInputs.SecurityRuleArgs
                {
                    Access = "Deny",
                    DestinationAddressPrefix = "*",
                    DestinationPortRange = "*",
                    Direction = "Outbound",
                    Name = "DenyAllOutBound",
                    Priority = 4050,
                    Protocol = "*",
                    SourceAddressPrefix = "*",
                    SourcePortRange = "*"
                }
            }
        });

        // create subnets
        var subnetDev = new Network.Subnet(config.Security.SubnetDevName, new Network.SubnetArgs()
        {
            VirtualNetworkName = virtualNetworkName,  
            ResourceGroupName = resourceGroupOriginName,      
            AddressPrefix = config.Security.SubnetDevAddressPrefix,
            Name = config.Security.SubnetDevName,
            PrivateEndpointNetworkPolicies = "Disabled",
            NetworkSecurityGroup = new NetworkInputs.NetworkSecurityGroupArgs
            {
                Id = nsgDwh.Id
            }
        });

        var subnetQa = new Network.Subnet(config.Security.SubnetQaName, new Pulumi.AzureNative.Network.SubnetArgs()
        {
            VirtualNetworkName = virtualNetworkName, 
            ResourceGroupName = resourceGroupOriginName,      
            AddressPrefix = config.Security.SubnetQaAddressPrefix,
            Name = config.Security.SubnetQaName,
            PrivateEndpointNetworkPolicies = "Disabled",
            NetworkSecurityGroup = new NetworkInputs.NetworkSecurityGroupArgs
            {
                Id = nsgDwh.Id
            }
        });

        var subnetProd = new Network.Subnet(config.Security.SubnetProdName, new Network.SubnetArgs()
        {
            VirtualNetworkName = virtualNetworkName,  
            ResourceGroupName = resourceGroupOriginName,      
            AddressPrefix = config.Security.SubnetProdAddressPrefix,
            Name = config.Security.SubnetProdName,
            PrivateEndpointNetworkPolicies = "Disabled",
            NetworkSecurityGroup = new NetworkInputs.NetworkSecurityGroupArgs
            {
                Id = nsgDwh.Id
            }
        });

        var subnetMgmt = new Network.Subnet(config.Security.SubnetMgmtName, new Network.SubnetArgs()
        {
            VirtualNetworkName = virtualNetworkName,  
            ResourceGroupName = resourceGroupOriginName,      
            AddressPrefix = config.Security.SubnetMgmtAddressPrefix,
            Name = config.Security.SubnetMgmtName,
            PrivateEndpointNetworkPolicies = "Disabled",
            NetworkSecurityGroup = new NetworkInputs.NetworkSecurityGroupArgs
            {
                Id = nsgMgmt.Id
            }
        });

        var subnetBastion = new Network.Subnet(config.Security.SubnetBastionName, new Network.SubnetArgs()
        {
            VirtualNetworkName = virtualNetworkName,  
            ResourceGroupName = resourceGroupOriginName,      
            AddressPrefix = config.Security.SubnetBastionAddressPrefix,
            Name = config.Security.SubnetBastionName,
            PrivateEndpointNetworkPolicies = "Disabled",
            NetworkSecurityGroup = new NetworkInputs.NetworkSecurityGroupArgs
            {
                Id = nsgBastion.Id
            },
        });

        // create bastion and allocated public ip
        var publicBastionIp = new Network.PublicIPAddress(bastionIpName, new Network.PublicIPAddressArgs()
        {
            ResourceGroupName = resourceGroupOriginName,
            PublicIPAllocationMethod = Network.IPAllocationMethod.Static,
            Sku = new Network.Inputs.PublicIPAddressSkuArgs
            {
                Name = Network.PublicIPAddressSkuName.Standard,
                Tier = Network.PublicIPAddressSkuTier.Global,
            },
            PublicIpAddressName = bastionIpName
        });

        var bastion = new Network.BastionHost(bastionName, new Pulumi.AzureNative.Network.BastionHostArgs()
        {
            BastionHostName = bastionName,
            ResourceGroupName = resourceGroupOriginName,
            IpConfigurations = new NetworkInputs.BastionHostIPConfigurationArgs()
            {
                Name = "IpConfiguration",
                PrivateIPAllocationMethod = Network.IPAllocationMethod.Dynamic,
                PublicIPAddress = new Network.Inputs.SubResourceArgs
                {
                    Id = publicBastionIp.Id,
                },
                Subnet = new Network.Inputs.SubResourceArgs
                {
                    Id = subnetBastion.Id,
                }
            }
        });

        this.SubnetDev = subnetDev.Id;
        this.SubnetQa = subnetQa.Id;
        this.SubnetProd = subnetProd.Id;
        this.SubnetBastion = subnetBastion.Id;
        this.SubnetMgmt = subnetMgmt.Id;
    }

    // outputs
    [Output] public Output<string> SubnetDev { get; set; }
    [Output] public Output<string> SubnetQa { get; set; }
    [Output] public Output<string> SubnetProd { get; set; }
    [Output] public Output<string> SubnetBastion { get; set; }
    [Output] public Output<string> SubnetMgmt { get; set; }
}