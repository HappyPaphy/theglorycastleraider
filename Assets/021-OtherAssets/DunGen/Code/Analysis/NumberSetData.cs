using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace DunGen.Analysis
{
	/// <summary>
	/// Represents a collection of floating-point numbers and provides statistical information such as minimum, maximum,
	/// average, and standard deviation.
	/// </summary>
	/// <remarks>Use the Add method to add values to the set. After modifying the collection, call RecalculateStats
	/// to update the statistical properties. The Min, Max, Average, and StandardDeviation properties are set to null if
	/// the collection is empty. This class is enumerable, allowing iteration over the contained values.</remarks>
	public sealed class NumberSetData : IEnumerable<double>
	{
		/// <summary>
		/// The smallest number in the collection, or null if the collection is empty
		/// </summary>
		public double Min
		{
			get
			{
				if (isDirty)
					RecalculateStats();

				return min;
			}
		}
		/// <summary>
		/// The largest number in the collection, or null if the collection is empty
		/// </summary>
		public double Max
		{
			get
			{
				if (isDirty)
					RecalculateStats();

				return max;
			}
		}
		/// <summary>
		/// The average of all numbers in the collection, or null if the collection is empty
		/// </summary>
		public double Mean
		{
			get
			{
				if (isDirty)
					RecalculateStats();

				return mean;
			}
		}
		/// <summary>
		/// The median of all numbers in the collection, or null if the collection is empty
		/// </summary>
		public double Median
		{
			get
			{
				if (isDirty)
					RecalculateStats();

				return median;
			}
		}
		/// <summary>
		/// The standard deviation of the set of numbers, or null if the collection is empty
		/// </summary>
		public double StandardDeviation
		{
			get
			{
				if (isDirty)
					RecalculateStats();

				return standardDeviation;
			}
		}

		public bool HasSamples => samples.Count > 0;

		private bool isDirty;
		private double min;
		private double max;
		private double mean;
		private double median;
		private double standardDeviation;
		private readonly List<double> samples = new List<double>();


		/// <summary>
		/// Removes all values from the collection and resets all calculated statistics
		/// </summary>
		public void Clear()
		{
			samples.Clear();
			min = 0;
			max = 0;
			mean = 0;
			median = 0;
			standardDeviation = 0;
			isDirty = false;
		}

		/// <summary>
		/// Adds a value to the collection
		/// </summary>
		/// <param name="value">The value to add to the collection</param>
		public void Add(double value)
		{
			samples.Add(value);
			isDirty = true;
		}

		/// <summary>
		/// Adds a range of values to the collection
		/// </summary>
		/// <param name="collection">The collection of values to add</param>
		public void AddRange(IEnumerable<double> collection)
		{
			samples.AddRange(collection);
			isDirty = true;
		}

		public IReadOnlyList<double> GetSamples() => samples;

		private void RecalculateStats()
		{
			isDirty = false;

			if (!HasSamples)
			{
				min = 0;
				max = 0;
				mean = 0;
				median = 0;
				standardDeviation = 0;

				return;
			}

			min = samples.Min();
			max = samples.Max();
			mean = samples.Average();

			// Median
			var sortedValues = samples
				.OrderBy(x => x)
				.ToArray();

			if (sortedValues.Length % 2 == 0)
				median = (sortedValues[(sortedValues.Length / 2) - 1] + sortedValues[sortedValues.Length / 2]) / 2f;
			else
				median = sortedValues[sortedValues.Length / 2];

			// Standard Deviation
			double[] temp = new double[samples.Count];
			for (int i = 0; i < temp.Length; i++)
				temp[i] = Math.Pow(samples[i] - Mean, 2);

			standardDeviation = Math.Sqrt(temp.Sum() / temp.Length);
		}

		public override string ToString()
		{
			if (isDirty)
				RecalculateStats();

			if (!HasSamples)
				return "[ No data available ]";
			else
				return $"[ Min: {Min:N1}, Max: {Max:N1}, Mean: {Mean:N1}, Median: {Median:N1}, Standard Deviation: {StandardDeviation:N2} ]";
		}

		#region IEnumerable Implementation

		public IEnumerator<double> GetEnumerator()
		{
			return samples.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		#endregion
	}
}

