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
const unsigned long SERIAL_WAIT_MS = 3000;
const unsigned long HELLO_INTERVAL_MS = 2000;

uint8_t prev1 = 0;
uint8_t prev2 = 0;
bool hasSentState = false;
unsigned long lastHelloMs = 0;

// 前方宣言（Arduino IDE は自動生成するが、PlatformIO 等でも確実にするため明示）。
char hexDigit(uint8_t v);
uint8_t readState(const int* pins);
void sendLine(const char* line);

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

// Mirror the controller protocol to both board connectors:
// Serial = ESP32-S3 native USB CDC, Serial0 = CH343 UART bridge.
void sendLine(const char* line) {
  Serial.print(line);
  Serial0.print(line);
}

void setup() {
  Serial.begin(BAUD);
  Serial0.begin(BAUD);

  for (int i = 0; i < 5; i++) {
    pinMode(P1_PINS[i], INPUT_PULLUP);
    pinMode(P2_PINS[i], INPUT_PULLUP);
  }

  // Native USB CDC is re-enumerated after reset. Give the host time to open
  // the port so the startup greeting is not lost, but never block forever.
  unsigned long t0 = millis();
  while (!Serial && millis() - t0 < SERIAL_WAIT_MS) {
    delay(10);
  }

  // Treat the boot-time input as the baseline. The first actual change sends
  // the first S line and stops the periodic HELLO messages below.
  prev1 = readState(P1_PINS);
  prev2 = readState(P2_PINS);

  // Unity 側の接続確認用に、起動を 1 行知らせる（プロトコル名 + バージョン）。
  // 受信側は "HELLO " で始まる行を「接続 OK」の合図として扱える。
  sendLine("HELLO 2P v1\n");
  lastHelloMs = millis();
}

void loop() {
  // setup() で記録した起動時状態から変化したときだけ S 行を送る。
  static unsigned long lastMs = 0;

  unsigned long now = millis();
  // Keep advertising the CDC port until the first input report proves that
  // controller data is flowing. Unsigned subtraction remains safe at wrap.
  if (!hasSentState && now - lastHelloMs >= HELLO_INTERVAL_MS) {
    sendLine("HELLO 2P v1\n");
    lastHelloMs = now;
  }

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
    sendLine(buf);

    prev1 = s1;
    prev2 = s2;
    hasSentState = true;
  }
}

// 0-15 を大文字 16 進 1 文字へ。
char hexDigit(uint8_t v) {
  v &= 0x0F;
  return (v < 10) ? (char)('0' + v) : (char)('A' + (v - 10));
}
