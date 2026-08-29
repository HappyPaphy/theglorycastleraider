using System.Collections;

namespace DunGen.Generation.Steps
{
	/// <summary>
	/// Defines a contract for a generation step that can be executed within a <see cref="GenerationPipeline"/>
	/// </summary>
	public interface IGenerationStep
	{
		IEnumerator Execute(GenerationContext context);
	}
}
