from pathlib import Path
import sys

PROJECT_DIR = Path.cwd().parent
sys.path.insert(0, str(PROJECT_DIR))

from datasets import load_from_disk

from model.config import TrainConfig, TrainingMode
from model.training_orchiestration import ModelRun


# Podanie None sprawia ze uzywane sa defaultowe wartosci

MODE = TrainingMode.FULL
DEBUG_SIZE = 200
DATA_PATH = "../data"
SEED = 0
EPOCHS = 15
BATCH_SIZE = 8
LR = 3e-4
NUM_WORKERS = 4



def main():
    overrides = {}

    if DATA_PATH is not None:
        overrides["data_path"] = DATA_PATH

    if DEBUG_SIZE is not None:
        overrides["debug_size"] = DEBUG_SIZE

    if SEED is not None:
        overrides["seed"] = SEED

    if EPOCHS is not None:
        overrides["num_epochs"] = EPOCHS

    if BATCH_SIZE is not None:
        overrides["batch_size"] = BATCH_SIZE

    if LR is not None:
        overrides["lr"] = LR

    if NUM_WORKERS is not None:
        overrides["num_workers"] = NUM_WORKERS

    config = TrainConfig.for_mode(MODE, **overrides)

    print(f"Running mode: {config.mode}")
    print(f"Config: {config.to_dict()}")

    dataset = load_from_disk(config.data_path)
    train_split = dataset[config.train_split_name]

    if config.mode == TrainingMode.DEBUG and config.debug_size:
        debug_size = min(config.debug_size, len(train_split))

        print(f"DEBUG MODE: using {debug_size} samples")

        train_split = (
            train_split
            .shuffle(seed=config.seed)
            .select(range(debug_size))
        )

    run = ModelRun.create(config)
    checkpoint_path = run.train(train_split)

    print(f"Training complete. Checkpoint: {checkpoint_path}")
    print(f"Run directory: {run.run_dir}")


if __name__ == "__main__":
    main()