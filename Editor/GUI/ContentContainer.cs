using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DreadScripts.HierarchyPlus
{
	internal class ContentContainer
	{
		internal static ContentContainer _contentContainer;
		internal static ContentContainer Content => _contentContainer ?? (_contentContainer = new ContentContainer());

		internal readonly GUIContent checkmarkIcon = new GUIContent(EditorGUIUtility.IconContent("TestPassed")) {tooltip = "Up to Date!"};
		internal readonly GUIContent infoIcon = new GUIContent(EditorGUIUtility.IconContent("UnityEditor.InspectorWindow"));
		internal readonly GUIContent resetIcon = new GUIContent(EditorGUIUtility.IconContent("Refresh")) {tooltip = "Reset"};
		internal readonly GUIContent folderIcon = new GUIContent(EditorGUIUtility.IconContent("FolderOpened Icon")) {tooltip = "Select a folder"};

		private readonly GUIContent _tempContent = new GUIContent();

		internal GUIContent Temp(string text, string tooltip = "", Texture2D image = null)
		{
			_tempContent.text = text;
			_tempContent.tooltip = tooltip;
			_tempContent.image = image;
			return _tempContent;
		}
	}
}
