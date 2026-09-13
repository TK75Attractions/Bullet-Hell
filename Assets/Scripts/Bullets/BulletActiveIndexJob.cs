using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

/// <summary>
/// 生存している弾(<c>isActive || isClearing</c>)のスロット番号を昇順に集める Job。
///
/// <see cref="QuadOrder"/> の <c>enemyBullets</c> は追加専用で、寿命切れ・画面外カリングで
/// 死んだ弾もスロットとして残り続ける。毎フレームの更新 Job / 当たり判定セルの再構築 /
/// 描画データ生成をこの表(<c>indices</c>)経由で回すことで、処理コストを「累計弾数」ではなく
/// 「生きている弾数」に比例させる。
///
/// 表の更新は 2 段:
/// 1. 既存の <c>indices</c> を先頭から走査し、まだ生きているものだけを前詰めで残す(間引き)。
///    ここは生存数ぶんしか回らない。
/// 2. <c>scannedLength</c> 以降(前回の更新以後に追加されたスロット)を走査して足す。
/// どちらも昇順を保つので、消費側のループ順序は従来の全スロット走査と同じになる。
/// </summary>
[BurstCompile]
public struct BulletActiveIndexJob : IJob
{
    [ReadOnly] public NativeArray<BulletData> bullets;
    public NativeList<int> indices;

    /// <summary>indices に反映済みのスロット数。これ以降のスロットを新規として拾う。</summary>
    public int scannedLength;

    /// <summary>1 = 全スロットを入れる(計測・検証用の従来相当モード)。</summary>
    public byte fullSlotScan;

    public void Execute()
    {
        if (fullSlotScan != 0)
        {
            indices.Clear();
            for (int i = 0; i < bullets.Length; i++)
            {
                indices.Add(i);
            }
            return;
        }

        int write = 0;
        for (int k = 0; k < indices.Length; k++)
        {
            int index = indices[k];
            if (index < 0 || index >= bullets.Length) continue;
            BulletData bullet = bullets[index];
            if (!bullet.isActive && !bullet.isClearing) continue;
            indices[write] = index;
            write++;
        }
        indices.ResizeUninitialized(write);

        for (int index = scannedLength; index < bullets.Length; index++)
        {
            BulletData bullet = bullets[index];
            if (!bullet.isActive && !bullet.isClearing) continue;
            indices.Add(index);
        }
    }
}
