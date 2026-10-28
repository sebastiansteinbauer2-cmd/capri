using System;
using System.Collections.Generic;
using System.IO;
#if !CROSS
using System.IO.Compression;
using System.Media;
#endif
using System.Net;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    [DataContract]
    internal sealed class VoiceEntry
    {
        [DataMember(Name = "voice_id")] public string Id;
        [DataMember(Name = "name")] public string Name;
        [DataMember(Name = "category")] public string Category;
    }

    [DataContract]
    internal sealed class VoicePage
    {
        [DataMember(Name = "voices")] public List<VoiceEntry> Voices;
        [DataMember(Name = "has_more")] public bool More;
        [DataMember(Name = "next_page_token")] public string Next;
    }

    [DataContract]
    internal sealed class VoiceModel
    {
        [DataMember(Name = "model_id")] public string Id;
        [DataMember(Name = "name")] public string Name;
        [DataMember(Name = "can_do_text_to_speech")] public bool Speaks;
    }

    internal sealed class VoiceException : Exception
    {
        internal readonly int Status;

        internal VoiceException(string message, int status = 0) : base(message) { Status = status; }
    }

    internal sealed class VoiceListing
    {
        internal List<VoiceEntry> Voices = new List<VoiceEntry>();
        internal List<VoiceModel> Models = new List<VoiceModel>();
    }

    internal sealed class Voice
    {
        internal static string Host = "https://api.elevenlabs.io";
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Capri.ElevenLabs");
        internal volatile string Key;
        internal volatile string VoiceId;
        internal volatile string Model;
#if CROSS
        internal static Action<byte[]> Playback;
        internal static Action Silence;
#else
        private SoundPlayer player;
#endif
        internal static volatile int Loudness = 100;

        internal static short[] Louder(short[] samples)
        {
            int percent = Loudness;
            if (percent == 100) return samples;
            short[] scaled = new short[samples.Length];
            for (int i = 0; i < samples.Length; i++) scaled[i] = (short)Math.Max(short.MinValue, Math.Min(short.MaxValue, samples[i] * percent / 100));
            return scaled;
        }

        internal static byte[] Louder(byte[] wav)
        {
            if (Loudness == 100) return wav;
            int rate;
            short[] samples = Samples(wav, out rate);
            return samples.Length == 0 ? wav : Wave(Louder(samples), rate);
        }

        internal bool Ready { get { return Key != null && VoiceId != null; } }

#if CROSS
        internal static string Protect(string key) { throw new PlatformNotSupportedException("Saving keys isn't ready on this system yet."); }

        internal static string Unprotect(string saved) { return null; }
#else
        internal static string Protect(string key) { return Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(key), Entropy, DataProtectionScope.CurrentUser)); }

        internal static string Unprotect(string saved)
        {
            if (string.IsNullOrEmpty(saved)) return null;
            try { return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(saved), Entropy, DataProtectionScope.CurrentUser)); }
            catch (CryptographicException) { return null; }
            catch (FormatException) { return null; }
        }
