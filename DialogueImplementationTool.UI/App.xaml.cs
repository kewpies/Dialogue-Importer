﻿using System.IO;
using System.Windows;
using Autofac;
using DialogueImplementationTool.Dialogue.Topics;
using DialogueImplementationTool.Parser;
using DialogueImplementationTool.Services;
using DialogueImplementationTool.UI.Models;
using DialogueImplementationTool.UI.Services;
using DialogueImplementationTool.UI.ViewModels;
using DialogueImplementationTool.UI.Views;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments.DI;
using Mutagen.Bethesda.Plugins.Order.DI;
namespace DialogueImplementationTool.UI;

public partial class App {
    public App() {
        AppDomain.CurrentDomain.UnhandledException += CurrentDomainOnFirstChanceException;
    }

    protected override void OnStartup(StartupEventArgs e) {
        base.OnStartup(e);

        var builder = new ContainerBuilder();

        builder.RegisterType<PythonEmotionClassifier>()
            .AsSelf()
            .As<IEmotionClassifier>()
            .SingleInstance();

        builder.RegisterType<EmotionChecker>()
            .SingleInstance();

        builder.RegisterType<MainWindowVM>()
            .SingleInstance();

        builder.RegisterType<DialogueVM>()
            .SingleInstance();

        builder.RegisterType<DialogueProcessor>()
            .SingleInstance();

        builder.RegisterType<OutputPathProvider>()
            .SingleInstance();

        builder.RegisterType<SpeakerFavoritesSelection>()
            .As<ISpeakerFavoritesSelection>()
            .SingleInstance();

        builder.RegisterType<OpenDocumentTextParser>()
            .SingleInstance();

        builder.RegisterType<DocXDocumentParser>()
            .SingleInstance();

        builder.RegisterType<MainWindow>()
            .SingleInstance();

        var container = builder.Build();

        using var scope = container.BeginLifetimeScope();
        var pathProvider = new PluginListingsPathProvider(new GameReleaseInjection(GameRelease.SkyrimSE));
        if (!File.Exists(pathProvider.Path)) MessageBox.Show($"Make sure {pathProvider.Path} exists.");

        var window = scope.Resolve<MainWindow>();
        window.Show();
    }

    private void CurrentDomainOnFirstChanceException(object sender, UnhandledExceptionEventArgs e) {
        var exception = (Exception) e.ExceptionObject;

        using var log = new StreamWriter(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CrashLog.txt"), false);
        log.WriteLine(exception);
    }
}