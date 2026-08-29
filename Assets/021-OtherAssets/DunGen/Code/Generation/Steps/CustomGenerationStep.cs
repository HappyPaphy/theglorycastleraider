using System;
using System.Collections;

namespace DunGen.Generation.Steps
{
	[Serializable]
	public abstract class CustomGenerationStep : IGenerationStep
	{
		public virtual string DisplayName => GetType().Name;

		public abstract IEnumerator Execute(GenerationContext context);
	}
}