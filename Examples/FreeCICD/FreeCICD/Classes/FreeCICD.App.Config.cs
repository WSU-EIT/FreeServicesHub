namespace FreeCICD;

// FreeCICD-specific configuration properties for Azure DevOps integration
public partial interface IConfigurationHelper
{
    public string? PAT { get; }
    public string? ProjectId { get; }
    public string? RepoId { get; }
    public string? Branch { get; }
    public string? OrgName { get; }
}

public partial class ConfigurationHelper : IConfigurationHelper
{
    public string? PAT {
        get {
            return _loader.PAT;
        }
    }

    public string? ProjectId {
        get {
            return _loader.ProjectId;
        }
    }

    public string? RepoId {
        get {
            return _loader.RepoId;
        }
    }

    public string? Branch {
        get {
            return _loader.Branch;
        }
    }

    public string? OrgName {
        get {
            return _loader.OrgName;
        }
    }
}

public partial class ConfigurationHelperLoader
{
    public string? PAT { get; set; }
    public string? ProjectId { get; set; }
    public string? RepoId { get; set; }
    public string? Branch { get; set; }
    public string? OrgName { get; set; }
}
