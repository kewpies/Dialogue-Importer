﻿using System;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
namespace DialogueImplementationTool.Services;

public enum LoadState {
    NotLoaded,
    InProgress,
    Loaded,
}

public sealed class PythonEmotionClassifierProvider : ReactiveObject, IEmotionClassifierProvider {
    private readonly Func<string, PythonEmotionClassifier> _emotionClassifierFactory;
    private PythonEmotionClassifier? _pythonEmotionClassifier;

    public IEmotionClassifier EmotionClassifier =>
        (IEmotionClassifier?) _pythonEmotionClassifier ?? new NullEmotionClassifier();

    [Reactive] public LoadState PythonState { get; set; }
    [Reactive] public string PythonDllPath { get; set; } = string.Empty;

    public PythonEmotionClassifierProvider(
        Func<string, PythonEmotionClassifier> emotionClassifierFactory) {
        _emotionClassifierFactory = emotionClassifierFactory;
        TrySetPythonFromEnv();
    }

    private void TrySetPythonFromEnv() {
        var paths = Environment.GetEnvironmentVariable("PATH");
        if (paths is null) return;

        foreach (var path in paths.Split(';')) {
            if (!path.Contains("python", StringComparison.OrdinalIgnoreCase)) continue;
            if (!Directory.Exists(path)) continue;

            var filePath = Directory
                .EnumerateFiles(path, "python3*.dll", SearchOption.TopDirectoryOnly)
                // Don't use python3.dll
                .Where(x => !x.Contains("python3.dll"))
                .FirstOrDefault(File.Exists);
            if (filePath is null) continue;

            Observable.Start(() => RefreshPython(filePath), RxApp.MainThreadScheduler).Subscribe();
            break;
        }
    }

    public void RefreshPython(string dllPath) {
        PythonDllPath = dllPath;
        if (_pythonEmotionClassifier?.PythonDllPath == PythonDllPath) return;

        try {
            PythonState = LoadState.InProgress;
            _pythonEmotionClassifier?.Dispose();
            _pythonEmotionClassifier = _emotionClassifierFactory(PythonDllPath);
            PythonState = LoadState.Loaded;
        } catch (Exception e) {
            Console.WriteLine($"Failed to load emotion classifier: {e.Message}");
            PythonState = LoadState.NotLoaded;
        }
    }
}
