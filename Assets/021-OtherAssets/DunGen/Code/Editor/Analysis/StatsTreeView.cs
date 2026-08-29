using DunGen.Analysis;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

#if UNITY_6000_3_OR_NEWER
using CompatTreeView = UnityEditor.IMGUI.Controls.TreeView<int>;
using CompatTreeViewState = UnityEditor.IMGUI.Controls.TreeViewState<int>;
using CompatTreeViewItem = UnityEditor.IMGUI.Controls.TreeViewItem<int>;
#else
using CompatTreeView = UnityEditor.IMGUI.Controls.TreeView;
using CompatTreeViewState = UnityEditor.IMGUI.Controls.TreeViewState;
using CompatTreeViewItem = UnityEditor.IMGUI.Controls.TreeViewItem;
#endif

namespace DunGen.Editor.Analysis
{
	internal sealed class StatsTreeView : CompatTreeView
	{
		#region Nested Types

		internal enum Col
		{
			Metric,
			Units,
			Min,
			Max,
			Mean,
			Median,
			StdDev,
			Histogram
		}

		internal sealed class StatItem : CompatTreeViewItem
		{
			public string FullKey;                  // "Generation.Time.Total"
			public AnalysisResults.Metric Metric;   // only meaningful if IsLeaf
			public bool IsLeaf;
		}

		#endregion

		private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
		private static GUIStyle numberStyle;

		private Dictionary<string, AnalysisResults.Metric> data = new Dictionary<string, AnalysisResults.Metric>();
		private GUIContent histogramButtonContent;


		public StatsTreeView(CompatTreeViewState state, MultiColumnHeader header)
			: base(state, header)
		{
			showAlternatingRowBackgrounds = false; // We draw our own
			showBorder = true;
			rowHeight = 20f;

			var histogramButtonIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(
				"Assets/DunGen/Code/Editor/Icons/HistogramButtonIcon.png");

			string histogramTooltip = "View histogram";
			histogramButtonContent = histogramButtonIcon != null
				? new GUIContent(histogramButtonIcon, histogramTooltip)
				: new GUIContent("H", histogramTooltip);

			Reload();
		}

		public void SetData(Dictionary<string, AnalysisResults.Metric> metrics)
		{
			data = new Dictionary<string, AnalysisResults.Metric>(metrics) ?? new Dictionary<string, AnalysisResults.Metric>();
		}

		public static MultiColumnHeaderState CreateDefaultHeaderState()
		{
			var cols = new[]
			{
				new MultiColumnHeaderState.Column
				{
					headerContent = new GUIContent("Metric"),
					width = 260, minWidth = 140, autoResize = true, allowToggleVisibility = false
				},
				new MultiColumnHeaderState.Column
				{
					headerContent = new GUIContent("Units"),
					width = 60, minWidth = 40, autoResize = false
				},
				new MultiColumnHeaderState.Column
				{
					headerContent = new GUIContent("Min"),
					width = 80, minWidth = 60, autoResize = true
				},
				new MultiColumnHeaderState.Column
				{
					headerContent = new GUIContent("Max"),
					width = 80, minWidth = 60, autoResize = true
				},
				new MultiColumnHeaderState.Column
				{
					headerContent = new GUIContent("Mean"),
					width = 90, minWidth = 60, autoResize = true
				},
				new MultiColumnHeaderState.Column
				{
					headerContent = new GUIContent("Median"),
					width = 90, minWidth = 60, autoResize = true
				},
				new MultiColumnHeaderState.Column
				{
					headerContent = new GUIContent("Std Dev"),
					width = 90, minWidth = 60, autoResize = true
				},
				new MultiColumnHeaderState.Column
				{
					headerContent = new GUIContent(""),
					width = 32, minWidth = 32, autoResize = false
				},
			};

			return new MultiColumnHeaderState(cols);
		}

