using System;
using UnityEngine;

namespace DreadScripts.HierarchyPlus
{
	internal sealed class IndentScope : IDisposable
	{
		internal IndentScope()
		{
			GUILayout.BeginHorizontal();
			GUILayout.Space(15);
			GUILayout.BeginVertical();
		}

		public void Dispose()
		{
			GUILayout.EndVertical();
			GUILayout.Space(15);
			GUILayout.EndHorizontal();
		}
	}
}
