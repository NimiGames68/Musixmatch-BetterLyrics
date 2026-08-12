using BetterLyrics.Plugins.Source.Musixmatch.Serialization;
using BetterLyrics.Sdk.Abstractions.Plugins;
using BetterLyrics.Sdk.Interfaces.Plugins;
using BetterLyrics.Sdk.Models.Lyrics;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace BetterLyrics.Plugins.Source.Musixmatch
{
    public class Plugin : PluginBase<Config>, ILyricsSource
    {
        private const string Host = "https://apic-appmobile.musixmatch.com/ws/1.1/";
        private const string AppId = "mac-ios-v2.0";

        private readonly HttpClient _httpClient;

        public override string Title { get; set; } = "Musixmatch";

        public Plugin()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Cookie", "x-mxm-token-guid=");
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("x-mxm-app-version", "10.1.1");
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-User-Agent",
                "Musixmatch/2025120901 CFNetwork/3860.300.31 Darwin/25.2.0");
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9");
        }

        protected override Task OnInitializeAsync()
        {
            return Task.CompletedTask;
        }

        protected override Task OnShutdownAsync()
        {
            _httpClient.Dispose();
            return Task.CompletedTask;
        }

        public async Task<LyricsSearchResult> GetLyricsAsync(string title, string artist, string album,
            double duration, CancellationToken token)
        {
            var userToken = await EnsureTokenAsync(token);

            var (macro, requestUrl) = await FetchMacroAsync(title, artist, album, duration, userToken, token);

            var matcherStatus = GetStatusCode(macro, "matcher.track.get");
            if (matcherStatus == 401)
            {
                userToken = await EnsureTokenAsync(token, forceRefresh: true);
                (macro, requestUrl) = await FetchMacroAsync(title, artist, album, duration, userToken, token);
                matcherStatus = GetStatusCode(macro, "matcher.track.get");
            }

            if (matcherStatus != 200)
                throw new Exception($"Musixmatch: track not found (status {matcherStatus}).");

            var meta = macro.GetProperty("matcher.track.get").GetProperty("message").GetProperty("body")
                .GetProperty("track");

            var isRestricted = macro.TryGetProperty("track.lyrics.get", out var lyricsGetEl) &&
                                lyricsGetEl.TryGetProperty("message", out var lyricsMsgEl) &&
                                lyricsMsgEl.TryGetProperty("body", out var lyricsBodyEl) &&
                                lyricsBodyEl.TryGetProperty("lyrics", out var lyricsEl) &&
                                lyricsEl.TryGetProperty("restricted", out var restrictedEl) &&
                                restrictedEl.ValueKind == JsonValueKind.True;

            if (isRestricted)
                throw new Exception("Musixmatch: not authorized to display these lyrics.");

            var resultTitle = GetStringOrNull(meta, "track_name") ?? title;
            var resultArtist = GetStringOrNull(meta, "artist_name") ?? artist;
            var resultAlbum = GetStringOrNull(meta, "album_name") ?? album;
            var resultDuration = TryGetDouble(meta, "track_length") ?? duration;

            var isInstrumental = TryGetBool(meta, "instrumental");
            if (isInstrumental)
            {
                return new LyricsSearchResult(
                    resultTitle, resultArtist, resultAlbum, resultDuration,
                    "[00:00.000]♪ Instrumental ♪\n[99:00.000]",
                    null, null, requestUrl);
            }

            var hasRichsync = TryGetBool(meta, "has_richsync");
            var hasSubtitles = TryGetBool(meta, "has_subtitles");
            var hasLyrics = TryGetBool(meta, "has_lyrics") || TryGetBool(meta, "has_lyrics_crowd");

            string? raw = null;
            Dictionary<string, double>? lineTimeMap = null;

            if (hasRichsync && Config.PreferWordByWord)
            {
                var commontrackId = TryGetInt64(meta, "commontrack_id");
                var trackLength = TryGetDouble(meta, "track_length") ?? duration;

                var richsyncLines = await FetchRichsyncAsync(commontrackId, trackLength, userToken, token);
                if (richsyncLines is { Count: > 0 })
                {
                    raw = BuildEnhancedLrc(richsyncLines, out lineTimeMap);
                }
            }

            if (raw == null && hasSubtitles)
            {
                var syncedLines = TryGetSubtitleLines(macro);
                if (syncedLines is { Count: > 0 })
                {
                    raw = BuildStandardLrc(syncedLines, out lineTimeMap);
                }
            }

            if (raw == null && hasLyrics)
            {
                var plain = TryGetPlainLyrics(macro);
                if (!string.IsNullOrWhiteSpace(plain))
                {
                    raw = plain;
                    lineTimeMap = null;
                }
            }

            if (raw == null)
                throw new Exception("Musixmatch: no lyrics available for this track.");

            string? translation = null;
            if (lineTimeMap is { Count: > 0 } && !string.Equals(Config.TranslationLanguage, "none",
                    StringComparison.OrdinalIgnoreCase))
            {
                var trackId = TryGetInt64(meta, "track_id");
                if (trackId.HasValue)
                {
                    translation = await BuildTranslationLrcAsync(trackId.Value, lineTimeMap, userToken, token);
                }
            }

            return new LyricsSearchResult(resultTitle, resultArtist, resultAlbum, resultDuration, raw, translation,
                null, requestUrl);
        }


        private async Task<string> EnsureTokenAsync(CancellationToken token, bool forceRefresh = false)
        {
            if (!forceRefresh && !string.IsNullOrWhiteSpace(Config.Token))
                return Config.Token;

            var url = $"{Host}token.get?app_id={AppId}";
            var json = await FetchJsonAsync(url, token);

            var status = GetStatusCode(json);
            if (status != 200)
                throw new Exception($"Musixmatch: failed to obtain a user token (status {status}). Try again in a moment.");

            var userToken = json.GetProperty("message").GetProperty("body").GetProperty("user_token").GetString();
            if (string.IsNullOrWhiteSpace(userToken))
                throw new Exception("Musixmatch: received an empty user token.");

            Config.Token = userToken;
            return userToken;
        }


        private async Task<(JsonElement Macro, string Url)> FetchMacroAsync(string title, string artist,
            string album, double duration, string userToken, CancellationToken token)
        {
            var floorDuration = Math.Floor(duration).ToString(CultureInfo.InvariantCulture);
            var durationStr = duration.ToString(CultureInfo.InvariantCulture);

            var query = new List<KeyValuePair<string, string>>
            {
                new("q_album", album ?? string.Empty),
                new("q_artist", artist ?? string.Empty),
                new("q_artists", artist ?? string.Empty),
                new("q_track", title ?? string.Empty),
                new("q_duration", durationStr),
                new("f_subtitle_length", floorDuration),
                new("usertoken", userToken),
                new("part", "track_lyrics_translation_status")
            };

            var url = $"{Host}macro.subtitles.get?format=json&namespace=lyrics_richsynched&subtitle_format=mxm&app_id={AppId}&" +
                       BuildQuery(query);

            var body = await FetchJsonAsync(url, token);
            var macro = body.GetProperty("message").GetProperty("body").GetProperty("macro_calls");
            return (macro, url);
        }

        private async Task<List<RichsyncLine>?> FetchRichsyncAsync(long? commontrackId, double trackLength,
            string userToken, CancellationToken token)
        {
            if (commontrackId is null) return null;

            var query = new List<KeyValuePair<string, string>>
            {
                new("f_subtitle_length", trackLength.ToString(CultureInfo.InvariantCulture)),
                new("q_duration", trackLength.ToString(CultureInfo.InvariantCulture)),
                new("commontrack_id", commontrackId.Value.ToString(CultureInfo.InvariantCulture)),
                new("usertoken", userToken)
            };

            var url = $"{Host}track.richsync.get?format=json&subtitle_format=mxm&app_id={AppId}&" + BuildQuery(query);

            var json = await FetchJsonAsync(url, token);
            if (GetStatusCode(json) != 200) return null;

            if (!json.GetProperty("message").GetProperty("body").TryGetProperty("richsync", out var richsyncEl))
                return null;

            if (!richsyncEl.TryGetProperty("richsync_body", out var bodyEl) || bodyEl.ValueKind != JsonValueKind.String)
                return null;

            var richsyncJson = bodyEl.GetString();
            if (string.IsNullOrWhiteSpace(richsyncJson)) return null;

            var arr = JsonSerializer.Deserialize(richsyncJson, SourceGenerationContext.Default.JsonElement);
            if (arr.ValueKind != JsonValueKind.Array) return null;

            var lines = new List<RichsyncLine>();
            foreach (var lineEl in arr.EnumerateArray())
            {
                var startMs = (lineEl.TryGetProperty("ts", out var tsEl) ? tsEl.GetDouble() : 0) * 1000;
                var endMs = (lineEl.TryGetProperty("te", out var teEl) ? teEl.GetDouble() : 0) * 1000;

                var words = new List<RichsyncWord>();
                if (lineEl.TryGetProperty("l", out var wordsEl) && wordsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var wordEl in wordsEl.EnumerateArray())
                    {
                        var text = wordEl.TryGetProperty("c", out var cEl) ? cEl.GetString() ?? string.Empty : string.Empty;
                        var offsetMs = (wordEl.TryGetProperty("o", out var oEl) ? oEl.GetDouble() : 0) * 1000;
                        words.Add(new RichsyncWord(startMs + offsetMs, text));
                    }
                }

                lines.Add(new RichsyncLine(startMs, endMs, words));
            }

            return lines;
        }

        private async Task<string?> BuildTranslationLrcAsync(long trackId, Dictionary<string, double> lineTimeMap,
            string userToken, CancellationToken token)
        {
            var query = new List<KeyValuePair<string, string>>
            {
                new("track_id", trackId.ToString(CultureInfo.InvariantCulture)),
                new("selected_language", Config.TranslationLanguage),
                new("usertoken", userToken)
            };

            var url = $"{Host}crowd.track.translations.get?translation_fields_set=minimal&comment_format=text&format=json&app_id={AppId}&" +
                       BuildQuery(query);

            JsonElement json;
            try
            {
                json = await FetchJsonAsync(url, token);
            }
            catch
            {
                return null;
            }

            if (GetStatusCode(json) != 200) return null;

            if (!json.GetProperty("message").GetProperty("body").TryGetProperty("translations_list", out var listEl) ||
                listEl.ValueKind != JsonValueKind.Array)
                return null;

            var sb = new StringBuilder();
            foreach (var item in listEl.EnumerateArray())
            {
                if (!item.TryGetProperty("translation", out var translationEl)) continue;

                var matchedLine = translationEl.TryGetProperty("matched_line", out var mlEl) ? mlEl.GetString() : null;
                var description = translationEl.TryGetProperty("description", out var dEl) ? dEl.GetString() : null;

                if (string.IsNullOrWhiteSpace(matchedLine) || string.IsNullOrWhiteSpace(description)) continue;

                var key = NormalizeForMatch(matchedLine);
                if (lineTimeMap.TryGetValue(key, out var startMs))
                {
                    sb.Append('[').Append(FormatTime(startMs)).Append(']').Append(SanitizeLrcText(description))
                        .Append('\n');
                }
            }

            return sb.Length > 0 ? sb.ToString() : null;
        }

        private async Task<JsonElement> FetchJsonAsync(string url, CancellationToken token)
        {
            using var response = await _httpClient.GetAsync(url, token);
            var content = await response.Content.ReadAsStringAsync(token);
            return JsonSerializer.Deserialize(content, SourceGenerationContext.Default.JsonElement);
        }


        private static List<(double StartMs, string Text)>? TryGetSubtitleLines(JsonElement macro)
        {
            if (!macro.TryGetProperty("track.subtitles.get", out var subEl)) return null;
            if (!subEl.TryGetProperty("message", out var msgEl)) return null;
            if (!msgEl.TryGetProperty("body", out var bodyEl)) return null;
            if (!bodyEl.TryGetProperty("subtitle_list", out var listEl) || listEl.ValueKind != JsonValueKind.Array || listEl.GetArrayLength() == 0)
                return null;

            var first = listEl[0];
            if (!first.TryGetProperty("subtitle", out var subtitleEl)) return null;
            if (!subtitleEl.TryGetProperty("subtitle_body", out var bodyStrEl) || bodyStrEl.ValueKind != JsonValueKind.String)
                return null;

            var raw = bodyStrEl.GetString();
            if (string.IsNullOrWhiteSpace(raw)) return null;

            var arr = JsonSerializer.Deserialize(raw, SourceGenerationContext.Default.JsonElement);
            if (arr.ValueKind != JsonValueKind.Array) return null;

            var result = new List<(double, string)>();
            foreach (var lineEl in arr.EnumerateArray())
            {
                var text = lineEl.TryGetProperty("text", out var tEl) ? tEl.GetString() ?? string.Empty : string.Empty;
                double startMs = 0;
                if (lineEl.TryGetProperty("time", out var timeEl) && timeEl.TryGetProperty("total", out var totalEl))
                    startMs = totalEl.GetDouble() * 1000;

                result.Add((startMs, string.IsNullOrEmpty(text) ? "♪" : text));
            }

            return result;
        }

        private static string? TryGetPlainLyrics(JsonElement macro)
        {
            if (!macro.TryGetProperty("track.lyrics.get", out var el)) return null;
            if (!el.TryGetProperty("message", out var msgEl)) return null;
            if (!msgEl.TryGetProperty("body", out var bodyEl)) return null;
            if (!bodyEl.TryGetProperty("lyrics", out var lyricsEl)) return null;
            if (!lyricsEl.TryGetProperty("lyrics_body", out var bodyStrEl) || bodyStrEl.ValueKind != JsonValueKind.String)
                return null;

            return bodyStrEl.GetString();
        }

        private static int GetStatusCode(JsonElement macro, string call)
        {
            if (!macro.TryGetProperty(call, out var callEl)) return -1;
            return GetStatusCode(callEl);
        }

        private static int GetStatusCode(JsonElement message)
        {
            try
            {
                return message.GetProperty("message").GetProperty("header").GetProperty("status_code").GetInt32();
            }
            catch
            {
                return -1;
            }
        }

        private static string? GetStringOrNull(JsonElement el, string prop) =>
            el.TryGetProperty(prop, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

        private static double? TryGetDouble(JsonElement el, string prop)
        {
            if (!el.TryGetProperty(prop, out var p)) return null;
            return p.ValueKind switch
            {
                JsonValueKind.Number => p.GetDouble(),
                JsonValueKind.String when double.TryParse(p.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d) => d,
                _ => null
            };
        }

        private static long? TryGetInt64(JsonElement el, string prop)
        {
            if (!el.TryGetProperty(prop, out var p)) return null;
            return p.ValueKind switch
            {
                JsonValueKind.Number => p.GetInt64(),
                JsonValueKind.String when long.TryParse(p.GetString(), out var l) => l,
                _ => null
            };
        }

        private static bool TryGetBool(JsonElement el, string prop)
        {
            if (!el.TryGetProperty(prop, out var p)) return false;
            return p.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Number => p.GetInt32() != 0,
                _ => false
            };
        }


        private static string BuildStandardLrc(List<(double StartMs, string Text)> lines,
            out Dictionary<string, double> lineTimeMap)
        {
            var sb = new StringBuilder();
            lineTimeMap = new Dictionary<string, double>();

            foreach (var (startMs, text) in lines)
            {
                var clean = SanitizeLrcText(text);
                sb.Append('[').Append(FormatTime(startMs)).Append(']').Append(clean).Append('\n');

                var key = NormalizeForMatch(text);
                if (key.Length > 0) lineTimeMap.TryAdd(key, startMs);
            }

            return sb.ToString();
        }

        private static string BuildEnhancedLrc(List<RichsyncLine> lines, out Dictionary<string, double> lineTimeMap)
        {
            var sb = new StringBuilder();
            lineTimeMap = new Dictionary<string, double>();

            foreach (var line in lines)
            {
                sb.Append('[').Append(FormatTime(line.StartMs)).Append(']');

                var plainTextBuilder = new StringBuilder();
                foreach (var word in line.Words)
                {
                    var clean = SanitizeLrcWordText(word.Text);
                    sb.Append('<').Append(FormatTime(word.StartMs)).Append('>').Append(clean);
                    plainTextBuilder.Append(word.Text);
                }

                sb.Append('<').Append(FormatTime(line.EndMs)).Append('>');
                sb.Append('\n');

                var key = NormalizeForMatch(plainTextBuilder.ToString());
                if (key.Length > 0) lineTimeMap.TryAdd(key, line.StartMs);
            }

            return sb.ToString();
        }

        private static string FormatTime(double ms)
        {
            if (ms < 0) ms = 0;
            var totalMs = (long)Math.Round(ms);
            var minutes = totalMs / 60000;
            var seconds = totalMs % 60000 / 1000;
            var millis = totalMs % 1000;
            return $"{minutes:00}:{seconds:00}.{millis:000}";
        }

        private static string SanitizeLrcText(string? text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text
                .Replace("\r", "")
                .Replace("\n", " ")
                .Replace('[', '(')
                .Replace(']', ')')
                .Replace('<', '(')
                .Replace('>', ')')
                .Trim();
        }

        private static string SanitizeLrcWordText(string? text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text
                .Replace("\r", "")
                .Replace("\n", " ")
                .Replace('[', '(')
                .Replace(']', ')')
                .Replace('<', '(')
                .Replace('>', ')');
        }

        private static string NormalizeForMatch(string? text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            Span<char> buffer = stackalloc char[text.Length];
            var count = 0;
            foreach (var c in text)
            {
                if (!char.IsWhiteSpace(c))
                    buffer[count++] = char.ToLowerInvariant(c);
            }
            return new string(buffer[..count]);
        }

        private static string BuildQuery(IEnumerable<KeyValuePair<string, string>> parameters) =>
            string.Join("&", parameters.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value ?? string.Empty)}"));

        private sealed record RichsyncWord(double StartMs, string Text);

        private sealed record RichsyncLine(double StartMs, double EndMs, List<RichsyncWord> Words);
    }
}
