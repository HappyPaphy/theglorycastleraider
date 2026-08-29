using DunGen.Analysis;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

#if UNITY_6000_3_OR_NEWER
using CompatTreeViewState = UnityEditor.IMGUI.Controls.TreeViewState<int>;
#else
using CompatTreeViewState = UnityEditor.IMGUI.Controls.TreeViewState;
#endif

namespace DunGen.Editor.Analysis
{
	public sealed class DungeonAnalysisWindow : EditorWindow
	{
		#region Nested Types

		[Flags]
		private enum SeverityMask
		{
			Info = 1 << 0,
			Warning = 1 << 1,
			Error = 1 << 2,
			All = Info | Warning | Error
		}

		public enum CopyFormat
		{
			TabSeparated,
			IndentedTable,
		}

		#endregion

		#region Statics

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void InitialiseStatics()
		{
			RuntimeAnalyzer.AnalysisComplete -= OnRuntimeAnalysisComplete;
			RuntimeAnalyzer.AnalysisComplete += OnRuntimeAnalysisComplete;
		}

		private static void OnRuntimeAnalysisComplete(RuntimeAnalyzer analyzer, AnalysisResults results)
		{
			Open(results);
		}

		#endregion

		private const float ToolbarHeight = 22f;
		private const float LogHeight = 180f;

		private AnalysisResults results;

		private CompatTreeViewState treeState;
		private MultiColumnHeader header;
		private StatsTreeView tree;

		private SearchField searchField;
		private string searchText = "";

		private Vector2 logScroll;
		private bool collapseDuplicateMessages = true;
		private SeverityMask severityMask = SeverityMask.All;


		public static void Open(AnalysisResults results)
		{
			var w = GetWindow<DungeonAnalysisWindow>("Dungeon Analysis");
			w.minSize = new Vector2(720, 420);

			w.results = results;

			w.RebuildTree();
			w.Show();
		}

		private void OnEnable()
		{
			treeState ??= new CompatTreeViewState();
			searchField ??= new SearchField();

			var headerState = StatsTreeView.CreateDefaultHeaderState();
			header = new MultiColumnHeader(headerState)
			{
				canSort = true,
				height = 22
			};
			header.ResizeToFit();

			tree = new StatsTreeView(treeState, header);
			RebuildTree();
		}

		private void RebuildTree()
		{
			if (tree == null || results == null || results.Metrics == null)
				return;

			tree.SetData(results.Metrics);
			tree.searchString = searchText ?? "";
			tree.Reload();
		}

		private void OnGUI()
		{
			var toolbarRect = new Rect(0, 0, position.width, ToolbarHeight);
			var treeRect = new Rect(0, ToolbarHeight, position.width, Mathf.Max(50, position.height - ToolbarHeight - LogHeight));
			var logRect = new Rect(0, position.height - LogHeight, position.width, LogHeight);

			DrawToolbar(toolbarRect);

			tree?.OnGUI(treeRect);

			DrawMessageLog(logRect);
		}

		private void DrawToolbar(Rect rect)
		{
			GUILayout.BeginArea(rect, EditorStyles.toolbar);
			EditorGUILayout.BeginHorizontal();

			// Search
			EditorGUI.BeginChangeCheck();
			searchText = searchField.OnToolbarGUI(searchText);
			if (EditorGUI.EndChangeCheck() && tree != null)
			{
				tree.searchString = searchText ?? "";
				tree.Reload();
			}

			GUILayout.FlexibleSpace();

			DrawExpandCollapseButtons();

			GUILayout.Space(10f);

			DrawCopySplitButton();

			EditorGUILayout.EndHorizontal();
			GUILayout.EndArea();
		}

		private void DrawExpandCollapseButtons()
		{
			using (new EditorGUI.DisabledScope(tree == null))
			{
				if (GUILayout.Button(new GUIContent("Expand All", "Expand all nodes"), EditorStyles.toolbarButton, GUILayout.Width(80)))
					tree.ExpandAll();
				if (GUILayout.Button(new GUIContent("Collapse All", "Collapse all nodes"), EditorStyles.toolbarButton, GUILayout.Width(80)))
					tree.CollapseAll();
			}
		}

