using System;
using System.Collections.Generic;

namespace DunGen.Weighting
{
	/// <summary>
	/// A collection of weighted entries from which random selections can be made using complex weights
	/// based on the tile placement context (main path, branch path, depth, etc).
	/// Replaces the older <see cref="DunGen.GameObjectChanceTable"/> class in new code.
	/// </summary>
	[Serializable]
	public class WeightedTable<T>
	{
		public sealed class SelectionContext
		{
			public bool IsOnMainPath = true;
			public float NormalizedPathDepth = 0f;
			public float NormalizedBranchDepth = 0f;
			public bool AllowNullSelection = false;
			public bool AllowImmediateRepeats = true;
			public T PreviouslyChosen;
		}

		public List<WeightedEntry<T>> Entries = new List<WeightedEntry<T>>();


		public WeightedTable() { }

		public WeightedTable(WeightedTable<T> original)
		{
			Entries = new List<WeightedEntry<T>>(original.Entries);
		}

		public WeightedTable(params WeightedTable<T>[] tables)
		{
			foreach (var table in tables)
				foreach (var entry in table.Entries)
					Entries.Add(entry);
		}

		private static bool ValuesEqual(T a, T b)
		{
			return EqualityComparer<T>.Default.Equals(a, b);
		}

		/// <summary>
		/// Determines whether an entry is allowed by the basic selection rules before weights are considered.
		/// Entries are rejected if the entry itself is null, or if null values are not allowed
		/// and the entry's Value is null.
		/// </summary>
		private static bool IsEntryAllowed(WeightedEntry<T> entry, SelectionContext context)
		{
			if (entry == null)
				return false;

			if (!context.AllowNullSelection && entry.Value == null)
				return false;

			return true;
		}

		/// <summary>
		/// Determines whether the previously chosen value should be excluded from selection when
		/// immediate repeats are disabled.
		/// </summary>
		/// <remarks>
		/// Repeats are only prevented when there is at least one other valid entry with a positive weight.
		/// If no such alternative exists, the previous value remains selectable so that selection does not fail
		/// unnecessarily.
		/// </remarks>
		private bool ShouldPreventImmediateRepeats(SelectionContext context)
		{
			if (context.AllowImmediateRepeats)
				return false;

			// If we should try to prevent immediate repeats, we first need to check if there are any valid non-repeating entries to choose from
			// otherwise, we should allow repeats to avoid failure
			foreach (var entry in Entries)
			{
				if (!IsEntryAllowed(entry, context))
					continue;

				if (ValuesEqual(entry.Value, context.PreviouslyChosen))
					continue;

				float weight = entry.GetEffectiveWeight(context.IsOnMainPath, context.NormalizedPathDepth, context.NormalizedBranchDepth);
				if (weight > 0f)
					return true;
			}

			return false;
		}

		/// <summary>
		/// Determines whether an entry is selectable for the current context, including null-selection rules
		/// and optional immediate-repeat suppression.
		/// </summary>
		private static bool IsEntrySelectable(WeightedEntry<T> entry, SelectionContext context, bool preventImmediateRepeats)
		{
			if (!IsEntryAllowed(entry, context))
				return false;

			if (preventImmediateRepeats && ValuesEqual(entry.Value, context.PreviouslyChosen))
				return false;

			return true;
		}

		/// <summary>
		/// Calculates the total effective weight of all entries that are selectable in the supplied context.
		/// </summary>
		/// <param name="context">The selection rules and path/depth information used to evaluate entries.</param>
		/// <returns>The sum of the effective weights of all selectable entries.</returns>
		public float GetTotalWeight(SelectionContext context)
		{
			float totalWeight = 0f;
			bool preventImmediateRepeats = ShouldPreventImmediateRepeats(context);

			foreach (var entry in Entries)
			{
				if (!IsEntrySelectable(entry, context, preventImmediateRepeats))
					continue;

				float weight = entry.GetEffectiveWeight(context.IsOnMainPath, context.NormalizedPathDepth, context.NormalizedBranchDepth);
				if (weight <= 0f)
					continue;

				totalWeight += weight;
			}

			return totalWeight;
		}

