﻿using System.Collections.Generic;
using DialogueImplementationTool.Dialogue.Speaker;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Skyrim;
namespace DialogueImplementationTool.Dialogue;

public interface IDialogueContext {
    SkyrimRelease Release { get; }
    ILinkCache LinkCache { get; }
    IQuest Quest { get; }
    IMod Mod { get; }
    FormKey GetNextFormKey();
    void AddScene(Scene scene);
    void AddQuest(Quest quest);
    void AddDialogBranch(DialogBranch branch);
    void AddDialogTopic(DialogTopic topic);
    DialogTopic? GetTopic(string editorId);
    DialogTopic GetTopic(FormKey formKey);
    IReadOnlyList<AliasSpeaker> GetAliasSpeakers(IEnumerable<string> speakerNames);
}
