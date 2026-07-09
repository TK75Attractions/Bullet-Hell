using UnityEditor;

// 浮浪者(vagrant)ステージを Play Mode 中に起動するデバッグメニュー。
// 石工の StoneStageDebugMenu.StartStageByName を汎用起動口として流用する。
public static class VagrantStageDebugMenu
{
    [MenuItem("Tools/Bullet Hell/Debug/Start Vagrant Stage")]
    private static void StartVagrant() => StoneStageDebugMenu.StartStageByName("浮浪者");
}
