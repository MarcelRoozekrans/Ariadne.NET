using Daedalus.Domain;

namespace Daedalus.Application.Planning;

public class AgentPlanner
{
    public string Plan(Agent agent, string goal) => goal + agent.Name;
}
