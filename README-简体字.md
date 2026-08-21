# Musixmatch-BetterLyrics
[BetterLyrics](https://github.com/jayfunc/BetterLyrics) 的一个插件，添加 Musixmatch 作为歌词来源

## 安装
前往 [releases](https://github.com/NimiGames68/Musixmatch-BetterLyrics/releases/latest) 下载 .blp 文件，然后运行它。
它应该会打开 BetterLyrics 并要求你重启。

## 编译
首先，下载 [.NET 10](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
同时需要安装 Visual Studio 2026

```
git clone https://github.com/NimiGames68/Musixmatch-BetterLyrics.git
git clone https://github.com/jayfunc/BetterLyrics.git
```

在 Visual Studio 顶部有一个小工具栏，找到一个叫 `Tools`（工具）的选项，点击它，会弹出一个带有几个选项的窗口，点击最后一个叫"选项"的，会打开一个菜单，在左侧面板中找到一个叫 `Manage NuGet packages`（管理 NuGet 包）的选项，点击它，然后往下滚动一会儿，找到一个叫 `Sources`（源）的东西，那里应该有一个叫 `Package Sources`（包源）的选项，旁边有个添加按钮，点击它，命名为 `CommunityToolkit Labs`，URL 填 `https://pkgs.dev.azure.com/dotnet/CommunityToolkit/_packaging/CommunityToolkit-Labs/nuget/v3/index.json`，然后保存。

在 BetterLyrics 文件夹中，进入 `BetterLyrics\src\BetterLyrics.DotNet\BetterLyrics.Core\Constants`，把文件名 `DiscordTemplate`、`LastFMTemplate` 改成 `Discord.cs`、`LastFM.cs`

现在进入 `BetterLyrics\src\BetterLyrics.DotNet`，点击 `BetterLyrics.slnx` 文件，按 ctrl+shift+b 编译它（可能需要一些时间）

编译完 BetterLyrics 之后，你就可以编译插件了

进入 Musixmatch-BetterLyrics 的根目录，打开 `.csproj` 文件，应该有一行写着 `<BetterLyricsHostDir>C:\Users\HP\Projects\BetterLyrics\src\BetterLyrics.DotNet</BetterLyricsHostDir>`，把它改成 `<BetterLyricsHostDir>C:\Users\WINDOWSUSER\BetterLyrics\src\BetterLyrics.DotNet</BetterLyricsHostDir>`（把 WINDOWSUSER 改成你的账户名。如果你把 BetterLyrics 仓库克隆到了别的地方，就把那个文件夹的路径填进去，只要保留后面的 BetterLyrics\src\BetterLyrics.DotNet 就行）

现在打开 `BetterLyrics.Plugins.Source.Musixmatch.slnx` 文件，按 ctrl+shift+b，它应该会生成一个 `Dist` 文件夹，在那里你会找到一个以 `.blp` 结尾的文件，那就是扩展文件

## 截图
|卡拉OK|逐行同步|
|-|-|
| <img src="https://github.com/NimiGames68/Musixmatch-BetterLyrics/blob/main/assets/karaoke.png?raw=true"> | <img src="https://github.com/NimiGames68/Musixmatch-BetterLyrics/blob/main/assets/synced.png?raw=true"> |

> [!IMPORTANT]
> 这可能违反 Musixmatch 的服务条款，它没有使用 Musixmatch 的官方 API，而是使用了一个供其移动应用内部使用的 API（apic-appmobile.musixmatch.com），使用时请谨慎。

## 许可证
MIT

> [!NOTE]
> 此文本由 AI 翻译，可能包含错误
> 这个仓库的主人不懂中文
