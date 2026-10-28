# vykon.py - ať Windows Capri nebrzdí
#
# Windows na noteboocích šetří baterii tím, že programy, které nejsou zrovna
# v popředí, přesune na pomalá úsporná jádra procesoru ("EcoQoS").
# Capri ale běží skoro pořád na pozadí - a pak mluvil i poslouchal až 20x pomaleji.
# Tahle funkce řekne Windows: "tenhle jeden program nebrzdi".
# Týká se jen Capri, žádné nastavení Windows se nemění.

import ctypes
import sys
from ctypes import wintypes


class _NastaveniBrzdeni(ctypes.Structure):
    _fields_ = [("Version", wintypes.ULONG),
                ("ControlMask", wintypes.ULONG),
                ("StateMask", wintypes.ULONG)]


def vypni_brzdeni():
    """Vypne úsporné brzdění pro tenhle program. Vrátí True, když se to povedlo."""
    if sys.platform != "win32":
        return False
    kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
    kernel32.GetCurrentProcess.restype = wintypes.HANDLE
    kernel32.SetProcessInformation.argtypes = [wintypes.HANDLE, ctypes.c_int,
                                               ctypes.c_void_p, wintypes.DWORD]
    kernel32.SetProcessInformation.restype = wintypes.BOOL

    # Version=1, ControlMask=1 (rychlost), StateMask=0 (= nebrzdit)
    nastaveni = _NastaveniBrzdeni(1, 1, 0)
    PROCESS_POWER_THROTTLING = 4
    return bool(kernel32.SetProcessInformation(
        kernel32.GetCurrentProcess(), PROCESS_POWER_THROTTLING,
        ctypes.byref(nastaveni), ctypes.sizeof(nastaveni)))
