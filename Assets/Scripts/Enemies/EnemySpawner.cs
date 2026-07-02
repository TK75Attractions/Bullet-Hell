using System.Collections.Generic;
using System;

[Serializable]
public class EnemySpawner
{
    public int id;
    public string enemyName = "";
    public string visualId = "";
    public float fadeInSec;   // 出現からこの秒数でスプライトα 0→1(0 なら即時表示)
    public float fadeOutSec;  // 消滅(orbit.life)前この秒数でスプライトα 1→0(0 ならフェードなし)
    public int sortingOrder;  // 0 以外なら SpriteRenderer.sortingOrder を明示指定(敵同士の前後関係用)
    public EnemyAnimationPlan animation = new EnemyAnimationPlan();
    public int count;
    public float enemyInterval;
    public float enemyAppearTime;
    public float bulletEmitTime;
    public int bulletCount;
    public float bulletInterval;
    public BulletData orbit;
    public BulletClip bulletClip;
    public List<BulletChangeClip> bulletChangeClips = new List<BulletChangeClip>();
}
