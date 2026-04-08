namespace FreeCICD;

public partial class Program
{
    /// <summary>
    /// Loads FreeCICD-specific configuration from appsettings.json
    /// Called from ConfigurationHelpersLoadApp in Program.App.cs
    /// </summary>
    public static ConfigurationHelperLoader MyConfigurationHelpersLoadApp(
        ConfigurationHelperLoader loader, 
        WebApplicationBuilder builder)
    {
        var output = loader;

        // Load Azure DevOps configuration
        output.PAT = builder.Configuration.GetValue<string>("App:AzurePAT");
        output.ProjectId = builder.Configuration.GetValue<string>("App:AzureProjectId");
        output.RepoId = builder.Configuration.GetValue<string>("App:AzureRepoId");
        output.Branch = builder.Configuration.GetValue<string>("App:AzureBranch");
        output.OrgName = builder.Configuration.GetValue<string>("App:AzureOrgName");

        return output;
    }
}
