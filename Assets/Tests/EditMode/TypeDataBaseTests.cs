using NUnit.Framework;
using UnityEditor;

/// <summary>
/// Validates <see cref="BulletTypeDataBase"/> integrity: type/name resolution
/// round-trips, texture accessors are consistent, and verts arrays are present.
/// Sprite/readability issues are surfaced as linter warnings, not test failures.
/// </summary>
public class TypeDataBaseTests
{
    [Test]
    public void TypeDatabaseIsConsistent()
    {
        BulletTypeDataBase btdb = AssetDatabase.LoadAssetAtPath<BulletTypeDataBase>(StageGoldenDumper.BtdbAssetPath);
        Assert.IsNotNull(btdb, "BulletTypeDataBase asset not found.");

        StageValidation.Report report = new StageValidation.Report();
        StageValidation.ValidateTypeDatabase(btdb, report);

        Assert.IsEmpty(report.Errors, "BulletTypeDataBase errors:\n" + string.Join("\n", report.Errors));
    }
}
