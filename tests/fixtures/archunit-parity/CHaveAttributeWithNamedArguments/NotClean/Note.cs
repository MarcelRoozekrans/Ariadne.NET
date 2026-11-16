using Parity.CHaveAttributeWithNamedArguments.Contracts;

namespace Parity.CHaveAttributeWithNamedArguments.NotClean;

// The pair sits on another attribute than TagAttribute, so the attribute-specific form leaves this type alone.
[Note(Area = "billing")]
public class Note
{
    public int Total { get; set; }
}
