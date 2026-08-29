using System;

namespace DunGen
{
	/**
     * A series of classes for getting a random value between a given range
     */

	[Serializable]
	public class IntRange
	{
		public int Min;
		public int Max;


		public IntRange() { }
		public IntRange(int min, int max)
		{
			Min = min;
			Max = max;
		}

		public int GetRandom(RandomStream random)
		{
			if (Min > Max)
				Max = Min;

			return random.Next(Min, Max + 1);
		}

		public override string ToString()
		{
			return Min + " - " + Max;
		}
	}

	[Serializable]
	public class FloatRange
	{
		public float Min;
		public float Max;


		public FloatRange() { }
		public FloatRange(float min, float max)
		{
			Min = min;
			Max = max;
		}

		public float GetRandom(RandomStream random)
		{
			if (Min > Max)
			{
				float temp = Min;
				Min = Max;
				Max = temp;
			}

			float range = Max - Min;
			return Min + ((float)random.NextDouble() * range);
		}
	}

	[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
	public sealed class FloatRangeLimitAttribute : Attribute
	{
		public float MinLimit { get; private set; }
		public float MaxLimit { get; private set; }


		public FloatRangeLimitAttribute(float minLimit, float maxLimit)
		{
			MinLimit = minLimit;
			MaxLimit = maxLimit;
		}
	}
}
