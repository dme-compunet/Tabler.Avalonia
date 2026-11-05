using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace Tabler.Avalonia;

/// <summary>
/// Lossless compact binary encoding of icon path data (the content of <c>icons.bin</c> before Brotli).
/// </summary>
/// <remarks>
/// <para>
/// This file is shared with the icon generator (linked into Generator.Icons), so the encoder and
/// the decoder always use the same prediction logic.
/// </para>
/// <para>
/// Path data is expected in the canonical form produced by the generator: commands M/L/C/Q/Z,
/// numbers with at most two decimals, numbers separated by single spaces, and fills prefixed
/// with the fill rule ("F0 " / "F1 "). Coordinates are stored as integers (value × 100), as the
/// difference from a predicted value, which makes them tiny and highly compressible:
/// </para>
/// <list type="bullet">
/// <item>M/L/C points are predicted by the current point.</item>
/// <item>A Q control point following another Q is predicted by reflecting the previous control
/// point (Tabler arcs are converted to smooth chains of quadratic curves).</item>
/// <item>A Q end point is predicted as symmetric to the start point around the control point.</item>
/// </list>
/// <para>
/// Layout: int32 icon count, int32 header length, int32 command length, then the header section
/// (per icon: fill count, then per path: segment count [+ fill rule byte]), the command section
/// (one ASCII byte per segment) and the number section (zigzag varints).
/// Keeping the three kinds of data in separate sections compresses noticeably better.
/// </para>
/// </remarks>
internal static class IconPathCodec
{
    private const int Scale = 100;
    private const string FillRulePrefix = "F";

    public static byte[] Encode(IReadOnlyList<(string Stroke, string[] Fills)> icons)
    {
        var header = new List<byte>();
        var commands = new List<byte>();
        var numbers = new NumberEncoder(new List<byte>());

        foreach (var (stroke, fills) in icons)
        {
            WriteVarint(header, (uint)fills.Length);

            EncodePath(stroke, header, commands, ref numbers);

            foreach (var fill in fills)
            {
                if (fill.Length < 3 || !fill.StartsWith(FillRulePrefix, StringComparison.Ordinal) || fill[1] is not ('0' or '1') || fill[2] != ' ')
                {
                    throw new FormatException($"Fill path must start with 'F0 ' or 'F1 ': '{Truncate(fill)}'.");
                }

                EncodePath(fill[3..], header, commands, ref numbers, fillRule: (byte)(fill[1] - '0'));
            }
        }

        var result = new byte[12 + header.Count + commands.Count + numbers.Output.Count];
        var span = result.AsSpan();

        BinaryPrimitives.WriteInt32LittleEndian(span, icons.Count);
        BinaryPrimitives.WriteInt32LittleEndian(span[4..], header.Count);
        BinaryPrimitives.WriteInt32LittleEndian(span[8..], commands.Count);

        header.CopyTo(result, 12);
        commands.CopyTo(result, 12 + header.Count);
        numbers.Output.CopyTo(result, 12 + header.Count + commands.Count);

        return result;
    }

    /// <summary>
    /// Indexes encoded data so individual icons can be decoded on demand
    /// (only the compact binary stays in memory, not the path strings of all icons).
    /// </summary>
    public static IconPathTable Open(byte[] data) => new(data);

    /// <summary>Encoded icon data with a per-icon index; decoding is thread-safe.</summary>
    public sealed class IconPathTable
    {
        private readonly byte[] _data;
        private readonly int _headerStart, _commandStart, _numberStart;

        // Per icon: start positions inside the header, command and number sections.
        private readonly int[] _offsets;

