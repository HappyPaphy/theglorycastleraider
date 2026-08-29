using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DunGen.Editor
{
	public class DungeonStatsDisplayWindow : EditorWindow
	{
		#region Statics

		[MenuItem("Window/DunGen/Generation Stats")]
		public static void ShowWindow()
		{
			var window = GetWindow<DungeonStatsDisplayWindow>("Generation Stats");
			window.minSize = new Vector2(380, 590);
			window.Show();
		}

		#endregion

		private const int WideModeThreshold = 600;

		private GenerationStats currentStats;
		private bool isGenerating;
		private ScrollView tileStatsScrollView;
		private VisualElement leftPanel;
		private VisualElement rightPanel;
		private VisualElement statsContainer;
		private Label generatingLabel;
		private Label noStatsLabel;

		private void OnEnable()
		{
			DungeonGenerator.OnAnyDungeonGenerationStarted += OnGenerationStarted;
			CreateUI();
		}

		private void OnDisable()
		{
			DungeonGenerator.OnAnyDungeonGenerationStarted -= OnGenerationStarted;
		}

		private void CreateUI()
		{
			var root = rootVisualElement;

			// Load stylesheet
			var styleSheet = FindStyleSheet();
			if (styleSheet != null)
				root.styleSheets.Add(styleSheet);

			// Create status labels container
			var statusContainer = new VisualElement() { name = "StatusContainer" };
			root.Add(statusContainer);

			generatingLabel = new Label("Generation in progress. Please wait...") { name = "StatusLabel" };
			noStatsLabel = new Label("No generation stats available. Generate a dungeon to see statistics.") { name = "StatusLabel" };
			statusContainer.Add(generatingLabel);
			statusContainer.Add(noStatsLabel);

			// Create main container with column flex
			var mainContainer = new VisualElement() { name = "MainContainer" };
			mainContainer.style.flexDirection = FlexDirection.Column;
			mainContainer.style.flexGrow = 1;
			root.Add(mainContainer);

			// Content container for panels (row or column based on width)
			var contentContainer = new VisualElement() { name = "ContentContainer" };
			contentContainer.style.flexGrow = 1;
			mainContainer.Add(contentContainer);

			// Panels
			leftPanel = new VisualElement() { name = "LeftPanel" };
			rightPanel = new VisualElement() { name = "RightPanel" };
			contentContainer.Add(leftPanel);
			contentContainer.Add(rightPanel);

			CreateLeftPanel();
			CreateRightPanel();

			// Register callback for window resize
			root.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);

			UpdateUI();
		}

		private void OnGeometryChanged(GeometryChangedEvent evt)
		{
			var contentContainer = rootVisualElement.Q("ContentContainer");

			// Switch between row and column layout based on width
			if (contentContainer != null)
			{
				bool isWide = evt.newRect.width >= WideModeThreshold;

				contentContainer.style.flexDirection = isWide ? FlexDirection.Row : FlexDirection.Column;

				if (isWide)
				{
					leftPanel.style.width = new Length(40, LengthUnit.Percent);
					rightPanel.style.width = new Length(60, LengthUnit.Percent);
					leftPanel.style.marginRight = 10;
					rightPanel.style.marginLeft = 10;
					rightPanel.style.marginTop = 0;

					// Reset height styles
					leftPanel.style.height = StyleKeyword.Auto;
					rightPanel.style.height = StyleKeyword.Auto;
				}
				else
				{
					// In column mode, use full width
					leftPanel.style.width = new Length(100, LengthUnit.Percent);
					rightPanel.style.width = new Length(100, LengthUnit.Percent);
					leftPanel.style.marginRight = 0;
					rightPanel.style.marginLeft = 0;
					rightPanel.style.marginTop = 10;

					leftPanel.style.height = StyleKeyword.Auto;
					rightPanel.style.flexGrow = 1;
				}
			}
		}

		private StyleSheet FindStyleSheet()
		{
			// Find the stylesheet
			const string styleSheetName = "GenerationStatsWindow";
			var guids = AssetDatabase.FindAssets($"{styleSheetName} t:StyleSheet");

			if (guids.Length == 0)
			{
				Debug.LogWarning($"Could not find {styleSheetName}.uss stylesheet");
				return null;
			}

			var path = AssetDatabase.GUIDToAssetPath(guids[0]);
			return AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
		}

		private void CreateLeftPanel()
		{
			// Overview section
			leftPanel.Add(new Label("Generation Overview") { name = "SectionHeader" });
			statsContainer = new VisualElement() { name = "StatsContainer" };
			leftPanel.Add(statsContainer);

			// Generation Steps section
			leftPanel.Add(new Label("Generation Step Times") { name = "SectionHeader" });
			var stepsContainer = new VisualElement() { name = "StepsContainer" };
			leftPanel.Add(stepsContainer);
		}

		private void CreateRightPanel()
		{
			var rightContainer = new VisualElement() { name = "RightContainer" };
			rightContainer.style.flexGrow = 1;
			rightContainer.style.flexDirection = FlexDirection.Column; // Ensure vertical layout
			rightPanel.Add(rightContainer);

			rightContainer.Add(new Label("Tile Statistics") { name = "SectionHeader" });

			// Create scroll view container that fills remaining space
			var scrollContainer = new VisualElement() { name = "ScrollContainer" };
			scrollContainer.style.flexGrow = 1;
			scrollContainer.style.overflow = Overflow.Hidden;
			rightContainer.Add(scrollContainer);

			// Create scroll view for tile stats
			tileStatsScrollView = new ScrollView(ScrollViewMode.Vertical) { name = "TileStatsScrollView" };
			tileStatsScrollView.style.flexGrow = 1;
			tileStatsScrollView.style.minHeight = 0;
			scrollContainer.Add(tileStatsScrollView);
		}

		private void UpdateUI()
		{
			if (rootVisualElement == null)
				return;

			generatingLabel.style.display = isGenerating ? DisplayStyle.Flex : DisplayStyle.None;
			noStatsLabel.style.display = (!isGenerating && currentStats == null) ? DisplayStyle.Flex : DisplayStyle.None;

			var mainContainer = rootVisualElement.Q("MainContainer");
			mainContainer.style.display = (!isGenerating && currentStats != null) ? DisplayStyle.Flex : DisplayStyle.None;

			if (currentStats != null && !isGenerating)
			{
				UpdateStats();
				UpdateTileStats();
			}
		}

		private void UpdateStats()
		{
			statsContainer.Clear();

			AddStatRow("Total Time", $"{currentStats.TotalTime:F2} ms");
			AddStatRow("Total Room Count", currentStats.TotalRoomCount.ToString());
			AddStatRow("Main Path Rooms", currentStats.MainPathRoomCount.ToString());
			AddStatRow("Branch Path Rooms", currentStats.BranchPathRoomCount.ToString());
			AddStatRow("Max Branch Depth", currentStats.MaxBranchDepth.ToString());
			AddStatRow("Total Retries", currentStats.TotalRetries.ToString());

			var stepsContainer = rootVisualElement.Q("StepsContainer");
			stepsContainer.Clear();

			foreach (var step in currentStats.GenerationStepTimes)
				AddStatRow(step.Key.ToString(), $"{step.Value:F2} ms", stepsContainer);
		}

		private void AddStatRow(string label, string value, VisualElement container = null)
		{
			container ??= statsContainer;

			var row = new VisualElement() { name = "StatRow" };
			row.style.flexDirection = FlexDirection.Row;

			// Add alternate background based on row index
			if (container.Children().Count() % 2 == 1)
				row.AddToClassList("alternate");

			var labelElement = new Label(label) { name = "StatLabel" };
			var valueElement = new Label(value) { name = "StatValue" };

			row.Add(labelElement);
			row.Add(valueElement);
			container.Add(row);
		}

		private void UpdateTileStats()
		{
			tileStatsScrollView.Clear();

			foreach (var stat in currentStats.GetTileStatistics())
			{
				var tileContainer = new VisualElement() { name = "TileStatContainer" };

				var prefabLabel = new Label(stat.TilePrefab.name) { name = "TilePrefabLabel" };
				prefabLabel.AddToClassList("clickable-label");

				// Add click handler to select the prefab in Project window
				prefabLabel.RegisterCallback<ClickEvent>(_ =>
				{
					EditorGUIUtility.PingObject(stat.TilePrefab);
					Selection.activeObject = stat.TilePrefab;
				});

				tileContainer.Add(prefabLabel);
				tileContainer.Add(new Label("Usage Statistics:") { name = "SubHeader" });
				tileContainer.Add(new Label($"Total Uses: {stat.TotalCount}"));

				int instantiated = stat.TotalCount - stat.FromPoolCount;
				if (stat.FromPoolCount > 0)
				{
					tileContainer.Add(new Label($"• New Instances: {instantiated}"));
					tileContainer.Add(new Label($"• From Pool: {stat.FromPoolCount}"));
				}

				tileStatsScrollView.Add(tileContainer);
			}
		}

		private void OnGenerationStarted(DungeonGenerator generator)
		{
			generator.OnDungeonGenerationComplete += OnGenerationComplete;
			isGenerating = true;
			SetStats(null);
		}

		private void OnGenerationComplete(DungeonGenerator generator, Dungeon dungeon)
		{
			generator.OnDungeonGenerationComplete -= OnGenerationComplete;
			isGenerating = false;
			SetStats(generator.GenerationStats);
		}

		public void SetStats(GenerationStats stats)
		{
			currentStats = stats;
			UpdateUI();
		}
	}
}