		private void DrawCopySplitButton()
		{
			// Simple split button
			using (new EditorGUI.DisabledScope(tree == null))
			{
				var copyLabel = new GUIContent("Copy");
				var copyStyle = EditorStyles.toolbarButton;
				var dropdownStyle = EditorStyles.toolbarDropDown;

				// Main copy button
				if (GUILayout.Button(copyLabel, copyStyle, GUILayout.Width(60)))
					CopyToClipboard(CopyFormat.TabSeparated);

				// Dropdown
				if (GUILayout.Button(GUIContent.none, dropdownStyle, GUILayout.Width(22)))
				{
					var menu = new GenericMenu();
					menu.AddItem(new GUIContent("Tab Separated"), false, () => CopyToClipboard(CopyFormat.TabSeparated));
					menu.AddItem(new GUIContent("Indented Table"), false, () => CopyToClipboard(CopyFormat.IndentedTable));
					menu.ShowAsContext();
				}
			}
		}

		private void CopyToClipboard(CopyFormat format)
		{
			if (tree == null)
				return;

			var text = tree.BuildCopyText(format);
			EditorGUIUtility.systemCopyBuffer = text;
			ShowNotification(new GUIContent($"Copied ({format})"));
		}

		private void DrawMessageLog(Rect rect)
		{
			GUILayout.BeginArea(rect);
			EditorGUILayout.LabelField("Messages", EditorStyles.boldLabel);

			using (new EditorGUILayout.HorizontalScope())
			{
				severityMask = (SeverityMask)EditorGUILayout.EnumFlagsField("Severity", severityMask);
				collapseDuplicateMessages = EditorGUILayout.ToggleLeft("Collapse duplicates", collapseDuplicateMessages, GUILayout.Width(160));
			}

			logScroll = EditorGUILayout.BeginScrollView(logScroll);

			IEnumerable<AnalysisMessage> filtered = results.Messages
				.Where(PassesSeverityFilter);

			if (filtered.Any())
			{
				if (collapseDuplicateMessages)
				{
					// Group by (severity + text + context)
					var groups = filtered
						.GroupBy(m => (m.Severity, m.Message, m.ContextObject))
						.Select(g => (Key: g.Key, Count: g.Count(), Sample: g.First()))
						.ToList();

					foreach (var g in groups)
						DrawMessageLine(g.Sample, g.Count);
				}
				else
				{
					foreach (var m in filtered)
						DrawMessageLine(m, count: 1);
				}
			}
			else
			{
				EditorGUILayout.HelpBox("No messages to display.", MessageType.Info);
			}

			EditorGUILayout.EndScrollView();
			GUILayout.EndArea();
		}

		private bool PassesSeverityFilter(AnalysisMessage m)
		{
			return m.Severity switch
			{
				AnalysisMessageSeverity.Info => (severityMask & SeverityMask.Info) != 0,
				AnalysisMessageSeverity.Warning => (severityMask & SeverityMask.Warning) != 0,
				AnalysisMessageSeverity.Error => (severityMask & SeverityMask.Error) != 0,
				_ => true
			};
		}

		private void DrawMessageLine(AnalysisMessage m, int count)
		{
			var icon = m.Severity switch
			{
				AnalysisMessageSeverity.Info => EditorGUIUtility.IconContent("console.infoicon"),
				AnalysisMessageSeverity.Warning => EditorGUIUtility.IconContent("console.warnicon"),
				AnalysisMessageSeverity.Error => EditorGUIUtility.IconContent("console.erroricon"),
				_ => GUIContent.none
			};

			var colour = m.Severity switch
			{
				AnalysisMessageSeverity.Info => Color.white,
				AnalysisMessageSeverity.Warning => Color.yellow,
				AnalysisMessageSeverity.Error => Color.red,
				_ => Color.white
			};

			using (new EditorGUILayout.HorizontalScope())
			{
				var previousColor = GUI.color;
				GUI.color = colour;
				GUILayout.Label(icon, GUILayout.Width(18), GUILayout.Height(18));
				GUI.color = previousColor;

				var text = count > 1 ? $"{m.Message} (x{count})" : m.Message;
				var content = new GUIContent(text);

				var r = GUILayoutUtility.GetRect(content, EditorStyles.label, GUILayout.ExpandWidth(true));
				EditorGUI.LabelField(r, content);

				// Double-click to ping context
				if (m.ContextObject != null)
				{
					if (Event.current.type == EventType.MouseDown && Event.current.clickCount == 2 && r.Contains(Event.current.mousePosition))
					{
						EditorGUIUtility.PingObject(m.ContextObject);
						Event.current.Use();
					}
				}
			}
		}
	}
}