﻿using System;
using System.Collections.Generic;
using DialogueImplementationTool.Dialogue.Model;
using DialogueImplementationTool.Dialogue.Speaker;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Skyrim;
namespace DialogueImplementationTool.Dialogue;

public sealed class SkyrimDialogueContext(
    ILinkCache<ISkyrimMod, ISkyrimModGetter> linkCache,
    ISkyrimMod mod,
    IQuest quest,
    ISpeakerSelection speakerSelection)
    : IDialogueContext {
    public SkyrimRelease Release => SkyrimRelease.SkyrimSE;
    public ILinkCache LinkCache { get; } = linkCache;
    public IQuest Quest { get; } = quest;
    public IMod Mod { get; } = mod;

    public FormKey GetNextFormKey() {
        return mod.GetNextFormKey();
    }

    public void AddScene(Scene scene) {
        if (!mod.Scenes.ContainsKey(scene.FormKey)) mod.Scenes.Add(scene);
    }

    public void AddQuest(Quest quest) {
        if (!mod.Quests.ContainsKey(quest.FormKey)) mod.Quests.Add(quest);
    }

    public void AddDialogBranch(DialogBranch branch) {
        if (!mod.DialogBranches.ContainsKey(branch.FormKey)) mod.DialogBranches.Add(branch);
    }

    public void AddDialogTopic(DialogTopic topic) {
        if (!mod.DialogTopics.ContainsKey(topic.FormKey)) mod.DialogTopics.Add(topic);
    }

    public DialogTopic? GetTopic(string editorId) {
        if (!linkCache.TryResolveIdentifier<IDialogTopicGetter>(editorId, out var formKey)) return null;

        return GetTopic(formKey);
    }

    public DialogTopic GetTopic(FormKey formKey) {
        var topic = linkCache.ResolveContext<DialogTopic, IDialogTopicGetter>(formKey);
        return topic.GetOrAddAsOverride(mod);
    }

    public IDialogTopicGetter? GetTopic(DialogueTopic topic) {
        foreach (var implementedTopic in linkCache.PriorityOrder.WinningOverrides<IDialogTopicGetter>()) {
            if (implementedTopic.Quest.FormKey != Quest.FormKey) continue;

            if (Matches(implementedTopic)) {
                return implementedTopic;
            }
        }

        return null;

        bool Matches(IDialogTopicGetter implementedTopic) {
            var playerText = topic.GetPlayerText();
            if (playerText != string.Empty && playerText != implementedTopic.Name?.String) return false;

            if (topic.TopicInfos.Count != implementedTopic.Responses.Count) return false;
            for (var topicInfoIndex = 0; topicInfoIndex < topic.TopicInfos.Count; topicInfoIndex++) {
                var topicInfo = topic.TopicInfos[topicInfoIndex];
                var implementedTopicInfo = implementedTopic.Responses[topicInfoIndex];

                // Check prompt
                if (playerText == string.Empty && topicInfo.Prompt != implementedTopicInfo.Prompt?.String) return false;

                // Check shared info
                if (topicInfo.SharedInfo is null != implementedTopicInfo.ResponseData.IsNull) return false;

                // Check flags
                if (topicInfo.InvisibleContinue
                    != ((implementedTopicInfo.Flags?.Flags & DialogResponses.Flag.InvisibleContinue) != 0))
                    return false;
                if (topicInfo.Goodbye != ((implementedTopicInfo.Flags?.Flags & DialogResponses.Flag.Goodbye) != 0))
                    return false;
                if (topicInfo.Random != ((implementedTopicInfo.Flags?.Flags & DialogResponses.Flag.Random) != 0))
                    return false;
                if (topicInfo.SayOnce != ((implementedTopicInfo.Flags?.Flags & DialogResponses.Flag.SayOnce) != 0))
                    return false;

                // Check responses
                if (topicInfo.Responses.Count != implementedTopicInfo.Responses.Count) return false;
                for (var responseIndex = 0; responseIndex < topicInfo.Responses.Count; responseIndex++) {
                    var response = topicInfo.Responses[responseIndex];
                    var implementedResponse = implementedTopicInfo.Responses[responseIndex];

                    if (!string.Equals(response.FullResponse, implementedResponse.Text.String, StringComparison.Ordinal))
                        return false;
                }
            }

            return true;
        }
    }

    public IReadOnlyList<AliasSpeaker> GetAliasSpeakers(IEnumerable<string> speakerNames) {
        return speakerSelection.GetAliasSpeakers(speakerNames);
    }
}
