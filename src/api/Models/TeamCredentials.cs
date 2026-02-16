namespace Api.Models;

public class TeamCredentials
{
    public string TeamName { get; set; } = string.Empty;
    public List<CredentialCategory> Categories { get; set; } = new();
}

public class CredentialsFile
{
    public List<TeamCredentials> Teams { get; set; } = new();
}
