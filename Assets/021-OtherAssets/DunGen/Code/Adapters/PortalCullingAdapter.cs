using DunGen.Generation;

namespace DunGen.Adapters
{
	public abstract class CullingAdapter : BaseAdapter
	{
		public CullingAdapter()
		{
			Priority = -1;
		}

		protected abstract void PrepareForCulling(GenerationContext context);

		protected override void Run(GenerationContext context)
		{
			PrepareForCulling(context);
		}
	}
}
