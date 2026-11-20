def yield_phonemes(dataset):
        for row in dataset["phonemes"]:
            for p in row:
                yield p["phoneme"]

def yield_phonemes_without_accent(dataset):
    for p in yield_phonemes(dataset):
        yield reduce_phoneme_accent(p)
        
def yield_visemes(dataset):
    for p in yield_phonemes(dataset):
            yield phoneme_to_viseme(p)
        
def reduce_phoneme_accent(phoneme: str):
    return phoneme.rstrip("012")
     


ARPABET_TO_META = {
    # Bilabial - zamknięte usta
    "P": "PP",
    "B": "PP",
    "M": "PP",

    # Labiodental - warga i zęby
    "F": "FF",
    "V": "FF",

    # Dental - język między zębami
    "TH": "TH",
    "DH": "TH",

    # Alveolar - przednia część języka
    "T": "DD",
    "D": "DD",

    # Velar - tylna część języka
    "K": "kk",
    "G": "kk",
    "HH": "kk",
    "NG": "kk",

    # Affricates / postalveolar
    "CH": "CH",
    "JH": "CH",
    "SH": "CH",
    "ZH": "CH",

    # Sibilants
    "S": "SS",
    "Z": "SS",

    # Nasals / liquids
    "N": "nn",
    "NG": "nn",
    "L": "nn",

    # Rhotic
    "R": "RR",
    "ER": "RR",
    "AXR": "RR",

    # Vowels - open
    "AA": "aa",
    "AH": "aa",
    "AX": "aa",
    "AY": "aa",
    "AW": "aa",

    # Vowels - mid front
    "EH": "E",
    "AE": "E",
    "EY": "E",

    # Vowels - close front
    "IH": "ih",
    "IY": "ih",
    "IX": "ih",
    "Y": "ih",

    # Vowels - rounded
    "AO": "oh",
    "OW": "oh",
    "OY": "oh",

    # Vowels / rounded back
    "UW": "ou",
    "UH": "ou",
    "W": "ou",

    # Silence / non-speech
    "SIL": "sil",
    "SP": "sil",
    "SPN": "sil",
    "": "sil",
}


def phoneme_to_viseme(phoneme: str) -> str:
    """
    Converts ARPAbet phoneme to Meta/Oculus viseme.
    """
    if not phoneme:
        return "sil"

    phoneme = reduce_phoneme_accent(phoneme.upper())

    return ARPABET_TO_META.get(phoneme, "unknown")


