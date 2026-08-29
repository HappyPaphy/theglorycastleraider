using DunGen.Analysis;
using System;
using System.Linq;
using System.Text;
using UnityEngine;

namespace DunGen.Demo
{
	public class Generator : MonoBehaviour
	{
		public RuntimeDungeon DungeonGenerator;
		public Action<StringBuilder> GetAdditionalText;

		private readonly StringBuilder infoText = new StringBuilder();
		private IDemoInputBridge inputBridge;
		private bool showStats = true;
		private float keypressDelay = 0.1f;
		private float timeSinceLastPress;
		private bool allowHold;
		private bool isKeyDown;


		private void Start()
		{
			inputBridge = new DemoInputBridge();

			if (DungeonGenerator == null)
				DungeonGenerator = GetComponentInChildren<RuntimeDungeon>();

			DungeonGenerator.Generator.OnGenerationStatusChanged += OnGenerationStatusChanged;
			GenerateRandom();
		}

		private void OnGenerationStatusChanged(DungeonGenerator generator, GenerationStatus status)
		{
			const int textPadding = 30;

			static void AddEntry(StringBuilder stringBuilder, string title, string entry)
			{
				string spacing = new string(' ', Mathf.Max(0, textPadding - title.Length));

				stringBuilder.Append($"\n\t{title}:{spacing}\t{entry}");
			}


			infoText.Length = 0;

			if (status != GenerationStatus.Complete)
			{
				if (status == GenerationStatus.Failed)
					infoText.Append("Generation Failed");
				else if (status == GenerationStatus.NotStarted)
				{
				}
				else
					infoText.Append($"Generating ({status})...");

				return;
			}

			infoText.AppendLine("Seed: " + generator.ChosenSeed);
			infoText.AppendLine();
			infoText.Append("## TIME TAKEN ##");

			foreach (var step in GenerationAnalysis.MeasurableSteps)
			{
				float generationTime = generator.GenerationStats.GetGenerationStepTime(step);
				AddEntry(infoText, step.ToString(), $"{generationTime:0.00} ms ({generationTime / generator.GenerationStats.TotalTime:P0})");
			}

			infoText.Append("\n\t-------------------------------------------------------");
			AddEntry(infoText, "Total", $"{generator.GenerationStats.TotalTime:0.00} ms");

			infoText.AppendLine();
			infoText.AppendLine();

			infoText.AppendLine("## ROOM COUNT ##");
			infoText.Append($"\n\tMain Path: {generator.GenerationStats.MainPathRoomCount}");
			infoText.Append($"\n\tBranch Paths: {generator.GenerationStats.BranchPathRoomCount}");
			infoText.Append("\n\t-------------------");
			infoText.Append($"\n\tTotal: {generator.GenerationStats.TotalRoomCount}");

			infoText.AppendLine();

			infoText.Append($"\n\tRetry Count: {generator.GenerationStats.TotalRetries}");

			infoText.AppendLine();
			infoText.AppendLine();

			infoText.AppendLine("Press 'F1' to toggle this information");
			infoText.AppendLine("Press 'R' to generate a new layout");

			GetAdditionalText?.Invoke(infoText);
		}

		public void GenerateRandom()
		{
			DungeonGenerator.Generate();
		}

		private void Update()
		{
			timeSinceLastPress += Time.deltaTime;

			var resetInputState = inputBridge.GetResetInputState();

			if (resetInputState == InputState.Pressed)
			{
				timeSinceLastPress = 0;
				isKeyDown = true;

				GenerateRandom();
			}

			if (resetInputState == InputState.Released)
			{
				isKeyDown = false;
				allowHold = false;
			}

			if (!allowHold && isKeyDown && timeSinceLastPress >= keypressDelay)
			{
				allowHold = true;
				timeSinceLastPress = 0;
			}


			if (allowHold && resetInputState == InputState.Held)
			{
				if (timeSinceLastPress >= keypressDelay)
				{
					GenerateRandom();
					timeSinceLastPress = 0;
				}
			}

			if (inputBridge.GetToggleStats())
				showStats = !showStats;
		}

		private void OnGUI()
		{
			if (showStats)
				GUILayout.Label(infoText.ToString());
		}
	}
}