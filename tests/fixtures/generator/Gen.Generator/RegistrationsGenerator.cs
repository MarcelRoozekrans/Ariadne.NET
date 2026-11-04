using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Gen.Generator;

[Generator]
public sealed class RegistrationsGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context) =>
        context.RegisterPostInitializationOutput(static output =>
            output.AddSource(
                "Registrations.cs",
                SourceText.From("namespace Gen.App.Domain; class Registrations { Gen.App.Infrastructure.Store? S; }")));
}
