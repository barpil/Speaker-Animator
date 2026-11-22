using System.Diagnostics;

namespace SpeakerAnimator.Model.NN;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

public sealed class VisemeFramePrediction
{
    public int FrameIndex { get; init; }
    public float TimeSeconds { get; init; }
    public string Viseme { get; init; } = "";
    public float Confidence { get; init; }
    public float[] Probabilities { get; init; } = Array.Empty<float>();
}

public sealed class VisemePredictionResult
{
    public IReadOnlyList<string> Labels { get; init; } = Array.Empty<string>();
    public IReadOnlyList<VisemeFramePrediction> Frames { get; init; } = Array.Empty<VisemeFramePrediction>();
}


public sealed class VisemeOnnxPredictor : IDisposable
{
    public static int SampleRate { get; private set; }
    public int NMels { get; }
    private const float HopSeconds = 160f / 16000;
    private const float FirstFrameCenter = 200f / 16000;

    private readonly InferenceSession _melSession;
    private readonly InferenceSession _classSession;
    public IReadOnlyList<string> Labels { get; }

    public VisemeOnnxPredictor(string basePath, bool preferGpu = true)
    {
        var melPath = Path.Combine(basePath, "mel_extractor.onnx");
        var classPath = Path.Combine(basePath, "viseme_classifier.onnx");
        var labelsPath = Path.Combine(basePath, "viseme_labels.json");
        var configPath = Path.Combine(basePath, "model_config.json");

        if (!File.Exists(melPath))
            throw new FileNotFoundException($"Mel extractor not found: {melPath}");
        if (!File.Exists(classPath))
            throw new FileNotFoundException($"Classifier not found: {classPath}");
        if (!File.Exists(labelsPath))
            throw new FileNotFoundException($"Labels file not found: {labelsPath}");

        Labels = JsonSerializer.Deserialize<List<string>>(
            File.ReadAllText(labelsPath)) ?? new List<string>();
        
        if (File.Exists(configPath))
        {
            var config = JsonSerializer.Deserialize<Dictionary<string, int>>(
                File.ReadAllText(configPath));
            SampleRate = config?.GetValueOrDefault("sample_rate", 16000) ?? 16000;
            NMels = config?.GetValueOrDefault("n_mels", 80) ?? 80;
        }
        else
        {
            SampleRate = 16000;
            NMels = 80;
        }
        
        var melOptions = new SessionOptions();
        melOptions.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
        _melSession = new InferenceSession(melPath, melOptions);
        Console.WriteLine("Mel extractor: using CPU");
        
        var classOptions = new SessionOptions();
        classOptions.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;

        bool gpuEnabled = false;
        if (preferGpu)
        {
            try
            {
                classOptions.AppendExecutionProvider_DML(0);
                _classSession = new InferenceSession(classPath, classOptions);
                Console.WriteLine("Classifier: using DirectML (GPU)");
                gpuEnabled = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DirectML failed: {ex.Message}");
            }

            if (!gpuEnabled)
            {
                try
                {
                    classOptions = new SessionOptions();
                    classOptions.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
                    classOptions.AppendExecutionProvider_CUDA(0);
                    _classSession = new InferenceSession(classPath, classOptions);
                    Console.WriteLine("Classifier: using CUDA (GPU)");
                    gpuEnabled = true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"CUDA failed: {ex.Message}");
                }
            }
        }

        if (!gpuEnabled)
        {
            classOptions = new SessionOptions();
            classOptions.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
            _classSession = new InferenceSession(classPath, classOptions);
            Console.WriteLine("Classifier: using CPU");
        }
    }

    public VisemePredictionResult Predict(float[] audio)
    {
        var stopwatch = Stopwatch.StartNew();
    
        if (audio == null || audio.Length == 0)
            throw new ArgumentException("Audio is empty.");
        
        var melStart = stopwatch.ElapsedMilliseconds;
        var audioTensor = new DenseTensor<float>(audio, new[] { 1, audio.Length });
        var melInputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("audio", audioTensor)
        };

        Tensor<float> mel;
        using (var melResults = _melSession.Run(melInputs))
        {
            mel = melResults.First().AsTensor<float>().Clone();
        }
        
        var classStart = stopwatch.ElapsedMilliseconds;
        var classInputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("mel", mel)
        };

        using var classResults = _classSession.Run(classInputs);
        var logits = classResults.First().AsTensor<float>();
        
        int frameCount = logits.Dimensions[1];
        int classCount = logits.Dimensions[2];

        var frames = new List<VisemeFramePrediction>(frameCount);

        for (int f = 0; f < frameCount; f++)
        {
            var frameLogits = new float[classCount];
            float maxLogit = float.NegativeInfinity;
            for (int c = 0; c < classCount; c++)
            {
                frameLogits[c] = logits[0, f, c];
                if (frameLogits[c] > maxLogit) maxLogit = frameLogits[c];
            }

            float sumExp = 0;
            for (int c = 0; c < classCount; c++)
            {
                frameLogits[c] = (float)Math.Exp(frameLogits[c] - maxLogit);
                sumExp += frameLogits[c];
            }

            var probs = new float[classCount];
            int best = 0;
            for (int c = 0; c < classCount; c++)
            {
                probs[c] = frameLogits[c] / sumExp;
                if (probs[c] > probs[best]) best = c;
            }

            frames.Add(new VisemeFramePrediction
            {
                FrameIndex = f,
                TimeSeconds = f * HopSeconds + FirstFrameCenter,
                Viseme = Labels[best],
                Confidence = probs[best],
                Probabilities = probs
            });
        }

        return new VisemePredictionResult
        {
            Labels = Labels,
            Frames = frames
        };
    }

    public void Dispose()
    {
        _melSession.Dispose();
        _classSession.Dispose();
    }
}