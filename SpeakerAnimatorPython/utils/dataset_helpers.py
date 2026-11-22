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
    "P": "PP",
    "B": "PP",
    "M": "PP",

    "F": "FF",
    "V": "FF",

    "TH": "TH",
    "DH": "TH",

    "T": "DD",
    "D": "DD",

    "K": "kk",
    "G": "kk",
    "HH": "kk",
    "NG": "kk",

    "CH": "CH",
    "JH": "CH",
    "SH": "CH",
    "ZH": "CH",

    "S": "SS",
    "Z": "SS",
    
    "N": "nn",
    "NG": "nn",
    "L": "nn",

    "R": "RR",
    "ER": "RR",
    "AXR": "RR",

    "AA": "aa",
    "AH": "aa",
    "AX": "aa",
    "AY": "aa",
    "AW": "aa",

    "EH": "E",
    "AE": "E",
    "EY": "E",

    "IH": "I",
    "IY": "I",
    "IX": "I",
    "Y": "I",

    "AO": "O",
    "OW": "O",
    "OY": "O",

    "UW": "U",
    "UH": "U",
    "W": "U",

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


SIL_TOKEN = "SIL"


def insert_silence_segments(
    phonemes,
    audio_duration: float,
    min_silence_duration: float = 0.03,
    include_leading: bool = True,
    include_trailing: bool = True,
):
    """
    Uzupelnia dane labelami "sil" (silence) w miejscach gdzie nie ma zadnych
    (bo dane nie maja zdefiniowanej ciszy)
    """
    ordered = sorted(phonemes, key=lambda p: float(p["start"]))

    result = []
    cursor = 0.0

    for p in ordered:
        start = float(p["start"])
        end = float(p["end"])

        if end <= start:
            continue

        start = max(start, cursor)
        gap = start - cursor

        if gap >= min_silence_duration and (cursor > 0 or include_leading):
            result.append({"phoneme": SIL_TOKEN, "start": cursor, "end": start})

        result.append({"phoneme": p["phoneme"], "start": start, "end": end})
        cursor = end

    tail = audio_duration - cursor
    if include_trailing and tail >= min_silence_duration:
        result.append({"phoneme": SIL_TOKEN, "start": cursor, "end": audio_duration})

    return result


def add_silence_to_dataset(
    dataset,
    min_silence_duration: float = 0.05,
    include_leading: bool = True,
    include_trailing: bool = True,
    num_proc=None,
    desc="Inserting silence segments",
):

    def _fn(example):
        audio = example["audio"]
        duration = len(audio["array"]) / float(audio["sampling_rate"])

        return {
            "phonemes": insert_silence_segments(
                example["phonemes"],
                duration,
                min_silence_duration,
                include_leading,
                include_trailing,
            )
        }

    return dataset.map(_fn, desc=desc, num_proc=num_proc)


