﻿using System;
using System.Collections.Generic;
using DialogueImplementationTool.Parser;
using Mutagen.Bethesda.Skyrim;
namespace DialogueImplementationTool.Dialogue.Responses;

public record DialogueResponse {
	private static readonly IEnumerable<IDialogueResponsePreProcessor> PreProcessors = new List<IDialogueResponsePreProcessor> {
		new InvalidStringFixer(),
		new ScriptNotesParser(),
	};

	private static readonly IEnumerable<IDialogueResponsePostProcessor> PostProcessors = new List<IDialogueResponsePostProcessor> {
		new BackToDialogueRemover(),
		new BracesRemover(),
		new Trimmer(),
	};

	public string Response { get; set; } = string.Empty;
	public string ScriptNote { get; set; } = string.Empty;
	public Emotion Emotion { get; set; } = Emotion.Neutral;
	public uint EmotionValue { get; set; } = 50;

	public static DialogueResponse Build(IEnumerable<FormattedText> textSnippets) {
		//Apply pre processors
		var combinedResponses = new List<DialogueResponse>();
		foreach (var formattedText in textSnippets) {
			var dialogueResponse = new DialogueResponse { Response = formattedText.Text, };
			foreach (var preProcessor in PreProcessors) {
				dialogueResponse = preProcessor.Process(dialogueResponse, formattedText);
			}

			combinedResponses.Add(dialogueResponse);
		}

		//Combine snippets
		var finalResponse = new DialogueResponse();
		foreach (var combinedResponse in combinedResponses) {
			finalResponse.Response += combinedResponse.Response;
			finalResponse.ScriptNote += combinedResponse.ScriptNote;
		}

		//Apply post processors
		foreach (var postProcessor in PostProcessors) {
			postProcessor.Process(finalResponse);
		}

		return finalResponse;
	}

	public virtual bool Equals(DialogueResponse? other) {
		if (ReferenceEquals(null, other)) return false;
		if (ReferenceEquals(this, other)) return true;

		return Response == other.Response && ScriptNote == other.ScriptNote;
	}

	public override int GetHashCode() {
		return HashCode.Combine(Response, ScriptNote);
	}
}
