using Daedalus.Agents.Contracts;

namespace Daedalus.Application.Planning;

public class AgentPlanner
{
    private readonly AgentDescriptor _descriptor = new AgentDescriptor();

    public string Plan(string goal) => goal + _descriptor.Name;
}
