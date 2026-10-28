# J.A.R.V.I.S. – osobní AI asistent

Hlasový asistent ve stylu JARVISe z Iron Mana, který mluví česky. Stačí říct
**„Jarvisi, …"** a odpoví hlasem.

Projekt vzniká postupně jako výuka programování v Pythonu.

## Co umí
- 🎤 poslouchá na slovo „Jarvisi" (rozpoznávání řeči Whisper běží na vlastním PC)
- 🧠 přemýšlí přes Claude API (model Claude Haiku 5.5)
- 🔊 odpovídá hlasem po větách (edge-tts)
- ☀️ zjistí počasí (Open-Meteo, zdarma)
- 🧠 dlouhodobá paměť v souboru `pamet.txt`
- ☀️ ranní briefing (počasí + co tě dnes čeká)
- 📓 večerní shrnutí dne do deníku, v neděli týdenní shrnutí
- 📚 knihovna – co jsi četl/viděl a co ses naučil
- 📁 čte dokumenty (txt, md, csv, pdf, docx) – jen ze složky `dokumenty`
- 📸 na požádání se podívá na obrazovku a poradí s tím, co na ní je

Paměť, deník, knihovna i dokumenty zůstávají jen na tvém PC a na GitHub se nenahrávají.
- 🖥️ okno s animovaným reaktorem, titulky a počítadlem ceny

## Soubory
| Soubor | Co dělá |
|---|---|
| `jarvis_app.py` | aplikace s oknem – hlavní věc, tu spouštěj |
| `ui.html` | vzhled okna |
| `jarvis.py` | „mozek" – komunikace s Claudem, paměť, osobnost (jde spustit i samostatně jako textový chat) |
| `jarvis_hlas.py` | „uši a pusa" – Whisper a hlas (jde spustit i samostatně) |
| `nastroje.py` | nástroje, které JARVIS umí použít (počasí) |

## Spuštění
1. Nainstaluj [Python 3.12](https://www.python.org/downloads/)
2. Nainstaluj knihovny:
   ```
   pip install -r requirements.txt
   ```
3. Vytvoř si API klíč na [console.anthropic.com](https://console.anthropic.com) a ulož ho do Windows
   (klíč nikdy nedávej do kódu):
   ```
   setx ANTHROPIC_API_KEY "tvuj-klic"
   ```
4. V novém terminálu spusť:
   ```
   python jarvis_app.py
   ```

## Bezpečnostní pravidla
- API klíč je jen v proměnné prostředí, nikdy v kódu
- nic nemaže, neplatí, neposílá ani nespouští bez výslovného potvrzení
- přístup k souborům jen do jedné vybrané složky (připravuje se)

## Plán
- [x] Textový chat
- [x] Hlas
- [x] Probouzecí slovo „Jarvisi"
- [ ] Kamera – „podívej se"
- [ ] Režim učitele – kontrola matematiky
- [ ] Soubory a stahování
- [ ] Email a škola
