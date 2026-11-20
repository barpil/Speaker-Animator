from pathlib import Path
import json

import numpy as np
import torch
import torchaudio

from torch.utils.data import Dataset, DataLoader
from tqdm import tqdm

from model.config import TrainConfig
from utils.dataset_helpers import phoneme_to_viseme, yield_visemes


FEATURE_SAVE_DTYPE = torch.float16
LABEL_SAVE_DTYPE = torch.int16


class FramedVisemeDataset(Dataset):
    """
    Przeksztalcenie audio do mel spectogramow. Ustawienie dla kazdej "klatki" audio labelu z wizemami.
    Used for precomputing features.
    """

    def __init__(
        self,
        dataset,
        label2id,
        config: TrainConfig,
        label_transform=None,
    ):
        self.dataset = dataset
        self.label2id = label2id
        self.config = config

        self.label_transform = label_transform or (lambda x: x)

        self.hop_sec = config.hop_length / config.sample_rate
        self.win_sec = config.n_fft / config.sample_rate

        self.mel_transform = torchaudio.transforms.MelSpectrogram(
            sample_rate=config.sample_rate,
            n_fft=config.n_fft,
            hop_length=config.hop_length,
            n_mels=config.n_mels,
            power=2.0,
        )

        self.db_transform = torchaudio.transforms.AmplitudeToDB(top_db=80)

    def __len__(self):
        return len(self.dataset)

    def __getitem__(self, idx):
        row = self.dataset[idx]

        waveform = torch.tensor(row["audio"]["array"], dtype=torch.float32)

        if waveform.ndim > 1:
            waveform = waveform.mean(dim=0)

        original_sr = int(row["audio"]["sampling_rate"])

        if original_sr != self.config.sample_rate:
            waveform = torchaudio.functional.resample(
                waveform,
                orig_freq=original_sr,
                new_freq=self.config.sample_rate,
            )

        mel = self.db_transform(self.mel_transform(waveform))
        mel = (mel - mel.mean()) / (mel.std() + 1e-6)

        frame_labels = self.make_frame_labels(
            phonemes=row["phonemes"],
            num_frames=mel.shape[1],
        )

        return mel, frame_labels

    def make_frame_labels(self, phonemes, num_frames):
        labels = np.full(
            num_frames,
            self.config.ignore_index,
            dtype=np.int64,
        )

        if num_frames == 0:
            return labels

        frame_times = (
            np.arange(num_frames) * self.hop_sec
            + self.win_sec / 2.0
        )

        phonemes = sorted(phonemes, key=lambda p: p["start"])

        for p in phonemes:
            label = self.label_transform(p["phoneme"])

            if label not in self.label2id:
                continue

            start = float(p["start"])
            end = float(p["end"])

            mask = (frame_times >= start) & (frame_times < end)
            labels[mask] = self.label2id[label]

        return labels


class PrecomputedMelDataset(Dataset):
    """
    Zaladowanie prekomputowanych danych z pliku .pt
    """

    def __init__(self, feature_dir):
        self.files = sorted(Path(feature_dir).glob("*.pt"))

        if len(self.files) == 0:
            raise FileNotFoundError(
                f"No precomputed .pt files found in: {feature_dir}"
            )

    def __len__(self):
        return len(self.files)

    def __getitem__(self, idx):
        data = torch.load(self.files[idx], map_location="cpu")

        mel = data["mel"].to(torch.float32)
        labels = data["labels"].to(torch.int64).numpy()

        return mel, labels


class FeaturePrecomputer:
    """
    Prekomputowanie featurow i zapisanie ich na dysku
    """

    def __init__(self, config: TrainConfig, label_transform=None):
        self.config = config
        self.label_transform = label_transform

    def run(
        self,
        hf_split,
        label2id,
        output_dir,
        desc="Precomputing features",
    ):
        output_dir = Path(output_dir)
        output_dir.mkdir(parents=True, exist_ok=True)

        base_dataset = FramedVisemeDataset(
            hf_split,
            label2id,
            self.config,
            label_transform=self.label_transform,
        )

        for idx in tqdm(range(len(base_dataset)), desc=desc):
            mel, labels = base_dataset[idx]

            torch.save(
                {
                    "mel": mel.to(FEATURE_SAVE_DTYPE),
                    "labels": torch.from_numpy(labels).to(LABEL_SAVE_DTYPE),
                },
                output_dir / f"{idx:08d}.pt",
            )

        return output_dir


