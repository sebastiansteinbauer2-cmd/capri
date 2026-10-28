# capri_hlas.py - Krok 2: mluvíš do mikrofonu a Capri odpovídá hlasem
# Spuštění:  python capri_hlas.py
#
# Mozek (Claude, paměť, osobnost, cena) si půjčujeme z capri.py,
# tady přidáváme jen "uši" (Whisper) a "pusu" (Piper, záloha edge-tts).

import os
import re
import sys
import wave
import asyncio
import queue
import tempfile
import threading
from pathlib import Path

import vykon
vykon.vypni_brzdeni()  # ať Windows Capri na pozadí nezpomaluje (viz vykon.py)

os.environ["PYGAME_HIDE_SUPPORT_PROMPT"] = "1"  # ať pygame nevypisuje reklamu

import anthropic
import numpy as np
import sounddevice as sd          # nahrávání z mikrofonu
import edge_tts                   # záložní hlas přes internet (Microsoft)
import pygame                     # přehrání zvuku
from faster_whisper import WhisperModel  # převod řeči na text
import onnxruntime                       # na tomhle běží Piper
from piper import PiperVoice, SynthesisConfig  # hlavní hlas - běží přímo na PC

import capri  # náš soubor z kroku 1 - jeho funkce a nastavení

# ---------------------------------------------------------------
# NASTAVENÍ - tady si můžeš hrát a měnit věci
# ---------------------------------------------------------------

# Který hlas je hlavní:
#   "microsoft" - ženský anglický hlas Capri přes internet
#   "piper"     - hlas přímo na PC, bez internetu (zatím jen český mužský Jirka)
# Ten druhý se použije jako záloha, když hlavní nefunguje.
HLAVNI_HLAS = "microsoft"

# Ženské anglické hlasy Microsoftu pro Capri - zkouší se popořadě.
# Další k vyzkoušení: "en-US-AnaNeural" (dětský), "en-US-JennyNeural", "en-GB-SoniaNeural" (britský),
# "en-US-AriaNeural". Když budeš chtít, ať mluví česky: "en-US-AvaMultilingualNeural".
HLASY = ["en-US-AvaNeural", "en-US-AvaMultilingualNeural", "en-US-EmmaNeural"]
VYSKA_HLASU = "+10Hz"   # vyšší/hravější hlas: "+20Hz", hlubší: "-10Hz"
TEMPO_HLASU = "+5%"     # rychlejší: "+15%", pomalejší: "-10%"

# Záložní hlas Piper (český, mužský)
PIPER_HLAS = Path(__file__).parent / "hlasy" / "cs_CZ-jirka-medium.onnx"
RYCHLOST_RECI = 1.0  # víc = pomalejší řeč (např. 1.15), míň = rychlejší (např. 0.9)

# Model pro rozpoznávání řeči:
#   na grafice: "large-v3" (nejpřesnější, doporučeno), "large-v3-turbo" (rychlejší)
#   na procesoru (záloha, když grafika nejde): "small" nebo "base"
WHISPER_GRAFIKA = "large-v3"
WHISPER_PROCESOR = "small"

FREKVENCE = 16000  # kolikrát za sekundu se měří zvuk (Whisper chce 16000)
MIN_ZNAKU = 60     # nejkratší kousek odpovědi pro vícejazyčný hlas (ať nesplete jazyk)

# V jakém jazyce mluvíš TY na Capri:
#   None = Whisper to pozná sám (můžeš mluvit česky i anglicky)
#   "cs" = jen česky (přesnější),  "en" = jen anglicky
JAZYK_RECI = None

# Odpovědi se čtou nahlas, takže Capri řekneme, ať mluví krátce
capri.OSOBNOST += """
Your replies are read out loud, so keep them short (1 to 3 sentences)
and don't use bullet points, tables, code or emoji.
User messages are an automatic speech-to-text transcript from a microphone, so some
words may be misheard. Guess what the user most likely meant (by how the words sound
and what makes sense). If it really makes no sense, briefly ask what they meant."""

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
        list(model.transcribe(np.zeros(FREKVENCE, dtype=np.float32), language="en")[0])
        print(f"Whisper běží na grafice ({WHISPER_GRAFIKA}).")
        return model
    except Exception as chyba:
        print(f"Grafika nejde ({chyba}), používám procesor ({WHISPER_PROCESOR}).")
        return WhisperModel(WHISPER_PROCESOR, device="cpu", compute_type="int8")


def nacti_piper():
    """Načte český hlas Piper. Když nejde, vrátí None a použije se Microsoft."""
    try:
        hlas = PiperVoice.load(PIPER_HLAS)
        # "spinning" = procesor by mezi větami naprázdno točil a čekal; bez něj je to rychlejší
        nastaveni = onnxruntime.SessionOptions()
        nastaveni.add_session_config_entry("session.intra_op.allow_spinning", "0")
        hlas.session = onnxruntime.InferenceSession(
            str(PIPER_HLAS), sess_options=nastaveni, providers=["CPUExecutionProvider"])
        return hlas
    except Exception as chyba:
        print(f"Piper nejde ({chyba}), použiju hlas Microsoftu.")
        return None


print("Načítám rozpoznávání řeči (poprvé se stahuje model, chvíli to trvá)...")
whisper = nacti_whisper()
piper = nacti_piper()
PIPER_ZAMEK = threading.Lock()
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
             "konec videa", "přeložil", "subtitles", "thanks for watching",
             "thank you for watching", "subscribe", "amara.org"]

