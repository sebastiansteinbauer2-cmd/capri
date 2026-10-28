using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CapriDesktop
{
    internal sealed class Mainframe : Form
    {
        private static readonly Color Field = Color.FromArgb(17, 21, 28);
        private static readonly Color Hover = Color.FromArgb(26, 32, 42);
        private static readonly Color Pressed = Color.FromArgb(34, 42, 55);
        private static readonly Color Edge = Color.FromArgb(52, 60, 74);
        private static readonly Color Track = Color.FromArgb(30, 36, 46);
        private const int Wide = 380;
        private const int Tall = 618;
        private static readonly int[] Gaps = { 10, 15, 30, 60, 90, 120, 180, 300, 600 };
        private static readonly string[] GapNames = { "10 s", "15 s", "30 s", "1 min", "1.5 min", "2 min", "3 min", "5 min", "10 min" };
        private const int SetupHeight = 118;
        private const int PageTall = 668;
        // Settings opens a second column to the right, so every setting shows at once.
        private const int Column = 380;
        private const int Far = 30000;
        private static readonly int[][] PageBars = { new[] { 0, 58, 206 }, new[] { 0, 220, 376 }, new[] { 0, 386, 538 }, new[] { Column, 58, 146 }, new[] { Column, 162, 250 }, new[] { Column, 266, 424 }, new[] { Column, 446, 550 }, new[] { 0, 572, 632 } };
        private static readonly int[] Volumes = { 0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100, 125, 150 };
        private static readonly string[] VolumeNames = { "Mute", "10%", "20%", "30%", "40%", "50%", "60%", "70%", "80%", "90%", "100%", "125%", "150%" };
        private static readonly int[] SizeSteps = { 140, 170, 200, 240, 280, 320, 380, 440, 500, 560 };
        private static readonly string[] SizeNames = { "Tiny", "Small", "Small+", "Medium", "Medium+", "Large", "Large+", "Huge", "Huge+", "Giant" };
        private static readonly int[] Sleeps = { 0, 60, 120, 300, 600, 1800 };
        private static readonly string[] SleepNames = { "Never", "1 min", "2 min", "5 min", "10 min", "30 min" };
        private static readonly string[] BubbleNames = { "Quick", "Normal", "Long" };
        private static readonly string[] SoundNames = { "Off", "10%", "20%", "30%", "40%", "50%", "60%", "70%", "80%", "90%", "100%" };
        private static readonly string[] WanderNames = { "Off", "Rarely", "Sometimes", "Often", "Restless" };
        private static readonly string[] Lengths = { "Short", "Medium", "Long" };
        private static readonly int[] Recalls = { 8, 16, 32 };
        private const int VoiceHeight = 276;
        // While her free voice or ears download, a row with the brain setup's dotted bar opens here and pushes the rest down.
        private const int InstallAt = 419;
        private const int InstallRow = 40;
        // The ElevenLabs key row. Unless ELEVENLABS is picked with a key saved, one row under it folds away and the rest moves up.
        private const int KeyAt = 286;
        private const int KeyFold = 36;
        private float voiceFilled = -1;
        private readonly Label installNote;
        private readonly Companion capri;
        private readonly Label brainName;
        private readonly Label brainNote;
        private readonly Key models;
        private readonly ContextMenuStrip modelMenu;
        private readonly Key refresh;
        private readonly Label[] steps = new Label[3];
        private readonly string[] stepText = new string[3];
        private readonly Key setup;
        private readonly Label hint;
        private readonly Label tierNote;
        private readonly Rung[] rungs = new Rung[3];
        private int tier;
        private readonly Entry nickname;
        private readonly Label personalityNote;
        private readonly Key showHide;
        private readonly Key perch;
        private readonly Slider sizeSlider;
        private readonly Label sizeName;
        private readonly Toggle checkIns;
        private readonly Toggle onTop;
        private readonly Toggle startup;
        private readonly Toggle discord;
        private readonly Toggle cursor;
        private readonly Toggle bypass;
        private readonly Key talkKeys;
        private readonly Key muteKeys;
        private readonly Key typeKeys;
        private readonly Key hideKeys;
        private readonly Key confirmKeys;
        private readonly Key cancelKeys;
        // The keys pressed so far while one of her keys is being picked: -1 until a real key comes, then its modifiers.
        private int pendingMods = -1;
        private int pendingVk;
        private int heldMods;
        private readonly Toggle watch;
        private readonly Label voiceHead;
        private readonly Rung[] voiceRungs = new Rung[3];
        private bool off;
        private bool eleven;
        private bool wantEleven;
        private int arrangedFold = -1;
        private readonly Key freeVoices;
        private readonly ContextMenuStrip freeMenu;
        private readonly Key freeSetup;
        private bool freeReady;
        private readonly Slider gap;
        private readonly Label gapName;
        private readonly Entry keyEntry;
        private readonly Key connect;
        private readonly Entry voiceEntry;
        private readonly ContextMenuStrip voiceMenu;
        private readonly Key speechModels;
        private readonly ContextMenuStrip speechModelMenu;
        private readonly Label voiceNote;
        private readonly Label keysNote;
        private readonly Label blackwallNote;
        private readonly Label updateNote;
        private readonly Key keyHelp;
        private readonly Label shortcut;
        private bool shortcutReady;
        private bool muteReady = true;
        private int capturing;
        private string talkName = "Ctrl + Alt + V";
        private string muteName = "Ctrl + Alt + N";
        private string typeName = "Ctrl + Alt + T";
        private string hideName = "Ctrl + Alt + H";
        private bool typeReady = true;
        private bool hideReady = true;
        private readonly Label muteShortcut;
        private readonly Key mics;
        private readonly ContextMenuStrip micMenu;
        private bool keySaved;
        private readonly Entry textEntry;
        private readonly Key textConnect;
        private readonly Key freeKey;
        private bool textSaved;
        private readonly List<KeyValuePair<Control, int>> lower = new List<KeyValuePair<Control, int>>();
        private string[] lastInstalled;
        private float filled;
        private bool expanded = true;
        private bool loaded;
        private bool syncing;
        internal bool Quitting;
        private readonly Key more;
        private readonly Key minimize;
        private readonly Key close;
        private readonly Label build;
        private readonly List<KeyValuePair<Control, int>> mainLeft = new List<KeyValuePair<Control, int>>();
        private readonly List<KeyValuePair<Control, int>> pageLeft = new List<KeyValuePair<Control, int>>();
        private readonly Dictionary<Control, bool> stops = new Dictionary<Control, bool>();
        private bool paged;
        private readonly Key languages;
        private readonly ContextMenuStrip languageMenu;
        private readonly Slider volume;
        private readonly Label volumeName;
        private readonly Slider sounds;
        private readonly Slider sleepAfter;
        private readonly Slider bubble;
        private readonly Label bubbleName;
        private readonly Toggle intro;
        private readonly Toggle typing;
        private readonly Label sleepName;
        private readonly Label soundsName;
        private readonly Slider wander;
        private readonly Label wanderName;
        private readonly Toggle dance;
        private readonly Slider replies;
        private readonly Label repliesName;
        private readonly Slider recall;
        private readonly Label recallName;
        private readonly Entry custom;
        private readonly Label customNote;

        internal Mainframe(Companion owner, Icon icon, string version)
        {
            capri = owner;
            Text = "Capri Mainframe";
            Icon = icon;
            ShowInTaskbar = true;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96, 96);
            Font = Look.Mono(9f, FontStyle.Regular, GraphicsUnit.Point);
            BackColor = Look.Panel;
            ForeColor = Look.Ink;
            DoubleBuffered = true;
            ClientSize = new Size(Wide, Tall + SetupHeight + KeyRow);
            Caption("MAINFRAME", 26, 20, Look.Mono(13f, FontStyle.Bold, GraphicsUnit.Point), Look.Ink);
            build = Caption("v" + version, 200, 20, Look.Mono(8.5f, FontStyle.Regular, GraphicsUnit.Point), Look.Dim);
            build.AutoSize = false;
            build.TextAlign = ContentAlignment.MiddleRight;
            build.Bounds = new Rectangle(230, 20, 62, 22);
            minimize = Make("", 300, 20, 26, 22, delegate { WindowState = FormWindowState.Minimized; });
            minimize.Glyph = 1;
            minimize.AccessibleName = "Minimize";
            close = Make("", 330, 20, 26, 22, delegate { Close(); });
            close.Glyph = 2;
            close.AccessibleName = "Close";
            more = Make("Settings", 146, 20, 84, 22, delegate { ShowSettings(!paged); });
            more.Accent = Look.Cyan;
            more.Loud = true;
            int header = Controls.Count;
            Caption("BRAIN >", 28, 58, Look.Mono(9f, FontStyle.Bold, GraphicsUnit.Point), Look.Yellow);
            brainName = Caption("", 28, 78, Look.Mono(10.5f, FontStyle.Bold, GraphicsUnit.Point), Look.Ink);
            brainNote = Caption("", 28, 100, Font, Look.Dim);
            modelMenu = Dropdown();
            models = Make("", 28, 134, 244, 28, delegate { modelMenu.Show(models, new Point(0, models.Height)); });
            models.Chevron = true;
            refresh = Make("Refresh", 280, 134, 76, 28, delegate { capri.FindBrain(); });
            for (int i = 0; i < 3; i++) steps[i] = Caption("", 28, 138 + i * 22, Font, Look.Dim);
            setup = Make("Set up her brain", 28, 226, 170, 28, delegate { if (capri.SettingUp) capri.CancelSetup(); else capri.SetUpBrain(); });
            setup.Accent = Look.Cyan;
            setup.Loud = true;
            hint = Caption("Have Ollama? Open it, then press Refresh.", 28, 264, Look.Mono(9f, FontStyle.Italic, GraphicsUnit.Point), Look.Dim);
            tierNote = Caption("Pick one. Only one brain runs at a time.", 28, 168, Look.Mono(8.5f, FontStyle.Italic, GraphicsUnit.Point), Look.Dim);
            string[,] tiers = { { "NORMAL", "Free · local model" }, { "HIGHER", "Free · NVIDIA API" }, { "MAX INTELLIGENCE", "ChatGPT or Claude API" } };
            for (int i = 0; i < rungs.Length; i++)
            {
                int picked = i;
                Rung rung = new Rung();
                rung.Text = tiers[i, 0];
                rung.Hint = tiers[i, 1];
                rung.Font = Look.Mono(9f, FontStyle.Bold, GraphicsUnit.Point);
                rung.HintFont = Font;
                rung.AccessibleName = tiers[i, 0] + ", " + tiers[i, 1];
                rung.Bounds = new Rectangle(28, 184 + 24 * i, i == 1 ? 232 : 328, 24);
                rung.CheckedChanged += delegate { if (!syncing && rungs[picked].Checked) capri.ChooseTier(picked); };
                Controls.Add(rung);
                rungs[i] = rung;
            }
            rungs[0].Flag = "Start with this!";
            rungs[0].FlagFont = Look.Mono(8.5f, FontStyle.Regular, GraphicsUnit.Point);
            freeKey = Make("Free key", 266, 209, 90, 22, delegate { capri.OpenFreeKeyPage(); });
            textEntry = Input(28, 262, 244);
            textEntry.Box.UseSystemPasswordChar = true;
            textEntry.Box.AccessibleName = "NVIDIA API key";
            textEntry.Cue = "Paste your free NVIDIA key";
            textConnect = Make("Connect", 280, 262, 76, 28, delegate { ConnectText(); });
            textEntry.Box.TextChanged += delegate { RelabelText(); };
            textEntry.Box.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; ConnectText(); } };
            voiceHead = Lower(Caption("VOICE >", 28, 184, Look.Mono(9f, FontStyle.Bold, GraphicsUnit.Point), Look.Yellow));
            shortcut = Caption("Ctrl + Alt + V to talk", 156, 182, Look.Mono(8.5f, FontStyle.Regular, GraphicsUnit.Point), Look.Dim);
            shortcut.AutoSize = false;
            shortcut.TextAlign = ContentAlignment.MiddleRight;
            shortcut.Bounds = new Rectangle(156, 182, 200, 20);
            shortcut.Cursor = Cursors.Hand;
            // Keys are picked in Settings, where Confirm and Cancel sit beside them.
            shortcut.Click += delegate { ShowSettings(true); PickKeys(0); };
            Lower(shortcut);
            string[,] voiceTiers = { { "OFF", "Silent · text only" }, { "FREE VOICE", "Free · runs on your PC" }, { "ELEVENLABS", "Sounds amazing · paid" } };
            for (int i = 0; i < voiceRungs.Length; i++)
            {
                bool silent = i == 0;
                bool paid = i == 2;
                Rung rung = new Rung();
                // A pair of its own: a self-checking radio button would untick the BRAIN tiers too.
                rung.AutoCheck = false;
                rung.TabStop = true;
                rung.Text = voiceTiers[i, 0];
                rung.Hint = voiceTiers[i, 1];
                rung.Font = Look.Mono(9f, FontStyle.Bold, GraphicsUnit.Point);
                rung.HintFont = Font;
                rung.AccessibleName = voiceTiers[i, 0] + ", " + voiceTiers[i, 1];
                rung.Bounds = new Rectangle(28, 208 + 24 * i, paid ? 246 : 328, 24);
                rung.Click += delegate { if (syncing) return; if (silent) PickSilent(); else PickVoice(paid); };
                Controls.Add(rung);
                voiceRungs[i] = Lower(rung);
            }
            keyHelp = Lower(Make("Setup guide", 276, 257, 80, 22, delegate { capri.OpenKeyPage(); }));
            keyEntry = Lower(Input(28, KeyAt, 244));
            keyEntry.Box.UseSystemPasswordChar = true;
            keyEntry.Box.AccessibleName = "ElevenLabs API key";
            connect = Lower(Make("Connect", 280, KeyAt, 76, 28, delegate { Connect(); }));
            keyEntry.Box.TextChanged += delegate { Relabel(); };
            keyEntry.Box.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Connect(); } };
            voiceMenu = Dropdown();
            voiceEntry = Lower(Input(28, 322, 196));
            voiceEntry.Chevron = true;
            voiceEntry.Cue = "Voice ID";
            voiceEntry.Box.AccessibleName = "ElevenLabs voice ID";
            voiceEntry.Drop += delegate { voiceMenu.Show(voiceEntry, new Point(0, voiceEntry.Height)); };
            voiceEntry.Box.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; capri.ChooseVoice(voiceEntry.Box.Text); } };
            voiceEntry.Box.Leave += delegate { capri.ChooseVoice(voiceEntry.Box.Text); };
            speechModelMenu = Dropdown();
            speechModels = Lower(Make("Default model", 232, 322, 124, 28, delegate { speechModelMenu.Show(speechModels, new Point(0, speechModels.Height)); }));
            speechModels.Chevron = true;
            freeMenu = Dropdown();
            freeVoices = Lower(Make("Free voice: Her own", 28, 322, 196, 28, delegate { freeMenu.Show(freeVoices, new Point(0, freeVoices.Height)); }));
            freeVoices.Chevron = true;
            freeVoices.Visible = false;
            freeSetup = Lower(Make("Get free voice", 232, 322, 124, 28, delegate { if (capri.SettingUpVoice) capri.CancelFreeVoice(); else capri.SetUpFreeVoice(); }));
            freeSetup.Accent = Look.Cyan;
            freeSetup.Loud = true;
            freeSetup.Visible = false;
            micMenu = Dropdown();
            mics = Lower(Make("Mic: default", 28, 358, 244, 28, delegate { ShowMics(); }));
            mics.Chevron = true;
            mics.AccessibleName = "Microphone";
            Lower(Make("Test", 280, 358, 76, 28, delegate { capri.TestVoice(); }));
            muteShortcut = Lower(Caption("Ctrl + Alt + N mutes mic", 28, 393, Look.Mono(8.5f, FontStyle.Regular, GraphicsUnit.Point), Look.Dim));
            muteShortcut.Cursor = Cursors.Hand;
            muteShortcut.Click += delegate { ShowSettings(true); PickKeys(1); };
            Deactivate += delegate { CancelKeys(); };
            voiceNote = Lower(Caption("", 28, InstallAt, Font, Look.Dim));
            installNote = Caption("", 28, InstallAt + 2, Font, Look.Ink);
            installNote.Visible = false;
            int v = VoiceHeight;
            Lower(Caption("CAPRI >", 28, 184 + v, Look.Mono(9f, FontStyle.Bold, GraphicsUnit.Point), Look.Yellow));
            showHide = Lower(Make("Hide Capri", 28, 206 + v, 104, 28, delegate { capri.ToggleHidden(); }));
            Lower(Make("Bring here", 140, 206 + v, 104, 28, delegate { capri.BringHere(); }));
            perch = Lower(Make("Ride cursor", 252, 206 + v, 104, 28, delegate { capri.TogglePerch(); }));
            Lower(Make("Check for updates", 28, 250 + v, 150, 28, delegate { capri.CheckUpdates(); }));
            Key help = Lower(Make("Help", 186, 250 + v, 62, 28, delegate { capri.OpenHelp(); }));
            help.AccessibleName = "Help: ask Capri on the website";
            Key quit = Lower(Make("Quit Capri", 256, 250 + v, 100, 28, delegate { capri.Quit(); }));
            quit.Accent = Look.Red;
            updateNote = Lower(Caption("", 28, 288 + v, Look.Mono(8.5f, FontStyle.Regular, GraphicsUnit.Point), Look.Dim));
            for (int i = header; i < Controls.Count; i++) mainLeft.Add(new KeyValuePair<Control, int>(Controls[i], Controls[i].Left));
            int first = Controls.Count;
            Caption("YOU >", 28, 58, Look.Mono(9f, FontStyle.Bold, GraphicsUnit.Point), Look.Yellow);
            personalityNote = Caption("", 84, 59, Look.Mono(8.5f, FontStyle.Regular, GraphicsUnit.Point), Look.Dim);
            Caption("Language", 28, 86, Font, Look.Dim);
            languageMenu = Dropdown();
            for (int i = 0; i < Brain.Languages.GetLength(0); i++)
            {
                string picked = Brain.Languages[i, 1];
                ToolStripMenuItem item = Choice(Brain.Languages[i, 0], false, delegate { capri.SetLanguage(picked); Sync(); });
                item.Tag = picked;
                languageMenu.Items.Add(item);
            }
            languages = Make("English", 128, 80, 228, 28, delegate { languageMenu.Show(languages, new Point(0, languages.Height)); });
            languages.Chevron = true;
            languages.AccessibleName = "Her language";
            // A label of its own, so the question still shows once a name is saved in the box.
            Caption("What should she call you?", 28, 118, Font, Look.Dim);
            nickname = Input(28, 140, 244);
            nickname.Cue = "Your name or a nickname";
            nickname.Box.AccessibleName = "What should she call you?";
            nickname.Box.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; UseNickname(); } };
            Make("Save", 280, 140, 76, 28, delegate { UseNickname(); });
            Make("Open her notes", 28, 176, 120, 28, delegate { capri.OpenNotes(); }).AccessibleName = "Open her notes: me.md, her memories";
            Make("Edit personality", 156, 176, 130, 28, delegate { capri.OpenPersonality(); }).AccessibleName = "Edit her personality: personality.md";
            Make("Reset", 294, 176, 62, 28, delegate { capri.ResetPersonality(); }).AccessibleName = "Reset her personality to her default";
            Caption("VOICE >", 28, 220, Look.Mono(9f, FontStyle.Bold, GraphicsUnit.Point), Look.Yellow);
            Caption("Volume", 28, 249, Font, Look.Dim);
            volume = PageSlider(VolumeNames, 242, "Her voice volume");
            volumeName = Caption("100%", 292, 249, Font, Look.Ink);
            volume.Changed += delegate { if (syncing) return; volumeName.Text = VolumeNames[volume.Value]; capri.SetVolume(Volumes[volume.Value]); };
            Caption("Sounds", 28, 285, Font, Look.Dim);
            sounds = PageSlider(SoundNames, 278, "Sound effects volume");
            soundsName = Caption("60%", 292, 285, Font, Look.Ink);
            sounds.Changed += delegate { if (syncing) return; soundsName.Text = SoundNames[sounds.Value]; capri.SetSoundLevel(sounds.Value * 10); };
            Caption("Bubble", 28, 321, Font, Look.Dim);
            bubble = PageSlider(BubbleNames, 314, "How long her replies stay up");
            bubbleName = Caption("Normal", 292, 321, Font, Look.Ink);
            bubble.Changed += delegate { if (syncing) return; bubbleName.Text = BubbleNames[bubble.Value]; capri.SetBubbleTime(bubble.Value); };
            typing = Check("Typing sound", 28, 350, 328, delegate { if (!syncing) capri.ToggleTyping(); });
            typing.Hint = "When she types";
            typing.HintFont = Look.Mono(8.5f, FontStyle.Regular, GraphicsUnit.Point);
            Caption("MOVES >", 28, 386, Look.Mono(9f, FontStyle.Bold, GraphicsUnit.Point), Look.Yellow);
            Caption("Wander", 28, 415, Font, Look.Dim);
            wander = PageSlider(WanderNames, 408, "How much she wanders");
            wanderName = Caption("Off", 292, 415, Font, Look.Ink);
            wander.Changed += delegate { if (syncing) return; wanderName.Text = WanderNames[wander.Value]; capri.SetWanderLevel(wander.Value); };
            Caption("Sleep", 28, 451, Font, Look.Dim);
            sleepAfter = PageSlider(SleepNames, 444, "How long until she falls asleep");
            sleepName = Caption("2 min", 292, 451, Font, Look.Ink);
            sleepAfter.Changed += delegate { if (syncing) return; sleepName.Text = SleepNames[sleepAfter.Value]; capri.SetSleepAfter(Sleeps[sleepAfter.Value]); };
            dance = Check("Dance to music", 28, 480, 328, delegate { if (!syncing) capri.ToggleDance(); });
            dance.Hint = "When songs play";
            dance.HintFont = Look.Mono(8.5f, FontStyle.Regular, GraphicsUnit.Point);
            intro = Check("Glitch in at startup", 28, 512, 328, delegate { if (!syncing) capri.ToggleIntro(); });
            intro.Hint = "Tendrils + sound";
            intro.HintFont = Look.Mono(8.5f, FontStyle.Regular, GraphicsUnit.Point);
            const int c = Column;
            Caption("CAPRI >", 28 + c, 58, Look.Mono(9f, FontStyle.Bold, GraphicsUnit.Point), Look.Yellow);
            Caption("Size", 28 + c, 87, Font, Look.Dim);
            sizeSlider = PageSlider(SizeNames, 80, "Her size", c);
            sizeSlider.Changed += delegate { if (syncing) return; sizeName.Text = SizeNames[sizeSlider.Value]; capri.SetCharacterSize(SizeSteps[sizeSlider.Value]); };
            sizeName = Caption("Medium", 292 + c, 87, Font, Look.Ink);
            checkIns = Check("Check in on me", 28 + c, 116, 150, delegate { if (!syncing) capri.ToggleCheckIns(); });
            onTop = Check("Always on top", 186 + c, 116, 170, delegate { if (!syncing) capri.ToggleOnTop(); });
            Caption("WATCH >", 28 + c, 162, Look.Mono(9f, FontStyle.Bold, GraphicsUnit.Point), Look.Yellow);
            watch = Check("Watch my screen", 28 + c, 186, 328, delegate { if (!syncing) capri.ToggleWatch(); });
            watch.Hint = "She comments now and then";
            watch.HintFont = Look.Mono(8.5f, FontStyle.Regular, GraphicsUnit.Point);
            Caption("Every", 28 + c, 223, Font, Look.Dim);
            gap = PageSlider(GapNames, 216, "Time between glances", c);
            gap.Changed += delegate { if (syncing) return; gapName.Text = GapNames[gap.Value]; capri.SetGlanceGap(Gaps[gap.Value]); };
            gapName = Caption("2 min", 292 + c, 223, Font, Look.Ink);
            Caption("BRAIN >", 28 + c, 266, Look.Mono(9f, FontStyle.Bold, GraphicsUnit.Point), Look.Yellow);
            Caption("Replies", 28 + c, 295, Font, Look.Dim);
            replies = PageSlider(Lengths, 288, "Reply length", c);
            repliesName = Caption("Short", 292 + c, 295, Font, Look.Ink);
            replies.Changed += delegate { if (syncing) return; repliesName.Text = Lengths[replies.Value]; capri.SetWordiness(replies.Value); };
            Caption("Memory", 28 + c, 331, Font, Look.Dim);
            recall = PageSlider(Lengths, 324, "How much of the chat she keeps", c);
            recallName = Caption("Short", 292 + c, 331, Font, Look.Ink);
            recall.Changed += delegate { if (syncing) return; recallName.Text = Lengths[recall.Value]; capri.SetRecall(Recalls[recall.Value]); };
            custom = Input(28 + c, 362, 244);
            custom.Cue = "Custom model name";
            custom.Box.AccessibleName = "Custom model name";
            custom.Box.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; UseCustom(); } };
            Make("Use", 280 + c, 362, 76, 28, delegate { UseCustom(); });
            customNote = Caption("Any Ollama model you've pulled, or a model id on your key.", 28 + c, 398, Look.Mono(8.5f, FontStyle.Regular, GraphicsUnit.Point), Look.Dim);
            customNote.MaximumSize = new Size(328, 0);
            Caption("HABITS >", 28 + c, 446, Look.Mono(9f, FontStyle.Bold, GraphicsUnit.Point), Look.Yellow);
            startup = Check("Start with Windows", 28 + c, 470, 150, delegate { if (!syncing) capri.ToggleStartup(); });
            discord = Check("Show on Discord", 186 + c, 470, 170, delegate { if (!syncing) capri.ToggleDiscord(); });
            cursor = Check("Use her cursor", 28 + c, 502, 150, delegate { if (!syncing) capri.ToggleCursor(); });
            // Blackwall mode was "Bypass all permissions" until 0.7.4; its label says what it does.
            bypass = Check("Blackwall mode", 186 + c, 502, 170, delegate { if (!syncing) capri.ToggleBypass(); });
            bypass.AccessibleName = "Blackwall mode. Turned off for safety in this version";
            bypass.Enabled = false;  // SAFETY: the switch can't be turned on
            blackwallNote = Caption("Turned off for safety. She always asks first.", 28 + c, 526, Look.Mono(8.5f, FontStyle.Regular, GraphicsUnit.Point), Look.Dim);
            blackwallNote.AutoSize = false;
            blackwallNote.TextAlign = ContentAlignment.MiddleRight;
            blackwallNote.Bounds = new Rectangle(28 + c, 526, 328, 18);
            Caption("KEYS >", 28, 576, Look.Mono(9f, FontStyle.Bold, GraphicsUnit.Point), Look.Yellow);
            talkKeys = Make("Talk: Ctrl + Alt + V", 96, 570, 250, 28, delegate { PickKeys(0); });
            talkKeys.AccessibleName = "Her talk key. Click, press new keys, then Confirm";
            muteKeys = Make("Mute mic: Ctrl + Alt + N", 358, 570, 250, 28, delegate { PickKeys(1); });
            muteKeys.AccessibleName = "Her mute key. Click, press new keys, then Confirm";
            typeKeys = Make("Type: Ctrl + Alt + T", 96, 604, 250, 28, delegate { PickKeys(2); });
            typeKeys.AccessibleName = "Her typing key. Click, press new keys, then Confirm";
            hideKeys = Make("Hide/show: Ctrl + Alt + H", 358, 604, 250, 28, delegate { PickKeys(3); });
            hideKeys.AccessibleName = "Her hide key. Click, press new keys, then Confirm";
            // Only a click ends picking a key, so Enter, Esc and Tab can be her keys too.
            confirmKeys = Make("Confirm", 620, 570, 116, 28, delegate { ConfirmKeys(); });
            confirmKeys.Accent = Look.Cyan;
            confirmKeys.Loud = true;
            confirmKeys.AccessibleName = "Confirm her new keys";
            cancelKeys = Make("Cancel", 620, 604, 116, 28, delegate { CancelKeys(); });
            cancelKeys.AccessibleName = "Cancel and keep her old keys";
            confirmKeys.Visible = cancelKeys.Visible = false;
            keysNote = Caption("", 96, 640, Look.Mono(8.5f, FontStyle.Regular, GraphicsUnit.Point), Look.Dim);
            keysNote.MaximumSize = new Size(512, 0);
            for (int i = first; i < Controls.Count; i++)
            {
                Control control = Controls[i];
                pageLeft.Add(new KeyValuePair<Control, int>(control, control.Left));
                control.Left += Far;
                Reachable(control, false);
                // Clicking into anything else while a key is being picked keeps the old key, so typing in a box is never swallowed.
                control.Enter += delegate { if (capturing != 0 && control != confirmKeys && control != cancelKeys && control != PickedKey) CancelKeys(); };
            }
            Searching();
            Sync();
        }

        private Slider PageSlider(string[] names, int y, string name, int x = 0)
        {
            Slider slider = new Slider(names);
            slider.Bounds = new Rectangle(96 + x, y, 188, 30);
            slider.AccessibleName = name;
            Controls.Add(slider);
            return slider;
        }

        private void UseNickname()
        {
            capri.SetNickname(nickname.Box.Text);
            nickname.Box.Text = capri.Nickname;
        }

        internal string NicknameShown { get { return nickname.Box.Text; } }

        internal void TypeNickname(string typed) { nickname.Box.Text = typed; UseNickname(); }

        private void UseCustom()
        {
            customNote.Text = capri.ChooseCustomModel(custom.Box.Text, lastInstalled);
        }

        internal bool SettingsShown { get { return paged; } }

        internal string SizeStatus { get { return sizeName.Text; } }

        internal bool TypingShown { get { return typing.Checked; } }

        internal string HabitsStatus { get { return startup.Checked ? "startup" : "-"; } }

        internal bool DiscordShown { get { return discord.Checked; } }

        internal string JobsStatus { get { return (cursor.Checked ? "cursor" : "-") + " | " + (bypass.Checked ? "bypass" : "-"); } }

        internal string BlackwallStatus { get { return bypass.Text + " | " + blackwallNote.Text; } }

        internal string SettingsStatus { get { return languages.Text + " | " + volumeName.Text + " | " + soundsName.Text + " | " + wanderName.Text + " | " + sleepName.Text + " | " + (dance.Checked ? "dance" : "-") + " | " + (intro.Checked ? "intro" : "-") + " | " + repliesName.Text + " | " + recallName.Text + " | " + bubbleName.Text; } }

        internal void ShowSettings(bool on)
        {
            if (!on) CancelKeys();
            paged = on;
            more.Text = on ? "Back" : "Settings";
            more.Invalidate();
            foreach (KeyValuePair<Control, int> entry in mainLeft) { entry.Key.Left = Scaled(entry.Value) + (on ? Far : 0); Reachable(entry.Key, !on); }
            foreach (KeyValuePair<Control, int> entry in pageLeft) { entry.Key.Left = Scaled(entry.Value) + (on ? 0 : Far); Reachable(entry.Key, on); }
            int edge = on ? Column : 0;
            build.Left = Scaled(230 + edge);
            minimize.Left = Scaled(300 + edge);
            close.Left = Scaled(330 + edge);
            if (Hidden(ActiveControl)) more.Focus();
            if (on) Sync();
            Arrange(expanded);
            Rectangle area = Screen.FromControl(this).WorkingArea;
            if (on && Right > area.Right) Left = Math.Max(area.Left, area.Right - Width);
            // She opens centred on the shorter main page, so the taller Settings page can hang off the bottom.
            if (on && Bottom > area.Bottom) Top = Math.Max(area.Top, area.Bottom - Height);
        }

        private void Reachable(Control control, bool on)
        {
            if (!stops.ContainsKey(control)) stops[control] = control.TabStop;
            control.TabStop = on && stops[control];
            foreach (Control child in control.Controls) Reachable(child, on);
        }

        private bool Hidden(Control control)
        {
            List<KeyValuePair<Control, int>> hidden = paged ? mainLeft : pageLeft;
            for (Control top = control; top != null && top != this; top = top.Parent)
                foreach (KeyValuePair<Control, int> entry in hidden) if (entry.Key == top) return true;
            return false;
        }

        internal string TabWalk()
        {
            Control from = more;
            int reached = 0;
            for (int i = 0; i < 80; i++)
            {
                if (!SelectNextControl(from, true, true, true, true)) break;
                Control at = ActiveControl;
                if (at == null || at == from || at == more) break;
                if (Hidden(at)) return "Tab reached a hidden " + (at.AccessibleName ?? at.Text);
                reached++;
                from = at;
            }
            more.Focus();
            return reached == 0 ? "Tab reached nothing" : null;
        }

        private T Lower<T>(T control) where T : Control
        {
            lower.Add(new KeyValuePair<Control, int>(control, control.Top));
            return control;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams value = base.CreateParams;
                value.Style = (value.Style | 0x00020000 | 0x00080000) & ~0x00010000;
                return value;
            }
        }

        protected override void WndProc(ref Message message)
        {
            base.WndProc(ref message);
            if (message.Msg == 0x0084 && message.Result == new IntPtr(1)) message.Result = new IntPtr(2);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            loaded = true;
            Arrange(expanded);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            Shape();
            Invalidate(true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            float s = DeviceScale();
            Graphics graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Backdrop(graphics, this, s);
            using (GraphicsPath edge = Look.Plate(new RectangleF(s, s, Width - 2 * s - 1, Height - 2 * s - 1), 12 * s, -1, Height / 2f, 0, true))
            using (Pen cyan = new Pen(Look.Cyan, 2 * s))
                graphics.DrawPath(cyan, edge);
            if (paged)
            {
                using (Brush red = new SolidBrush(Look.Red))
                    foreach (int[] bar in PageBars) graphics.FillRectangle(red, (13 + bar[0]) * s, bar[1] * s, 3 * s, (bar[2] - bar[1]) * s);
                return;
            }
            int shift = (expanded ? SetupHeight : 0) + KeyRow;
            int below = voiceFilled >= 0 ? InstallRow : 0;
            int fold = arrangedFold;
            using (Brush red = new SolidBrush(Look.Red))
            {
                graphics.FillRectangle(red, 13 * s, 58 * s, 3 * s, ((expanded ? 280 : 162) + KeyRow - 58) * s);
                graphics.FillRectangle(red, 13 * s, (184 + shift) * s, 3 * s, (InstallAt + 29 - 184 + below - fold) * s);
                graphics.FillRectangle(red, 13 * s, (184 + VoiceHeight + shift + below - fold) * s, 3 * s, (234 - 184) * s);
            }
            if (expanded) Dots(graphics, s, 210, filled);
            if (below > 0) Dots(graphics, s, InstallAt + 24 + shift - fold, voiceFilled);
        }

        // The dotted bar her brain setup and her voice downloads share.
        private static void Dots(Graphics graphics, float s, int y, float share)
        {
            float pitch = 6 * s;
            int count = (int)(328 / 6f);
            int lit = (int)Math.Round(count * share);
            using (Brush on = new SolidBrush(Look.Cyan))
            using (Brush off = new SolidBrush(Track))
                for (int i = 0; i < count; i++) graphics.FillRectangle(i < lit ? on : off, 28 * s + i * pitch, y * s, pitch - 2 * s, 6 * s);
        }

        private static void Backdrop(Graphics graphics, Control control, float s)
        {
            graphics.Clear(Look.Panel);
            float pitch = 3 * s;
            float first = control is Form ? 0 : (pitch - control.Top % pitch) % pitch;
            using (Pen scan = new Pen(Color.FromArgb(9, 255, 255, 255), Math.Max(1, s)))
                for (float row = first; row < control.Height; row += pitch) graphics.DrawLine(scan, 0, row, control.Width, row);
        }

        private void Shape()
        {
            float s = DeviceScale();
            Region old = Region;
            using (GraphicsPath plate = Look.Plate(new RectangleF(0, 0, Width, Height), 12 * s, -1, Height / 2f, 0, true)) Region = new Region(plate);
            if (old != null) old.Dispose();
        }

        private float DeviceScale() { return CurrentAutoScaleDimensions.Width / 96f; }

        private int Scaled(int value) { return loaded ? (int)Math.Round(value * DeviceScale()) : value; }

        private void Arrange(bool setupShown)
        {
            expanded = setupShown;
            int top = setupShown ? SetupHeight : 0, shift = top + KeyRow;
            tierNote.Top = Scaled(168 + top);
            for (int i = 0; i < rungs.Length; i++) rungs[i].Top = Scaled(184 + 24 * i + top);
            freeKey.Top = Scaled(209 + top);
            textEntry.Top = Scaled(262 + top);
            textConnect.Top = textEntry.Top;
            textEntry.Visible = textConnect.Visible = tier > 0;
            int below = voiceFilled >= 0 ? InstallRow : 0;
            int fold = arrangedFold = Fold;
            foreach (KeyValuePair<Control, int> entry in lower) entry.Key.Top = Scaled(entry.Value + shift + (entry.Value >= InstallAt ? below : 0) - (entry.Value > KeyAt ? fold : 0));
            installNote.Top = Scaled(InstallAt + 2 + shift - fold);
            installNote.Visible = below > 0;
            foreach (Label step in steps) step.Visible = setupShown;
            setup.Visible = setupShown;
            refresh.Top = Scaled(setupShown ? 226 : 134);
            brainNote.MaximumSize = new Size(Scaled(328), 0);
            voiceNote.MaximumSize = new Size(Scaled(328), 0);
            updateNote.MaximumSize = new Size(Scaled(328), 0);
            customNote.MaximumSize = new Size(Scaled(328), 0);
            if (keysNote != null) keysNote.MaximumSize = new Size(Scaled(512), 0);
            ClientSize = new Size(Scaled(paged ? Wide + Column : Wide), Scaled(paged ? PageTall : Tall + shift + below - fold));
            Invalidate(true);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!Quitting && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; WindowState = FormWindowState.Minimized; }
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { modelMenu.Dispose(); voiceMenu.Dispose(); speechModelMenu.Dispose(); micMenu.Dispose(); languageMenu.Dispose(); freeMenu.Dispose(); }
            base.Dispose(disposing);
        }

        internal void Restore()
        {
            if (!Visible) Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate();
        }

        internal void Sync()
        {
            syncing = true;
            showHide.Text = capri.IsHidden ? "Show Capri" : "Hide Capri";
            perch.Text = capri.Riding ? "Hop off" : "Ride cursor";
            int near = 0;
            for (int i = 0; i < SizeSteps.Length; i++) if (Math.Abs(SizeSteps[i] - capri.CharacterSize) < Math.Abs(SizeSteps[near] - capri.CharacterSize)) near = i;
            sizeSlider.Value = near;
            sizeName.Text = SizeNames[near];
            checkIns.Checked = capri.CheckIns;
            onTop.Checked = capri.OnTop;
            startup.Checked = capri.StartsWithWindows;
            discord.Checked = capri.OnDiscord;
            cursor.Checked = capri.UsesCursor;
            bypass.Checked = capri.Bypassing;
            watch.Checked = capri.Watching;
            VoiceTier();
            int chosen = Array.IndexOf(Gaps, capri.GlanceGap);
            gap.Value = chosen < 0 ? 5 : chosen;
            gapName.Text = GapNames[gap.Value];
            if (!nickname.Box.Focused) nickname.Box.Text = capri.Nickname;
            string language = capri.Language;
            foreach (ToolStripMenuItem item in languageMenu.Items)
            {
                item.Checked = (string)item.Tag == language;
                if (item.Checked) languages.Text = item.Text;
            }
            languages.Invalidate();
            int loud = 0;
            for (int i = 0; i < Volumes.Length; i++) if (Math.Abs(Volumes[i] - capri.Volume) < Math.Abs(Volumes[loud] - capri.Volume)) loud = i;
            volume.Value = loud;
            volumeName.Text = VolumeNames[loud];
            sounds.Value = Math.Max(0, Math.Min(10, (capri.SoundLevel + 5) / 10));
            soundsName.Text = SoundNames[sounds.Value];
            sleepAfter.Value = Math.Max(0, Array.IndexOf(Sleeps, capri.SleepAfter));
            sleepName.Text = SleepNames[sleepAfter.Value];
            wander.Value = capri.WanderLevel;
            wanderName.Text = WanderNames[wander.Value];
            dance.Checked = capri.Dancing;
            bubble.Value = capri.BubbleTime;
            bubbleName.Text = BubbleNames[bubble.Value];
            intro.Checked = capri.Intro;
            typing.Checked = capri.TypingSound;
            replies.Value = capri.Wordiness;
            repliesName.Text = Lengths[replies.Value];
            recall.Value = Math.Max(0, Array.IndexOf(Recalls, capri.Recall));
            recallName.Text = Lengths[recall.Value];
            ShowPersonality();
            syncing = false;
        }

        // Says which personality she's using, and why it's her default when personality.md is missing, empty or can't be read.
        internal void ShowPersonality()
        {
            string note = Brain.PersonalityNote;
            personalityNote.Text = note;
            personalityNote.ForeColor = note == Brain.HerDefault || note == Brain.YourEdit ? Look.Dim : note == Brain.LongNote ? Look.Yellow : Look.Red;
        }

        internal string PersonalityShown { get { return personalityNote.Text; } }

        internal Color PersonalityColour { get { return personalityNote.ForeColor; } }

        // Back from editing personality.md in Notepad, the note catches up.
        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            ShowPersonality();
        }

        internal string Status { get { return brainName.Text + " | " + brainNote.Text; } }

        internal bool WatchShown { get { return watch.Checked; } }

        internal bool ElevenShown { get { return voiceRungs[2].Checked && !voiceRungs[1].Checked && !voiceRungs[0].Checked; } }

        internal string VoiceTierStatus { get { return (off ? "OFF" : ElevenShown ? "ELEVENLABS" : "FREE VOICE") + " | " + (eleven && !off ? "key" : "-") + " | " + (off ? "-" : !eleven ? "free voice" : keySaved ? "voice id" : "-") + " | fold " + arrangedFold; } }

        internal string Overlap()
        {
            for (int i = 0; i < Controls.Count; i++)
                for (int j = i + 1; j < Controls.Count; j++)
                {
                    Control a = Controls[i], b = Controls[j];
                    if (!a.Visible || !b.Visible || (a is Label && a.Text.Length == 0) || (b is Label && b.Text.Length == 0)) continue;
                    if (a.Bounds.IntersectsWith(b.Bounds)) return a.Text + " / " + b.Text;
                }
            return null;
        }

        internal string GapShown { get { return gapName.Text; } }

        internal void SlideGap(int steps) { gap.Focus(); for (int i = 0; i < Math.Abs(steps); i++) { KeyEventArgs press = new KeyEventArgs(steps > 0 ? Keys.Right : Keys.Left); typeof(Control).GetMethod("OnKeyDown", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(gap, new object[] { press }); } }

        internal void Searching()
        {
            brainName.Text = "Looking for a brain...";
            brainNote.Text = "";
            models.Visible = false;
            hint.Visible = false;
            Arrange(false);
        }

        internal void ShowOllama(string[] installed, string chosen)
        {
            brainName.Text = "Ollama · " + chosen;
            brainNote.Text = "Running on this PC";
            models.Text = chosen;
            models.Visible = true;
            hint.Visible = false;
            lastInstalled = installed;
            modelMenu.Items.Clear();
            foreach (string name in installed)
            {
                string pick = name;
                ToolStripMenuItem item = new ToolStripMenuItem(name, null, delegate { capri.ChooseModel(pick); ShowOllama(lastInstalled, pick); });
                item.Checked = name == chosen;
                modelMenu.Items.Add(item);
            }
            Arrange(false);
        }

        internal void ShowCloud(Cloud remote, List<string> found)
        {
            brainName.Text = remote.Name + " · " + remote.Model;
            brainNote.Text = "Using your " + remote.Company + " key";
            models.Text = remote.Model;
            models.Visible = true;
            hint.Visible = false;
            modelMenu.Items.Clear();
            foreach (string name in found ?? new List<string> { remote.Model })
            {
                string pick = name;
                modelMenu.Items.Add(Choice(name, name == remote.Model, delegate { capri.ChooseModel(pick); ShowCloud(remote, found); }));
            }
            Arrange(false);
        }

        private int KeyRow { get { return tier > 0 ? 128 : 94; } }

        internal void ShowTiers(int pick, string nvidiaTail, string paidTail)
        {
            tier = Math.Max(0, Math.Min(rungs.Length - 1, pick));
            syncing = true;
            for (int i = 0; i < rungs.Length; i++) rungs[i].Checked = i == tier;
            syncing = false;
            string tail = tier == 1 ? nvidiaTail : tier == 2 ? paidTail : null;
            textSaved = tail != null;
            textEntry.Box.AccessibleName = tier == 1 ? "NVIDIA API key" : "OpenAI, Anthropic or DeepSeek API key";
            tierNote.Text = tier == 1 ? "Most NVIDIA models can't see your screen." : "Pick one. Only one brain runs at a time.";
            textEntry.Cue = textSaved ? (tier == 1 ? "NVIDIA key saved, ends " : "Key saved, ends ") + tail : tier == 1 ? "Paste your free NVIDIA key" : "Paste a ChatGPT or Claude key";
            RelabelText();
            Arrange(expanded);
        }

        internal bool TiersFit
        {
            get
            {
                Rectangle last = rungs[rungs.Length - 1].Bounds;
                bool row = !textEntry.Visible || (textEntry.Top >= last.Bottom && textConnect.Right <= ClientSize.Width);
                int end = textEntry.Visible ? textEntry.Bottom : last.Bottom;
                return rungs[0].Top >= tierNote.Bottom && rungs[1].Top >= rungs[0].Bottom && rungs[2].Top >= rungs[1].Bottom && freeKey.Left >= rungs[1].Right && freeKey.Top >= rungs[1].Top && freeKey.Bottom <= rungs[1].Bottom && row && end <= voiceHead.Top && rungs[0].Right <= ClientSize.Width;
            }
        }

        internal string TextStatus { get { return "tier " + tier + " | " + brainName.Text + " | " + brainNote.Text + " | " + (textEntry.Visible ? textEntry.Cue + " | " + textConnect.Text : "no key row"); } }

        private void ConnectText()
        {
            string typed = textEntry.Box.Text.Trim();
            if (typed.Length > 0) { textEntry.Box.Text = ""; capri.ConnectText(typed); }
            else if (textSaved) capri.ForgetText();
            else textEntry.Box.Focus();
        }

        private void RelabelText()
        {
            bool forget = textSaved && textEntry.Box.Text.Trim().Length == 0;
            textConnect.Text = forget ? "Forget" : "Connect";
            textConnect.Accent = forget ? Look.Red : Look.Ink;
            textConnect.Invalidate();
        }

        internal void ShowLocal(Pick pick)
        {
            brainName.Text = "Built-in · " + pick.Name;
            brainNote.Text = "Running on this PC";
            models.Visible = false;
            hint.Visible = false;
            Arrange(false);
        }

        internal void ShowSetup(Machine machine, Pick pick, bool viaOllama, string problem)
        {
            brainName.Text = problem == null ? "No brain yet" : "Setup stopped";
            brainNote.Text = problem ?? "Best fit for this PC: " + pick.Name;
            models.Visible = false;
            Step(0, "✓", machine.Describe(), Look.Ink);
            Step(1, "2", "Download " + pick.Name + " (" + pick.Download + ")" + (viaOllama ? " with Ollama" : ""), Look.Dim);
            Step(2, "3", "Wake her up", Look.Dim);
            filled = 0;
            setup.Text = problem == null ? "Set up her brain" : "Try again";
            setup.Enabled = true;
            hint.Visible = !viaOllama;
            Arrange(true);
        }

        internal void ShowProgress(int step, string text, long done, long total)
        {
            Step(1, step > 1 ? "✓" : "2", stepText[1], step == 1 ? Look.Cyan : Look.Ink);
            Step(2, "3", stepText[2], step == 2 ? Look.Cyan : Look.Dim);
            brainName.Text = step == 1 ? "Setting up her brain" : "Waking her up";
            brainNote.Text = total > 0 ? text + " · " + Engine.Amount(done) + " of " + Engine.Amount(total) : text;
            filled = total > 0 ? Math.Min(1f, done / (float)total) : step == 2 ? 1f : 0f;
            setup.Text = step == 1 ? "Cancel" : "Starting...";
            setup.Enabled = step == 1;
            hint.Visible = false;
            if (!expanded) Arrange(true);
            else Invalidate(new Rectangle(0, Scaled(204), Width, Scaled(18)));
        }

        internal void ShowProblem(string problem) { brainNote.Text = problem; }

        internal void ShowInstall(string text, long done, long total)
        {
            bool opening = voiceFilled < 0;
            installNote.Text = total > 0 ? text + " · " + Engine.Amount(done) + " of " + Engine.Amount(total) : text + "...";
            voiceFilled = total > 0 ? Math.Min(1f, done / (float)total) : 0f;
            if (opening) Arrange(expanded);
            else Invalidate(new Rectangle(0, Scaled(InstallAt + 20 + (expanded ? SetupHeight : 0) + KeyRow - arrangedFold), Width, Scaled(14)));
        }

        internal void HideInstall()
        {
            if (voiceFilled < 0) return;
            voiceFilled = -1;
            installNote.Text = "";
            Arrange(expanded);
        }

        internal string InstallShown { get { return voiceFilled < 0 ? null : installNote.Text + " | " + (int)Math.Round(voiceFilled * 100) + "%"; } }

        internal int CapriRowTop { get { return showHide.Top; } }

        // OFF folds both rows under the VOICE rows away. FREE VOICE shows her free voice picker. ELEVENLABS shows the key box, then the Voice ID and model once a key is saved.
        private int Fold { get { return off ? 2 * KeyFold : eleven && keySaved ? 0 : KeyFold; } }

        private void VoiceTier()
        {
            off = capri.Silent;
            eleven = keySaved ? capri.Speaking : wantEleven;
            voiceRungs[0].Checked = off;
            voiceRungs[1].Checked = !off && !eleven;
            voiceRungs[2].Checked = !off && eleven;
            keyEntry.Visible = eleven && !off;
            connect.Visible = eleven && !off;
            voiceEntry.Visible = eleven && keySaved && !off;
            speechModels.Visible = eleven && keySaved && !off;
            freeVoices.Visible = !eleven && !off;
            freeSetup.Visible = !eleven && !off && !freeReady;
            if (Fold != arrangedFold) Arrange(expanded);
        }

        internal void PickVoice(bool paid)
        {
            wantEleven = paid;
            capri.ChooseVoiceTier(paid);
            if (paid && !keySaved) keyEntry.Box.Focus();
        }

        internal void PickSilent()
        {
            capri.Silence();
        }

        internal void ShowVoice(string tail, string voiceId, string model, VoiceListing listing)
        {
            keySaved = tail != null;
            keyEntry.Cue = keySaved ? "Key saved, ends " + tail : "Paste ElevenLabs API key";
            Relabel();
            VoiceTier();
            if (!voiceEntry.Box.Focused || voiceEntry.Box.Text.Trim().Length == 0) voiceEntry.Box.Text = voiceId;
            voiceMenu.Items.Clear();
            voiceEntry.Chevron = listing != null && listing.Voices.Count > 0;
            speechModelMenu.Items.Clear();
            speechModelMenu.Items.Add(Choice("Default model", string.IsNullOrEmpty(model), delegate { capri.ChooseVoiceModel(null); }));
            speechModels.Text = string.IsNullOrEmpty(model) ? "Default model" : model;
            if (listing != null)
            {
                foreach (VoiceEntry entry in listing.Voices)
                {
                    string id = entry.Id;
                    voiceMenu.Items.Add(Choice(entry.Name ?? id, id == voiceId, delegate { voiceEntry.Box.Text = id; capri.ChooseVoice(id); }));
                }
                foreach (VoiceModel option in listing.Models)
                {
                    string id = option.Id;
                    string name = option.Name ?? id;
                    if (id == model) speechModels.Text = name.StartsWith("Eleven ", StringComparison.OrdinalIgnoreCase) ? name.Substring(7) : name;
                    speechModelMenu.Items.Add(Choice(name, id == model, delegate { capri.ChooseVoiceModel(id); }));
                }
            }
            speechModels.Invalidate();
        }

        internal void ShowFree(bool ready, string chosen, bool busy)
        {
            freeReady = ready;
            freeVoices.Text = "Free voice: " + chosen;
            freeVoices.Width = Scaled(ready ? 328 : 196);
            freeSetup.Text = busy ? "Cancel" : "Get free voice";
            freeSetup.Visible = !eleven && !off && !ready;
            freeMenu.Items.Clear();
            foreach (FreeStyle style in LocalVoice.Styles)
            {
                string name = style.Name;
                freeMenu.Items.Add(Choice(name, name == chosen, delegate { capri.ChooseVoiceStyle(name); }));
            }
            freeVoices.Invalidate();
            freeSetup.Invalidate();
        }

        internal string FreeStatus { get { return (freeVoices.Visible ? freeVoices.Text : "-") + " | " + (freeSetup.Visible ? freeSetup.Text : "-"); } }

        internal void ShowShortcut(bool talk, bool mute)
        {
            shortcutReady = talk;
            muteReady = mute;
            KeyLabels();
        }

        internal void ShowKeys(bool type, bool hide)
        {
            typeReady = type;
            hideReady = hide;
            KeyLabels();
        }

        internal void Hotkeys(string talk, string mute, string type, string hide)
        {
            talkName = talk;
            muteName = mute;
            typeName = type;
            hideName = hide;
            KeyLabels();
        }

        // Which of her keys to change: 0 talk, 1 mute mic, 2 type to her, 3 hide or show her.
        // Opening another key while one is being picked drops what was pressed, so the old key stays.
        internal void PickKeys(int slot)
        {
            capturing = slot + 1;
            pendingMods = -1;
            heldMods = 0;
            if (keysNote != null) keysNote.Text = "";
            KeyLabels();
        }

        internal void TakeKeys(Keys keys) { TakeKeys(keys, false); }

        // Every press shows in her key's button until Confirm or Cancel is clicked. The latest key with what was held wins.
        internal void TakeKeys(Keys keys, bool win)
        {
            if (capturing == 0) return;
            Keys key = keys & Keys.KeyCode;
            int mods = Companion.HotMods(keys, win || key == Keys.LWin || key == Keys.RWin);
            if (key == Keys.ControlKey || key == Keys.LControlKey || key == Keys.RControlKey || key == Keys.Menu || key == Keys.LMenu || key == Keys.RMenu || key == Keys.ShiftKey || key == Keys.LShiftKey || key == Keys.RShiftKey || key == Keys.LWin || key == Keys.RWin)
            {
                // A modifier on its own only shows until a key joins it, and never counts as her key.
                if (pendingMods < 0) { heldMods = mods; KeyLabels(); }
                return;
            }
            if (!Companion.Bindable(mods, (int)key)) return;
            pendingMods = mods;
            pendingVk = (int)key;
            KeyLabels();
            string name = Companion.KeyName(pendingMods, pendingVk);
            KeysNote(Companion.Swallows(pendingMods, pendingVk) ? name + " won't type in other apps while she runs. Confirm to keep it." : "Confirm to keep " + name + ", or Cancel.", false);
        }

        internal void ConfirmKeys()
        {
            if (capturing == 0 || pendingMods < 0) return;
            int slot = capturing - 1, mods = pendingMods, vk = pendingVk;
            string problem = capri.SetHotkey(slot, mods, vk);
            // A key that's taken or already hers keeps the picking open, so another can be pressed straight away.
            if (problem != null) { KeysNote(problem, true); return; }
            capturing = 0;
            pendingMods = -1;
            KeyLabels();
            KeysNote("Saved. " + (slot == 0 ? talkName + " starts voice chat." : slot == 1 ? muteName + " mutes your mic." : slot == 2 ? typeName + " opens her typing box." : hideName + " hides or shows her.") + (Companion.Swallows(mods, vk) ? " It won't type in other apps while she runs." : ""), false);
        }

        internal void CancelKeys()
        {
            if (capturing == 0) return;
            capturing = 0;
            pendingMods = -1;
            if (keysNote != null) keysNote.Text = "";
            KeyLabels();
        }

        internal bool ConfirmReady { get { return confirmKeys.Visible && confirmKeys.Enabled; } }

        private Control PickedKey { get { return capturing == 1 ? talkKeys : capturing == 2 ? muteKeys : capturing == 3 ? typeKeys : capturing == 4 ? hideKeys : null; } }

        private static bool WinHeld() { return (Native.GetKeyState(0x5B) & 0x8000) != 0 || (Native.GetKeyState(0x5C) & 0x8000) != 0; }

        // A key change answers where it was made: under the KEYS row in Settings, or under VOICE on the main page.
        private void KeysNote(string text, bool problem)
        {
            if (!paged) { VoiceNote(text, problem); return; }
            keysNote.Text = text;
            keysNote.ForeColor = problem ? Look.Red : Look.Dim;
        }

        internal string KeysNoteText { get { return keysNote.Text; } }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (capturing == 0) return base.ProcessCmdKey(ref msg, keyData);
            TakeKeys(keyData, WinHeld());
            return true;
        }

        private const string Waiting = "Press new keys, then Confirm";

        private void KeyLabels()
        {
            shortcut.Text = capturing == 1 ? Waiting : talkName + (shortcutReady ? " to talk" : " is taken");
            muteShortcut.Text = capturing == 2 ? Waiting : muteName + (muteReady ? " mutes mic" : " is taken");
            if (cancelKeys == null) return;
            talkKeys.Text = capturing == 1 ? PickingText("Talk: ") : "Talk: " + talkName + (shortcutReady ? "" : " (taken)");
            muteKeys.Text = capturing == 2 ? PickingText("Mute mic: ") : "Mute mic: " + muteName + (muteReady ? "" : " (taken)");
            typeKeys.Text = capturing == 3 ? PickingText("Type: ") : "Type: " + typeName + (typeReady ? "" : " (taken)");
            hideKeys.Text = capturing == 4 ? PickingText("Hide/show: ") : "Hide/show: " + hideName + (hideReady ? "" : " (taken)");
            confirmKeys.Visible = cancelKeys.Visible = capturing != 0;
            confirmKeys.Enabled = pendingMods >= 0;
            confirmKeys.Invalidate();
        }

        // What the key being picked shows: the keys pressed so far, the modifiers held so far, or what to do.
        private string PickingText(string job)
        {
            if (pendingMods >= 0) return job + Companion.KeyName(pendingMods, pendingVk);
            if (heldMods != 0) return job + Companion.ModNames(heldMods) + "...";
            return Waiting;
        }

        internal string TierNote { get { return tierNote.Text; } }
        internal string KeyStatus { get { return shortcut.Text + " | " + muteShortcut.Text; } }
        internal string KeysShown { get { return talkKeys.Text + " | " + muteKeys.Text + " | " + typeKeys.Text + " | " + hideKeys.Text; } }

        internal void ShowMic(string name)
        {
            mics.Text = "Mic: " + (name ?? "default");
            mics.Invalidate();
        }

        private void ShowMics()
        {
            string chosen = capri.MicId;
            List<Mic> found = Ears.List();
            micMenu.Items.Clear();
            micMenu.Items.Add(Choice("Windows default", string.IsNullOrEmpty(chosen), delegate { capri.ChooseMic(null); }));
            if (found.Count > 0) micMenu.Items.Add(new ToolStripSeparator());
            foreach (Mic mic in found)
            {
                string id = mic.Id;
                micMenu.Items.Add(Choice(mic.Name, id == chosen, delegate { capri.ChooseMic(id); }));
            }
            micMenu.Show(mics, new Point(0, mics.Height));
        }

        internal string MicStatus { get { return "mic | " + mics.Text + " | " + (keyHelp.Visible ? "help" : "-") + " | " + (shortcut.Visible ? (shortcutReady ? "keys" : "taken") : "-") + " | " + (muteShortcut.Visible ? (muteReady ? "mute" : "taken") : "-"); } }

        internal void VoiceNote(string text, bool problem)
        {
            voiceNote.Text = text;
            voiceNote.ForeColor = problem ? Look.Red : Look.Dim;
        }

        internal string VoiceNoteText { get { return voiceNote.Text; } }

        internal void UpdateNote(string text, bool problem)
        {
            updateNote.Text = text;
            updateNote.ForeColor = problem ? Look.Red : Look.Dim;
        }

        internal string UpdateStatus { get { return updateNote.Text; } }

        internal string VoiceStatus { get { return voiceNote.Text + " | " + voiceEntry.Box.Text + " | " + speechModels.Text + " | " + connect.Text; } }
        private void Connect()
        {
            string typed = keyEntry.Box.Text.Trim();
            if (typed.Length > 0) { keyEntry.Box.Text = ""; capri.ConnectVoice(typed); }
            else if (keySaved) capri.ForgetVoice();
            else keyEntry.Box.Focus();
        }

        private void Relabel()
        {
            bool forget = keySaved && keyEntry.Box.Text.Trim().Length == 0;
            connect.Text = forget ? "Forget" : "Connect";
            connect.Accent = forget ? Look.Red : Look.Ink;
            connect.Invalidate();
        }

        private static ToolStripMenuItem Choice(string text, bool chosen, EventHandler click)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text, null, click);
            item.Checked = chosen;
            return item;
        }

        private ContextMenuStrip Dropdown()
        {
            ContextMenuStrip strip = new ContextMenuStrip();
            strip.Renderer = new ToolStripProfessionalRenderer(new CapriMenuColors());
            strip.BackColor = Look.Panel;
            strip.ForeColor = Look.Ink;
            strip.Font = Font;
            strip.ShowImageMargin = false;
            strip.ShowCheckMargin = true;
            return strip;
        }

        private Entry Input(int x, int y, int width)
        {
            Entry entry = new Entry();
            entry.Bounds = new Rectangle(x, y, width, 28);
            Controls.Add(entry);
            return entry;
        }

        private void Step(int index, string mark, string text, Color colour)
        {
            stepText[index] = text;
            steps[index].Text = mark + "  " + text;
            steps[index].ForeColor = colour;
        }

        private Label Caption(string text, int x, int y, Font font, Color colour)
        {
            Label label = new Note();
            label.Text = text;
            label.Font = font;
            label.ForeColor = colour;
            label.BackColor = Look.Panel;
            label.AutoSize = true;
            label.Location = new Point(x, y);
            Controls.Add(label);
            return label;
        }

        private Key Make(string text, int x, int y, int width, int height, EventHandler click)
        {
            Key key = new Key();
            key.Text = text;
            key.Bounds = new Rectangle(x, y, width, height);
            key.Click += click;
            Controls.Add(key);
            return key;
        }

        private Toggle Check(string text, int x, int y, int width, EventHandler changed)
        {
            Toggle box = new Toggle();
            box.Text = text;
            box.Bounds = new Rectangle(x, y, width, 22);
            box.CheckedChanged += changed;
            Controls.Add(box);
            return box;
        }

        private sealed class Note : Label
        {
            internal Note() { SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true); }

            protected override void OnPaintBackground(PaintEventArgs e) { Backdrop(e.Graphics, this, ((Mainframe)Parent).DeviceScale()); }

            protected override void WndProc(ref Message message)
            {
                if (message.Msg == 0x0084) { message.Result = new IntPtr(-1); return; }
                base.WndProc(ref message);
            }
        }

        private sealed class Key : Button
        {
            internal Color Accent = Look.Ink;
            internal bool Loud;
            internal bool Picked;
            internal bool Chevron;
            internal int Glyph;
            private bool hot;
            private bool down;

            internal Key()
            {
                SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
                BackColor = Look.Panel;
                Cursor = Cursors.Hand;
            }

            protected override void OnMouseEnter(EventArgs e) { hot = true; Invalidate(); base.OnMouseEnter(e); }

            protected override void OnMouseLeave(EventArgs e) { hot = false; down = false; Invalidate(); base.OnMouseLeave(e); }

            protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { down = true; Invalidate(); } base.OnMouseDown(e); }

            protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }

            protected override void OnPaint(PaintEventArgs e)
            {
                float s = ((Mainframe)Parent).DeviceScale();
                Graphics graphics = e.Graphics;
                Backdrop(graphics, this, s);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                bool lit = hot || Picked || (Focused && ShowFocusCues);
                Color hotColour = Accent == Look.Ink ? Look.Cyan : Accent;
                if (Glyph == 0 || hot)
                {
                    using (GraphicsPath plate = Look.Plate(new RectangleF(0.7f * s, 0.7f * s, Width - 1.4f * s - 1, Height - 1.4f * s - 1), 6 * s, -1, Height / 2f, 0, true))
                    {
                        using (Brush fill = new SolidBrush(down ? Pressed : hot ? Hover : Field)) graphics.FillPath(fill, plate);
                        if (Glyph == 0)
                        {
                            Color edge = !Enabled ? Field : lit ? hotColour : Loud ? Color.FromArgb(170, Accent) : Edge;
                            using (Pen pen = new Pen(edge, 1.4f * s)) graphics.DrawPath(pen, plate);
                        }
                    }
                }
                Color ink = !Enabled ? Look.Dim : Picked ? Look.Cyan : Accent;
                float x = Width / 2f;
                float y = Height / 2f;
                if (Glyph != 0)
                {
                    using (Pen pen = new Pen(hot ? (Glyph == 2 ? Look.Red : Look.Cyan) : Look.Dim, 1.5f * s))
                    {
                        if (Glyph == 1) graphics.DrawLine(pen, x - 5 * s, y, x + 5 * s, y);
                        else
                        {
                            graphics.DrawLine(pen, x - 4.5f * s, y - 4.5f * s, x + 4.5f * s, y + 4.5f * s);
                            graphics.DrawLine(pen, x - 4.5f * s, y + 4.5f * s, x + 4.5f * s, y - 4.5f * s);
                        }
                    }
                    return;
                }
                if (Chevron)
                {
                    float left = Width - 18 * s;
                    using (Brush mark = new SolidBrush(Look.Dim)) graphics.FillPolygon(mark, new[] { new PointF(left, y - 2 * s), new PointF(left + 8 * s, y - 2 * s), new PointF(left + 4 * s, y + 3 * s) });
                }
                Rectangle area = Chevron ? new Rectangle((int)(10 * s), 0, Width - (int)(30 * s), Height) : ClientRectangle;
                TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis | (Chevron ? TextFormatFlags.Left : TextFormatFlags.HorizontalCenter);
                TextRenderer.DrawText(graphics, Text, Font, area, ink, flags);
            }
        }

        private sealed class Entry : Control
        {
            internal readonly TextBox Box = new TextBox();
            internal event EventHandler Drop;
            private bool chevron;
            private string cue = "";
            private bool hot;

            internal Entry()
            {
                SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                BackColor = Look.Panel;
                Cursor = Cursors.IBeam;
                Box.BorderStyle = BorderStyle.None;
                Box.BackColor = Field;
                Box.ForeColor = Look.Ink;
                Box.GotFocus += delegate { Invalidate(); };
                Box.LostFocus += delegate { Invalidate(); };
                Box.HandleCreated += delegate { ShowCue(); };
                Controls.Add(Box);
            }

            internal string Cue { get { return cue; } set { cue = value ?? ""; ShowCue(); } }

            internal bool Chevron { get { return chevron; } set { chevron = value; PerformLayout(); Invalidate(); } }

            private float Unit()
            {
                Mainframe owner = Parent as Mainframe;
                return owner == null ? 1f : owner.DeviceScale();
            }

            private bool OnChevron(int x) { return Chevron && x >= Width - 28 * Unit(); }

            private void ShowCue() { if (Box.IsHandleCreated) Native.SendMessage(Box.Handle, 0x1501, new IntPtr(1), cue); }

            protected override void OnLayout(LayoutEventArgs e)
            {
                base.OnLayout(e);
                float s = Unit();
                int left = (int)(10 * s);
                int right = (int)((Chevron ? 28 : 10) * s);
                int height = Box.PreferredHeight;
                Box.SetBounds(left, (Height - height) / 2, Math.Max(1, Width - left - right), height);
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                bool over = OnChevron(e.X);
                Cursor = over ? Cursors.Hand : Cursors.IBeam;
                if (over != hot) { hot = over; Invalidate(); }
                base.OnMouseMove(e);
            }

            protected override void OnMouseLeave(EventArgs e) { hot = false; Invalidate(); base.OnMouseLeave(e); }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left)
                {
                    if (!OnChevron(e.X)) Box.Focus();
                    else if (Drop != null) Drop(this, EventArgs.Empty);
                }
                base.OnMouseDown(e);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                float s = Unit();
                Graphics graphics = e.Graphics;
                Backdrop(graphics, this, s);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath plate = Look.Plate(new RectangleF(0.7f * s, 0.7f * s, Width - 1.4f * s - 1, Height - 1.4f * s - 1), 6 * s, -1, Height / 2f, 0, true))
                {
                    using (Brush fill = new SolidBrush(Field)) graphics.FillPath(fill, plate);
                    using (Pen pen = new Pen(Box.Focused ? Look.Cyan : Edge, 1.4f * s)) graphics.DrawPath(pen, plate);
                }
                if (!Chevron) return;
                float left = Width - 18 * s;
                float y = Height / 2f;
                using (Brush mark = new SolidBrush(hot ? Look.Cyan : Look.Dim)) graphics.FillPolygon(mark, new[] { new PointF(left, y - 2 * s), new PointF(left + 8 * s, y - 2 * s), new PointF(left + 4 * s, y + 3 * s) });
            }
        }

        private sealed class Slider : Control
        {
            private readonly string[] labels;
            private int value;
            private bool hot;
            private bool sliding;
            internal event EventHandler Changed;

            internal Slider(string[] labels)
            {
                this.labels = labels;
                SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.Selectable, true);
                BackColor = Look.Panel;
                Cursor = Cursors.Hand;
                TabStop = true;
                AccessibleRole = AccessibleRole.Slider;
            }

            internal int Value
            {
                get { return value; }
                set
                {
                    int next = Math.Max(0, Math.Min(labels.Length - 1, value));
                    if (next == this.value) return;
                    this.value = next;
                    AccessibleDescription = labels[next];
                    Invalidate();
                }
            }

            private void Pick(int next)
            {
                int before = value;
                Value = next;
                if (value != before && Changed != null) Changed(this, EventArgs.Empty);
            }

            private float Unit() { return ((Mainframe)Parent).DeviceScale(); }

            private int At(int x)
            {
                float s = Unit();
                float start = 8 * s;
                float span = Width - 16 * s;
                return (int)Math.Round((x - start) / span * (labels.Length - 1));
            }

            protected override void OnMouseEnter(EventArgs e) { hot = true; Invalidate(); base.OnMouseEnter(e); }

            protected override void OnMouseLeave(EventArgs e) { hot = false; Invalidate(); base.OnMouseLeave(e); }

            protected override void OnMouseDown(MouseEventArgs e) { Focus(); sliding = true; Pick(At(e.X)); base.OnMouseDown(e); }

            protected override void OnMouseMove(MouseEventArgs e) { if (sliding) Pick(At(e.X)); base.OnMouseMove(e); }

            protected override void OnMouseUp(MouseEventArgs e) { sliding = false; base.OnMouseUp(e); }

            protected override bool IsInputKey(Keys key) { return key == Keys.Left || key == Keys.Right || base.IsInputKey(key); }

            protected override void OnKeyDown(KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Left) Pick(value - 1);
                else if (e.KeyCode == Keys.Right) Pick(value + 1);
                base.OnKeyDown(e);
            }

            protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }

            protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

            protected override void OnPaint(PaintEventArgs e)
            {
                float s = Unit();
                Graphics graphics = e.Graphics;
                Backdrop(graphics, this, s);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                float start = 8 * s;
                float span = Width - 16 * s;
                float middle = Height / 2f;
                float thumb = start + span * value / (labels.Length - 1);
                using (Brush track = new SolidBrush(Track)) graphics.FillRectangle(track, start, middle - 2 * s, span, 4 * s);
                using (Brush lit = new SolidBrush(Look.Cyan)) graphics.FillRectangle(lit, start, middle - 2 * s, thumb - start, 4 * s);
                using (Brush tick = new SolidBrush(Edge))
                    for (int i = 0; i < labels.Length; i++) graphics.FillRectangle(tick, start + span * i / (labels.Length - 1) - 0.5f * s, middle + 5 * s, 1.5f * s, 4 * s);
                RectangleF knob = new RectangleF(thumb - 5 * s, middle - 8 * s, 10 * s, 16 * s);
                using (Brush fill = new SolidBrush(Field)) graphics.FillRectangle(fill, knob);
                using (Pen edge = new Pen(hot || Focused || sliding ? Look.Cyan : Look.Ink, 1.6f * s)) graphics.DrawRectangle(edge, knob.X, knob.Y, knob.Width, knob.Height);
            }
        }

        private sealed class Rung : RadioButton
        {
            private bool hot;
            internal string Hint;
            internal Font HintFont;
            internal string Flag;
            internal Font FlagFont;

            internal Rung()
            {
                SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
                BackColor = Look.Panel;
                Cursor = Cursors.Hand;
            }

            protected override void OnMouseEnter(EventArgs e) { hot = true; Invalidate(); base.OnMouseEnter(e); }

            protected override void OnMouseLeave(EventArgs e) { hot = false; Invalidate(); base.OnMouseLeave(e); }

            // A radio button clicks itself when focus arrives by arrow key or from code. The VOICE pair picks on a real click or Space only.
            protected override void OnEnter(EventArgs e) { if (AutoCheck) base.OnEnter(e); }

            protected override void OnPaint(PaintEventArgs e)
            {
                float s = ((Mainframe)Parent).DeviceScale();
                Graphics graphics = e.Graphics;
                Backdrop(graphics, this, s);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                float side = 13 * s;
                RectangleF dot = new RectangleF(s, (Height - side) / 2, side, side);
                using (Brush fill = new SolidBrush(Field)) graphics.FillEllipse(fill, dot);
                using (Pen edge = new Pen(hot || (Focused && ShowFocusCues) ? Look.Cyan : Edge, 1.4f * s)) graphics.DrawEllipse(edge, dot);
                if (Checked) using (Brush mark = new SolidBrush(Look.Cyan)) graphics.FillEllipse(mark, dot.X + 3.5f * s, dot.Y + 3.5f * s, dot.Width - 7 * s, dot.Height - 7 * s);
                int left = (int)(dot.Right + 9 * s);
                TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix;
                TextRenderer.DrawText(graphics, Text, Font, new Rectangle(left, 0, Width - left, Height), Checked ? Look.Cyan : Look.Ink, flags);
                int after = left + TextRenderer.MeasureText(graphics, Text, Font, Size.Empty, flags).Width + (int)(10 * s);
                if (Hint != null) TextRenderer.DrawText(graphics, Hint, HintFont ?? Font, new Rectangle(after, 0, Math.Max(0, Width - after), Height), Look.Dim, flags);
                if (Flag != null) TextRenderer.DrawText(graphics, Flag, FlagFont ?? Font, ClientRectangle, Look.Yellow, TextFormatFlags.VerticalCenter | TextFormatFlags.Right | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            }
        }

        private sealed class Toggle : CheckBox
        {
            private bool hot;
            internal string Hint;
            internal Font HintFont;

            internal Toggle()
            {
                SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
                BackColor = Look.Panel;
                Cursor = Cursors.Hand;
            }

            protected override void OnMouseEnter(EventArgs e) { hot = true; Invalidate(); base.OnMouseEnter(e); }

            protected override void OnMouseLeave(EventArgs e) { hot = false; Invalidate(); base.OnMouseLeave(e); }

            protected override void OnPaint(PaintEventArgs e)
            {
                float s = ((Mainframe)Parent).DeviceScale();
                Graphics graphics = e.Graphics;
                Backdrop(graphics, this, s);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                float side = 13 * s;
                RectangleF box = new RectangleF(s, (Height - side) / 2, side, side);
                using (Brush fill = new SolidBrush(Field)) graphics.FillRectangle(fill, box);
                using (Pen edge = new Pen(hot || (Focused && ShowFocusCues) ? Look.Cyan : Edge, 1.4f * s)) graphics.DrawRectangle(edge, box.X, box.Y, box.Width, box.Height);
                if (Checked) using (Brush mark = new SolidBrush(Look.Cyan)) graphics.FillRectangle(mark, box.X + 3.5f * s, box.Y + 3.5f * s, box.Width - 7 * s, box.Height - 7 * s);
                int left = (int)(box.Right + 9 * s);
                TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix;
                TextRenderer.DrawText(graphics, Text, Font, new Rectangle(left, 0, Width - left, Height), Look.Ink, flags);
                if (Hint == null) return;
                int after = left + TextRenderer.MeasureText(graphics, Text, Font, Size.Empty, flags).Width + (int)(14 * s);
                TextRenderer.DrawText(graphics, Hint, HintFont ?? Font, new Rectangle(after, 0, Math.Max(0, Width - after), Height), Look.Dim, flags);
            }
        }
    }
}
