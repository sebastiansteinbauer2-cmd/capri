# CAPRI – osobní AI parťačka

Hlasová AI parťačka v kyberpunkovém stylu, která mluví anglicky (rozumí i česky). Stačí říct
**„Capri, …"** a odpoví hlasem.

Projekt vzniká postupně jako výuka programování v Pythonu.

## Co umí
- 🎤 poslouchá na slovo „Capri" (rozpoznávání řeči Whisper běží na vlastním PC)
- 🧠 přemýšlí přes Claude API (model Claude Haiku 5.5)
- 🔊 odpovídá anglicky ženským hlasem po větách (edge-tts, záloha Piper)
- ☀️ zjistí počasí (Open-Meteo, zdarma)
- 🧠 dlouhodobá paměť v souboru `pamet.txt`
- ☀️ ranní briefing (počasí + co tě dnes čeká)
- 📓 večerní shrnutí dne do deníku, v neděli týdenní shrnutí
- 📚 knihovna – co jsi četl/viděl a co ses naučil
- 📁 čte dokumenty (txt, md, csv, pdf, docx) – jen ze složky `dokumenty`
- 📸 na požádání se podívá na obrazovku a poradí s tím, co na ní je
- 🖥️ okno v kyberpunkovém stylu s animovanou postavičkou, titulky a počítadlem ceny
- 🚶 malá Capri chodí po ploše - klikni na ni a otevře se menu

Paměť, deník, knihovna i dokumenty zůstávají jen na tvém PC a na GitHub se nenahrávají.


## Soubory
| Soubor | Co dělá |
|---|---|
| `capri_app.py` | aplikace s oknem – hlavní věc, tu spouštěj |
| `ui.html` | vzhled hlavního okna |
| `mazlicek.html` | malá Capri na ploše |
| `postava.svg` | postavička Capri (používají ji obě okna) |
| `capri.py` | „mozek" – komunikace s Claudem, paměť, osobnost (jde spustit i samostatně jako textový chat) |
| `capri_hlas.py` | „uši a pusa" – Whisper a hlas (jde spustit i samostatně) |
| `nastroje.py` | nástroje, které Capri umí použít (počasí, paměť, deník, dokumenty, obrazovka) |
| `vykon.py` | ať Windows Capri na pozadí nezpomaluje |

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
   python capri_app.py
   ```

## Bezpečnostní pravidla
- API klíč je jen v proměnné prostředí, nikdy v kódu
- nic nemaže, neplatí, neposílá ani nespouští bez výslovného potvrzení
- přístup k souborům jen do jedné složky (`dokumenty`), a jen ke čtení

## Plán
- [x] Textový chat
- [x] Hlas
- [x] Probouzecí slovo „Capri"
- [ ] Kamera – „podívej se"
- [ ] Režim učitele – kontrola matematiky
- [x] Čtení dokumentů ze složky, [ ] stahování
- [ ] Email a škola