def collate_frames(batch):
    mels, labels = zip(*batch)

    lengths = torch.tensor([mel.shape[1] for mel in mels], dtype=torch.long)
    max_len = max(lengths).item()

    n_mels = mels[0].shape[0]

    mel_batch = torch.zeros(len(mels), n_mels, max_len)
    label_batch = torch.full(
        (len(mels), max_len),
        -100,
        dtype=torch.long,
    )

    for i, (mel, label) in enumerate(zip(mels, labels)):
        time = mel.shape[1]

        mel_batch[i, :, :time] = mel
        label_batch[i, :time] = torch.from_numpy(label)

    return mel_batch, label_batch, lengths


def make_dataloader(dataset, config: TrainConfig, shuffle: bool):
    num_workers = int(getattr(config, "num_workers", 0) or 0)
    prefetch_factor = int(getattr(config, "prefetch_factor", 2) or 2)

    loader_kwargs = {
        "batch_size": config.batch_size,
        "shuffle": shuffle,
        "num_workers": num_workers,
        "collate_fn": collate_frames,
    }

    if num_workers > 0:
        loader_kwargs["persistent_workers"] = True
        loader_kwargs["prefetch_factor"] = prefetch_factor

    if torch.cuda.is_available():
        loader_kwargs["pin_memory"] = True

    if shuffle:
        loader_kwargs["drop_last"] = True

    return DataLoader(dataset, **loader_kwargs)


def split_by_speaker(dataset, val_ratio: float, seed: int):
    ids = dataset["id"]
    speakers = np.array([int(x.split("-")[0]) for x in ids])
    unique_speakers = np.unique(speakers)

    if len(unique_speakers) <= 1:
        print("Only one speaker found.\nUsing the same data for train and validation.")
        return dataset, dataset

    rng = np.random.default_rng(seed)
    rng.shuffle(unique_speakers)

    num_val_speakers = max(1, int(len(unique_speakers) * val_ratio))
    num_val_speakers = min(num_val_speakers, len(unique_speakers) - 1)

    val_speakers = unique_speakers[:num_val_speakers]
    val_mask = np.isin(speakers, val_speakers)

    train_split = dataset.select(np.where(~val_mask)[0].tolist())
    val_split = dataset.select(np.where(val_mask)[0].tolist())

    print(f"Train split size: {len(train_split)}")
    print(f"Validation split size: {len(val_split)}")

    return train_split, val_split


class VisemeVocabulary:
    """
    Slownik mapowan fonem (ARPAbet) - wizem (Meta Oculus)
    """

    def __init__(self, labels):
        self.labels = sorted(set(labels))
        self.label2id = {label: i for i, label in enumerate(self.labels)}
        self.id2label = {i: label for label, i in self.label2id.items()}

    @classmethod
    def from_dataset(cls, dataset):
        label_set = set(yield_visemes(dataset))

        if "unknown" in label_set:
            print("Warning: some phonemes mapped to 'unknown'.\nCheck ARPABET_TO_META.")

        return cls(label_set)

    def save(self, path):
        path = Path(path)
        path.parent.mkdir(parents=True, exist_ok=True)

        with open(path, "w", encoding="utf-8") as f:
            json.dump(
                {
                    "labels": self.labels,
                    "label2id": self.label2id,
                },
                f,
                indent=2,
            )

    @classmethod
    def load(cls, path):
        with open(path, "r", encoding="utf-8") as f:
            data = json.load(f)

        return cls(data["labels"])

    def __len__(self):
        return len(self.labels)