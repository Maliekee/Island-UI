<img src="icon.png" width="128" align="right" alt="Island UI">

# Island UI

**あなたの島を、ひと目で。**

[English](README.md) · [简体中文](README.zh-CN.md) · **日本語** · [Русский](README.ru.md) · [Português (Brasil)](README.pt-BR.md) · [Español](README.es.md) · [Deutsch](README.de.md)

<sub>このページは機械翻訳です。英語版が正となります。</sub>

![プレイ中の Island UI：HUD、キャラクターシート、名簿、クラフト、設定](O1.webp)

**丁寧に作り込んだ、一貫性のある Mad Island のインターフェース。** いくつかの原則を土台にしていて、[200 個の機能 →](FEATURES.md)はすべてそこから生まれています。

- **[土台から作り直す。](SHOWCASE.md#built-from-the-ground-up)** ばんそうこうを千枚貼るのではありません。Island UI は一歩引いて、構想からコードまでインターフェースを丸ごと作り直します。
- **[自然に手になじむ。](SHOWCASE.md#second-nature)** すべてが人の自然な受け取り方に沿って示されるので、説明はいりません。拾ったアイテムはあなたのもとへ飛んできて、ステータスはただの数字ではなく意味を示します。
- **[遊びに集中、雑務はなし。](SHOWCASE.md#play-not-paperwork)** 面倒な作業は邪魔をしません。木を叩けば斧が出て、井戸ではクリックなしで水が飲め、ワンタッチで周りのチェストの中身がすべてバッグに入ります。
- **[やることは一つ。](SHOWCASE.md#one-job)** Island UI は、インターフェースだけに狙いを絞った大きなプロジェクトです。ゲームの内容やほかの Mod の邪魔にならないよう、あえて自分の領分から出ません。便利な機能はゲームの精神に沿ったものにとどめ、ルールを変える調整は Island Ex に分けています。

これは愛情を込めて作っているもので、本当に良いものにしたいと思っています。ぜひ感想を聞かせてください。[Discord](https://discord.com/users/1523362502224777439) で連絡するか、[ここで issue を開いて](../../issues)ください。

**[プレイ映像を見る →](SHOWCASE.md)**

## インストール

[Releases](../../releases) から `Island-UI-vX.Y.Z-installer.zip` をダウンロードし、好きな場所に展開して `install.bat` をダブルクリックします。Steam 経由でゲームを見つけ、BepInEx がまだなければ追加し、MOD をコピーします。`uninstall.bat` で元に戻せます。

<details><summary><b>その他のダウンロードと、手動でのアンインストール</b></summary>

| ダウンロード | 対象 |
|---|---|
| `Island-UI-vX.Y.Z-installer.zip` | **いちばん簡単。** 上のインストーラーです。他の MOD や設定には触れず、何もダウンロードしません。 |
| `Island-UI-vX.Y.Z-with-Island-Ex-installer.zip` | 同じインストーラーに Island Ex も付いたもの。 |
| `Island-UI-vX.Y.Z-with-BepInEx.zip` | インストーラーを使わない場合：ゲームフォルダ（`Mad Island.exe` のある場所）に展開します。BepInEx 5.4.23.5 同梱。 |
| `Island-UI-vX.Y.Z.zip` | BepInEx 5 を導入済みの場合：ゲームフォルダに展開します。 |
| `Island-Ex-vX.Y.Z.zip` | Island Ex のみ：ゲームフォルダに展開します。BepInEx 5 が必要です。 |

ゲームフォルダは通常 `C:\Program Files (x86)\Steam\steamapps\common\Mad Island` です（Steam でゲームを右クリック、*管理*、*ローカルファイルを閲覧*）。ダウンロードしたスクリプトの実行について Windows が警告することがあります。インストーラーはただのテキストで、[`installer/`](installer) で中身を確認できます。

手動でアンインストールするには、`BepInEx/plugins/IslandUI` と `BepInEx/plugins/islandui-art` を削除します（Island Ex は `BepInEx/plugins/IslandEx` も）。

</details>

## 設定

ゲームのメニューの *Option* の下に **Island UI** の項目があります。変更はすぐに反映されます。MOD はゲームの言語に従いますが、自分で選ぶこともできます：英語、簡体字中国語、日本語、ロシア語、ブラジルポルトガル語、スペイン語、ドイツ語。

<details><summary><b>設定ページ、翻訳、設定ファイル</b></summary>

設定ページはグループに分かれています：一般（ポインターと言語）、ターゲット、戦闘、通知、ウィンドウ、ストーリー、ホットキー。**Island Ex** を導入していると、その項目も追加されます。

<img src="SET.jpg" width="480" alt="設定ウィンドウの戦闘ページ">

翻訳は機械翻訳なので、修正を歓迎します。issue を開くか、`BepInEx/plugins/IslandUI/Localization/<language>/strings.txt`（と `item-guide.tsv`、`story-choices.tsv`）を編集して送ってください。やり方は各ファイルの冒頭に書いてあります。

大きな部分（周辺ウィンドウ、アクションバー、ターゲットパネル、名簿、キャラクターシート）のオフは `BepInEx/config/madisland.islandui.cfg` で行います。

タイトル画面の左下にある MOD のアイコンは、読み込まれたことを示します。カーソルを合わせるとバージョン、クリックで MOD のオン/オフを切り替えます。

</details>

## Island Ex

<img src="island-ex.png" width="64" align="right" alt="Island Ex">

Island UI はゲームのルールに従います。変えるのはゲームの見え方とそこへのたどり着き方で、できることは決して変えません。ルールを変える調整は、同じリリースに入っている別の小さな MOD、Island Ex にあります。Island UI はそれを必要としません。

<details><summary><b>Island Ex でできること</b></summary>

- **エフェクトフィルター**：見たくない視覚エフェクトをオフにします。
- **長押しで抵抗**：組みつかれたとき、攻撃ボタンを押し続けると攻撃し続けます。
- **画面に留まる**：Alt+Tab や別のモニターのクリックで、ゲームが最小化されなくなります。
- **屋内は濡れない**：家の中に雨が降らなくなります。
- **ズームアウト**：ゲームの限界よりもカメラを引けます。
- **壊れた道具を残す**：使い切った道具は壊れた状態でバッグに残り、修理キットで直せます。

</details>

## 制限事項

Island UI はまだ若い MOD です。0.9 ではインターフェースの大部分を作り直しました。プレイはしていますが、長期間のテストはしていません。粗い部分やときどきのバグはあるはずです。ゲーム本来の画面の一部はまだ作り直しておらず、以前と同じ見た目と動作です。何かが壊れたら、`BepInEx/LogOutput.log` を添付した issue がいちばん助かります。

Steam の `beta_open` ブランチ、ビルド 25095856、BepInEx 5.4.23.5 で確認しています。ゲームのアップデート後は、`BepInEx/LogOutput.log` で `Patch census [IslandUI]: ... all bound.` を探してください。違う内容が出ていれば、アップデートで何かが変わっていて、MOD の新しいリリースが必要です。

## ソース

ここのコミットはそれぞれ一つのリリースです。バージョンタグはリリースの zip と対応しています。

<details><summary><b>ビルド方法</b></summary>

ビルドには .NET SDK と、インストール済みのゲームが必要です（プロジェクトがゲームの DLL を参照します）：

```
dotnet build src/IslandUI/IslandUI.csproj -c Release
dotnet build src/IslandEx/IslandEx.csproj -c Release
```

ビルドすると DLL がゲームの `BepInEx/plugins` にコピーされます。ゲームが別の場所にある場合は `-p:GameDir="D:\...\Mad Island"` を、コピーせずにビルドするには `-p:NoDeploy=true` を付けます。Island UI には `BepInEx/plugins/islandui-art` に `art/*.png` も必要です。

</details>

## 連絡先

バグ報告、アイデア、何かを再利用したい場合は、[issue を開く](../../issues)か、[maliekee@pm.me](mailto:maliekee@pm.me) にメールするか、[Discord](https://discord.com/users/1523362502224777439) で声をかけてください。

## 関わってくれて、支えてくれてありがとう

h1sagi、NoobInDaHause、Ramiyak、Ex0du5_2169、Dimastrelok、Yuuto、Cosmo、Abyss、NotTeaBut、Kringe、Kestraz
