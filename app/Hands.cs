using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
#if !CROSS
using System.Windows.Automation;
#endif

namespace CapriDesktop
{
    [DataContract]
    internal sealed class ToolArguments
    {
        [DataMember(Name = "name")] public string Name;
        [DataMember(Name = "site")] public string Site;
        [DataMember(Name = "search")] public string Search;
        [DataMember(Name = "action")] public string Action;
        [DataMember(Name = "focus")] public string Focus;
        [DataMember(Name = "song")] public string Song;
        [DataMember(Name = "app")] public string App;
        [DataMember(Name = "kind")] public string Kind;
        [DataMember(Name = "task")] public string Task;
        internal string Asked;
        [DataMember(Name = "target")] public string Target;
        [DataMember(Name = "text")] public string Text;
        [DataMember(Name = "keys")] public string Keys;
        [DataMember(Name = "button")] public string Button;
        [DataMember(Name = "enter")] public string Enter;
        [DataMember(Name = "direction")] public string Direction;
        [DataMember(Name = "seconds")] public string Seconds;
        [DataMember(Name = "minutes")] public string Minutes;
        [DataMember(Name = "level")] public string Level;
        [DataMember(Name = "coordinate")] public double[] Coordinate;
        [DataMember(Name = "start_coordinate")] public double[] From;
        [DataMember(Name = "region")] public double[] Region;
        [DataMember(Name = "scroll_direction")] public string Way;
        [DataMember(Name = "scroll_amount")] public double? Amount;
        [DataMember(Name = "duration")] public double? Duration;
        [DataMember(Name = "repeat")] public double? Repeat;

        internal ToolArguments Copy() { return (ToolArguments)MemberwiseClone(); }
    }

    internal sealed class InstalledApp
    {
        internal readonly string Name;
        internal readonly string Id;
        internal InstalledApp(string name, string id) { Name = name; Id = id; }
    }

    internal sealed class OpenWindow
    {
        internal readonly IntPtr Handle;
        internal readonly string Title;
        internal readonly string Program;
        internal OpenWindow(IntPtr handle, string title, string program) { Handle = handle; Title = title; Program = program; }
    }

    internal static class Hands
    {
        internal const string Note =
            "\nYou have tools for their PC: open (launch, play) their apps and games, open their folders in File Explorer, switch to, minimize or close apps that are open, play songs or videos they name, open websites or search them, control their music and volume, look at their screen, and use their mouse and keyboard for jobs inside apps and sites. " +
            "Use a tool whenever they ask you to do something like that, and never say you did or are doing it without one. Look at their screen when they ask what you see or what you think of something on it. Only close an app when they ask you to. " +
            "Your own website, where people download you, is " + Home + ". " +
            "Harmless jobs like opening apps, playing music or searching, do straight away, and when they leave the choice to you, like which song, video or game, pick one yourself. " +
            "Before anything that sends, posts, buys, pays, deletes, installs, ends a call or changes their account, ask them first and wait for a clear yes; read a message back before you send it. Never type passwords or card numbers. Mute my mic means mute their mic, not deafen them. Never say you can't do something your tools can do. " +
            "You can press shortcuts: mute or deafen them in Discord, screenshots, clips, recording, Game Bar, show desktop, Task Manager, clipboard history and emoji. " +
            "You can shut down, restart, sleep, lock or sign out of their PC with power: when they ask, check once that they're sure first. " +
            "When they ask you to remember or forget something about them, or to leave or make a memory, use remember: it saves to your notes, me.md, which you read every chat. Never say you can't keep memories. " +
            "You have a web browser: when they ask something you don't know or that changes, like the weather, news, scores, prices or times, search Google for it instead of guessing or saying you can't. " +
            "Most messages are just chat, not jobs: answer those as yourself, with your own opinions and tastes, and don't offer to open, play or look at anything unless they ask.";

        internal const string Prompt =
            "You are the hands of Capri, a desktop companion on the user's Windows PC. You don't chat.\n" +
            "Act only on their latest request. Earlier messages are context for names, not commands to repeat. Call one tool for each thing they ask for, however many there are (open Discord, Spotify and Steam is three open_app calls): open an app, game or website, open a folder with open_folder, search a site, play music, control media or volume, check the current song, switch to, minimize or close an app, or look at their screen when asked about it. Use the names they gave.\n" +
            "If the job needs clicking, typing or more than one step, like opening an app and then searching, going somewhere or making something in it, call use_computer once with the whole job in their words.\n" +
            "Sending a message, DM, reply or post, joining, leaving or ending a call, and any other job inside an app are use_computer jobs: pass their exact words, including what to send. They asked, so it's a job even when it sends, deletes or ends something.\n" +
            "When they name an app, never guess a website for it: use open_app, or use_computer if there's more to do.\n" +
            "If they leave the pick to you, like a song or a video, pick one yourself that fits.\n" +
            "Your own website is " + Home + ": when they ask for your website or page, call open_website with that address.\n" +
            "If they ask something you'd have to look up, like the weather, news, scores, prices, times or facts you're unsure of, call open_website with site google and their question as the search.\n" +
            "To mute, unmute or turn off their mic, call shortcut with discord_mute; to deafen or undeafen them, discord_deafen. Don't ask which: mute means the mic. Screenshots, clips, recording, Game Bar, show desktop, Task Manager, clipboard and emoji are shortcuts too.\n" +
            "To shut down, turn off, restart, sleep, lock or sign out of their PC, call power. They asked, so do it: never ask if they're sure.\n" +
            "Never reply with a question about whether to do what they asked. Call the tool.\n" +
            "If they ask you to remember, note down or forget something about them, or to leave, make or save a memory, call remember with a short note about them in the third person.\n" +
            "Questions about their current screen or song need a tool too. If the target is unclear, pick the closest match yourself. For chat, negated or hypothetical requests, call no tool and reply with only the word: chat";

        internal const string Chat =
            "\nNo tools ran for this message. Answer as yourself, with your own opinions and tastes. Don't claim you opened, played or looked at anything. You keep notes about them in me.md, saved whenever they ask you to remember something, so never say you can't remember things or keep memories; if they asked you to and nothing was saved, ask what they want you to remember. You can search Google for them: if they ask something you don't know or that changes, like the weather, news, scores or prices, say you'll look it up instead of guessing or saying you can't. If they asked you to do something on their PC, like send a message or end a call, never ask whether to: say On it and what you're doing, like On it, sending it now.";

        private static readonly string[,] Specs =
        {
            { "open_app", "Open, launch or play an app or game installed on their PC, like Spotify, Discord, Steam or Roblox.",
              "{\"type\":\"object\",\"properties\":{\"name\":{\"type\":\"string\",\"description\":\"The app's name\"}},\"required\":[\"name\"]}" },
            { "open_folder", "Open one of their folders in File Explorer, like Documents, Downloads, Desktop, Pictures, or a folder by its name. It only opens it: nothing is moved or deleted.",
              "{\"type\":\"object\",\"properties\":{\"name\":{\"type\":\"string\",\"description\":\"The folder's name, like Downloads or capri-desktop, or its full path\"}},\"required\":[\"name\"]}" },
            { "open_website", "Open a website in their browser, or search a site like YouTube or Google.",
              "{\"type\":\"object\",\"properties\":{\"site\":{\"type\":\"string\",\"description\":\"The site's name or address, like youtube.com\"},\"search\":{\"type\":\"string\",\"description\":\"Words to search for on that site, if any\"}},\"required\":[\"site\"]}" },
            { "media", "Control the music or video playing on their PC, the PC's volume (up, down, set to a level, mute or unmute), or check what song is playing.",
              "{\"type\":\"object\",\"properties\":{\"action\":{\"type\":\"string\",\"description\":\"What to do\",\"enum\":[\"play_pause\",\"next\",\"previous\",\"volume_up\",\"volume_down\",\"volume_set\",\"mute\",\"unmute\",\"now_playing\"]},\"level\":{\"type\":\"string\",\"description\":\"For volume_set, the volume from 0 to 100. For volume_up or volume_down, how many percent to change it, only if they said\"}},\"required\":[\"action\"]}" },
            { "look_at_screen", "Take a screenshot of their screen and look at it right now: what they're doing, playing or showing you. Use it whenever they ask you to check, look at or see their screen, or ask what something on it is. You can always look, so never say you can't see their screen.",
              "{\"type\":\"object\",\"properties\":{\"focus\":{\"type\":\"string\",\"description\":\"What to look at, if they said\"}}}" },
            { "play_music", "Play a song, playlist, album or artist they name, on Spotify or YouTube. For their liked songs, use kind playlist and song Liked Songs. If they leave the pick to you, pick a track from the Cyberpunk 2077 or Edgerunners soundtrack.",
              "{\"type\":\"object\",\"properties\":{\"song\":{\"type\":\"string\",\"description\":\"What to play, like a song and its artist or a playlist's name\"},\"kind\":{\"type\":\"string\",\"description\":\"What it is\",\"enum\":[\"song\",\"playlist\",\"album\",\"artist\"]},\"app\":{\"type\":\"string\",\"description\":\"Where to play it, if they said\",\"enum\":[\"spotify\",\"youtube\"]}},\"required\":[\"song\"]}" },
            { "app_window", "Switch to, minimize or close an app or game that's open on their PC. Naming a site closes that browser tab.",
              "{\"type\":\"object\",\"properties\":{\"name\":{\"type\":\"string\",\"description\":\"The app's name, or the site in a browser tab\"},\"action\":{\"type\":\"string\",\"description\":\"What to do\",\"enum\":[\"switch_to\",\"minimize\",\"close\"]}},\"required\":[\"name\",\"action\"]}" },
            { "use_computer", "Use their mouse and keyboard for any job inside an app or website: sending a message or DM, joining, leaving or ending a call, or anything that needs clicking, typing or several steps, like opening an app and searching in it.",
              "{\"type\":\"object\",\"properties\":{\"task\":{\"type\":\"string\",\"description\":\"The whole job in their exact words, including any message to send\"}},\"required\":[\"task\"]}" },
            { "shortcut", "Press a keyboard shortcut for them: mute or unmute their mic in Discord (discord_mute), deafen or undeafen in Discord (discord_deafen), take a screenshot, clip the last 30 seconds, start or stop recording, open the Game Bar, show the desktop, open Task Manager, clipboard history or the emoji picker.",
              "{\"type\":\"object\",\"properties\":{\"name\":{\"type\":\"string\",\"description\":\"Which shortcut\",\"enum\":[\"discord_mute\",\"discord_deafen\",\"screenshot\",\"clip_that\",\"record\",\"game_bar\",\"show_desktop\",\"task_manager\",\"clipboard\",\"emoji\"]}},\"required\":[\"name\"]}" },
            { "remember", "Save a short note about them to your notes (me.md) so you remember it next time, like a favourite game or what they're making, or forget a note. Use it whenever they ask you to remember, note down, save or forget something, or to leave or make a memory.",
              "{\"type\":\"object\",\"properties\":{\"text\":{\"type\":\"string\",\"description\":\"The note, about them in the third person, or what to forget\"},\"action\":{\"type\":\"string\",\"description\":\"What to do\",\"enum\":[\"remember\",\"forget\"]}},\"required\":[\"text\"]}" },
            { "power", "Shut down, restart, sleep, lock or sign out of their PC, or cancel a shutdown. Do it as soon as they ask, without asking if they're sure.",
              "{\"type\":\"object\",\"properties\":{\"action\":{\"type\":\"string\",\"description\":\"What to do\",\"enum\":[\"shut_down\",\"restart\",\"sleep\",\"lock\",\"sign_out\",\"cancel\"]},\"minutes\":{\"type\":\"string\",\"description\":\"How many minutes from now, only if they said when\"}},\"required\":[\"action\"]}" }
        };

        internal static readonly string Tools = Render(false);
        internal static readonly string ClaudeTools = Render(true);
        internal static readonly string Listing = Written();
        private const string LookupWords = @"^(what('?s| is| are)|how('?s| is)|is it|will it|when('?s| is| does| do)|who won|did|check|get|tell me|show me|find)\b.*\b(weather|forecast|temperature|raining|snowing|news|headlines|scores?|stock price|price of|exchange rate|time (is it )?in|opening hours|open (today|now|tonight)|release date|showtimes|rain|snow|sunny|hot out|cold out)\b|^(weather|forecast|news|headlines)\b|^who (won|is winning)\b";

        private static readonly Regex Asks = new Regex(
            @"^(open|launch|start|run|load up|boot up|fire up|play|shuffle|put on|blast|queue|close|quit|exit|kill|shut down|switch (back |over )?to|back to|go to|bring up|pull up|show me|minimi[sz]e|pause|unpause|resume|stop the music|skip|next|previous|go back|volume|louder|quieter|mute|unmute|search|google|look up|mind (opening|launching|playing|closing))\b" +
            @"|^(turn|make) (it|this|that|the volume|the music|the sound) (up|down|louder|quieter)\b|^(turn (up|down)|lower|raise) (the )?(volume|music|sound)\b|^put (some |a )?(music|song|tunes) on\b|^(get|bring) .+ (going|up)$" +
            @"|^(look at|take a (look|peek)( at)?|check out|check|rate|watch) (this|that|it|my|the|what)\b" +
            @"|^(click|tap|double click|right click|type|press|scroll|go on|get on|hop on|sign in|log in|fill (in|out)|use (my|the) (mouse|keyboard|computer|pc))\b|^(use|make|create|write|draw|search|find) .+ (in|on|with|using|into) (the |my )?\w+" +
            @"|^(shut ?down|turn off|power (off|down)|switch off|restart|reboot|lock|sleep|hibernate|put|send) (my |the |this )?(pc|computer|laptop|system|machine|rig|windows|device|screen)\b|^(shut|turn|power|switch) (my |the |this )?(pc|computer|laptop|system|machine|rig|device) (down|off)\b|^(sign|log) me (out|off)$" +
            @"|^(send|dm|pm|message|text|ping|reply to|respond to|post|tweet)\b|^(tell|ask|say|let|invite|kick|ban|add|react|call|ring|share|pin|block|unblock|unfriend) .+\b(on|in|into|through|via|over) (the |my )?(discord|slack|teams|whatsapp|telegram|signal|messenger|skype|instagram|insta|snapchat|snap|reddit|twitter|tiktok|facebook|roblox|steam|chat|call|vc|server|channel|group chat|gc|dms?)\b" +
            @"|^(end|leave|hang up|disconnect( from)?|join|accept|decline|pick up|answer|reject|hop (in|on|off)|get off) (the |my |this |that |a |our |their )?(\w+('s)? )?(call|vc|voice (chat|channel|call)|video call|meeting|stream|server|channel)\b|^(hang up|disconnect|hop off|get off)( (the )?(call|vc))?$" +
            @"|^(see|tell me|show me) (what|which)\b|^see (this|that|my screen|the screen)\b|^what('?s| is) (on my screen|on the screen|playing|this song|this track|the song|on spotify)\b" +
            @"|^what ((song|track)( is|'s)? (this|playing|on)|am i (doing|playing|watching|listening)|are we (doing|playing|watching|listening)|do you see|can you see)\b|^what do you think (of|about) (this|that|it|my (build|skin|setup))\b|^do you (see|like) (this|that|my (build|skin|setup))\b" +
            @"|^(i (want|need|wanna|would like) (you to |to )?(open|launch|play|listen|watch|search|remember|forget|mute|unmute|deafen)|^(deafen|undeafen|keep in mind|make a note)|let's (play|listen|watch)|find (me )?.+ (on|online)|can (you|u) see)\b" +
            "|" + LookupWords +
            @"|^(ouvre|ouvrir|lancer?|d[ée]marrer?|fermer?|quitter?|r[ée]duis|r[ée]duire|minimiser?|chercher?|rechercher?|mets|mettre|jouer?|passer?|couper?|augmenter?|baisser?|regarder?|afficher?|montre-moi|va sur|aller sur|retourne sur)\b" +
            @"|^(tu vois|qu'est-ce que (tu vois|je fais)|c'est quoi (cette|la) (chanson|musique)|quelle (chanson|musique)|qu'est-ce qu'il y a sur mon [ée]cran)\b", RegexOptions.IgnoreCase);

