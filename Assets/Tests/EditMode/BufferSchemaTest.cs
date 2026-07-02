// EditMode test: BulletBuffer JSON schema + typeName resolution.
using NUnit.Framework;
using UnityEditor;

/// <summary>
/// Parses every BulletBuffer JSON and asserts required fields exist and each
/// non-laser bullet's typeName resolves in <see cref="BulletTypeDataBase"/>.
/// </summary>
public class BufferSchemaTest
{
    [Test]
    public void AllBufferJsonAreValid()
    {
        BulletTypeDataBase btdb = AssetDatabase.LoadAssetAtPath<BulletTypeDataBase>(StageGoldenDumper.BtdbAssetPath);
        Assert.IsNotNull(btdb, "BulletTypeDataBase asset not found.");

        StageValidation.Report report = new StageValidation.Report();
        StageValidation.ValidateBuffers(btdb, report);

        Assert.IsEmpty(report.Errors, "Buffer schema errors:\n" + string.Join("\n", report.Errors));
    }
}
