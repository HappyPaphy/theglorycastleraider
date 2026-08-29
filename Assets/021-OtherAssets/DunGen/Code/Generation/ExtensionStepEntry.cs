using DunGen.Generation.Steps;
using System;
using UnityEngine;

namespace DunGen.Generation
{
	[Serializable]
	public sealed class ExtensionStepEntry
	{
		public bool Enabled = true;
		public PipelineAnchor Anchor = PipelineAnchor.BeforeAll;
		public int Order;

		[SerializeReference, SubclassSelector(allowNone: true)]
		public CustomGenerationStep Step;
	}
}