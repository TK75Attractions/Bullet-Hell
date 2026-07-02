using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// Unit tests for the pure <see cref="ChartTimeExpr"/> evaluator: bar:beat, raw
/// seconds, marker references, relative beat/bar offsets (incl. negative and
/// fractional coefficients), nested marker definitions, and cyclic-reference
/// detection.
/// </summary>
public class ChartTimeExprTest
{
    private const double Bpm = 144.0;   // beat = 0.4166667s, bar (4/4) = 1.6666667s
    private const int Measure = 4;
    private const double Eps = 1e-9;

    private static ChartTimeExpr.Context Ctx(Dictionary<string, string> markers = null, double offset = 0.0)
    {
        return new ChartTimeExpr.Context(Bpm, Measure, offset, markers ?? new Dictionary<string, string>());
    }

    private static double Beat => 60.0 / Bpm;
    private static double Bar => Measure * Beat;

    [Test]
    public void BareSeconds()
    {
        Assert.AreEqual(12.5, ChartTimeExpr.Evaluate("12.5", Ctx()), Eps);
        Assert.AreEqual(0.0, ChartTimeExpr.Evaluate("0", Ctx()), Eps);
        Assert.AreEqual(63.750002, ChartTimeExpr.Evaluate("63.750002", Ctx()), Eps);
    }

    [Test]
    public void BarBeatIsOneBased()
    {
        // 1:1 is time zero (+offset).
        Assert.AreEqual(0.0, ChartTimeExpr.Evaluate("1:1", Ctx()), Eps);
        // 5:1 == 16 beats.
        Assert.AreEqual(16.0 * Beat, ChartTimeExpr.Evaluate("5:1", Ctx()), Eps);
        // 7:1 == 24 beats == 10.0s at 144 BPM.
        Assert.AreEqual(10.0, ChartTimeExpr.Evaluate("7:1", Ctx()), 1e-6);
        // fractional beat.
        Assert.AreEqual(80.75 * Beat, ChartTimeExpr.Evaluate("21:1.75", Ctx()), Eps);
    }

    [Test]
    public void BeatOffsetShiftsBarBeatButNotSeconds()
    {
        var ctx = Ctx(offset: 0.5);
        Assert.AreEqual(0.5, ChartTimeExpr.Evaluate("1:1", ctx), Eps);
        Assert.AreEqual(12.5, ChartTimeExpr.Evaluate("12.5", ctx), Eps); // seconds are absolute
    }

    [Test]
    public void MarkerReference()
    {
        var markers = new Dictionary<string, string> { { "M1", "5:1" }, { "M20", "37:1" } };
        Assert.AreEqual(16.0 * Beat, ChartTimeExpr.Evaluate("M1", Ctx(markers)), Eps);
        Assert.AreEqual(144.0 * Beat, ChartTimeExpr.Evaluate("M20", Ctx(markers)), Eps);
    }

    [Test]
    public void StoneSeekTargetsMatchVerificationValues()
    {
        // Pins the exact seek targets the P4 debug UI jumps to for the stone chart
        // (bpm 144, 4/4). These are the numbers used in the seek verification pass.
        var markers = new Dictionary<string, string> { { "M20", "37:1" }, { "M21", "39:1" } };
        Assert.AreEqual(60.0, ChartTimeExpr.Evaluate("M20", Ctx(markers)), 1e-6);       // 一斉落下(残置)
        Assert.AreEqual(63.333333, ChartTimeExpr.Evaluate("M21", Ctx(markers)), 1e-5);  // カッター粉砕・ゴーレム出現
    }

    [Test]
    public void RelativeBeatAndBarOffsets()
    {
        var markers = new Dictionary<string, string> { { "M20", "37:1" } };
        double m20 = 144.0 * Beat;
        Assert.AreEqual(m20 - Beat, ChartTimeExpr.Evaluate("M20 - 1beat", Ctx(markers)), Eps);
        Assert.AreEqual(m20 + 0.5 * Bar, ChartTimeExpr.Evaluate("M20 + 0.5bar", Ctx(markers)), Eps);
        Assert.AreEqual(m20 + 2.0 * Beat, ChartTimeExpr.Evaluate("M20 + 2beat", Ctx(markers)), Eps);
        // no-space form.
        Assert.AreEqual(m20 + 0.5 * Bar, ChartTimeExpr.Evaluate("M20+0.5bar", Ctx(markers)), Eps);
        // chained.
        Assert.AreEqual(m20 + Beat - Bar, ChartTimeExpr.Evaluate("M20 + 1beat - 1bar", Ctx(markers)), Eps);
        // bare seconds offset.
        Assert.AreEqual(m20 + 1.25, ChartTimeExpr.Evaluate("M20 + 1.25", Ctx(markers)), Eps);
    }

    [Test]
    public void NegativeCoefficient()
    {
        var markers = new Dictionary<string, string> { { "M1", "5:1" } };
        double m1 = 16.0 * Beat;
        Assert.AreEqual(m1 - 2.0 * Beat, ChartTimeExpr.Evaluate("M1 + -2beat", Ctx(markers)), Eps);
    }

    [Test]
    public void NestedMarkerDefinitions()
    {
        var markers = new Dictionary<string, string>
        {
            { "A", "5:1" },
            { "B", "A + 2beat" },
            { "C", "B + 1bar" },
        };
        double a = 16.0 * Beat;
        Assert.AreEqual(a, ChartTimeExpr.Evaluate("A", Ctx(markers)), Eps);
        Assert.AreEqual(a + 2.0 * Beat, ChartTimeExpr.Evaluate("B", Ctx(markers)), Eps);
        Assert.AreEqual(a + 2.0 * Beat + Bar, ChartTimeExpr.Evaluate("C", Ctx(markers)), Eps);
    }

    [Test]
    public void UnknownMarkerThrows()
    {
        Assert.Throws<ChartTimeExpr.ChartTimeException>(() => ChartTimeExpr.Evaluate("Nope", Ctx()));
    }

    [Test]
    public void DirectCycleThrows()
    {
        var markers = new Dictionary<string, string> { { "A", "A + 1beat" } };
        Assert.Throws<ChartTimeExpr.ChartTimeException>(() => ChartTimeExpr.Evaluate("A", Ctx(markers)));
    }

    [Test]
    public void IndirectCycleThrows()
    {
        var markers = new Dictionary<string, string>
        {
            { "A", "B + 1beat" },
            { "B", "A - 1beat" },
        };
        Assert.Throws<ChartTimeExpr.ChartTimeException>(() => ChartTimeExpr.Evaluate("A", Ctx(markers)));
    }

    [Test]
    public void MalformedExpressionsThrow()
    {
        Assert.Throws<ChartTimeExpr.ChartTimeException>(() => ChartTimeExpr.Evaluate("", Ctx()));
        Assert.Throws<ChartTimeExpr.ChartTimeException>(() => ChartTimeExpr.Evaluate("5:1 * 2", Ctx()));
        Assert.Throws<ChartTimeExpr.ChartTimeException>(() => ChartTimeExpr.Evaluate("M1 + 2furlong",
            new ChartTimeExpr.Context(Bpm, Measure, 0.0, new Dictionary<string, string> { { "M1", "5:1" } })));
    }

    [Test]
    public void InvalidContextThrows()
    {
        Assert.Throws<ChartTimeExpr.ChartTimeException>(() => new ChartTimeExpr.Context(0.0, 4, 0.0, null));
        Assert.Throws<ChartTimeExpr.ChartTimeException>(() => new ChartTimeExpr.Context(120.0, 0, 0.0, null));
    }
}
