from pathlib import Path
import json

import numpy as np
import torch
import torchaudio

from torch.utils.data import Dataset, DataLoader
from tqdm import tqdm

from model.config import TrainConfig
from model.models import MelWithFlatness
from utils.dataset_helpers import phoneme_to_viseme, yield_visemes


FEATURE_SAVE_DTYPE = torch.float16
LABEL_SAVE_DTYPE = torch.int16


class FramedVisemeDataset(Dataset):
    """
    Przeksztalcenie audio do mel spectogramow. Ustawienie dla kazdej "klatki" audio labelu z wizemami.
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

        self.frontend = MelWithFlatness(
            sample_rate=config.sample_rate,
            n_fft=config.n_fft,
            hop_length=config.hop_length,
            n_mels=config.n_mels,
            use_flatness=bool(getattr(config, "use_flatness", False)),
            flatness_eps=float(getattr(config, "flatness_eps", 1e-8)),
        )

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

        mel = self.frontend(waveform)[0]

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
    Wczytanie prekompowanych mel spektrogramow + etykiet z plikow .pt.
    augment=True wlacza SpecAugment (tylko dla zbioru treningowego).
    """

    def __init__(
        self,
        feature_dir,
        augment=False,
        freq_mask_param=20,
        time_mask_param=40,
        num_time_masks=2,
        mel_mean=0.0,
        mel_std=1.0,
        gain_aug_db=0.0,
        n_mels=80,
        flat_std=1.0,
        flat_mean=0.0
    ):
        self.gain_aug_db = float(gain_aug_db)
        self.feature_dir = Path(feature_dir)
        self.files = sorted(self.feature_dir.glob("*.pt"))
        self.n_mels = n_mels
        self.flat_std = flat_std
        self.flat_mean = flat_mean

        if len(self.files) == 0:
            raise FileNotFoundError(
                f"No precomputed .pt files found in: {feature_dir}"
            )

        self.augment = augment
        self.num_time_masks = num_time_masks

        if augment:
            self.freq_mask = torchaudio.transforms.FrequencyMasking(freq_mask_param)
            self.time_mask = torchaudio.transforms.TimeMasking(time_mask_param)

        self.lengths = self._load_lengths()
        self.mel_mean = float(mel_mean)
        self.mel_std = float(mel_std)

    def _load_lengths(self):
        index_path = self.feature_dir / "lengths.json"

        if index_path.exists():
            with open(index_path, "r", encoding="utf-8") as f:
                table = json.load(f)

            lengths = [int(table.get(p.stem, 0)) for p in self.files]
            if all(l > 0 for l in lengths):
                return lengths

        lengths = []
        for p in tqdm(self.files, desc="Reading lengths", leave=False):
            data = torch.load(p, map_location="cpu")
            lengths.append(int(data["labels"].numel()))
        return lengths

    def __len__(self):
        return len(self.files)

    def __getitem__(self, idx):
        data = torch.load(self.files[idx], map_location="cpu")
        feats = data["mel"].to(torch.float32)
        n = self.n_mels

        if self.augment and self.gain_aug_db > 0:
            offset = torch.empty(1).uniform_(
                -self.gain_aug_db, self.gain_aug_db
            ).item()
            feats[:n] = feats[:n] + offset

        feats[:n] = (feats[:n] - self.mel_mean) / (self.mel_std + 1e-6)
        if feats.shape[0] > n:
            feats[n:] = (feats[n:] - self.flat_mean) / (self.flat_std + 1e-6)

        labels = data["labels"].to(torch.int64).numpy()

        if self.augment:
            feats[:n] = self.freq_mask(feats[:n])
            for _ in range(self.num_time_masks):
                feats = self.time_mask(feats)

        return feats, labels


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

        lengths = {}
        n_mel = self.config.n_mels

        mel_n = 0; mel_s = 0.0; mel_sq = 0.0
        fl_n = 0;  fl_s = 0.0;  fl_sq = 0.0

        for idx in tqdm(range(len(base_dataset)), desc=desc):
            mel, labels = base_dataset[idx]
            lengths[f"{idx:08d}"] = int(mel.shape[1])

            m = mel.double()
            mp = m[:n_mel]
            mel_n += mp.numel()
            mel_s += mp.sum().item()
            mel_sq += (mp * mp).sum().item()

            if m.shape[0] > n_mel:
                fp = m[n_mel:]
                fl_n += fp.numel()
                fl_s += fp.sum().item()
                fl_sq += (fp * fp).sum().item()

            torch.save(
                {
                    "mel": mel.to(FEATURE_SAVE_DTYPE),
                    "labels": torch.from_numpy(labels).to(LABEL_SAVE_DTYPE),
                },
                output_dir / f"{idx:08d}.pt",
            )

        with open(output_dir / "lengths.json", "w") as f:
            json.dump(lengths, f)

        def _ms(s, sq, n):
            if n == 0:
                return 0.0, 1.0
            mu = s / n
            return mu, max(sq / n - mu * mu, 1e-12) ** 0.5

        mel_mean, mel_std = _ms(mel_s, mel_sq, mel_n)
        flat_mean, flat_std = _ms(fl_s, fl_sq, fl_n)

        stats = {
            "mel_mean": mel_mean, "mel_std": mel_std,
            "flat_mean": flat_mean, "flat_std": flat_std,
        }
        with open(output_dir / "feature_stats.json", "w") as f:
            json.dump(stats, f, indent=2)

        print(f"{desc}: mel {mel_mean:.2f}±{mel_std:.2f} | "
              f"flat {flat_mean:.2f}±{flat_std:.2f}")
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


