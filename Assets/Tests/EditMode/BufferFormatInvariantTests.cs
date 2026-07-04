// EditMode tests: BulletBuffer file-format and registration-name invariants.
using System.Text;
using NUnit.Framework;

/// <summary>
/// Guards two classes of authoring accidents that schema-level parsing cannot
/// see: byte-level file corruption (invalid UTF-8, mixed line endings, the
/// \r\r\n text-mode double-conversion signature) and registration-name
/// collisions inside one runtime load scope (built-ins + common/debug + one
/// stage folder), where a duplicate name makes one file silently replace the
/// other. Both suites are error-free on the current data; keep them that way.
/// </summary>
public class BufferFormatInvariantTests
{
    [Test]
    public void AllBufferFilesAreCleanUtf8WithConsistentLineEndings()
    {
        StageValidation.Report report = new StageValidation.Report();
        StageValidation.ValidateBufferFileFormat(report);

        Assert.IsEmpty(report.Errors, "Buffer file format errors:\n" + string.Join("\n", report.Errors));
    }

    [Test]
    public void BufferRegistrationNamesAreUniquePerLoadScope()
    {
        StageValidation.Report report = new StageValidation.Report();
        StageValidation.ValidateBufferNames(report);

        Assert.IsEmpty(report.Errors, "Buffer name registry errors:\n" + string.Join("\n", report.Errors));
    }

    // ---- Detector self-tests with synthetic bytes (prove the checks fire) ----

    [Test]
    public void ByteValidatorCatchesTextModeDoubleConversion()
    {
        StageValidation.Report report = new StageValidation.Report();
        StageValidation.ValidateBufferBytes("synthetic.json", Encoding.UTF8.GetBytes("{\r\r\n  \"name\": \"x\"\r\r\n}"), report);

        Assert.That(report.Errors, Has.Some.Contains("CR CR"));
    }

    [Test]
    public void ByteValidatorCatchesMixedLineEndings()
    {
        StageValidation.Report report = new StageValidation.Report();
        StageValidation.ValidateBufferBytes("synthetic.json", Encoding.UTF8.GetBytes("{\r\n  \"name\": \"x\"\n}"), report);

        Assert.That(report.Errors, Has.Some.Contains("mixes CRLF"));
    }

    [Test]
    public void ByteValidatorCatchesInvalidUtf8()
    {
        StageValidation.Report report = new StageValidation.Report();
        StageValidation.ValidateBufferBytes("synthetic.json", new byte[] { (byte)'{', 0xFF, 0xFE, (byte)'}' }, report);

        Assert.That(report.Errors, Has.Some.Contains("not valid UTF-8"));
    }

    [Test]
    public void ByteValidatorAcceptsCleanBomCrlfAndCleanLf()
    {
        StageValidation.Report report = new StageValidation.Report();
        byte[] bomCrlf = Encoding.UTF8.GetPreamble();
        byte[] body = Encoding.UTF8.GetBytes("{\r\n  \"name\": \"x\"\r\n}\r\n");
        byte[] withBom = new byte[bomCrlf.Length + body.Length];
        bomCrlf.CopyTo(withBom, 0);
        body.CopyTo(withBom, bomCrlf.Length);
        StageValidation.ValidateBufferBytes("bom-crlf.json", withBom, report);
        StageValidation.ValidateBufferBytes("lf.json", Encoding.UTF8.GetBytes("{\n  \"name\": \"x\"\n}\n"), report);

        Assert.IsEmpty(report.Errors, "Clean files must pass:\n" + string.Join("\n", report.Errors));
    }
}
