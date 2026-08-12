using BetterLyrics.Sdk.Abstractions.Plugins;
using System.ComponentModel.DataAnnotations;

namespace BetterLyrics.Plugins.Source.Musixmatch
{
    public class Config : PluginConfigBase
    {

        [Display(Name = "Musixmatch User Token", Description = "Optional. Leave empty to let the plugin fetch and cache a token automatically.")]
        public string Token
        {
            get => Get("");
            set => Set(value);
        }

        [Display(Name = "Translation Language", Description = "ISO language code for line translations fetched from Musixmatch (e.g. pt, en, es). Use 'none' to disable.")]
        public string TranslationLanguage
        {
            get => Get("none");
            set => Set(value);
        }

        [Display(Name = "Prefer Word-by-word Lyrics", Description = "When available, use Musixmatch's word-by-word timing instead of line-only synced lyrics.")]
        public bool PreferWordByWord
        {
            get => Get(true);
            set => Set(value);
        }
    }
}
