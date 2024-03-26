using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using static DreadScripts.HierarchyPlus.StylesContainer;
using static DreadScripts.HierarchyPlus.ContentContainer;

namespace DreadScripts.HierarchyPlus
{
	[Serializable]

	internal class SavedSettings
	{
		internal static SavedSettings settings => data;

		private const string PrefsKey = "HierarchyPlusSettingsJSON";
		private const bool isEditor = true;

		#region Main

		private static bool saveDisabled;
		private static bool _pendingSave;
		private static bool _savePaused;
		private static SavedSettings _data;

		internal static bool savePaused
		{
			get => _savePaused;
			set
			{
				bool wasPaused = _savePaused;
				_savePaused = value;

				if (wasPaused && !_savePaused && _pendingSave) Save();
			}
		}

		internal static Action onClear;

		internal static SavedSettings data
		{
			get
			{
				if (_data == null) Load();
				return _data;
			}
		}


		#region Methods

		internal static void Save()
		{
			_pendingSave = false;
			if (savePaused) _pendingSave = true;
			else if (!saveDisabled)
			{
				StringBuilder dataBuilder = new StringBuilder($"MAIN[{JsonUtility.ToJson(data)}]\u200B\u200B\u200B");

				string rawData = dataBuilder.ToString();
				string compressedData = CompressString(rawData);
				if (isEditor) EditorPrefs.SetString(PrefsKey, compressedData);
				else PlayerPrefs.SetString(PrefsKey, compressedData);
			}
		}

		private static void Load()
		{
			try
			{
				string fullData = string.Empty;

				if (isEditor && EditorPrefs.HasKey(PrefsKey))
					fullData = EditorPrefs.GetString(PrefsKey, string.Empty);
				else if (!isEditor && PlayerPrefs.HasKey(PrefsKey))
					fullData = PlayerPrefs.GetString(PrefsKey, string.Empty);
				if (!string.IsNullOrWhiteSpace(fullData))
					fullData = DecompressString(fullData);

				Dictionary<string, string> dataDictionary = new Dictionary<string, string>();

				if (!string.IsNullOrEmpty(fullData))
				{
					var matches = Regex.Matches(fullData, @"(\w+)\[(.*?)\]\u200B\u200B\u200B");
					for (int i = 0; i < matches.Count; i++)
					{
						var m = matches[i];
						dataDictionary.Add(m.Groups[1].Value, m.Groups[2].Value);
					}
				}

				if (dataDictionary.TryGetValue("MAIN", out string mainJson))
				{
					_data = JsonUtility.FromJson<SavedSettings>(mainJson);
				}

				if (_data == null) _data = new SavedSettings();
			}
			catch (Exception ex)
			{
				HierarchyPlus.CustomLog($"There was an error loading settings. Settings have been reset.\n\n{ex}", HierarchyPlus.CustomLogType.Warning);
				_data = new SavedSettings();
			}
		}

		internal static void AskClear()
		{
			if (EditorUtility.DisplayDialog("Clearing Settings", "Are you sure you want to clear the settings?", "Clear", "Cancel")) Clear();
		}

		internal static void Clear()
		{
			_data = new SavedSettings();
			onClear?.Invoke();
			Save();
		}

		private static string CompressString(string text)
		{
			byte[] buffer = Encoding.UTF8.GetBytes(text);
			var memoryStream = new MemoryStream();
			using (var gZipStream = new GZipStream(memoryStream, CompressionMode.Compress, true))
			{
				gZipStream.Write(buffer, 0, buffer.Length);
			}

			memoryStream.Position = 0;

			var compressedData = new byte[memoryStream.Length];
			memoryStream.Read(compressedData, 0, compressedData.Length);

			var gZipBuffer = new byte[compressedData.Length + 4];
			Buffer.BlockCopy(compressedData, 0, gZipBuffer, 4, compressedData.Length);
			Buffer.BlockCopy(BitConverter.GetBytes(buffer.Length), 0, gZipBuffer, 0, 4);
			return Convert.ToBase64String(gZipBuffer);
		}

		private static string DecompressString(string compressedText)
		{
			byte[] gZipBuffer = Convert.FromBase64String(compressedText);
			using (var memoryStream = new MemoryStream())
			{
				int dataLength = BitConverter.ToInt32(gZipBuffer, 0);
				memoryStream.Write(gZipBuffer, 4, gZipBuffer.Length - 4);

				var buffer = new byte[dataLength];

				memoryStream.Position = 0;
				using (var gZipStream = new GZipStream(memoryStream, CompressionMode.Decompress))
				{
					gZipStream.Read(buffer, 0, buffer.Length);
				}

				return Encoding.UTF8.GetString(buffer);
			}
		}

