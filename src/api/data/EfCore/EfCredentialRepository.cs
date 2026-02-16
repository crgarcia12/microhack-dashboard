using Api.Data.Entities;
using Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Api.Data.EfCore;

public class EfCredentialRepository : ICredentialRepository
{
    private readonly HackboxDbContext _db;

    public EfCredentialRepository(HackboxDbContext db)
    {
        _db = db;
    }

    public TeamCredentials GetCredentials(string teamName)
    {
        var categories = _db.CredentialCategories
            .Include(c => c.Credentials)
            .AsNoTracking()
            .Where(c => c.TeamName == teamName)
            .OrderBy(c => c.Id)
            .ToList();

        return new TeamCredentials
        {
            TeamName = teamName,
            Categories = categories.Select(c => new CredentialCategory
            {
                Name = c.Name,
                Credentials = c.Credentials.Select(i => new CredentialItem
                {
                    Label = i.Label,
                    Value = i.Value
                }).ToList()
            }).ToList()
        };
    }

    public List<TeamCredentials> GetAllCredentials()
    {
        var categories = _db.CredentialCategories
            .Include(c => c.Credentials)
            .AsNoTracking()
            .OrderBy(c => c.TeamName)
            .ThenBy(c => c.Id)
            .ToList();

        return categories
            .GroupBy(c => c.TeamName)
            .Select(g => new TeamCredentials
            {
                TeamName = g.Key,
                Categories = g.Select(c => new CredentialCategory
                {
                    Name = c.Name,
                    Credentials = c.Credentials.Select(i => new CredentialItem
                    {
                        Label = i.Label,
                        Value = i.Value
                    }).ToList()
                }).ToList()
            }).ToList();
    }

    public void SaveCredentials(TeamCredentials credentials)
    {
        // Remove existing categories for this team
        var existing = _db.CredentialCategories
            .Include(c => c.Credentials)
            .Where(c => c.TeamName == credentials.TeamName)
            .ToList();

        _db.CredentialCategories.RemoveRange(existing);

        // Add new categories
        foreach (var category in credentials.Categories)
        {
            var entity = new CredentialCategoryEntity
            {
                TeamName = credentials.TeamName,
                Name = category.Name,
                Credentials = category.Credentials.Select(i => new CredentialItemEntity
                {
                    Label = i.Label,
                    Value = i.Value
                }).ToList()
            };
            _db.CredentialCategories.Add(entity);
        }

        _db.SaveChanges();
    }

    public void DeleteCredentials(string teamName)
    {
        var existing = _db.CredentialCategories
            .Include(c => c.Credentials)
            .Where(c => c.TeamName == teamName)
            .ToList();

        _db.CredentialCategories.RemoveRange(existing);
        _db.SaveChanges();
    }

    public bool HasCredentials()
    {
        return _db.CredentialCategories.Any();
    }

    public void SeedCredentials(List<TeamCredentials> teams)
    {
        foreach (var team in teams)
        {
            foreach (var category in team.Categories)
            {
                var entity = new CredentialCategoryEntity
                {
                    TeamName = team.TeamName,
                    Name = category.Name,
                    Credentials = category.Credentials.Select(i => new CredentialItemEntity
                    {
                        Label = i.Label,
                        Value = i.Value
                    }).ToList()
                };
                _db.CredentialCategories.Add(entity);
            }
        }
        _db.SaveChanges();
    }
}