#endif

        internal static string Tail(string key) { return key == null || key.Length < 4 ? "" : key.Substring(key.Length - 4); }

        internal static string Spoken(string said)
        {
            string words = Regex.Replace(Regex.Replace(said ?? "", @"\[[^\]]*\]", " ").Replace("*", ""), @"\s+", " ").Trim();
            return Regex.IsMatch(words, @"[\p{L}\p{Nd}]") ? words : "";
        }

        private static readonly string[,] Respelled = { { "chooms", "chewms" }, { "choom", "chewm" }, { "preem", "preme" }, { "yezzirrr", "yessirrr" } };

        internal static string Sayable(string text) { return Swap(Plain(text), 0, 1); }

        private static string Plain(string text)
        {
            text = Regex.Replace(text ?? "", @"https?://(?:www\.)?([^/\s]+?)(?:/\S*?)?(?=[.,!?)]*(?:\s|$))", delegate(Match link) { return link.Groups[1].Value.Replace(".", " dot "); });
            text = Regex.Replace(text, @"[\uD800-\uDBFF][\uDC00-\uDFFF]|[☀-➿️‍]", "");
            text = Regex.Replace(text, @"\s*&\s*", " and ");
            text = Regex.Replace(text, @"(\d)\s*%", "$1 percent");
            text = Regex.Replace(text, @"\bw/(?=\s)", "with");
            text = Regex.Replace(text, @"\s*(->|=>|~+)\s*", " ");
            return Regex.Replace(text, @"\s+", " ").Trim();
        }

        internal static string Unsaid(string text) { return Swap(text, 1, 0); }

        private static string Swap(string text, int from, int to)
        {
            text = text ?? "";
            for (int i = 0; i < Respelled.GetLength(0); i++)
            {
                string target = Respelled[i, to];
                text = Regex.Replace(text, @"\b" + Respelled[i, from] + @"\b", delegate(Match found)
                {
                    string word = found.Value;
                    if (word.ToUpperInvariant() == word) return target.ToUpperInvariant();
                    return char.IsUpper(word[0]) ? char.ToUpperInvariant(target[0]) + target.Substring(1) : target;
                }, RegexOptions.IgnoreCase);
            }
            return text;
        }

        internal static HttpWebRequest Open(string path, string key, string method)
        {
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(Host + path);
            request.Method = method;
            request.UserAgent = "Capri-Desktop";
            request.Headers["xi-api-key"] = key;
            request.ServicePoint.Expect100Continue = false;
            request.Timeout = 20000;
            request.ReadWriteTimeout = 20000;
            return request;
        }

        internal static byte[] Send(HttpWebRequest request, byte[] body)
        {
            try
            {
                if (body != null)
                {
                    request.ContentType = "application/json";
                    request.ContentLength = body.Length;
                    using (Stream upload = request.GetRequestStream()) upload.Write(body, 0, body.Length);
                }
                using (WebResponse response = request.GetResponse())
                using (Stream stream = response.GetResponseStream())
                using (MemoryStream copy = new MemoryStream())
                {
                    stream.CopyTo(copy);
                    return copy.ToArray();
                }
            }
            catch (WebException error)
            {
                HttpWebResponse response = error.Response as HttpWebResponse;
                if (response == null) throw new VoiceException("Can't reach ElevenLabs. Check your internet.");
                int status = (int)response.StatusCode;
                string detail;
                using (response)
                using (StreamReader reader = new StreamReader(response.GetResponseStream())) detail = reader.ReadToEnd();
                throw new VoiceException(Explain(status, detail), status);
            }
            catch (IOException) { throw new VoiceException("Can't reach ElevenLabs. Check your internet."); }
        }

        internal const string Rejected = "ElevenLabs rejected the key. Press Forget, paste a new one.";
        internal const string RejectedNew = "ElevenLabs rejected that key. Copy it again from ElevenLabs and paste it.";

        internal const string Restricted = "ElevenLabs has restricted your account, so her voice can't connect. Sign in at elevenlabs.io to see why, or pick FREE VOICE in the Mainframe.";

        internal static string Unsaved(string problem) { return problem == Rejected ? RejectedNew : problem; }

        internal static string Closed(string reason)
        {
            if (Regex.IsMatch(reason ?? "", "account_restricted|workspace is restricted", RegexOptions.IgnoreCase)) return Restricted;
            return Short("Voice link closed: " + reason);
        }

        internal static string Explain(int status, string detail)
        {
            detail = detail ?? "";
            if (detail.Contains("quota_exceeded")) return "Out of ElevenLabs credits. Pick FREE VOICE in the Mainframe.";
            if (detail.Contains("account_restricted")) return Restricted;
            if (detail.Contains("missing_permissions")) return "Key is missing permissions. Make a new ElevenLabs key with Restrict key turned off.";
            if (detail.Contains("voice_not_found")) return "Voice not found. Add it to your ElevenLabs voices.";
            // Voices other people shared in the Voice Library only work outside ElevenLabs' site on paid plans; a fresh clone may still be training.
            if (Regex.IsMatch(detail, @"free_users_not_allowed|library voices?", RegexOptions.IgnoreCase)) return LibraryVoice;
            if (Regex.IsMatch(detail, @"fine.?tun", RegexOptions.IgnoreCase)) return "That voice is still training on ElevenLabs. Pick another until it's ready.";
            if (detail.Contains("invalid_api_key")) return Rejected;
            Match message = Regex.Match(detail, "\"message\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
            string text = message.Success ? Regex.Unescape(message.Groups[1].Value).Trim() : "";
            if (Regex.IsMatch(text, @"empty text|non-empty", RegexOptions.IgnoreCase)) return Silent;
            if (status == 403 || Regex.IsMatch(text, @"permission|not allowed|forbidden|unauthori[sz]ed|access", RegexOptions.IgnoreCase)) return "Key can't do that. Make a new ElevenLabs key with Restrict key turned off.";
            // A new key can't fix a voice, model or plan problem, so those skip the key hint.
            if (text.Length > 0) return Short(text) + (status == 401 || status == 402 || status == 429 || Regex.IsMatch(text, @"unusual activity|\b(voices?|models?|plans?|subscription|upgrade)\b", RegexOptions.IgnoreCase) ? "" : RestrictHint);
            if (status == 401) return Rejected;
            if (status == 429) return "ElevenLabs is busy. Try again in a moment.";
            return "ElevenLabs error " + status + "." + RestrictHint;
        }

        internal const string Silent = "That reply had nothing to say out loud.";
        internal const string LibraryVoice = "ElevenLabs only lets paid plans use Voice Library voices in apps. Pick a voice you made or a default one.";
        internal const string RestrictHint = " Still failing? Make a new key with Restrict key turned off.";

        internal static string Short(string text)
        {
            text = (text ?? "").Trim();
            return text.Length > 72 ? text.Substring(0, 69) + "..." : text;
        }

        internal static T Read<T>(byte[] json) { using (MemoryStream stream = new MemoryStream(json)) return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream); }

        internal const string Unreadable = "ElevenLabs sent a reply Capri couldn't read. Right-click her, Help, Copy error details, and send it in the Discord.";

        internal static T Reply<T>(byte[] json, string path)
        {
            try { return Read<T>(json); }
            catch (SerializationException)
            {
                string text = Encoding.UTF8.GetString(json).Trim();
                Brain.Log(Host + path, -1, "Unreadable " + typeof(T).Name + " reply: " + (text.Length > 200 ? text.Substring(0, 200) : text));
                throw new VoiceException(Unreadable);
            }
        }

        internal static VoiceListing Fetch(string key)
        {
            VoiceListing listing = new VoiceListing();
            string next = null;
            for (int page = 0; page < 10; page++)
            {
                string path = "/v2/voices?page_size=100" + (next == null ? "" : "&next_page_token=" + Uri.EscapeDataString(next));
                VoicePage found = Reply<VoicePage>(Send(Open(path, key, "GET"), null), "/v2/voices");
                if (found.Voices != null) listing.Voices.AddRange(found.Voices);
                if (!found.More || string.IsNullOrEmpty(found.Next)) break;
                next = found.Next;
            }
            try { foreach (VoiceModel model in Read<List<VoiceModel>>(Send(Open("/v1/models", key, "GET"), null))) if (model.Speaks) listing.Models.Add(model); }
            catch (VoiceException error) { if (error.Status == 401) throw; }
            catch (SerializationException) { }
            listing.Voices.Sort(delegate(VoiceEntry a, VoiceEntry b)
            {
                int order = (a.Category == "premade").CompareTo(b.Category == "premade");
                return order != 0 ? order : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
            return listing;
        }

        internal byte[] Speak(string text)
        {
            string key = Key;
            string id = VoiceId;
            string model = Model;
            if (key == null || id == null) throw new VoiceException("No voice picked.");
            StringBuilder body = new StringBuilder("{\"text\":");
            string said = Sayable(text);
            Spend.Voice(said.Length);  // ElevenLabs charges about a credit per letter
            body.Append(Quote(said));
            if (!string.IsNullOrEmpty(model)) body.Append(",\"model_id\":").Append(Quote(model));
            body.Append('}');
            return Clean(Send(Open("/v1/text-to-speech/" + Uri.EscapeDataString(id) + "?output_format=wav_24000", key, "POST"), Encoding.UTF8.GetBytes(body.ToString())));
        }

        internal static string Quote(string value)
        {
            StringBuilder quoted = new StringBuilder("\"");
            foreach (char c in value)
            {
                if (c == '"' || c == '\\') quoted.Append('\\').Append(c);
                else if (c < ' ') quoted.Append("\\u").Append(((int)c).ToString("x4"));
                else quoted.Append(c);
            }
            return quoted.Append('"').ToString();
        }

        internal static short[] Samples(byte[] wav, out int rate)
        {
            rate = 24000;
            int at = 12;
            while (at + 8 <= wav.Length)
            {
                string chunk = Encoding.ASCII.GetString(wav, at, 4);
                int size = BitConverter.ToInt32(wav, at + 4);
                if (chunk == "fmt " && at + 16 <= wav.Length) rate = Math.Max(1000, BitConverter.ToInt32(wav, at + 12));
                if (chunk == "data")
                {
                    int available = wav.Length - at - 8;
                    int count = (size > 0 && size <= available ? size : available) / 2;
                    short[] samples = new short[count];
                    Buffer.BlockCopy(wav, at + 8, samples, 0, count * 2);
                    return samples;
                }
                long next = (long)at + 8 + size + (size & 1);
                if (size < 0 || next > wav.Length) break;
                at = (int)next;
            }
            return new short[0];
        }

        internal static int[] Mouths(byte[] wav, double perSecond)
        {
            int rate;
            short[] samples = Samples(wav, out rate);
            int step = Math.Max(1, (int)(rate / perSecond));
            int count = samples.Length / step;
            double[] levels = new double[count];
            for (int i = 0; i < count; i++)
            {
                double sum = 0;
                for (int j = i * step; j < (i + 1) * step; j++) sum += (double)samples[j] * samples[j];
                levels[i] = Math.Sqrt(sum / step) / 32768.0;
            }
            double loudest = 0;
            foreach (double level in levels) loudest = Math.Max(loudest, level);
            double quiet = Math.Max(0.004, loudest * 0.08);
            List<double> talking = new List<double>();
            foreach (double level in levels) if (level > quiet) talking.Add(level);
            talking.Sort();
            Func<double, double> cut = delegate(double q) { return talking.Count == 0 ? 1 : talking[Math.Min(talking.Count - 1, (int)(q * talking.Count))]; };
            double low = cut(0.35), mid = cut(0.7), high = cut(0.97);
            int[] shapes = new int[count];
            for (int i = 0; i < count; i++)
            {
                double level = levels[i];
                shapes[i] = level <= quiet ? -1 : level < low ? (i % 3 == 0 ? 0 : 1) : level < mid ? (i % 2 == 1 ? 2 : 1) : level < high ? 3 : 4;
            }
            return shapes;
        }

        internal static double Seconds(byte[] wav)
        {
            int rate;
            return Samples(wav, out rate).Length / (double)rate;
        }

        internal static byte[] Clean(byte[] wav)
        {
            int rate;
            short[] samples = Samples(wav, out rate);
            return Wave(samples, rate);
        }

        internal static byte[] Wave(short[] samples, int rate)
        {
            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + samples.Length * 2);
                writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)1);
                writer.Write(rate);
                writer.Write(rate * 2);
                writer.Write((short)2);
                writer.Write((short)16);
                writer.Write(Encoding.ASCII.GetBytes("data"));
                writer.Write(samples.Length * 2);
                byte[] data = new byte[samples.Length * 2];
                Buffer.BlockCopy(samples, 0, data, 0, data.Length);
                writer.Write(data);
                writer.Flush();
                return stream.ToArray();
            }
        }

        internal void Play(byte[] wav)
        {
            wav = Louder(wav);
#if CROSS
            if (Playback != null) Playback(wav);
#else
            Stop();
            player = new SoundPlayer(new MemoryStream(wav));
            player.Play();
#endif
        }

        internal void Stop()
        {
#if CROSS
            if (Silence != null) Silence();
#else
            if (player == null) return;
            player.Stop();
            player.Dispose();
            player = null;
#endif
        }
    }

    internal enum Cue { Arrive, Leave, Hop, Click }

    internal static class Sfx
    {
        internal const int Rate = 24000;
        internal static volatile int Level = 60;
        internal static volatile bool Typing = true;
        internal static Action<short[]> Out;
        internal static Action<short[]> TypeOut;
        internal static Action Untyped;
        internal const double TypeLimit = 6;

        internal static void Play(Cue cue)
        {
            Action<short[]> output = Out;
            int level = Level;
            if (output != null && level > 0) output(Make(cue, level));
        }

        internal static void Type(string text, double pace, int seed)
        {
            Untype();
            Action<short[]> output = TypeOut;
            int level = Level;
            if (output != null && Typing && level > 0 && !string.IsNullOrEmpty(text)) output(Typed(text, pace, level, seed));
        }

        internal static void Untype()
        {
            Action hush = Untyped;
            if (hush != null) hush();
        }

        internal static short[] Typed(string text, double pace, int level, int seed)
        {
            if (string.IsNullOrEmpty(text) || pace <= 0 || level <= 0) return new short[0];
            double seconds = Math.Min(TypeLimit, text.Length / pace) + 0.05;
            int count = (int)(seconds * Rate);
            short[] samples = new short[count];
            Random random = new Random(seed);
            double peak = 0.09 * Math.Min(100, level) / 100.0;
            int length = Rate * 18 / 1000;
            for (int i = 0; i < text.Length; i += 2)
            {
                if (!char.IsLetterOrDigit(text[i])) continue;
                double at = i / pace;
                if (at >= TypeLimit) break;
                double fade = at < TypeLimit - 1 ? 1 : TypeLimit - at;
                int start = (int)(at * Rate) + random.Next(0, Rate / 200);
                double pitch = 1900 * (0.9 + random.NextDouble() * 0.2);
                double loud = peak * fade * (0.7 + random.NextDouble() * 0.3);
                double phase = 0, smooth = 0;
                for (int j = 0; j < length && start + j < count; j++)
                {
                    double t = (double)j / Rate;
                    phase += 2 * Math.PI * pitch / Rate;
                    double envelope = Math.Min(1, t / 0.0015) * Math.Exp(-t / 0.004);
                    double wave = Math.Sin(phase) + 0.3 * Math.Sin(phase * 2.01);
                    smooth += (wave - smooth) * 0.45;
                    int mixed = samples[start + j] + (int)(smooth * envelope / 1.3 * loud * 32767);
                    samples[start + j] = (short)Math.Max(short.MinValue, Math.Min(short.MaxValue, mixed));
                }
            }
            return samples;
        }

        internal static short[] Make(Cue cue, int level)
        {
            switch (cue)
            {
                case Cue.Arrive: return Sweep(0.42, 330, 990, 0.03, level);
                case Cue.Leave: return Sweep(0.36, 880, 250, 0.012, level);
                case Cue.Hop: return Sweep(0.16, 520, 1240, 0.008, level);
                default: return Sweep(0.05, 1600, 1350, 0.003, level * 3 / 4);
            }
        }

        private static short[] Sweep(double seconds, double from, double to, double attack, int level)
        {
            int count = (int)(seconds * Rate);
            short[] samples = new short[count];
            double phase = 0, fifth = 0, smooth = 0;
            double peak = 0.26 * Math.Max(0, Math.Min(100, level)) / 100.0;
            for (int i = 0; i < count; i++)
            {
                double t = (double)i / Rate;
                double along = (double)i / count;
                double pitch = from * Math.Pow(to / from, 1 - Math.Pow(1 - along, 3));
                phase += 2 * Math.PI * pitch / Rate;
                fifth += 3 * Math.PI * pitch / Rate;
                double envelope = Math.Min(1, t / attack) * Math.Pow(1 - along, 2.2);
                double wave = Math.Sin(phase) + 0.28 * Math.Sin(fifth) + 0.12 * Math.Sin(2 * phase);
                smooth += (wave - smooth) * 0.5;
                samples[i] = (short)(Math.Max(-1, Math.Min(1, smooth * envelope / 1.4)) * peak * 32767);
            }
            return samples;
        }
    }

