namespace DunGen.Analysis
{
	public enum AnalysisMessageSeverity
	{
		Info,
		Warning,
		Error
	}

	public sealed class AnalysisMessage
	{
		/// <summary>
		/// The severity of the message
		/// </summary>
		public AnalysisMessageSeverity Severity { get; private set; }

		/// <summary>
		/// The text of the message
		/// </summary>
		public string Message { get; private set; }

		/// <summary>
		/// An optional context object related to the message
		/// </summary>
		public UnityEngine.Object ContextObject { get; private set; }


		public AnalysisMessage(AnalysisMessageSeverity severity, string message, UnityEngine.Object contextObject = null)
		{
			Severity = severity;
			Message = message;
			ContextObject = contextObject;
		}

		public void LogToConsole()
		{ 	
			switch (Severity)
			{
				case AnalysisMessageSeverity.Info:
					UnityEngine.Debug.Log(Message, ContextObject);
					break;
				case AnalysisMessageSeverity.Warning:
					UnityEngine.Debug.LogWarning(Message, ContextObject);
					break;
				case AnalysisMessageSeverity.Error:
					UnityEngine.Debug.LogError(Message, ContextObject);
					break;
			}
		}

		public override string ToString() => $"[{Severity}] {Message}";
	}
}