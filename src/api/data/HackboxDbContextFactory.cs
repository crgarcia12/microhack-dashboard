using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Api.Data;

/// <summary>
/// Design-time factory for EF Core migrations tooling.
/// Used by `dotnet ef migrations add` commands.
/// </summary>
public class HackboxDbContextFactory : IDesignTimeDbContextFactory<HackboxDbContext>
{
    public HackboxDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<HackboxDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=hackbox;Username=postgres;Password=postgres");
        return new HackboxDbContext(optionsBuilder.Options);
    }
}