# Ukázka, jak vypadá řeč, kterou má čekat - pomůže mu se slovy i interpunkcí
NAPOVEDY = {
    "cs": "Capri, jaké je dnes počasí? Zapamatuj si, že v pátek píšu test z matiky.",
    "en": "Capri, what's the weather like today? Remember that I have a math test on Friday.",
}
NAPOVEDA = NAPOVEDY.get(JAZYK_RECI, "Capri,")


def prepis(zvuk):
    """Převede nahraný zvuk na text pomocí Whisperu."""
    segmenty, _ = whisper.transcribe(
        zvuk,
        language=JAZYK_RECI,                 # None = pozná jazyk sám
        beam_size=5,                         # zkouší 5 variant a vybere nejlepší
        vad_filter=True,                     # vyřízne ticho, ať nevymýšlí slova z šumu
        vad_parameters={"threshold": 0.3},   # ...ale tiše řečená slova nechá
        hotwords="Capri",           # slova, která má čekat
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


def vyrob_zvuk(text, jmeno):
    """Udělá z textu nahrávku a vrátí cestu k ní.

    jmeno = název souboru bez přípony (Piper dělá .wav, Microsoft .mp3)."""
    zaklad = os.path.join(tempfile.gettempdir(), jmeno)

    if HLAVNI_HLAS == "microsoft" or not piper:
        try:
            return vyrob_microsoft(text, zaklad + ".mp3")
        except Exception as chyba:
            if not piper:
                raise
            print(f"(hlas Microsoftu nejde: {chyba} - mluvím Piperem)")
    return vyrob_piper(text, zaklad + ".wav")


def vyrob_piper(text, soubor):
    with PIPER_ZAMEK, wave.open(soubor, "wb") as wav:  # zámek = jen jedna věta naráz
        piper.synthesize_wav(text, wav, syn_config=SynthesisConfig(length_scale=RYCHLOST_RECI))
    return soubor


def vyrob_microsoft(text, soubor):
    """Pošle text Microsoftu (edge-tts). Každý hlas zkusí dvakrát, pak další."""
    while True:
        for pokus in range(2):  # Microsoft občas napoprvé nevrátí nic
            try:
                asyncio.run(edge_tts.Communicate(text, HLASY[0], rate=TEMPO_HLASU,
                                                 pitch=VYSKA_HLASU).save(soubor))
                return soubor
            except edge_tts.exceptions.NoAudioReceived:
                pass
        if len(HLASY) == 1:
            raise RuntimeError("žádný hlas Microsoftu nefunguje")
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
    """Nechá Capri přečíst celý text nahlas."""
    prehraj(vyrob_zvuk(text, "capri_odpoved"))


class Mluvci:
    """Čte odpověď po větách - první větu říká, zatímco Claude píše další.

    Běží jako dvě "linky" najednou (vlákna):
      1) výroba: věta -> nahrávka (Piper, nebo Microsoft)
      2) přehrávání: nahrávka -> reproduktor
    Takže zatímco Capri říká první větu, druhá se už vyrábí."""

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
        """Přidá kousek textu. Celé věty rovnou pošle na výrobu zvuku.

        U hlasu Microsoftu slepí krátké věty dohromady (aspoň MIN_ZNAKU znaků) -
        vícejazyčný hlas podle krátké věty špatně pozná češtinu. Piper to nepotřebuje."""
        self.zbytek += kousek
        vicejazycny = "Multilingual" in HLASY[0] and (HLAVNI_HLAS == "microsoft" or not piper)
        nejmene = MIN_ZNAKU if vicejazycny else 1
        while True:
            # konce vět (tečka/otazník + mezera), ale jen ty, před kterými je dost textu
            konce = [m.end() for m in re.finditer(r"[.!?…]\s", self.zbytek)
                     if m.end() >= nejmene]
            if not konce:
                break
            veta = self.zbytek[:konce[0]].strip()
            self.zbytek = self.zbytek[konce[0]:]
            if veta:
                self.vety.put(veta)

    def dokonci(self):
        """Pošle i poslední kousek a počká, než Capri domluví."""
        if self.zbytek.strip():
            self.vety.put(self.zbytek.strip())
        self.vety.put(None)  # None = "konec odpovědi"
        self.hotovo.wait()
        if self.chyba:
            raise self.chyba

    def _vyrabej(self):
        while (veta := self.vety.get()) is not None:
            self.cislo += 1
            try:
                self.nahravky.put(vyrob_zvuk(veta, f"capri_veta_{self.cislo % 5}"))
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
    print("\nCapri online s hlasem.")
    print("Zmáčkni Enter a mluv. Nebo napiš zprávu. Příkazy: konec / reset / cena\n")
    promluv("Link established. Capri is online and listening.")

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
            promluv("Signing off. See you later.")
            print(f"(konverzace stála cca {capri.celkova_cena:.2f} Kč)")
            break
        if prikaz == "reset":
            capri.historie.clear()
            promluv("Clean slate. What's on your mind?")
            continue
        if prikaz == "cena":
            print(f"Zatím cca {capri.celkova_cena:.2f} Kč\n")
            continue

        print("Capri: ", end="")
        mluvci = Mluvci()

        def vypis_a_mluv(kousek):
            print(kousek, end="", flush=True)
            mluvci.pridej(kousek)

        try:
            capri.zeptej_se_ai(text, pri_kousku=vypis_a_mluv)
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
