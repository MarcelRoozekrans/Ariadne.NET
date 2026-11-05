using Ariadne.NET.Abstractions;
using FluentAssertions;
using ZeroAlloc.Results;

namespace Ariadne.NET.Tests.Abstractions;

public class AriadneErrorTests
{
    [Fact]
    public void Default_result_is_a_failure_with_an_undefined_error_code()
    {
        var result = default(Result<int, AriadneError>);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be((AriadneErrorCode)0);
        Enum.IsDefined(result.Error.Code).Should().BeFalse();
    }

    [Fact]
    public void Codes_start_at_one() =>
        ((int)AriadneErrorCode.NotFound, (int)AriadneErrorCode.Invalid, (int)AriadneErrorCode.Unavailable)
            .Should().Be((1, 2, 3));

    [Fact]
    public void Failure_carries_code_message_and_detail()
    {
        var result = Result<int, AriadneError>.Failure(new AriadneError(AriadneErrorCode.Invalid, "bad", "why"));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(new AriadneError(AriadneErrorCode.Invalid, "bad", "why"));
    }
}