        internal IconPathTable(byte[] data)
        {
            var span = data.AsSpan();
            Count = BinaryPrimitives.ReadInt32LittleEndian(span);
            var headerLength = BinaryPrimitives.ReadInt32LittleEndian(span[4..]);
            var commandLength = BinaryPrimitives.ReadInt32LittleEndian(span[8..]);

            _data = data;
            _headerStart = 12;
            _commandStart = _headerStart + headerLength;
            _numberStart = _commandStart + commandLength;
            _offsets = new int[Count * 3];

            var header = span[_headerStart.._commandStart];
            var commands = span[_commandStart.._numberStart];
            var numbers = span[_numberStart..];

            int headerPosition = 0, commandPosition = 0, numberPosition = 0;

            for (var index = 0; index < Count; index++)
            {
                _offsets[index * 3] = headerPosition;
                _offsets[index * 3 + 1] = commandPosition;
                _offsets[index * 3 + 2] = numberPosition;

                var pathCount = 1 + ReadVarint(header, ref headerPosition);

                for (var path = 0; path < pathCount; path++)
                {
                    var segmentCount = ReadVarint(header, ref headerPosition);

                    if (path > 0)
                    {
                        headerPosition++; // fill rule
                    }

                    var valueCount = 0;

                    foreach (var command in commands.Slice(commandPosition, segmentCount))
                    {
                        valueCount += GetArgumentCount((char)command);
                    }

                    commandPosition += segmentCount;

                    // Skip the varints: each one ends with a byte that has the high bit clear.
                    while (valueCount > 0)
                    {
                        if (numbers[numberPosition++] < 0x80)
                        {
                            valueCount--;
                        }
                    }
                }
            }

            if (headerPosition != header.Length || commandPosition != commands.Length || numberPosition != numbers.Length)
            {
                throw new InvalidDataException("Unexpected trailing data in the icon binary.");
            }
        }

        [ThreadStatic]
        private static StringBuilder? t_builder;

        public int Count { get; }

        public (string Stroke, string[] Fills) Decode(int index)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Count);

            var span = _data.AsSpan();
            var header = span[_headerStart.._commandStart];
            var commands = span[_commandStart.._numberStart];
            var numbers = new NumberDecoder(span[_numberStart..], _offsets[index * 3 + 2]);

            var headerPosition = _offsets[index * 3];
            var commandPosition = _offsets[index * 3 + 1];

            var builder = t_builder ??= new StringBuilder(4096);
            var fills = new string[ReadVarint(header, ref headerPosition)];

            var stroke = DecodePath(header, ref headerPosition, commands, ref commandPosition, ref numbers, builder, isFill: false);

            for (var i = 0; i < fills.Length; i++)
            {
                fills[i] = DecodePath(header, ref headerPosition, commands, ref commandPosition, ref numbers, builder, isFill: true);
            }

