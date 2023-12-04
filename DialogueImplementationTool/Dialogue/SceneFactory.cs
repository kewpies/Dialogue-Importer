﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using DialogueImplementationTool.Dialogue.Responses;
using DialogueImplementationTool.Dialogue.Topics;
using DialogueImplementationTool.UI;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Skyrim.Internals;
using Noggog;
using Condition = Mutagen.Bethesda.Skyrim.Condition;
namespace DialogueImplementationTool.Dialogue; 

public abstract class SceneFactory : DialogueFactory {
    private static readonly Regex SceneLineRegex = new(@"([^:]*):? *([\S\s]+)");

    protected List<AliasSpeaker> AliasSpeakers = new();
    protected List<(FormKey FormKey, List<AliasSpeaker> Speakers)> NameMappedSpeakers = new();
    
    private List<int> _aliasIndices = new();

    protected void AddLines(
        IQuestGetter quest,
        Scene scene,
        List<DialogueTopic> topics) {
        uint currentPhaseIndex = 0;

        scene.LastActionIndex ??= 1;
        
        foreach (var topic in topics) {
            var aliasSpeaker = GetSpeaker(topic.Speaker.Name);

            var sceneTopic = new DialogTopic(Mod.GetNextFormKey(), Release) {
                Priority = 2500,
                Quest = new FormLinkNullable<IQuestGetter>(quest),
                Category = DialogTopic.CategoryEnum.Scene,
                Subtype = DialogTopic.SubtypeEnum.Scene,
                SubtypeName = "SCEN",
                Responses = GetResponsesList(topic),
            };
            Mod.DialogTopics.Add(sceneTopic);

            AddTopic(sceneTopic, aliasSpeaker.AliasIndex);
        }
        
        scene.LastActionIndex = Convert.ToUInt32(topics.Count * _aliasIndices.Count);
        
        void AddTopic(IDialogTopicGetter topic, int speakerAliasID) {
            scene.Phases.Add(new ScenePhase { Name = string.Empty, EditorWidth = 200 });

            //Speaker action
            var speakerAction = new SceneAction {
                Type = SceneAction.TypeEnum.Dialog,
                ActorID = speakerAliasID,
                Emotion = Emotion.Neutral,
                EmotionValue = 0,
                Flags = new SceneAction.Flag(),
                StartPhase = currentPhaseIndex,
                EndPhase = currentPhaseIndex,
                Topic = new FormLinkNullable<IDialogTopicGetter>(topic.FormKey),
                LoopingMin = 1,
                LoopingMax = 10,
                Index = scene.LastActionIndex,
                Name = string.Empty
            };
            
            // We can only be sure who the speaker should look at when there are only two NPCs involved.
            if (AliasSpeakers.Count == 2) {
                speakerAction.HeadtrackActorID = _aliasIndices.Find(aliasIndex => aliasIndex != speakerAliasID);
            }
            
            scene.Actions.Add(speakerAction);
            scene.LastActionIndex += 1;

            //Head track actions
            foreach (var aliasIndex in _aliasIndices.Where(aliasIndex => aliasIndex != speakerAliasID)) {
                scene.Actions.Add(new SceneAction {
                    Type = SceneAction.TypeEnum.Dialog,
                    ActorID = aliasIndex,
                    Emotion = Emotion.Neutral,
                    EmotionValue = 0,
                    Flags = SceneAction.Flag.FaceTarget,
                    StartPhase = currentPhaseIndex,
                    EndPhase = currentPhaseIndex,
                    Topic = new FormLinkNullable<IDialogTopicGetter>(FormKey.Null),
                    LoopingMin = 1,
                    LoopingMax = 10,
                    HeadtrackActorID = speakerAliasID,
                    Index = scene.LastActionIndex,
                    Name = string.Empty
                });
                scene.LastActionIndex += 1;
            }
        
            currentPhaseIndex++;
        }
    }
    
    protected static QuestAlias GetEventAlias(string name, byte[] eventData, FormKey npc1, FormKey npc2) {
        return new QuestAlias {
            Name = name,
            FindMatchingRefFromEvent = new FindMatchingRefFromEvent {
                FromEvent = RecordTypes.ADIA,
                EventData = eventData
            },
            Conditions = new ExtendedList<Condition> {
                GetFormKeyCondition(Condition.Function.GetIsID, npc1, 1, true),
                GetFormKeyCondition(Condition.Function.GetIsID, npc2, 1, true)
            },
            Flags = QuestAlias.Flag.AllowReserved,
            VoiceTypes = new FormLinkNullable<IAliasVoiceTypeGetter>(FormKey.Null)
        };
    }
    