		#endregion

		internal class SaveOnChange : IDisposable
		{
			private readonly Action OnChange;
			private readonly bool wasPaused;
			private readonly EditorGUI.ChangeCheckScope changeScope;
			internal bool changed => changeScope.changed;

			public SaveOnChange(Action OnChange = null)
			{
				this.OnChange = OnChange;
				wasPaused = savePaused;
				savePaused = true;
				changeScope = new EditorGUI.ChangeCheckScope();
			}

			public void Dispose()
			{
				bool hasChanged = changeScope.changed;
				changeScope.Dispose();
				if (hasChanged)
				{
					OnChange?.Invoke();
					Save();
				}

				savePaused = wasPaused;
			}

			public static implicit operator bool(SaveOnChange soc) => soc.changeScope.changed;
		}

		internal class SavePauseScope : IDisposable
		{
			private readonly bool wasPaused;

			public SavePauseScope()
			{
				wasPaused = savePaused;
				savePaused = true;
			}

			public void Dispose()
			{
				savePaused = wasPaused;
			}
		}

		#endregion

		#region Classes

		[Serializable]
		internal class SavedBool : SavedValue
		{
			[SerializeField] private bool _value;
			internal readonly Action OnChanged;

			internal bool value
			{
				get => _value;
				set
				{
					if (_value == value) return;
					_value = value;
					OnChanged?.Invoke();
					Save();
				}
			}

			internal SavedBool(bool defaultValue, Action OnChangedCallback = null)
			{
				this.defaultValue = defaultValue;
				_value = defaultValue;
				OnChanged = OnChangedCallback;
			}

			internal void Toggle() => value = !_value;

			internal void DrawField(string label, GUIStyle style = null, params GUILayoutOption[] options)
				=> DrawField(new GUIContent(label), style, options);


			internal void DrawField(GUIContent label, GUIStyle style = null, params GUILayoutOption[] options)
			{
				if (style == null) value = EditorGUILayout.Toggle(label, value, options);
				else value = EditorGUILayout.Toggle(label, value, style, options);
			}

			internal void DrawToggle(string activeLabel, string inactiveLabel = null, GUIStyle style = null, Color? activeColor = null, Color? inactiveColor = null, params GUILayoutOption[] options)
				=> DrawToggle(string.IsNullOrEmpty(activeLabel) ? GUIContent.none : new GUIContent(activeLabel), string.IsNullOrEmpty(inactiveLabel) ? GUIContent.none : new GUIContent(inactiveLabel), style, activeColor, inactiveColor, options);

			internal void DrawToggle(GUIContent activeLabel, GUIContent inactiveLabel = null, GUIStyle style = null, Color? activeColor = null, Color? inactiveColor = null, params GUILayoutOption[] options)
			{
				activeColor = activeColor ?? GUI.backgroundColor;
				inactiveColor = inactiveColor ?? GUI.backgroundColor;
				Color ogColor = GUI.backgroundColor;
				GUI.backgroundColor = value ? (Color) activeColor : (Color) inactiveColor;
				value = GUILayout.Toggle(value, value || inactiveLabel == null ? activeLabel : inactiveLabel, style == null ? GUI.skin.button : style, options);
				GUI.backgroundColor = ogColor;
			}

			internal bool DrawFoldout(string label) => DrawFoldout(new GUIContent(label));

			internal bool DrawFoldout(GUIContent label)
			{
				return _value = EditorGUILayout.Foldout(_value, label);
				;
			}

			public static implicit operator bool(SavedBool s) => s._value;
			internal override void Reset() => value = (bool) defaultValue;

		}

		[Serializable]
		internal class SavedFloat : SavedValue
		{

			[SerializeField] private float _value;
			internal readonly Action OnChanged;

			internal float value
			{
				get => _value;
				set
				{
					if (_value != value)
					{
						_value = value;
						OnChanged?.Invoke();
						Save();
					}
				}
			}

			internal SavedFloat(float defaultValue, Action OnChangedCallback = null)
			{
				this.defaultValue = defaultValue;
				_value = defaultValue;
				OnChanged = OnChangedCallback;
			}

			internal override void Reset() => value = (float) defaultValue;

			public static implicit operator int(SavedFloat s) => (int) s._value;
			public static implicit operator float(SavedFloat s) => s._value;
		}

		[Serializable]
		internal class SavedString : SavedValue
		{
			[SerializeField] private string _value;
			internal readonly Action OnChanged;

			internal string value
			{
				get => _value;
				set
				{
					if (_value != value)
					{
						_value = value;
						OnChanged?.Invoke();
						Save();
					}
				}
			}

