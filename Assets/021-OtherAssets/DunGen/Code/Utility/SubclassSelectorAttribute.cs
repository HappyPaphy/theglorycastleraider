using System;
using UnityEngine;

namespace DunGen
{
	[AttributeUsage(AttributeTargets.Field)]
	public class SubclassSelectorAttribute : PropertyAttribute
	{
		public bool AllowNone { get; set; }

		public SubclassSelectorAttribute(bool allowNone = true)
		{
			AllowNone = allowNone;
		}
	}

	[AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
	public class ExcludeSubclassAttribute : Attribute
	{
		public Type[] ExcludedTypes { get; private set; }
		public bool ExcludeDerivedTypes { get; private set; }


		public ExcludeSubclassAttribute(Type excludedType, bool excludeDerivedTypes = true)
		{
			ExcludedTypes = new Type[] { excludedType };
			ExcludeDerivedTypes = excludeDerivedTypes;
		}

		public ExcludeSubclassAttribute(Type[] excludedTypes, bool excludeDerivedTypes = true)
		{
			ExcludedTypes = excludedTypes;
			ExcludeDerivedTypes = excludeDerivedTypes;
		}

	}

	[AttributeUsage(AttributeTargets.Class)]
	public class SubclassDisplayAttribute : PropertyAttribute
	{
		public string DisplayName { get; set; }
		public string Description { get; set; }
		public bool Hidden { get; set; }
		public string HelpUrl { get; set; }


		public SubclassDisplayAttribute(
			string displayName = null,
			string description = null,
			bool hidden = false,
			string helpUrl = null)
		{
			DisplayName = displayName;
			Description = description;
			Hidden = hidden;
			HelpUrl = helpUrl;
		}
	}
}
