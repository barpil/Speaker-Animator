using SpeakerAnimator.Model.NN;

namespace SpeakerAnimator.Model.utils;

using System;
using System.Collections.Generic;
using System.IO;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

public static class AudioHelper
{
    
    public static float[] LoadFileAs16kMono(string path)
    {
        using var reader = new AudioFileReader(path);
        var mono = reader.ToMono();
        var resampled = new WdlResamplingSampleProvider(mono, VisemeOnnxPredictor.SampleRate);

        var samples = new List<float>(capacity: 16000 * 30);
        var buffer = new float[16000];
        int read;

        while ((read = resampled.Read(buffer)) > 0)
        {
            for (int i = 0; i < read; i++)
                samples.Add(buffer[i]);
        }

        return samples.ToArray();
    }

    public static float[] Pcm16BytesToFloats(byte[] pcm)
    {
        var floats = new float[pcm.Length / 2];
        for (int i = 0; i < floats.Length; i++)
        {
            short sample = BitConverter.ToInt16(pcm, i * 2);
            floats[i] = sample / 32768f;
        }

        return floats;
    }

    public static byte[] FloatsToPcm16Bytes(float[] samples)
    {
        var bytes = new byte[samples.Length * 2];
        for (int i = 0; i < samples.Length; i++)
        {
            float clamped = Math.Clamp(samples[i], -1f, 1f);
            short sample = (short)(clamped * 32767f);
            BitConverter.GetBytes(sample).CopyTo(bytes, i * 2);
        }

        return bytes;
    }
}