		/// <summary>
		/// Attempts to select a random weighted entry using the supplied context.
		/// </summary>
		/// <param name="result">When this method returns true, contains the selected entry.</param>
		/// <param name="random">The random number source used to perform the weighted selection.</param>
		/// <param name="context">The selection rules and path/depth information used to evaluate entries.</param>
		/// <param name="removeFromTable">If true, removes the selected entry from the table.</param>
		/// <returns>
		/// true if a selectable entry with positive weight was found; otherwise, false.
		/// </returns>
		public bool TrySelectRandomEntry(out WeightedEntry<T> result, RandomStream random, SelectionContext context, bool removeFromTable = false)
		{
			bool preventImmediateRepeats = ShouldPreventImmediateRepeats(context);
			float totalWeight = 0f;

			foreach (var entry in Entries)
			{
				if (!IsEntrySelectable(entry, context, preventImmediateRepeats))
					continue;

				float weight = entry.GetEffectiveWeight(context.IsOnMainPath, context.NormalizedPathDepth, context.NormalizedBranchDepth);
				if (weight <= 0f)
					continue;

				totalWeight += weight;
			}

			if (totalWeight <= 0f)
			{
				result = default;
				return false;
			}

			float randomNumber = (float)(random.NextDouble() * totalWeight);

			foreach (var entry in Entries)
			{
				if (!IsEntrySelectable(entry, context, preventImmediateRepeats))
					continue;

				float weight = entry.GetEffectiveWeight(context.IsOnMainPath, context.NormalizedPathDepth, context.NormalizedBranchDepth);
				if (weight <= 0f)
					continue;

				if (randomNumber < weight)
				{
					if (removeFromTable)
						Entries.Remove(entry);

					result = entry;
					return true;
				}

				randomNumber -= weight;
			}

			result = default;
			return false;
		}

		/// <summary>
		/// Attempts to select a random value from the table using the supplied context.
		/// </summary>
		/// <param name="result">When this method returns true, contains the selected value.</param>
		/// <param name="random">The random number source used to perform the weighted selection.</param>
		/// <param name="context">The selection rules and path/depth information used to evaluate entries.</param>
		/// <param name="removeFromTable">If true, removes the selected entry from the table.</param>
		/// <returns>
		/// true if a selectable entry with positive weight was found; otherwise, false.
		/// </returns>
		public bool TrySelectRandom(out T result, RandomStream random, SelectionContext context, bool removeFromTable = false)
		{
			if (TrySelectRandomEntry(out var selectedEntry, random, context, removeFromTable))
			{
				result = selectedEntry.Value;
				return true;
			}
			else
			{
				result = default;
				return false;
			}
		}

		/// <summary>
		/// Determines whether the table contains an entry whose value is equal to the supplied value.
		/// </summary>
		public bool ContainsValue(T value)
		{
			foreach (var entry in Entries)
			{
				if (entry == null)
					continue;

				if (ValuesEqual(entry.Value, value))
					return true;
			}

			return false;
		}

		/// <summary>
		/// Determines whether the table contains at least one selectable entry with a positive effective weight
		/// for the supplied context.
		/// </summary>
		public bool HasAnyValidEntries(SelectionContext context)
		{
			if (context.AllowNullSelection)
				return true;

			bool preventImmediateRepeats = ShouldPreventImmediateRepeats(context);

			foreach (var entry in Entries)
			{
				if (!IsEntrySelectable(entry, context, preventImmediateRepeats))
					continue;

				float weight = entry.GetEffectiveWeight(context.IsOnMainPath, context.NormalizedPathDepth, context.NormalizedBranchDepth);
				if (weight > 0f)
					return true;
			}

			return false;
		}
	}
}