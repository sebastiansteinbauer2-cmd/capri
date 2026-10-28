# capri_app.py - Capri jako aplikace s oknem. Stačí na něj mluvit.
# Spuštění:  python capri_app.py
#
# Jak to funguje:
#   - okno (ui.html) jen ukazuje, co se děje - reaktor, titulky, tlačítka
#   - tenhle soubor pořád poslouchá mikrofon a když uslyší "Capri...",
#     pošle větu Claudovi (capri.py) a odpověď přečte nahlas (capri_hlas.py)

import collections
import datetime
import json
import os
import queue
import random
import sys
import threading
import time
import unicodedata
from pathlib import Path

# Při spuštění přes ikonu (pythonw) není terminál, kam by šly výpisy a chyby.
# Bez tohohle by stahování modelu spadlo - takže je posíláme do souboru.
if sys.stdout is None:
    sys.stdout = sys.stderr = open(Path(__file__).parent / "capri_log.txt", "w", encoding="utf-8")

import anthropic
import numpy as np
import sounddevice as sd
import webview  # pywebview - udělá z HTML stránky okno aplikace
from faster_whisper.vad import get_vad_model  # malá AI, která pozná lidský hlas

import capri

# ---------------------------------------------------------------
# NASTAVENÍ - tady si můžeš hrát a měnit věci
# ---------------------------------------------------------------

# Jak může Whisper zapsat slovo "Capri" (bez háčků a čárek, malými písmeny)
PROBOUZECI_SLOVA = ["capri", "capry", "kapri", "kapry", "capree", "kepri"]

AKTIVNI_SEKUND = 20  # jak dlouho po odpovědi můžeš mluvit dál bez "Capri"
TICHO_KONEC = 1.0    # kolik sekund ticha znamená, že jsi domluvil (víc = neuřízne pauzy)
JISTOTA_RECI = 0.5   # jak moc si musí být jistý, že mluvíš (0 až 1, míň = citlivější)
MAX_DELKA = 20       # nejdelší věta v sekundách

RANNI_OD, RANNI_DO = 5, 11       # od kolika do kolika hodin dělá ranní briefing
VECERNI_OD, VECERNI_DO = 20, 23  # kdy se ptá na denní shrnutí (v neděli týdenní)

FREKVENCE = 16000
BLOK = 512           # mikrofon čteme po kouscích 32 ms (tolik chce rozpoznávač hlasu)

STAV_DNE = Path(__file__).parent / "stav_dne.json"  # kdy naposledy byl briefing/shrnutí

# Malá Capri, která chodí po ploše (jako ve videu)
MAZLICEK = True          # False = jen hlavní okno
RYCHLOST_CHUZE = 3       # o kolik bodů se posune každý krok (víc = rychlejší)
VELIKOST_MAZLICKA = (260, 330)  # šířka a výška jejího průhledného okna

# ---------------------------------------------------------------
# STAV APLIKACE
# ---------------------------------------------------------------

okno = None
mazlicek = None            # malé průhledné okno s Capri na ploše
chodi = True               # jestli se Capri na ploše prochází
hlas = None                # sem se načte capri_hlas.py (uši a pusa)
fronta = queue.Queue()     # zprávy napsané v okně čekají tady
mikrofon_zapnut = True
posledni_odpoved = 0.0     # kdy Capri naposledy domluvil
aktualni_stav = None


def ui(funkce, *argumenty):
    """Zavolá JavaScriptovou funkci v oknech, např. ui("ukazTy", "ahoj").

    Posílá se do hlavního okna i do malé Capri na ploše - obě mají stejné funkce."""
    # json.dumps převede text tak, aby mu JavaScript rozuměl (uvozovky, háčky...)
    prikaz = f"{funkce}({', '.join(json.dumps(a) for a in argumenty)})"
    for o in (okno, mazlicek):
        if o:
            try:
                o.evaluate_js(prikaz)
            except Exception:
                pass  # okno je třeba zrovna zavřené nebo se ještě načítá


def nastav_stav(stav, text):
    """Změní vzhled reaktoru a nápis pod ním (jen když se opravdu mění)."""
    global aktualni_stav
    if (stav, text) != aktualni_stav:
        aktualni_stav = (stav, text)
        ui("nastavStav", stav, text)


class Api:
    """Funkce, které může volat okno - tlačítka a psaní."""

    def posli_text(self, text):
        fronta.put(text)

    def prepni_mikrofon(self):
        global mikrofon_zapnut
        mikrofon_zapnut = not mikrofon_zapnut
        return mikrofon_zapnut

    def reset(self):
        fronta.put("reset")


