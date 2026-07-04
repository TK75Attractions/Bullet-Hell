using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor menu that runs the full stage/buffer validation suite and prints a
/// single consolidated report to the console. "All green" means zero errors
/// (warnings may still be listed as advisories).
/// </summary>
public static class StageLinterMenu
{
    public const string ValidateMenuPath = "Tools/Bullet Hell/Validate All Stages";

    [MenuItem(ValidateMenuPath)]
    public static void ValidateAllStages()
    {
        StageValidation.Report report = new StageValidation.Report();

        BulletTypeDataBase btdb = AssetDatabase.LoadAssetAtPath<BulletTypeDataBase>(StageGoldenDumper.BtdbAssetPath);
        if (btdb == null)
        {
            Debug.LogError($"[Lint] BulletTypeDataBase not found at {StageGoldenDumper.BtdbAssetPath}");
            return;
        }

        StageValidation.ValidateTypeDatabase(btdb, report);
        StageValidation.ValidateBuffers(btdb, report);
        StageValidation.ValidateBufferFileFormat(report);
        StageValidation.ValidateBufferNames(report);
        StageValidation.ValidateStageEnemyTypeNames(btdb, report);

        using (EditorStageProbe probe = new EditorStageProbe(StageGoldenDumper.BtdbAssetPath, StageGoldenDumper.EdbAssetPath))
        {
            Dictionary<string, StageData> stages = StageGoldenDumper.LoadOfficialStages();
            StageValidation.ValidateStageLinks(stages, report);
        }

        StringBuilder sb = new StringBuilder();
        sb.Append($"[Lint] Validation finished: {report.Errors.Count} error(s), {report.Warnings.Count} warning(s).\n");

        if (report.Warnings.Count > 0)
        {
            sb.Append("--- Warnings ---\n");
            for (int i = 0; i < report.Warnings.Count; i++)
            {
                sb.Append("  ").Append(report.Warnings[i]).Append('\n');
            }
        }

        if (report.Errors.Count > 0)
        {
            sb.Append("--- Errors ---\n");
            for (int i = 0; i < report.Errors.Count; i++)
            {
                sb.Append("  ").Append(report.Errors[i]).Append('\n');
            }
            Debug.LogError(sb.ToString());
        }
        else
        {
            sb.Append("All green.");
            Debug.Log(sb.ToString());
        }
    }
}
