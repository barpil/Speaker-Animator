import csv
import json
import time
from pathlib import Path

import numpy as np
import torch

from sklearn.metrics import classification_report, confusion_matrix
from tqdm import tqdm


def extract_scalar_report_metrics(report_dict, prefix="val"):
    if not report_dict:
        return {}

    metrics = {}

    if "accuracy" in report_dict:
        metrics[f"{prefix}_accuracy_report"] = report_dict["accuracy"]

    for avg_key, short_name in [
        ("macro avg", "macro"),
        ("weighted avg", "weighted"),
    ]:
        if avg_key in report_dict:
            metrics[f"{prefix}_precision_{short_name}"] = report_dict[avg_key].get("precision")
            metrics[f"{prefix}_recall_{short_name}"] = report_dict[avg_key].get("recall")
            metrics[f"{prefix}_f1_{short_name}"] = report_dict[avg_key].get("f1-score")

    return metrics


def json_serializer(obj):
    if isinstance(obj, np.integer):
        return int(obj)

    if isinstance(obj, np.floating):
        return float(obj)

    if isinstance(obj, np.ndarray):
        return obj.tolist()

    return str(obj)


class TrainingHistory:
    """
    Zapisanie historii JSON+CSV
    """

    def __init__(self, save_dir):
        self.save_dir = Path(save_dir)
        self.save_dir.mkdir(parents=True, exist_ok=True)

        self.records = []

        self.json_path = self.save_dir / "training_history.json"
        self.csv_path = self.save_dir / "training_history.csv"

    def add(self, record):
        self.records.append(record)
        self.save()

    def save(self):
        with open(self.json_path, "w", encoding="utf-8") as f:
            json.dump(self.records, f, indent=2, default=json_serializer)

        fieldnames = [
            "epoch",
            "train_loss",
            "train_acc",
            "val_loss",
            "val_acc",
            "val_accuracy_report",
            "val_precision_macro",
            "val_recall_macro",
            "val_f1_macro",
            "val_precision_weighted",
            "val_recall_weighted",
            "val_f1_weighted",
            "lr",
            "duration_sec",
        ]

        with open(self.csv_path, "w", newline="", encoding="utf-8") as f:
            writer = csv.DictWriter(
                f,
                fieldnames=fieldnames,
                restval="",
                extrasaction="ignore",
            )

            writer.writeheader()

            for record in self.records:
                scalar_record = {
                    k: ("" if v is None else v)
                    for k, v in record.items()
                    if k != "val_report"
                }
                writer.writerow(scalar_record)


class FrameEvaluator:

    def __init__(self, criterion, id2label, device, ignore_index=-100):
        self.criterion = criterion
        self.id2label = id2label
        self.device = device
        self.ignore_index = ignore_index

    @torch.no_grad()
    def evaluate(self, model, loader, desc="Evaluating"):
        model.eval()

        total_loss = 0.0
        total_correct = 0
        total_frames = 0
        total_samples = 0

        all_true = []
        all_pred = []

        for mel, labels, lengths in tqdm(loader, desc=desc, leave=False):
            mel = mel.to(self.device, non_blocking=True)
            labels = labels.to(self.device, non_blocking=True)

            logits = model(mel, lengths)

            loss = self.criterion(
                logits.reshape(-1, logits.size(-1)),
                labels.reshape(-1),
            )

            batch_size = mel.size(0)
            total_samples += batch_size
            total_loss += loss.item() * batch_size

            mask = labels != self.ignore_index
            preds = logits.argmax(dim=-1)

            total_correct += (preds[mask] == labels[mask]).sum().item()
            total_frames += mask.sum().item()

            all_true.append(labels[mask].cpu())
            all_pred.append(preds[mask].cpu())

        avg_loss = total_loss / max(total_samples, 1)
        accuracy = total_correct / max(total_frames, 1)

        report_str = ""
        report_dict = {}
        cm = []

        if len(all_true) > 0:
            y_true = torch.cat(all_true).numpy()
            y_pred = torch.cat(all_pred).numpy()

            labels = list(range(len(self.id2label)))
            target_names = [self.id2label[i] for i in labels]

            report_str = classification_report(
                y_true,
                y_pred,
                labels=labels,
                target_names=target_names,
                zero_division=0,
            )

            report_dict = classification_report(
                y_true,
                y_pred,
                labels=labels,
                target_names=target_names,
                zero_division=0,
                output_dict=True,
            )

            cm = confusion_matrix(
                y_true,
                y_pred,
                labels=labels,
            ).tolist()

        return {
            "loss": avg_loss,
            "accuracy": accuracy,
            "report_str": report_str,
            "report_dict": report_dict,
            "confusion_matrix": cm,
            "num_frames": total_frames,
        }


