using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

/// <summary>
/// Pure, deterministic evaluator for StageChart time expressions. Lives in
/// BulletHell.Core so it is unit-testable without the editor and can be shared by
/// the compiler and any future runtime consumer.
///
/// Grammar (a single expression string):
///   expr    := base ( ws? ('+'|'-') ws? offset )*
///   base    := seconds | barBeat | markerRef
///   offset  := number unit?          (unit == beat|beats|bar|bars; no unit == seconds)
///   seconds := number                (absolute seconds)
///   barBeat := int ':' number        (1-based bar : 1-based beat, fractional beat ok)
///   markerRef := ident               (a key in the markers dictionary)
///   number  := [-+]? digits ('.' digits)?  |  [-+]? '.' digits
///   ident   := [A-Za-z_][A-Za-z0-9_]*
///
/// Bar/beat conversion uses BPM / measure / beat offset from the context. All
/// arithmetic is done in <see cref="double"/>; callers decide when to quantize to
/// float. Marker definitions may reference other markers; cycles are detected and
/// reported. Nothing here rounds to 6 digits — that is a compiler output concern.
/// </summary>
public static class ChartTimeExpr
{
    public sealed class ChartTimeException : Exception
    {
        public ChartTimeException(string message) : base(message) { }
    }

    /// <summary>
    /// Immutable-ish evaluation context. Holds the beat grid plus the named marker
    /// table; caches resolved marker seconds and tracks the active resolution stack
    /// so cyclic marker definitions surface as an error instead of a stack overflow.
    /// </summary>
    public sealed class Context
    {
        public readonly double Bpm;
        public readonly int Measure;
        public readonly double BeatOffsetSec;
        private readonly Dictionary<string, string> _markers;
        private readonly Dictionary<string, double> _memo = new Dictionary<string, double>(StringComparer.Ordinal);
        private readonly HashSet<string> _resolving = new HashSet<string>(StringComparer.Ordinal);

        public Context(double bpm, int measure, double beatOffsetSec, IReadOnlyDictionary<string, string> markers)
        {
            if (bpm <= 0.0)
            {
                throw new ChartTimeException($"BPM must be positive (got {bpm.ToString(CultureInfo.InvariantCulture)}).");
            }
            if (measure <= 0)
            {
                throw new ChartTimeException($"measure (beats per bar) must be positive (got {measure}).");
            }
            Bpm = bpm;
            Measure = measure;
            BeatOffsetSec = beatOffsetSec;
            _markers = new Dictionary<string, string>(StringComparer.Ordinal);
            if (markers != null)
            {
                foreach (KeyValuePair<string, string> kv in markers)
                {
                    _markers[kv.Key] = kv.Value;
                }
            }
        }

        public double BeatSeconds => 60.0 / Bpm;
        public double BarSeconds => Measure * BeatSeconds;

        public bool HasMarker(string name) => _markers.ContainsKey(name);

        internal double ResolveMarker(string name)
        {
            if (_memo.TryGetValue(name, out double cached))
            {
                return cached;
            }
            if (!_markers.TryGetValue(name, out string expr))
            {
                throw new ChartTimeException($"Unknown marker '{name}'.");
            }
            if (!_resolving.Add(name))
            {
                throw new ChartTimeException($"Cyclic marker reference detected at '{name}'.");
            }
            try
            {
                double value = Evaluate(expr, this);
                _memo[name] = value;
                return value;
            }
            finally
            {
                _resolving.Remove(name);
            }
        }
    }

    /// <summary>Evaluates a time expression to absolute seconds.</summary>
    public static double Evaluate(string expr, Context ctx)
    {
        if (ctx == null)
        {
            throw new ChartTimeException("Context is null.");
        }
        if (string.IsNullOrWhiteSpace(expr))
        {
            throw new ChartTimeException("Empty time expression.");
        }

        var p = new Parser(expr);
        double value = ParseBase(ref p, ctx);
        while (true)
        {
            p.SkipWs();
            if (p.Eof)
            {
                break;
            }
            char op = p.Peek();
            if (op != '+' && op != '-')
            {
                throw new ChartTimeException($"Unexpected character '{op}' in expression '{expr}'.");
            }
            p.Next();
            p.SkipWs();
            double term = ParseOffset(ref p, ctx, expr);
            value = op == '+' ? value + term : value - term;
        }
        return value;
    }

