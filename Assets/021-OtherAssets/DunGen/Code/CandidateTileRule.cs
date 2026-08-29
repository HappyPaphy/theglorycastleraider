using DunGen.TilePlacement;
using System.Collections.Generic;

namespace DunGen
{
	public sealed class CandidateTileRule
	{
		public delegate void MutateCandidateTilesDelegate(PairingRequest request, ref List<CandidateTile> candidateTiles);

		/// <summary>
		/// This rule's priority. Higher priority rules are evaluated first
		/// </summary>
		public int Priority = 0;

		/// <summary>
		/// The delegate used to mutate the list of candidate tiles for a given doorway pairing request.
		/// This is useful if you want to add, remove, or modify candidate tiles based on custom logic.
		/// </summary>
		public MutateCandidateTilesDelegate MutateCandidateTiles;


		public CandidateTileRule(MutateCandidateTilesDelegate mutateCandidateTiles, int priority = 0)
		{
			MutateCandidateTiles = mutateCandidateTiles;
			Priority = priority;
		}
	}
}
