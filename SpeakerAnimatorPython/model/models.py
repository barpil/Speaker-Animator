import torch
import torch.nn as nn
import torchaudio


class MelFeatureExtractor(nn.Module):
    """
    Zamiana audio w znormalizowane mel spektrogramy
    """

    def __init__(
        self,
        sample_rate=16000,
        n_fft=400,
        hop_length=160,
        n_mels=80,
    ):
        super().__init__()

        self.mel_transform = torchaudio.transforms.MelSpectrogram(
            sample_rate=sample_rate,
            n_fft=n_fft,
            hop_length=hop_length,
            n_mels=n_mels,
            power=2.0,
        )

        self.db_transform = torchaudio.transforms.AmplitudeToDB(top_db=80)

    def forward(self, waveform):
        if waveform.ndim == 1:
            waveform = waveform.unsqueeze(0)

        mel = self.mel_transform(waveform)
        mel_db = self.db_transform(mel)

        # Normalize each sample independently.
        mean = mel_db.mean(dim=(1, 2), keepdim=True)
        std = mel_db.std(dim=(1, 2), keepdim=True)

        mel_db = (mel_db - mean) / (std + 1e-6)

        return mel_db


class VisemeClassifier(nn.Module):
    """
    CRNN classifier.

    Training input:
        mel spectrogram

    Training output:
        logits
    """

    def __init__(
        self,
        num_classes,
        n_mels=80,
        hidden_dim=256,
        dropout=0.2,
    ):
        super().__init__()

        self.cnn = nn.Sequential(
            nn.Conv1d(n_mels, 128, kernel_size=3, padding=1),
            nn.BatchNorm1d(128),
            nn.GELU(),
            nn.Dropout(dropout),

            nn.Conv1d(128, 128, kernel_size=3, padding=2, dilation=2),
            nn.BatchNorm1d(128),
            nn.GELU(),
            nn.Dropout(dropout),

            nn.Conv1d(128, hidden_dim, kernel_size=3, padding=4, dilation=4),
            nn.BatchNorm1d(hidden_dim),
            nn.GELU(),
            nn.Dropout(dropout),
        )

        self.lstm = nn.LSTM(
            input_size=hidden_dim,
            hidden_size=hidden_dim // 2,
            num_layers=2,
            batch_first=True,
            bidirectional=True,
        )

        self.classifier = nn.Linear(hidden_dim, num_classes)

    def forward(self, mel):
        x = self.cnn(mel)
        x = x.transpose(1, 2)
        x, _ = self.lstm(x)
        logits = self.classifier(x)
        return logits

    @torch.no_grad()
    def predict_proba(self, mel):
        logits = self.forward(mel)
        return torch.softmax(logits, dim=-1)


class FullVisemeModel(nn.Module):
    """
    Pelny model MelFeatureExtractor + VisemeClassifier
    """
    

    def __init__(self, feature_extractor, classifier):
        super().__init__()
        self.feature_extractor = feature_extractor
        self.classifier = classifier

    def forward(self, audio):
        mel = self.feature_extractor(audio)
        logits = self.classifier(mel)
        probs = torch.softmax(logits, dim=-1)
        return probs