def make_dataloader(dataset, config: TrainConfig, shuffle: bool,
                    bucket_by_length: bool = True):
    num_workers = int(getattr(config, "num_workers", 0) or 0)
    prefetch_factor = int(getattr(config, "prefetch_factor", 2) or 2)

    loader_kwargs = {
        "num_workers": num_workers,
        "collate_fn": collate_frames,
    }

    if num_workers > 0:
        loader_kwargs["persistent_workers"] = True
        loader_kwargs["prefetch_factor"] = prefetch_factor

    if torch.cuda.is_available():
        loader_kwargs["pin_memory"] = True

    lengths = getattr(dataset, "lengths", None)

    if bucket_by_length and lengths is not None:
        batch_sampler = LengthBucketBatchSampler(
            lengths=lengths,
            batch_size=config.batch_size,
            shuffle=shuffle,
            seed=getattr(config, "seed", 0),
            drop_last=shuffle,
        )
        return DataLoader(dataset, batch_sampler=batch_sampler, **loader_kwargs)

    return DataLoader(
        dataset,
        batch_size=config.batch_size,
        shuffle=shuffle,
        drop_last=shuffle,
        **loader_kwargs,
    )


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
    
    
class LengthBucketBatchSampler:
    """
    Sortuje probki po dlugosci, buduje batche z podobnych dlugosci,
    a nastepnie losuje kolejnosc batchy (nie ich sklad).
    """

    def __init__(self, lengths, batch_size, shuffle=True, seed=0, drop_last=False):
        self.lengths = np.asarray(lengths, dtype=np.int64)
        self.batch_size = int(batch_size)
        self.shuffle = shuffle
        self.drop_last = drop_last
        self.rng = np.random.default_rng(seed)

    def __iter__(self):
        order = np.argsort(self.lengths, kind="stable")

        batches = [
            order[i:i + self.batch_size].tolist()
            for i in range(0, len(order), self.batch_size)
        ]

        if self.drop_last and batches and len(batches[-1]) < self.batch_size:
            batches.pop()

        if self.shuffle:
            perm = self.rng.permutation(len(batches))
            batches = [batches[i] for i in perm]

        return iter(batches)

    def __len__(self):
        n = len(self.lengths)
        if self.drop_last:
            return n // self.batch_size
        return (n + self.batch_size - 1) // self.batch_size
    
    
def count_frame_labels(feature_dir, num_classes, ignore_index=-100):
    counts = np.zeros(num_classes, dtype=np.int64)

    for f in sorted(Path(feature_dir).glob("*.pt")):
        labels = torch.load(f, map_location="cpu")["labels"].numpy()
        valid = labels[labels != ignore_index].astype(np.int64)
        if valid.size:
            counts += np.bincount(valid, minlength=num_classes)

    return counts