			internal SavedString(string defaultValue = "", Action OnChangedCallback = null)
			{
				this.defaultValue = defaultValue;
				_value = defaultValue;
				OnChanged = OnChangedCallback;
			}

			internal override void Reset() => value = (string) defaultValue;

			public override string ToString() => value;

			public void DrawField(string label, GUIStyle style = null, params GUILayoutOption[] options) => DrawField(new GUIContent(label), style, options);

			public void DrawField(GUIContent label, GUIStyle style = null, params GUILayoutOption[] options)
			{
				value = EditorGUILayout.DelayedTextField(label, value);
			}

			public static implicit operator string(SavedString s) => s._value;
		}

		[Serializable]
		internal class SavedColor : SavedValue
		{
			internal readonly Action OnChanged;

			[SerializeField] private float r;
			[SerializeField] private float g;
			[SerializeField] private float b;
			[SerializeField] private float a;

			internal Color color
			{
				get => new Color(r, g, b, a);
				set
				{
					r = value.r;
					g = value.g;
					b = value.b;
					a = value.a;
					OnChanged?.Invoke();
					Save();
				}
			}

			internal SavedColor(float r, float g, float b, float a = 1, Action OnChangedCallback = null)
			{
				Color def = new Color(r, g, b, a);
				defaultValue = def;
				this.r = r;
				this.g = g;
				this.b = b;
				this.a = a;
				OnChanged = OnChangedCallback;
			}

			internal SavedColor(Color defaultColor, Action OnChangedCallback = null)
			{
				this.defaultValue = defaultColor;
				r = defaultColor.r;
				g = defaultColor.g;
				b = defaultColor.b;
				a = defaultColor.a;
				OnChanged = OnChangedCallback;
			}

			internal void DrawField(string label, bool drawReset = true, params GUILayoutOption[] options)
			{
				DrawField(new GUIContent(label), drawReset, options);
			}

			internal void DrawField(GUIContent label, bool drawReset = true, params GUILayoutOption[] options)
			{
				using (new GUILayout.HorizontalScope())
				{
					color = EditorGUILayout.ColorField(label, color, options);
					if (!drawReset) return;
					if (GUILayout.Button(Content.resetIcon, Styles.labelButton, GUILayout.Width(18), GUILayout.Height(18)))
						Reset();
					HierarchyPlus.MakeRectLinkCursor();
				}
			}

			internal override void Reset() => color = (Color) defaultValue;

			public static implicit operator Color(SavedColor s) => s.color;
		}


		internal abstract class SavedValue
		{
			internal object defaultValue;
			internal abstract void Reset();
		}



		#endregion

		#region Saved Data

		[SerializeField] internal SavedString[]
			hiddenIconTypes = {new SavedString("MeshFilter")};

		[SerializeField] internal SavedColor
			rowOddColor = new SavedColor(new Color(0.5f, 0.5f, 1, 0.07f)),
			rowEvenColor = new SavedColor(new Color(0, 0, 0, 0.07f)),
			colorOne = new SavedColor(Color.white),
			colorTwo = new SavedColor(Color.white),
			colorThree = new SavedColor(Color.white),
			guideLinesColor = new SavedColor(Color.white),
			iconTintColor = new SavedColor(Color.white),
			iconFadedTintColor = new SavedColor(new Color(1, 1, 1, 0.5f));

		[SerializeField] internal SavedBool
			enabled = new SavedBool(true),
			colorsEnabled = new SavedBool(true),
			iconsEnabled = new SavedBool(true),
			enableContextClick = new SavedBool(true),
			enableDragToggle = new SavedBool(true),
			colorOneEnabled = new SavedBool(false),
			colorTwoEnabled = new SavedBool(false),
			colorThreeEnabled = new SavedBool(false),
			guideLinesEnabled = new SavedBool(true),
			rowColoringOddEnabled = new SavedBool(false),
			rowColoringEvenEnabled = new SavedBool(true),
			showGameObjectIcon = new SavedBool(true),
			useCustomGameObjectIcon = new SavedBool(true),
			showTransformIcon = new SavedBool(false),
			showNonBehaviourIcons = new SavedBool(true),
			linkCursorOnHover = new SavedBool(false);
		
		[SerializeField] internal SavedFloat
			guiXOffset = new SavedFloat(0);

		#endregion

		internal bool GetColorsEnabled() => enabled && colorsEnabled;
		internal bool GetIconsEnabled() => enabled && iconsEnabled;
		internal bool GetRowColoringEnabled() => (rowColoringOddEnabled || rowColoringEvenEnabled);
	}
}
