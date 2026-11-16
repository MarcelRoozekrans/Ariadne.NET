using Parity.PHaveAttributeWithNamedArguments.Contracts;
using Parity.PHaveAttributeWithNamedArguments.Target;

namespace Parity.PHaveAttributeWithNamedArguments.NotClean;

[Tag("notes", 3, Area = "billing")]
[Tag("notes", 4, Audited = true)]
public class Note
{
    public Repository? Store;
}
