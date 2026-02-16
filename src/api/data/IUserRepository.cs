using Api.Models;

namespace Api.Data;

public interface IUserRepository
{
    List<User> GetAllUsers();
    User? GetUser(string username);
    void AddUser(User user);
    void UpdateUser(User user);
    void DeleteUser(string username);
    List<string> GetAllTeams();
    void AddTeam(string teamName);
    void DeleteTeam(string teamName);
    bool HasUsers();
    void SeedUsers(List<User> users);
}
