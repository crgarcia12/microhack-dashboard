using Api.Models;

namespace Api.Services;

public interface ISolutionService
{
    IReadOnlyList<Solution> GetSolutions();
    Solution? GetSolution(int number);
}
