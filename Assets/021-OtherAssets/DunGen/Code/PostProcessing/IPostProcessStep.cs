using DunGen.Generation;
using System.Collections;

namespace DunGen.PostProcessing
{
	public interface IPostProcessStep
	{
		string DisplayName { get; }

		IEnumerator Execute(GenerationContext context);
	}
}