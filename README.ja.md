<img src="icon.png" width="128" align="right" alt="Island UI">

# Island UI

**遊ぶのは島。インターフェースじゃない。**

[English](README.md) · [简体中文](README.zh-CN.md) · **日本語** · [Русский](README.ru.md) · [Português (Brasil)](README.pt-BR.md) · [Español](README.es.md) · [Deutsch](README.de.md)

<sub>このページは機械翻訳です。英語版が正となります。</sub>

![プレイ中の Island UI：HUD、キャラクターシート、名簿、クラフト、設定](O1.webp)

**ばんそうこうを千枚貼るのではなく。** Island UI は一歩引いて、Mad Island のインターフェースを根本から書き直します。画面、ターゲット、そしてそこから生まれるすべてを。土台にあるのはいくつかのシンプルな考えです。画面の上に、あなた自身の場所、周りにあるものの場所、いま相手にしているものの場所を用意し、それらを一つのターゲットでつなぐ。[すべての機能（現在 202 個）→](FEATURES.md)はそこから生まれました。

島のために作られ、一歩引いてあなたを**遊び**に集中させます。知りたいことは目を向けた場所にあり、やりたいことはワンアクションで届き、ゲームの隠れたルールが見えるようになります。

## できること

- **[自分のものは、いつも自分のもの。](SHOWCASE.md#yours-is-always-yours)** 右にあなた、真ん中にいま相手にしているもの、左に周りにあるもの。アイテムの使用は常に自分自身に。誰かに渡すときは、その相手にドロップします。
- **[ターゲットは一つ、離さない。](SHOWCASE.md#a-target)** `Tab` で、歩いて近づかなくても相手にする人や物を選び、手放すまで保持します。できることはすべてドックに並びます。
- **[数字の羅列ではなく、村として。](SHOWCASE.md#your-village)** 名簿で誰が不満かが分かり、シートでその理由が分かり、ボタン一つで解決策へ移動できます。
- **[アイテムのやりくりはもう不要。](SHOWCASE.md#no-more-item-juggling)** 手の届く範囲の収納をすべて一つのウィンドウに。クラフトはバッグと周りのすべてから素材を使います。
- **[戦闘のすべてを把握。](SHOWCASE.md#the-whole-fight)** 誰が戦っているか、誰があなたを狙っているか、誰が画面外から近づいているか。
- **[すべての数字に理由を。](SHOWCASE.md#on-top)** 各アイテムの入手方法、ストーリーの選択肢の行き先、すべての数字の裏にあるルール。7 言語に対応。

**[プレイ映像を見る →](SHOWCASE.md)** · **[大小すべての機能 →](FEATURES.md)**（英語のページ）

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

Island UI はまだ若い MOD です。0.9.0 ではインターフェースの大部分を作り直しました。プレイはしていますが、長期間のテストはしていません。粗い部分やときどきのバグはあるはずです。ゲーム本来の画面の一部はまだ作り直しておらず、以前と同じ見た目と動作です。何かが壊れたら、`BepInEx/LogOutput.log` を添付した issue がいちばん助かります。

## 互換性

Steam の `beta_open` ブランチ、ビルド 25095856、BepInEx 5.4.23.5 で確認しています。ゲームのアップデート後は、`BepInEx/LogOutput.log` で `Patch census [IslandUI]: ... all bound.` を探してください。違う内容が出ていれば、アップデートで何かが変わっていて、MOD の新しいリリースが必要です。

## ソース

ここのコミットはそれぞれ一つのリリースです。バージョンタグはリリースの zip と対応しています。

<details><summary><b>構成とビルド方法</b></summary>

```
src/IslandUI/    the mod: Inventory/ (the windows, action bar, tool belt, target), Crafting/ and
                 Browser/ (the crafting window), Card/ (item and recipe cards), Model/ (favourites,
                 moods, recipes), QoL/ (keys, quick save), UI/ (controls, cursor, console, map),
                 Data/ (the item guide and story hints, as tables)
src/IslandEx/    Island Ex: one file per feature
src/Shared/      linked into both DLLs: PatchCensus.cs applies the Harmony patches one class at
                 a time and logs any that no longer bind; TitleBadge.cs is the title-screen icon
art/             the interface's art, shipped as islandui-art
installer/       the install.bat / install.ps1 shipped in the installer downloads
```

ビルドには .NET SDK と、インストール済みのゲームが必要です（プロジェクトがゲームの DLL を参照します）：

```
dotnet build src/IslandUI/IslandUI.csproj -c Release
dotnet build src/IslandEx/IslandEx.csproj -c Release
```

ビルドすると DLL がゲームの `BepInEx/plugins` にコピーされます。ゲームが別の場所にある場合は `-p:GameDir="D:\...\Mad Island"` を、コピーせずにビルドするには `-p:NoDeploy=true` を付けます。Island UI には `BepInEx/plugins/islandui-art` に `art/*.png` も必要です。

</details>

## 連絡先

バグ報告、アイデア、何かを再利用したい場合は、[issue を開く](../../issues)か、[maliekee@pm.me](mailto:maliekee@pm.me) にメールするか、[Discord](https://discord.com/users/1523362502224777439) で声をかけてください。
