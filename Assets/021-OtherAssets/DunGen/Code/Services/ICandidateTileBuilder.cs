using DunGen.TilePlacement;
using System.Collections.Generic;

namespace DunGen.Services
{
	/// <summary>
	/// Defines a method for generating candidate tiles based on a request for finding valid doorway pairs.
	/// </summary>
	public interface ICandidateTileBuilder
	{
		/// <summary>
		/// Builds a collection of candidate tiles based on the specified pairing request.
		/// </summary>
		/// <param name="request">The pairing request that defines the criteria for selecting candidate tiles.</param>
		/// <param name="outputCandidates">A list to which the method adds the generated candidate tiles. Existing items in the list may be modified or new
		/// items may be appended. This list is pooled and should not be cached by the user</param>
		/// <param name="customCandidateRules">A list of rules that can be applied to modify candidate tiles after they have been generated. This allows for user code to adjust candidates before they are used for selection.</param>
		public void BuildCandidates(PairingRequest request, ref List<KeyedCandidateTile> outputCandidates, List<CandidateTileRule> customCandidateRules);
	}
}