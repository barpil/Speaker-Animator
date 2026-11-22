import json
from pathlib import Path
import sys

PROJECT_DIR = Path.cwd().parent
sys.path.insert(0, str(PROJECT_DIR))

import torch

from model.models import MelFeatureExtractor
from model.training_orchiestration import ModelRun

MODEL_CATALOG = "C:/Users/Bcom_/Documents/Projekty/SpeakerAnimator/SpeakerAnimatorPython/model/model_catalog/20261121_231847_TrainingMode.FULL_9daa12"
OUTPUT_PATH = "."


def main():
    run = ModelRun(MODEL_CATALOG)
    config = run.config

    out = Path(OUTPUT_PATH)
    out.mkdir(parents=True, exist_ok=True)

    classifier = run.build_classifier(device=torch.device("cpu")).eval()
    extractor = MelFeatureExtractor(
        sample_rate=config.sample_rate,
        n_fft=config.n_fft,
        hop_length=config.hop_length,
        n_mels=config.n_mels,
        use_flatness=bool(getattr(config, "use_flatness", False)),
        flatness_eps=float(getattr(config, "flatness_eps", 1e-8)),
        **run._mel_stats(),
    ).eval()

    torch.onnx.export(
        extractor,
        torch.randn(1, config.sample_rate * 2),
        out / "mel_extractor.onnx",
        input_names=["audio"],
        output_names=["mel"],
        dynamic_axes={"audio": {1: "time_samples"}, "mel": {2: "time"}},
        opset_version=18,
    )

    in_ch = config.n_mels + (1 if getattr(config, "use_flatness", False) else 0)
    n_frames = config.sample_rate // config.hop_length  # ~1s
    torch.onnx.export(
        classifier,
        torch.randn(1, in_ch, n_frames),
        out / "viseme_classifier.onnx",
        input_names=["mel"],
        output_names=["logits"],
        dynamic_axes={"mel": {2: "time"}, "logits": {1: "time"}},
        opset_version=18,
    )

    with open(out / "viseme_labels.json", "w", encoding="utf-8") as f:
        json.dump(run.get_vocabulary().labels, f, indent=2)

    with open(out / "model_config.json", "w", encoding="utf-8") as f:
        json.dump({
            "sample_rate": config.sample_rate,
            "n_mels": config.n_mels,
            "n_fft": config.n_fft,
            "hop_length": config.hop_length,
            "use_flatness": int(bool(getattr(config, "use_flatness", False))),
            "n_input_channels": in_ch,
        }, f, indent=2)

    print(f"Exported 4 files to {out.resolve()}")


if __name__ == "__main__":
    main()