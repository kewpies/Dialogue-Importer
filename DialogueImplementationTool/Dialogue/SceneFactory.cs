﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using DialogueImplementationTool.UI;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Skyrim;
using Noggog;
using Condition = Mutagen.Bethesda.Skyrim.Condition;
namespace DialogueImplementationTool.Dialogue; 

public abstract class SceneFactory : DialogueFactory {
    private static readonly Regex SceneLineRegex = new(@"([\S\s]*): *([\S\s]+)");
    protected int SceneCount = 1; 
    
    private uint _currentPhaseIndex;
    private List<int> _aliasIndices = new();

    protected static List<Speaker> GetSpeakers(IEnumerable<DialogueTopic> topics) {
        //Get speaker strings
        var speakerNames = topics
            .SelectMany(topic => topic.Responses, (_, response) => SceneLineRegex.Match(response))
            .Where(match => match.Success)
            .Select(match => match.Groups[1].Value)
            .ToHashSet();

        //Map speaker form keys
        var speakers = new ObservableCollection<Speaker>(speakerNames.Select(s => new Speaker(s)).ToList());
        new SceneSpeakerWindow(speakers).ShowDialog();

        return speakers.ToList();
    }
    
    protected static Dictionary<string,(FormKey FormKey, string? EditorID, int AliasIndex)> GetNameMappedSpeakers(IEnumerable<Speaker> speakers) {
        return speakers.ToDictionary(
            speaker => speaker.Name,
            speaker => (speaker.FormKey, speaker.EditorID, speaker.AliasIndex)
        );
    }

    protected void AddLines(IQuestGetter quest, Scene scene, List<(string Speaker, List<string> Responses)> lines, Dictionary<string,(FormKey FormKey, string? EditorID, int AliasIndex)> speakers) {
        //Merge lines
        var i = 0;
        var currentIndex = 0;
        var currentSpeaker = FormKey.Null;
        while (i < lines.Count) {
            if (speakers[lines[i].Speaker].FormKey == currentSpeaker) {
                lines[currentIndex].Responses.AddRange(lines[i].Responses);
                lines.RemoveAt(i);
            } else {
                currentIndex = i;
                currentSpeaker = speakers[lines[i].Speaker].FormKey;
                i++;
            }
        }
        
        //Add lines
        foreach (var (speaker, responses) in lines) {
            var (formKey, _, index) = speakers[speaker];

            var sceneTopic = new DialogTopic(Mod.GetNextFormKey(), Release) {
                Priority = 2500,
                Quest = new FormLinkNullable<IQuestGetter>(quest),
                Category = DialogTopic.CategoryEnum.Scene,
                Subtype = DialogTopic.SubtypeEnum.Scene,
                SubtypeName = "SCEN",
                Responses = GetResponses(formKey, responses)
            };
            Mod.DialogTopics.Add(sceneTopic);

            AddTopic(scene, sceneTopic, index);
        }
        
        scene.LastActionIndex = 0;
    }

    protected static List<(string, List<string>)> ParseLines(List<string> lines) {
        var output = new List<(string, List<string>)>();
        var currentSpeaker = string.Empty;
        var currentLines = new List<string>();
        foreach (var response in lines) {
            var match = SceneLineRegex.Match(response);
            if (!match.Success) continue;
            
            var speaker = match.Groups[1].Value;
            if (currentSpeaker == speaker) {
                currentLines.Add(match.Groups[2].Value);
            } else {
                if (currentLines.Count > 0) {
                    output.Add((currentSpeaker, new List<string>(currentLines)));
                }
                currentLines.Clear();
                
                currentSpeaker = speaker;
                currentLines.Add(match.Groups[2].Value);
            }
        }

        return output;
    }
    
    protected static QuestAlias GetEventAlias(string name, FormKey npc1, FormKey npc2) {
        return new QuestAlias {
            Name = name,
            FindMatchingRefFromEvent = new FindMatchingRefFromEvent {
                FromEvent = "ADIA",
                EventData = new MemorySlice<byte>(new byte[] { 0x52, 0x31, 0x0, 0x0 })
            },
            Conditions = new ExtendedList<Condition> {
                GetIsIDCondition(npc1, true),
                GetIsIDCondition(npc2, true)
            },
            Flags = QuestAlias.Flag.AllowReserved,
            VoiceTypes = new FormLinkNullable<IAliasVoiceTypeGetter>(FormKey.Null)
        };
    }
    
    protected static QuestAlias GetAlias(Speaker speaker) {
        return new QuestAlias {
            Name = speaker.Name,
            UniqueActor = new FormLinkNullable<INpcGetter>(speaker.FormKey),
            Flags = QuestAlias.Flag.AllowReserved | QuestAlias.Flag.AllowReuseInQuest,
            VoiceTypes = new FormLinkNullable<IAliasVoiceTypeGetter>(FormKey.Null)
        };
    }

    protected Scene AddScene(string editorID, FormKey quest, List<int> aliasesIDs) {
        SceneCount++;
        _aliasIndices = aliasesIDs;
        
        return new Scene(Mod.GetNextFormKey(), Release) {
            EditorID = editorID,
            Actions = new ExtendedList<SceneAction>(),
            Actors = aliasesIDs.Select(id => new SceneActor {
                BehaviorFlags = SceneActor.BehaviorFlag.DeathEnd | SceneActor.BehaviorFlag.CombatEnd | SceneActor.BehaviorFlag.DialoguePause,
                Flags = new SceneActor.Flag(),
                ID = Convert.ToUInt32(id)
            }).ToExtendedList(),
            Flags = Scene.Flag.BeginOnQuestStart | Scene.Flag.StopOnQuestEnd | Scene.Flag.Interruptable,
            Quest = new FormLinkNullable<IQuestGetter>(quest)
        };
    }

    private void AddTopic(IScene scene, DialogTopic topic, int speakerAliasID) {
        scene.Phases.Add(new ScenePhase {
            Name = string.Empty,
            EditorWidth = 200
        });

        //Speaker action
        scene.Actions.Add(new SceneAction {
            Type = SceneAction.TypeEnum.Dialog,
            ActorID = speakerAliasID,
            Emotion = Emotion.Neutral,
            EmotionValue = 0,
            Flags = new SceneAction.Flag(),
            StartPhase = _currentPhaseIndex,
            EndPhase = _currentPhaseIndex,
            Topic = new FormLinkNullable<IDialogTopicGetter>(topic.FormKey),
            LoopingMin = 1,
            LoopingMax = 10,
            Index = scene.LastActionIndex,
            Name = string.Empty
        });
        scene.LastActionIndex += 1;

        //Head track actions
        foreach (var aliasIndex in _aliasIndices.Where(aliasIndex => aliasIndex != speakerAliasID)) {
            scene.Actions.Add(new SceneAction {
                Type = SceneAction.TypeEnum.Dialog,
                ActorID = aliasIndex,
                Emotion = Emotion.Neutral,
                EmotionValue = 0,
                Flags = SceneAction.Flag.FaceTarget,
                StartPhase = _currentPhaseIndex,
                EndPhase = _currentPhaseIndex,
                Topic = new FormLinkNullable<IDialogTopicGetter>(FormKey.Null),
                LoopingMin = 1,
                LoopingMax = 10,
                HeadtrackActorID = speakerAliasID,
                Index = scene.LastActionIndex,
                Name = string.Empty
            });
            scene.LastActionIndex += 1;
        }
        
        _currentPhaseIndex++;
    }
    
    public override void PostProcess() {}
}