        private static readonly Regex Errands = new Regex(
            @"^(send|dm|pm|reply to|respond to)\b(?!\s+(me|us|help)\b)|^(message|msg|text|ping) (?!(is|was|are|were|from|looks|said|says|me|us)\b)\w|^(post|tweet) (this|that|it|a|an|my|our|something|on|in|to)\b" +
            @"|^(tell|ask|say|let|invite|kick|ban|add|react|call|ring|share|pin|block|unblock|unfriend) .+\b(on|in|into|through|via|over|to) (the |my )?(#\S+|discord|slack|teams|whatsapp|telegram|signal|messenger|skype|instagram|insta|snapchat|snap|reddit|twitter|tiktok|facebook|roblox|steam|(group )?chat|call|vc|server|channel|gc|dms?)(?!\w)" +
            @"|^type (?!(of|is|was)\b)\w|^(click|tap|double click|right click) (?!(bait|is|was)\b)\w|^scroll (up|down|left|right|to|through)\b|^(fill (in|out)|sign in|log in)\b" +
            @"|^(end|leave|hang up|disconnect( from)?|join|accept|decline|pick up|answer|reject|hop (in|on|off)|get off) (the |my |this |that |a |our |their )?(\w+('s)? )?(call|vc|voice (chat|channel|call)|video call|meeting|stream|server|channel)\b|^(hang up|disconnect|hop off|get off)( (the )?(call|vc))?$", RegexOptions.IgnoreCase);
        private static readonly Regex Opener = new Regex(@"^((great|nice|cool|sweet|perfect|awesome|amazing|alright|alr|aight|bet|thanks|thank you|thx|ty|so|and|now|also|oh|wait|um|uh|hmm|lol|lmao|bro|dude|bruh)[\s,!.]+)+(?=((hey|yo|ok|okay)[\s,]+)?(capri\b|(can|could|would|will) (you|u)\b|please\b|pls\b|plz\b))", RegexOptions.IgnoreCase);