class Trainer:

    def __init__(
        self,
        model,
        config,
        train_loader,
        val_loader,
        criterion,
        device,
        vocabulary,
        save_dir,
    ):
        self.model = model
        self.config = config
        self.train_loader = train_loader
        self.val_loader = val_loader
        self.criterion = criterion
        self.device = device
        self.vocabulary = vocabulary

        self.save_dir = Path(save_dir)
        self.save_dir.mkdir(parents=True, exist_ok=True)

        self.history = TrainingHistory(self.save_dir)

        self.evaluator = FrameEvaluator(
            criterion=criterion,
            id2label=vocabulary.id2label,
            device=device,
            ignore_index=config.ignore_index,
        )

        self.optimizer = torch.optim.AdamW(
            model.parameters(),
            lr=config.lr,
        )

        self.scheduler = torch.optim.lr_scheduler.ReduceLROnPlateau(
            self.optimizer,
            mode="max",
            factor=0.5,
            patience=1,
            min_lr=1e-5,
        )

        self.use_amp = bool(config.use_amp and device.type == "cuda")

        self.scaler = (
            torch.amp.GradScaler("cuda", enabled=True)
            if self.use_amp
            else None
        )

        self.best_val_f1 = -1.0

    def train_one_epoch(self, epoch):
        self.model.train()

        total_loss = 0.0
        total_correct = 0
        total_frames = 0
        total_samples = 0

        total_batches = len(self.train_loader)

        pbar = tqdm(
            self.train_loader,
            desc=f"Epoch {epoch + 1}/{self.config.num_epochs}",
            leave=True,
        )

        for batch_idx, (mel, labels, lengths) in enumerate(pbar, start=1):
            mel = mel.to(self.device, non_blocking=True)
            labels = labels.to(self.device, non_blocking=True)

            self.optimizer.zero_grad(set_to_none=True)

            with torch.autocast(
                device_type=self.device.type,
                dtype=torch.float16,
                enabled=self.use_amp,
            ):
                logits = self.model(mel, lengths)

                loss = self.criterion(
                    logits.reshape(-1, logits.size(-1)),
                    labels.reshape(-1),
                )

            if self.scaler is not None:
                self.scaler.scale(loss).backward()
                self.scaler.unscale_(self.optimizer)

                torch.nn.utils.clip_grad_norm_(
                    self.model.parameters(),
                    max_norm=1.0,
                )

                self.scaler.step(self.optimizer)
                self.scaler.update()
            else:
                loss.backward()

                torch.nn.utils.clip_grad_norm_(
                    self.model.parameters(),
                    max_norm=1.0,
                )

                self.optimizer.step()

            batch_size = mel.size(0)
            total_samples += batch_size
            total_loss += loss.item() * batch_size

            mask = labels != self.config.ignore_index
            preds = logits.argmax(dim=-1)

            batch_correct = (preds[mask] == labels[mask]).sum().item()
            batch_frames = mask.sum().item()

            total_correct += batch_correct
            total_frames += batch_frames

            running_acc = total_correct / max(total_frames, 1)

            pbar.set_postfix(
                {
                    "batch": f"{batch_idx}/{total_batches}",
                    "loss": f"{loss.item():.4f}",
                    "acc": f"{running_acc:.4f}",
                }
            )

        avg_loss = total_loss / max(total_samples, 1)
        frame_accuracy = total_correct / max(total_frames, 1)

        return avg_loss, frame_accuracy

    def save_checkpoint(self, epoch, val_loss):
        checkpoint = {
            "epoch": epoch + 1,
            "model_state_dict": self.model.state_dict(),
            "optimizer_state_dict": self.optimizer.state_dict(),
            "val_loss": val_loss,
            "labels": self.vocabulary.labels,
            "label2id": self.vocabulary.label2id,
            "id2label": self.vocabulary.id2label,
            "config": self.config.to_dict(),
            "model_type": "VisemeClassifier",
            "label_type": "viseme",
        }

        torch.save(
            checkpoint,
            self.save_dir / "best_model.pt",
        )

    def fit(self):
        print(f"Starting training for {self.config.num_epochs} epochs...")

        for epoch in range(self.config.num_epochs):
            start_time = time.time()

            train_loss, train_acc = self.train_one_epoch(epoch)

            val_result = self.evaluator.evaluate(
                self.model,
                self.val_loader,
                desc="Validating",
            )

            val_loss = val_result["loss"]
            val_acc = val_result["accuracy"]
            val_report_dict = val_result["report_dict"]

            val_f1 = val_result["report_dict"].get("macro avg", {}).get("f1-score", 0.0)
            self.scheduler.step(val_f1)

            current_lr = self.optimizer.param_groups[0]["lr"]
            duration_sec = time.time() - start_time

            record = {
                "epoch": epoch + 1,
                "train_loss": train_loss,
                "train_acc": train_acc,
                "val_loss": val_loss,
                "val_acc": val_acc,
                "lr": current_lr,
                "duration_sec": duration_sec,
                **extract_scalar_report_metrics(val_report_dict, prefix="val"),
                "val_report": val_report_dict or {},
            }

            self.history.add(record)

            print(f"\nEpoch {epoch + 1}/{self.config.num_epochs}")
            print(f"Train Loss: {train_loss:.4f}, Train Acc: {train_acc:.4f}")
            print(f"Val Loss: {val_loss:.4f}, Val Acc: {val_acc:.4f}")
            print(f"LR: {current_lr:.6f}, Time: {duration_sec:.2f}s")

            if epoch % 2 == 0 and val_result["report_str"]:
                print("Validation classification report:")
                print(val_result["report_str"])

            if val_f1 > self.best_val_f1:
                self.best_val_f1 = val_f1
                self.save_checkpoint(epoch, val_loss)
                print(f"Saved new best model. Val macro F1: {val_f1:.4f}")

        print("\nTraining finished.")
        print(f"Best model saved to: {self.save_dir / 'best_model.pt'}")
        print(f"History saved to: {self.history.json_path}")