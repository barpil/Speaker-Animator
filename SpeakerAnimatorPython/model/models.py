import torch
import torch.nn as nn
import torchaudio


class MelFeatureExtractor(nn.Module):
    def __init__(
        self,
        sample_rate=16000,
        n_fft=400,
        hop_length=160,
        n_mels=80,
        use_flatness=False,
        flatness_eps=1e-8,
        mel_mean=0.0,
        mel_std=1.0,
        flat_mean=0.0,
        flat_std=1.0,
    ):
        super().__init__()

        self.frontend = MelWithFlatness(
            sample_rate, n_fft, hop_length, n_mels, use_flatness, flatness_eps
        )
        self.n_mels = n_mels
        self.use_flatness = bool(use_flatness)

        self.register_buffer("mel_mean", torch.tensor(float(mel_mean)))
        self.register_buffer("mel_std", torch.tensor(float(mel_std)))
        self.register_buffer("flat_mean", torch.tensor(float(flat_mean)))
        self.register_buffer("flat_std", torch.tensor(float(flat_std)))

    def forward(self, waveform):
        feats = self.frontend(waveform)                # (B, C, T)
        n = self.n_mels

        mel = (feats[:, :n] - self.mel_mean) / (self.mel_std + 1e-6)
        if not self.use_flatness:
            return mel

        flat = (feats[:, n:] - self.flat_mean) / (self.flat_std + 1e-6)
        return torch.cat([mel, flat], dim=1)



class ChannelLayerNorm(nn.Module):

    def __init__(self, channels, eps=1e-5):
        super().__init__()
        self.eps = eps
        self.weight = nn.Parameter(torch.ones(channels))
        self.bias = nn.Parameter(torch.zeros(channels))

    def forward(self, x):
        mean = x.mean(dim=1, keepdim=True)
        var = x.var(dim=1, keepdim=True, unbiased=False)
        x = (x - mean) / torch.sqrt(var + self.eps)
        return x * self.weight.unsqueeze(-1) + self.bias.unsqueeze(-1)


def _make_norm(kind, channels, groups=8):
    kind = (kind or "channel_ln").lower()

    if kind == "channel_ln":
        return ChannelLayerNorm(channels)

    if kind == "batch":
        return nn.BatchNorm1d(channels)

    if kind == "group":
        g = max(1, min(groups, channels))
        while channels % g != 0:
            g -= 1
        return nn.GroupNorm(g, channels)

    if kind == "none":
        return nn.Identity()

    raise ValueError(f"Unknown norm_type: {kind}")


#
# TDS block  (Hannun et al., "Sequence-to-Sequence Speech Recognition
#             with Time-Depth Separable Convolutions", 2019)
#

class TDSBlock(nn.Module):


    def __init__(self, channels, kernel_size=17, dropout=0.2,
                 norm="channel_ln", norm_groups=8, expansion=2):
        super().__init__()

        if kernel_size % 2 == 0:
            kernel_size += 1

        mid = channels * expansion
        pad = kernel_size // 2

        self.depthwise = nn.Conv1d(
            channels, mid, kernel_size, padding=pad, groups=channels
        )
        self.pointwise = nn.Conv1d(mid, channels, 1)
        self.norm_conv = _make_norm(norm, channels, norm_groups)

        self.fc1 = nn.Conv1d(channels, mid, 1)
        self.fc2 = nn.Conv1d(mid, channels, 1)
        self.norm_fc = _make_norm(norm, channels, norm_groups)

        self.act = nn.ReLU()
        self.drop = nn.Dropout(dropout)

    def forward(self, x, mask=None):
        h = self.depthwise(x)
        h = self.act(h)
        h = self.pointwise(h)
        h = self.drop(h)
        x = self.norm_conv(x + h)
        if mask is not None:
            x = x * mask

        h = self.fc1(x)
        h = self.act(h)
        h = self.drop(h)
        h = self.fc2(h)
        h = self.drop(h)
        x = self.norm_fc(x + h)
        if mask is not None:
            x = x * mask

        return x



class VisemeClassifier(nn.Module):

    def __init__(
        self,
        num_classes,
        n_mels=80,
        hidden_dim=256,
        dropout=0.2,
        num_blocks=4,
        kernel_size=17,
        norm="channel_ln",
        norm_groups=8,
        in_channels=None,
        expansion=2,
        **_ignored,
    ):
        super().__init__()

        ch = hidden_dim
        cin = int(n_mels if in_channels is None else in_channels)

        self.stem = nn.Sequential(
            nn.Conv1d(cin, ch, kernel_size=3, padding=1),
            _make_norm(norm, ch, norm_groups),
            nn.ReLU(),
            nn.Dropout(dropout),
        )

        self.blocks = nn.ModuleList([
            TDSBlock(ch, kernel_size, dropout, norm, norm_groups, expansion)
            for _ in range(num_blocks)
        ])

        self.head = nn.Conv1d(ch, num_classes, 1)

        self.receptive_field_frames = 3 + num_blocks * (kernel_size - 1)

    def forward(self, mel, lengths=None):
        x = self.stem(mel)

        mask = None
        if lengths is not None:
            t = torch.arange(x.size(2), device=x.device).unsqueeze(0)
            mask = (t < lengths.to(x.device).unsqueeze(1)).unsqueeze(1).to(x.dtype)
            x = x * mask

        for block in self.blocks:
            x = block(x, mask)

        logits = self.head(x)
        return logits.transpose(1, 2)

    @torch.no_grad()
    def predict_proba(self, mel, lengths=None):
        return torch.softmax(self.forward(mel, lengths), dim=-1)



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
   
   
    
def build_viseme_classifier(config, num_classes):
    use_flat = bool(getattr(config, "use_flatness", False))
    return VisemeClassifier(
        num_classes=num_classes,
        n_mels=config.n_mels,
        in_channels=config.n_mels + (1 if use_flat else 0),
        hidden_dim=config.hidden_dim,
        dropout=config.dropout,
        num_blocks=int(getattr(config, "tds_blocks", 4)),
        kernel_size=int(getattr(config, "tds_kernel_size", 17)),
        norm=str(getattr(config, "norm_type", "batch")),
        norm_groups=int(getattr(config, "norm_groups", 8)),
        expansion=int(getattr(config, "tds_expansion", 2)),
    )
    
class MelWithFlatness(nn.Module):

    def __init__(self, sample_rate=16000, n_fft=400, hop_length=160,
                 n_mels=80, use_flatness=True, flatness_eps=1e-8):
        super().__init__()

        self.spectrogram = torchaudio.transforms.Spectrogram(
            n_fft=n_fft, hop_length=hop_length, power=2.0,
        )
        self.mel_scale = torchaudio.transforms.MelScale(
            n_mels=n_mels, sample_rate=sample_rate, n_stft=n_fft // 2 + 1,
        )
        self.db = torchaudio.transforms.AmplitudeToDB(stype="power", top_db=80)

        self.n_mels = n_mels
        self.use_flatness = bool(use_flatness)
        self.eps = float(flatness_eps)

    def forward(self, waveform):
        if waveform.ndim == 1:
            waveform = waveform.unsqueeze(0)

        spec = self.spectrogram(waveform)
        mel_db = self.db(self.mel_scale(spec))

        if not self.use_flatness:
            return mel_db

        log_p = torch.log(spec + self.eps)
        geo = log_p.mean(dim=1, keepdim=True).exp()
        ari = spec.mean(dim=1, keepdim=True)
        sf = (geo / (ari + self.eps)).clamp(self.eps, 1.0)
        flat_db = 10.0 * torch.log10(sf)

        return torch.cat([mel_db, flat_db], dim=1)