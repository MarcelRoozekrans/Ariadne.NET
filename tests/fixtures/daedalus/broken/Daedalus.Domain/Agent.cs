using Daedalus.Infrastructure.Persistence;

namespace Daedalus.Domain;

public class Agent
{
    private readonly PersistenceMarker _marker = new PersistenceMarker();

    public string Name { get; set; } = "";
}
