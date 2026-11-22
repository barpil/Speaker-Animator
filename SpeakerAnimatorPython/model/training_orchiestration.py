from pathlib import Path
import sys

from tqdm import tqdm

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
from utils.dataset_helpers import add_silence_to_dataset, phoneme_to_viseme, yield_visemes

from model.models import (
    MelFeatureExtractor,
    VisemeClassifier,
    FullVisemeModel,
    build_viseme_classifier,
)

from model.data import (
    FeaturePrecomputer,
    PrecomputedMelDataset,
    VisemeVocabulary,
    count_frame_labels,
    make_dataloader,
    split_by_speaker,
)

from model.model_training import Trainer, FrameEvaluator


class ModelRun:

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

    def _apply_silence(self, split, vocabulary=None):
        cfg = self.config

        if vocabulary is not None and "sil" not in vocabulary.label2id:
            print("Checkpoint has no 'sil' class - skipping silence insertion.")
            return split

        return add_silence_to_dataset(
            split,
            min_silence_duration=cfg.min_silence_duration,
            include_leading=cfg.include_leading_silence,
            include_trailing=cfg.include_trailing_silence,
        )

    
    def get_vocabulary(self):
        checkpoint = self.load_checkpoint()
        return VisemeVocabulary(checkpoint["labels"])

    def build_classifier(self, device=None):
        checkpoint = self.load_checkpoint()
        config = self.config

        vocabulary = self.get_vocabulary()

        model = build_viseme_classifier(config, len(vocabulary))

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
                use_flatness=bool(getattr(config, "use_flatness", False)),
                flatness_eps=float(getattr(config, "flatness_eps", 1e-8)),
                **self._mel_stats(),
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

        train_split = self._apply_silence(train_split)

        train_hf, val_hf = split_by_speaker(train_split, config.val_ratio, config.seed)

        vocabulary = VisemeVocabulary.from_dataset(train_split)
        vocabulary.save(self.labels_path)

        print(f"Number of viseme classes: {len(vocabulary)}")
        print(f"Viseme labels: {vocabulary.labels}")

        if "sil" not in vocabulary.label2id:
            print("WARNING: 'sil' missing from vocabulary - silence will NOT be learned.")

        with tempfile.TemporaryDirectory(prefix="features_", dir=self.run_dir) as tmp_dir:
            tmp_path = Path(tmp_dir)
            train_feature_dir = tmp_path / "train"
            val_feature_dir = tmp_path / "val"

            precomputer = FeaturePrecomputer(config, label_transform=phoneme_to_viseme)

            precomputer.run(train_hf, vocabulary.label2id, train_feature_dir,
                            desc="Precomputing train features")
            
            with open(train_feature_dir / "feature_stats.json") as f:
                stats = json.load(f)
            with open(self.run_dir / "mel_stats.json", "w") as f:
                json.dump(stats, f, indent=2)
                
            precomputer.run(val_hf, vocabulary.label2id, val_feature_dir,
                            desc="Precomputing validation features")

            counts = count_frame_labels(train_feature_dir, len(vocabulary), config.ignore_index)
            total_frames = int(counts.sum())

            frame_stats = {
                label: {"count": int(c), "share": float(c / max(total_frames, 1))}
                for label, c in zip(vocabulary.labels, counts)
            }
            with open(self.run_dir / "train_frame_counts.json", "w", encoding="utf-8") as f:
                json.dump(frame_stats, f, indent=2)

            print("Frame distribution:")
            for label, c in zip(vocabulary.labels, counts):
                print(f"  {label:6s} {c:9d}  {c / max(total_frames, 1):6.2%}")

            counts_f = counts.astype(np.float64)
            weights = np.where(counts_f > 0, 1.0 / (counts_f + 1.0), 0.0)
            if weights.sum() > 0:
                weights = weights / weights.sum() * len(vocabulary)
            else:
                weights = np.ones_like(weights)
            weights = np.clip(weights, None, weights.mean() * 5.0)

            class_weights = torch.tensor(weights, dtype=torch.float32).to(device)
            criterion = torch.nn.CrossEntropyLoss(
                weight=class_weights, ignore_index=config.ignore_index, label_smoothing=0.05
            )

            model = build_viseme_classifier(config, len(vocabulary)).to(device)

            train_dataset = PrecomputedMelDataset(
                train_feature_dir, augment=True,
                gain_aug_db=config.gain_aug_db, n_mels=config.n_mels, **stats,
            )
            val_dataset = PrecomputedMelDataset(
                val_feature_dir, augment=False, n_mels=config.n_mels, **stats,
            )
            train_loader = make_dataloader(train_dataset, config, shuffle=True)
            val_loader = make_dataloader(val_dataset, config, shuffle=False)

            trainer = Trainer(
                model=model, config=config,
                train_loader=train_loader, val_loader=val_loader,
                criterion=criterion, device=device,
                vocabulary=vocabulary, save_dir=self.run_dir,
            )
            trainer.fit()

            del train_loader, val_loader, train_dataset, val_dataset

        print("Temporary feature cache deleted.")
        return self.checkpoint_path

    def test(self, test_split, batch_size=None, num_workers=0):
        device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
        print(f"Testing using device: {device}")

        checkpoint = self.load_checkpoint()
        vocabulary = self.get_vocabulary()

        test_split = self._apply_silence(test_split, vocabulary)
        silence_applied = "sil" in vocabulary.label2id

        classifier = self.build_classifier(device=device)

        test_config = replace(
            self.config,
            batch_size=batch_size or self.config.batch_size,
            num_workers=num_workers,
        )

        criterion = torch.nn.CrossEntropyLoss(ignore_index=test_config.ignore_index)

        with tempfile.TemporaryDirectory(prefix="test_features_", dir=self.run_dir) as tmp_dir:
            tmp_path = Path(tmp_dir)

            precomputer = FeaturePrecomputer(test_config, label_transform=phoneme_to_viseme)
            precomputer.run(test_split, vocabulary.label2id, tmp_path,
                            desc="Precomputing test features")

            test_dataset = PrecomputedMelDataset(
                tmp_path, augment=False, n_mels=self.config.n_mels, **self._mel_stats(),
            )
            test_loader = make_dataloader(test_dataset, test_config, shuffle=False)

            evaluator = FrameEvaluator(
                criterion=criterion, id2label=vocabulary.id2label,
                device=device, ignore_index=test_config.ignore_index,
            )
            result = evaluator.evaluate(classifier, test_loader, desc="Testing")

        test_results = {
            "run_dir": str(self.run_dir),
            "checkpoint_epoch": checkpoint.get("epoch"),
            "num_test_samples": len(test_split),
            "silence_applied": silence_applied,
            "min_silence_duration": self.config.min_silence_duration,
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
        print(f"Silence evaluated: {silence_applied}")
        if result["report_str"]:
            print(result["report_str"])

        return test_results
    
    @torch.no_grad()
    def diagnose_boundaries(self, split, max_utterances=300):
        """
        Accuracy as a function of distance to the nearest phoneme boundary.
        Tells you whether errors come from the model or from alignment noise.
        Zbadanie dystansu pomiedzy wizemami, pozwala na okreslenie czy bledy pochodza z modelu czy z blednych etykiet
        """
        device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
        vocabulary = self.get_vocabulary()
        classifier = self.build_classifier(device=device)

        split = self._apply_silence(split, vocabulary)
        n = min(max_utterances, len(split))
        split = split.select(range(n))

        cfg = self.config
        hop = cfg.hop_length / cfg.sample_rate
        win = cfg.n_fft / cfg.sample_rate

        edges = [("<10ms", 0, 10), ("10-30ms", 10, 30),
                 ("30-50ms", 30, 50), (">50ms", 50, 1e9)]
        correct = {name: 0 for name, _, _ in edges}
        total = {name: 0 for name, _, _ in edges}

        sil_id = vocabulary.label2id.get("sil")
        speech_correct = speech_total = 0

        with tempfile.TemporaryDirectory(prefix="diag_", dir=self.run_dir) as tmp:
            FeaturePrecomputer(cfg, label_transform=phoneme_to_viseme).run(
                split, vocabulary.label2id, tmp, desc="Diagnostic features"
            )
            dataset = PrecomputedMelDataset(tmp, n_mels=cfg.n_mels, **self._mel_stats())

            for i in tqdm(range(len(dataset)), desc="Boundary analysis"):
                mel, labels = dataset[i]

                logits = classifier(mel.unsqueeze(0).to(device))
                pred = logits.argmax(-1)[0].cpu().numpy()

                phonemes = split[i]["phonemes"]
                bounds = np.array(
                    [float(p[k]) for p in phonemes for k in ("start", "end")],
                    dtype=np.float64,
                )
                if bounds.size == 0:
                    continue

                times = np.arange(len(labels)) * hop + win / 2.0
                dist_ms = np.abs(times[:, None] - bounds[None, :]).min(axis=1) * 1000.0

                valid = labels >= 0
                hits = pred == labels

                for name, lo, hi in edges:
                    m = valid & (dist_ms >= lo) & (dist_ms < hi)
                    total[name] += int(m.sum())
                    correct[name] += int((hits & m).sum())

                if sil_id is not None:
                    sm = valid & (labels != sil_id)
                    speech_total += int(sm.sum())
                    speech_correct += int((hits & sm).sum())

        print(f"\nBoundary analysis on {n} utterances")
        print(f"{'distance':>10} {'frames':>10} {'accuracy':>10}")
        for name, _, _ in edges:
            t = total[name]
            acc = correct[name] / t if t else float("nan")
            print(f"{name:>10} {t:>10d} {acc:>9.2%}")

        if speech_total:
            print(f"\nSpeech-only accuracy (sil excluded): "
                  f"{speech_correct / speech_total:.2%}")

        return {name: {"frames": total[name], "correct": correct[name]}
                for name, _, _ in edges}

    @torch.no_grad()
    def predict_audio(self, audio_array, sample_rate=None, device=None):
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
    
    
    def _mel_stats(self):
        path = self.run_dir / "mel_stats.json"
        if path.exists():
            with open(path, "r", encoding="utf-8") as f:
                return json.load(f)
        print("WARNING: mel_stats.json missing — using (0, 1). Old run?")
        return {"mel_mean": 0.0, "mel_std": 1.0}