using System;
using System.Collections.Generic;
using System.Reflection;

using Pulumi;
using Pulumi.AzureAD;
using Pulumi.AzureNative.Resources;
using Pulumi.AzureNative.Storage;
using Pulumi.AzureNative.Sql;
using Pulumi.AzureNative.DataFactory;
using Pulumi.AzureNative.Network;
using Pulumi.AzureNative.Network.Inputs;
using Pulumi.AzureNative.Authorization;
using Pulumi.Random;

using Ceteris.Configuration;
using Ceteris.Extensions;

class PrivateDataWarehouse : Stack
{
    public PrivateDataWarehouse() : base(
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
        var resourceGroupName = companyName.ToUpper() + "-" + projectName.ToUpper() + "-" + stackName.ToUpper();
        var storageAccountName = companyName.ToLower() + projectName.ToLower() + stackName.ToLower() + "st";
        var sqlServerName = companyName.ToLower() + "-" + projectName.ToLower() + "-" + stackName.ToLower() + "-sql";
        var dataFactoryName = companyName.ToLower() + "-" + projectName.ToLower() + "-" + stackName.ToLower() + "-adf";
        var privateEndpointSqlName = companyName.ToLower() + "-" + projectName.ToLower() + "-" + stackName.ToLower() + "-pe-sql";
        var privateEndpointBlobName = companyName.ToLower() + "-" + projectName.ToLower() + "-" + stackName.ToLower() + "-pe-blob";
        var privateEndpointAdfName = companyName.ToLower() + "-" + projectName.ToLower() + "-" + stackName.ToLower() + "-pe-adf";

        // retrieve resource group
        var resourceGroupDatamartRef = Output.Create(GetResourceGroup.InvokeAsync(new GetResourceGroupArgs
        {
            ResourceGroupName = resourceGroupName
        }));
        var resourceGroupDatamartName = resourceGroupDatamartRef.Apply(x => x.Name);
        var resourceGroupDatamartId = resourceGroupDatamartRef.Apply(x => x.Id);

        // retrieve security groups for end user permissions from AD
        var adSecurityGroupAdminRef = Output.Create(GetGroup.InvokeAsync(new GetGroupArgs
        {
            SecurityEnabled = true,
            DisplayName = config.Ad.AdminGroupName
        }));
        var adSecurityGroupAdminObjectId = adSecurityGroupAdminRef.Apply(x => x.Id) ?? throw new ArgumentNullException("Provide a valid admin group in your config!");
        var adSecurityGroupAdminName = adSecurityGroupAdminRef.Apply(x => x.DisplayName) ?? throw new ArgumentNullException("Provide a valid admin group in your config!");

        var adSecurityGroupUserRef = Output.Create(GetGroup.InvokeAsync(new GetGroupArgs
        {
            SecurityEnabled = true,
            DisplayName = config.Ad.UserGroupName
        }));
        var adSecurityGroupUserObjectId = adSecurityGroupUserRef.Apply(x => x.Id) ?? throw new ArgumentNullException("Provide a valid user group in your config!");
        
        // retrieve dependent datamart subnet
        var subnetRef = Output.Create(GetSubnet.InvokeAsync(new GetSubnetArgs
        {
            SubnetName = config.Network.SubnetName,
            VirtualNetworkName = config.Network.VirtualNetworkName,
            ResourceGroupName = config.Network.ResourceGroupName,
        }));
        var SubnetId = subnetRef.Apply(x => x.Id) ?? throw new ArgumentNullException("Provide a valid subnet in your config!");

        // Create Storage Account and containers
        var storageAccount = new StorageAccount(storageAccountName, new StorageAccountArgs
        {
            ResourceGroupName = resourceGroupDatamartName,
            AllowBlobPublicAccess = false,
            MinimumTlsVersion = MinimumTlsVersion.TLS1_2,
            Sku = new Pulumi.AzureNative.Storage.Inputs.SkuArgs
            {
                Name = Pulumi.AzureNative.Storage.SkuName.Standard_LRS
            },
            Kind = Kind.StorageV2,
            AccountName = storageAccountName,
            IsHnsEnabled = true,
            NetworkRuleSet = new Pulumi.AzureNative.Storage.Inputs.NetworkRuleSetArgs
            {
                DefaultAction = DefaultAction.Deny,
                Bypass = Bypass.None
            }
        });

        var containerImport = new BlobContainer(config.Storage.ContainerImportName, new BlobContainerArgs
        {
            AccountName = storageAccount.Name,
            PublicAccess = PublicAccess.None,
            ResourceGroupName = resourceGroupDatamartName,
            ContainerName = config.Storage.ContainerImportName
        }); 

        var containerCurated = new BlobContainer(config.Storage.ContainerCuratedName, new BlobContainerArgs
        {
            AccountName = storageAccount.Name,
            PublicAccess = PublicAccess.None,
            ResourceGroupName = resourceGroupDatamartName,
            ContainerName = config.Storage.ContainerCuratedName
        }); 

        var containerSpeed = new BlobContainer(config.Storage.ContainerSpeedName, new BlobContainerArgs
        {
            AccountName = storageAccount.Name,
            PublicAccess = PublicAccess.None,
            ResourceGroupName = resourceGroupDatamartName,
            ContainerName = config.Storage.ContainerSpeedName
        });

        // vulnerability storage account is always set staticly for now
        var containerVulnerability = new BlobContainer("vulnerability", new BlobContainerArgs
        {
            AccountName = storageAccount.Name,
            PublicAccess = PublicAccess.None,
            ResourceGroupName = resourceGroupDatamartName,
            ContainerName = "vulnerability"
        }); 

        // create sql server and database
        var sqlServer = new Server(sqlServerName, new ServerArgs
        {
            ResourceGroupName = resourceGroupDatamartName,
            ServerName = sqlServerName,
            AdministratorLogin = config.Sql.SqlServerAdmin,
            AdministratorLoginPassword = config.Sql.SqlServerPassword ?? throw new ArgumentNullException("Provide a Password for SQL server!"),
            MinimalTlsVersion = "1.2",
            PublicNetworkAccess = ServerPublicNetworkAccess.Disabled,
            Identity = new Pulumi.AzureNative.Sql.Inputs.ResourceIdentityArgs
            {
                Type = Pulumi.AzureNative.Sql.IdentityType.SystemAssigned
            },
        });

        var serverAzureAdAdministrator = new ServerAzureADAdministrator("ActiveDirectory",new ServerAzureADAdministratorArgs
        {
            AdministratorName = "ActiveDirectory",
            AdministratorType = AdministratorType.ActiveDirectory,
            Login = adSecurityGroupAdminName,
            ServerName = sqlServer.Name,
            ResourceGroupName = resourceGroupDatamartName,
            TenantId = config.General.TenantId ?? throw new ArgumentNullException("Provide a tenantId for this deployment!"),
            Sid = adSecurityGroupAdminObjectId
        });

        var serverAzureAdOnlyAuth = new ServerAzureADOnlyAuthentication("Default", new ServerAzureADOnlyAuthenticationArgs
        {
            AuthenticationName = "Default",
            AzureADOnlyAuthentication = true,
            ResourceGroupName = resourceGroupDatamartName,
            ServerName = sqlServer.Name
        },
        new CustomResourceOptions
        { 
            DependsOn = {serverAzureAdAdministrator} 
        });

        var sqlDatabaseStaging = new Database(config.Sql.SqlDatabaseNameStaging, new DatabaseArgs
        {
            ResourceGroupName = resourceGroupDatamartName,
            DatabaseName = config.Sql.SqlDatabaseNameStaging,
            ServerName = sqlServer.Name,
            Sku = new Pulumi.AzureNative.Sql.Inputs.SkuArgs
            {
                Name = config.Sql.SqlDatabaseTier
            }
        });

        var sqlDatabaseBusiness = new Database(config.Sql.SqlDatabaseNameBusiness, new DatabaseArgs
        {
            ResourceGroupName = resourceGroupDatamartName,
            DatabaseName = config.Sql.SqlDatabaseNameBusiness,
            ServerName = sqlServer.Name,
            Sku = new Pulumi.AzureNative.Sql.Inputs.SkuArgs
            {
                Name = config.Sql.SqlDatabaseTier
            }
        });

        var sqlDatabaseIntegration = new Database(config.Sql.SqlDatabaseNameIntegration, new DatabaseArgs
        {
            ResourceGroupName = resourceGroupDatamartName,
            DatabaseName = config.Sql.SqlDatabaseNameIntegration,
            ServerName = sqlServer.Name,
            Sku = new Pulumi.AzureNative.Sql.Inputs.SkuArgs
            {
                Name = config.Sql.SqlDatabaseTier
            }
        });

        // add vulnerability assessment to sql server
        var sqlServerVulnerabilityAssessment = new ServerVulnerabilityAssessment("sqlServerVulnerabilityAssessment", new ServerVulnerabilityAssessmentArgs
        {
            RecurringScans = new Pulumi.AzureNative.Sql.Inputs.VulnerabilityAssessmentRecurringScansPropertiesArgs
            {
                EmailSubscriptionAdmins = true,
                IsEnabled = true,
            },
            ResourceGroupName = resourceGroupDatamartName,
            ServerName = sqlServer.Name,
            StorageContainerPath = $"{storageAccount.PrimaryEndpoints}/{containerVulnerability.Name}",  //"https://myStorage.blob.core.windows.net/vulnerability-assessment/",
            VulnerabilityAssessmentName = "default"
        });


        // create data factory
        var dataFactory = new Factory(dataFactoryName, new FactoryArgs
        {
            FactoryName = dataFactoryName,
            Identity = new Pulumi.AzureNative.DataFactory.Inputs.FactoryIdentityArgs
            {
                Type = Pulumi.AzureNative.DataFactory.FactoryIdentityType.SystemAssigned
            },
            ResourceGroupName = resourceGroupDatamartName,
            PublicNetworkAccess = PublicNetworkAccess.Disabled
        });

        // create endpoints
        var privateEndpointSql = new PrivateEndpoint(privateEndpointSqlName, new PrivateEndpointArgs
        {
            PrivateEndpointName = privateEndpointSqlName,
            
            PrivateLinkServiceConnections = 
            {
                new PrivateLinkServiceConnectionArgs
                {
                    GroupIds = 
                    {
                        "sqlServer"
                    },
                    PrivateLinkServiceConnectionState = new PrivateLinkServiceConnectionStateArgs
                    {
                        ActionsRequired = "None",
                        Description = "Auto-approved",
                        Status = "Approved"
                    },
                    PrivateLinkServiceId = sqlServer.Id,
                    Name = "PrivateLinkSql"
                }
            },
            ResourceGroupName = resourceGroupDatamartName,
            Subnet = new Pulumi.AzureNative.Network.Inputs.SubnetArgs
            {
                Id = SubnetId,
            }
        });

        var privateEndpointBlob = new PrivateEndpoint(privateEndpointBlobName, new PrivateEndpointArgs
        {
            PrivateEndpointName = privateEndpointBlobName,
            
            PrivateLinkServiceConnections = 
            {
                new PrivateLinkServiceConnectionArgs
                {
                    GroupIds = 
                    {
                        "blob"
                    },
                    PrivateLinkServiceConnectionState = new PrivateLinkServiceConnectionStateArgs
                    {
                        ActionsRequired = "None",
                        Description = "Auto-approved",
                        Status = "Approved"
                    },
                    PrivateLinkServiceId = storageAccount.Id,
                    Name = "PrivateLinkBlob"
                }
            },
            ResourceGroupName = resourceGroupDatamartName,
            Subnet = new Pulumi.AzureNative.Network.Inputs.SubnetArgs
            {
                Id = SubnetId,
            }
        });

        var privateEndpointAdf = new PrivateEndpoint(privateEndpointAdfName, new PrivateEndpointArgs
        {
            PrivateEndpointName = privateEndpointAdfName,
            
            PrivateLinkServiceConnections = 
            {
                new PrivateLinkServiceConnectionArgs
                {
                    GroupIds = 
                    {
                        "dataFactory"
                    },
                    PrivateLinkServiceConnectionState = new PrivateLinkServiceConnectionStateArgs
                    {
                        ActionsRequired = "None",
                        Description = "Auto-approved",
                        Status = "Approved"
                    },
                    PrivateLinkServiceId = dataFactory.Id,
                    Name = "PrivateLinkAdf"
                }
            },
            ResourceGroupName = resourceGroupDatamartName,
            Subnet = new Pulumi.AzureNative.Network.Inputs.SubnetArgs
            {
                Id = SubnetId,
            }
        });

        // create authorizations
        var authAdfToSqlName = new RandomUuid("authAdfToSqlName");
        var authAdfToSql = new RoleAssignment("authAdfToSql", new RoleAssignmentArgs
        {
            PrincipalId = dataFactory.Identity.Apply(x => x.PrincipalId),
            PrincipalType = "ServicePrincipal",
            RoleAssignmentName = authAdfToSqlName.Result,
            RoleDefinitionId = StackExtensions.RetrieveRoleDefinitionId("Contributor", subscription),
            Scope = sqlServer.Id,
        });

        var authAdfToStorageName = new RandomUuid("authAdfToStorageName");
        var authAdfToStorage = new RoleAssignment("authAdfToStorage", new RoleAssignmentArgs
        {
            PrincipalId = dataFactory.Identity.Apply(x => x.PrincipalId),
            PrincipalType = "ServicePrincipal",
            RoleAssignmentName = authAdfToStorageName.Result,
            RoleDefinitionId = StackExtensions.RetrieveRoleDefinitionId("Storage Blob Data Contributor", subscription),
            Scope = storageAccount.Id,
        });

        var authSqlToStorageName = new RandomUuid("authSqlToStorageName");
        var authSqlToStorage = new RoleAssignment("authSqlToStorage", new RoleAssignmentArgs
        {
            PrincipalId = sqlServer.Identity.Apply(x => x.PrincipalId),
            PrincipalType = "ServicePrincipal",
            RoleAssignmentName = authSqlToStorageName.Result,
            RoleDefinitionId = StackExtensions.RetrieveRoleDefinitionId("Storage Blob Data Contributor", subscription),
            Scope = storageAccount.Id,
        });

        var authAdminGroupToStorageName1 = new RandomUuid("authAdminGroupToStorageName1");
        var authAdminGroupToStorage1 = new RoleAssignment("authAdminGroupToStorage1", new RoleAssignmentArgs
        {
            PrincipalId = adSecurityGroupAdminObjectId,
            PrincipalType = "Group",
            RoleAssignmentName = authAdminGroupToStorageName1.Result,
            RoleDefinitionId = StackExtensions.RetrieveRoleDefinitionId("Storage Blob Data Owner", subscription),
            Scope = storageAccount.Id,
        });

        var authAdminGroupToResourceGroupName = new RandomUuid("authAdminGroupToResourceGroupName");
        var authAdminGroupToResourceGroup = new RoleAssignment("authAdminGroupToResourceGroup", new RoleAssignmentArgs
        {
            PrincipalId = adSecurityGroupAdminObjectId,
            PrincipalType = "Group",
            RoleAssignmentName = authAdminGroupToResourceGroupName.Result,
            RoleDefinitionId = StackExtensions.RetrieveRoleDefinitionId("Contributor", subscription),
            Scope = resourceGroupDatamartId,
        });

        var authUserGroupToStorageName = new RandomUuid("authUserGroupToStorageName");
        var authUserGroupToStorage = new RoleAssignment("authUserGroupToStorage", new RoleAssignmentArgs
        {
            PrincipalId = adSecurityGroupUserObjectId,
            PrincipalType = "Group",
            RoleAssignmentName = authUserGroupToStorageName.Result,
            RoleDefinitionId = StackExtensions.RetrieveRoleDefinitionId("Storage Blob Data Reader", subscription),
            Scope = storageAccount.Id,
        });
    }
}
