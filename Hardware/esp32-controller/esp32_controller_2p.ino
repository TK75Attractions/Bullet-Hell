// esp32_controller_2p.ino
// 2人プレイ用コントローラー ファームウェア
// ボード: sparkle IoT XH-S3E（**ESP32-S3**・N16R8）— Arduino IDE では「ESP32S3 Dev Module」を選択
//   ※2026-07-13 実機写真で ESP32-S3 と確定（DevKitC-32E ではなかった）。ピンを S3 用に更新。
//   ※シリアルが見えない場合はツール→「USB CDC On Boot: Enabled」にして書き込み直す
//
// 設計の正: Instructions/controller-2p-esp32.md・Instructions/2P-design.md
//
// 概要:
//   P1/P2 それぞれ 5 入力（上/下/左/右/ボタン）を内部プルアップで読み、
//   状態が変化したときだけ 1 行を 115200bps で送る。
//     フォーマット: "S <P1hex> <P2hex>\n"  （例: "S 05 00"）
//     各バイトのビット割り当て: bit0=上, bit1=下, bit2=左, bit3=右, bit4=ボタン
//   起動時に接続確認用の 1 行 "HELLO 2P v1\n" を送る。
//
// 配線: 全スイッチ NO 端子 → GPIO、COM → GND（INPUT_PULLUP・押下で LOW・外付け抵抗不要）。
//
//   入力 | P1（既存配線のまま）| P2
//   -----+---------------------+-------
//   上   | GPIO4               | GPIO9
//   下   | GPIO5               | GPIO10
//   左   | GPIO6               | GPIO11
//   右   | GPIO7               | GPIO12
//   ボタン| GPIO8               | GPIO13
//
// ESP32-S3 で避けるピン: 0/3/45/46（ストラップ）・19/20（native USB）・35/36/37（octal PSRAM）・
// 26〜32（フラッシュ）。GPIO4〜18 は自由に使える。
//
// ※ 実機未検証（机上実装）。導通・起動確認は README のチェックリスト参照。

// --- ピン定義（順序は 上,下,左,右,ボタン）---
const int P1_PINS[5] = {4, 5, 6, 7, 8};      // 既存配線に一致（S3 では全て安全）
const int P2_PINS[5] = {9, 10, 11, 12, 13};  // P1 と同じ列の下側・連番で配線しやすく

// ビット名の対応（デバッグ表示・ドキュメント用）: bit0=U,1=D,2=L,3=R,4=B
const char* KEYS = "UDLRB";

// 送信ボーレート・デバッグ設定
const unsigned long BAUD = 115200;
const unsigned long DEBOUNCE_MS = 5;   // デバウンス兼レート制限（最大 200Hz）

// 前方宣言（Arduino IDE は自動生成するが、PlatformIO 等でも確実にするため明示）。
char hexDigit(uint8_t v);
uint8_t readState(const int* pins);

// --- ヘルパ: 5 ピンを読んで 5bit の状態バイトを作る（押下=LOW=1）---
uint8_t readState(const int* pins) {
  uint8_t s = 0;
  for (int i = 0; i < 5; i++) {
    if (digitalRead(pins[i]) == LOW) {   // 内部プルアップ: 押下で LOW
      s |= (uint8_t)(1 << i);
    }
  }
  return s;
}

void setup() {
  Serial.begin(BAUD);

  for (int i = 0; i < 5; i++) {
    pinMode(P1_PINS[i], INPUT_PULLUP);
    pinMode(P2_PINS[i], INPUT_PULLUP);
  }

  // Unity 側の接続確認用に、起動を 1 行知らせる（プロトコル名 + バージョン）。
  // 受信側は "HELLO " で始まる行を「接続 OK」の合図として扱える。
  Serial.print("HELLO 2P v1\n");
}

void loop() {
  // 前回送信した状態。初期値 0xFF（実際には下位 5bit のみ使用）にしておくと、
  // 起動直後の最初の 1 回で必ず現在状態を送る（全解放 0x00 でも初回送信される）。
  static uint8_t prev1 = 0xFF, prev2 = 0xFF;
  static unsigned long lastMs = 0;

  unsigned long now = millis();
  if (now - lastMs < DEBOUNCE_MS) {
    return;                       // デバウンス兼レート制限
  }
  lastMs = now;

  uint8_t s1 = readState(P1_PINS);
  uint8_t s2 = readState(P2_PINS);

  if (s1 != prev1 || s2 != prev2) {      // どちらかが変化したときだけ送信
    // printf 依存を避けるため手組み（ESP32 Arduino core では printf も可だが、
    // 環境差を減らすため固定 2 桁の 16 進を自前整形する）。
    char buf[16];
    // "S " + 2桁hex + " " + 2桁hex + "\n"
    buf[0] = 'S';
    buf[1] = ' ';
    buf[2] = hexDigit(s1 >> 4);
    buf[3] = hexDigit(s1 & 0x0F);
    buf[4] = ' ';
    buf[5] = hexDigit(s2 >> 4);
    buf[6] = hexDigit(s2 & 0x0F);
    buf[7] = '\n';
    buf[8] = '\0';
    Serial.print(buf);

    prev1 = s1;
    prev2 = s2;
  }
}

// 0-15 を大文字 16 進 1 文字へ。
char hexDigit(uint8_t v) {
  v &= 0x0F;
  return (v < 10) ? (char)('0' + v) : (char)('A' + (v - 10));
}
