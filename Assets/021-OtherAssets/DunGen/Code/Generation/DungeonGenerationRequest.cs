using System.Collections.Generic;

namespace DunGen.Generation
{
	/// <summary>
	/// Represents a request to generate a dungeon, including configuration settings and optional tile injection
	/// methods.
	/// </summary>
	public class DungeonGenerationRequest
	{
		/// <summary>
		/// The configuration settings used by the dungeon generator
		/// </summary>
		public DungeonGeneratorSettings Settings;

		/// <summary>
		/// Settings for generating the new dungeon as an attachment to a previous dungeon
		/// </summary>
		public DungeonAttachmentSettings AttachmentSettings { get; set; }

		/// <summary>
		/// An optional list of methods that can be used to inject tiles into the dungeon during generation
		/// </summary>
		public List<TileInjectionDelegate> TileInjectionMethods = new List<TileInjectionDelegate>();


		public DungeonGenerationRequest(
			DungeonGeneratorSettings settings,
			DungeonAttachmentSettings attachmentSettings = null)
		{
			Settings = settings;
			AttachmentSettings = attachmentSettings;
		}
	}
}