            return (stroke, fills);
        }
    }

    #region Encoding

    private static void EncodePath(string path, List<byte> header, List<byte> commands, ref NumberEncoder numbers, byte? fillRule = null)
    {
        var segments = ParsePath(path);

        WriteVarint(header, (uint)segments.Count);

        if (fillRule is { } rule)
        {
            header.Add(rule);
        }

        var state = new PathState();

        foreach (var (command, values) in segments)
        {
            commands.Add((byte)command);
            CodeSegment(ref numbers, command, values, ref state);
        }
    }

    private static List<(char Command, int[] Values)> ParsePath(string path)
    {
        var segments = new List<(char, int[])>();
        var position = 0;

        while (position < path.Length)
        {
            var command = path[position++];
            var values = new int[GetArgumentCount(command)];

            for (var i = 0; i < values.Length; i++)
            {
                if (i > 0)
                {
                    if (position >= path.Length || path[position] != ' ')
                    {
                        throw new FormatException($"Expected a space at {position} in '{Truncate(path)}'.");
                    }

                    position++;
                }

                var start = position;

                while (position < path.Length && (char.IsAsciiDigit(path[position]) || path[position] is '-' or '.'))
                {
                    position++;
                }

                var number = decimal.Parse(path.AsSpan(start, position - start), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture) * Scale;

                if (number != decimal.Truncate(number))
                {
                    throw new FormatException($"Numbers must have at most two decimals in '{Truncate(path)}'.");
                }

                values[i] = (int)number;
            }

            segments.Add((command, values));
        }

        return segments;
    }

    private static void WriteVarint(List<byte> output, uint value)
    {
        while (value >= 0x80)
        {
            output.Add((byte)(value | 0x80));
            value >>= 7;
        }

        output.Add((byte)value);
    }

    private struct NumberEncoder(List<byte> output) : ICoder
    {
        public List<byte> Output { get; } = output;

        public int Code(int value, int predicted)
        {
            var delta = value - predicted;
            WriteVarint(Output, (uint)((delta << 1) ^ (delta >> 31)));
            return value;
        }
    }

    #endregion

    #region Decoding

    private static string DecodePath(ReadOnlySpan<byte> header, ref int headerPosition, ReadOnlySpan<byte> commands, ref int commandPosition, ref NumberDecoder numbers, StringBuilder builder, bool isFill)
    {
        var segmentCount = ReadVarint(header, ref headerPosition);

        builder.Clear();

        if (isFill)
        {
            builder.Append(FillRulePrefix).Append((char)('0' + header[headerPosition++])).Append(' ');
        }

        var state = new PathState();
        Span<int> values = stackalloc int[6];

        for (var i = 0; i < segmentCount; i++)
        {
            var command = (char)commands[commandPosition++];
            var segmentValues = values[..GetArgumentCount(command)];

            CodeSegment(ref numbers, command, segmentValues, ref state);

            builder.Append(command);

            for (var j = 0; j < segmentValues.Length; j++)
            {
                if (j > 0)
                {
                    builder.Append(' ');
                }

                AppendNumber(builder, segmentValues[j]);
            }
        }

        return builder.ToString();
    }

    /// <summary>Formats value / 100 exactly like <c>double.ToString("0.##")</c>.</summary>
    private static void AppendNumber(StringBuilder builder, int value)
    {
        if (value < 0)
        {
            builder.Append('-');
            value = -value;
        }

        var integer = value / Scale;

        if (integer < 10)
        {
            builder.Append((char)('0' + integer));
        }
        else if (integer < 100)
        {
            builder.Append((char)('0' + integer / 10)).Append((char)('0' + integer % 10));
        }
        else
        {
            builder.Append(integer);
        }

        var fraction = value % Scale;

        if (fraction != 0)
        {
            builder.Append('.').Append((char)('0' + fraction / 10));

            if (fraction % 10 != 0)
            {
                builder.Append((char)('0' + fraction % 10));
            }
        }
    }

    private static int ReadVarint(ReadOnlySpan<byte> data, ref int position)
    {
        uint result = 0;
        var shift = 0;
        byte current;

        do
        {
            current = data[position++];
            result |= (uint)(current & 0x7F) << shift;
            shift += 7;
        }
        while ((current & 0x80) != 0);

        return (int)result;
    }

    private ref struct NumberDecoder(ReadOnlySpan<byte> data, int position) : ICoder
    {
        private readonly ReadOnlySpan<byte> _data = data;
        private int _position = position;

        public int Code(int value, int predicted)
        {
            var encoded = (uint)ReadVarint(_data, ref _position);
            return predicted + ((int)(encoded >> 1) ^ -(int)(encoded & 1));
        }
    }

    #endregion

    #region Shared prediction

    private interface ICoder
    {
        /// <summary>Encoder: writes <paramref name="value"/>. Decoder: ignores it and returns the decoded value.</summary>
        int Code(int value, int predicted);
    }

    private struct PathState
    {
        public int X, Y, StartX, StartY, ControlX, ControlY;
        public char Previous;
    }

    private static int GetArgumentCount(char command) => command switch
    {
        'M' or 'L' => 2,
        'C' => 6,
        'Q' => 4,
        'Z' => 0,
        _ => throw new NotSupportedException($"Unsupported path command '{command}'."),
    };

    private static void CodeSegment<TCoder>(ref TCoder coder, char command, scoped Span<int> v, ref PathState s)
        where TCoder : ICoder, allows ref struct
    {
        switch (command)
        {
            case 'M':
                v[0] = coder.Code(v[0], s.X);
                v[1] = coder.Code(v[1], s.Y);
                s.X = s.StartX = v[0];
                s.Y = s.StartY = v[1];
                break;

            case 'L':
                v[0] = coder.Code(v[0], s.X);
                v[1] = coder.Code(v[1], s.Y);
                s.X = v[0];
                s.Y = v[1];
                break;

            case 'C':
                for (var i = 0; i < 6; i += 2)
                {
                    v[i] = coder.Code(v[i], s.X);
                    v[i + 1] = coder.Code(v[i + 1], s.Y);
                }

                s.X = v[4];
                s.Y = v[5];
                break;

            case 'Q':
                var smooth = s.Previous == 'Q';
                v[0] = coder.Code(v[0], smooth ? 2 * s.X - s.ControlX : s.X);
                v[1] = coder.Code(v[1], smooth ? 2 * s.Y - s.ControlY : s.Y);
                v[2] = coder.Code(v[2], 2 * v[0] - s.X);
                v[3] = coder.Code(v[3], 2 * v[1] - s.Y);
                s.ControlX = v[0];
                s.ControlY = v[1];
                s.X = v[2];
                s.Y = v[3];
                break;

            case 'Z':
                s.X = s.StartX;
                s.Y = s.StartY;
                break;

            default:
                throw new NotSupportedException($"Unsupported path command '{command}'.");
        }

        s.Previous = command;
    }

    #endregion

    private static string Truncate(string value) => value.Length <= 60 ? value : value[..60] + "...";
}
