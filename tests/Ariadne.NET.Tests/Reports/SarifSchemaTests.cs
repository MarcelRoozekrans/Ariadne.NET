using FluentAssertions;
using Xunit;

namespace Ariadne.NET.Tests.Reports;

public sealed class SarifSchemaTests
{
    private const string Minimal = """{"version":"2.1.0","runs":[{"tool":{"driver":{"name":"Ariadne.NET"}}}]}""";

    [Fact]
    public void A_minimal_sarif_document_is_valid() =>
        SarifSchema.Validate(Minimal).Should().BeEmpty();

    [Fact]
    public void A_document_without_version_is_rejected() =>
        SarifSchema.Validate("""{"runs":[{"tool":{"driver":{"name":"Ariadne.NET"}}}]}""").Should().NotBeEmpty();

    [Fact]
    public void A_document_with_a_wrong_version_is_rejected() =>
        SarifSchema.Validate(Minimal.Replace("2.1.0", "1.0.0", StringComparison.Ordinal)).Should().NotBeEmpty();
}
