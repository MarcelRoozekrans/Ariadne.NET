using Daedalus.Application.Agents;

namespace Daedalus.Api.Controllers;

public class AgentsController
{
    private readonly ISubagentRunner _runner;

    public AgentsController(ISubagentRunner runner) => _runner = runner;

    public void Run(string name) => _runner.Run(name);
}
