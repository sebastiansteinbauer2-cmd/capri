# jarvis_hlas.py - Krok 2: mluvíš do mikrofonu a JARVIS odpovídá hlasem
# Spuštění:  python jarvis_hlas.py
#
# Mozek (Claude, paměť, osobnost, cena) si půjčujeme z jarvis.py,
# tady přidáváme jen "uši" (Whisper) a "pusu" (edge-tts).

import os
import re
import sys
import asyncio
import queue
import tempfile
import threading
from pathlib import Path

os.environ["PYGAME_HIDE_SUPPORT_PROMPT"] = "1"  # ať pygame nevypisuje reklamu

import anthropic
import numpy as np
import sounddevice as sd          # nahrávání z mikrofonu
import edge_tts                   # převod textu na řeč (český hlas)
import pygame                     # přehrání zvuku
from faster_whisper import WhisperModel  # převod řeči na text

import jarvis  # náš soubor z kroku 1 - jeho funkce a nastavení

# ---------------------------------------------------------------
# NASTAVENÍ - tady si můžeš hrát a měnit věci
# ---------------------------------------------------------------

# Hlasy JARVISe - zkouší se popořadě, dokud jeden nefunguje.
# Čistě české hlasy ("cs-CZ-...") Microsoft občas vypne, proto jsou za nimi
# záložní "vícejazyčné" hlasy, které česky mluví taky (s lehkým přízvukem).
# Další k vyzkoušení: "en-US-BrianMultilingualNeural", "fr-FR-RemyMultilingualNeural",
# "cs-CZ-VlastaNeural" (ženský).
HLASY = ["cs-CZ-AntoninNeural", "de-DE-FlorianMultilingualNeural"]

# Model pro rozpoznávání řeči:
#   na grafice: "large-v3" (nejpřesnější, doporučeno), "large-v3-turbo" (rychlejší)
#   na procesoru (záloha, když grafika nejde): "small" nebo "base"
WHISPER_GRAFIKA = "large-v3"
WHISPER_PROCESOR = "small"

FREKVENCE = 16000  # kolikrát za sekundu se měří zvuk (Whisper chce 16000)

# Odpovědi se čtou nahlas, takže JARVISovi řekneme, ať mluví krátce
jarvis.OSOBNOST += """
Tvoje odpovědi se čtou nahlas, takže mluv krátce (1 až 3 věty)
a nepoužívej odrážky, tabulky, kód ani emoji.
Zprávy uživatele jsou automatický přepis řeči z mikrofonu, takže v nich můžou
být špatně rozpoznaná slova. Odhadni, co uživatel nejspíš myslel (podle zvuku
slov a smyslu věty). Když to opravdu nedává smysl, krátce se zeptej, co myslel."""

# ---------------------------------------------------------------
# PŘÍPRAVA - "UŠI"
# ---------------------------------------------------------------


def pridej_cuda_knihovny():
    """Řekne Windows, kde jsou knihovny NVIDIA (nainstalované přes pip)."""
    site = Path(sys.prefix) / "Lib" / "site-packages" / "nvidia"
    for slozka in (site / "cublas" / "bin", site / "cudnn" / "bin"):
        if slozka.exists():
            os.add_dll_directory(str(slozka))
            os.environ["PATH"] = str(slozka) + os.pathsep + os.environ["PATH"]


def nacti_whisper():
    """Zkusí Whisper na grafice. Když to nejde, použije procesor."""
    try:
        pridej_cuda_knihovny()
        model = WhisperModel(WHISPER_GRAFIKA, device="cuda", compute_type="float16")
        # zkušební přepis 1 s ticha - chybějící knihovny se projeví až tady
        list(model.transcribe(np.zeros(FREKVENCE, dtype=np.float32), language="cs")[0])
        print(f"Whisper běží na grafice ({WHISPER_GRAFIKA}).")
        return model
    except Exception as chyba:
        print(f"Grafika nejde ({chyba}), používám procesor ({WHISPER_PROCESOR}).")
        return WhisperModel(WHISPER_PROCESOR, device="cpu", compute_type="int8")


