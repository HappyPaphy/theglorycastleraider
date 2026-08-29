namespace DunGen.Async
{
	public readonly struct YieldSignal
	{
		public readonly YieldReason Reason;
		public YieldSignal(YieldReason reason) => Reason = reason;


		public static readonly YieldSignal Work = new YieldSignal(YieldReason.WorkBudget);
		public static readonly YieldSignal RoomPlaced = new YieldSignal(YieldReason.RoomPlaced);
		public static readonly YieldSignal BetweenSteps = new YieldSignal(YieldReason.BetweenSteps);
	}
}