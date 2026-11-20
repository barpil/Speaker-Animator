import json
from pathlib import Path

import torch

from model.training_orchiestration import ModelRun

MODEL_CATALOG = ""
OUTPUT_PATH = "."



def main():
    run = ModelRun(MODEL_CATALOG)
    config = run.config

    full_model = run.build_full_model(device=torch.device("cpu"))
    full_model.eval()

    dummy_audio = torch.randn(1, config.sample_rate * 2)

    torch.onnx.export(
        full_model,
        dummy_audio,
        OUTPUT_PATH / "viseme_model.onnx",
        input_names=["audio"],
        output_names=["probs"],
        dynamic_axes={
            "audio": {
                0: "batch_size",
                1: "time_samples",
            },
            "probs": {
                0: "batch_size",
                1: "time_frames",
            },
        },
        opset_version=17,
    )

    vocabulary = run.get_vocabulary()

    with open(OUTPUT_PATH / "viseme_labels.json", "w", encoding="utf-8") as f:
        json.dump(vocabulary.labels, f, indent=2)

    print(f"ONNX model and labels exported to: {OUTPUT_PATH}")



if __name__ == "__main__":
    main()