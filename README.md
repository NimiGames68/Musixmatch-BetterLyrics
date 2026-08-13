# Musixmatch-BetterLyrics
A plugin for [BetterLyrics](https://github.com/jayfunc/BetterLyrics) that adds Musixmatch as a lyrics source

## Install
Go to the [releases](https://github.com/NimiGames68/Musixmatch-BetterLyrics/releases/latest) and download the .blp file, then run it.

It should open BetterLyrics asking you to restart.

## Building
First things first, download [.NET 10](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)

Also have Visual Studio 2026 ([tutorial](https://www.youtube.com/watch?v=NAfUAxUQVRs))

```
git clone https://github.com/NimiGames68/Musixmatch-BetterLyrics.git
git clone https://github.com/jayfunc/BetterLyrics.git
```

On Visual Studio, on the top there is a little bar, search for an option saying `Tools` (or something like that) click it, it should bring up a pop-up with some options, click on the last one saying options, that brings up a menu, on the left panel, look into an option saying `Manage NuGet packages`, click that and scroll for a bit until you find an thing called `Sources` right there should be an option saying `Package Sources` with an add button, click it and name it `CommunityToolkit Labs` with this URL `https://pkgs.dev.azure.com/dotnet/CommunityToolkit/_packaging/CommunityToolkit-Labs/nuget/v3/index.json` and save it.


On the BetterLyrics folder, go to `BetterLyrics\src\BetterLyrics.DotNet\BetterLyrics.Core\Constants` and change the file names from `DiscordTemplate` `LastFMTemplate` to `Discord.cs` `LastFM.cs`

Now go to `BetterLyrics\src\BetterLyrics.DotNet` and click on the `BetterLyrics.slnx` file, and compile it by pressing ctrl+shift+b (it could take some time)


Once you've compiled BetterLyrics, you are ready to compile the plugin

Go to the root of Musixmatch-BetterLyrics, and open the `.csproj` file, there should be a line saying `<BetterLyricsHostDir>C:\Users\HP\Projects\BetterLyrics\src\BetterLyrics.DotNet</BetterLyricsHostDir>`, change that to `<BetterLyricsHostDir>C:\Users\WINDOWSUSER\BetterLyrics\src\BetterLyrics.DotNet</BetterLyricsHostDir>` (change WINDOWSUSER to the name of your account. If you cloned the BetterLyrics repo onto somewhere else, put the path of the folder there, just keep the BetterLyrics\src\BetterLyrics.DotNet after)

Now open the `BetterLyrics.Plugins.Source.Musixmatch.slnx` file and press crtl+shift+b, and it should generate a `Dist` folder, there you will find a file ending in the `.blp` file extension, that is the extension

## Screenshots

|Karaoke|Line Synced|
|-|-|
| <img src="https://github.com/NimiGames68/Musixmatch-BetterLyrics/blob/main/assets/karaoke.png?raw=true"> | <img src="https://github.com/NimiGames68/Musixmatch-BetterLyrics/blob/main/assets/synced.png?raw=true"> |

## License

MIT
