using Ariadne.NET.Abstractions;
using ModelContextProtocol;
using ZeroAlloc.Results;

namespace Ariadne.NET.Mcp;

/// <summary>
/// Maps a failed resolution onto the MCP tool error idiom.
/// </summary>
internal static class ToolError
{
    /// <summary>
    /// Returns the resolved pack, or throws an <see cref="McpException"/> carrying the error's message and detail.
    /// The SDK catches it inside the tool call and answers with a tool result that has <c>IsError</c> set and the
    /// text as content, so the model sees the reason; the exception never reaches the client.
    /// </summary>
    internal static RulePack ValueOrToolError(Result<RulePack, AriadneError> result)
    {
        if (result.IsSuccess)
        {
            return result.Value;
        }

        throw new McpException(ErrorText.Format(result.Error));
    }
}
