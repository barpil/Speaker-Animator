from pathlib import Path
import sys

PROJECT_DIR = Path.cwd().parent
sys.path.insert(0, str(PROJECT_DIR))


import json
import tempfile
import uuid

from dataclasses import replace
from datetime import datetime
from pathlib import Path
from collections import Counter

import numpy as np
import torch
import torchaudio

from model.config import TrainConfig
from utils.dataset_helpers import phoneme_to_viseme, yield_visemes

from model.models import (
    MelFeatureExtractor,
    VisemeClassifier,
    FullVisemeModel,
)

from model.data import (
    FeaturePrecomputer,
    PrecomputedMelDataset,
    VisemeVocabulary,
    make_dataloader,
    split_by_speaker,
)

from model.model_training import Trainer, FrameEvaluator


class ModelRun:
    """
    Represents one training run directory inside the model catalog.

    Can:
      - create a new unique run
      - train a model
      - test a saved model
      - load a model for inference
      - predict probabilities from raw audio
    """

    def __init__(self, run_dir, config=None):
        self.run_dir = Path(run_dir)

        if config is None:
            config_path = self.run_dir / "config.json"

            with open(config_path, "r", encoding="utf-8") as f:
                config = TrainConfig.from_dict(json.load(f))

        self.config = config

        self.checkpoint_path = self.run_dir / "best_model.pt"
        self.labels_path = self.run_dir / "labels.json"

        self._checkpoint = None
        self._full_model = None

    @classmethod
    def create(cls, config: TrainConfig, catalog_root=None):
        root = Path(catalog_root or config.model_catalog_dir)
        root.mkdir(parents=True, exist_ok=True)

        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        short_id = uuid.uuid4().hex[:6]

        run_name = f"{timestamp}_{config.mode}_{short_id}"
        run_dir = root / run_name
        run_dir.mkdir(parents=True, exist_ok=True)

        with open(run_dir / "config.json", "w", encoding="utf-8") as f:
            json.dump(config.to_dict(), f, indent=2)

        metadata = {
            "run_name": run_name,
            "mode": config.mode,
            "created_at": datetime.now().isoformat(),
            "seed": config.seed,
        }

        with open(run_dir / "run_metadata.json", "w", encoding="utf-8") as f:
            json.dump(metadata, f, indent=2)

        print(f"Created model run directory: {run_dir}")

        return cls(run_dir, config)

    def load_checkpoint(self):
        if self._checkpoint is None:
            if not self.checkpoint_path.exists():
                raise FileNotFoundError(
                    f"No checkpoint found: {self.checkpoint_path}"
                )

            self._checkpoint = torch.load(
                self.checkpoint_path,
                map_location="cpu",
                weights_only=False,
            )

        return self._checkpoint

    def get_vocabulary(self):
        checkpoint = self.load_checkpoint()
        return VisemeVocabulary(checkpoint["labels"])

    def build_classifier(self, device=None):
        checkpoint = self.load_checkpoint()
        config = self.config

        vocabulary = self.get_vocabulary()

        model = VisemeClassifier(
            num_classes=len(vocabulary),
            n_mels=config.n_mels,
            hidden_dim=config.hidden_dim,
            dropout=config.dropout,
        )

        model.load_state_dict(checkpoint["model_state_dict"])

        if device is not None:
            model = model.to(device)

        model.eval()

        return model

    def build_full_model(self, device=None):
        if self._full_model is None:
            config = self.config

            classifier = self.build_classifier(device=device)

            extractor = MelFeatureExtractor(
                sample_rate=config.sample_rate,
                n_fft=config.n_fft,
                hop_length=config.hop_length,
                n_mels=config.n_mels,
            )

            if device is not None:
                extractor = extractor.to(device)

            full_model = FullVisemeModel(
                feature_extractor=extractor,
                classifier=classifier,
            )

            full_model.eval()

            self._full_model = full_model

        return self._full_model

    def train(self, train_split):
        config = self.config

        device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
        print(f"Using device: {device}")

        # Split by speaker
        train_hf, val_hf = split_by_speaker(
            train_split,
            config.val_ratio,
            config.seed,
        )

        # Build vocabulary from the provided training data
        vocabulary = VisemeVocabulary.from_dataset(train_split)
        vocabulary.save(self.labels_path)

        print(f"Number of viseme classes: {len(vocabulary)}")
        print(f"Viseme labels: {vocabulary.labels}")

        # Class weights
        counter = Counter(yield_visemes(train_hf))

        counts = np.array(
            [counter.get(label, 0) for label in vocabulary.labels],
            dtype=np.float64,
        )

        weights = np.where(counts > 0, 1.0 / (counts + 1.0), 0.0)

        if weights.sum() > 0:
            weights = weights / weights.sum() * len(vocabulary)
        else:
            weights = np.ones_like(weights)

        weights = np.clip(weights, None, weights.mean() * 5.0)

        class_weights = torch.tensor(weights, dtype=torch.float32).to(device)

        criterion = torch.nn.CrossEntropyLoss(
            weight=class_weights,
            ignore_index=config.ignore_index,
        )

        # Classifier only during training
        model = VisemeClassifier(
            num_classes=len(vocabulary),
            n_mels=config.n_mels,
            hidden_dim=config.hidden_dim,
            dropout=config.dropout,
        ).to(device)

        # Temporary precomputed feature cache inside run directory
        with tempfile.TemporaryDirectory(
            prefix="features_",
            dir=self.run_dir,
        ) as tmp_dir:
            tmp_path = Path(tmp_dir)

            train_feature_dir = tmp_path / "train"
            val_feature_dir = tmp_path / "val"

            precomputer = FeaturePrecomputer(
                config,
                label_transform=phoneme_to_viseme,
            )

            precomputer.run(
                train_hf,
                vocabulary.label2id,
                train_feature_dir,
                desc="Precomputing train features",
            )

            precomputer.run(
                val_hf,
                vocabulary.label2id,
                val_feature_dir,
                desc="Precomputing validation features",
            )

            train_dataset = PrecomputedMelDataset(train_feature_dir)
            val_dataset = PrecomputedMelDataset(val_feature_dir)

            print(f"Precomputed train samples: {len(train_dataset)}")
            print(f"Precomputed validation samples: {len(val_dataset)}")

            train_loader = make_dataloader(
                train_dataset,
                config,
                shuffle=True,
            )

            val_loader = make_dataloader(
                val_dataset,
                config,
                shuffle=False,
            )

            trainer = Trainer(
                model=model,
                config=config,
                train_loader=train_loader,
                val_loader=val_loader,
                criterion=criterion,
                device=device,
                vocabulary=vocabulary,
                save_dir=self.run_dir,
            )

            trainer.fit()

            del train_loader
            del val_loader
            del train_dataset
            del val_dataset

        print("Temporary feature cache deleted.")

        return self.checkpoint_path

    def test(self, test_split, batch_size=None, num_workers=0):
        """
        Tests saved model on a HuggingFace split, for example test_clean.
        Saves test_results.json inside the run directory.
        """
        device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
        print(f"Testing using device: {device}")

        checkpoint = self.load_checkpoint()
        vocabulary = self.get_vocabulary()

        classifier = self.build_classifier(device=device)

        test_config = replace(
            self.config,
            batch_size=batch_size or self.config.batch_size,
            num_workers=num_workers,
        )

        # For test metrics, use unweighted loss.
        criterion = torch.nn.CrossEntropyLoss(
            ignore_index=test_config.ignore_index,
        )

        with tempfile.TemporaryDirectory(
            prefix="test_features_",
            dir=self.run_dir,
        ) as tmp_dir:
            tmp_path = Path(tmp_dir)

            precomputer = FeaturePrecomputer(
                test_config,
                label_transform=phoneme_to_viseme,
            )

            precomputer.run(
                test_split,
                vocabulary.label2id,
                tmp_path,
                desc="Precomputing test features",
            )

            test_dataset = PrecomputedMelDataset(tmp_path)

            test_loader = make_dataloader(
                test_dataset,
                test_config,
                shuffle=False,
            )

            evaluator = FrameEvaluator(
                criterion=criterion,
                id2label=vocabulary.id2label,
                device=device,
                ignore_index=test_config.ignore_index,
            )

            result = evaluator.evaluate(
                classifier,
                test_loader,
                desc="Testing",
            )

        test_results = {
            "run_dir": str(self.run_dir),
            "checkpoint_epoch": checkpoint.get("epoch"),
            "num_test_samples": len(test_split),
            "loss": result["loss"],
            "accuracy": result["accuracy"],
            "num_frames": result["num_frames"],
            "report_dict": result["report_dict"],
            "confusion_matrix": result["confusion_matrix"],
            "labels": vocabulary.labels,
        }

        output_path = self.run_dir / "test_results.json"

        with open(output_path, "w", encoding="utf-8") as f:
            json.dump(test_results, f, indent=2)

        print(f"Test loss: {result['loss']:.4f}")
        print(f"Test accuracy: {result['accuracy']:.4f}")
        print(f"Test results saved to: {output_path}")

        if result["report_str"]:
            print(result["report_str"])

        return test_results

    @torch.no_grad()
    def predict_audio(self, audio_array, sample_rate=None, device=None):
        """
        Predict viseme probabilities from raw audio.

        Returns:
            probs: numpy array [time_frames, num_classes]
            labels: list of class names
        """
        if device is None:
            device = torch.device("cuda" if torch.cuda.is_available() else "cpu")

        full_model = self.build_full_model(device=device)

        waveform = torch.tensor(audio_array, dtype=torch.float32)

        if waveform.ndim > 1:
            waveform = waveform.mean(dim=0)

        if sample_rate is not None and sample_rate != self.config.sample_rate:
            waveform = torchaudio.functional.resample(
                waveform,
                orig_freq=sample_rate,
                new_freq=self.config.sample_rate,
            )

        waveform = waveform.unsqueeze(0).to(device)

        probs = full_model(waveform)

        probs = probs.squeeze(0).cpu().numpy()

        vocabulary = self.get_vocabulary()

        return probs, vocabulary.labels