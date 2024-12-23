﻿using DialogueImplementationTool.Extension;
using Mutagen.Bethesda.FormKeys.SkyrimSE;
using Mutagen.Bethesda.Skyrim;
using Noggog;
namespace DialogueImplementationTool.Dialogue;

public sealed class VoiceTypeGenericDialogueQuestFactory(IDialogueContext context, VoiceType voiceType) : IGenericDialogueQuestFactory {
    public Quest Create() {
        var questEditorId = context.Prefix + "GenericDialogue" + voiceType.EditorID?.TrimStart(context.Prefix);

        return context.GetOrAddQuest(
            questEditorId,
            () => new Quest(context.GetNextFormKey(), context.Release) {
                EditorID = questEditorId,
                Priority = 30,
                Filter = context.Quest.Filter,
                DialogConditions = [
                    new IsCommandedActorConditionData().ToConditionFloat(comparisonValue: 0, or: true),
                    new HasKeywordConditionData {
                        Keyword = { Link = { FormKey = Update.Keyword.CommandedVoiceExcluded.FormKey } }
                    }.ToConditionFloat(),
                    new GetIsVoiceTypeConditionData {
                        VoiceTypeOrList = { Link = { FormKey = voiceType.FormKey } },
                    }.ToConditionFloat(),
                ],
                Name = $"Generic Dialogue for {voiceType.EditorID}",
                Flags = Quest.Flag.StartGameEnabled,
            });
    }
}
