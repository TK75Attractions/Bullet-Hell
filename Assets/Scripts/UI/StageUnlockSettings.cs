/// <summary>
/// ステージ選択のロック(COMING SOON)設定を 1 か所に集めたもの。
///
/// 2026-09-09 のユーザー指示「プレイしてみたいのでロックを外して、他のステージや難易度も」で
/// 既定を「全解放」にした。<see cref="UnlockAll"/> を false に戻せば、従来の
/// 「石工・姿見は全難易度 COMING SOON / 浮浪者は NORMAL のみ」へそのまま戻る。
///
/// ロックの仕組み(DefficultyBar の減光 + COMING SOON、CanConfirm による確定ブロック)は
/// 残したままで、どのステージ・難易度を無効にするかだけをここで決める。
/// </summary>
public static class StageUnlockSettings
{
    /// <summary>全ステージ・全難易度を解放するか。false で従来のロックへ戻る。</summary>
    public const bool UnlockAll = true;

    /// <summary>
    /// UnlockAll でも解放しないステージ。姿見(mirror)は endTime 未設定の WIP で、
    /// 起動すると BulletRenderSystem が範囲外参照で落ちるため常にロックしておく。
    /// </summary>
    private static readonly string[] AlwaysLockedDirectories = { "mirror" };

    /// <summary>UnlockAll=false のときにロックするステージ(従来の挙動)。</summary>
    private static readonly string[] LegacyLockedDirectories = { "stone", "mirror" };

    /// <summary>ステージ全体が確定不可(全難易度 COMING SOON)か。</summary>
    public static bool IsStageLocked(string stageDirectoryName)
    {
        if (string.IsNullOrEmpty(stageDirectoryName)) return false;
        string[] locked = UnlockAll ? AlwaysLockedDirectories : LegacyLockedDirectories;
        for (int i = 0; i < locked.Length; i++)
        {
            if (locked[i] == stageDirectoryName) return true;
        }
        return false;
    }

    /// <summary>
    /// 難易度 3 行(EASY / NORMAL / LUNATIC)の選択可否。ロック中のステージは全行不可。
    /// UnlockAll=false のときだけ、浮浪者を NORMAL のみに絞る従来の制限が残る。
    /// </summary>
    public static void GetDifficultyMask(string stageDirectoryName,
        out bool easy, out bool normal, out bool lunatic)
    {
        if (IsStageLocked(stageDirectoryName))
        {
            easy = normal = lunatic = false;
            return;
        }
        if (!UnlockAll && stageDirectoryName == "vagrant")
        {
            easy = false; normal = true; lunatic = false;
            return;
        }
        easy = normal = lunatic = true;
    }
}