		protected override CompatTreeViewItem BuildRoot()
		{
			var root = new StatItem
			{
				id = 0,
				depth = -1,
				displayName = "Root",
				FullKey = "",
				IsLeaf = false
			};

			root.children = new List<CompatTreeViewItem>();

			// Build a hierarchy from dot-separated keys
			int nextId = 1;
			var byPrefix = new Dictionary<string, StatItem>(StringComparer.Ordinal);

			foreach (var entry in data.Where(d => !string.IsNullOrEmpty(d.Key)))
			{
				var parts = entry.Key.Split('.');
				var parent = root;
				string prefix = "";

				for (int i = 0; i < parts.Length; i++)
				{
					var segment = parts[i];
					prefix = (prefix.Length == 0) ? segment : $"{prefix}.{segment}";
					bool isLeaf = (i == parts.Length - 1);

					if (!byPrefix.TryGetValue(prefix, out var node))
					{
						node = new StatItem
						{
							id = nextId++,
							displayName = segment,
							FullKey = prefix,
							IsLeaf = false,
							children = new List<CompatTreeViewItem>()
						};
						parent.AddChild(node);
						byPrefix[prefix] = node;

						// Auto-expand "Generation" top-level node by default
						if (prefix.StartsWith("Generation"))
							SetExpanded(node.id, true);
					}

					if (isLeaf)
					{
						node.IsLeaf = true;
						node.Metric = entry.Value;
					}

					parent = node;
				}
			}

			// Sort children alphabetically
			SortRecursively(root);

			SetupDepthsFromParentsAndChildren(root);
			return root;
		}

		private static void SortRecursively(CompatTreeViewItem item)
		{
			if (item.children == null) return;
			item.children.Sort((a, b) => string.CompareOrdinal(a.displayName, b.displayName));
			foreach (var c in item.children)
				SortRecursively(c);
		}

