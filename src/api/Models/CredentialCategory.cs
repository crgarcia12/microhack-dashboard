namespace Api.Models;

public class CredentialCategory
{
    public string Name { get; set; } = string.Empty;
    public List<CredentialItem> Credentials { get; set; } = new();
}
