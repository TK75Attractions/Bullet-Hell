// EditMode tests: BulletBuffer file-format and registration-name invariants.
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
}
