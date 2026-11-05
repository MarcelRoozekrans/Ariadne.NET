namespace Daedalus.Application.Agents;

public class SubagentRunExecutor
{
    private readonly ISubagentRunner _runner;

    public SubagentRunExecutor(ISubagentRunner runner) => _runner = runner;

    public void Execute(string name) => _runner.Run(name);
}
