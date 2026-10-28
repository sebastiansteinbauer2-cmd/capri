# jarvis_app.py - JARVIS jako aplikace s oknem. Stačí na něj mluvit.
# Spuštění:  python jarvis_app.py
#
# Jak to funguje:
#   - okno (ui.html) jen ukazuje, co se děje - reaktor, titulky, tlačítka
#   - tenhle soubor pořád poslouchá mikrofon a když uslyší "Jarvisi...",
#     pošle větu Claudovi (jarvis.py) a odpověď přečte nahlas (jarvis_hlas.py)

import collections
import json
import queue
import sys
import time
import unicodedata
from pathlib import Path

# Při spuštění přes ikonu (pythonw) není terminál, kam by šly výpisy a chyby.
# Bez tohohle by stahování modelu spadlo - takže je posíláme do souboru.
if sys.stdout is None:
    sys.stdout = sys.stderr = open(Path(__file__).parent / "jarvis_log.txt", "w", encoding="utf-8")

import anthropic
import numpy as np
import sounddevice as sd
import webview  # pywebview - udělá z HTML stránky okno aplikace
from faster_whisper.vad import get_vad_model  # malá AI, která pozná lidský hlas

import jarvis

# ---------------------------------------------------------------
# NASTAVENÍ - tady si můžeš hrát a měnit věci
# ---------------------------------------------------------------

# Jak může Whisper zapsat slovo "Jarvis" (bez háčků a čárek, malými písmeny)
PROBOUZECI_SLOVA = ["jarvis", "jarvys", "jarvic", "jarviz", "dzarvis", "dzarvys",
                    "dzervis", "jervis", "jervys", "garvis"]

AKTIVNI_SEKUND = 20  # jak dlouho po odpovědi můžeš mluvit dál bez "Jarvisi"
TICHO_KONEC = 1.0    # kolik sekund ticha znamená, že jsi domluvil (víc = neuřízne pauzy)
JISTOTA_RECI = 0.5   # jak moc si musí být jistý, že mluvíš (0 až 1, míň = citlivější)
MAX_DELKA = 20       # nejdelší věta v sekundách

FREKVENCE = 16000
BLOK = 512           # mikrofon čteme po kouscích 32 ms (tolik chce rozpoznávač hlasu)

# ---------------------------------------------------------------
# STAV APLIKACE
# ---------------------------------------------------------------

okno = None
hlas = None                # sem se načte jarvis_hlas.py (uši a pusa)
fronta = queue.Queue()     # zprávy napsané v okně čekají tady
mikrofon_zapnut = True
posledni_odpoved = 0.0     # kdy JARVIS naposledy domluvil
aktualni_stav = None


def ui(funkce, *argumenty):
    """Zavolá JavaScriptovou funkci v okně, např. ui("ukazTy", "ahoj")."""
    # json.dumps převede text tak, aby mu JavaScript rozuměl (uvozovky, háčky...)
    okno.evaluate_js(f"{funkce}({', '.join(json.dumps(a) for a in argumenty)})")


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
    """Zahodí zvuk, který se nahrál, zatímco JARVIS mluvil (ať neslyší sám sebe)."""
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


