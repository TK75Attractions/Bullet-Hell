using NUnit.Framework;
using UnityEngine;

/// <summary>
/// ESP32 2P コントローラーのシリアル入力層(InputManager)の契約を固定する。
/// 実シリアルを介さず疑似 "S xx yy" / "HELLO" 行を注入し、P1/P2 のビット展開・
/// A/B ボタンのマッピング・戻る(B)のエッジ検出・入力向き変換を検証する。
///
/// ファーム(Hardware/esp32-controller/esp32_controller_2p.ino)のプロトコル:
///   "HELLO 2P v2"           起動時の接続確認
///   "S <P1hex> <P2hex>"     状態変化時。bit0=上,1=下,2=左,3=右,4=A(決定/ダッシュ),5=B(戻る)。押下=1。
///
/// InputManager は MonoBehaviour だが Awake/Start を持たないため、Init()(実ポートを
/// 開く/設定ファイルを書く)を呼ばずに component を生成し、注入 → UpdateInput() で検証する。
/// 向き設定は PlayerPrefs を使うため、テスト内で退避・復元する。
/// </summary>
public class ControllerSerial2PTests
{
    private static readonly string[] OrientKeys =
    {
        "inputDir.rotation", "inputDir.flipX", "inputDir.flipY",
        "inputDir.rotation2", "inputDir.flipX2", "inputDir.flipY2"
    };
    private int[] savedVals;
    private bool[] hadKey;