		protected override IList<CompatTreeViewItem> BuildRows(CompatTreeViewItem root)
		{
			// Default behaviour when no search:
			if (string.IsNullOrWhiteSpace(searchString))
				return base.BuildRows(root);

			// Custom search: show matching items + their ancestors so the hierarchy remains understandable
			var term = searchString.Trim();
			var matches = new List<CompatTreeViewItem>();
			var added = new HashSet<int>();

			void AddWithAncestors(CompatTreeViewItem item)
			{
				if (item == null || item == root)
					return;

				// Walk up to root, add in correct order (ancestors first)
				var stack = new Stack<CompatTreeViewItem>();
				var cur = item;
				while (cur != null && cur != root)
				{
					stack.Push(cur);
					cur = cur.parent;
				}

				while (stack.Count > 0)
				{
					var x = stack.Pop();
					if (added.Add(x.id))
					{
						// Force-expand so indentation / parent rows make sense during search
						if (x.hasChildren)
							SetExpanded(x.id, true);

						matches.Add(x);
					}
				}
			}

			void Recurse(CompatTreeViewItem item)
			{
				if (item.children == null) return;
				foreach (var c in item.children)
				{
					// Match against FULL key if available
					var si = c as StatItem;
					var hay = (si?.FullKey ?? c.displayName) ?? "";

					if (hay.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
						AddWithAncestors(c);

					Recurse(c);
				}
			}

			Recurse(root);
			return matches;
		}

		protected override void RowGUI(RowGUIArgs args)
		{
			// Draw alternating row backgrounds
			bool even = (args.row % 2) == 0;

			var colour = EditorGUIUtility.isProSkin
				? (even ? new Color(1f, 1f, 1f, 0.03f) : new Color(1f, 1f, 1f, 0.0f))
				: (even ? new Color(0f, 0f, 0f, 0.03f) : new Color(0f, 0f, 0f, 0.0f));

			EditorGUI.DrawRect(args.rowRect, colour);

			// Draw cells
			var item = (StatItem)args.item;

			for (int i = 0; i < args.GetNumVisibleColumns(); ++i)
			{
				var col = (Col)args.GetColumn(i);      // maps visible column index -> actual column id
				var cellRect = args.GetCellRect(i);    // rect for this column’s cell in this row
				CellGUI(cellRect, item, col, ref args);
			}
		}

		private void CellGUI(Rect cellRect, StatItem item, Col column, ref RowGUIArgs args)
		{
			CenterRectUsingSingleLineHeight(ref cellRect);

			switch (column)
			{
				case Col.Metric:
					// Draw foldout + icon + label using TreeView’s default rendering,
					// but constrain it to the Metric column cell rect:
					args.rowRect = cellRect;
					base.RowGUI(args);
					break;

				case Col.Units:
					if (item.IsLeaf && !string.IsNullOrEmpty(item.Metric.UnitsLabel))
						EditorGUI.LabelField(cellRect, item.Metric.UnitsLabel);
					break;

				case Col.Min:
					DrawNumberCell(cellRect, item, d => d.Min);
					break;

				case Col.Max:
					DrawNumberCell(cellRect, item, d => d.Max);
					break;

				case Col.Mean:
					DrawNumberCell(cellRect, item, d => d.Mean);
					break;

				case Col.Median:
					DrawNumberCell(cellRect, item, d => d.Median);
					break;

				case Col.StdDev:
					DrawNumberCell(cellRect, item, d => d.StandardDeviation);
					break;

				case Col.Histogram:
					if (item.IsLeaf && GUI.Button(cellRect, histogramButtonContent, EditorStyles.miniButton))
						HistogramWindow.Open(item.FullKey, item.Metric.UnitsLabel, item.Metric.Data);
					break;
			}
		}

		private static void DrawNumberCell(Rect r, StatItem item, Func<NumberSetData, double> selector)
		{
			if (!item.IsLeaf)
				return;

			if (numberStyle == null)
			{
				numberStyle = new GUIStyle(GUI.skin.label)
				{
					alignment = TextAnchor.MiddleRight,
					wordWrap = false,
					richText = false
				};
			}

			double v = selector(item.Metric.Data);
			string s = v.ToString("N2", Invariant);

			EditorGUI.LabelField(r, s, numberStyle);
		}

		private IEnumerable<StatItem> GetAllLeafItemsRespectingSearch()
		{
			var term = (searchString ?? "").Trim();
			bool hasTerm = !string.IsNullOrEmpty(term);

			bool MatchesSearch(StatItem it)
			{
				if (!hasTerm)
					return true;

				var hay = it.FullKey ?? it.displayName ?? "";
				return hay.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;
			}

			var result = new List<StatItem>(256);
			CollectLeaves(rootItem, result, MatchesSearch);
			return result;
		}

		private static void CollectLeaves(CompatTreeViewItem node, List<StatItem> output, Func<StatItem, bool> include)
		{
			if (node is StatItem si && si.IsLeaf)
			{
				if (include(si))
					output.Add(si);
			}

			if (node.hasChildren)
			{
				foreach (var child in node.children)
					CollectLeaves(child, output, include);
			}
		}

		/// <summary>
		/// Builds a formatted text representation of the current visible dungeon analysis data using the specified copy
		/// format.
		/// </summary>
		/// <param name="format">The format to use when generating the copy text. Must be a valid value of <see
		/// cref="DungeonAnalysisWindow.CopyFormat"/>.</param>
		/// <returns>A string containing the formatted dungeon analysis data in the specified copy format.</returns>
		/// <exception cref="ArgumentException">Thrown if <paramref name="format"/> is not a supported copy format.</exception>
		public string BuildCopyText(DungeonAnalysisWindow.CopyFormat format)
		{
			var leafRows = GetAllLeafItemsRespectingSearch();
			var dataSet = new Dictionary<string, AnalysisResults.Metric>();

			foreach (var row in leafRows)
				dataSet[row.FullKey] = row.Metric;

			switch (format)
			{
				case DungeonAnalysisWindow.CopyFormat.TabSeparated:
					return dataSet.MetricsToTabSeparatedTable();
				case DungeonAnalysisWindow.CopyFormat.IndentedTable:
					return dataSet.MetricsToIndentedTable();
				default:
					throw new ArgumentException($"Unsupported copy format '{format}'");
			}
		}
	}
}