print("Načítám rozpoznávání řeči (poprvé se stahuje model, chvíli to trvá)...")
whisper = nacti_whisper()
pygame.mixer.init()


def nahraj():
    """Nahrává z mikrofonu, dokud nezmáčkneš Enter. Vrátí zvuk jako čísla."""
    kousky = []

    # Tahle funkce se zavolá automaticky pokaždé, když mikrofon pošle kousek zvuku
    def kdyz_prijde_zvuk(data, pocet, cas, stav):
        kousky.append(data.copy())

    with sd.InputStream(samplerate=FREKVENCE, channels=1, dtype="float32",
                        callback=kdyz_prijde_zvuk):
        input("🎤 Poslouchám... (Enter = hotovo)")

    if not kousky:
        return None
    return np.concatenate(kousky).flatten()  # slepí kousky do jednoho dlouhého zvuku


# Věty, které si Whisper občas vymyslí, když slyší jen šum (naučil se je z titulků videí)
VYMYSLENE = ["titulky", "děkuji za pozornost", "děkuji za sledování", "www.", "odebírejte",
             "konec videa", "přeložil", "subtitles"]

# Ukázka, jak vypadá řeč, kterou má čekat - pomůže mu se slovy i interpunkcí
NAPOVEDA = "Jarvisi, jaké je dnes počasí? Zapamatuj si, že v pátek píšu test z matiky."


def prepis(zvuk):
    """Převede nahraný zvuk na text pomocí Whisperu."""
    segmenty, _ = whisper.transcribe(
        zvuk,
        language="cs",
        beam_size=5,                         # zkouší 5 variant a vybere nejlepší
        vad_filter=True,                     # vyřízne ticho, ať nevymýšlí slova z šumu
        vad_parameters={"threshold": 0.3},   # ...ale tiše řečená slova nechá
        hotwords="Jarvisi Jarvis",           # slova, která má čekat
        initial_prompt=NAPOVEDA,
        condition_on_previous_text=False,
    )
    casti = []
    for s in segmenty:
        # no_speech_prob = jak moc si myslí, že tam nikdo nemluvil
        # avg_logprob = jak moc si je jistý tím, co napsal (blíž k 0 = jistější)
        if s.no_speech_prob > 0.6 and s.avg_logprob < -0.7:
            continue  # nejspíš šum, ne řeč
        if any(v in s.text.lower() for v in VYMYSLENE):
            continue  # typická vymyšlená věta
        casti.append(s.text)
    return " ".join(casti).strip()

# ---------------------------------------------------------------
# "PUSA"
# ---------------------------------------------------------------


def vyrob_zvuk(text, soubor):
    """Pošle text Microsoftu (edge-tts) a uloží nahrávku s hlasem do souboru."""
    while True:
        try:
            asyncio.run(edge_tts.Communicate(text, HLASY[0]).save(soubor))
            return
        except edge_tts.exceptions.NoAudioReceived:
            if len(HLASY) == 1:
                raise  # žádný hlas nefunguje
            print(f"(hlas {HLASY[0]} nefunguje, zkouším další)")
            HLASY.pop(0)  # nefunkční hlas vyřadíme až do vypnutí programu


def prehraj(soubor):
    """Přehraje nahrávku a počká, než doběhne."""
    pygame.mixer.music.load(soubor)
    pygame.mixer.music.play()
    while pygame.mixer.music.get_busy():
        pygame.time.wait(50)
    pygame.mixer.music.unload()  # uvolní soubor, aby šel příště přepsat


def promluv(text):
    """Nechá JARVISe přečíst celý text nahlas."""
    soubor = os.path.join(tempfile.gettempdir(), "jarvis_odpoved.mp3")
    vyrob_zvuk(text, soubor)
    prehraj(soubor)


