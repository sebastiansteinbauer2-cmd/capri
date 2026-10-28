using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Speech.AudioFormat;
using System.Speech.Recognition;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Win32;

namespace CapriDesktop
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct WaveFormat
    {
        internal short Tag, Channels;
        internal int SampleRate, BytesPerSecond;
        internal short Align, Bits, Extra;

        internal static WaveFormat Mono(int rate) { return new WaveFormat { Tag = 1, Channels = 1, SampleRate = rate, BytesPerSecond = rate * 2, Align = 2, Bits = 16 }; }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct WaveHeader
    {
        internal IntPtr Data;
        internal int Length, Recorded;
        internal IntPtr User;
        internal int Flags, Loops;
        internal IntPtr Next, Reserved;
    }

    internal sealed class Mic
    {
        internal readonly string Id;
        internal readonly string Name;

        internal Mic(string id, string name)
        {
            Id = id;
            Name = name;
        }
    }

    internal sealed class Ears : IDisposable
    {
        internal const int Rate = 16000;
        internal const int Chunk = Rate / 10;
        private const int Buffers = 4;
        internal event Action<short[]> Heard;
        internal event Action<double> Level;
        internal volatile bool Muted;
        internal volatile string Chosen;
        private IntPtr device;
        private IntPtr[] headers;
        private Thread worker;
        private AutoResetEvent signal;
        private volatile bool running;

        [DllImport("winmm.dll")] private static extern int waveInOpen(out IntPtr device, int id, ref WaveFormat format, IntPtr callback, IntPtr instance, int flags);
        [DllImport("winmm.dll")] private static extern int waveInPrepareHeader(IntPtr device, IntPtr header, int size);
        [DllImport("winmm.dll")] private static extern int waveInUnprepareHeader(IntPtr device, IntPtr header, int size);
        [DllImport("winmm.dll")] private static extern int waveInAddBuffer(IntPtr device, IntPtr header, int size);
        [DllImport("winmm.dll")] private static extern int waveInStart(IntPtr device);
        [DllImport("winmm.dll")] private static extern int waveInReset(IntPtr device);
        [DllImport("winmm.dll")] private static extern int waveInClose(IntPtr device);
        [DllImport("winmm.dll")] private static extern int waveInGetNumDevs();
        [DllImport("winmm.dll", CharSet = CharSet.Unicode)] private static extern int waveInGetDevCaps(IntPtr id, ref MicCaps caps, int size);
        [DllImport("winmm.dll")] private static extern int waveInMessage(IntPtr device, int message, out int size, IntPtr unused);
        [DllImport("winmm.dll", CharSet = CharSet.Unicode)] private static extern int waveInMessage(IntPtr device, int message, StringBuilder text, IntPtr size);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct MicCaps
        {
            internal short Maker, Product;
            internal int Driver;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] internal string Name;
            internal int Formats;
            internal short Channels, Reserved;
        }

        internal bool Listening { get { return running; } }

        internal static List<Mic> List()
        {
            List<Mic> found = new List<Mic>();
            int count = waveInGetNumDevs();
            for (int i = 0; i < count; i++)
            {
                MicCaps caps = new MicCaps();
                if (waveInGetDevCaps(new IntPtr(i), ref caps, Marshal.SizeOf(typeof(MicCaps))) != 0) continue;
                string id = Endpoint(i);
                found.Add(new Mic(id ?? caps.Name, FullName(id) ?? caps.Name));
            }
            return found;
        }

        private static string Endpoint(int index)
        {
            int size;
            if (waveInMessage(new IntPtr(index), 0x0812, out size, IntPtr.Zero) != 0 || size <= 0) return null;
            StringBuilder text = new StringBuilder(size / 2 + 1);
            if (waveInMessage(new IntPtr(index), 0x0811, text, new IntPtr(size)) != 0) return null;
            return text.Length == 0 ? null : text.ToString();
        }

        private static string FullName(string id)
        {
            if (id == null) return null;
            int cut = id.LastIndexOf("}.{", StringComparison.Ordinal);
            if (cut < 0) return null;
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Capture\" + id.Substring(cut + 2) + @"\Properties"))
                {
                    if (key == null) return null;
                    string kind = key.GetValue("{a45c254e-df1c-4efd-8020-67d146a850e0},2") as string;
                    string name = key.GetValue("{b3f8fa53-0004-438e-9003-51a46e139bfc},6") as string;
                    if (string.IsNullOrEmpty(kind)) return string.IsNullOrEmpty(name) ? null : name;
                    return string.IsNullOrEmpty(name) ? kind : kind == "Microphone" ? name : kind + " (" + name + ")";
                }
            }
            catch (Exception) { return null; }
        }

        private static int Pick(string chosen)
        {
            if (string.IsNullOrEmpty(chosen)) return -1;
            List<Mic> mics = List();
            for (int i = 0; i < mics.Count; i++) if (mics[i].Id == chosen) return i;
            return -1;
        }

        internal void Start()
        {
            if (running) return;
            WaveFormat format = WaveFormat.Mono(Rate);
            signal = new AutoResetEvent(false);
            int size = Marshal.SizeOf(typeof(WaveHeader));
            int id = Pick(Chosen);
            IntPtr wake = signal.SafeWaitHandle.DangerousGetHandle();
            if (waveInOpen(out device, id, ref format, wake, IntPtr.Zero, 0x00050000) != 0 && (id == -1 || waveInOpen(out device, -1, ref format, wake, IntPtr.Zero, 0x00050000) != 0))
            {
                signal.Dispose();
                signal = null;
                throw new VoiceException("Can't open your mic. Pick another under VOICE.");
            }
            headers = new IntPtr[Buffers];
            for (int i = 0; i < Buffers; i++)
            {
                WaveHeader header = new WaveHeader { Data = Marshal.AllocHGlobal(Chunk * 2), Length = Chunk * 2 };
                headers[i] = Marshal.AllocHGlobal(size);
                Marshal.StructureToPtr(header, headers[i], false);
                waveInPrepareHeader(device, headers[i], size);
                waveInAddBuffer(device, headers[i], size);
            }
            running = true;
            waveInStart(device);
            worker = new Thread(Pump);
            worker.IsBackground = true;
            worker.Name = "Capri ears";
            worker.Start();
        }

        internal void Stop()
        {
            if (!running) return;
            running = false;
            signal.Set();
            worker.Join(1000);
            waveInReset(device);
            int size = Marshal.SizeOf(typeof(WaveHeader));
            foreach (IntPtr pointer in headers)
            {
                waveInUnprepareHeader(device, pointer, size);
                WaveHeader header = (WaveHeader)Marshal.PtrToStructure(pointer, typeof(WaveHeader));
                Marshal.FreeHGlobal(header.Data);
                Marshal.FreeHGlobal(pointer);
            }
            waveInClose(device);
            device = IntPtr.Zero;
            headers = null;
            signal.Dispose();
            signal = null;
        }

        public void Dispose() { Stop(); }

        private void Pump()
        {
            int size = Marshal.SizeOf(typeof(WaveHeader));
            int flags = (int)Marshal.OffsetOf(typeof(WaveHeader), "Flags");
            int next = 0;
            try
            {
                while (running)
                {
                    signal.WaitOne(250);
                    while (running)
                    {
                        IntPtr pointer = headers[next];
                        WaveHeader header = (WaveHeader)Marshal.PtrToStructure(pointer, typeof(WaveHeader));
                        if ((header.Flags & 1) == 0) break;
                        short[] chunk = new short[header.Recorded / 2];
                        Marshal.Copy(header.Data, chunk, 0, chunk.Length);
                        Marshal.WriteInt32(pointer, flags, header.Flags & ~1);
                        waveInAddBuffer(device, pointer, size);
                        next = (next + 1) % Buffers;
                        try { Feed(chunk); }
                        catch (Exception) { }
                    }
                }
            }
            catch (Exception) { }
        }

        internal void Feed(short[] chunk)
        {
            Action<double> level = Level;
            if (level != null) level(Muted ? 0 : Speaker.Loudness(chunk, 0, chunk.Length));
            Action<short[]> heard = Heard;
            if (heard != null) heard(Muted ? new short[chunk.Length] : chunk);
        }
    }

    internal sealed class Speaker : IDisposable
    {
        private readonly bool silent;
        internal bool Plain;
        private readonly List<IntPtr> pending = new List<IntPtr>();
        private readonly List<int> shapes = new List<int>();
        private readonly List<short> leftover = new List<short>();
        private IntPtr device;
        private int rate;
        private double loudest = 0.05;
        private long began;
        private long until;

        [DllImport("winmm.dll")] private static extern int waveOutOpen(out IntPtr device, int id, ref WaveFormat format, IntPtr callback, IntPtr instance, int flags);
        [DllImport("winmm.dll")] private static extern int waveOutPrepareHeader(IntPtr device, IntPtr header, int size);
        [DllImport("winmm.dll")] private static extern int waveOutUnprepareHeader(IntPtr device, IntPtr header, int size);
        [DllImport("winmm.dll")] private static extern int waveOutWrite(IntPtr device, IntPtr header, int size);
        [DllImport("winmm.dll")] private static extern int waveOutReset(IntPtr device);
        [DllImport("winmm.dll")] private static extern int waveOutClose(IntPtr device);

        internal Speaker(bool quiet) { silent = quiet; }

        internal static double Loudness(short[] samples, int start, int count)
        {
            if (count <= 0) return 0;
            double sum = 0;
            for (int i = start; i < start + count; i++) sum += (double)samples[i] * samples[i];
            return Math.Sqrt(sum / count) / 32768.0;
        }

        internal void Open(int sampleRate)
        {
            if (sampleRate == rate && (silent || device != IntPtr.Zero)) return;
            Close();
            rate = sampleRate;
            if (silent) return;
            WaveFormat format = WaveFormat.Mono(rate);
            int failed = waveOutOpen(out device, -1, ref format, IntPtr.Zero, IntPtr.Zero, 0);
            if (failed != 0)
            {
                device = IntPtr.Zero;
                throw new VoiceException(Speakers(failed));
            }
        }

        internal static string Speakers(int failed)
        {
            if (failed == 2 || failed == 6) return "Can't open your speakers: Windows has no speakers or headphones on. Pick one in Settings > System > Sound.";
            if (failed == 4) return "Can't open your speakers: another app has them to itself. Close it, or untick exclusive mode in your speaker's Sound settings.";
            return "Can't open your speakers (Windows error " + failed + "). Check Settings > System > Sound, then try again.";
        }

        internal bool Busy { get { return Stopwatch.GetTimestamp() < Interlocked.Read(ref until) + Stopwatch.Frequency * 35 / 100; } }

        internal void Play(short[] samples)
        {
            if (rate == 0 || samples.Length == 0) return;
            Sweep();
            long now = Stopwatch.GetTimestamp();
            if (now >= Interlocked.Read(ref until)) { began = now; shapes.Clear(); leftover.Clear(); Interlocked.Exchange(ref until, now); }
            Interlocked.Add(ref until, samples.Length * Stopwatch.Frequency / rate);
            leftover.AddRange(samples);
            int step = Math.Max(1, rate / 12);
            short[] all = leftover.ToArray();
            int at = 0;
            for (; at + step <= all.Length; at += step)
            {
                double level = Loudness(all, at, step);
                loudest = Math.Max(loudest * 0.995, level);
                int index = shapes.Count;
                shapes.Add(level <= Math.Max(0.004, loudest * 0.08) ? -1 : level < loudest * 0.3 ? (index % 3 == 0 ? 0 : 1) : level < loudest * 0.55 ? (index % 2 == 1 ? 2 : 1) : level < loudest * 0.85 ? 3 : 4);
            }
            leftover.RemoveRange(0, at);
            if (silent || device == IntPtr.Zero) return;
            if (!Plain) samples = Voice.Louder(samples);
            int size = Marshal.SizeOf(typeof(WaveHeader));
            WaveHeader header = new WaveHeader { Data = Marshal.AllocHGlobal(samples.Length * 2), Length = samples.Length * 2 };
            Marshal.Copy(samples, 0, header.Data, samples.Length);
            IntPtr pointer = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(header, pointer, false);
            waveOutPrepareHeader(device, pointer, size);
            waveOutWrite(device, pointer, size);
            pending.Add(pointer);
        }

        internal int Shape()
        {
            long now = Stopwatch.GetTimestamp();
            if (now >= Interlocked.Read(ref until) || shapes.Count == 0) return -1;
            int index = (int)((now - began) * 12 / Stopwatch.Frequency);
            return index >= 0 && index < shapes.Count ? shapes[index] : -1;
        }

        internal void Clear()
        {
            if (device != IntPtr.Zero) waveOutReset(device);
            Sweep();
            shapes.Clear();
            leftover.Clear();
            Interlocked.Exchange(ref until, 0);
        }

        private void Sweep()
        {
            int size = Marshal.SizeOf(typeof(WaveHeader));
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                WaveHeader header = (WaveHeader)Marshal.PtrToStructure(pending[i], typeof(WaveHeader));
                if ((header.Flags & 1) == 0) continue;
                waveOutUnprepareHeader(device, pending[i], size);
                Marshal.FreeHGlobal(header.Data);
                Marshal.FreeHGlobal(pending[i]);
                pending.RemoveAt(i);
            }
        }

        internal void Close()
        {
            Clear();
            if (device != IntPtr.Zero) waveOutClose(device);
            device = IntPtr.Zero;
            rate = 0;
        }

        public void Dispose() { Close(); }
    }

    internal static class Hearing
    {
        internal const string NoRecognizer = "Windows has no offline speech recognizer for your language. Turn on Online speech recognition in Settings > Privacy & security > Speech.";
        internal static Func<short[], string> Run = Recognize;
        // Her own Parakeet ears while they suit her language; Windows' offline recognizer otherwise.
        internal static volatile LocalEars Own;
        private static readonly object gate = new object();
        private static SpeechRecognitionEngine engine;

        internal static string Text(short[] samples) { return Clean(Run(samples)); }

        internal static string Clean(string text)
        {
            string plain = Regex.Replace(text ?? "", @"\s+", " ").Trim();
            return Regex.IsMatch(plain, @"[\p{L}\p{N}]") ? plain : "";
        }

        internal static string Pick(IList<string> cultures, string wanted)
        {
            string language = wanted.Split('-')[0];
            foreach (string culture in cultures) if (string.Equals(culture, wanted, StringComparison.OrdinalIgnoreCase)) return culture;
            foreach (string culture in cultures) if (string.Equals(culture.Split('-')[0], language, StringComparison.OrdinalIgnoreCase)) return culture;
            foreach (string culture in cultures) if (string.Equals(culture, "en-US", StringComparison.OrdinalIgnoreCase)) return culture;
            return cultures.Count > 0 ? cultures[0] : null;
        }

        private static SpeechRecognitionEngine Open()
        {
            List<RecognizerInfo> installed = new List<RecognizerInfo>(SpeechRecognitionEngine.InstalledRecognizers());
            List<string> cultures = installed.ConvertAll(delegate(RecognizerInfo info) { return info.Culture.Name; });
            string chosen = Pick(cultures, CultureInfo.CurrentUICulture.Name);
            if (chosen == null) throw new IOException(NoRecognizer);
            SpeechRecognitionEngine made = new SpeechRecognitionEngine(installed[cultures.IndexOf(chosen)]);
            made.LoadGrammar(new DictationGrammar());
            return made;
        }

        private static string Recognize(short[] samples)
        {
            LocalEars own = Own;
            if (own != null && own.Ready) return own.Hear(samples);
            lock (gate)
            {
                if (engine == null) engine = Open();
                byte[] bytes = new byte[samples.Length * 2];
                Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
                StringBuilder words = new StringBuilder();
                ManualResetEvent done = new ManualResetEvent(false);
                EventHandler<SpeechRecognizedEventArgs> recognized = delegate(object sender, SpeechRecognizedEventArgs e) { lock (words) words.Append(e.Result.Text).Append(' '); };
                EventHandler<RecognizeCompletedEventArgs> completed = delegate { done.Set(); };
                engine.SpeechRecognized += recognized;
                engine.RecognizeCompleted += completed;
                try
                {
                    using (MemoryStream stream = new MemoryStream(bytes))
                    {
                        engine.SetInputToAudioStream(stream, new SpeechAudioFormatInfo(Ears.Rate, AudioBitsPerSample.Sixteen, AudioChannel.Mono));
                        engine.RecognizeAsync(RecognizeMode.Multiple);
                        if (!done.WaitOne(20000))
                        {
                            engine.RecognizeAsyncCancel();
                            done.WaitOne(3000);
                            throw new IOException("Hearing that took too long. Try a shorter line.");
                        }
                    }
                }
                finally
                {
                    engine.SpeechRecognized -= recognized;
                    engine.RecognizeCompleted -= completed;
                    engine.SetInputToNull();
                }
                lock (words) return words.ToString();
            }
        }
    }

    // Windows 10's tar has no bzip2 ("Can't initialize filter"), so her ears unpack that layer here. Each block's CRC is checked.
    internal static class Bunzip
    {
        private static readonly uint[] Crcs = MakeCrcs();

        private static uint[] MakeCrcs()
        {
            uint[] table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint c = i << 24;
                for (int k = 0; k < 8; k++) c = (c & 0x80000000u) != 0 ? (c << 1) ^ 0x04c11db7u : c << 1;
                table[i] = c;
            }
            return table;
        }

        private static IOException Damaged() { return new IOException("The ears download is damaged."); }

        private sealed class Bits
        {
            private readonly Stream from;
            private readonly byte[] buffer = new byte[1 << 16];
            private int at, end;
            private ulong held;
            private int count;

            internal Bits(Stream from) { this.from = from; }

            private int Next()
            {
                if (at == end)
                {
                    at = 0;
                    end = from.Read(buffer, 0, buffer.Length);
                    if (end <= 0) { end = 0; return -1; }
                }
                return buffer[at++];
            }

            internal uint Read(int n)
            {
                while (count < n)
                {
                    int b = Next();
                    if (b < 0) throw new IOException("The ears download ended early.");
                    held = (held << 8) | (uint)b;
                    count += 8;
                }
                count -= n;
                return (uint)(held >> count) & ((1u << n) - 1);
            }

            internal uint Bit() { return Read(1); }

            // A stream ends padded to a whole byte; this is the byte after it, or -1 at the end.
            internal int NextByte() { count = 0; return Next(); }
        }

        internal static void Copy(Stream from, Stream to)
        {
            Bits bits = new Bits(from);
            byte[] output = new byte[1 << 16];
            for (int streams = 0; ; streams++)
            {
                int b = bits.NextByte();
                if (b != 'B') { if (streams > 0) return; throw Damaged(); }
                if (bits.Read(8) != 'Z' || bits.Read(8) != 'h') throw Damaged();
                int level = (int)bits.Read(8) - '0';
                if (level < 1 || level > 9) throw Damaged();
                Unpack(bits, level * 100000, output, to);
            }
        }

        private static void Unpack(Bits bits, int most, byte[] output, Stream to)
        {
            int[] tt = new int[most];
            uint combined = 0;
            while (true)
            {
                uint high = bits.Read(24), low = bits.Read(24);
                if (high == 0x177245 && low == 0x385090)
                {
                    if (((bits.Read(16) << 16) | bits.Read(16)) != combined) throw Damaged();
                    return;
                }
                if (high != 0x314159 || low != 0x265359) throw Damaged();
                uint blockCrc = (bits.Read(16) << 16) | bits.Read(16);
                if (bits.Bit() != 0) throw new IOException("The ears download uses old bzip2 randomizing.");
                int origin = (int)bits.Read(24);

                byte[] used = new byte[256];
                int inUse = 0;
                uint rows = bits.Read(16);
                for (int i = 0; i < 16; i++)
                {
                    if ((rows & (0x8000u >> i)) == 0) continue;
                    uint row = bits.Read(16);
                    for (int j = 0; j < 16; j++) if ((row & (0x8000u >> j)) != 0) used[inUse++] = (byte)(i * 16 + j);
                }
                if (inUse == 0) throw Damaged();
                int alphabet = inUse + 2;
                int tables = (int)bits.Read(3);
                if (tables < 2 || tables > 6) throw Damaged();
                int selectorCount = (int)bits.Read(15);
                if (selectorCount < 1) throw Damaged();
                byte[] order = { 0, 1, 2, 3, 4, 5 };
                byte[] selectors = new byte[selectorCount];
                for (int i = 0; i < selectorCount; i++)
                {
                    int j = 0;
                    while (bits.Bit() != 0) if (++j >= tables) throw Damaged();
                    byte picked = order[j];
                    for (; j > 0; j--) order[j] = order[j - 1];
                    order[0] = picked;
                    selectors[i] = picked;
                }

                // Canonical Huffman tables, the way bzip2 builds them.
                int[][] limits = new int[tables][], bases = new int[tables][], perms = new int[tables][];
                int[] shortest = new int[tables];
                for (int t = 0; t < tables; t++)
                {
                    int[] lengths = new int[alphabet];
                    int length = (int)bits.Read(5);
                    for (int s = 0; s < alphabet; s++)
                    {
                        while (true)
                        {
                            if (length < 1 || length > 20) throw Damaged();
                            if (bits.Bit() == 0) break;
                            length += bits.Bit() == 0 ? 1 : -1;
                        }
                        lengths[s] = length;
                    }
                    int min = 32, max = 0;
                    foreach (int l in lengths) { if (l < min) min = l; if (l > max) max = l; }
                    int[] perm = new int[alphabet], start = new int[23], limit = new int[23];
                    int p = 0;
                    for (int l = min; l <= max; l++) for (int s = 0; s < alphabet; s++) if (lengths[s] == l) perm[p++] = s;
                    foreach (int l in lengths) start[l + 1]++;
                    for (int i = 1; i < 23; i++) start[i] += start[i - 1];
                    int vec = 0;
                    for (int l = min; l <= max; l++)
                    {
                        vec += start[l + 1] - start[l];
                        limit[l] = vec - 1;
                        vec <<= 1;
                    }
                    for (int l = min + 1; l <= max; l++) start[l] = ((limit[l - 1] + 1) << 1) - start[l];
                    limits[t] = limit;
                    bases[t] = start;
                    perms[t] = perm;
                    shortest[t] = min;
                }

                // Huffman, then runs of the front symbol (RUNA/RUNB) and move-to-front, into tt.
                byte[] front = new byte[256];
                for (int i = 0; i < 256; i++) front[i] = (byte)i;
                int[] counts = new int[256];
                int end = inUse + 1, n = 0, group = -1, left = 0, run = 0, weight = 1, gMin = 0;
                int[] gLimit = null, gBase = null, gPerm = null;
                while (true)
                {
                    if (left == 0)
                    {
                        if (++group >= selectorCount) throw Damaged();
                        left = 50;
                        int pick = selectors[group];
                        gLimit = limits[pick];
                        gBase = bases[pick];
                        gPerm = perms[pick];
                        gMin = shortest[pick];
                    }
                    left--;
                    int zn = gMin;
                    int zvec = (int)bits.Read(zn);
                    while (zvec > gLimit[zn])
                    {
                        if (++zn > 20) throw Damaged();
                        zvec = (zvec << 1) | (int)bits.Bit();
                    }
                    int at = zvec - gBase[zn];
                    if (at < 0 || at >= alphabet) throw Damaged();
                    int symbol = gPerm[at];
                    if (symbol <= 1)
                    {
                        if (weight >= 1 << 21) throw Damaged();
                        run += (symbol + 1) * weight;
                        weight <<= 1;
                        continue;
                    }
                    if (run > 0)
                    {
                        if (run > most - n) throw Damaged();
                        byte value = used[front[0]];
                        counts[value] += run;
                        for (; run > 0; run--) tt[n++] = value;
                    }
                    weight = 1;
                    if (symbol == end) break;
                    if (n >= most) throw Damaged();
                    int index = symbol - 1;
                    byte moved = front[index];
                    Array.Copy(front, 0, front, 1, index);
                    front[0] = moved;
                    counts[used[moved]]++;
                    tt[n++] = used[moved];
                }
                if (origin >= n) throw Damaged();

                // Undo the Burrows-Wheeler sort, then bzip2's first run-length step (4 equal bytes, then a count).
                int[] next = new int[257];
                for (int i = 0; i < 256; i++) next[i + 1] = next[i] + counts[i];
                for (int i = 0; i < n; i++) tt[next[tt[i] & 0xff]++] |= i << 8;
                int pos = tt[origin] >> 8, last = -1, same = 0, filled = 0;
                uint crc = 0xffffffffu;
                for (int i = 0; i < n; i++)
                {
                    if ((uint)pos >= (uint)n) throw Damaged();
                    int entry = tt[pos];
                    int value = entry & 0xff;
                    pos = entry >> 8;
                    int repeat = 1;
                    if (same == 4) { repeat = value; value = last; same = 0; }
                    else if (value == last) same++;
                    else { last = value; same = 1; }
                    for (; repeat > 0; repeat--)
                    {
                        crc = (crc << 8) ^ Crcs[(crc >> 24) ^ (uint)value];
                        output[filled++] = (byte)value;
                        if (filled == output.Length) { to.Write(output, 0, filled); filled = 0; }
                    }
                }
                if (filled > 0) to.Write(output, 0, filled);
                if (~crc != blockCrc) throw Damaged();
                combined = ((combined << 1) | (combined >> 31)) ^ blockCrc;
            }
        }
    }

    // Her own ears: NVIDIA's Parakeet TDT 0.6B (CC-BY-4.0), run by the sherpa-onnx her free voice ships. v2 hears English;
    // v3 hears 25 European languages and works out which one it's hearing. They hear whichever mic is picked, offline,
    // without Windows' online speech setting.
    // Words her ears keep getting wrong, fixed before she reads them: heard-fixes.txt, one "heard = meant" per line.
    // They can add their friends' names there, like "mark = mar123ek45". It's reread whenever they save it.
    internal static class HeardFixes
    {
        internal static string Path;
        private static readonly object gate = new object();
        private static DateTime loadedAt = DateTime.MinValue;
        private static List<KeyValuePair<Regex, string>> fixes = new List<KeyValuePair<Regex, string>>();

        internal const string Starter =
            "# Words Capri's ears get wrong, fixed before she reads them. One per line: heard = meant\n" +
            "# Add your friends' names the way she mishears them, like: mark = mar123ek45\n" +
            "# Lines starting with # are notes. Save the file and she uses it straight away.\n\n" +
            "capre = Capri\ncapry = Capri\nkapri = Capri\nkapre = Capri\ncaprie = Capri\n" +
            "the disc = Discord\ndisc = Discord\ndiscord's = Discord's\ndis cord = Discord\n" +
            "spot if i = Spotify\nspot a fly = Spotify\n";

        internal static string Apply(string heard)
        {
            if (string.IsNullOrEmpty(heard) || Path == null) return heard;
            List<KeyValuePair<Regex, string>> current;
            lock (gate)
            {
                try
                {
                    if (!File.Exists(Path)) File.WriteAllText(Path, Starter);
                    DateTime saved = File.GetLastWriteTimeUtc(Path);
                    if (saved != loadedAt)
                    {
                        List<KeyValuePair<Regex, string>> read = new List<KeyValuePair<Regex, string>>();
                        foreach (string line in File.ReadAllLines(Path))
                        {
                            int split = line.IndexOf('=');
                            if (line.TrimStart().StartsWith("#", StringComparison.Ordinal) || split <= 0) continue;
                            string wrong = line.Substring(0, split).Trim(), right = line.Substring(split + 1).Trim();
                            if (wrong.Length == 0 || right.Length == 0) continue;
                            read.Add(new KeyValuePair<Regex, string>(new Regex(@"(?<![\p{L}\p{N}])" + Regex.Escape(wrong) + @"(?![\p{L}\p{N}])", RegexOptions.IgnoreCase), right));
                        }
                        // Longer phrases first, so "the disc" wins over "disc".
                        read.Sort(delegate(KeyValuePair<Regex, string> a, KeyValuePair<Regex, string> b) { return b.Key.ToString().Length.CompareTo(a.Key.ToString().Length); });
                        fixes = read;
                        loadedAt = saved;
                    }
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                current = fixes;
            }
            foreach (KeyValuePair<Regex, string> fix in current) heard = fix.Key.Replace(heard, fix.Value.Replace("$", "$$"));
            return heard;
        }
    }

    internal sealed class LocalEars : IDisposable
    {
        internal const string Pack = "parakeet-tdt-0.6b-v2-int8";
        internal static string PackUrl = "https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/sherpa-onnx-nemo-parakeet-tdt-0.6b-v2-int8.tar.bz2";
        internal static string PackSha = "157c157bc51155e03e37d2466522a3a737dd9c72bb25f36eb18912964161e1ad";
        internal static long PackSize = 482468385;
        private const string Inside = "sherpa-onnx-nemo-parakeet-tdt-0.6b-v2-int8";
        internal const string EuropeanPack = "parakeet-tdt-0.6b-v3-int8";
        internal static string EuropeanUrl = "https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8.tar.bz2";
        internal static string EuropeanSha = "5793d0fd397c5778d2cf2126994d58e9d56b1be7c04d13c7a15bb1b4eafb16bf";
        internal static long EuropeanSize = 487170055;
        private const string EuropeanInside = "sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8";
        // What v3 hears, as two-letter language codes.
        internal static readonly string[] European = { "bg", "hr", "cs", "da", "nl", "en", "et", "fi", "fr", "de", "el", "hu", "it", "lv", "lt", "mt", "pl", "pt", "ro", "sk", "sl", "es", "sv", "ru", "uk" };
        internal static readonly string[] Needed = { "encoder.int8.onnx", "decoder.int8.onnx", "joiner.int8.onnx", "tokens.txt" };
        private readonly string root;
        private readonly string engine;
        private readonly object gate = new object();
        private IntPtr recognizer;
        private string opened;
        private volatile string language = "en";
        internal volatile bool Cancel;

        internal LocalEars(string data)
        {
            root = Path.Combine(data, "ears");
            engine = LocalVoice.Folder(data);
        }

        // The language they'll speak: Capri's, unless she's on English and Windows isn't, which makes English just her default.
        internal static string Spoken(string capri, string windows) { return capri != "en" || string.IsNullOrEmpty(windows) ? capri : windows.ToLowerInvariant(); }

        // A change takes effect at the next thing she hears.
        internal string Language { get { return language; } set { language = value ?? "en"; } }

        internal bool Fits { get { return IntPtr.Size == 8 && Array.IndexOf(European, language) >= 0; } }

        // The pack that hears their language: v2 for English, v3 for its other languages, none for the rest, which keep Windows speech.
        internal string Model { get { return !Fits ? null : language == "en" ? Pack : EuropeanPack; } }

        internal long Size { get { return Model == EuropeanPack ? EuropeanSize : PackSize; } }

        private string Folder { get { return Path.Combine(root, Model ?? Pack); } }

        internal bool Installed { get { return Fits && Complete(Folder); } }

        // Ready to hear: the model is in, and so is the free voice that brings sherpa-onnx.
        internal bool Ready { get { return IntPtr.Size == 8 && Installed && File.Exists(Path.Combine(engine, "sherpa-onnx-c-api.dll")) && File.Exists(Path.Combine(engine, "onnxruntime.dll")); } }

        private static bool Complete(string where)
        {
            foreach (string name in Needed) if (!File.Exists(Path.Combine(where, name))) return false;
            return true;
        }

        internal void Install(Action<string, long, long> progress)
        {
            if (IntPtr.Size != 8) throw new IOException("Her ears need 64-bit Windows.");
            if (!Fits) throw new IOException("Her own ears don't know your language yet.");
            bool english = Model == Pack;
            string folder = Folder;
            long size = english ? PackSize : EuropeanSize;
            Directory.CreateDirectory(root);
            long needed = size * 3 + (64L << 20);
            string drive = Path.GetPathRoot(root);
            if (new DriveInfo(drive).AvailableFreeSpace < needed) throw new IOException("Her ears need " + Engine.Amount(needed) + " free on " + drive.TrimEnd('\\') + ".");
            string tarball = folder + ".tar.bz2";
            Engine.Fetch(english ? PackUrl : EuropeanUrl, tarball, size, english ? PackSha : EuropeanSha, "her ears", progress, delegate { return Cancel; });
            progress("Unpacking her ears", size, size);
            string staging = folder + ".new";
            if (Directory.Exists(staging)) Directory.Delete(staging, true);
            Directory.CreateDirectory(staging);
            Untar(tarball, staging);
            string unpacked = Path.Combine(staging, english ? Inside : EuropeanInside);
            if (!Complete(unpacked)) throw new IOException("The ears download is missing files.");
            lock (gate)
            {
                Close();
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
                Directory.Move(unpacked, folder);
            }
            Directory.Delete(staging, true);
            File.Delete(tarball);
        }

        internal static bool SystemBzip = true;

        // Windows 10 and 11 ship bsdtar, but only Windows 11's reads bzip2. When tar can't, she unpacks the bzip2 layer herself and tar gets the plain archive.
        // The checksum was already matched, so this is the known archive.
        internal static void Untar(string tarball, string into)
        {
            string tar = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "tar.exe");
            if (!File.Exists(tar)) throw new IOException("Windows is missing tar.exe, which unpacks her ears.");
            if (SystemBzip && Tar(tar, "-xjf \"" + tarball + "\" -C \"" + into + "\"") == null) return;
            string plain = Path.ChangeExtension(tarball, null);
            try
            {
                using (FileStream packed = new FileStream(tarball, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20))
                using (FileStream opened = new FileStream(plain, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20))
                {
                    try { Bunzip.Copy(packed, opened); }
                    catch (IOException) { throw; }
                    catch (Exception) { throw new IOException("The ears download is damaged."); }
                }
                File.Delete(tarball);
                Directory.Delete(into, true);
                Directory.CreateDirectory(into);
                string problem = Tar(tar, "-xf \"" + plain + "\" -C \"" + into + "\"");
                if (problem != null) throw new IOException("Couldn't unpack her ears: " + Voice.Short(problem));
            }
            finally { if (File.Exists(plain)) File.Delete(plain); }
        }

        private static string Tar(string tar, string arguments)
        {
            ProcessStartInfo start = new ProcessStartInfo(tar, arguments) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
            using (Process unpacking = Process.Start(start))
            {
                string problem = unpacking.StandardError.ReadToEnd();
                unpacking.WaitForExit();
                return unpacking.ExitCode == 0 ? null : problem.Trim();
            }
        }

        internal void Warm() { lock (gate) Open(); }

        internal string Hear(short[] samples)
        {
            lock (gate)
            {
                Open();
                IntPtr stream = SherpaOnnxCreateOfflineStream(recognizer);
                if (stream == IntPtr.Zero) throw new IOException("Her ears glitched.");
                IntPtr result = IntPtr.Zero;
                try
                {
                    float[] wave = new float[samples.Length];
                    for (int i = 0; i < samples.Length; i++) wave[i] = samples[i] / 32768f;
                    SherpaOnnxAcceptWaveformOffline(stream, Ears.Rate, wave, wave.Length);
                    SherpaOnnxDecodeOfflineStream(recognizer, stream);
                    result = SherpaOnnxGetOfflineStreamResult(stream);
                    return result == IntPtr.Zero ? "" : HeardFixes.Apply(Text(Marshal.ReadIntPtr(result)));
                }
                finally
                {
                    if (result != IntPtr.Zero) SherpaOnnxDestroyOfflineRecognizerResult(result);
                    SherpaOnnxDestroyOfflineStream(stream);
                }
            }
        }

        private void Open()
        {
            string folder = Folder;
            if (recognizer != IntPtr.Zero && opened == folder) return;
            Close();
            if (!Ready) throw new IOException("Her ears aren't in yet.");
            LocalVoice.Load(engine);
            List<IntPtr> held = new List<IntPtr>();
            Func<string, IntPtr> keep = delegate(string value) { IntPtr pointer = Utf8(value); held.Add(pointer); return pointer; };
            // SherpaOnnxOfflineRecognizerConfig from sherpa-onnx 1.13.8 on 64-bit Windows, written by offset. Every field left at
            // zero takes the library's default: 16 kHz features, greedy search, no language model.
            IntPtr config = Marshal.AllocHGlobal(ConfigBytes);
            try
            {
                for (int at = 0; at < ConfigBytes; at += 8) Marshal.WriteInt64(config, at, 0);
                Marshal.WriteIntPtr(config, 8, keep(Path.Combine(folder, "encoder.int8.onnx")));
                Marshal.WriteIntPtr(config, 16, keep(Path.Combine(folder, "decoder.int8.onnx")));
                Marshal.WriteIntPtr(config, 24, keep(Path.Combine(folder, "joiner.int8.onnx")));
                Marshal.WriteIntPtr(config, 104, keep(Path.Combine(folder, "tokens.txt")));
                Marshal.WriteInt32(config, 112, Math.Max(1, Math.Min(4, Environment.ProcessorCount / 2)));
                Marshal.WriteIntPtr(config, 120, keep("cpu"));
                Marshal.WriteIntPtr(config, 128, keep("nemo_transducer"));
                recognizer = SherpaOnnxCreateOfflineRecognizer(config);
            }
            finally
            {
                Marshal.FreeHGlobal(config);
                foreach (IntPtr pointer in held) Marshal.FreeHGlobal(pointer);
            }
            if (recognizer == IntPtr.Zero) throw new IOException("Her ears wouldn't start.");
            opened = folder;
        }

        // Far more than the real struct, which only grows at its end as sherpa-onnx adds models.
        private const int ConfigBytes = 4096;

        private void Close()
        {
            if (recognizer == IntPtr.Zero) return;
            SherpaOnnxDestroyOfflineRecognizer(recognizer);
            recognizer = IntPtr.Zero;
        }

        // Unloads the model, about 650 MB of memory, once voice chat ends.
        public void Dispose() { lock (gate) Close(); }

        private static IntPtr Utf8(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? "");
            IntPtr pointer = Marshal.AllocHGlobal(bytes.Length + 1);
            Marshal.Copy(bytes, 0, pointer, bytes.Length);
            Marshal.WriteByte(pointer, bytes.Length, 0);
            return pointer;
        }

        private static string Text(IntPtr pointer)
        {
            if (pointer == IntPtr.Zero) return "";
            int length = 0;
            while (Marshal.ReadByte(pointer, length) != 0) length++;
            byte[] bytes = new byte[length];
            Marshal.Copy(pointer, bytes, 0, length);
            return Encoding.UTF8.GetString(bytes);
        }

        [DllImport("sherpa-onnx-c-api.dll", CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr SherpaOnnxCreateOfflineRecognizer(IntPtr config);
        [DllImport("sherpa-onnx-c-api.dll", CallingConvention = CallingConvention.Cdecl)] private static extern void SherpaOnnxDestroyOfflineRecognizer(IntPtr recognizer);
        [DllImport("sherpa-onnx-c-api.dll", CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr SherpaOnnxCreateOfflineStream(IntPtr recognizer);
        [DllImport("sherpa-onnx-c-api.dll", CallingConvention = CallingConvention.Cdecl)] private static extern void SherpaOnnxDestroyOfflineStream(IntPtr stream);
        [DllImport("sherpa-onnx-c-api.dll", CallingConvention = CallingConvention.Cdecl)] private static extern void SherpaOnnxAcceptWaveformOffline(IntPtr stream, int sampleRate, float[] samples, int count);
        [DllImport("sherpa-onnx-c-api.dll", CallingConvention = CallingConvention.Cdecl)] private static extern void SherpaOnnxDecodeOfflineStream(IntPtr recognizer, IntPtr stream);
        [DllImport("sherpa-onnx-c-api.dll", CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr SherpaOnnxGetOfflineStreamResult(IntPtr stream);
        [DllImport("sherpa-onnx-c-api.dll", CallingConvention = CallingConvention.Cdecl)] private static extern void SherpaOnnxDestroyOfflineRecognizerResult(IntPtr result);
    }

    internal static class Tempo
    {
        internal const int Rate = 100;
        internal const double Slowest = 70;
        internal const double Fastest = 140;
        internal const double Clear = 2.3;

        internal static double Fold(double bpm)
        {
            if (bpm <= 0) return 0;
            while (bpm >= Fastest) bpm /= 2;
            while (bpm < Slowest) bpm *= 2;
            return bpm;
        }

        internal static double Toward(double bpm, double near)
        {
            if (bpm <= 0 || near <= 0) return Fold(bpm);
            while (bpm >= near * 1.414) bpm /= 2;
            while (bpm < near / 1.414) bpm *= 2;
            return bpm;
        }

        internal static bool Find(float[] onsets, int count, out double bpm, out double lastBeat, out double strength)
        {
            bpm = 0;
            lastBeat = 0;
            strength = 0;
            if (count < Rate * 4) return false;
            double[] onset = new double[count];
            double sum = 0;
            for (int i = 0; i < count; i++) { onset[i] = onsets[i]; sum += onset[i]; }
            double spread = 0;
            for (int i = 0; i < count; i++) { onset[i] -= sum / count; spread += onset[i] * onset[i]; }
            spread = Math.Sqrt(spread / count);
            if (spread <= 1e-9) return false;
            double best = 0, bestScore = double.MinValue, phase;
            for (double period = Rate * 60.0 / 200; period <= Rate * 60.0 / 50; period += 0.5)
            {
                double octaves = Math.Log(Rate * 60.0 / period / 120, 2);
                double score = Pulses(onset, period, 1, 0.5, out phase) * Math.Exp(-0.5 * octaves * octaves);
                if (score > bestScore) { bestScore = score; best = period; }
            }
            double fine = best, fineScore = double.MinValue;
            for (double period = best - 0.5; period <= best + 0.5; period += 0.05)
            {
                double score = Pulses(onset, period, 1, 0.5, out phase);
                if (score > fineScore) { fineScore = score; fine = period; }
            }
            Pulses(onset, fine, 0, 0, out phase);
            bpm = Rate * 60.0 / fine;
            lastBeat = phase / Rate;
            strength = fineScore / spread;
            return strength >= Clear;
        }

        private static double Pulses(double[] onset, double period, double half, double quarter, out double phase)
        {
            int count = onset.Length;
            double top = double.MinValue;
            phase = 0;
            for (int k = 0; k < period; k++)
            {
                double sum = 0;
                int beats = 0;
                for (double at = count - 1 - k; at >= 0; at -= period)
                {
                    sum += At(onset, at) + half * At(onset, at - period / 2);
                    if (quarter > 0) sum += quarter * (At(onset, at - period / 4) + At(onset, at - period * 3 / 4));
                    beats++;
                }
                sum /= beats;
                if (sum > top) { top = sum; phase = k; }
            }
            return top;
        }

        private static double At(double[] onset, double x)
        {
            int i = (int)Math.Floor(x);
            if (i < 0 || i >= onset.Length - 1) return 0;
            double f = x - i;
            return onset[i] * (1 - f) + onset[i + 1] * f;
        }
    }

    internal sealed class Biquad
    {
        private readonly double b0, b1, b2, a1, a2;
        private double x1, x2, y1, y2;

        private Biquad(double b0, double b1, double b2, double a0, double a1, double a2)
        {
            this.b0 = b0 / a0;
            this.b1 = b1 / a0;
            this.b2 = b2 / a0;
            this.a1 = a1 / a0;
            this.a2 = a2 / a0;
        }

        internal static Biquad Low(double rate, double cut)
        {
            double w = 2 * Math.PI * cut / rate, c = Math.Cos(w), alpha = Math.Sin(w) / (2 * 0.7071);
            return new Biquad((1 - c) / 2, 1 - c, (1 - c) / 2, 1 + alpha, -2 * c, 1 - alpha);
        }

        internal static Biquad High(double rate, double cut)
        {
            double w = 2 * Math.PI * cut / rate, c = Math.Cos(w), alpha = Math.Sin(w) / (2 * 0.7071);
            return new Biquad((1 + c) / 2, -(1 + c), (1 + c) / 2, 1 + alpha, -2 * c, 1 - alpha);
        }

        internal double Run(double x)
        {
            double y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
            x2 = x1;
            x1 = x;
            y2 = y1;
            y1 = y;
            return y;
        }
    }

    internal sealed class Onset
    {
        private const double Floor = 1e-4;
        internal readonly int Block;
        private readonly Biquad bass, midLow, midHigh, treble;
        private readonly double[] energy = new double[3];
        private readonly double[] before = new double[3];
        internal int Filled;
        private bool primed;

        internal Onset(int rate)
        {
            Block = Math.Max(1, rate / Tempo.Rate);
            bass = Biquad.Low(rate, 150);
            midLow = Biquad.High(rate, 300);
            midHigh = Biquad.Low(rate, 3000);
            treble = Biquad.High(rate, 5000);
        }

        internal bool Add(double sample, out float strength)
        {
            double low = bass.Run(sample), mid = midHigh.Run(midLow.Run(sample)), high = treble.Run(sample);
            energy[0] += low * low;
            energy[1] += mid * mid;
            energy[2] += high * high;
            strength = 0;
            if (++Filled < Block) return false;
            double rise = 0;
            for (int i = 0; i < 3; i++)
            {
                double level = Math.Log(Floor + Math.Sqrt(energy[i] / Block));
                if (primed) rise += Math.Max(0, level - before[i]);
                before[i] = level;
                energy[i] = 0;
            }
            strength = (float)rise;
            Filled = 0;
            primed = true;
            return true;
        }
    }

    internal sealed class BeatLock
    {
        internal double Bpm;
        internal double BeatAt;
        private double pending;
        private int agree;
        private int misses;

        internal void Take(bool steady, double bpm, double beatAt)
        {
            if (!steady)
            {
                if (++misses >= 8) { Bpm = 0; pending = 0; agree = 0; }
                return;
            }
            misses = 0;
            double kept = Tempo.Toward(bpm, Bpm);
            if (Bpm > 0 && Near(kept, Bpm)) { Bpm = Bpm * 0.8 + kept * 0.2; BeatAt = beatAt; pending = 0; agree = 0; return; }
            double guess = Tempo.Toward(bpm, pending);
            if (pending > 0 && Near(guess, pending)) agree++;
            else { guess = Tempo.Fold(bpm); pending = guess; agree = 1; }
            if (agree < (Bpm > 0 ? 3 : 2)) return;
            Bpm = guess;
            BeatAt = beatAt;
            pending = 0;
            agree = 0;
        }

        private static bool Near(double a, double b) { return Math.Abs(a - b) / b < 0.04; }
    }

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    internal class SoundDevices { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ISoundDevices
    {
        [PreserveSig] int EnumAudioEndpoints(int flow, int states, out IntPtr devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int flow, int role, out ISoundDevice device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ISoundDevice
    {
        [PreserveSig] int Activate(ref Guid iid, int context, IntPtr parameters, [MarshalAs(UnmanagedType.IUnknown)] out object found);
        [PreserveSig] int OpenPropertyStore(int access, out IntPtr store);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
    }

    [ComImport, Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ISoundClient
    {
        [PreserveSig] int Initialize(int shareMode, int streamFlags, long bufferDuration, long periodicity, IntPtr format, IntPtr session);
        [PreserveSig] int GetBufferSize(out int frames);
        [PreserveSig] int GetStreamLatency(out long latency);
        [PreserveSig] int GetCurrentPadding(out int frames);
        [PreserveSig] int IsFormatSupported(int shareMode, IntPtr format, out IntPtr closest);
        [PreserveSig] int GetMixFormat(out IntPtr format);
        [PreserveSig] int GetDevicePeriod(out long normal, out long shortest);
        [PreserveSig] int Start();
        [PreserveSig] int Stop();
        [PreserveSig] int Reset();
        [PreserveSig] int SetEventHandle(IntPtr handle);
        [PreserveSig] int GetService(ref Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object service);
    }

    [ComImport, Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ISoundCapture
    {
        [PreserveSig] int GetBuffer(out IntPtr data, out int frames, out int flags, out long position, out long time);
        [PreserveSig] int ReleaseBuffer(int frames);
        [PreserveSig] int GetNextPacketSize(out int frames);
    }

    // IAudioEndpointVolume, up to GetMute.
    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ISpeakerVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(IntPtr notify);
        [PreserveSig] int UnregisterControlChangeNotify(IntPtr notify);
        [PreserveSig] int GetChannelCount(out int count);
        [PreserveSig] int SetMasterVolumeLevel(float decibels, IntPtr context);
        [PreserveSig] int SetMasterVolumeLevelScalar(float level, IntPtr context);
        [PreserveSig] int GetMasterVolumeLevel(out float decibels);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
        [PreserveSig] int SetChannelVolumeLevel(int channel, float decibels, IntPtr context);
        [PreserveSig] int SetChannelVolumeLevelScalar(int channel, float level, IntPtr context);
        [PreserveSig] int GetChannelVolumeLevel(int channel, out float decibels);
        [PreserveSig] int GetChannelVolumeLevelScalar(int channel, out float level);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, IntPtr context);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    }

    // The PC's own volume and mute on the default speakers, the ones the volume keys change. Null or false when Windows won't say.
    internal static class Speakers
    {
        internal static bool? Muted() { bool muted = false; return Use(delegate(ISpeakerVolume volume) { return volume.GetMute(out muted); }) ? muted : (bool?)null; }

        internal static bool Mute(bool muted) { return Use(delegate(ISpeakerVolume volume) { return volume.SetMute(muted, IntPtr.Zero); }); }

        internal static double? Level() { float level = 0; return Use(delegate(ISpeakerVolume volume) { return volume.GetMasterVolumeLevelScalar(out level); }) ? level : (double?)null; }

        internal static bool SetLevel(double level) { return Use(delegate(ISpeakerVolume volume) { return volume.SetMasterVolumeLevelScalar((float)Math.Max(0, Math.Min(1, level)), IntPtr.Zero); }); }

        private static bool Use(Func<ISpeakerVolume, int> act)
        {
            ISoundDevices devices = null;
            ISoundDevice device = null;
            object found = null;
            try
            {
                devices = (ISoundDevices)new SoundDevices();
                if (devices.GetDefaultAudioEndpoint(0, 0, out device) != 0 || device == null) return false;
                Guid volumeId = typeof(ISpeakerVolume).GUID;
                if (device.Activate(ref volumeId, 23, IntPtr.Zero, out found) != 0 || found == null) return false;
                return act((ISpeakerVolume)found) == 0;
            }
            catch (Exception) { return false; }
            finally
            {
                if (found != null) Marshal.ReleaseComObject(found);
                if (device != null) Marshal.ReleaseComObject(device);
                if (devices != null) Marshal.ReleaseComObject(devices);
            }
        }
    }

    internal sealed class SoundTap : IDisposable
    {
        internal readonly string Device;
        internal readonly int Rate;
        internal readonly int Channels;
        internal readonly int Bits;
        internal readonly bool Float;
        private readonly ISoundClient client;
        private readonly ISoundCapture capture;
        private float[] floats = new float[0];
        private short[] shorts = new short[0];
        private int[] ints = new int[0];

        private SoundTap(string device, ISoundClient client, ISoundCapture capture, int rate, int channels, int bits, bool isFloat)
        {
            Device = device;
            this.client = client;
            this.capture = capture;
            Rate = rate;
            Channels = channels;
            Bits = bits;
            Float = isFloat;
        }

        internal string Format { get { return Rate + " Hz, " + Channels + " channels, " + Bits + "-bit " + (Float ? "float" : "PCM"); } }

        internal static string DefaultDevice()
        {
            ISoundDevices devices = null;
            ISoundDevice device = null;
            try
            {
                devices = (ISoundDevices)new SoundDevices();
                string id;
                if (devices.GetDefaultAudioEndpoint(0, 1, out device) != 0 || device == null || device.GetId(out id) != 0) return null;
                return id;
            }
            catch (Exception) { return null; }
            finally
            {
                if (device != null) Marshal.ReleaseComObject(device);
                if (devices != null) Marshal.ReleaseComObject(devices);
            }
        }

        internal static SoundTap Open(bool start)
        {
            ISoundDevices devices = null;
            ISoundDevice device = null;
            ISoundClient client = null;
            IntPtr format = IntPtr.Zero;
            try
            {
                devices = (ISoundDevices)new SoundDevices();
                string id;
                if (devices.GetDefaultAudioEndpoint(0, 1, out device) != 0 || device == null || device.GetId(out id) != 0) return null;
                Guid clientId = typeof(ISoundClient).GUID;
                object found;
                if (device.Activate(ref clientId, 23, IntPtr.Zero, out found) != 0) return null;
                client = (ISoundClient)found;
                if (client.GetMixFormat(out format) != 0 || format == IntPtr.Zero) return null;
                int tag = Marshal.ReadInt16(format, 0) & 0xFFFF;
                int channels = Marshal.ReadInt16(format, 2);
                int rate = Marshal.ReadInt32(format, 4);
                int bits = Marshal.ReadInt16(format, 14);
                if (tag == 0xFFFE) tag = Marshal.ReadInt32(format, 24);
                bool isFloat = tag == 3 && bits == 32;
                if ((!isFloat && !(tag == 1 && (bits == 16 || bits == 32))) || channels < 1 || rate < 8000) return null;
                if (client.Initialize(0, 0x00020000, 1000000, 0, format, IntPtr.Zero) != 0) return null;
                Guid captureId = typeof(ISoundCapture).GUID;
                object service;
                if (client.GetService(ref captureId, out service) != 0) return null;
                if (start && client.Start() != 0) { Marshal.ReleaseComObject(service); return null; }
                SoundTap tap = new SoundTap(id, client, (ISoundCapture)service, rate, channels, bits, isFloat);
                client = null;
                return tap;
            }
            catch (Exception) { return null; }
            finally
            {
                if (format != IntPtr.Zero) Marshal.FreeCoTaskMem(format);
                if (client != null) Marshal.ReleaseComObject(client);
                if (device != null) Marshal.ReleaseComObject(device);
                if (devices != null) Marshal.ReleaseComObject(devices);
            }
        }

        internal bool Read(Action<float> push, Onset onset, out long newest)
        {
            newest = 0;
            while (true)
            {
                int waiting;
                if (capture.GetNextPacketSize(out waiting) != 0) return false;
                if (waiting == 0) return true;
                IntPtr data;
                int frames, flags;
                long position, time;
                if (capture.GetBuffer(out data, out frames, out flags, out position, out time) != 0) return false;
                int count = frames * Channels;
                bool silent = (flags & 2) != 0 || data == IntPtr.Zero;
                if (!silent && Float) { if (floats.Length < count) floats = new float[count]; Marshal.Copy(data, floats, 0, count); }
                else if (!silent && Bits == 16) { if (shorts.Length < count) shorts = new short[count]; Marshal.Copy(data, shorts, 0, count); }
                else if (!silent) { if (ints.Length < count) ints = new int[count]; Marshal.Copy(data, ints, 0, count); }
                for (int frame = 0; frame < frames; frame++)
                {
                    double mix = 0;
                    if (!silent)
                        for (int i = frame * Channels; i < (frame + 1) * Channels; i++) mix += Float ? floats[i] : Bits == 16 ? shorts[i] / 32768.0 : ints[i] / 2147483648.0;
                    float strength;
                    if (onset.Add(mix / Channels, out strength)) push(strength);
                }
                capture.ReleaseBuffer(frames);
                if (time > 0) newest = time + (long)(frames * 1e7 / Rate);
            }
        }

        public void Dispose()
        {
            try { client.Stop(); } catch (Exception) { }
            try { Marshal.ReleaseComObject(capture); } catch (Exception) { }
            try { Marshal.ReleaseComObject(client); } catch (Exception) { }
        }
    }

    internal sealed class Groove
    {
        internal const double Latency = 0.03;
        private const double Step = 1.0 / Tempo.Rate;
        private const int Window = Tempo.Rate * 8;
        private readonly Func<double> now;
        private readonly object gate = new object();
        private BeatLock beat = new BeatLock();
        private volatile int playing;
        private int started;

        internal Groove(Func<double> now) { this.now = now; }

        internal void Want(bool on)
        {
            if (on == (playing != 0)) return;
            if (!on) { playing = 0; return; }
            int run = ++started;
            lock (gate) beat = new BeatLock();
            playing = run;
            Thread worker = new Thread(delegate() { Listen(run); });
            worker.IsBackground = true;
            worker.Name = "Capri beat";
            worker.Start();
        }

        internal bool Locked(out double bpm, out double beatAt)
        {
            lock (gate) { bpm = beat.Bpm; beatAt = beat.BeatAt; }
            return bpm > 0;
        }

        internal void Pretend(double bpm, double beatAt) { lock (gate) { beat = new BeatLock(); beat.Bpm = bpm; beat.BeatAt = beatAt; } }

        internal static string Probe()
        {
            string format = null;
            Thread check = new Thread(delegate()
            {
                SoundTap tap = SoundTap.Open(false);
                if (tap == null) return;
                format = tap.Format;
                tap.Dispose();
            });
            check.IsBackground = true;
            check.Start();
            check.Join(5000);
            return format;
        }

        private void Listen(int run)
        {
            SoundTap tap = null;
            float[] ring = new float[Window];
            float[] ordered = new float[Window];
            int head = 0, count = 0, pushed = 0;
            Onset onset = null;
            Action<float> push = delegate(float level)
            {
                ring[head] = level;
                head = (head + 1) % Window;
                if (count < Window) count++;
                pushed++;
            };
            try
            {
                double heardAt = now(), checkedAt = heardAt, openedAt = -10, estimateAt = heardAt + 1;
                while (playing == run)
                {
                    Thread.Sleep(10);
                    double at = now();
                    if (tap != null && at - checkedAt > 5)
                    {
                        checkedAt = at;
                        if (SoundTap.DefaultDevice() != tap.Device) { tap.Dispose(); tap = null; }
                    }
                    if (tap == null)
                    {
                        if (at - openedAt < 2) continue;
                        openedAt = at;
                        tap = SoundTap.Open(true);
                        count = 0;
                        heardAt = at;
                        if (tap == null) continue;
                        onset = new Onset(tap.Rate);
                    }
                    int before = pushed;
                    long newest;
                    if (!tap.Read(push, onset, out newest)) { tap.Dispose(); tap = null; continue; }
                    double clockAt = at;
                    if (newest > 0) clockAt = newest * 1e-7 + at - Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency - onset.Filled / (double)tap.Rate;
                    if (Math.Abs(clockAt - at) > 0.2) clockAt = at;
                    if (pushed != before) heardAt = at;
                    else for (; at - heardAt > 0.05; heardAt += Step) push(0);
                    if (at < estimateAt || count < Tempo.Rate * 6) continue;
                    estimateAt = at + 0.5;
                    for (int i = 0; i < count; i++) ordered[i] = ring[(head - count + i + Window) % Window];
                    double bpm, lastBeat, strength;
                    bool steady = Tempo.Find(ordered, count, out bpm, out lastBeat, out strength);
                    lock (gate) if (playing == run) beat.Take(steady, bpm, clockAt - lastBeat - Step / 2 + Latency);
                }
            }
            catch (Exception) { }
            finally { if (tap != null) tap.Dispose(); }
        }
    }
}
