using Daedalus.Application.Agents;
using Daedalus.Infrastructure.Agents;

namespace Daedalus.Api.Composition;

public class AgentRegistration
{
    public ISubagentRunner Create() => new SubagentRunner();
}
