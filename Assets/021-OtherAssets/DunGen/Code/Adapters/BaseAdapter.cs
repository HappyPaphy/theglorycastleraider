using DunGen.Generation;
using DunGen.PostProcessing;
using System;
using UnityEngine;

namespace DunGen.Adapters
{
	public abstract class BaseAdapter : MonoBehaviour
	{
		public int Priority = 0;

		public virtual bool RunDuringAnalysis { get; set; }

		protected DungeonGenerator dungeonGenerator;
		protected PostProcessHook registeredHook;


		protected virtual void OnEnable()
		{
			if (TryGetComponent<RuntimeDungeon>(out var runtimeDungeon))
			{
				dungeonGenerator = runtimeDungeon.Generator;
				registeredHook = dungeonGenerator.RegisterPostProcessHook(OnPostProcess, Priority);
				dungeonGenerator.Cleared += Clear;
			}
			else
				Debug.LogError("[DunGen Adapter] RuntimeDungeon component is missing on GameObject '" + gameObject.name + "'. Adapters must be attached to the same GameObject as your RuntimeDungeon component");
		}

		protected virtual void OnDisable()
		{
			if (dungeonGenerator != null)
			{
				dungeonGenerator.UnregisterPostProcessHook(registeredHook);
				registeredHook = null;
				dungeonGenerator.Cleared -= Clear;
			}
		}

		private void OnPostProcess(GenerationContext context)
		{
			if (!context.IsAnalysis || RunDuringAnalysis)
				Run(context);
		}

		protected virtual void Clear() { }

		[Obsolete("Deprecated in 2.19. Use the version that takes a GenerationContext argument instead")]
		protected virtual void Run(DungeonGenerator generator) { }

		protected abstract void Run(GenerationContext context);
	}
}