class Mluvci:
    """Čte odpověď po větách - první větu říká, zatímco Claude píše další.

    Běží jako dvě "linky" najednou (vlákna):
      1) výroba: věta -> nahrávka (edge-tts)
      2) přehrávání: nahrávka -> reproduktor
    Takže zatímco JARVIS říká první větu, druhá se už vyrábí."""

    def __init__(self, pri_zacatku=None):
        self.zbytek = ""               # rozepsaná věta, která ještě nemá tečku
        self.vety = queue.Queue()      # věty čekající na výrobu zvuku
        self.nahravky = queue.Queue()  # hotové nahrávky čekající na přehrání
        self.hotovo = threading.Event()
        self.pri_zacatku = pri_zacatku  # zavolá se, když začne mluvit
        self.chyba = None
        self.cislo = 0
        threading.Thread(target=self._vyrabej, daemon=True).start()
        threading.Thread(target=self._prehravej, daemon=True).start()

    def pridej(self, kousek):
        """Přidá kousek textu. Celé věty rovnou pošle na výrobu zvuku."""
        self.zbytek += kousek
        while True:
            konec = re.search(r"[.!?…]\s", self.zbytek)  # tečka/otazník + mezera = konec věty
            if not konec:
                break
            veta = self.zbytek[:konec.end()].strip()
            self.zbytek = self.zbytek[konec.end():]
            if veta:
                self.vety.put(veta)

    def dokonci(self):
        """Pošle i poslední kousek a počká, než JARVIS domluví."""
        if self.zbytek.strip():
            self.vety.put(self.zbytek.strip())
        self.vety.put(None)  # None = "konec odpovědi"
        self.hotovo.wait()
        if self.chyba:
            raise self.chyba

    def _vyrabej(self):
        while (veta := self.vety.get()) is not None:
            self.cislo += 1
            soubor = os.path.join(tempfile.gettempdir(), f"jarvis_veta_{self.cislo % 5}.mp3")
            try:
                vyrob_zvuk(veta, soubor)
                self.nahravky.put(soubor)
            except Exception as chyba:
                self.chyba = chyba
        self.nahravky.put(None)

    def _prehravej(self):
        prvni = True
        while (soubor := self.nahravky.get()) is not None:
            if prvni and self.pri_zacatku:
                self.pri_zacatku()
            prvni = False
            prehraj(soubor)
        self.hotovo.set()

# ---------------------------------------------------------------
# HLAVNÍ PROGRAM (verze s Enterem v terminálu)
# ---------------------------------------------------------------


def main():
    print("\nJARVIS online s hlasem.")
    print("Zmáčkni Enter a mluv. Nebo napiš zprávu. Příkazy: konec / reset / cena\n")
    promluv("Systémy online. K vašim službám, pane.")

    while True:
        text = input("Ty (Enter = mluvit): ").strip()

        if text == "":
            zvuk = nahraj()
            if zvuk is None or len(zvuk) < FREKVENCE * 0.5:  # kratší než půl sekundy
                print("Nic jsem neslyšel.\n")
                continue
            print("(přepisuji...)")
            text = prepis(zvuk)
            if not text:
                print("Nerozuměl jsem, zkus to znovu.\n")
                continue
            print(f"Ty (řekl jsi): {text}")

        # "Konec." z mikrofonu má tečku, tak ji pro příkazy odstraníme
        prikaz = text.lower().strip(" .!?")
        if prikaz == "konec":
            promluv("Na shledanou, pane.")
            print(f"(konverzace stála cca {jarvis.celkova_cena:.2f} Kč)")
            break
        if prikaz == "reset":
            jarvis.historie.clear()
            promluv("Paměť vymazána, pane.")
            continue
        if prikaz == "cena":
            print(f"Zatím cca {jarvis.celkova_cena:.2f} Kč\n")
            continue

        print("JARVIS: ", end="")
        mluvci = Mluvci()

        def vypis_a_mluv(kousek):
            print(kousek, end="", flush=True)
            mluvci.pridej(kousek)

        try:
            jarvis.zeptej_se_ai(text, pri_kousku=vypis_a_mluv)
        except anthropic.APIError as chyba:
            print(f"\n[Chyba] {chyba}")
        print()
        try:
            mluvci.dokonci()
        except Exception as chyba:
            print(f"[Hlas nefunguje: {chyba}] Jsi na internetu?")
        print()


if __name__ == "__main__":
    main()
