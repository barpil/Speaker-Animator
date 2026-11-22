from dataclasses import dataclass, asdict, fields
from enum import Enum
from typing import Optional


class TrainingMode(str, Enum):
    FULL  = "full",
    DEBUG = "debug"
    
    
    

@dataclass
class TrainConfig:
    data_path: str = "../data"
    train_split_name: str = "train_clean_100"
    test_split_name: str = "test_clean"
    debug_size: Optional[int] = 200
    seed: int = 0
    val_ratio: float = 0.1
    
    # Architecture
    model_arch: str = "tds"
    tds_blocks: int = 4
    tds_kernel_size: int = 17
    tds_expansion: int = 2
    norm_type: str = "batch"
    norm_groups: int = 8
    lstm_layers: int = 2

    sample_rate: int = 16000
    n_fft: int = 400
    hop_length: int = 160
    n_mels: int = 80
    ignore_index: int = -100
    min_silence_duration: float = 0.1
    gain_aug_db: float = 18.0
    include_leading_silence: bool = True
    include_trailing_silence: bool = True

    hidden_dim: int = 256
    dropout: float = 0.2
    use_flatness: bool = True
    flatness_eps: float = 1e-8

    num_epochs: int = 15
    batch_size: int = 8
    lr: float = 3e-4
    num_workers: int = 4
    prefetch_factor: int = 2
    use_amp: bool = True

    mode: TrainingMode = TrainingMode.FULL
    model_catalog_dir: str = "./model_catalog"

    @classmethod
    def for_mode(cls, mode: TrainingMode, **overrides):
        if mode == TrainingMode.DEBUG:
            config = cls(
                mode=TrainingMode.DEBUG,
                debug_size=200,
                num_epochs=3,
                batch_size=4,
                lr=1e-3,
                num_workers=0,
            )
        elif mode == TrainingMode.FULL:
            config = cls(
                mode=TrainingMode.FULL,
                debug_size=None,
                num_epochs=15,
                batch_size=8,
                lr=3e-4,
                num_workers=4,
            )
        else:
            raise ValueError(f"Unknown mode: {mode}")

        for key, value in overrides.items():
            if not hasattr(config, key):
                raise ValueError(f"Unknown config field: {key}")
            setattr(config, key, value)

        return config

    def to_dict(self):
        return asdict(self)

    @classmethod
    def from_dict(cls, data: dict):
        allowed = {f.name for f in fields(cls)}
        filtered = {k: v for k, v in data.items() if k in allowed}
        return cls(**filtered)