#if !CROSS
    internal sealed class FreeStyle
    {
        internal readonly string Name;
        internal readonly int Speaker;
        internal readonly float Speed;
        internal readonly bool British;

        internal FreeStyle(string name, int speaker, float speed, bool british)
        {
            Name = name;
            Speaker = speaker;
            Speed = speed;
            British = british;
        }
    }

    internal sealed class LocalVoice : IDisposable
    {
        internal const string Pack = VoicePack.Name;
        internal static string PackUrl = VoicePack.Url;
        internal static string PackSha = VoicePack.Sha;
        internal static long PackSize = VoicePack.Size;
        internal static readonly FreeStyle[] Styles =
        {
            new FreeStyle("Her own", 53, 1.1f, false),
            new FreeStyle("Bright", 2, 1.1f, false),
            new FreeStyle("Sly", 6, 1.08f, false),
            new FreeStyle("Sharp", 21, 1.1f, true)
        };
        internal static readonly string[] Needed = VoicePack.Needed;
        private static readonly object loading = new object();
        private static bool loaded;
        private readonly string folder;
        private readonly object gate = new object();
        private IntPtr tts;
        private bool british;
        internal volatile FreeStyle Style = Styles[0];
        internal volatile bool Cancel;

        internal LocalVoice(string data) { folder = Folder(data); }

        internal static string Folder(string data) { return VoicePack.Folder(data); }

        internal static FreeStyle Find(string name)
        {
            foreach (FreeStyle style in Styles) if (style.Name == name) return style;
            return Styles[0];
        }

        internal bool Installed { get { return Complete(folder); } }

        private static bool Complete(string where) { return VoicePack.Complete(where); }

        internal void Install(Action<string, long, long> progress)
        {
            if (IntPtr.Size != 8) throw new IOException("Her free voice needs 64-bit Windows.");
            string root = Path.GetDirectoryName(folder);
            Directory.CreateDirectory(root);
            long needed = PackSize * 3 + (64L << 20);
            string drive = Path.GetPathRoot(root);
            if (new DriveInfo(drive).AvailableFreeSpace < needed) throw new IOException("Her free voice needs " + Engine.Amount(needed) + " free on " + drive.TrimEnd('\\') + ".");
            string zip = folder + ".zip";
            Engine.Fetch(PackUrl, zip, PackSize, PackSha, "her free voice", progress, delegate { return Cancel; });
            progress("Unpacking her free voice", PackSize, PackSize);
            string staging = folder + ".new";
            if (Directory.Exists(staging)) Directory.Delete(staging, true);
            ZipFile.ExtractToDirectory(zip, staging);
            if (!Complete(staging)) throw new IOException("The voice download is missing files.");
            lock (gate)
            {
                Close();
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
                Directory.Move(staging, folder);
            }
            File.Delete(zip);
        }

        internal void Warm()
        {
            FreeStyle style = Style;
            lock (gate) Open(style.British);
        }

        internal byte[] Speak(string text)
        {
            FreeStyle style = Style;
            lock (gate)
            {
                Open(style.British);
                IntPtr words = Utf8(text);
                IntPtr audio = IntPtr.Zero;
                try
                {
                    audio = SherpaOnnxOfflineTtsGenerate(tts, words, style.Speaker, style.Speed);
                    if (audio == IntPtr.Zero) throw new VoiceException("Her free voice glitched.");
                    Generated made = (Generated)Marshal.PtrToStructure(audio, typeof(Generated));
                    float[] samples = new float[Math.Max(0, made.Count)];
                    if (samples.Length > 0) Marshal.Copy(made.Samples, samples, 0, samples.Length);
                    short[] pcm = new short[samples.Length];
                    for (int i = 0; i < samples.Length; i++) pcm[i] = (short)Math.Max(-32767, Math.Min(32767, Math.Round(samples[i] * 32767)));
                    return Voice.Wave(pcm, made.Rate);
                }
                finally
                {
                    if (audio != IntPtr.Zero) SherpaOnnxDestroyOfflineTtsGeneratedAudio(audio);
                    Marshal.FreeHGlobal(words);
                }
            }
        }

        private void Open(bool gb)
        {
            if (tts != IntPtr.Zero && british == gb) return;
            if (IntPtr.Size != 8) throw new VoiceException("Her free voice needs 64-bit Windows.");
            if (!Installed) throw new VoiceException("Set up her free voice in Capri Mainframe.");
            Load(folder);
            Close();
            List<IntPtr> held = new List<IntPtr>();
            Func<string, IntPtr> keep = delegate(string value) { IntPtr pointer = Utf8(value); held.Add(pointer); return pointer; };
            try
            {
                Config config = new Config();
                config.Model.Threads = Math.Max(1, Math.Min(8, Environment.ProcessorCount / 2));  // up to 8 threads for her free voice
                config.Model.Provider = keep("cpu");
                config.Model.Kokoro.Model = keep(Path.Combine(folder, "model.onnx"));
                config.Model.Kokoro.Voices = keep(Path.Combine(folder, "voices.bin"));
                config.Model.Kokoro.Tokens = keep(Path.Combine(folder, "tokens.txt"));
                config.Model.Kokoro.Data = keep(Path.Combine(folder, "espeak-ng-data"));
                config.Model.Kokoro.Lexicon = keep(Path.Combine(folder, gb ? "lexicon-gb-en.txt" : "lexicon-us-en.txt"));
                config.Model.Kokoro.Lang = keep(gb ? "en" : "en-us");
                config.MaxSentences = 1;
                tts = SherpaOnnxCreateOfflineTts(ref config);
            }
            finally { foreach (IntPtr pointer in held) Marshal.FreeHGlobal(pointer); }
            if (tts == IntPtr.Zero) throw new VoiceException("Her free voice wouldn't start.");
            british = gb;
        }

        // Her ears run on the same sherpa-onnx, so they load it from here too.
        internal static void Load(string where)
        {
            lock (loading)
            {
                if (loaded) return;
                foreach (string name in new[] { "onnxruntime.dll", "sherpa-onnx-c-api.dll" })
                    if (LoadLibraryEx(Path.Combine(where, name), IntPtr.Zero, 8) == IntPtr.Zero) throw new VoiceException("Her free voice wouldn't load (error " + Marshal.GetLastWin32Error() + ").");
                loaded = true;
            }
        }

        private void Close()
        {
            if (tts == IntPtr.Zero) return;
            SherpaOnnxDestroyOfflineTts(tts);
            tts = IntPtr.Zero;
        }

        public void Dispose() { lock (gate) Close(); }

        private static IntPtr Utf8(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? "");
            IntPtr pointer = Marshal.AllocHGlobal(bytes.Length + 1);
            Marshal.Copy(bytes, 0, pointer, bytes.Length);
            Marshal.WriteByte(pointer, bytes.Length, 0);
            return pointer;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr LoadLibraryEx(string path, IntPtr file, int flags);
        [DllImport("sherpa-onnx-c-api.dll", CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr SherpaOnnxCreateOfflineTts(ref Config config);
        [DllImport("sherpa-onnx-c-api.dll", CallingConvention = CallingConvention.Cdecl)] private static extern void SherpaOnnxDestroyOfflineTts(IntPtr tts);
        [DllImport("sherpa-onnx-c-api.dll", CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr SherpaOnnxOfflineTtsGenerate(IntPtr tts, IntPtr text, int speaker, float speed);
        [DllImport("sherpa-onnx-c-api.dll", CallingConvention = CallingConvention.Cdecl)] private static extern void SherpaOnnxDestroyOfflineTtsGeneratedAudio(IntPtr audio);

        [StructLayout(LayoutKind.Sequential)]
        private struct Vits { internal IntPtr Model, Lexicon, Tokens, Data; internal float Noise, NoiseW, Length; internal IntPtr Dict; }

        [StructLayout(LayoutKind.Sequential)]
        private struct Matcha { internal IntPtr Acoustic, Vocoder, Lexicon, Tokens, Data; internal float Noise, Length; internal IntPtr Dict; }

        [StructLayout(LayoutKind.Sequential)]
        private struct Kokoro { internal IntPtr Model, Voices, Tokens, Data; internal float Length; internal IntPtr Dict, Lexicon, Lang; }

        [StructLayout(LayoutKind.Sequential)]
        private struct Kitten { internal IntPtr Model, Voices, Tokens, Data; internal float Length; }

        [StructLayout(LayoutKind.Sequential)]
        private struct Zipvoice { internal IntPtr Tokens, Encoder, Decoder, Vocoder, Data, Lexicon; internal float Feat, Shift, Rms, Guidance; }

        [StructLayout(LayoutKind.Sequential)]
        private struct Pocket { internal IntPtr Flow, Main, Encoder, Decoder, Conditioner, Vocab, Scores; internal int Cache; }

        [StructLayout(LayoutKind.Sequential)]
        private struct Supertonic { internal IntPtr Duration, Encoder, Estimator, Vocoder, Json, Indexer, Style; }

        [StructLayout(LayoutKind.Sequential)]
        private struct ModelConfig
        {
            internal Vits Vits;
            internal int Threads, Debug;
            internal IntPtr Provider;
            internal Matcha Matcha;
            internal Kokoro Kokoro;
            internal Kitten Kitten;
            internal Zipvoice Zipvoice;
            internal Pocket Pocket;
            internal Supertonic Supertonic;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Config
        {
            internal ModelConfig Model;
            internal IntPtr Rules;
            internal int MaxSentences;
            internal IntPtr Fars;
            internal float Silence;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Generated { internal IntPtr Samples; internal int Count, Rate; }
    }
#endif
}
