using UnityEditor;

namespace DunGen.Editor
{
	public sealed class CreateDunGenSettings : AssetPostprocessor
	{
#if UNITY_2021_2_OR_NEWER
		static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths, bool didDomainReload)
		{
			// Ignore if we're importing the DunGenSettings asset to avoid an infinite loop
			foreach (var path in importedAssets)
			{
				var assetType = AssetDatabase.GetMainAssetTypeAtPath(path);

				if (assetType == typeof(DunGenSettings))
					return;
			}

			DunGenSettings.FindOrCreateInstanceAsset();
		}
#else
		static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
		{
			// Ignore if we're importing the DunGenSettings asset to avoid an infinite loop
			foreach (var path in importedAssets)
			{
				var assetType = AssetDatabase.GetMainAssetTypeAtPath(path);

				if (assetType == typeof(DunGenSettings))
					return;
			}

			DunGenSettings.FindOrCreateInstanceAsset();
		}
#endif
	}
}
