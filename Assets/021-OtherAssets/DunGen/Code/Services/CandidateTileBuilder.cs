using DunGen.Pooling;
using DunGen.TilePlacement;
using System;
using System.Collections.Generic;

namespace DunGen.Services
{
	/// <summary>
	/// Default implementation of <see cref="ICandidateTileBuilder"/>. Generates candidate tiles based on the
	/// tile weights specified in the pairing request, allowing for user-defined mutation of candidates before
	/// finalising the list of valid candidates with computed keys for selection.
	/// </summary>
	[Serializable, SubclassDisplay(displayName: "Default")]
	public class CandidateTileBuilder : ICandidateTileBuilder
	{
		public virtual void BuildCandidates(PairingRequest request, ref List<KeyedCandidateTile> outputCandidates, List<CandidateTileRule> customCandidateRules)
		{
			using var pendingCandidatesHandle = CollectionPool.List<CandidateTile>.Get(out var pendingCandidates);

			GetInitialTileCandidates(request, ref pendingCandidates);
			MutateCandidates(request, customCandidateRules, ref pendingCandidates);
			FinaliseCandidates(request, pendingCandidates, ref outputCandidates);
		}

		/// <summary>
		/// Extract the initial set of candidates from the request's tile weights, calculating the effective
		/// weight for each candidate based on the request's path position and the weighting parameters of
		/// each tile weight entry.
		/// </summary>
		/// <param name="request">The doorway pairing request</param>
		/// <param name="pendingCandidates">Output list of pending candidates to fill</param>
		protected virtual void GetInitialTileCandidates(PairingRequest request, ref List<CandidateTile> pendingCandidates)
		{
			// Build list of pending candidates with their effective weights based on path position
			foreach (var entry in request.TileWeights)
			{
				if (entry == null || entry.Value == null)
					continue;

				float weight = entry.GetEffectiveWeight(
					request.IsOnMainPath,
					request.NormalizedPathDepth,
					request.NormalizedBranchDepth);

				pendingCandidates.Add(new CandidateTile(entry.Value, weight, request.GenerationContext.TileSetLookup[entry]));
			}
		}

		/// <summary>
		/// Allows custom candidate rules to modify the list of pending candidate tiles before finalisation.
		/// </summary>
		/// <remarks>Override this method to implement additional mutation logic or to customize how candidate rules
		/// are applied. This method enables extensibility by allowing user-defined rules to influence the final set of
		/// candidate tiles.</remarks>
		/// <param name="request">The pairing request that provides context for candidate mutation.</param>
		/// <param name="customCandidateRules">A list of custom candidate tile rules to apply for mutating the pending candidates.</param>
		/// <param name="pendingCandidates">A reference to the list of candidate tiles that can be modified by the custom rules.</param>
		protected virtual void MutateCandidates(PairingRequest request, List<CandidateTileRule> customCandidateRules, ref List<CandidateTile> pendingCandidates)
		{
			// Allow user code to modify weights here before we finalise the candidates
			foreach (var rule in customCandidateRules)
				rule.MutateCandidateTiles?.Invoke(request, ref pendingCandidates);
		}

		/// <summary>
		/// Processes the list of pending candidate tiles and adds valid candidates, each with a computed key, to the output
		/// collection for further selection.
		/// </summary>
		/// <remarks>Candidates with null prefabs or invalid weights (non-positive, NaN, or infinite) are excluded
		/// from the output. Each valid candidate is assigned a key for use in weighted random selection.</remarks>
		/// <param name="request">The pairing request that provides context for candidate evaluation and key calculation.</param>
		/// <param name="pendingCandidates">The list of candidate tiles to be evaluated and potentially added to the output collection.</param>
		/// <param name="outputCandidates">A reference to the list where valid, keyed candidate tiles will be added for subsequent selection.</param>
		protected virtual void FinaliseCandidates(PairingRequest request, List<CandidateTile> pendingCandidates, ref List<KeyedCandidateTile> outputCandidates)
		{
			foreach (var candidate in pendingCandidates)
			{
				// Disallow candidates with invalid prefabs
				if (candidate.TilePrefab == null)
					continue;

				// Disallow candidates with non-positive or invalid weights
				if (candidate.Weight <= 0f || float.IsNaN(candidate.Weight) || float.IsInfinity(candidate.Weight))
					continue;

				// We use a keyed candidate to allow for a weighted random selection
				// of candidates later using a 'top-n' approach
				double key = CalculateCandidateKey(request, candidate);

				outputCandidates.Add(new KeyedCandidateTile(candidate, key));
			}
		}

		/// <summary>
		/// Calculates a random key for a candidate tile based on its weight, used for probabilistic selection.
		/// </summary>
		/// <remarks>This method uses the exponential distribution to generate keys</remarks>
		/// <param name="request">The pairing request that provides the random number generator context used for key calculation.</param>
		/// <param name="candidate">The candidate tile for which the random key is calculated. The tile's weight influences the distribution of the
		/// generated key.</param>
		/// <returns>A double value representing the randomly generated key for the candidate tile. Lower values indicate higher
		/// selection priority.</returns>
		protected double CalculateCandidateKey(PairingRequest request, CandidateTile candidate)
		{
			double u = 1.0 - request.GenerationContext.RandomStream.NextDouble();
			double key = -Math.Log(u) / candidate.Weight;

			return key;
		}
	}
}