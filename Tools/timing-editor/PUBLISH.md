# timing-editor を GitHub Pages で公開する手順

`index.html` は 1 ファイル完結（外部 CDN・アセット依存なし）なので、静的ホスティングにそのまま置けば動きます。

料金メモ: **public リポジトリなら GitHub Pages は無料**。private リポジトリでの Pages は有料プラン限定です。

---

## 方法 A: 自分の新規 public リポジトリで公開（おすすめ・gh 不要）

チーム共用リポジトリ（`TK75Attractions/Bullet-Hell`）に触れず、Unity プロジェクト全体を公開せずに済みます。

### 1. リポジトリを作る
1. https://github.com/new を開く
2. Repository name: `timing-editor`（任意）
3. **Public** を選択
4. 「Add a README」などはチェック不要
5. Create repository

### 2. index.html をアップロード
作ったリポジトリのページで:
- 「uploading an existing file」リンク、または Add file → Upload files
- `D:\unity\Bullet-Hell\tools\timing-editor\index.html` をドラッグ＆ドロップ
- Commit changes

> ファイル名は必ず `index.html` のままにする（Pages がトップページとして認識する）。

### 3. Pages を有効化
1. リポジトリの Settings → 左メニュー Pages
2. Build and deployment → Source: **Deploy from a branch**
3. Branch: `main` / フォルダ: `/ (root)` → Save
4. 1〜2 分待つと、同じ Pages 画面上部に公開 URL が表示される
   - `https://<ユーザー名>.github.io/timing-editor/`

以降 `index.html` を更新したいときは、同じ手順でファイルを上書きアップロードすれば数十秒〜数分で反映されます。

---

## 方法 A（コマンド版・gh CLI を使う場合）

```powershell
# gh 未インストールなら: winget install --id GitHub.cli
gh auth login
cd D:\unity\Bullet-Hell\tools\timing-editor

gh repo create timing-editor --public --source=. --push
# ↑ カレントの index.html / README.md などが push される

# Pages を main/root で有効化
gh api -X POST repos/{owner}/timing-editor/pages -f "source[branch]=main" -f "source[path]=/"
```

---

## 方法 B: 既存の共用リポジトリで公開（影響大）

`TK75Attractions/Bullet-Hell` のサブフォルダ `tools/timing-editor/` を配信する場合、
Pages の標準設定はルートか `/docs` のみなので **GitHub Actions のワークフローが必要**です。
かつリポジトリの **admin 権限** が必要で、**Unity プロジェクト全体が public 公開** されます（チームへの影響が大きい）。

必要になったら `.github/workflows/pages.yml` を用意する形になります（`actions/upload-pages-artifact` の `path` に `tools/timing-editor` を指定）。
