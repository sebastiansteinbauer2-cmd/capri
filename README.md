> **This is a safety-modified copy of [lachydotmcg/capri-desktop](https://github.com/lachydotmcg/capri-desktop)**,
> used for a personal, non-commercial hobby project under the original
> [PolyForm Noncommercial License 1.0.0](LICENSE.md).
> Required Notice: Copyright 2026 Lachy (https://github.com/lachydotmcg)
>
> **What's changed in this copy** (branch history shows every line):
> - **Blackwall mode is locked off.** The switch can't be turned on, and a saved "on" is ignored.
> - **Risky PC jobs ask first.** Anything that sends, posts, buys, pays, deletes, installs,
>   ends a call or shuts down the PC waits for a clear "yes".
> - **Her own "want me to…?" questions reach you** instead of being overridden.
> - **Self-updates are off**, so an update can't replace this build. Build it yourself
>   from source with `ci/build-windows.ps1`.
>
> The download links below point to the original project's releases, **not** to this safe build.
> The original author is not responsible for these changes.

<p align="center">
  <img src="assets/chip-source.png" alt="Chip" width="40%" align="middle">
  <img src="assets/capri.png" alt="Capri" width="40%" align="middle">
</p>

<p align="center">
  Bring Capri (back lol) to life as a desktop companion! Inspired by jpartnma_'s tiktok video.
</p>

<p align="center">
  <a href="https://github.com/lachydotmcg/capri-desktop/releases/latest/download/Capri-Setup.exe"><b>Download for Windows</b></a>
  &nbsp;·&nbsp;
  <a href="#mac-and-linux-early-preview">Mac and Linux</a>
  &nbsp;·&nbsp;
  <a href="https://lachydotmcg.github.io/capri-desktop/setup/">Setup guide</a>
  &nbsp;·&nbsp;
  <a href="https://lachydotmcg.github.io/capri-desktop/">Website</a>
  &nbsp;·&nbsp;
  <a href="https://discord.gg/F4fDHfGtFk">Discord</a>
</p>

<table align="center">
  <tr>
    <td align="center"><img src="assets/readme/idle.webp" alt="Capri idling and blinking" width="150"><br><sub>Hanging out</sub></td>
    <td align="center"><img src="assets/readme/talk.webp" alt="Capri talking" width="150"><br><sub>Talking</sub></td>
    <td align="center"><img src="assets/readme/tossed.webp" alt="Capri flailing while dragged" width="150"><br><sub>Getting tossed</sub></td>
    <td align="center"><img src="assets/readme/glitch.webp" alt="Capri glitching" width="150"><br><sub>Glitching</sub></td>
    <td align="center"><img src="assets/readme/sleep.webp" alt="Capri asleep, then booting back up" width="150"><br><sub>Napping</sub></td>
  </tr>
</table>

Beware that Capri may try to take over your human CPU. We take no responsibility for her actions.

## What she does

- **Talks back.** Type to her and she answers in her own words.
- **Talks out loud.** Add an ElevenLabs key and she speaks, or voice chat with her.
- **Does things for you.** Ask her to open apps, play music or close a tab.
- **Watches you play.** Turn it on and she comments on your games.
- **Sits on your stuff.** Drop her on a window, an icon or the bottom of your screen.

<p align="center">
  <img src="assets/readme/bubble.png" alt="Capri's speech box with the pencil and wave buttons" width="31%">
  <img src="assets/readme/sit.png" alt="Capri sitting on a window" width="31%">
  <img src="assets/readme/mainframe.png" alt="The Capri Mainframe" width="22%">
</p>

## What you need

- Windows 10 or 11 (64-bit). Nothing else to install.
- A mic, if you want to voice chat.

| | Brain on your PC | Brain from a key (like the free NVIDIA one) |
| --- | --- | --- |
| Memory | 8 GB, 16 GB is better | 4 GB |
| Graphics card | Optional, but 4 GB or more makes her faster | Not needed |
| Space | 3 to 5 GB | Under 100 MB |
| Internet | Only for the download | Always |

## Getting started

1. Download **Capri-Setup.exe** and run it
2. In the Capri Mainframe, press **Set up her brain**, or press **Free key** to use a free NVIDIA key
3. Optional: paste an ElevenLabs key so she can talk. Make the key with **Restrict key** turned off

Stuck? The [setup guide](https://lachydotmcg.github.io/capri-desktop/setup/) walks through it, or ask in the [Discord](https://discord.gg/F4fDHfGtFk).

Windows or your antivirus may warn you because it's a new app. All the code is here.

## Mac and Linux (early preview)

She sits on your windows and chats, talks with an ElevenLabs key, opens apps and controls your music. The Mainframe, voice chat and jobs with her own cursor are Windows-only for now.

1. Download [Mac (Apple Silicon)](https://github.com/lachydotmcg/capri-desktop/releases/latest/download/Capri-mac-apple-silicon.zip), [Mac (Intel)](https://github.com/lachydotmcg/capri-desktop/releases/latest/download/Capri-mac-intel.zip) or [Linux](https://github.com/lachydotmcg/capri-desktop/releases/latest/download/Capri-linux-x64.tar.gz)
2. Install [Ollama](https://ollama.com), then run `ollama pull gemma4:e4b` for her brain
3. **Mac:** unzip it, drag Capri into Applications, then open Terminal and run this once. Needs macOS 15 or newer
   ```bash
   xattr -dr com.apple.quarantine /Applications/Capri.app
   ```
   Apple hasn't approved (notarized) her yet, so macOS blocks her or calls her "damaged" until you do. That line only clears the downloaded-from-the-internet flag on Capri
4. **Linux:** unpack it and run `Capri/Capri`. She sits on windows on X11; on Wayland she sits on the bottom of your screen

## How to use her

- **Pencil:** type to her
- **Wave:** voice chat
- **Drag her:** sit her on a window
- **Right-click her** or the tray icon: menu and settings

Just ask her for things, like "open Spotify", "play Blinding Lights", "pause", "what's playing?" or "close Discord".

<details>
<summary>All controls</summary>

| Control | What it does |
| --- | --- |
| Shift + drop | sit her right where you drop her |
| Ctrl + drop | she hangs on; click her to let go |
| Mic button | mute your mic in voice chat |
| Ctrl + Alt + V | start or stop voice chat (click its hint in the Mainframe to change it) |
| Ctrl + Alt + N | mute your mic (click its hint in the Mainframe to change it) |
| Ctrl + Alt + T | open her typing box from anywhere |
| Ctrl + Alt + H | hide or show her |
| Esc | stop a job she's doing |
| Ctrl + Alt + C | get her off your cursor |
| Right-click, Move | she hops between your open windows |
| Watch my screen | in the Mainframe: she comments as you play |
| FREE VOICE / ELEVENLABS | in the Mainframe under VOICE: FREE VOICE runs on your PC and costs nothing; ELEVENLABS sounds amazing and uses your ElevenLabs credits |
| Help | in the Mainframe: opens the help page |

</details>

## Make her yours

Who she is and how she talks lives in `personality.md`, a plain text file you can rewrite. She's open source, so make her whoever you like.

1. In the Capri Mainframe, press **Settings**, then **Edit personality**. It opens in Notepad
2. Rewrite anything under the note at the top, then save
3. Her next reply uses it. No restart

- **Where it lives:** `%LOCALAPPDATA%\Capri\personality.md` on Windows, `~/.config/Capri/personality.md` on Mac and Linux (right-click her, Settings, Edit her personality)
- **Capri's own rules stay in the app:** reply length, language, what she can do on your PC and her safety rules come after your text, so you don't need to write them
- **Reset** puts her default back and keeps yours as `personality.backup.md`
- Her default is `DefaultPersonality` in [app/Brain.cs](app/Brain.cs), so a pull request can change her for everyone

## Roadmap

**Her personality**

- [x] A playful, teasing chaos gremlin again who's openly fond of you, not a hype bot
- [x] Saying hi in her own voice when she starts
- [x] Head pats (smh): rub your cursor back and forth over her
- [x] Check in on me (on by default): every so often she glitches in on whatever you're doing
- [x] With Check in on me on, she asks for your attention now and then when you ignore her
- [x] Your own Capri: her personality in a file you can open from the Mainframe and change
- [ ] A charm meter: how fond of you she is, growing as you spend time with her
- [ ] Mischief mode: screen glitches, stealing your cursor, "you've been hacked" notes

**Chatting and her brain**

- [x] Typing to her, with replies in her speech bubble
- [x] A brain on your PC that sets itself up, or Ollama if you have it
- [x] A free NVIDIA key, or your own ChatGPT, Claude or DeepSeek key
- [x] Three brain tiers: NORMAL, HIGHER and MAX INTELLIGENCE
- [x] Remembering things about you, in her notes (me.md)
- [x] Keeping your chat for up to a week
- [x] Calling you by a nickname, and replying in 20 languages
- [x] Saying in plain words when your PC is short on RAM or storage
- [x] Watching your screen and commenting on your games
- [x] Looking at your screen when you say "check my screen"
- [ ] A DeepSeek brain that can see your screen

**Her voice**

- [x] Talking out loud with an ElevenLabs key
- [x] Voice chat, with your pick of mic and a mute button
- [x] A free voice that runs on your PC
- [x] Free voice chat, with her own offline ears for English
- [x] Turning her voice off, so she just types
- [x] Muting your mic pauses ElevenLabs voice chat
- [x] ElevenLabs voice chat stops asking if you're still there when you go quiet or mute
- [ ] An optional bigger voice for strong PCs (Qwen3-TTS 1.7B, or other voice models). Kokoro stays the default
- [ ] Sliders for her free voice: pitch, the glitch effect and more, so you can tune how she sounds

**Jobs on your PC**

- [x] Opening apps, games and websites
- [x] Playing music on Spotify or YouTube: pause, skip, volume and "what's playing?"
- [x] Switching to, minimizing or closing your apps and tabs
- [x] Pressing Discord and Windows shortcuts, like mute, deafen and screenshot
- [x] Jobs that take a few steps: clicking, typing and scrolling in your apps
- [x] Her own cursor that she moves around your screen
- [x] Pulling things up on Google when she doesn't know
- [x] Following through on jobs: she does what she says, or tells you she hasn't
- [x] Saying what she did, instead of "nice, you opened Notepad"
- [x] Doing what you ask without asking first, and picking the song or video herself when you leave it to her
- [x] Opening as many apps as you name in one ask, like "open Discord, Spotify and Steam"
- [x] Use her cursor, on by default: she searches YouTube and picks a video, plays from Spotify's search and drags its volume slider herself
- [x] Blackwall mode (was Bypass all permissions): a switch so she won't ask twice when you ask her to do something, even to buy or delete
- [ ] Controlling YouTube: skip and fullscreen
- [ ] Looking things up and telling you the answer
- [ ] Working with your files: open, read and edit the ones you pick
- [ ] Better hands: a stronger computer-use harness, with open-computer-use as a reference
- [ ] Planning a job before she starts (with stronger brains), with the plan shown in her task panel
- [ ] Jobs that run for hours, on a schedule: full control of your PC while you're away, like answering your Discord DMs while you sleep

**Life on your desktop**

- [x] Blinking, glitching and napping when you're away
- [x] Moving her mouth when she talks
- [x] Flailing and frowning when you pick her up, and glitching when you toss her
- [x] Beaming in with crimson glitch tendrils when she starts
- [x] Sitting on app windows, desktop icons and the bottom of your screen
- [x] Sitting or hanging anywhere: hold Shift or Ctrl when you drop her
- [x] Riding your cursor
- [x] Wandering between your open windows
- [x] Dancing to the beat when music plays
- [x] Staying on top of your games
- [x] Sizes from Tiny to Giant
- [x] A tray icon and a right-click menu

**Mac and Linux**

- [x] An early preview: she sits on your windows and chats through Ollama
- [x] Her ElevenLabs voice, wandering and dancing
- [x] Opening apps and sites, and controlling your music
- [ ] The Mainframe
- [ ] Voice chat and her free voice
- [ ] Her built-in brain and brains from a key, not just Ollama
- [ ] Jobs with her own cursor, and switching or closing windows
- [ ] Watching your screen
- [ ] Updates that install themselves
- [ ] Apple approving (notarizing) her, so macOS stops blocking her
- [ ] Everything else Windows has, so they stop being an early preview

**Setup and updates**

- [x] One installer with everything in it, no admin rights needed
- [x] The Capri Mainframe for her brain, voice and settings
- [x] A Start with Windows switch, on by default
- [x] Updates that install themselves
- [x] Changing her keys from a KEYS row in Settings: talk, mute mic, type to her and hide/show
- [x] A setup guide, a Help page and Copy error details for the Discord
- [x] Help from Capri herself in the corner of the website
- [ ] Changing her hop-off key (Ctrl + Alt + C) too
- [ ] The Mainframe fitting any screen shape and scaling, like Virtual Super Resolution
- [ ] Code signing, so Windows stops warning you about her

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for how to build her, test her and send a pull request.

## Licence

Free for anything non-commercial: use her, share her, change her. No selling her or putting her in paid products. Full terms in [LICENSE.md](LICENSE.md) (PolyForm Noncommercial 1.0.0).

Have fun and more updates to come!

<sub>Setup guide based on Dancer's guide in the Discord. Font: Share Tech Mono (OFL). Free voice: Kokoro-82M (Apache-2.0). Her own ears: [NVIDIA Parakeet TDT 0.6B v2](https://huggingface.co/nvidia/parakeet-tdt-0.6b-v2) for English and [v3](https://huggingface.co/nvidia/parakeet-tdt-0.6b-v3) for 25 European languages ([CC-BY-4.0](https://creativecommons.org/licenses/by/4.0/)), int8 exports by sherpa-onnx. Both run with [sherpa-onnx](https://github.com/k2-fsa/sherpa-onnx) (Apache-2.0).</sub>
