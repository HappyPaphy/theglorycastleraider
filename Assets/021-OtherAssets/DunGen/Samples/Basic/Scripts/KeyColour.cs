using System.ComponentModel;
using UnityEngine;

namespace DunGen.Demo
{
	public class KeyColour : MonoBehaviour, IKeyLock
	{
		[SerializeField]
		private int keyID;

		[SerializeField]
		private KeyManager keyManager;

		private MaterialPropertyBlock propertyBlock;


		public void OnKeyAssigned(Key key, KeyManager manager)
		{
			keyID = key.ID;
			keyManager = manager;

			SetColour(key.Colour);
		}

		private void Start()
		{
			if (keyManager == null)
				return;

			var key = keyManager.GetKeyByID(keyID);
			SetColour(key.Colour);
		}

		private void SetColour(Color colour)
		{
			if (Application.isPlaying)
			{
				if(propertyBlock == null)
					propertyBlock = new MaterialPropertyBlock();

				propertyBlock.SetColor("_Color", colour);

				foreach (var r in GetComponentsInChildren<Renderer>())
					r.SetPropertyBlock(propertyBlock);
			}
		}
	}
}