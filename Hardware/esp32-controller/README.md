# ESP32-S3 2P controller

sparkle IoT XH-S3E（ESP32-S3・N16R8）用の2人プレイコントローラーです。
シリアルプロトコルとピン割り当ては Unity 側と共有しているため、変更しないでください。

## USB-C ポートの使い分け

XH-S3E には USB-C コネクタが2つあります。

- UART ブリッジ側: CH343 として列挙されます。`pio device list` では
  `USB-Enhanced-SERIAL CH343`（VID:PID `1A86:55D3`）と表示されます。
  ROM ブートログと書き込みに使えます。
- native USB 側: ESP32-S3 本体の USB CDC として列挙されます。
  このファームの `Serial`（`HELLO 2P v1` / `S xx yy`）を確認するモニタはこちらへ接続します。

両方を接続したうえで次を実行し、表示名と COM 番号を確認してください。

```powershell
pio device list
```

COM 番号は接続し直すと変わる場合があります。UART 側で ROM の
`mode:DIO ... entry ...` だけが見え、`HELLO 2P v1` が見えない場合は、
native USB 側の COM ポートを選び直してください。

## ビルド・書き込み・モニタ

`Hardware/esp32-controller/` で実行します。

```powershell
pio run -e xh_s3e
pio run -e xh_s3e -t upload
pio device monitor -e xh_s3e -p COMx
```

`platformio.ini` ではモニタの DTR/RTS を無効にしています。モニタを開いた際に
ESP32-S3 のリセット線やブートストラップ線を誤って操作しないためです。

付属の確認用モニタも使えます。ポートを省略すると検出ポートを選びますが、
2ポートある場合は native USB 側を明示してください。

```powershell
.\test-monitor.ps1 -Port COMx
```

## 出力と確認項目

- 起動時: `HELLO 2P v1`
- 最初の入力変化までは2秒ごと: `HELLO 2P v1`
- 入力変化時: `S <P1hex> <P2hex>`
- bit 0=上、bit 1=下、bit 2=左、bit 3=右、bit 4=ボタン
- P1: GPIO 4, 5, 6, 7, 8
- P2: GPIO 9, 10, 11, 12, 13

モニタを開いたまま RST を3回押し、毎回 `HELLO 2P v1` が残ることを確認します。
その後 P1/P2 の各方向とボタンを操作し、`S xx yy` の各 bit が対応どおり変化することを確認します。
