from pathlib import Path
import sys

PROJECT_DIR = Path.cwd().parent
sys.path.insert(0, str(PROJECT_DIR))

from datasets import load_from_disk

from model.training_orchiestration import ModelRun

 #None = defaultowe z konfiguracji

MODEL_CATALOG = "C:/Users/Bcom_/Documents/Projekty/SpeakerAnimator/SpeakerAnimatorPython/model/model_catalog/20261120_235501_TrainingMode.DEBUG_9d4f5d"

DATA_PATH = "../data"
SPLIT_NAME = None
BATCH_SIZE = 2
NUM_WORKERS = 2

def main():
    run = ModelRun(MODEL_CATALOG)

    data_path = DATA_PATH or run.config.data_path
    split_name = SPLIT_NAME or run.config.test_split_name

    dataset = load_from_disk(data_path)
    test_split = dataset[split_name]

    print(f"Testing run: {run.run_dir}")
    print(f"Test split: {split_name}")
    print(f"Test samples: {len(test_split)}")

    run.test(
        test_split,
        batch_size=BATCH_SIZE,
        num_workers=NUM_WORKERS,
    )
    
    run.diagnose_boundaries(test_split)


if __name__ == "__main__":
    main()