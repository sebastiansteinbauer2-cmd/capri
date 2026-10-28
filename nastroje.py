# nastroje.py - věci, které JARVIS umí udělat ve skutečném světě
#
# Claude sám umí jen psát text. Nástroj je funkce v Pythonu, o které
# Claudovi řekneme (jméno + popis). Když ji Claude potřebuje, napíše
# "zavolej zjisti_pocasi(mesto='Brno')" - a my ji spustíme a vrátíme výsledek.
#
# Bezpečnost: tady jsou jen nástroje, které nic nemění (jen čtou).
# Nástroje, které by něco mazaly, posílaly nebo platily, budou vždy
# chtít tvoje potvrzení.

import datetime
import json
import re
import urllib.parse
import urllib.request
from pathlib import Path

# ---------------------------------------------------------------
# NASTAVENÍ
# ---------------------------------------------------------------

DOMOVSKE_MESTO = "Praha"  # kde bydlíš - přepiš si na své město

# Dlouhodobá paměť - obyčejné textové soubory, můžeš je otevřít a upravit
PAMET = Path(__file__).parent / "pamet.txt"
ZAPOMENUTO = Path(__file__).parent / "pamet_zapomenuto.txt"  # nic se nemaže, jen se přesune sem

# Čísla počasí (WMO kódy) -> česky
POPIS_POCASI = {
    0: "jasno", 1: "skoro jasno", 2: "polojasno", 3: "zataženo",
    45: "mlha", 48: "mlha s námrazou",
    51: "slabé mrholení", 53: "mrholení", 55: "silné mrholení",
    56: "mrznoucí mrholení", 57: "mrznoucí mrholení",
    61: "slabý déšť", 63: "déšť", 65: "silný déšť",
    66: "mrznoucí déšť", 67: "mrznoucí déšť",
    71: "slabé sněžení", 73: "sněžení", 75: "silné sněžení", 77: "sněhová zrna",
    80: "slabé přeháňky", 81: "přeháňky", 82: "silné přeháňky",
    85: "sněhové přeháňky", 86: "silné sněhové přeháňky",
    95: "bouřka", 96: "bouřka s kroupami", 99: "silná bouřka s kroupami",
}

# ---------------------------------------------------------------
# NÁSTROJE
# ---------------------------------------------------------------


def stahni_json(adresa, parametry):
    """Stáhne data z internetu a převede je z JSON na slovník."""
    url = adresa + "?" + urllib.parse.urlencode(parametry)
    with urllib.request.urlopen(url, timeout=10) as odpoved:
        return json.load(odpoved)


def zjisti_pocasi(mesto):
    """Zjistí počasí pro město přes bezplatnou službu Open-Meteo (bez klíče)."""
    # 1) Najdeme souřadnice města
    hledani = stahni_json("https://geocoding-api.open-meteo.com/v1/search",
                          {"name": mesto, "count": 1, "language": "cs"})
    if not hledani.get("results"):
        return f"Město '{mesto}' jsem nenašel."
    misto = hledani["results"][0]

    # 2) Stáhneme počasí pro ty souřadnice
    data = stahni_json("https://api.open-meteo.com/v1/forecast", {
        "latitude": misto["latitude"],
        "longitude": misto["longitude"],
        "current": "temperature_2m,apparent_temperature,weather_code,wind_speed_10m",
        "daily": "temperature_2m_max,temperature_2m_min,precipitation_probability_max,weather_code",
        "timezone": "auto",
        "forecast_days": 3,
    })

    ted = data["current"]
    vysledek = {
        "misto": f"{misto['name']}, {misto.get('country', '')}",
        "ted": {
            "teplota_C": ted["temperature_2m"],
            "pocitova_teplota_C": ted["apparent_temperature"],
            "pocasi": POPIS_POCASI.get(ted["weather_code"], "neznámé"),
            "vitr_km_h": ted["wind_speed_10m"],
        },
        "predpoved": [],
    }
    dny = data["daily"]
    for i, datum in enumerate(dny["time"]):
        vysledek["predpoved"].append({
            "datum": datum,
            "max_C": dny["temperature_2m_max"][i],
            "min_C": dny["temperature_2m_min"][i],
            "sance_srazek_procent": dny["precipitation_probability_max"][i],
            "pocasi": POPIS_POCASI.get(dny["weather_code"][i], "neznámé"),
        })
    return json.dumps(vysledek, ensure_ascii=False)