    [SetUp]
    public void SetUp()
    {
        savedVals = new int[OrientKeys.Length];
        hadKey = new bool[OrientKeys.Length];
        for (int i = 0; i < OrientKeys.Length; i++)
        {
            hadKey[i] = PlayerPrefs.HasKey(OrientKeys[i]);
            savedVals[i] = PlayerPrefs.GetInt(OrientKeys[i], 0);
            PlayerPrefs.DeleteKey(OrientKeys[i]);
        }
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = 0; i < OrientKeys.Length; i++)
        {
            if (hadKey[i]) PlayerPrefs.SetInt(OrientKeys[i], savedVals[i]);
            else PlayerPrefs.DeleteKey(OrientKeys[i]);
        }
        PlayerPrefs.Save();
    }

    private static InputManager NewManager()
    {
        var go = new GameObject("~test-input");
        var im = go.AddComponent<InputManager>();
        im.isDebugMode = true; // Init は呼ばない。念のため serial 無効フラグも立てておく。
        return im;
    }

    private static void Kill(InputManager im)
    {
        if (im != null) Object.DestroyImmediate(im.gameObject);
    }

    // ---- S 行のパース(純粋関数) ----

    [Test]
    public void TryParseSLine_ValidLines_ParseBothBytes()
    {
        Assert.IsTrue(InputManager.TryParseSLine("S 05 00", out byte p1, out byte p2));
        Assert.AreEqual(0x05, p1);
        Assert.AreEqual(0x00, p2);

        Assert.IsTrue(InputManager.TryParseSLine("S 3F 21", out p1, out p2));
        Assert.AreEqual(0x3F, p1);
        Assert.AreEqual(0x21, p2);

        // 小文字 's' と小文字 hex、末尾 CR も許容する。
        Assert.IsTrue(InputManager.TryParseSLine("s 0a 0B\r", out p1, out p2));
        Assert.AreEqual(0x0A, p1);
        Assert.AreEqual(0x0B, p2);
    }

    [Test]
    public void TryParseSLine_InvalidLines_Rejected()
    {
        Assert.IsFalse(InputManager.TryParseSLine("HELLO 2P v2", out _, out _));
        Assert.IsFalse(InputManager.TryParseSLine("", out _, out _));
        Assert.IsFalse(InputManager.TryParseSLine("S", out _, out _));
        Assert.IsFalse(InputManager.TryParseSLine("S 05", out _, out _), "1 バイトのみは不正");
        Assert.IsFalse(InputManager.TryParseSLine("{\"x\":0,\"y\":0,\"dash\":false}", out _, out _));
        Assert.IsFalse(InputManager.TryParseSLine("Dir:LEFT", out _, out _));
        Assert.IsFalse(InputManager.TryParseSLine("S GG 00", out _, out _), "不正 hex は拒否");
    }

    // ---- P1 のビット展開(注入 → UpdateInput) ----

    [Test]
    public void Inject_P1_ExpandsDirectionsAndButtons()
    {
        var im = NewManager();
        try
        {
            // 0x39 = bit0(上)+bit3(右)+bit4(A)+bit5(B)。対向しない方向を選ぶ。
            im.InjectSerialLine("S 39 00");
            im.UpdateInput();

            Assert.IsTrue(im.upPressed, "bit0=上");
            Assert.IsTrue(im.rightPressed, "bit3=右");
            Assert.IsFalse(im.downPressed);
            Assert.IsFalse(im.leftPressed);
            Assert.IsTrue(im.buttonPressed, "bit4=A(決定/ダッシュ)");
            Assert.IsTrue(im.backPressed, "bit5=B(戻る)");

            // P2 側は一切反応しない。
            Assert.IsFalse(im.p2Up);
            Assert.IsFalse(im.p2ButtonPressed);
            Assert.IsFalse(im.p2BackPressed);
        }
        finally { Kill(im); }
    }

    [Test]
    public void Inject_P2_ExpandsDirectionsAndButtons_Independently()
    {
        var im = NewManager();
        try
        {
            // 0x3F = 全 6 ビット。P2 は生ビット展開のため対向方向も同時に立つ。
            im.InjectSerialLine("S 00 3F");
            im.UpdateInput();

            Assert.IsTrue(im.p2Up);
            Assert.IsTrue(im.p2Down);
            Assert.IsTrue(im.p2Left);
            Assert.IsTrue(im.p2Right);
            Assert.IsTrue(im.p2ButtonPressed, "bit4=A");
            Assert.IsTrue(im.p2BackPressed, "bit5=B");

            // P1 側は一切反応しない。
            Assert.IsFalse(im.upPressed);
            Assert.IsFalse(im.buttonPressed);
            Assert.IsFalse(im.backPressed);
        }
        finally { Kill(im); }
    }

    // ---- 戻る(B)のエッジ検出。5 つの戻り操作が読む backPressedThisFrame を担保 ----

    [Test]
    public void P1_BButton_DrivesBackPressedThisFrame_EdgeOnce()
    {
        var im = NewManager();
        try
        {
            im.InjectSerialLine("S 20 00"); // P1 bit5 = B
            im.UpdateInput();
            Assert.IsTrue(im.backPressed);
            Assert.IsTrue(im.backPressedThisFrame, "押した瞬間のみ true");

            // 押しっぱなしなら次フレームはエッジが立たない。
            im.UpdateInput();
            Assert.IsTrue(im.backPressed);
            Assert.IsFalse(im.backPressedThisFrame);

            // 離すと backPressed が下りる。
            im.InjectSerialLine("S 00 00");
            im.UpdateInput();
            Assert.IsFalse(im.backPressed);
        }
        finally { Kill(im); }
    }

    [Test]
    public void P2_BButton_DrivesP2BackOnly_NotP1Back()
    {
        var im = NewManager();
        try
        {
            im.InjectSerialLine("S 00 20"); // P2 bit5 = B
            im.UpdateInput();

            Assert.IsTrue(im.p2BackPressed);
            Assert.IsTrue(im.p2BackPressedThisFrame);
            // P2 の B は P1 の戻る(メニュー操作)には流さない。
            Assert.IsFalse(im.backPressed);
            Assert.IsFalse(im.backPressedThisFrame);
        }
        finally { Kill(im); }
    }

    // ---- HELLO ハンドシェイク ----

    [Test]
    public void Hello_SetsHandshakeFlags()
    {
        var im = NewManager();
        try
        {
            Assert.IsFalse(im.HelloSeen);
            Assert.IsFalse(im.SProtocolSeen);

            im.InjectSerialLine("HELLO 2P v2");
            Assert.IsTrue(im.HelloSeen);
            Assert.IsFalse(im.SProtocolSeen, "HELLO だけでは S 未受信");

            im.InjectSerialLine("S 01 00");
            Assert.IsTrue(im.SProtocolSeen);
        }
        finally { Kill(im); }
    }

    // ---- 向き変換(純粋関数) ----

    [Test]
    public void ApplyOrientation_Identity_NoChange()
    {
        bool up = true, down = false, left = false, right = false;
        InputManager.ApplyOrientation(0, false, false, ref up, ref down, ref left, ref right);
        Assert.IsTrue(up);
        Assert.IsFalse(down || left || right);
    }

    [Test]
    public void ApplyOrientation_Rotate90_UpBecomesLeft()
    {
        bool up = true, down = false, left = false, right = false;
        InputManager.ApplyOrientation(1, false, false, ref up, ref down, ref left, ref right);
        Assert.IsTrue(left, "反時計回り 90 度: 上 -> 左");
        Assert.IsFalse(up || down || right);
    }

    [Test]
    public void ApplyOrientation_Rotate180_UpBecomesDown()
    {
        bool up = true, down = false, left = false, right = false;
        InputManager.ApplyOrientation(2, false, false, ref up, ref down, ref left, ref right);
        Assert.IsTrue(down);
        Assert.IsFalse(up || left || right);
    }

    [Test]
    public void ApplyOrientation_Rotate270_UpBecomesRight()
    {
        bool up = true, down = false, left = false, right = false;
        InputManager.ApplyOrientation(3, false, false, ref up, ref down, ref left, ref right);
        Assert.IsTrue(right);
        Assert.IsFalse(up || down || left);
    }

    [Test]
    public void ApplyOrientation_NegativeRotationWraps()
    {
        // -1 は 270 度と等価。
        bool up = true, down = false, left = false, right = false;
        InputManager.ApplyOrientation(-1, false, false, ref up, ref down, ref left, ref right);
        Assert.IsTrue(right);
    }

    [Test]
    public void ApplyOrientation_FlipX_SwapsLeftRight()
    {
        bool up = false, down = false, left = true, right = false;
        InputManager.ApplyOrientation(0, true, false, ref up, ref down, ref left, ref right);
        Assert.IsTrue(right);
        Assert.IsFalse(left);
    }

    [Test]
    public void ApplyOrientation_FlipY_SwapsUpDown()
    {
        bool up = true, down = false, left = false, right = false;
        InputManager.ApplyOrientation(0, false, true, ref up, ref down, ref left, ref right);
        Assert.IsTrue(down);
        Assert.IsFalse(up);
    }

    // ---- 向き変換が UpdateInput に P1/P2 個別で配線されていることの実証 ----

    [Test]
    public void PerPlayerOrientation_IsAppliedIndependently()
    {
        var im = NewManager();
        try
        {
            im.CycleInputRotation(1, 2); // P2 のみ 180 度回転。P1 は無変換。

            im.InjectSerialLine("S 01 01"); // P1 上(bit0)、P2 上(bit0)
            im.UpdateInput();

            Assert.IsTrue(im.upPressed, "P1 は無回転なので上のまま");
            Assert.IsFalse(im.downPressed);

            Assert.IsTrue(im.p2Down, "P2 は 180 度回転で上->下");
            Assert.IsFalse(im.p2Up);
        }
        finally { Kill(im); }
    }
}