class ApiMazlicka:
    """Funkce, které volá malá Capri na ploše (její menu po kliknutí)."""

    def poslouchej(self):
        global posledni_odpoved
        posledni_odpoved = time.time()  # chvíli poslouchá i bez "Capri"

    def ukaz_okno(self):
        okno.restore()
        okno.show()

    def otevri_dokumenty(self):
        capri.nastroje.DOKUMENTY.mkdir(exist_ok=True)
        os.startfile(capri.nastroje.DOKUMENTY)  # otevře složku v Průzkumníkovi

    def prepni_chuzi(self):
        global chodi
        chodi = not chodi
        mazlicek.evaluate_js(f"nastavChuzi({json.dumps(chodi)})")
        return chodi


def prochazka():
    """Malá Capri se prochází po spodním okraji obrazovky (nad hlavním panelem).

    Běží pořád na pozadí. Když Capri přemýšlí nebo mluví, zastaví se."""
    plocha = webview.screens[0].frame  # pracovní plocha = obrazovka bez hlavního panelu
    sirka, vyska = VELIKOST_MAZLICKA
    vlevo, vpravo = plocha.X, plocha.X + plocha.Width - sirka
    y = plocha.Y + plocha.Height - vyska
    x, smer = vpravo - 40, -1

    while True:
        time.sleep(0.04)
        zastavit = not chodi or (aktualni_stav and aktualni_stav[0] in ("premyslim", "mluvim"))
        if zastavit:
            continue
        if random.random() < 0.003:          # občas se zastaví a chvíli postojí
            time.sleep(random.uniform(2, 6))
        if random.random() < 0.002:          # občas se otočí
            smer = -smer
            mazlicek.evaluate_js(f"otoc({smer})")
        x += smer * RYCHLOST_CHUZE
        if x <= vlevo or x >= vpravo:        # na kraji obrazovky se otočí
            x = max(vlevo, min(x, vpravo))
            smer = -smer
            mazlicek.evaluate_js(f"otoc({smer})")
        mazlicek.move(int(x), int(y))

# ---------------------------------------------------------------
# POSLOUCHÁNÍ
# ---------------------------------------------------------------


class Ucho:
    """Pozná, jestli v mikrofonu mluví člověk (Silero VAD).

    Na rozdíl od měření hlasitosti nereaguje na bouchnutí, větrák nebo hudbu
    a pozná i tiše řečená slova."""

    def __init__(self):
        self.model = get_vad_model()
        self.okenko = collections.deque(maxlen=16)  # posledních 0.5 s zvuku

    def je_to_rec(self, blok):
        """Vrátí číslo 0 až 1 - jak moc si je jistý, že v posledním kousku mluvíš."""
        self.okenko.append(blok)
        jistoty = self.model(np.concatenate(self.okenko))
        return float(np.asarray(jistoty).reshape(-1)[-1])


def hlasitost(blok):
    """Jak hlasitý je kousek zvuku (0 = ticho)."""
    return float(np.sqrt(np.mean(blok ** 2)))


