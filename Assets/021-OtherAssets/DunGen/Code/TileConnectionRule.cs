using System;

namespace DunGen
{
	public sealed class TileConnectionRule
	{
		#region Legacy

		[Obsolete("Deprecated in 2.19. Use EvaluateConnection instead.")]
		public EvaluateConnectionDelegate ConnectionDelegate
		{
			get => EvaluateConnection;
			set => EvaluateConnection = value;
		}

		#endregion

		/// <summary>
		/// The result of evaluating a TileConnectionRule
		/// </summary>
		public enum ConnectionResult
		{
			/// <summary>
			/// The connection is allowed
			/// </summary>
			Allow,
			/// <summary>
			/// The connection is not allowed
			/// </summary>
			Deny,
			/// <summary>
			/// Let any lower priority rules decide whether this connection is allowed or not
			/// </summary>
			Passthrough,
		}

		public delegate ConnectionResult EvaluateConnectionDelegate(ProposedConnection connection);
		public delegate void AdjustConnectionWeightDelegate(ProposedConnection connection, ref float weight);

		/// <summary>
		/// This rule's priority. Higher priority rules are evaluated first. Lower priority rules are
		/// only evaluated if the delegate returns 'Passthrough' as the result
		/// </summary>
		public int Priority = 0;

		/// <summary>
		/// An optional delegate to determine if two tiles can connect using a given doorway pairing.
		/// Returning 'Passthrough' will allow lower priority rules to evaluate. If no rule handles the connection,
		/// the default method is used (only matching doorways are allowed to connect).
		/// </summary>
		public EvaluateConnectionDelegate EvaluateConnection;

		/// <summary>
		/// An optional delegate used to modify the weight of a proposed connection. This is useful if you want to make
		/// certain connections more or less likely to be used, without outright denying them.
		/// The higher the weight, the more likely it is that this connection will be used (relative to other connections).
		/// </summary>
		public AdjustConnectionWeightDelegate AdjustConnectionWeight;


		public TileConnectionRule(EvaluateConnectionDelegate evaluateConnection, int priority = 0)
		{
			EvaluateConnection = evaluateConnection;
			Priority = priority;
		}

		public TileConnectionRule(AdjustConnectionWeightDelegate adjustConnectionWeight, int priority = 0)
		{
			AdjustConnectionWeight = adjustConnectionWeight;
			Priority = priority;
		}

		public TileConnectionRule(EvaluateConnectionDelegate evaluateConnection, AdjustConnectionWeightDelegate adjustConnectionWeight, int priority = 0)
		{
			EvaluateConnection = evaluateConnection;
			AdjustConnectionWeight = adjustConnectionWeight;
			Priority = priority;
		}
	}
}