        internal static Func<List<InstalledApp>> Catalog = ReadCatalog;
        internal static Func<string, bool> Exists = File.Exists;
        internal static Action<string> Run = Start;
        internal static Action<byte> Press = Tap;
        internal static Func<string, string> Look = delegate { return "You can't see their screen right now."; };
        internal static Func<string, string> Fetch = Download;
        internal static Func<List<OpenWindow>> Windows = ReadWindows;
        internal static Func<OpenWindow, string, bool> Steer = Apply;
        internal static Func<string, bool> PressPlay = ClickPlay;
        internal static Func<string> NowPlaying = SpotifyTitle;
        internal static Func<string, string, string> Work = delegate { return "Using the mouse and keyboard isn't ready on this PC yet."; };
        internal static Func<string, string> Resume = delegate { return null; };
        // Puts dots over a password she typed into a password box, wherever it would show or be saved.
        internal static Func<string, string> Hush = delegate(string text) { return text; };
        internal static Action<byte[]> Chord = PressChord;
        internal static Func<string, int, bool> Power = Switch;
        private static readonly string[] Browsers = { "chrome", "msedge", "firefox", "brave", "opera", "vivaldi" };
        private static readonly string[] Shell = { "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd" };
        private delegate bool Visit(IntPtr window, IntPtr data);
        [DllImport("user32.dll")] private static extern bool EnumWindows(Visit visit, IntPtr data);
        [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr window);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
        [DllImport("user32.dll")] private static extern int GetWindowTextLength(IntPtr window);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr window, StringBuilder text, int size);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, StringBuilder name, int size);
        [DllImport("user32.dll")] private static extern int GetWindowThreadProcessId(IntPtr window, out int process);
        [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out int value, int size);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr window, int command);
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
        [DllImport("user32.dll")] private static extern bool BringWindowToTop(IntPtr window);
        [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] private static extern bool AttachThreadInput(int from, int to, bool attach);
        [DllImport("kernel32.dll")] private static extern int GetCurrentThreadId();
        private static readonly Regex Junk = new Regex(@"\b(uninstall\w*|desinstall\w*|readme|read me|lisez moi|help|aide|manual|manuel|documentation|release notes|notes de version|licen[cs]e|website|site web|support|assistance|changelog)\b", RegexOptions.IgnoreCase);
        private static readonly string[,] Searches =
        {
            { "youtube", "https://www.youtube.com/results?search_query=" },
            { "google", "https://www.google.com/search?q=" },
            { "twitch", "https://www.twitch.tv/search?term=" },
            { "reddit", "https://www.reddit.com/search/?q=" },
            { "wikipedia", "https://en.wikipedia.org/w/index.php?search=" },
            { "spotify", "https://open.spotify.com/search/" },
            { "roblox", "https://www.roblox.com/discover/?Keyword=" },
            { "github", "https://github.com/search?q=" }
        };
        private static readonly object gate = new object();
        private static List<InstalledApp> cached;
        private static DateTime cachedAt;

        internal static string Do(string tool, ToolArguments args)
        {
            if (args == null) args = new ToolArguments();
            try
            {
                if (tool == "open_app") return OpenApp(args.Name);
                if (tool == "open_folder") return OpenFolder(args.Name);
                if (tool == "open_website") return OpenSite(args.Site, args.Search);
                if (tool == "media") return Media(args);
                if (tool == "look_at_screen") return Look(args.Focus);
                if (tool == "play_music") return PlayMusic(args.Song, args.App, args.Kind);
                if (tool == "app_window") return Window(args.Name, args.Action);
                if (tool == "use_computer") return string.IsNullOrWhiteSpace(args.Task) ? "Which job, choom?" : Safety.Stops(args.Task) != null ? "Not doing that, choom." : Work(args.Task.Trim(), args.Asked);
                if (tool == "shortcut") return Shortcut(args.Name);
                if (tool == "remember") return args.Action == "forget" ? Brain.Unnote(Theirs(args.Text)) : CallMe(args.Text) ?? Brain.Note(Theirs(args.Text));
                if (tool == "power") return Turn(args.Action, args.Minutes);
                return "There's no tool called " + tool + ".";
            }
            catch (Exception error) { return "That didn't work: " + error.Message; }
        }

        internal static void Forget() { lock (gate) cached = null; }

        private static readonly string[] Swaps = { @"\bi'?m\b", "they're", @"\bi am\b", "they are", @"\bi was\b", "they were", @"\bi've\b", "they've", @"\bi'll\b", "they'll", @"\bi'd\b", "they'd", @"\bi\b", "they", @"\bmyself\b", "themselves", @"\bmy\b", "their", @"\bmine\b", "theirs", @"\bme\b", "them" };

        // "Remember my name is Sam" sets what she calls them, the same as the box in Settings, instead of a note.
        internal static Func<string, string> Rename;

        private static string CallMe(string text)
        {
            Match named = Regex.Match((text ?? "").Replace('’', '\'').Trim(), @"^(?:that\s+)?(?:my name is|my name's|call me|i'm called|i am called|i go by)\s+([\p{L}][\p{L}'-]*(?:\s[\p{L}][\p{L}'-]*)?)[.!]*$", RegexOptions.IgnoreCase);
            Func<string, string> rename = Rename;
            return named.Success && rename != null ? rename(named.Groups[1].Value) : null;
        }

        // Her asking what to remember, so their next message is the note.
        internal static bool AskedWhat(string said) { return Regex.IsMatch(said ?? "", @"\bwhat\b[^.!?]*\b(remember|note|memory|save|keep)\b[^.!?]*\?\s*$", RegexOptions.IgnoreCase); }

        // "Remember that" or "make a note of it" points back at what they just said.
        internal static bool Back(string said) { return Regex.IsMatch(Asked(said), @"\b(that|this|it)$", RegexOptions.IgnoreCase); }

        // Ways of asking her to save a note: remember, keep in mind, leave or make a memory, put it in your notes or me.md.
        private const string Memory = @"(?:leave|make|take|save|add|write|store|create|keep|drop) (?:a |an |one |this |that |the )?(?:new |quick |little )?(?:memory|memories|note|notes)(?: (?:in|to|into|on) (?:your |ur |the )?(?:notes|memory|memories|me\.?md|md)(?: file)?)?";
        private const string Remembering = @"^(?:(?:i (?:want|need|would like) (?:you|u) to|you (?:should|gotta|need to))\s+)?(?:remember|memori[sz]e|don't forget|dont forget|do not forget|keep in mind|note (?:down|that)|(?:write|jot|note|put) (?:it|that|this) down|" + Memory + @"(?: (?:that|saying|about|of))?|(?:save|put|add|write|store|log|jot|keep)(?: (?:it|that|this))?(?: down)? (?:to|in|into|on) (?:your |ur |the )?(?:notes|memory|memories|me\.?md|md|brain|head)(?: file)?)";

        internal static string Theirs(string text)
        {
            string note = Regex.Replace((text ?? "").Replace('’', '\'').Trim(), @"^(that|this)\s+", "", RegexOptions.IgnoreCase);
            for (int i = 0; i < Swaps.Length; i += 2) note = Regex.Replace(note, Swaps[i], Swaps[i + 1], RegexOptions.IgnoreCase);
            return note;
        }

        internal static int Count { get { return Specs.GetLength(0); } }

        internal static string Name(int i) { return Specs[i, 0]; }

        internal static string Description(int i) { return Specs[i, 1]; }

        internal static string ElevenLabsTool(int i) { return "{\"tool_config\":{\"type\":\"client\",\"name\":\"" + Specs[i, 0] + "\",\"description\":" + Voice.Quote(Specs[i, 1]) + ",\"parameters\":" + Specs[i, 2] + ",\"expects_response\":true,\"response_timeout_secs\":" + (Specs[i, 0] == "use_computer" ? 60 : 20) + "}}"; }

        private static string Render(bool claude)
        {
            List<string> tools = new List<string>();
            for (int i = 0; i < Specs.GetLength(0); i++)
                tools.Add(claude
                    ? "{\"name\":\"" + Specs[i, 0] + "\",\"description\":" + Voice.Quote(Specs[i, 1]) + ",\"input_schema\":" + Specs[i, 2] + "}"
                    : "{\"type\":\"function\",\"function\":{\"name\":\"" + Specs[i, 0] + "\",\"description\":" + Voice.Quote(Specs[i, 1]) + ",\"parameters\":" + Specs[i, 2] + "}}");
            return "[" + string.Join(",", tools.ToArray()) + "]";
        }

        private static string Written()
        {
            StringBuilder text = new StringBuilder("\nYour tools:");
            for (int i = 0; i < Specs.GetLength(0); i++)
            {
                List<string> keys = new List<string>();
                foreach (Match key in Regex.Matches(Specs[i, 2], @"""(\w+)"":\{""type"":""string"",""description"":""[^""]*""(?:,""enum"":\[([^\]]*)\])?"))
                    keys.Add("\"" + key.Groups[1].Value + "\": \"" + (key.Groups[2].Success ? key.Groups[2].Value.Replace("\"", "").Replace(",", "|") : "...") + "\"");
                text.Append("\n- " + Specs[i, 0] + "{" + string.Join(", ", keys.ToArray()) + "}: " + Specs[i, 1]);
            }
            return text.Append("\nChoose one value from each | choice and omit unused fields.\nTo use one, reply with only the call, like: open_app{\"name\": \"Spotify\"}").ToString();
        }

        internal const string SayPlease = "Say please.";
        private static readonly Regex Polite = new Regex(@"\b(please|pls|plz|pleas|thank you|thanks|por favor|porfa|porfis|gracias|merci|s'il (te|vous) pla[iî]t|stp|svp)\b", RegexOptions.IgnoreCase);
        private static readonly Regex JustPlease = new Regex(@"^\W*((ok|okay|fine|sorry|vale|perd[oó]n|d[ée]sol[ée])\W+)?(pretty\s+)?(please+|pls|plz|pleas+e*|por favor|porfa|porfis|s'il (te|vous) pla[iî]t|stp|svp)(\s+capri)?\W*$", RegexOptions.IgnoreCase);
        private static readonly Random Dice = new Random();
        internal static Func<int> PleaseRoll = delegate { lock (Dice) return Dice.Next(10); };
        private static string heldForPlease;

        internal static string Manners(string said, out bool askPlease)
        {
            askPlease = false;
            string held = heldForPlease;
            heldForPlease = null;
            if (held != null && JustPlease.IsMatch(said ?? "")) return held + " please";
            if (Polite.IsMatch(said ?? "") || !Wants(said) || PleaseRoll() != 0) return said;
            heldForPlease = said;
            askPlease = true;
            return said;
        }

        internal static bool Wants(string said)
        {
            if (Regex.IsMatch(said ?? "", @"\b(please|pls|plz|stp)\s*[.!?]*$", RegexOptions.IgnoreCase) && Site(Asked(said))) return true;
            foreach (string part in Regex.Split(said ?? "", @"[,;!?]\s*|\.\s+"))
            {
                string text = Asked(part);
                if (!Chatter.IsMatch(text) && Asks.IsMatch(text)) return true;
            }
            return false;
        }

        internal static void Rescue(string said, List<KeyValuePair<string, ToolArguments>> calls)
        {
            string text = Asked(said);
            if (calls.Exists(call => call.Key == "use_computer" || call.Key == "look_at_screen" || call.Key == "media" || call.Key == "play_music")) return;
            if (Wants(said) && ((Errand(said) && !calls.Exists(call => call.Key == "shortcut" || call.Key == "remember")) || Regex.IsMatch(text, @"\s(and|then|&)\s+(then\s+)?(\w+\s+)?(search|look|type|click|go|make|create|write|find|open|sign|log|send|start|join|watch|check|put|add|draw|set|turn|say|tell|message|text|dm|reply|post|ask|paste|share|ping)\b", RegexOptions.IgnoreCase)))
            {
                calls.Clear();
                calls.Add(new KeyValuePair<string, ToolArguments>("use_computer", new ToolArguments { Task = text }));
                return;
            }
            for (int i = 0; i < calls.Count; i++)
            {
                ToolArguments args = calls[i].Value;
                if (calls[i].Key != "open_website" || args == null || string.IsNullOrEmpty(args.Site) || Regex.IsMatch(text, @"[a-z0-9-]+\.[a-z]{2,}", RegexOptions.IgnoreCase)) continue;
                string name = Regex.Replace(args.Site.Trim(), @"^(https?://)?(www\.)?|\.[a-z.]+(/.*)?$", "", RegexOptions.IgnoreCase);
                if (Regex.IsMatch(text, @"\b" + Regex.Escape(name) + @"\b", RegexOptions.IgnoreCase) && Installed(name) && string.IsNullOrEmpty(args.Search)) calls[i] = new KeyValuePair<string, ToolArguments>("open_app", new ToolArguments { Name = name });
            }
        }

        private static readonly Regex Unknown = new Regex(@"\b(can'?t|cannot|can not|unable to|don'?t have|no way to|not able to)\b[^.!?]{0,40}\b(check|browse|look|search|google|access|know|tell|real[- ]?time|live|internet|web|online|current|up[- ]to[- ]date|forecast)\b|\b(i'?ll|i will|let me|lemme|i'?m (gonna|going to)|gonna|i gotta|i have to|i need to)\s+(\w+\s+){0,2}(search|google|look (it |that |this )?up|check)\b|\b(you('ll)? (might|may|will|would)? ?(need|want|have) to|maybe (you can|try)|you (gotta|got to|could|can|should)|try)\s+(\w+\s+){0,3}(look|check|search|google)\b|\bcheck (a|the|your) (\w+\s+){0,2}(weather|news|app|site|feed|forecast|website)\b|\bcrystal ball\b", RegexOptions.IgnoreCase);
        private static readonly Regex Question = new Regex(@"\?\s*$|^(what|what's|whats|how|how's|when|where|who|which|why|is|are|was|were|will|does|do|did|can|could|would|tell me|check|look up|google|search|any idea)\b", RegexOptions.IgnoreCase);

        internal static ToolArguments Lookup(string said, string reply)
        {
            string text = Asked(said);
            string plain = Regex.Replace(text, @"^(do|did) (you|u) (know|have any idea)\b|\byou know\b|^(tell|show|get|find) me\b|\b(near me|where i (am|live)|in my (area|city|town|suburb))\b", "", RegexOptions.IgnoreCase);
            if (text.Length < 4 || !Question.IsMatch((said ?? "").Trim()) || !Unknown.IsMatch((reply ?? "").Replace('’', '\'')) || Regex.IsMatch(plain, @"\b(screen|song|playing|track|you|your|yourself|u|ur|capri|me|my|i|i'm|im)\b", RegexOptions.IgnoreCase) || Regex.IsMatch(reply ?? "", @"\bscreen\b", RegexOptions.IgnoreCase) || Safety.Screen(text) != null) return null;
            return new ToolArguments { Site = "google", Search = text };
        }

        internal static string Pulled(string result)
        {
            if ((result ?? "").StartsWith("Opened your own website", StringComparison.Ordinal)) return "Here's my site, choom. That's where people download me.";
            // Power lines go out as they are: no second brain call while the shutdown counts down.
            if (Regex.IsMatch(result ?? "", @"^(Shutting your PC down|Restarting your PC|Signing you out|Putting your PC to sleep|Lock(ed|ing) your PC|Cancelled it\.)")) return result;
            Match searched = Regex.Match(result ?? "", @"^Searched google\.com for (.+)\.$");
            return searched.Success ? "Pulled up \"" + searched.Groups[1].Value + "\" on Google for you, choom." : null;
        }

        internal const string Unpromised = "I haven't done that yet, choom. Tell me what to open or click and I'm on it.";
        private static readonly Regex Promise = new Regex(@"\b(i'?ll|i will|let me|lemme|i'?m (gonna|going to|about to)|gonna|on it,?)[\s—–-]+(\w+[\s,—–-]+){0,2}(open|launch|load|boot|fire|pull|bring|search|google|look up|click|type|close|switch|head|install|download|play(?!\s+(along|around|with|nice|fair|dumb|it (cool|safe)|((a|some|the|this|that|any|your|my)\s+)?(games?|round|match)\b|devil))|put on|queue|blast|spin|mute|unmute|deafen|undeafen|send(?!\s+(you|u|ya)\b)|message(?!\s+(you|u)\b)|text(?!\s+(you|u)\b)|dm|ping|reply|post|hang up|(end|leave|join) (the|your|this|that) (call|vc|meeting))\b" +
            @"|^((sure|ok|okay|alright|bet|got it|on it|say less|yessir+|yezzir+)[,.!\s—–-]+)?((choom|chooms)[,.!\s—–-]+)?(opening|launching|loading|pulling up|bringing up|firing up|booting up|heading to|searching|clicking|typing|closing|switching|playing|putting on|queuing|queueing|blasting|spinning|muting|unmuting|deafening|sending|messaging|texting|dming|replying|posting|hanging up|ending (the|your) call|leaving (the|your) (call|vc)|joining (the|your) (call|vc)|disconnecting)\b" +
            @"|\bi'?m\s+(now\s+)?(sending|messaging|texting|dming|opening(?!\s+up\b)|launching|closing|muting|unmuting|deafening|hanging up|posting|replying)\b" +
            @"|^\W*((ok|okay|sure|bet|alright)[,.!\s—–-]+)?(i'?m\s+)?on it\b|\b(call|vc|message|dm|text)\s+(ended|sent|posted)\b" +
            @"|^((done|sorted|there|boom|bet|ok|okay|alright)[,.!\s—–-]+)?(i('ve| have)\s+)?(just\s+)?(opened|launched|closed|sent(?!\s+(you|u|ya)\b)|messaged|texted|dmed|dm'd|posted|replied|muted|unmuted|deafened|undeafened|hung up|(ended|left|joined) (the|your|this|that) (call|vc|meeting|server|channel))\b", RegexOptions.IgnoreCase);

        // "I'm sending you the biggest hug" is warmth, not a message she owes them. Warmth sent to someone by name still counts.
        private static readonly Regex Warmth = new Regex(@"\b(send(s|ing)?|sent)\s+((you|u|ya|y'all)\s+)?((a|an|the|my|some|all|so|much|many|lots|of|this|big|bigger|biggest|huge|massive|giant|tight|warm|warmest|virtual|good|positive|happy|healing|cozy|cosy|little|tiny|extra|more|mega|super|best|sweet|soft|gentle|digital|cyber|neon)\s+)*(hugs?|huggies|squeezes?|kiss(es)?|love|xoxo|vibes?|energy|thoughts|wishes|prayers?|luck|cuddles?|smooches?|hearts?|sunshine|strength|support|positivity|juju|blessings?|regards|cheers|kudos|props|high[- ]?fives?|fist[- ]?bumps?|headpats?|pats)\b(?!\s+(to|over to)\s+(?!(you|u|ya|y'all|yourself)\b)\w)", RegexOptions.IgnoreCase);

        internal static bool Promised(string said) { return Promise.IsMatch(Warmth.Replace((said ?? "").Replace('’', '\''), "")); }

        private static readonly Regex Credit = new Regex(@"\b(you|ya|u)('ve| have)?\s+(just\s+|already\s+|finally\s+|actually\s+)?(opened|typed|launched|sent|closed|played|paused|skipped|muted|unmuted|deafened|undeafened|clicked|searched|posted|messaged|texted|dmed|dm'd|replied|wrote|pulled up|fired up|booted up|hung up|ended|left|joined)\b", RegexOptions.IgnoreCase);

        internal static bool Credits(string said) { return Credit.IsMatch((said ?? "").Replace('’', '\'')); }

        private static readonly Regex Doing = new Regex(@"^((i (want|need) (you|u) to|go ahead and|just|now|then|and)\s+)*(send|dm|pm|message|msg|text|ping|tell|say|ask|type|write|post|tweet|reply|respond|put|paste|share|open|launch|start|run|close|quit|exit|kill|join|leave|end|hang up|call|ring|turn|shut|power|switch|restart|reboot|sleep|lock|log|sign|mute|unmute|deafen|undeafen|play|pause|skip|go|click|tap|press|scroll|search|google|find|make|create|delete|remove|add|invite|kick|ban|block|unblock|react|pin|accept|decline|install|uninstall|download|change|set|minimi[sz]e|maximi[sz]e|move|copy|rename|save|record|clip|fill|submit|hop|get on|get off|pick up|answer|reject)\b(?!\s+(me|us)\b)", RegexOptions.IgnoreCase);
        private static readonly Regex Hesitation = new Regex(@"\b(do you want me to|you want me to|want me to|would you like me to|should i|shall i|is it ok(ay)? (if i|for me to|to)|ok(ay)? (if i|for me to|to)|mind if i|are you sure|you sure|just to (check|confirm)|(can|could) you confirm|confirm(ing)?|double[- ]?check(ing)?|ready (for me )?to)\b[^.!?]{0,160}?\b(send|sent|post|messag|dm|text|typ|writ|past|shar|repl|open|launch|start|clos|quit|join|leav|end|hang|call|ring|shut|turn|power|restart|reboot|sleep|lock|sign|log|delet|remov|click|press|mut|unmut|deafen|undeafen|play|paus|skip|search|install|uninstall|go ahead|proceed|do (it|that|this))\w*" +
            @"|\b(say the word|just say (yes|go|so|the word)|give me the (go[- ]ahead|ok|okay|word|green light)|(with|on) your (ok|okay|go[- ]ahead|say[- ]so)|let me know (if|when) (you want|you'?re ready|to go|it'?s ok))\b|\b(is that|that) (right|correct|ok(ay)?|what you want)\s*\?|^\W*(are you sure|you sure|really|confirm)\W*$", RegexOptions.IgnoreCase);
        private static readonly Regex Agreeing = new Regex(@"^(yes+|yeah|yea|yep|yup|ya|ye|sure|ok(ay)?|k|do it|go ahead|go for it|send it|post it|of course|obviously|duh|absolutely|definitely|correct|right|i'?m sure|i said (so|yes)|that'?s (right|it|what i said))\b(?!.*\b(but|except|instead|change|actually|wait|don'?t|not)\b)", RegexOptions.IgnoreCase);

        // SAFETY: jobs that change something outside the PC or can't be undone. She asks before these, every time.
        private static readonly Regex Risky = new Regex(@"(send|dm|pm|message|msg|text|post|tweet|reply|respond|comment|publish|upload|buy|purchase|pay|order|checkout|check out|subscribe|donate|transfer|delete|remove|erase|wipe|trash|uninstall|install|download|format|shut ?down|turn off|power off|restart|reboot|sign out|log ?out|log ?off|end (the |this )?call|hang up|leave (the |this )?(call|server|group|chat)|kick|ban|unfriend|block|pošli|napiš|odešli|odpověz|zveřejni|kup|zaplať|objednej|smaž|vymaž|odstraň|odinstaluj|nainstaluj|stáhni|vypni|restartuj|odhlas)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        internal static bool NeedsOk(string said) { return Risky.IsMatch(Asked(said)); }

        // Her check before a risky job. "Want me to" is what Hesitant looks for, so their "yes" runs the job.
        internal static string Confirm(string said)
        {
            string job = Asked(said).Trim().TrimEnd('.', '!', '?');
            return "Just checking before I touch anything: want me to " + job + "? Say yes and I'll do it.";
        }

        // Her reply asking whether to do the PC job they just asked for, like "Want me to send that in #general?".
        internal static bool Hesitant(string reply, string said)
        {
            string text = Asked(said);
            if (text.Length == 0 || Chatter.IsMatch(text) || !Doing.IsMatch(text)) return false;
            foreach (string sentence in Regex.Split((reply ?? "").Replace('’', '\'').Trim(), @"(?<=[.!?])\s+"))
                if (!Regex.IsMatch(sentence, @"^\W*(what|which|who|whom|whose|where|when|how|why)\b", RegexOptions.IgnoreCase) && Hesitation.IsMatch(sentence)) return true;
            return false;
        }

        internal static bool Agrees(string said)
        {
            string text = Asked(said);
            return text.Length > 0 && text.Length <= 60 && Agreeing.IsMatch(text);
        }

        // What she should have done instead of asking: their own words as a mouse and keyboard job.
        internal static KeyValuePair<string, ToolArguments> Forced(string said) { return new KeyValuePair<string, ToolArguments>("use_computer", new ToolArguments { Task = Asked(said) }); }

        internal static bool Errand(string said)
        {
            foreach (string part in Regex.Split(said ?? "", @"[,;!?]\s*|\.\s+"))
            {
                string text = Asked(part);
                if (!Chatter.IsMatch(text) && Errands.IsMatch(text)) return true;
            }
            return false;
        }

        internal static bool Follows(string said, string asked, string answered)
        {
            string text = Asked(said);
            if (Chatter.IsMatch(text) || Regex.IsMatch(text, @"^(no|nah|never mind|nevermind|don't|do not|non|annule)\b", RegexOptions.IgnoreCase)) return false;
            if (Wants(text) && Regex.IsMatch(text, @"\b(it|this|that|them|there|again|instead|ça|celui)\b|-(le|la)\b", RegexOptions.IgnoreCase)) return true;
            if (!Wants(asked)) return false;
            if (Regex.IsMatch(text, @"^(do (it|that)|same again|again|on (spotify|youtube)( instead)?|fais(-le| ça)|encore)$", RegexOptions.IgnoreCase)) return true;
            if (!Regex.IsMatch(answered ?? "", @"\b(which|what should I play|lequel|laquelle)\b|\b(or|ou)\b[^?]*\?", RegexOptions.IgnoreCase)) return false;
            if (Regex.IsMatch(text, @"^(the )?(first|second|other|last)( one)?$|^(le|la) (premi[èe]re?|deuxi[èe]me|seconde?|derni[èe]re?|autre)$|^(yes|yeah|yep|oui)$", RegexOptions.IgnoreCase)) return true;
            return text.Length > 1 && text.Length < 80 && Regex.IsMatch(answered ?? "", @"(?<!\w)" + Regex.Escape(text) + @"(?!\w)", RegexOptions.IgnoreCase);
        }

        private static readonly Regex Lead = new Regex(@"^((hey|yo|ok|okay|salut|bonjour|oye|hola)[\s,]+)?(capri\b[\s,]*)?(((can|could|would|will) (you|u)|do (you|u)(?= mind)|please|pls|plz|stp|s'il te pla[iî]t|(est-ce que )?tu peux|peux-tu|pourrais-tu|(me )?puedes|(me )?podr[ií]as|por favor|porfa)\s+)*", RegexOptions.IgnoreCase);
        // "Check my screen", "can you see this?", "what game is this?": she takes a screenshot and looks, every time.
        private static readonly Regex ScreenAsk = new Regex(@"^(?:(?:can|could|would|will|do|did) (?:you|u) )?(?:(?:take a |have a |quick )?(?:look|peek|peep|glance)(?: at)?|check(?: out)?|see|watch|read|scan|view|look over|screenshot)(?: on)? (?:my|the|our|this|ur|your) (?:screen|monitor|display|desktop|pc|computer|stream|window|tab)s?$|^(?:(?:can|could|would|will|do|did) (?:you|u) )?(?:see|look at|check(?: out)?|peep|watch) (?:this|that|what i'?m (?:doing|playing|watching|looking at|working on|seeing))$|^(?:look at|check out|check|peep|watch) (?:this|that)(?: (?:out|real quick|quick))?$|^(?:take a|have a|quick) (?:look|peek|glance)(?: at (?:this|that|it|my screen|the screen))?$|^what(?:'s| is) on (?:my|the|our) (?:screen|monitor|display|desktop|pc)\b|^what (?:do|can|did) (?:you|u) see\b|^what are (?:you|u) (?:seeing|looking at)$|^what am i (?:doing|playing|watching|looking at|working on|reading)(?: (?:on my (?:screen|pc|computer)|rn|right now|now))?$|^regardes? (?:mon [ée]cran|[çc]a)$|^(?:tu )?(?:peux )?(?:voir|vois) mon [ée]cran$|^tu vois (?:mon [ée]cran|[çc]a)$|^qu'est-ce que (?:je fais|tu vois)$|^qu'est-ce qu'il y a sur mon [ée]cran$", RegexOptions.IgnoreCase);
        private static readonly Regex ScreenQuestion = new Regex(@"^what(?:'s| is) this(?: (?:game|app|thing|site|website|show|anime|movie|video|error|bug|code|program|word|map|level|boss|character|item|guy))?$|^what(?:'s| is) that (?:game|app|thing|site|website|show|anime|movie|video|error|bug|code|program|word|map|level|boss|character|item|guy)$|^what (?:game|app|anime|show|movie|video|website|site|error|program|level|map|boss|character|item) is (?:this|that)$|^who is this(?: (?:guy|character|boss))?$|^(?:how does|does) this look(?: (?:good|ok|okay|right|alright))?$|^what do (?:you|u) think (?:of|about) (?:this|my (?:screen|setup|build|base|drawing|art|design|avatar|character|thumbnail|outfit|fit|skin))$|^(?:look at|check out|peep|read) (?:this|that) \w+(?: \w+)?$|^rate (?:this|my (?:setup|desktop|wallpaper|screen|build|base|drawing|art|design|avatar|character|thumbnail|outfit|fit|skin))$", RegexOptions.IgnoreCase);
        // A reply that says she can't see their screen means she should look instead.
        private static readonly Regex Unseen = new Regex(@"\b(can'?t|cannot|can not|don'?t|do not|unable to|not able to|no way to|have no way to)\s+(\w+\s+){0,4}(see|look at|view|access|check|read)\s+(your|ur|the|their)\s+(screen|monitor|display|desktop)\b|\b(no|without)\s+(access to|way to see|eyes on)\s+(your|ur|the)\s+screen\b|\b(share|show)\s+(me\s+)?(your|ur)\s+screen\b|\bi('m| am)\s+(blind|not looking at (your|the) screen)\b", RegexOptions.IgnoreCase);

        internal static bool CantSee(string reply) { return Unseen.IsMatch(reply ?? ""); }

        private static readonly Regex Chatter = new Regex(@"^(play (with|along|around|dead|dumb|nice|fair|a game with|a joke)|open up (to|about)|start (a story|a joke|an argument)|run (a mile|a race)|kill (the boss|me|time)|look (at it this way|forward)|show me (how|why)|check my (spelling|grammar)|rate my joke|mets-toi)\b", RegexOptions.IgnoreCase);

        private static string Asked(string said)
        {
            said = Regex.Replace(said ?? "", @"^[\s¿¡]+", "");
            string text = Lead.Replace(Opener.Replace(Regex.Replace((said ?? "").Replace('\u2019', '\'').Trim(), @"\s+", " "), ""), "");
            for (int i = 0; i < 3; i++) text = Regex.Replace(Regex.Replace(text, @"[\s,.!?]+$", ""), @"\s+(for me|please|pls|plz|rn|right now|now|stp|s'il te pla[iî]t|por favor|porfa|porfis)$", "", RegexOptions.IgnoreCase);
            return text;
        }
        // Volume, mute and music in everyday words, in English, Spanish and French, so they never wait on a brain.
        // Volume means the PC's volume: Spotify's own slider only when they say Spotify.
        private const string Owner = @"(?:(?:the|my|ur|your|our|this|that|all the|pc'?s?|computer'?s?|laptop'?s?|spotify'?s?|youtube'?s?|game'?s?|video'?s?|music'?s?|song'?s?)\s+)*";
        private const string Noise = Owner + @"(?:volume|vol|sound|sounds|audio|music|song|track|tune|tunes|video|game|pc|computer|laptop|speakers?|headphones?|headset|everything|spotify|youtube)";
        private const string Thing = @"(?:it|this|that|" + Noise + ")";
        private const string Sound = Owner + @"(?:volume|vol|sound|sounds|audio|speakers?)";
        private const string Loud = Owner + @"(?:volume|vol|sound|sounds|audio|music|speakers?)";
        private const string Tune = @"(?:(?:the|my|this|that|spotify'?s?|youtube'?s?)\s+)*(?:music|song|track|tune|tunes|video|vid|playback|spotify|youtube|podcast)";
        private const string Percent = @"\s*(?:%|percent|per ?cent|por ?ciento|pour ?cent)?";
        private static readonly string[] MediaWords =
        {
            // A level: "volume to 50", "set the volume to 30%", "max the volume".
            @"^(?:(?:set|put|turn|change|make|bring|get|adjust|move|crank|drop|lower|raise|have)\s+)?" + Sound + @"(?:\s+level)?\s+(?:to|at|on|=)?\s*(?<n>\d{1,3})" + Percent + "$", "volume_set", "n",
            @"^(?:set|put|turn|bring|crank|drop|lower|raise|move)\s+(?:it|this|that)\s+(?:(?:up|down)\s+)?(?:to|at)\s+(?<n>\d{1,3})\s*(?:%|percent|per ?cent)$", "volume_set", "n",
            @"^(?:turn|crank|bring|put)\s+" + Thing + @"\s+(?:up|down)\s+to\s+(?<n>\d{1,3})" + Percent + "$", "volume_set", "n",
            @"^(?:max|maximi[sz]e|full)\s+(?:out\s+)?" + Sound + @"(?:\s+out)?$|^" + Sound + @"\s+(?:to\s+|at\s+|on\s+)?(?:the\s+)?(?:max|maximum|full|top)$|^(?:full|max|maximum)\s+volume$|^(?:turn|crank|pump|put)\s+" + Thing + @"\s+(?:all the way up|up all the way|up to (?:the\s+)?max|to (?:the\s+)?max)$", "volume_set", "100",
            @"^(?:turn|bring|put)\s+" + Thing + @"\s+(?:all the way down|down all the way)$|^" + Sound + @"\s+(?:to\s+)?(?:zero|nothing)$", "volume_set", "0",
            @"^(?:half|50%)\s+volume$|^" + Sound + @"\s+(?:to\s+|at\s+)?half(?:\s*way)?$", "volume_set", "50",
            @"^(?:(?:pon|sube|baja|ajusta|deja)\s+)?(?:el\s+)?volumen\s+(?:al|a|en)\s+(?<n>\d{1,3})" + Percent + @"$|^(?:mets\s+)?(?:le\s+)?(?:son|volume)\s+[a\u00e0]\s+(?<n>\d{1,3})" + Percent + "$", "volume_set", "n",
            // Unmute before mute, since "turn mute off" says mute.
            @"^(?:un-?mute|unsilence)(?:\s+" + Thing + @")?$|^(?:turn|switch|put|set)\s+(?:the\s+)?mute\s+off$|^(?:turn|switch)\s+off\s+(?:the\s+)?mute$|^(?:take|get)\s+(?:" + Thing + @"\s+)?off\s+(?:of\s+)?mute$|^mute off$", "unmute", "",
            @"^(?:turn|switch|put|bring)\s+" + Sound + @"\s+(?:back\s+)?on$|^(?:turn|switch)\s+(?:back\s+)?on\s+" + Sound + @"$|^(?:sound|volume|audio)\s+(?:back\s+)?on$|^bring\s+(?:back\s+)?" + Sound + @"(?:\s+back)?$", "unmute", "",
            @"^(?:quita(?:le)? el silencio|desmutea(?:r)?|activa el (?:sonido|audio|volumen)|pon el (?:sonido|audio|volumen)|enciende el (?:sonido|audio|volumen)|devu[e\u00e9]lveme el sonido)$|^(?:remets|r[\u00e9e]active|rallume) le son$|^enl[e\u00e8]ve la sourdine$", "unmute", "",
            @"^(?:mute|silence|hush)(?:\s+" + Thing + @")?$|^(?:kill|cut)\s+" + Sound + @"$|^(?:turn|switch|shut)\s+off\s+" + Sound + @"$|^(?:turn|switch|shut)\s+" + Sound + @"\s+off$", "mute", "",
            @"^(?:turn|switch|put|set)\s+(?:on\s+)?(?:the\s+)?mute(?:\s+on)?$|^(?:put|set)\s+(?:" + Thing + @"\s+)?on mute$|^(?:mute on|sound off|volume off|audio off|no sound)$", "mute", "",
            @"^(?:silencia(?:r)?|mutea(?:r)?|quita(?:le)? el (?:sonido|volumen|audio)|pon(?:lo)? en silencio|apaga el (?:sonido|volumen|audio))(?:\s+(?:el|la)\s+(?:volumen|sonido|audio|pc|computadora|ordenador|compu))?$|^(?:coupe(?:r)?|couper) (?:le )?(?:son|volume)$|^mets(?: le pc| l'ordi)? en sourdine$", "mute", "",
            // Up and down. "A bit", "a lot" or "by 20" say how far.
            @"^(?:turn|crank|pump|bump|jack|kick|ramp|dial|boost|raise|push|bring|knock|put)\s+" + Thing + @"\s+up$|^(?:turn|crank|pump|bump|jack|ramp|dial|boost)\s+up\s+" + Thing + @"$|^(?:raise|increase|boost|up|amp up)\s+" + Thing + @"$|^(?:make|get)\s+" + Thing + @"\s+(?:louder|loud|higher)$|^louder$|^" + Loud + @"\s+(?:louder|up|higher)$|^more (?:volume|sound)$", "volume_up", "",
            @"^(?:(?:it'?s|its|it is|that'?s|thats|this is|the (?:volume|sound|music|audio|game|video|song) is)\s+)?(?:(?:way|so|really|kinda|a bit|bit)\s+)?too (?:quiet|soft|low)$|^i can'?t hear (?:it|that|anything|the (?:music|video|game|sound|audio|song))$", "volume_up", "",
            @"^(?:sube(?:le)?|s[u\u00fa]bele|subir|aumenta(?:r)?|alza|pon(?:le)? m[a\u00e1]s alto|m[a\u00e1]s alto|m[a\u00e1]s volumen)(?:\s+(?:el|la|al)\s+(?:volumen|sonido|m[u\u00fa]sica|audio|canci[o\u00f3]n))?$|^(?:monte(?:r)?|augmente(?:r)?|mets plus fort|plus fort)(?:\s+(?:le|la)\s+(?:son|volume|musique))?$", "volume_up", "",
            @"^(?:turn|crank|bring|knock|dial|drop)\s+" + Thing + @"\s+down$|^(?:turn|crank|knock|dial|drop|bring)\s+down\s+" + Thing + @"$|^(?:lower|decrease|reduce|quieten)\s+" + Thing + @"$|^(?:drop|soften)\s+" + Noise + @"$|^(?:make|get)\s+" + Thing + @"\s+(?:quieter|softer|lower|less loud)$|^(?:quieter|softer)$|^" + Loud + @"\s+(?:down|quieter|softer|lower)$|^less (?:volume|sound)$", "volume_down", "",
            @"^(?:(?:it'?s|its|it is|that'?s|thats|this is|you'?re|ur|the (?:volume|sound|music|audio|game|video|song) is)\s+)?(?:(?:way|so|really|kinda|a bit|bit)\s+)?too loud$", "volume_down", "",
            @"^(?:baja(?:le)?|b[a\u00e1]jale|bajar|disminuye|reduce|pon(?:le)? m[a\u00e1]s bajo|m[a\u00e1]s bajo|menos volumen)(?:\s+(?:el|la|al)\s+(?:volumen|sonido|m[u\u00fa]sica|audio|canci[o\u00f3]n))?$|^(?:baisse(?:r)?|diminue(?:r)?|moins fort|mets moins fort)(?:\s+(?:le|la)\s+(?:son|volume|musique))?$", "volume_down", "",
            // The music itself.
            @"^pause(?:\s+" + Tune + @"|\s+(?:it|this|that))?$|^(?:stop|hold|freeze)\s+" + Tune + @"$|^(?:turn|switch|shut)\s+off\s+(?:the\s+)?(?:music|song|video|podcast)$|^(?:turn|switch|shut)\s+(?:the\s+)?(?:music|song|video|podcast)\s+off$", "pause", "",
            @"^(?:pausa(?:r)?|pon pausa|pausa la (?:m[u\u00fa]sica|canci[o\u00f3]n)|det[e\u00e9]n la (?:m[u\u00fa]sica|canci[o\u00f3]n)|para la (?:m[u\u00fa]sica|canci[o\u00f3]n))$|^(?:mets(?: la musique)? en pause|pause la musique)$", "pause", "",
            @"^(?:resume|unpause)(?:\s+" + Tune + @"|\s+(?:it|this|that))?$|^continue\s+" + Tune + @"$|^(?:keep playing|hit play|press play|play|play it again)$|^(?:turn|switch|put)\s+(?:the\s+)?(?:music|song|video|podcast)\s+back\s+on$", "resume", "",
            @"^(?:reanuda(?:r)?|contin[u\u00fa]a la (?:m[u\u00fa]sica|canci[o\u00f3]n)|dale play|quita la pausa|sigue con la m[u\u00fa]sica)$|^(?:reprends|relance|remets)(?: la musique)?$", "resume", "",
            @"^(?:skip|next)(?:\s+(?:to\s+)?(?:the\s+)?(?:next\s+)?(?:(?:this|that|current)\s+)?(?:song|track|one|tune|it|this|that))?$|^(?:play|go to|put on)\s+(?:the\s+)?next\s+(?:song|track|one|tune)$|^(?:change|switch)\s+(?:the\s+)?(?:song|track|tune)$|^(?:another|different|new)\s+(?:song|track|tune)$|^skip\s+(?:ahead|forward)$", "next", "",
            @"^(?:siguiente(?: canci[o\u00f3]n)?|la siguiente|pasa(?: la)? canci[o\u00f3]n|p[a\u00e1]sala|salta(?: esta| la)?(?: canci[o\u00f3]n)?|s[a\u00e1]ltala|otra canci[o\u00f3]n|cambia(?: la)? canci[o\u00f3]n)$|^(?:suivant(?:e)?|chanson suivante|passe (?:la chanson|[a\u00e0] la suivante)|la suivante)$", "next", "",
            @"^(?:previous|prev)(?:\s+(?:song|track|one|tune))?$|^go\s+back(?:\s+(?:a|one)\s+(?:song|track|tune))?$|^(?:(?:play|go to|put on)\s+)?(?:the\s+)?(?:last|previous|prior)\s+(?:song|track|one|tune)(?:\s+again)?$|^back\s+(?:a|one)\s+(?:song|track|tune)$", "previous", "",
            @"^(?:anterior|canci[o\u00f3]n anterior|la anterior|pon la anterior)$|^(?:pr[\u00e9e]c[\u00e9e]dent(?:e)?|chanson pr[\u00e9e]c[\u00e9e]dente|la pr[\u00e9e]c[\u00e9e]dente)$", "previous", "",
            @"^((what('|\u2019)?s|what is) (playing|this song|this track|the song|on spotify)|what (song|track)( is|('|\u2019)s)? (this|playing|on)|what am i listening to|what are we listening to)$", "now_playing", ""
        };

        internal static bool Loudness(string said, out string action, out string level, out bool spotify)
        {
            action = level = null;
            string text = Regex.Replace((said ?? "").ToLowerInvariant().Replace('\u2019', '\''), @"^(?:(?:i (?:want|need) (?:you|u) to|go ahead and|just|quickly|quick)\s+)+", "");
            spotify = Regex.IsMatch(text, @"\bspotify\b");
            text = Regex.Replace(text, @"\s+(?:on|in|for)\s+(?:spotify|youtube|my pc|the pc|my computer|the computer|my laptop)$", "");
            text = Regex.Replace(text, @"(?<!all the )\bway (up|down)$", "$1 a lot");
            string step = null;
            Match by = Regex.Match(text, @"\s+(?:(?<small>(?:just )?a (?:bit|little|lil|tad|notch|touch|smidge|hair)(?: more)?|a little bit(?: more)?|slightly)|(?<big>a lot(?: more)?|loads|heaps|tons|(?:a )?(?:bunch|ton)(?: more)?|much more)|by (?<n>\d{1,3})" + Percent + @")$");
            if (by.Success)
            {
                step = by.Groups["small"].Success ? "6" : by.Groups["big"].Success ? "20" : by.Groups["n"].Value;
                text = text.Substring(0, by.Index);
            }
            for (int i = 0; i < MediaWords.Length; i += 3)
            {
                Match found = Regex.Match(text, MediaWords[i]);
                if (!found.Success) continue;
                action = MediaWords[i + 1];
                bool stepped = action == "volume_up" || action == "volume_down";
                if (step != null && !stepped) return false;
                level = stepped ? step : MediaWords[i + 2] == "n" ? found.Groups["n"].Value : MediaWords[i + 2].Length > 0 ? MediaWords[i + 2] : null;
                int value;
                return level == null || (int.TryParse(level, out value) && value <= 100);
            }
            return false;
        }

        // A model's media call squared with what they said: unmute isn't mute, and only saying Spotify means Spotify's own volume.
        internal static void Aim(ToolArguments args, string said)
        {
            if (args == null) return;
            if (args.Action == "mute" && Regex.IsMatch(said ?? "", @"\bun-?mute\b|\bmute off\b|\boff (of )?mute\b|\b(sound|volume|audio) (back )?on\b|\bturn on (the )?(sound|volume|audio)\b", RegexOptions.IgnoreCase)) args.Action = "unmute";
            if (args.App == null && (args.Action == "volume_up" || args.Action == "volume_down") && Regex.IsMatch(said ?? "", @"\bspotify\b", RegexOptions.IgnoreCase)) args.App = "spotify";
        }

        internal static bool Heard(string said, out string tool, out ToolArguments args)
        {
            List<string> tries = new List<string>();
            Match named = Regex.Match(said ?? "", @"\b[ck]app?r(?:i|ee|ie|y)\b[\s,.!?]*([^\s,.!?].*)$", RegexOptions.IgnoreCase | RegexOptions.RightToLeft);
            if (named.Success) tries.Add(named.Groups[1].Value);
            string[] sentences = Regex.Split((said ?? "").Trim(), @"(?<=[.!?])\s+");
            if (sentences.Length > 1) tries.Add(sentences[sentences.Length - 1]);
            tries.Add(said ?? "");
            foreach (string attempt in tries) if (Direct(attempt, out tool, out args)) return true;
            tool = null;
            args = new ToolArguments();
            return false;
        }

        internal static bool Direct(string said, out string tool, out ToolArguments args)
        {
            tool = null;
            args = new ToolArguments();
            if (Moon.IsMatch(said ?? "")) { tool = "play_music"; args.Song = MoonSong; args.Kind = "song"; return true; }
            string text = Asked(said);
            if (Chatter.IsMatch(text) || Regex.IsMatch(text, @"\b(if|unless|until|after|before|when) (i|you|we|it|the)\b", RegexOptions.IgnoreCase)) return false;
            string minutes;
            string switched = PowerFor(text, out minutes);
            if (switched != null) { tool = "power"; args.Action = switched; args.Minutes = minutes; return true; }
            string pressed = ShortcutFor(text);
            if (pressed != null) { tool = "shortcut"; args.Name = pressed; return true; }
            if (ScreenAsk.IsMatch(text) || ScreenQuestion.IsMatch(text)) { tool = "look_at_screen"; args.Focus = ScreenQuestion.IsMatch(text) ? text : null; return true; }
            bool question = Regex.IsMatch((said ?? "").Trim(), @"\?\s*$");
            // "Can you remember that I main Jett?" asks her to save it; "remember when we played?" is chat.
            bool polite = Regex.IsMatch(Opener.Replace((said ?? "").Trim(), ""), @"^((hey|yo|ok|okay)[\s,]+)?(capri[\s,]+)?(can|could|would|will) (you|u)\b", RegexOptions.IgnoreCase);
            Match noted = Regex.Match(text, Remembering + @"(?:\s+(?:that|this))?[:,]?\s+(.+)$", RegexOptions.IgnoreCase);
            if (noted.Success && (!question || polite) && !Regex.IsMatch(noted.Groups[1].Value, @"^(me|us|when|what|how|why|who|where|if|it|to|in|on|for|with|using|the time|last time|our|we|you|stuff|things|anything|everything|something)\b|^(of )?(that|this)$", RegexOptions.IgnoreCase)) { tool = "remember"; args.Text = noted.Groups[1].Value; return true; }
            // "Leave a memory" or "remember that" on its own: she asks what to remember, or Brain.Ask saves what they just said.
            if ((!question || polite) && Regex.IsMatch(text, Remembering + @"(?: of)?(?: (?:that|this|it))?$", RegexOptions.IgnoreCase)) { tool = "remember"; args.Text = ""; return true; }
            noted = Regex.Match(text, @"^(?:i (?:want|need|would like) (?:you|u) to\s+)?forget(?:\s+(?:that|about|the note about|my note about|what i said about))?\s+([^,;]+)", RegexOptions.IgnoreCase);
            if (noted.Success && !question && !Regex.IsMatch(noted.Groups[1].Value.Trim(), @"^(it|that|this|about (it|that|this)|i asked|i said|what i said|the last thing|me|you|everything i said)\b", RegexOptions.IgnoreCase)) { tool = "remember"; args.Action = "forget"; args.Text = noted.Groups[1].Value.Trim(); return true; }
            string loud, step;
            bool spotify;
            if (Loudness(text, out loud, out step, out spotify))
            {
                tool = "media";
                args.Action = loud;
                args.Level = step;
                if (spotify && (loud == "volume_up" || loud == "volume_down")) args.App = "spotify";
                return true;
            }
            Match found = Regex.Match(text, @"^(?:open|launch|start|go to|fire up|load up)\s+(spotify|youtube)\s*(?:,|and|&)?\s*(?:then\s+)?(?:play|shuffle|put on|blast)\s+(.+)$", RegexOptions.IgnoreCase);
            if (found.Success) text = "play " + found.Groups[2].Value + " on " + found.Groups[1].Value;
            if (Regex.IsMatch(text, @"\b(and|then|but)\s+(open|launch|play|close|quit|pause|don't|do not)\b", RegexOptions.IgnoreCase)) return false;
            found = Regex.Match(text, @"^(open|launch|start|run|load up|boot up|fire up)\s+(up\s+)?(.+)$", RegexOptions.IgnoreCase);
            if (found.Success)
            {
                string target = found.Groups[3].Value.Trim();
                if (Regex.IsMatch(target, @"\s(and|then|&)\s|,\s*\S", RegexOptions.IgnoreCase)) return false;
                if (Regex.IsMatch(target, @"^(your|ur|capri'?s|the capri)( own)? (web ?site|site|page|download page)$", RegexOptions.IgnoreCase)) { tool = "open_website"; args.Site = Home; return true; }
                if (Installed(target)) { tool = "open_app"; args.Name = target; return true; }
                if (Site(target)) { tool = "open_website"; args.Site = target; return true; }
                return false;
            }
            found = Regex.Match(text, @"^(?:google|look up)\s+(.+)$", RegexOptions.IgnoreCase);
            if (found.Success && !Regex.IsMatch(found.Groups[1].Value, @"^(it|this|that|something|stuff|for me)$", RegexOptions.IgnoreCase)) { tool = "open_website"; args.Site = "google"; args.Search = found.Groups[1].Value.Trim(); return true; }
            if (Regex.IsMatch(text, LookupWords, RegexOptions.IgnoreCase) && !Regex.IsMatch(Regex.Replace(text, @"\bnear me\b", ""), @"\b(my|your|you|me|i)\b", RegexOptions.IgnoreCase) && Safety.Screen(text) == null) { tool = "open_website"; args.Site = "google"; args.Search = text; return true; }
            found = Regex.Match(text, @"^(?:play|shuffle|put on|blast)\s+(.+?)(\s+on\s+(spotify|youtube))?$", RegexOptions.IgnoreCase);
            if (found.Success)
            {
                string target = found.Groups[1].Value.Trim();
                if (!found.Groups[3].Success && Installed(target)) { tool = "open_app"; args.Name = target; return true; }
                if (Regex.IsMatch(target, @"^(with|along|around|a game|games?\b|dead|nice|fair|it\b|me\b|us\b|together|((a|any|some|random) )?playlists?$)", RegexOptions.IgnoreCase)) return false;
                if (found.Groups[3].Success) args.App = found.Groups[3].Value.ToLowerInvariant();
                if (Regex.IsMatch(target, @"^(music$|some music$|((a|any|some|random) )?(song|track|tune|music)s?$|(something|anything)\b(?!\s+by\s))", RegexOptions.IgnoreCase)) { tool = "play_music"; args.Kind = "song"; args.Song = Pick(); return true; }
                Match kind = Regex.Match(target, @"^(?:(?:the|my|your)\s+)?(.+?)\s+(playlist|album)$", RegexOptions.IgnoreCase);
                Match first = Regex.Match(target, @"^(?:the\s+)?(playlist|album)\s+(.+)$", RegexOptions.IgnoreCase);
                Match by = Regex.Match(target, @"^(?:some(?:thing)?|anything|(?:(?:some|a|any)\s+)?(?:songs?|music|tracks?))\s+by\s+(.+)$", RegexOptions.IgnoreCase);
                if (Regex.IsMatch(target, @"^((my|your|the)\s+)?((liked|saved)\s+songs|likes)(\s+playlist)?$", RegexOptions.IgnoreCase)) { args.Kind = "playlist"; args.Song = "Liked Songs"; }
                else if (kind.Success) { args.Kind = kind.Groups[2].Value.ToLowerInvariant(); args.Song = kind.Groups[1].Value; }
                else if (first.Success) { args.Kind = first.Groups[1].Value.ToLowerInvariant(); args.Song = first.Groups[2].Value; }
                else if (by.Success) { args.Kind = "artist"; args.Song = by.Groups[1].Value; }
                else { args.Kind = "song"; args.Song = target; }
                tool = "play_music";
                return true;
            }
            found = Regex.Match(text, @"^(close|quit|exit|kill|shut down|switch to|go to|bring up|show me|minimi[sz]e)\s+(.+)$", RegexOptions.IgnoreCase);
            if (!found.Success || Regex.IsMatch(found.Groups[2].Value, @"^(it|this|that|them|this one|that one|everything|all|all of (it|them)|the window|this window|that window)$", RegexOptions.IgnoreCase) || !Showing(found.Groups[2].Value)) return false;
            string verb = found.Groups[1].Value.ToLowerInvariant();
            tool = "app_window";
            args.Name = found.Groups[2].Value.Trim();
            args.Action = verb.StartsWith("minimi", StringComparison.Ordinal) ? "minimize" : Regex.IsMatch(verb, "^(close|quit|exit|kill|shut down)$") ? "close" : "switch_to";
            return true;
        }

        internal static bool Failed(string result) { return Regex.IsMatch(result ?? "", @"^(Which app|What should I play|Not (opening|playing) that|No app called|More than one|That didn't work|Couldn't|There's no|That isn't a website|Which job|Not doing that|Should I |Got lost|Ran out of steps|You pressed Esc|You grabbed the mouse|Using the mouse and keyboard isn't)|isn't open\.$|but (couldn't|it's still open)"); }

        private static readonly Regex LooseText = new Regex(@"\b(open_app|open_website|media|look_at_screen|play_music|app_window|use_computer|shortcut|remember|power)\s*(\{[^{}]*\}|\([^()]*\))");
        private static readonly Regex ToolPair = new Regex(@"[""']?(\w+)[""']?\s*[:=]\s*(?:""([^""]*)""|'([^']*)'|([^,})\s][^,})]*))");

        private static ToolArguments Pairs(string tool, string call)
        {
            ToolArguments args = new ToolArguments();
            string inside = call.Substring(1, call.Length - 2);
            bool paired = false;
            foreach (Match pair in ToolPair.Matches(inside))
            {
                paired = true;
                Set(args, pair.Groups[1].Value.ToLowerInvariant(), pair.Groups[2].Success ? pair.Groups[2].Value : pair.Groups[3].Success ? pair.Groups[3].Value : pair.Groups[4].Value.Trim());
            }
            string lone = inside.Trim().Trim('"', '\'');
            if (!paired && lone.Length > 0) Set(args, tool == "open_website" ? "site" : tool == "media" || tool == "power" ? "action" : tool == "look_at_screen" ? "focus" : tool == "play_music" ? "song" : tool == "use_computer" ? "task" : tool == "remember" ? "text" : "name", lone);
            return args;
        }

        private static void Set(ToolArguments args, string key, string value)
        {
            if (key == "name") args.Name = value;
            else if (key == "site") args.Site = value;
            else if (key == "search") args.Search = value;
            else if (key == "action") args.Action = value;
            else if (key == "focus") args.Focus = value;
            else if (key == "song") args.Song = value;
            else if (key == "app") args.App = value;
            else if (key == "kind") args.Kind = value;
            else if (key == "task") args.Task = value;
            else if (key == "text") args.Text = value;
            else if (key == "minutes") args.Minutes = value;
        }

        private static readonly Regex ToolText = new Regex(@"\A(open_app|open_website|media|look_at_screen|play_music|app_window|use_computer|shortcut|remember|power)\s*(\{.*\})\z", RegexOptions.Singleline);

        internal static bool Typed(string reply, out string tool, out ToolArguments args, bool loose = false)
        {
            tool = null;
            args = new ToolArguments();
            string text = Brain.Visible(reply).Trim();
            Match fence = Regex.Match(text, @"\A```(?:json)?\s*\n(.*?)\s*```\z", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (fence.Success) text = fence.Groups[1].Value.Trim();
            text = Regex.Replace(text, @"<\|""\|>|<escape>", "\"");
            Match found = ToolText.Match(text);
            if (found.Success) args = Read(found.Groups[2].Value);
            else
            {
                if (!loose) return false;
                found = LooseText.Match(text);
                if (!found.Success || Regex.IsMatch(text.Substring(0, found.Index), @"\b(not|never|won't|can't|cannot|don't)\b", RegexOptions.IgnoreCase)) return false;
                args = Read(found.Groups[2].Value) ?? Pairs(found.Groups[1].Value, found.Groups[2].Value);
            }
            if (!Valid(found.Groups[1].Value, args)) return false;
            tool = found.Groups[1].Value;
            return true;
        }

        internal static ToolArguments Read(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                using (System.Xml.XmlDictionaryReader reader = JsonReaderWriterFactory.CreateJsonReader(Encoding.UTF8.GetBytes(json), System.Xml.XmlDictionaryReaderQuotas.Max))
                {
                    ToolArguments args = (ToolArguments)new DataContractJsonSerializer(typeof(ToolArguments)).ReadObject(reader);
                    return reader.Read() ? null : args;
                }
            }
            catch (SerializationException) { return null; }
            catch (System.Xml.XmlException) { return null; }
        }

        internal static bool Valid(string tool, ToolArguments args)
        {
            if (args == null) return false;
            if (tool == "open_app") return Named(args.Name);
            if (tool == "open_folder") return Named(args.Name);
            if (tool == "open_website") return Named(args.Site);
            if (tool == "look_at_screen") return true;
            if (tool == "media")
            {
                int level;
                bool leveled = int.TryParse(args.Level, out level) && level >= 0 && level <= 100;
                if (args.Action == "volume_set") return leveled;
                return Regex.IsMatch(args.Action ?? "", @"\A(play_pause|pause|resume|next|previous|volume_up|volume_down|mute|unmute|now_playing)\z") && (string.IsNullOrEmpty(args.Level) || leveled);
            }
            if (tool == "app_window") return Named(args.Name) && Regex.IsMatch(args.Action ?? "", @"\A(switch_to|minimize|close)\z");
            if (tool == "use_computer") return Named(args.Task);
            if (tool == "shortcut") return Regex.IsMatch(args.Name ?? "", @"\A(discord_mute|discord_deafen|screenshot|clip_that|record|game_bar|show_desktop|task_manager|clipboard|emoji)\z");
            if (tool == "remember") return Named(args.Text) && (string.IsNullOrEmpty(args.Action) || args.Action == "remember" || args.Action == "forget");
            if (tool == "power") return Regex.IsMatch(args.Action ?? "", @"\A(shut_down|restart|sleep|lock|sign_out|cancel)\z") && (string.IsNullOrEmpty(args.Minutes) || Regex.IsMatch(args.Minutes.Trim(), @"\A\d{1,4}\z"));
            return tool == "play_music" && Named(args.Song) && (string.IsNullOrEmpty(args.Kind) || Regex.IsMatch(args.Kind, @"\A(song|playlist|album|artist)\z")) && (string.IsNullOrEmpty(args.App) || args.App == "spotify" || args.App == "youtube");
        }

        // "Open Discord and Spotify" opens both, so nobody has to ask for each app on its own. Anything she doesn't know goes to her brain.
        internal static bool Several(string said, List<KeyValuePair<string, ToolArguments>> calls)
        {
            Match found = Regex.Match(Asked(said), @"^(?:open|launch|start|run|load up|boot up|fire up)\s+(?:up\s+)?(.+)$", RegexOptions.IgnoreCase);
            if (!found.Success) return false;
            string[] parts = Regex.Split(found.Groups[1].Value.Trim(), @"\s*,\s*(?:and\s+|&\s*)?|\s+and\s+|\s*&\s*", RegexOptions.IgnoreCase);
            if (parts.Length < 2) return false;
            List<KeyValuePair<string, ToolArguments>> opening = new List<KeyValuePair<string, ToolArguments>>();
            foreach (string part in parts)
            {
                string target = Regex.Replace(part.Trim(), @"^(?:open|launch|start|run)\s+", "", RegexOptions.IgnoreCase);
                if (Installed(target)) opening.Add(new KeyValuePair<string, ToolArguments>("open_app", new ToolArguments { Name = target }));
                else if (Site(target)) opening.Add(new KeyValuePair<string, ToolArguments>("open_website", new ToolArguments { Site = target }));
                else return false;
            }
            calls.AddRange(opening);
            return true;
        }

        private static bool Named(string text) { return !string.IsNullOrWhiteSpace(text) && text.Trim() != "..."; }

        internal static bool Installed(string target)
        {
            string want = Regex.Replace(Plain(target), @"^the |( app| application| game)$", "");
            if (want.Length == 0) return false;
            try
            {
                foreach (InstalledApp app in Apps())
                    if (Score(app, want) >= 3) return true;
            }
            catch (Exception) { }
            return false;
        }

        private static bool Site(string target)
        {
            string host = target.Trim().ToLowerInvariant();
            if (Regex.IsMatch(host, @"^(https?://)?[a-z0-9-]+(\.[a-z0-9-]+)+(/\S*)?$")) return true;
            for (int i = 0; i < Searches.GetLength(0); i++) if (host == Searches[i, 0]) return true;
            return false;
        }

        private static bool Showing(string target)
        {
            string want = Regex.Replace(Plain(target), @"^the |( app| application| game| tab| window)$", "");
            if (want.Length == 0) return false;
            try
            {
                foreach (OpenWindow window in Windows())
                    if (Math.Max(Score(Plain(window.Program), want), Score(Plain(window.Title), want)) > 0) return true;
            }
            catch (Exception) { }
            return false;
        }

        // Opens one of their folders in File Explorer. It only opens it: nothing is moved, changed or deleted.
        private static string OpenFolder(string wanted)
        {
            string name = (wanted ?? "").Trim().Trim('"');
            string path = KnownFolder(name) ?? FindFolder(name);
            if (path == null) return "Couldn't find a folder called " + name + ", choom.";
            using (Process.Start(new ProcessStartInfo("explorer.exe", "\"" + path + "\"") { UseShellExecute = false })) { }
            return "Opened the " + Path.GetFileName(path.TrimEnd('\\')) + " folder.";
        }

        // Their usual folders, in English or Czech: "my downloads", "stažené soubory", "plocha"...
        private static string KnownFolder(string name)
        {
            if (Path.IsPathRooted(name) && Directory.Exists(name)) return Path.GetFullPath(name);
            string plain = Regex.Replace(name.ToLowerInvariant(), @"\b(my|the|folder|moje|můj|složk[auy]|slozk[auy])\b", " ").Trim();
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (Regex.IsMatch(plain, @"^(documents?|docs|dokumenty)$")) return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (Regex.IsMatch(plain, @"^(downloads?|stažené( soubory)?|stazene( soubory)?)$")) return Path.Combine(home, "Downloads");
            if (Regex.IsMatch(plain, @"^(desktop|plocha|plochu)$")) return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (Regex.IsMatch(plain, @"^(pictures?|photos?|obrázky|obrazky|fotky)$")) return Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            if (Regex.IsMatch(plain, @"^(music|hudba|hudbu)$")) return Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
            if (Regex.IsMatch(plain, @"^(videos?|videa)$")) return Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
            return null;
        }

        // A folder by its name, one level inside their home, Documents, Desktop or Downloads.
        private static string FindFolder(string name)
        {
            if (name.Length == 0) return null;
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string[] roots = { home, Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Path.Combine(home, "Downloads") };
            for (int pass = 0; pass < 2; pass++)
                foreach (string root in roots)
                {
                    string[] dirs;
                    try { dirs = Directory.GetDirectories(root); }
                    catch (UnauthorizedAccessException) { continue; }
                    catch (IOException) { continue; }
                    foreach (string dir in dirs)
                    {
                        string found = Path.GetFileName(dir);
                        // First an exact name, then a name that contains what they said.
                        if (pass == 0 ? string.Equals(found, name, StringComparison.OrdinalIgnoreCase) : found.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0) return dir;
                    }
                }
            return null;
        }

        private static string OpenApp(string wanted)
        {
            if (string.IsNullOrWhiteSpace(wanted)) return "Which app, choom?";
            if (Safety.Stops(wanted) != null) return "Not opening that, choom.";
            List<InstalledApp> found = Match(Apps(), wanted);
            if (found.Count == 0) return "No app called " + wanted.Trim() + " is installed on this PC.";
            // When several match equally well, the one with the fewest extra words is closest to what they said.
            InstalledApp chosen = found[0];
            foreach (InstalledApp app in found) if (AppName(app).Length < AppName(chosen).Length) chosen = app;
            Run(chosen.Id);
            return "Opened " + (AppName(chosen) == "spotify" ? "Spotify" : chosen.Name) + ".";
        }

        internal static List<InstalledApp> Match(List<InstalledApp> apps, string wanted)
        {
            string want = Regex.Replace(Plain(wanted), @"^the |( app| application| game)$", "");
            List<InstalledApp> top = new List<InstalledApp>();
            if (want.Length == 0) return top;
            int best = 0;
            foreach (InstalledApp app in apps)
            {
                string name = AppName(app);
                int score = Score(app, want);
                if (score == 0 || score < best) continue;
                if (score > best) { best = score; top.Clear(); }
                if (!top.Exists(other => AppName(other) == name)) top.Add(app);
            }
            return top;
        }

        private static int Score(string name, string want)
        {
            if (name == want || name.Replace(" ", "") == want.Replace(" ", "")) return 4;
            if (name.StartsWith(want + " ", StringComparison.Ordinal)) return 3;
            if ((" " + name + " ").Contains(" " + want + " ")) return 2;
            if (want.Length >= 4 && name.Contains(want)) return 1;
            return 0;
        }

        private static int Score(InstalledApp app, string want)
        {
            string name = AppName(app);
            return name.Length == 0 ? 0 : Math.Max(Score(name, want), Score(Plain(app.Name), want));
        }

        private static string AppName(InstalledApp app)
        {
            string name = Plain(app.Name);
            if (string.IsNullOrWhiteSpace(app.Id) || Junk.IsMatch(name) || app.Id.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return "";
            if (Regex.IsMatch(app.Id, @"^SpotifyAB\.SpotifyMusic_[a-z0-9]+!Spotify$|^(?:[A-Za-z]:\\|\\\\).*[\\]Spotify\.exe$", RegexOptions.IgnoreCase)) return "spotify";
            return name;
        }

        private static string Plain(string text) { return Regex.Replace(Regex.Replace((text ?? "").Normalize(NormalizationForm.FormD).ToLowerInvariant(), @"\p{M}", ""), @"[^\p{L}\p{Nd}]+", " ").Trim(); }

        private static List<InstalledApp> Apps()
        {
            lock (gate)
            {
                if (cached != null && DateTime.UtcNow - cachedAt <= TimeSpan.FromMinutes(2)) return cached;
                List<InstalledApp> apps;
                Exception failed = null;
                try { apps = new List<InstalledApp>(Catalog()); }
                catch (Exception error) { apps = new List<InstalledApp>(); failed = error; }
                string folder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string path = folder.Length == 0 ? "" : Path.Combine(folder, @"Spotify\Spotify.exe");
                if (path.Length > 0 && Exists(path) && !apps.Exists(app => AppName(app) == "spotify")) apps.Add(new InstalledApp("Spotify", path));
                if (failed != null)
                {
                    if (apps.Count == 0) throw failed;
                    return apps;
                }
                cached = apps;
                cachedAt = DateTime.UtcNow;
                return cached;
            }
        }

        private static List<InstalledApp> ReadCatalog()
        {
            List<InstalledApp> apps = new List<InstalledApp>();
            Exception failed = null;
            Thread reader = new Thread(delegate()
            {
                try
                {
                    object shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application"));
                    object items = Call(Call(shell, "NameSpace", "shell:AppsFolder"), "Items");
                    int count = (int)Get(items, "Count");
                    for (int i = 0; i < count; i++)
                    {
                        object item = Call(items, "Item", i);
                        string name = Get(item, "Name") as string;
                        string id = Get(item, "Path") as string;
                        if (!string.IsNullOrEmpty(id)) apps.Add(new InstalledApp(name ?? "", id));
                    }
                }
                catch (Exception error) { failed = error; }
            });
            reader.SetApartmentState(ApartmentState.STA);
            reader.IsBackground = true;
            reader.Start();
            if (!reader.Join(10000)) throw new TimeoutException("The app list took too long.");
            if (failed != null) throw failed;
            return apps;
        }

        private static object Call(object target, string method, params object[] args) { return target.GetType().InvokeMember(method, BindingFlags.InvokeMethod, null, target, args); }

        private static object Get(object target, string property) { return target.GetType().InvokeMember(property, BindingFlags.GetProperty, null, target, null); }

        private static void Start(string id)
        {
            using (Process.Start(Launch(id))) { }
        }

        internal static ProcessStartInfo Launch(string id)
        {
            ProcessStartInfo info = Regex.IsMatch(id, @"^[A-Za-z]:\\|^\\\\|^[a-z][a-z0-9+.-]*://|^spotify:", RegexOptions.IgnoreCase) ? new ProcessStartInfo(id) : new ProcessStartInfo("explorer.exe", "shell:AppsFolder\\" + id);
            info.UseShellExecute = true;
            return info;
        }

        private static string OpenSite(string site, string search)
        {
            if (Safety.Stops((site ?? "") + " " + (search ?? "")) != null) return "Not opening that, choom.";
            string address = Address(site, search);
            if (address == null) return "That isn't a website.";
            Run(address);
            if (address.TrimEnd('/') == "https://" + Home) return "Opened your own website, " + Home + ".";
            string host = new Uri(address).Host;
            if (host.StartsWith("www.", StringComparison.Ordinal)) host = host.Substring(4);
            return string.IsNullOrWhiteSpace(search) ? "Opened " + host + "." : "Searched " + host + " for " + search.Trim() + ".";
        }

        internal static string Address(string site, string search)
        {
            string host = Regex.Replace((site ?? "").Trim().ToLowerInvariant(), @"^(https?://)?(www\.)?", "");
            bool searching = !string.IsNullOrWhiteSpace(search);
            if (searching)
            {
                for (int i = 0; i < Searches.GetLength(0); i++)
                {
                    string key = Searches[i, 0];
                    if (host == key || host.StartsWith(key + ".", StringComparison.Ordinal) || host.Contains("." + key + ".")) return Searches[i, 1] + Uri.EscapeDataString(search.Trim());
                }
                return Searches[1, 1] + Uri.EscapeDataString((search.Trim() + " " + host).Trim());
            }
            if (host.Length == 0) return null;
            if (host.IndexOf('.') < 0) host += ".com";
            Uri uri;
            if (!Uri.TryCreate("https://" + host, UriKind.Absolute, out uri) || uri.Host.IndexOf('.') < 0 || uri.UserInfo.Length > 0) return null;
            return uri.AbsoluteUri;
        }

#if CROSS
        // Mac and Linux have no Windows audio API (Speakers lives in Ears.cs), so mute toggles with the key and a set level isn't possible.
        internal static Func<bool?> MuteState = delegate { return (bool?)null; };
        internal static Func<bool, bool> SetMute = delegate { return false; };
        internal static Func<double?> VolumeNow = delegate { return (double?)null; };
        internal static Func<double, bool> SetVolume = delegate { return false; };
        private const string VolumeRefused = "Couldn't set the volume: here I can only turn it up or down.";
#else
        internal static Func<bool?> MuteState = Speakers.Muted;
        internal static Func<bool, bool> SetMute = Speakers.Mute;
        internal static Func<double?> VolumeNow = Speakers.Level;
        internal static Func<double, bool> SetVolume = Speakers.SetLevel;
        private const string VolumeRefused = "Couldn't set the volume: Windows didn't let me.";
#endif

        private static string Media(ToolArguments args)
        {
            string action = args.Action;
            if (action == "play_pause" || action == "pause" || action == "resume") return Toggle(action);
            if (action == "next") { Press(0xB0); return "Skipped to the next track."; }
            if (action == "previous") { Press(0xB1); return "Went back a track."; }
            int level;
            bool leveled = int.TryParse(args.Level, out level) && level >= 0 && level <= 100;
            // Each volume key moves Windows' volume 2%, and shows its volume bar.
            if (action == "volume_up" || action == "volume_down")
            {
                bool up = action == "volume_up";
                int presses = leveled && level > 0 ? Math.Max(1, (level + 1) / 2) : 5;
                for (int i = 0; i < presses; i++) Press(up ? (byte)0xAF : (byte)0xAE);
                return up ? "Turned the volume up." : "Turned the volume down.";
            }
            if (action == "volume_set")
            {
                if (!leveled) return "Couldn't set the volume: it goes from 0 to 100.";
                if (!SetVolume(level / 100.0)) return VolumeRefused;
                if (level > 0 && MuteState() == true) SetMute(false);
                return "Set the volume to " + level + "%.";
            }
            if (action == "mute" || action == "unmute")
            {
                bool muting = action == "mute";
                bool? muted = MuteState();
                if (muted == null) { Press(0xAD); return "Toggled mute."; }
                double? now = VolumeNow();
                // "Turn the sound on" with the volume at zero needs volume as well as unmuting.
                if (!muting && now.HasValue && now.Value < 0.01 && SetVolume(0.2))
                {
                    if (muted.Value) SetMute(false);
                    return "Turned the sound back on at 20%.";
                }
                if (muted.Value == muting) return muting ? "The PC is already muted." : "The sound is already on.";
                if (!SetMute(muting)) { Press(0xAD); return "Toggled mute."; }
                return muting ? "Muted the PC." : "Unmuted the PC.";
            }
            if (action == "now_playing") return Playing();
            return "There's no media action called " + action + ".";
        }

        private static string Playing()
        {
            string title = NowPlaying();
            if (TrackTitle(title))
            {
                int dash = title.IndexOf(" - ", StringComparison.Ordinal);
                return "Spotify is playing " + (dash > 0 ? title.Substring(dash + 3) + " by " + title.Substring(0, dash) : title) + ".";
            }
            foreach (OpenWindow window in Windows())
            {
                Match video = Regex.Match(window.Title, @"^(?:\(\d+\)\s*)?(.+?) - YouTube( Music)?( - |$)");
                if (video.Success) return "Their YouTube tab is on " + video.Groups[1].Value + ".";
            }
            if (Regex.IsMatch(title, @"^Spotify( \w+)?$")) return "Spotify is open but paused.";
            return title.Length > 0 ? "Spotify is open, but I can't tell what's playing." : "Nothing's playing that I can see.";
        }

        private static string Toggle(string action)
        {
            string before = NowPlaying();
            Press(0xB3);
            for (int i = 0; i < 6 && before.Length > 0; i++)
            {
                Thread.Sleep(250);
                string after = NowPlaying();
                if (after == before) continue;
                if (TrackTitle(after)) return "Spotify is playing again.";
                if (TrackTitle(before)) return "Paused Spotify.";
            }
            return action == "pause" ? "Pressed pause." : action == "resume" ? "Pressed play." : "Pressed play/pause.";
        }

        private static readonly string[,] Shortcuts =
        {
            { "discord_mute", "discord", "11 10 4D", "Toggled their mic mute in Discord (Ctrl + Shift + M)." },
            { "discord_deafen", "discord", "11 10 44", "Toggled deafen in Discord (Ctrl + Shift + D)." },
            { "screenshot", "", "5B 10 53", "Opened the screenshot snip (Win + Shift + S). Drag over what to grab." },
            { "clip_that", "", "5B 12 47", "Saved the last 30 seconds with Game Bar (Win + Alt + G)." },
            { "record", "", "5B 12 52", "Started or stopped a Game Bar recording (Win + Alt + R)." },
            { "game_bar", "", "5B 47", "Opened the Xbox Game Bar (Win + G)." },
            { "show_desktop", "", "5B 44", "Showed the desktop (Win + D)." },
            { "task_manager", "", "11 10 1B", "Opened Task Manager." },
            { "clipboard", "", "5B 56", "Opened clipboard history (Win + V)." },
            { "emoji", "", "5B BE", "Opened the emoji picker (Win + .)." }
        };

        private static readonly string[] ShortcutWords =
        {
            @"^(un)?mute (my |me on |me in |)(mic|microphone|myself|me)( on discord| in discord| in (the )?(call|vc|voice( chat| channel)?))?$|^turn (off |on )?my (mic|microphone)( off| on| back on)?( on discord| in discord)?$|^(un)?mute (me|myself|my mic|my microphone) (on|in) discord$|^discord mute$", "discord_mute",
            @"^(un)?deafen( me| myself)?( on discord| in discord)?$|^discord deafen$", "discord_deafen",
            @"^(take|grab|get) (a |me a )?(screenshot|screen ?shot|screen grab|snip)( of (this|my screen|the screen))?$|^screenshot( this| that| my screen)?$", "screenshot",
            @"^clip (that|it|this)$|^(save|grab) (a |the )?clip$|^clip the last 30 seconds$", "clip_that",
            @"^(start|stop) (the )?recording( my screen| the screen)?$|^record (my screen|the screen|this)$", "record",
            @"^(open|show|bring up|pull up) (the |my )?(xbox )?game ?bar$", "game_bar",
            @"^show (me )?(the |my )?desktop$|^minimi[sz]e (everything|all( windows)?)$", "show_desktop",
            @"^(open|show|bring up|pull up) (the )?task manager$", "task_manager",
            @"^(open|show|bring up|pull up) (the |my )?clipboard( history)?$", "clipboard",
            @"^(open|show|bring up|pull up) (the )?(emoji|emojis)( picker| keyboard| panel| menu)?$", "emoji"
        };

        internal static string ShortcutFor(string text)
        {
            string lower = (text ?? "").Trim().ToLowerInvariant();
            for (int i = 0; i < ShortcutWords.Length; i += 2) if (Regex.IsMatch(lower, ShortcutWords[i])) return ShortcutWords[i + 1];
            return null;
        }

        private static string Shortcut(string name)
        {
            int row = -1;
            for (int i = 0; i < Shortcuts.GetLength(0); i++) if (Shortcuts[i, 0] == name) row = i;
            if (row < 0) return "There's no shortcut called " + name + ".";
            string[] codes = Shortcuts[row, 2].Split(' ');
            byte[] keys = new byte[codes.Length];
            for (int i = 0; i < codes.Length; i++) keys[i] = Convert.ToByte(codes[i], 16);
            if (Shortcuts[row, 1] != "discord") { Chord(keys); return Shortcuts[row, 3]; }
            OpenWindow discord = FindDiscord();
            if (discord == null)
            {
                string opened = OpenApp("Discord");
                if (!opened.StartsWith("Opened", StringComparison.Ordinal)) return "Discord isn't open, so I couldn't press its shortcut. " + opened;
                for (int i = 0; i < 20 && discord == null; i++) { Thread.Sleep(250); discord = FindDiscord(); }
                if (discord == null) return "Opened Discord, but its window didn't show up in time. Ask again in a sec.";
            }
            IntPtr before = Native.GetForegroundWindow();
            if (!Steer(discord, "switch_to")) return "Couldn't bring Discord to the front to press its shortcut.";
            Chord(keys);
            Thread.Sleep(150);
            if (before != IntPtr.Zero && before != discord.Handle) Steer(new OpenWindow(before, "", ""), "switch_to");
            return Shortcuts[row, 3];
        }

        private static OpenWindow FindDiscord()
        {
            foreach (OpenWindow window in Windows()) if (string.Equals(window.Program, "Discord", StringComparison.OrdinalIgnoreCase) || string.Equals(window.Program, "DiscordPTB", StringComparison.OrdinalIgnoreCase) || string.Equals(window.Program, "DiscordCanary", StringComparison.OrdinalIgnoreCase)) return window;
            return null;
        }

        private const string Machine = @"(?:(?:my|the|this|ur|your|our)\s+)?(?:pc|computer|laptop|system|machine|rig|windows|device)";
        private static readonly string[] PowerWords =
        {
            @"^(?:shut ?down|turn off|power (?:off|down)|switch off)\s+" + Machine + @"$|^(?:shut|turn|power|switch)\s+" + Machine + @"\s+(?:down|off)$", "shut_down",
            @"^(?:restart|reboot)\s+" + Machine + "$", "restart",
            @"^(?:put|send)\s+" + Machine + @"\s+to sleep$|^(?:sleep|hibernate)\s+" + Machine + @"$|^(?:make|let)\s+" + Machine + @"\s+(?:go to )?sleep$", "sleep",
            @"^lock\s+(?:" + Machine + @"|(?:(?:my|the|this)\s+)?(?:screen|desktop))$", "lock",
            @"^(?:sign|log) me (?:out|off)$|^(?:sign|log) (?:out|off)(?: of)?\s+(?:" + Machine + @"|(?:my )?windows account)$", "sign_out",
            @"^(?:cancel|stop|abort|undo)\s+(?:the |that |my )?(?:shut ?down|restart|reboot|sleep|sign ?out|log ?out)$|^(?:don'?t|do not)\s+(?:shut ?down|turn off|restart|reboot)(?:\s+(?:it|" + Machine + "))?$", "cancel"
        };
        private static DateTime powered = DateTime.MinValue;
        private static System.Threading.Timer powerLater;
        [DllImport("user32.dll")] private static extern bool LockWorkStation();
        [DllImport("user32.dll")] private static extern bool ExitWindowsEx(int flags, int reason);
        [DllImport("powrprof.dll")] private static extern bool SetSuspendState(bool hibernate, bool force, bool wakeEventsOff);

        private static string PowerFor(string text, out string minutes)
        {
            minutes = null;
            // Right after she starts shutting down, "no", "wait" or "stop" means cancel it.
            if ((DateTime.Now - powered).TotalSeconds < 20 && Regex.IsMatch(text, @"^(no+|nah|nope|wait|stop|cancel|abort|undo|don'?t|do not|never ?mind|hold on|hold up)\b", RegexOptions.IgnoreCase)) return "cancel";
            Match later = Regex.Match(text, @"\s+in\s+(\d{1,4}|an?|one)\s*(min(ute)?s?|h(ou)?rs?)$", RegexOptions.IgnoreCase);
            string asked = later.Success ? text.Substring(0, later.Index) : text;
            for (int i = 0; i < PowerWords.Length; i += 2)
            {
                if (!Regex.IsMatch(asked, PowerWords[i], RegexOptions.IgnoreCase)) continue;
                if (later.Success)
                {
                    int count = Regex.IsMatch(later.Groups[1].Value, @"^(an?|one)$", RegexOptions.IgnoreCase) ? 1 : int.Parse(later.Groups[1].Value);
                    minutes = (later.Groups[2].Value.StartsWith("h", StringComparison.OrdinalIgnoreCase) ? count * 60 : count).ToString();
                }
                return PowerWords[i + 1];
            }
            return null;
        }

        private static string Turn(string action, string minutes)
        {
            int later;
            if (!int.TryParse((minutes ?? "").Trim(), out later) || later < 0) later = 0;
            later = Math.Min(later, 1440);
            if (action == "cancel")
            {
                powered = DateTime.MinValue;
                return Power("cancel", 0) ? "Cancelled it. Your PC stays on." : "Nothing was shutting down, so there's nothing to cancel.";
            }
            int seconds = later > 0 ? later * 60 : action == "shut_down" || action == "restart" ? 10 : action == "sign_out" ? 5 : action == "sleep" ? 4 : 0;
            if (!Power(action, seconds)) return action == "shut_down" ? "Couldn't shut your PC down." : action == "restart" ? "Couldn't restart your PC." : action == "sleep" ? "Couldn't put your PC to sleep." : action == "lock" ? "Couldn't lock your PC." : "Couldn't sign you out.";
            if (later == 0 && action != "lock") powered = DateTime.Now;
            string when = later == 0 ? null : later % 60 == 0 ? (later / 60) + (later == 60 ? " hour" : " hours") : later + (later == 1 ? " minute" : " minutes");
            string stop = when == null ? " Say cancel to stop it." : " Say cancel the shutdown to stop it.";
            if (action == "shut_down") return "Shutting your PC down in " + (when ?? "10 seconds") + "." + stop;
            if (action == "restart") return "Restarting your PC in " + (when ?? "10 seconds") + "." + stop;
            if (action == "sign_out") return "Signing you out in " + (when ?? "5 seconds") + "." + stop;
            if (action == "sleep") return when == null ? "Putting your PC to sleep." : "Putting your PC to sleep in " + when + ".";
            return when == null ? "Locked your PC." : "Locking your PC in " + when + ".";
        }

        private static bool Switch(string action, int seconds)
        {
            if (action == "cancel")
            {
                bool stopped;
                lock (gate) { stopped = powerLater != null; if (stopped) powerLater.Dispose(); powerLater = null; }
                return Shutdown("/a") || stopped;
            }
            if (action == "shut_down" || action == "restart") return Shutdown((action == "restart" ? "/r" : "/s") + " /t " + seconds);
            if (action == "lock" && seconds == 0) return LockWorkStation();
            // Sleep, sign out and timed locks wait a moment so her line plays first.
            System.Threading.Timer timer = null;
            timer = new System.Threading.Timer(delegate
            {
                lock (gate) { if (powerLater != timer) return; powerLater = null; }
                if (action == "lock") LockWorkStation();
                else if (action == "sign_out") ExitWindowsEx(0, 0);
                else if (!SetSuspendState(false, false, false)) PostMessage(new IntPtr(0xFFFF), 0x0112, new IntPtr(0xF170), new IntPtr(2));
            }, null, Timeout.Infinite, Timeout.Infinite);
            lock (gate) { if (powerLater != null) powerLater.Dispose(); powerLater = timer; }
            timer.Change(seconds * 1000, Timeout.Infinite);
            return true;
        }

        private static bool Shutdown(string flags)
        {
            try
            {
                using (Process run = Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "shutdown.exe"), flags) { UseShellExecute = false, CreateNoWindow = true }))
                    return run != null && run.WaitForExit(5000) && run.ExitCode == 0;
            }
            catch (System.ComponentModel.Win32Exception) { return false; }
        }

        private static void PressChord(byte[] keys)
        {
            foreach (byte key in keys) Native.keybd_event(key, 0, key == 0x5B ? 0x1 : 0, UIntPtr.Zero);
            for (int i = keys.Length - 1; i >= 0; i--) Native.keybd_event(keys[i], 0, (keys[i] == 0x5B ? 0x1 : 0) | 0x2, UIntPtr.Zero);
        }

        private static void Tap(byte key)
        {
            Native.keybd_event(key, 0, 0x1, UIntPtr.Zero);
            Native.keybd_event(key, 0, 0x1 | 0x2, UIntPtr.Zero);
        }

        private static readonly string[,] KnownSongs =
        {
            { @"^in love with myself(\s+(by|from)\s+(petite meller|kalman chodakiewitz|eli shiffman|(cyberpunk:?\s+)?edgerunners)\b.*)?$", "2iyaHQlYrR8Eyr4wOmOT5D", "In Love With Myself", "In Love With Myself Petite Meller" },
            { @"^(i really want to stay at your house|irwtsayh)(\s+(by|from)\s+(rosa walton|hallie coggins|(cyberpunk:?\s+)?edgerunners)\b.*)?$", "42W7fJXB7razFefTxMSyYp", "I Really Want to Stay at Your House", "I Really Want to Stay at Your House Rosa Walton" },
            { @"^chippin(g|'|\u2019)?\s+in(\s+(by|from)\s+(samurai|refused|johnny silverhand|cyberpunk(\s+2077)?)\b.*)?$", "0UHxkkgI5GfV0XBRA1rqfe", "Chippin' In", "Chippin' In SAMURAI Refused" },
            { @"^never fade away(\s+(by|from)\s+(samurai|refused|p\.?\s*t\.?\s+adamczyk|olga jankowska|cyberpunk(\s+2077)?)\b.*)?$", "6pV1lc8lIc8ARNB6KsZukt", "Never Fade Away", "Never Fade Away SAMURAI Cyberpunk 2077" },
            { @"^black dog\s+(by|from)\s+(samurai|cyberpunk(\s+2077)?)\b.*$", "5Ln8eNkYC9to99zsPEZbS2", "Black Dog", "Black Dog SAMURAI Cyberpunk 2077" },
            { @"^let you down\s+(by|from)\s+(dawid podsiad[l\u0142]o|(cyberpunk:?\s+)?edgerunners)\b.*$", "6Efhl5xyn7MsuGhyboRThX", "Let You Down", "Let You Down Dawid Podsiadlo Edgerunners" },
            { @"^resist and disorder(\s+(by|from)\s+(rezodrone|cyberpunk(\s+2077)?)\b.*)?$", "4onvDep8OyHKT85RBO3b4V", "Resist and Disorder", "Resist and Disorder Rezodrone" },
            { @"^the rebel path(\s+(by|from)\s+(p\.?\s*t\.?\s+adamczyk|cyberpunk(\s+2077)?)\b.*)?$", "3m8UfZwPMMbiWfGR8Lg2cR", "The Rebel Path", "The Rebel Path Cyberpunk 2077" }
        };

        private static readonly string[] Ost = { "Chippin' In", "Never Fade Away", "Black Dog by SAMURAI", "I Really Want to Stay at Your House", "Let You Down by Dawid Podsiad\u0142o", "Resist and Disorder", "The Rebel Path", "In Love With Myself" };
        internal static Func<int, int> Roll = new Random().Next;

        internal static string Pick() { return Ost[Roll(Ost.Length)]; }

        private static readonly Regex Moon = new Regex(@"\bwish\s+(?:we|i|you\s+and\s+i|me\s+and\s+you)\s+could\s+(?:go|fly|get|run\s+away)\s+to\s+the\s+moon\b", RegexOptions.IgnoreCase);
        internal const string MoonSong = "I Really Want to Stay at Your House";

        internal const string Home = "lachydotmcg.github.io/capri-desktop";

        private static string PlayMusic(string song, string app, string kind)
        {
            if (string.IsNullOrWhiteSpace(song)) return "What should I play?";
            song = song.Trim();
            if (Regex.IsMatch(song, @"^((what('|\u2019)?s|what is)\s+)?((currently|now)\s+)?playing( now)?$|^(this|that|the current) (song|track)$", RegexOptions.IgnoreCase)) return Playing();
            if (Regex.IsMatch(song, @"^(play\s+)?(me\s+)?((some|any|a|random|good)\s+)?(music|songs?|tunes|tracks?|something)(\s+for me)?$", RegexOptions.IgnoreCase)) { song = Pick(); kind = "song"; }
            if (Safety.Stops(song) != null) return "Not playing that, choom.";
            Match tail = Regex.Match(song, @"^(?:(?:the|my|your)\s+)?(.+?)\s+(playlist|album)$", RegexOptions.IgnoreCase);
            if (tail.Success && kind != "artist") { kind = tail.Groups[2].Value.ToLowerInvariant(); song = tail.Groups[1].Value; }
            bool liked = Regex.IsMatch(song, @"^((my|your|the)\s+)?((liked|saved)\s+songs|likes)$", RegexOptions.IgnoreCase);
            if (liked) kind = "playlist";
            if (kind != "playlist" && kind != "album" && kind != "artist") kind = "song";
            string known = null;
            string look = song;
            if (kind == "song")
                for (int i = 0; i < KnownSongs.GetLength(0); i++)
                    if (Regex.IsMatch(song, KnownSongs[i, 0], RegexOptions.IgnoreCase)) { known = KnownSongs[i, 1]; song = KnownSongs[i, 2]; look = KnownSongs[i, 3]; }
            string named = kind == "song" ? song : "the " + song + " " + kind;
            if (app != "youtube" && Match(Apps(), "spotify").Count > 0)
            {
                if (liked) named = "their Liked Songs";
                string spotted = liked ? null : known ?? Find(Fetch("https://html.duckduckgo.com/html/?q=" + Uri.EscapeDataString("site:open.spotify.com/" + (kind == "song" ? "track" : kind) + " " + song)), @"open\.spotify\.com(?:/|%2F)" + (kind == "song" ? "track" : kind) + @"(?:/|%2F)([A-Za-z0-9]{22})");
                if (kind == "song" && spotted != null)
                {
                    string before = NowPlaying();
                    Run("spotify:track:" + spotted);
                    return Started(before) || PressPlay(song) ? "Playing " + song + " on Spotify." : "Opened " + song + " in Spotify, but couldn't press play. Hit the green Play button.";
                }
                if (liked || spotted != null)
                {
                    Run(liked ? "spotify:collection:tracks" : "spotify:" + kind + ":" + spotted);
                    return PressPlay(liked ? "Liked Songs" : song) ? "Playing " + named + " on Spotify." : "Opened " + named + " in Spotify, but couldn't press play. Hit the green Play button.";
                }
                Run("spotify:search:" + Uri.EscapeDataString(song));
                return PressPlay(song) ? "Playing " + named + " on Spotify." : "Couldn't find " + named + " to play, so Spotify is open on a search for it.";
            }
            string where = Searches[0, 1] + Uri.EscapeDataString(look);
            System.Text.RegularExpressions.Match list = Regex.Match(kind == "song" ? "" : Fetch(where + "&sp=EgIQAw%253D%253D") ?? "", @"""watchEndpoint"":\{""videoId"":""([A-Za-z0-9_-]{11})"",""playlistId"":""([A-Za-z0-9_-]+)""");
            string video = list.Success ? null : Find(Fetch(where), "\"videoId\":\"([A-Za-z0-9_-]{11})\"");
            if (!list.Success && video == null) { Run(where); return "Couldn't find " + named + " to play, so YouTube is open on a search for it."; }
            Run(list.Success ? "https://www.youtube.com/watch?v=" + list.Groups[1].Value + "&list=" + list.Groups[2].Value : "https://www.youtube.com/watch?v=" + video);
            return (app == "spotify" ? "Spotify isn't installed, so playing " : "Playing ") + named + " on YouTube.";
        }

        private static bool ClickPlay(string name)
        {
#if CROSS
            return false;
#else
            for (int i = 0; i < 30; i++)
            {
                Thread.Sleep(500);
                foreach (OpenWindow window in Windows())
                {
                    if (!string.Equals(window.Program, "Spotify", StringComparison.OrdinalIgnoreCase) || window.Handle == IntPtr.Zero) continue;
                    AutomationElement close = null;
                    int best = 0;
                    foreach (AutomationElement button in AutomationElement.FromHandle(window.Handle).FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button)))
                    {
                        int score = PlayScore(button.Current.Name, name);
                        if (score <= best) continue;
                        close = button;
                        best = score;
                        if (best == 3) break;
                    }
                    object pattern;
                    if (close == null || (i < 6 && best == 1) || !close.TryGetCurrentPattern(InvokePattern.Pattern, out pattern)) continue;
                    ((InvokePattern)pattern).Invoke();
                    return true;
                }
            }
            return false;
#endif
        }

        internal static int PlayScore(string label, string name)
        {
            Match play = Regex.Match(label ?? "", @"^(?:Play|Lire)\s+(.+)$", RegexOptions.IgnoreCase);
            if (!play.Success) return 0;
            string[] shown = Words(play.Groups[1].Value);
            string[] want = Words(name);
            bool shorter = Within(shown, want);
            bool longer = Within(want, shown);
            return shorter && longer ? 3 : shorter ? 2 : longer ? 1 : 0;
        }

        private static string[] Words(string text)
        {
            string plain = Plain(Regex.Replace(text ?? "", @"\s+(by|par)\s+.*$", "", RegexOptions.IgnoreCase));
            return (plain == "titres likes" ? "liked songs" : plain).Split(' ');
        }

        private static bool Within(string[] part, string[] whole)
        {
            if (part.Length == 0 || part[0].Length == 0) return false;
            foreach (string word in part) if (Array.IndexOf(whole, word) < 0) return false;
            return true;
        }

        private static string SpotifyTitle()
        {
            string title = "";
            foreach (OpenWindow window in Windows())
                if (string.Equals(window.Program, "Spotify", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(window.Title))
                {
                    if (TrackTitle(window.Title)) return window.Title;
                    title = window.Title;
                }
            return title;
        }

        private static bool TrackTitle(string title) { return Regex.IsMatch(title ?? "", @"^\S.* - \S") && !Regex.IsMatch(title, @"^Spotify(?:$|\s)| - Spotify(?:$|\s)", RegexOptions.IgnoreCase); }

        internal static bool Started(string before, string title) { return title != before && TrackTitle(title); }

        private static bool Started(string before)
        {
            for (int i = 0; i < 24; i++)
            {
                string title = NowPlaying();
                if (Started(before, title)) return true;
                Thread.Sleep(500);
            }
            return false;
        }

        private static string Find(string page, string pattern)
        {
            Group found = Regex.Match(page ?? "", pattern).Groups[1];
            return found.Success ? found.Value : null;
        }

        private static string Download(string address)
        {
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(address);
            request.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36";
            request.Headers["Accept-Language"] = "en";
            request.Timeout = 8000;
            request.ReadWriteTimeout = 8000;
            try
            {
                using (WebResponse response = request.GetResponse())
                using (StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8)) return reader.ReadToEnd();
            }
            catch (WebException) { return ""; }
            catch (IOException) { return ""; }
        }

        private static string Window(string wanted, string action)
        {
            if (action != "switch_to" && action != "minimize" && action != "close") return "There's no window action called " + action + ".";
            string want = Regex.Replace(Plain(wanted), @"^the |( app| application| game| tab| window)$", "");
            if (want.Length == 0) return "Which app, choom?";
            wanted = wanted.Trim();
            List<OpenWindow> top = new List<OpenWindow>();
            int best = 0;
            foreach (OpenWindow window in Windows())
            {
                int score = Math.Max(Score(Plain(window.Program), want), Score(Plain(window.Title), want));
                if (score == 0 || score < best) continue;
                if (score > best) { best = score; top.Clear(); }
                top.Add(window);
            }
            if (top.Count == 0) return wanted + " isn't open.";
            // When several apps match, the one whose own name matches goes first, then the one nearest the front.
            OpenWindow first = top.Find(window => Score(Plain(window.Program), want) == best) ?? top[0];
            top = top.FindAll(window => window.Program == first.Program);
            if (action == "switch_to") return Steer(top[0], action) ? "Switched to " + wanted + "." : "Couldn't bring " + wanted + " to the front.";
            if (action == "close" && Array.IndexOf(Browsers, top[0].Program.ToLowerInvariant()) >= 0 && Score(Plain(top[0].Program), want) == 0)
                return Steer(top[0], "close_tab") ? "Closed the " + wanted + " tab." : "Couldn't reach the " + wanted + " tab to close it.";
            bool all = true;
            foreach (OpenWindow window in top) all &= Steer(window, action);
            if (action == "minimize") return "Minimized " + wanted + ".";
            return all ? "Closed " + wanted + "." : "Asked " + wanted + " to close, but it's still open. It might be asking you to save.";
        }

        private static List<OpenWindow> ReadWindows()
        {
            List<OpenWindow> found = new List<OpenWindow>();
            Dictionary<int, string> programs = new Dictionary<int, string>();
            int own = Process.GetCurrentProcess().Id;
            Visit visit = delegate(IntPtr window, IntPtr data)
            {
                int cloaked;
                if (!IsWindowVisible(window) || Native.GetWindow(window, 4) != IntPtr.Zero || (Native.GetWindowLong(window, -20) & 0x80) != 0) return true;
                if (DwmGetWindowAttribute(window, 14, out cloaked, 4) == 0 && cloaked != 0) return true;
                int length = GetWindowTextLength(window);
                if (length == 0) return true;
                StringBuilder kind = new StringBuilder(64);
                GetClassName(window, kind, kind.Capacity);
                if (Array.IndexOf(Shell, kind.ToString()) >= 0) return true;
                int id;
                GetWindowThreadProcessId(window, out id);
                if (id == own) return true;
                string program;
                if (!programs.TryGetValue(id, out program))
                {
                    try { using (Process process = Process.GetProcessById(id)) program = process.ProcessName; }
                    catch (ArgumentException) { program = ""; }
                    catch (InvalidOperationException) { program = ""; }
                    programs[id] = program;
                }
                StringBuilder title = new StringBuilder(length + 1);
                GetWindowText(window, title, title.Capacity);
                found.Add(new OpenWindow(window, title.ToString(), program));
                return true;
            };
            EnumWindows(visit, IntPtr.Zero);
            GC.KeepAlive(visit);
            return found;
        }

        private static bool Apply(OpenWindow window, string action)
        {
            IntPtr handle = window.Handle;
            if (action == "minimize") { ShowWindow(handle, 6); return true; }
            if (action == "close")
            {
                PostMessage(handle, 0x0010, IntPtr.Zero, IntPtr.Zero);
                for (int i = 0; i < 20 && IsWindow(handle); i++) Thread.Sleep(100);
                return !IsWindow(handle);
            }
            if (!Front(handle)) return false;
            if (action != "close_tab") return true;
            Native.keybd_event(0x11, 0, 0, UIntPtr.Zero);
            Native.keybd_event(0x57, 0, 0, UIntPtr.Zero);
            Native.keybd_event(0x57, 0, 0x2, UIntPtr.Zero);
            Native.keybd_event(0x11, 0, 0x2, UIntPtr.Zero);
            return true;
        }

        internal static bool Front(IntPtr handle)
        {
            if (IsIconic(handle)) ShowWindow(handle, 9);
            int ignored;
            int mine = GetCurrentThreadId();
            int theirs = GetWindowThreadProcessId(Native.GetForegroundWindow(), out ignored);
            bool attached = theirs != 0 && theirs != mine && AttachThreadInput(mine, theirs, true);
            BringWindowToTop(handle);
            SetForegroundWindow(handle);
            if (attached) AttachThreadInput(mine, theirs, false);
            for (int i = 0; i < 10 && Native.GetForegroundWindow() != handle; i++) Thread.Sleep(50);
            return Native.GetForegroundWindow() == handle;
        }
    }
}