def cekej_na_vetu(mikrofon, ucho):
    """Půl sekundy čeká, jestli začneš mluvit. Když ano, nahraje celou větu."""
    predtim = collections.deque(maxlen=16)  # posledních 0.5 s, ať neuřízneme začátek slova
    zacal = False
    for i in range(FREKVENCE // BLOK // 2):
        blok = mikrofon.read(BLOK)[0][:, 0]
        if i % 3 == 0:
            ui("ukazHlasitost", hlasitost(blok))  # ukazatel v okně
        if ucho.je_to_rec(blok) > JISTOTA_RECI:
            zacal = True
            break
        predtim.append(blok)
    if not zacal:
        return None

    nahravka = list(predtim) + [blok]
    bloku_ticha = 0
    while (bloku_ticha < TICHO_KONEC * FREKVENCE / BLOK
           and len(nahravka) < MAX_DELKA * FREKVENCE / BLOK):
        blok = mikrofon.read(BLOK)[0][:, 0]
        nahravka.append(blok)
        if len(nahravka) % 3 == 0:
            ui("ukazHlasitost", hlasitost(blok))
        # pod 0.35 = ticho; trochu níž než začátek, ať se věta netrhá
        bloku_ticha = bloku_ticha + 1 if ucho.je_to_rec(blok) < JISTOTA_RECI - 0.15 else 0
    ui("ukazHlasitost", 0)
    return np.concatenate(nahravka)


def vyprazdni(mikrofon):
    """Zahodí zvuk, který se nahrál, zatímco Capri mluvil (ať neslyší sám sebe)."""
    if mikrofon.read_available:
        mikrofon.read(mikrofon.read_available)


def bez_diakritiky(text):
    """'Džárvisi, Konec!' -> 'dzarvisi, konec!'"""
    rozlozeno = unicodedata.normalize("NFD", text.lower())
    return "".join(z for z in rozlozeno if unicodedata.category(z) != "Mn")


def obsahuje_probouzeci_slovo(text):
    return any(slovo in bez_diakritiky(text) for slovo in PROBOUZECI_SLOVA)

# ---------------------------------------------------------------
# VYŘIZOVÁNÍ VĚT
# ---------------------------------------------------------------


def zpracuj(text, automaticky=False):
    """Vyřídí jednu větu - buď příkaz, nebo otázku pro Clauda.

    automaticky=True = zprávu neposlal uživatel, ale aplikace sama
    (ranní briefing, večerní shrnutí). Neukazuje se v titulcích."""
    global posledni_odpoved

    if not automaticky:
        # "Capri, konec." -> "konec"
        prikaz = bez_diakritiky(text)
        for slovo in PROBOUZECI_SLOVA:
            prikaz = prikaz.replace(slovo + "i", "").replace(slovo, "")
        prikaz = prikaz.strip(" .,!?")

        if prikaz in ("konec", "vypni se"):
            nastav_stav("mluvim", "TRANSMITTING")
            hlas.promluv("Signing off. See you later.")
            okno.destroy()
            return
        if prikaz in ("reset", "nova konverzace"):  # smaže jen konverzaci, dlouhodobá paměť zůstane
            capri.historie.clear()
            ui("ukazTy", "")
            ui("zacniOdpoved")
            hlas.promluv("Clean slate. What's on your mind?")
            return

    ui("ukazTy", "" if automaticky else text)
    ui("zacniOdpoved")
    nastav_stav("premyslim", "PROCESSING")

    # Mluvčí začne číst první větu, zatímco Claude ještě píše další
    mluvci = hlas.Mluvci(pri_zacatku=lambda: nastav_stav("mluvim", "TRANSMITTING"))

    def kousek_odpovedi(kousek):
        ui("pridejOdpoved", kousek)  # do titulků
        mluvci.pridej(kousek)        # do pusy

    try:
        capri.zeptej_se_ai(text, pri_kousku=kousek_odpovedi)
    except anthropic.APIError as chyba:
        ui("pridejOdpoved", f"[Chyba] {chyba}")
    ui("ukazCenu", f"utraceno cca {capri.celkova_cena:.2f} Kč")

    try:
        mluvci.dokonci()  # počká, než Capri domluví
    except Exception as chyba:
        ui("pridejOdpoved", f"\n[Hlas nefunguje: {chyba}]")
    posledni_odpoved = time.time()


# ---------------------------------------------------------------
# NAPLÁNOVANÉ UDÁLOSTI - ranní briefing, večerní a týdenní shrnutí
# ---------------------------------------------------------------
# Tyhle zprávy posílá Claudovi aplikace sama, ne uživatel - proto to v nich stojí.

RANNI_ZPRAVA = """(Automatická zpráva od aplikace Capri, ne od uživatele.)
Je ráno a uživatel je u počítače. Přivítej ho krátkým ranním briefingem:
zjisti počasí, podívej se do deníku za poslední 2 dny a z paměti připomeň,
co ho dnes čeká (testy, úkoly, plány). Nejvýš 4 věty, začni pozdravem "Good morning"."""

VECERNI_ZPRAVA = """(Automatická zpráva od aplikace Capri, ne od uživatele.)
Je večer - čas na denní shrnutí. Krátce se uživatele zeptej, jak se mu dnes
dařilo: co se povedlo a co ne. Až odpoví, zapiš to stručně do deníku."""

TYDENNI_ZPRAVA = """(Automatická zpráva od aplikace Capri, ne od uživatele.)
Je neděle večer - čas na týdenní shrnutí. Přečti deník za posledních 7 dní
a shrň uživateli týden: výhry, co se opakovalo (vzorce) a návrh plánu na
příští týden. Nejvýš 6 vět. Shrnutí zapiš i do deníku. Pak se zeptej,
jestli chce plán upravit."""


def nacti_stav_dne():
    """Kdy naposledy byl briefing a shrnutí (ať to není dvakrát za den)."""
    try:
        return json.loads(STAV_DNE.read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return {}


def naplanovana_udalost():
    """Vrátí automatickou zprávu, když je čas na briefing nebo shrnutí. Jinak None."""
    ted = datetime.datetime.now()
    dnes = ted.date().isoformat()
    stav = nacti_stav_dne()

    if RANNI_OD <= ted.hour < RANNI_DO and stav.get("rano") != dnes:
        zprava, stav["rano"] = RANNI_ZPRAVA, dnes
    elif VECERNI_OD <= ted.hour < VECERNI_DO and stav.get("vecer") != dnes:
        zprava = TYDENNI_ZPRAVA if ted.weekday() == 6 else VECERNI_ZPRAVA  # 6 = neděle
        stav["vecer"] = dnes
    else:
        return None

    STAV_DNE.write_text(json.dumps(stav), encoding="utf-8")
    return zprava


def mozek():
    """Spustí hlavní smyčku. Když spadne, ukáže chybu v okně místo zamrznutí."""
    if mazlicek:
        threading.Thread(target=prochazka, daemon=True).start()
    try:
        hlavni_smycka()
    except Exception as chyba:
        nastav_stav("nacitam", "ERROR - DETAILS BELOW")
        ui("pridejOdpoved", f"[Chyba] {type(chyba).__name__}: {chyba}")
        raise  # vypíše celou chybu do terminálu / capri_log.txt


def hlavni_smycka():
    """Hlavní smyčka - běží na pozadí, zatímco je otevřené okno."""
    global hlas, posledni_odpoved

    import capri_hlas  # načte Whisper (pár sekund), proto až když už je vidět okno
    hlas = capri_hlas

    # Když Capri fotí obrazovku, ukážeme to - ať víš, že se dívá
    capri.nastroje.pri_snimku = lambda: nastav_stav("premyslim", "📸 LOOKING AT YOUR SCREEN")

    ucho = Ucho()

    with sd.InputStream(samplerate=FREKVENCE, channels=1, dtype="float32",
                        blocksize=BLOK) as mikrofon:
        udalost = naplanovana_udalost()
        if udalost:
            zpracuj(udalost, automaticky=True)  # třeba rovnou ranní briefing
        else:
            nastav_stav("mluvim", "TRANSMITTING")
            hlas.promluv("Link established. Capri is online and listening.")
        posledni_odpoved = time.time()
        vyprazdni(mikrofon)
        posledni_kontrola = time.time()

        while True:
            # Chvíli po odpovědi můžeš mluvit dál i bez "Capri"
            aktivni = time.time() - posledni_odpoved < AKTIVNI_SEKUND

            # Jednou za minutu: není čas na briefing / večerní shrnutí?
            if time.time() - posledni_kontrola > 60 and mikrofon_zapnut and not aktivni:
                posledni_kontrola = time.time()
                udalost = naplanovana_udalost()
                if udalost:
                    zpracuj(udalost, automaticky=True)
                    vyprazdni(mikrofon)
                    continue

            if not fronta.empty():           # něco jsi napsal do okna
                zpracuj(fronta.get())
                vyprazdni(mikrofon)
                continue

            if not mikrofon_zapnut:
                nastav_stav("nacitam", "MIC MUTED")
                vyprazdni(mikrofon)          # zvuk se zahazuje, nikam nejde
                time.sleep(0.2)
                continue

            if aktivni:
                nastav_stav("posloucham", "LISTENING")
            else:
                nastav_stav("ceka", 'SAY "CAPRI..."')

            zvuk = cekej_na_vetu(mikrofon, ucho)
            if zvuk is None or len(zvuk) < FREKVENCE * 0.4:  # nic, nebo jen krátké ťuknutí
                continue

            nastav_stav("premyslim", "TRANSCRIBING")
            text = hlas.prepis(zvuk)
            if text and (aktivni or obsahuje_probouzeci_slovo(text)):
                zpracuj(text)
            elif text:
                # Slyšela, ale chybělo "Capri" - ukážeme to, ať víš, co slyšela
                ui("ukazTy", f"(heard „{text}“ - no „Capri“, ignoring)")
            vyprazdni(mikrofon)


if __name__ == "__main__":
    slozka = Path(__file__).parent
    postava = (slozka / "postava.svg").read_text(encoding="utf-8")
    html = (slozka / "ui.html").read_text(encoding="utf-8").replace("<!--POSTAVA-->", postava)
    okno = webview.create_window("CAPRI", html=html, js_api=Api(),
                                 width=680, height=880, background_color="#070b18")
    if MAZLICEK:
        # Průhledné okno bez rámečku, pořád navrchu - vypadá to, že Capri chodí po ploše
        html_mazlicka = (slozka / "mazlicek.html").read_text(encoding="utf-8").replace("<!--POSTAVA-->", postava)
        sirka, vyska = VELIKOST_MAZLICKA
        plocha = webview.screens[0].frame
        mazlicek = webview.create_window(
            "Capri na ploše", html=html_mazlicka, js_api=ApiMazlicka(),
            width=sirka, height=vyska,
            x=plocha.X + plocha.Width - sirka - 40, y=plocha.Y + plocha.Height - vyska,
            frameless=True, transparent=True, on_top=True, easy_drag=False,
            resizable=False, shadow=False, focus=False)
        okno.events.closed += lambda: mazlicek.destroy()  # zavřeš hlavní okno = odejde i malá Capri
    webview.start(mozek)  # otevře okno a funkci mozek() spustí na pozadí
