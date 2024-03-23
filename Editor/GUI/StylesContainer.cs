using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DreadScripts.HierarchyPlus
{
	internal class StylesContainer
	{
		internal static StylesContainer _styles;
		internal static StylesContainer Styles => _styles ?? (_styles = new StylesContainer());
		
		internal readonly GUIStyle
			labelButton = new GUIStyle(GUI.skin.label) {padding = new RectOffset(), margin = new RectOffset(1, 1, 1, 1)},
			faintLabel = new GUIStyle(GUI.skin.label) {fontStyle = FontStyle.Italic, richText = true, fontSize = 11, normal = {textColor = EditorGUIUtility.isProSkin ? Color.gray : new Color(0.357f, 0.357f, 0.357f)}},
			assetLabel = "AssetLabel",
			bigTitle = "in bigtitle",
			faintLinkLabel;

		internal StylesContainer()
		{
			faintLinkLabel = new GUIStyle(faintLabel) {name = "Toggle", hover = {textColor = new Color(0.3f, 0.7f, 1)}};
		}
	}
}
