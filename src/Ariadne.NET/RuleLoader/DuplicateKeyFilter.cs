using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace Ariadne.NET.RuleLoader;

/// <summary>
/// Passes parser events through, but drops the second and later occurrence of a scalar key in a mapping together
/// with its value, recording it by the mapping's start. The representation model would otherwise stop at the first
/// duplicate with an exception and no other problem would be reported.
/// </summary>
internal sealed class DuplicateKeyFilter(IParser inner) : IParser
{
    private readonly Stack<Frame> _frames = new();

    /// <summary>The repeated keys, by the start of the mapping that holds them.</summary>
    public Dictionary<(int Line, int Column), List<DuplicateKey>> Duplicates { get; } = [];

    public ParsingEvent? Current => inner.Current;

    /// <summary>The 1-based line and column of a mark.</summary>
    /// <param name="mark">The mark.</param>
    /// <returns>The line and column.</returns>
    public static (int Line, int Column) Position(Mark mark) => (checked((int)mark.Line), checked((int)mark.Column));

    public bool MoveNext()
    {
        while (inner.MoveNext())
        {
            var current = inner.Current;
            if (current is Scalar key
                && _frames.TryPeek(out var frame)
                && frame.IsMapping
                && frame.ExpectKey
                && !frame.Keys.Add(key.Value))
            {
                Record(frame.Start, key);
                SkipNode();
                continue;
            }

            Track(current);
            return true;
        }

        return false;
    }

    private void Record(Mark mappingStart, Scalar key)
    {
        var at = Position(mappingStart);
        if (!Duplicates.TryGetValue(at, out var list))
        {
            list = [];
            Duplicates[at] = list;
        }

        var (line, column) = Position(key.Start);
        list.Add(new DuplicateKey(key.Value, line, column));
    }

    // Consumes the next node, a scalar, an alias or a whole collection.
    private void SkipNode()
    {
        var depth = 0;
        while (inner.MoveNext())
        {
            switch (inner.Current)
            {
                case MappingStart or SequenceStart:
                    depth++;
                    break;
                case MappingEnd or SequenceEnd:
                    depth--;
                    break;
            }

            if (depth == 0)
            {
                return;
            }
        }
    }

    private void Track(ParsingEvent? current)
    {
        switch (current)
        {
            case DocumentStart:
                _frames.Clear();
                break;
            case MappingStart mapping:
                _frames.Push(new Frame(isMapping: true, mapping.Start));
                break;
            case SequenceStart sequence:
                _frames.Push(new Frame(isMapping: false, sequence.Start));
                break;
            case MappingEnd or SequenceEnd:
                _frames.Pop();
                Completed();
                break;
            case Scalar or AnchorAlias:
                Completed();
                break;
        }
    }

    // A node finished: in a mapping, a key is followed by its value and a value by the next key.
    private void Completed()
    {
        if (_frames.TryPeek(out var frame) && frame.IsMapping)
        {
            frame.ExpectKey = !frame.ExpectKey;
        }
    }

    /// <summary>A repeated key at the position of its second or later occurrence.</summary>
    /// <param name="Key">The key.</param>
    /// <param name="Line">The 1-based line of the repeated key.</param>
    /// <param name="Column">The 1-based column of the repeated key.</param>
    internal readonly record struct DuplicateKey(string Key, int Line, int Column);

    private sealed class Frame(bool isMapping, Mark start)
    {
        public bool IsMapping { get; } = isMapping;

        public Mark Start { get; } = start;

        public bool ExpectKey { get; set; } = true;

        public HashSet<string> Keys { get; } = new(StringComparer.Ordinal);
    }
}
