<img src="icon.png" width="128" align="right" alt="Island UI">

# Island UI

**玩的是岛，不是界面。**

[English](README.md) · **简体中文** · [日本語](README.ja.md) · [Русский](README.ru.md) · [Português (Brasil)](README.pt-BR.md) · [Español](README.es.md) · [Deutsch](README.de.md)

<sub>本页为机器翻译，以英文版为准。</sub>

![Island UI 游戏画面：HUD、角色面板、名册、制作和设置](O1.webp)

**不是一千块创可贴。** Island UI 退后一步，从头重写了 Mad Island 的界面：屏幕、目标选定，以及由此而来的一切。它建立在几个简单的想法上——屏幕上给你自己、你周围的东西、你正在打交道的对象各留一个位置，再用一个目标把它们连在一起——[所有功能（目前共 202 项）→](FEATURES.md)都由此而来。

为这座岛而做，它退到一旁，让你专心**游玩**：你需要知道的就在你看的地方，你想做的事只差一步，游戏里原本隐藏的规则也一一显露出来。

## 它做了什么

- **[你的永远是你的。](SHOWCASE.md#yours-is-always-yours)** 你自己在右边，你正在打交道的对象在中间，周围的东西在左边。使用物品永远作用在你自己身上；想给别人，就把物品拖到对方身上。
- **[一个目标，一直锁定。](SHOWCASE.md#a-target)** 按 `Tab` 无需走过去就能选中你要打交道的人或物，并一直保持到你放开为止；旁边的操作栏列出你能对它做的一切。
- **[一座村庄，而不是一堆数字。](SHOWCASE.md#your-village)** 名册显示谁不开心，角色面板说明原因，一个按钮直接带你去解决。
- **[告别来回倒腾物品。](SHOWCASE.md#no-more-item-juggling)** 触手可及的所有储物都在一个窗口里，制作时直接取用你的背包和周围的一切。
- **[整场战斗一目了然。](SHOWCASE.md#the-whole-fight)** 谁在战斗中，谁在攻击你，谁正从屏幕外赶来。
- **[每个数字都说明原因。](SHOWCASE.md#on-top)** 每件物品怎么获得，每个剧情选项通向哪里，每个数字背后的规则——支持七种语言。

**[看实际游玩片段 →](SHOWCASE.md)** · **[全部功能，大大小小 →](FEATURES.md)**（英文页面）

## 安装

从 [Releases](../../releases) 下载 `Island-UI-vX.Y.Z-installer.zip`，解压到任意位置，双击 `install.bat`。它会通过 Steam 找到游戏，只在游戏还没有 BepInEx 时才安装 BepInEx，然后把模组复制进去。`uninstall.bat` 可以再把它移除。

<details><summary><b>其他下载方式，以及手动卸载</b></summary>

| 下载 | 适合谁 |
|---|---|
| `Island-UI-vX.Y.Z-installer.zip` | **最简单。** 就是上面的安装器。它不会动其他模组或设置，也不会下载任何东西。 |
| `Island-UI-vX.Y.Z-with-Island-Ex-installer.zip` | 同样的安装器，附带 Island Ex。 |
| `Island-UI-vX.Y.Z-with-BepInEx.zip` | 不想用安装器：解压到游戏文件夹（`Mad Island.exe` 所在的位置）。自带 BepInEx 5.4.23.5。 |
| `Island-UI-vX.Y.Z.zip` | 已经装了 BepInEx 5：解压到游戏文件夹即可。 |
| `Island-Ex-vX.Y.Z.zip` | 只要 Island Ex：解压到游戏文件夹。需要 BepInEx 5。 |

游戏文件夹通常是 `C:\Program Files (x86)\Steam\steamapps\common\Mad Island`（在 Steam 中：右键点击游戏，*管理*，*浏览本地文件*）。Windows 可能会对运行下载的脚本发出警告；安装器是纯文本，可以在 [`installer/`](installer) 中查看。

手动卸载：删除 `BepInEx/plugins/IslandUI` 和 `BepInEx/plugins/islandui-art`（装了 Island Ex 的话还有 `BepInEx/plugins/IslandEx`）。

</details>

## 设置

游戏菜单中 *Option*（选项）下方有一个 **Island UI** 入口。更改立即生效。模组跟随游戏的语言，也可以自己选择：英语、简体中文、日语、俄语、巴西葡萄牙语、西班牙语或德语。

<details><summary><b>设置页面、翻译和配置文件</b></summary>

设置页面分为几组：常规（指针和语言）、目标、战斗、提示、窗口、剧情和快捷键。安装了 **Island Ex** 时，它也有自己的入口。

<img src="SET.jpg" width="480" alt="设置窗口的战斗页面">

翻译由机器生成，欢迎指正：提交一个 issue，或者编辑 `BepInEx/plugins/IslandUI/Localization/<language>/strings.txt`（以及 `item-guide.tsv`、`story-choices.tsv`）后发给我。每个文件的开头都写了做法。

关闭较大的部分（附近窗口、动作栏、目标面板、名册、角色面板）请在 `BepInEx/config/madisland.islandui.cfg` 中设置。

标题画面左下角的模组图标表示它们已经加载；鼠标悬停可查看版本，点击可关闭或开启该模组。

</details>

## Island Ex

<img src="island-ex.png" width="64" align="right" alt="Island Ex">

Island UI 遵守游戏规则：它改变的是你看游戏的方式和做事的途径，从不改变你能做什么。改变规则的调整放在一个单独的小模组 Island Ex 里，在同一个发布中提供。Island UI 不依赖它。

<details><summary><b>Island Ex 做了什么</b></summary>

- **特效过滤**：关闭任何你不想看到的视觉特效。
- **按住挣扎**：被抓住时，按住攻击键会持续攻击。
- **保持在屏幕上**：按 Alt+Tab 或点击另一个显示器时，游戏不再自动最小化。
- **室内不下雨**：雨不再落进屋里。
- **拉远镜头**：镜头可以拉得比游戏允许的更远。
- **保留损坏的工具**：用坏的工具以损坏状态留在背包里，等修理包来修。

</details>

## 局限

Island UI 还很年轻。0.9.0 重做了大部分界面，经过实际游玩，但没有长时间测试：可能会有粗糙之处和零星的 bug。游戏自带的一些界面还没有重建，外观和操作与以前一样。如果出了问题，提交 issue 时附上你的 `BepInEx/LogOutput.log` 最有帮助。

## 兼容性

在 Steam `beta_open` 分支、build 25095856、BepInEx 5.4.23.5 上测试。游戏更新后，在 `BepInEx/LogOutput.log` 中查找 `Patch census [IslandUI]: ... all bound.`。如果显示的是别的内容，说明更新改动了某些东西，模组需要发布新版本。

## 源代码

这里的每个提交都是一个发布版本；版本标签与发布的压缩包对应。

<details><summary><b>目录结构，以及如何构建</b></summary>

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

构建需要 .NET SDK，并且已安装游戏（项目会引用游戏的 DLL）：

```
dotnet build src/IslandUI/IslandUI.csproj -c Release
dotnet build src/IslandEx/IslandEx.csproj -c Release
```

构建会把 DLL 复制到游戏的 `BepInEx/plugins` 中；游戏在其他位置时加上 `-p:GameDir="D:\...\Mad Island"`，或者用 `-p:NoDeploy=true` 只构建不复制。Island UI 还需要把 `art/*.png` 放到 `BepInEx/plugins/islandui-art`。

</details>

## 联系

bug、想法，或者想复用某些内容？[提交 issue](../../issues)，写信到 [maliekee@pm.me](mailto:maliekee@pm.me)，或者在 [Discord](https://discord.com/users/1523362502224777439) 上找我。
