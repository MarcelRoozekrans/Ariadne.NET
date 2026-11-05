using Daedalus.Domain;

namespace Daedalus.Infrastructure.Persistence;

public class PersistenceMarker
{
    public AgentStore Store { get; } = new AgentStore();
}