    protected static QuestAlias GetAlias(AliasSpeaker aliasSpeaker) {
        return new QuestAlias {
            Name = aliasSpeaker.Name,
            UniqueActor = new FormLinkNullable<INpcGetter>(aliasSpeaker.FormKey),
            VoiceTypes = new FormLinkNullable<IAliasVoiceTypeGetter>(FormKey.Null)
        };
    }

    protected Scene AddScene(string editorID, FormKey quest) {
        _aliasIndices = NameMappedSpeakers.SelectMany(x => x.Speakers.Select(speaker => speaker.AliasIndex)).ToList();
        
        return new Scene(Mod.GetNextFormKey(), Release) {
            EditorID = editorID,
            Actions = new ExtendedList<SceneAction>(),
            Actors = _aliasIndices.Select(id => new SceneActor {
                BehaviorFlags = SceneActor.BehaviorFlag.DeathEnd | SceneActor.BehaviorFlag.CombatEnd | SceneActor.BehaviorFlag.DialoguePause,
                Flags = new SceneActor.Flag(),
                ID = Convert.ToUInt32(id)
            }).ToExtendedList(),
            Quest = new FormLinkNullable<IQuestGetter>(quest),
        };
    }
    
    public override void PreProcess(List<DialogueTopic> topics) {
        AliasSpeakers = GetSpeakers(topics);

        NameMappedSpeakers = AliasSpeakers
            .GroupBy(x => x.FormKey)
            .Select(x => (x.Key, x.ToList()))
            .ToList();

        //break up topics for every new speaker
        var separatedTopics = ParseLines(topics);

        topics.Clear();
        topics.AddRange(separatedTopics);

        PreProcessSpeakers();
    }
    
    public abstract void PreProcessSpeakers();

    private static List<AliasSpeaker> GetSpeakers(IEnumerable<DialogueTopic> topics) {
        //Get speaker strings
        var speakerNames = topics
            .SelectMany(topic => topic.Responses, (_, response) => SceneLineRegex.Match(response.Response))
            .Where(match => match.Success)
            .Select(match => match.Groups[1].Value)
            .ToHashSet();

        //Map speaker form keys
        var speakers = new ObservableCollection<AliasSpeaker>(speakerNames.Select(s => new AliasSpeaker(s)).ToList());
        new SceneSpeakerWindow(speakers).ShowDialog();
        
        while (speakers.Any(s => s.FormKey == FormKey.Null)) {
            MessageBox.Show("You must assign every speaker of the scene to an npc");
            new SceneSpeakerWindow(speakers).ShowDialog();
        }

        return speakers
            .ToList();
    }
    
    private List<DialogueTopic> ParseLines(List<DialogueTopic> topics) {
        var separatedTopics = new List<DialogueTopic>();
        var currentSpeaker = string.Empty;
        var currentLines = new List<DialogueResponse>();

        void AddCurrentTopic() {
            if (currentLines.Any()) {
                var dialogueTopic = new DialogueTopic();
                dialogueTopic.Responses.AddRange(currentLines);
                dialogueTopic.Speaker = GetSpeaker(currentSpeaker);

                separatedTopics.Add(dialogueTopic);
            }
            currentLines.Clear();
        }

        foreach (var topic in topics) {
            foreach (var response in topic.Responses) {
                var match = SceneLineRegex.Match(response.Response);
                if (!match.Success) continue;

                var speaker = match.Groups[1].Value;
                if (currentSpeaker != speaker) {
                    AddCurrentTopic();
                    currentSpeaker = speaker;
                }

                currentLines.Add(response with { Response = match.Groups[2].Value });
            }
        }

        if (currentLines.Any()) AddCurrentTopic();
        return separatedTopics;
    }
    
    public override void PostProcess() {}

    public AliasSpeaker GetSpeaker(string name) {
        name = ISpeaker.GetSpeakerName(name);
        foreach (var (formKey, speakers) in NameMappedSpeakers) {
            foreach (var speaker in speakers) {
                if (speaker.Name == name) {
                    return speaker;
                }
            }
        }

        throw new Exception("Didn't find speaker");
    }
}