def nacti_pamet():
    """Vrátí všechno, co si JARVIS pamatuje (nebo prázdný text)."""
    if PAMET.exists():
        return PAMET.read_text(encoding="utf-8").strip()
    return ""


def zapamatuj_si(informace):
    """Připíše jednu informaci na konec paměti, i s datem."""
    # Pojistka: dlouhá řada číslic může být číslo karty nebo účtu - to neukládáme
    if re.search(r"\d[\d \-]{11,}\d", informace):
        return "Neuloženo: vypadá to na číslo karty nebo účtu, takové věci si neukládám."
    with PAMET.open("a", encoding="utf-8") as soubor:
        soubor.write(f"- {datetime.date.today():%d.%m.%Y}: {informace}\n")
    return "Uloženo do paměti."


def zapomen(text):
    """Přesune z paměti řádky, které obsahují zadaný text, do pamet_zapomenuto.txt."""
    radky = nacti_pamet().splitlines()
    pryc = [r for r in radky if text.lower() in r.lower()]
    if not pryc:
        return f"V paměti nic s '{text}' není."
    zustava = [r for r in radky if r not in pryc]
    PAMET.write_text("\n".join(zustava) + ("\n" if zustava else ""), encoding="utf-8")
    with ZAPOMENUTO.open("a", encoding="utf-8") as soubor:
        soubor.write("\n".join(pryc) + "\n")
    return "Zapomenuto: " + "; ".join(pryc)

# ---------------------------------------------------------------
# SEZNAM NÁSTROJŮ PRO CLAUDA
# ---------------------------------------------------------------
# Claude nevidí náš kód - vidí jen jméno, popis a jaké údaje nástroj chce.
# Podle popisu se rozhodne, kdy ho použít, takže popis musí být jasný.

NASTROJE = [
    {
        "name": "zjisti_pocasi",
        "description": "Zjistí aktuální počasí a předpověď na 3 dny pro zadané město. "
                       "Použij vždy, když se uživatel ptá na počasí, teplotu, déšť apod. "
                       f"Když uživatel město neřekne, použij '{DOMOVSKE_MESTO}'.",
        "input_schema": {
            "type": "object",
            "properties": {
                "mesto": {"type": "string", "description": "Název města, např. Brno"},
            },
            "required": ["mesto"],
            "additionalProperties": False,
        },
    },
    {
        "name": "zapamatuj_si",
        "description": "Uloží jednu důležitou informaci o uživateli do dlouhodobé paměti, "
                       "která vydrží i po vypnutí. Použij, když tě uživatel požádá, ať si něco "
                       "zapamatuješ, nebo když o sobě řekne něco trvale důležitého (jméno, škola, "
                       "koníčky, plány, termíny testů, oblíbené věci). Neukládej drobnosti z "
                       "rozhovoru ani věci, které už v paměti jsou. NIKDY neukládej hesla, PINy, "
                       "čísla karet ani účtů.",
        "input_schema": {
            "type": "object",
            "properties": {
                "informace": {"type": "string",
                              "description": "Krátká věta, např. 'V pátek 30.10. píše test z matiky (zlomky).'"},
            },
            "required": ["informace"],
            "additionalProperties": False,
        },
    },
    {
        "name": "zapomen",
        "description": "Odebere z dlouhodobé paměti záznamy, které obsahují zadaný text. "
                       "Použij JEN když tě o to uživatel výslovně požádá.",
        "input_schema": {
            "type": "object",
            "properties": {
                "text": {"type": "string", "description": "Slovo nebo část věty, např. 'test z matiky'"},
            },
            "required": ["text"],
            "additionalProperties": False,
        },
    },
]

# Jméno nástroje -> funkce, která se spustí
FUNKCE = {
    "zjisti_pocasi": zjisti_pocasi,
    "zapamatuj_si": zapamatuj_si,
    "zapomen": zapomen,
}


def spust_nastroj(jmeno, udaje):
    """Spustí nástroj, o který si Claude řekl. Chyby vrátí jako text."""
    try:
        return FUNKCE[jmeno](**udaje)
    except Exception as chyba:
        return f"Nástroj selhal: {chyba}"