def zpracuj(text):
    """Vyřídí jednu větu - buď příkaz, nebo otázku pro Clauda."""
    global posledni_odpoved

    # "Jarvisi, konec." -> "konec"
    prikaz = bez_diakritiky(text)
    for slovo in PROBOUZECI_SLOVA:
        prikaz = prikaz.replace(slovo + "i", "").replace(slovo, "")
    prikaz = prikaz.strip(" .,!?")

    if prikaz in ("konec", "vypni se"):
        nastav_stav("mluvim", "MLUVÍM")
        hlas.promluv("Na shledanou, pane.")
        okno.destroy()
        return
    if prikaz in ("reset", "nova konverzace"):  # smaže jen konverzaci, dlouhodobá paměť zůstane
        jarvis.historie.clear()
        ui("ukazTy", "")
        ui("zacniOdpoved")
        hlas.promluv("Začínáme novou konverzaci, pane.")
        return

    ui("ukazTy", text)
    ui("zacniOdpoved")
    nastav_stav("premyslim", "PŘEMÝŠLÍM")

    # Mluvčí začne číst první větu, zatímco Claude ještě píše další
    mluvci = hlas.Mluvci(pri_zacatku=lambda: nastav_stav("mluvim", "MLUVÍM"))

    def kousek_odpovedi(kousek):
        ui("pridejOdpoved", kousek)  # do titulků
        mluvci.pridej(kousek)        # do pusy

    try:
        jarvis.zeptej_se_ai(text, pri_kousku=kousek_odpovedi)
    except anthropic.APIError as chyba:
        ui("pridejOdpoved", f"[Chyba] {chyba}")
    ui("ukazCenu", f"utraceno cca {jarvis.celkova_cena:.2f} Kč")

    try:
        mluvci.dokonci()  # počká, než JARVIS domluví
    except Exception as chyba:
        ui("pridejOdpoved", f"\n[Hlas nefunguje: {chyba}]")
    posledni_odpoved = time.time()


def mozek():
    """Spustí hlavní smyčku. Když spadne, ukáže chybu v okně místo zamrznutí."""
    try:
        hlavni_smycka()
    except Exception as chyba:
        nastav_stav("nacitam", "CHYBA - PODROBNOSTI NÍŽE")
        ui("pridejOdpoved", f"[Chyba] {type(chyba).__name__}: {chyba}")
        raise  # vypíše celou chybu do terminálu / jarvis_log.txt


def hlavni_smycka():
    """Hlavní smyčka - běží na pozadí, zatímco je otevřené okno."""
    global hlas, posledni_odpoved

    import jarvis_hlas  # načte Whisper (pár sekund), proto až když už je vidět okno
    hlas = jarvis_hlas

    ucho = Ucho()

    with sd.InputStream(samplerate=FREKVENCE, channels=1, dtype="float32",
                        blocksize=BLOK) as mikrofon:
        nastav_stav("mluvim", "MLUVÍM")
        hlas.promluv("Systémy online. K vašim službám, pane.")
        posledni_odpoved = time.time()
        vyprazdni(mikrofon)

        while True:
            # Chvíli po odpovědi můžeš mluvit dál i bez "Jarvisi"
            aktivni = time.time() - posledni_odpoved < AKTIVNI_SEKUND

            if not fronta.empty():           # něco jsi napsal do okna
                zpracuj(fronta.get())
                vyprazdni(mikrofon)
                continue

            if not mikrofon_zapnut:
                nastav_stav("nacitam", "MIKROFON VYPNUTÝ")
                vyprazdni(mikrofon)          # zvuk se zahazuje, nikam nejde
                time.sleep(0.2)
                continue

            if aktivni:
                nastav_stav("posloucham", "POSLOUCHÁM")
            else:
                nastav_stav("ceka", 'ŘEKNI "JARVISI..."')

            zvuk = cekej_na_vetu(mikrofon, ucho)
            if zvuk is None or len(zvuk) < FREKVENCE * 0.4:  # nic, nebo jen krátké ťuknutí
                continue

            nastav_stav("premyslim", "PŘEPISUJI")
            text = hlas.prepis(zvuk)
            if text and (aktivni or obsahuje_probouzeci_slovo(text)):
                zpracuj(text)
            elif text:
                # Slyšel, ale chybělo "Jarvisi" - ukážeme to, ať víš, co slyšel
                ui("ukazTy", f"(slyšel jsem „{text}“, ale bez „Jarvisi“ - ignoruji)")
            vyprazdni(mikrofon)


if __name__ == "__main__":
    html = (Path(__file__).parent / "ui.html").read_text(encoding="utf-8")
    okno = webview.create_window("J.A.R.V.I.S.", html=html, js_api=Api(),
                                 width=900, height=700, background_color="#05080d")
    webview.start(mozek)  # otevře okno a funkci mozek() spustí na pozadí