    private static double ParseBase(ref Parser p, Context ctx)
    {
        p.SkipWs();
        if (p.Eof)
        {
            throw new ChartTimeException("Expression ended before a base term.");
        }

        char c = p.Peek();
        if (IsIdentStart(c))
        {
            string ident = p.ReadIdent();
            return ctx.ResolveMarker(ident);
        }

        // number, possibly bar:beat.
        double first = p.ReadNumber();
        p.SkipWs();
        if (!p.Eof && p.Peek() == ':')
        {
            p.Next();
            p.SkipWs();
            double beat = p.ReadNumber();
            // 1-based bar and beat.
            double totalBeats = (first - 1.0) * ctx.Measure + (beat - 1.0);
            return ctx.BeatOffsetSec + totalBeats * ctx.BeatSeconds;
        }

        // Bare number == absolute seconds.
        return first;
    }

    private static double ParseOffset(ref Parser p, Context ctx, string expr)
    {
        double amount = p.ReadNumber();
        p.SkipWs();
        string unit = p.ReadIdentOptional();
        if (string.IsNullOrEmpty(unit))
        {
            return amount; // seconds
        }
        switch (unit.ToLowerInvariant())
        {
            case "beat":
            case "beats":
                return amount * ctx.BeatSeconds;
            case "bar":
            case "bars":
                return amount * ctx.BarSeconds;
            case "sec":
            case "secs":
            case "s":
                return amount;
            default:
                throw new ChartTimeException($"Unknown unit '{unit}' in expression '{expr}'.");
        }
    }

    private static bool IsIdentStart(char c) => char.IsLetter(c) || c == '_';

    private struct Parser
    {
        private readonly string _s;
        private int _i;

        public Parser(string s)
        {
            _s = s;
            _i = 0;
        }

        public bool Eof => _i >= _s.Length;
        public char Peek() => _s[_i];
        public void Next() => _i++;

        public void SkipWs()
        {
            while (_i < _s.Length && char.IsWhiteSpace(_s[_i]))
            {
                _i++;
            }
        }

        public string ReadIdent()
        {
            int start = _i;
            if (_i < _s.Length && (char.IsLetter(_s[_i]) || _s[_i] == '_'))
            {
                _i++;
                while (_i < _s.Length && (char.IsLetterOrDigit(_s[_i]) || _s[_i] == '_'))
                {
                    _i++;
                }
            }
            if (_i == start)
            {
                throw new ChartTimeException($"Expected identifier at position {start} in '{_s}'.");
            }
            return _s.Substring(start, _i - start);
        }

        public string ReadIdentOptional()
        {
            if (_i < _s.Length && (char.IsLetter(_s[_i]) || _s[_i] == '_'))
            {
                return ReadIdent();
            }
            return null;
        }

        public double ReadNumber()
        {
            int start = _i;
            if (_i < _s.Length && (_s[_i] == '+' || _s[_i] == '-'))
            {
                _i++;
            }
            bool anyDigit = false;
            while (_i < _s.Length && char.IsDigit(_s[_i]))
            {
                _i++;
                anyDigit = true;
            }
            if (_i < _s.Length && _s[_i] == '.')
            {
                _i++;
                while (_i < _s.Length && char.IsDigit(_s[_i]))
                {
                    _i++;
                    anyDigit = true;
                }
            }
            if (!anyDigit)
            {
                throw new ChartTimeException($"Expected number at position {start} in '{_s}'.");
            }
            string token = _s.Substring(start, _i - start);
            if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            {
                throw new ChartTimeException($"Malformed number '{token}' in '{_s}'.");
            }
            return value;
        }
    }
}
