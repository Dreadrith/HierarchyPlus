using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using System.Reflection;
using Object = UnityEngine.Object;
using System.IO;
using System.Linq;
using static DreadScripts.HierarchyPlus.SavedSettings;
using static DreadScripts.HierarchyPlus.StylesContainer;

namespace DreadScripts.HierarchyPlus
{
    public class HierarchyPlus : EditorWindow
    {
        #region Constants
        private const string PRODUCT_NAME = "HierarchyPlus";
        private const string PACKAGE_ICON_FOLDER_PATH = "CustomIcons";
        private const string MISSING_SCRIPT_ICON_NAME = "Missing";
        private const string DEFAULT_ICON_NAME = "Default";
        private static readonly int DRAG_TOGGLE_HOT_CONTROL_ID = "HierarchyPlusDragToggleId".GetHashCode();
        #endregion
        
        #region Variables
        private static readonly Dictionary<Type, GUIContent> iconCache = new Dictionary<Type, GUIContent>();
        private static readonly Dictionary<string, GUIContent> customIconCache = new Dictionary<string, GUIContent>();
        private static readonly Texture2D[] defaultTextures = new Texture2D[3];
        private static readonly HashSet<Object> dragToggledObjects = new HashSet<Object>();
        private static bool dragToggleNewState;

        private static MethodInfo GetGameObjectIconMethod;
        private static GUIContent gameObjectContent;
        private static GUIContent missingScriptContent;
        private static GUIContent defaultContent;
        private static string iconFolderPath;
        private static Vector2 scroll;

        private static bool
	        colorsFoldout = true,
	        mainColorsFoldout,
	        miscColorsFolddout,
	        iconsFoldout = true,
	        coloredItemsFoldout,
	        hiddenIconsFoldout,
	        rowShadingFolout;
        #endregion
        
        #region Window
        [MenuItem("DreadTools/HierarchyPlus", false, 366)]
        private static void OpenSettings()
		{
			GetWindow<HierarchyPlus>($"{PRODUCT_NAME} Settings");
		}

		private void OnGUI()
		{
			EditorGUI.BeginChangeCheck();
			scroll = EditorGUILayout.BeginScrollView(scroll);
			
			using (new GUILayout.HorizontalScope())
			{
				settings.enabled.DrawField("HierarchyPlus Enabled");
				GUILayout.FlexibleSpace();
				if (GUILayout.Button(new GUIContent("Refresh Icons", "Use this to update the icons in the hierarchy window."), GUI.skin.button, GUILayout.ExpandWidth(false))) 
					InitializeAll();
				MakeRectLinkCursor();
			}

			using (new GUILayout.VerticalScope(GUI.skin.box))
			{
				colorsFoldout = DrawFoldoutTitle("Colors", colorsFoldout, settings.colorsEnabled);

				if (colorsFoldout)
				{
					using (new EditorGUI.DisabledScope(!settings.GetColorsEnabled()))
					{
						using (new GUILayout.VerticalScope(EditorStyles.helpBox))
							if (Foldout("Main", ref mainColorsFoldout))
							{
								using (new IndentScope())
								{
									DrawColorSetting("Active Icon Tint", settings.iconTintColor);
									DrawColorSetting("Inactive Icon Tint", settings.iconFadedTintColor);
									DrawColorSetting("Guide Lines", settings.guideLinesColor, settings.guideLinesEnabled);
									DrawColorSetting("Icon Background", settings.iconBackgroundColor, settings.iconBackgroundColorEnabled);
									using (new GUILayout.HorizontalScope())
									{
										var toggle = settings.iconBackgroundOverlapOnly;
										GUIContent toggleTooltip = toggle ? new GUIContent(string.Empty, "Enabled") : new GUIContent(string.Empty, "Disabled");
										toggle.DrawToggle(toggleTooltip, null, EditorStyles.radioButton, pastelGreenColor, Color.grey, GUILayout.Width(18), GUILayout.Height(18));
										var r = GUILayoutUtility.GetLastRect();
										EditorGUIUtility.AddCursorRect(r, MouseCursor.Link);
										GUILayout.Label("Icon Background On Overlap Only", EditorStyles.label);
									}
								}
							}


						using (new GUILayout.VerticalScope(EditorStyles.helpBox))
						{
							Foldout("Row Coloring", ref rowShadingFolout);
							if (rowShadingFolout)
							{
								using (new IndentScope())
								{
									DrawColorSetting("Odd Color", settings.rowOddColor, settings.rowColoringOddEnabled);
									DrawColorSetting("Even Color", settings.rowEvenColor, settings.rowColoringEvenEnabled);
								}
							}
						}

						using (new GUILayout.VerticalScope(EditorStyles.helpBox))
						{
							Foldout("Misc", ref miscColorsFolddout);
							if (miscColorsFolddout)
								using (new IndentScope())
								{
									DrawColorSetting("Misc 1", settings.colorOne, settings.colorOneEnabled);
									DrawColorSetting("Misc 2", settings.colorTwo, settings.colorTwoEnabled);
									DrawColorSetting("Misc 3", settings.colorThree, settings.colorThreeEnabled);
								}
						}

					}

				}
			}

			using (new GUILayout.VerticalScope(GUI.skin.box))
			{
				iconsFoldout = DrawFoldoutTitle("Components", iconsFoldout, settings.iconsEnabled);

				if (iconsFoldout)
				{
					using (new EditorGUI.DisabledScope(!settings.GetIconsEnabled()))
					using (new GUILayout.VerticalScope())
					{
						EditorGUIUtility.labelWidth = 200;
						settings.enableContextClick.DrawField("Enable Context Click");
						settings.enableDragToggle.DrawField("Enable Drag-Toggle");
						settings.showGameObjectIcon.DrawField("Show GameObject Icon");
						using (new EditorGUI.DisabledScope(!settings.showGameObjectIcon))
							settings.useCustomGameObjectIcon.DrawField("Use Custom GameObject Icon");
						settings.showTransformIcon.DrawField("Show Transform Icon");
						settings.showNonBehaviourIcons.DrawField("Show Non-Toggleable Icons");
						settings.alwaysShowIcons.DrawField("Always Render Icons");
						settings.linkCursorOnHover.DrawField("Link Cursor On Hover");
						settings.guiXOffset.value = EditorGUILayout.FloatField("Icons X Offset", settings.guiXOffset.value);
						using (new GUILayout.VerticalScope())
						{
							Foldout(new GUIContent("Hidden Types", "Hover over an icon to see its type name.\nWrite the type name here to hide the icon from the hierarchy view."), ref hiddenIconsFoldout);
							if (hiddenIconsFoldout)
							{
								using (new IndentScope())
								{
									for (int i = 0; i < settings.hiddenIconTypes.Length; i++)
									{
										using (new EditorGUILayout.HorizontalScope())
										{
											settings.hiddenIconTypes[i].DrawField(GUIContent.none);
											if (GUILayout.Button("X", EditorStyles.boldLabel, GUILayout.ExpandWidth(false)))
											{
												var arr = settings.hiddenIconTypes;
												ArrayUtility.RemoveAt(ref arr, i);
												settings.hiddenIconTypes = arr;
												SavedSettings.Save();
												i--;
											}

											MakeRectLinkCursor();
										}
									}

									if (GUILayout.Button("+", EditorStyles.toolbarButton))
									{
										var arr = settings.hiddenIconTypes;
										ArrayUtility.Add(ref arr, new SavedString(""));
										settings.hiddenIconTypes = arr;
										SavedSettings.Save();
									}

									MakeRectLinkCursor();
								}
							}
						}

						EditorGUIUtility.labelWidth = 0;
					}
				}
			}
			
			EditorGUILayout.EndScrollView();
			using (new GUILayout.HorizontalScope())
			{
				GUILayout.FlexibleSpace();
				w_Credit();
			}

			if (EditorGUI.EndChangeCheck())
				EditorApplication.RepaintHierarchyWindow();
		}
    
		private static void w_Credit()
        {
	        using (new ColoredScope(ColoredScope.ColoringType.BG, Color.clear))
	        {
		        if (GUILayout.Button(new GUIContent("Made By @Dreadrith ♡", "https://dreadrith.com/links"), Styles.faintLinkLabel))
			        Application.OpenURL("https://dreadrith.com/links");
		        w_UnderlineLastRectOnHover();
	        }
        }
        internal static void w_UnderlineLastRectOnHover(Color? color = null)
        {
	        if (color == null) color = new Color(0.3f, 0.7f, 1);
	        if (Event.current.type == EventType.Repaint)
	        {
		        Rect rect = GUILayoutUtility.GetLastRect();
		        Vector2 mp = Event.current.mousePosition;
		        if (rect.Contains(mp)) EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1, rect.width, 1), color.Value);
		        EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);
	        }

        }
        #endregion

        #region Hierarchy
        private static ColoredScope colorScope;
        private static ColoredScope colorScope2;
        private static ColoredScope colorScope3;

        private static void OnHierarchyItemGUI(int id, Rect rect)
        {
	        DisposeOfColorScopes();
	        if (settings.GetColorsEnabled())
	        {
		        colorScope = new ColoredScope(ColoredScope.ColoringType.General, settings.colorOneEnabled, settings.colorOne);
		        colorScope2 = new ColoredScope(ColoredScope.ColoringType.FG, settings.colorTwoEnabled, settings.colorTwo);
		        colorScope3 = new ColoredScope(ColoredScope.ColoringType.BG, settings.colorThreeEnabled, settings.colorThree);
	        }

	        bool willDrawColors = settings.GetColorsEnabled() && (settings.guideLinesEnabled || settings.GetRowColoringEnabled());
	        bool willDrawIcons = settings.GetIconsEnabled();

	        if (!willDrawColors && !willDrawIcons) return;

	        Object obj = EditorUtility.InstanceIDToObject(id);
	        if (!(obj is GameObject go)) return;


	        if (willDrawColors)
	        {
		        var t = go.transform;
		        var p = t.parent;
		        bool hasParent = p;
		        bool isLastChild = hasParent && t.GetSiblingIndex() == p.childCount - 1;
		        bool hasChildren = t.childCount > 0;

		        List<bool> middleLines = new List<bool>();
		        int depth = 0;

		        if (settings.guideLinesEnabled)
		        {
			        while (p)
			        {
				        middleLines.Insert(0, t.GetSiblingIndex() != p.childCount - 1);
				        depth++;
				        t = p;
				        p = p.parent;
			        }
		        }
		        else
			        while (p)
			        {
				        depth++;
				        t = p;
				        p = p.parent;
			        }

		        int marginWidth = hasChildren ? 14 : 2;
		        int lineWidth = 14 * depth + 34;
		        Rect lineRect = new Rect(rect.x - lineWidth, rect.y, lineWidth - marginWidth, rect.height);

		        if (settings.GetRowColoringEnabled() && Event.current.type == EventType.Repaint)
		        {
			        Rect backgroundRect = new Rect(lineRect);
			        backgroundRect.width += rect.width + marginWidth + 12;
			        backgroundRect.x += 5;
			        //backgroundRect.y += 16;



			        if (backgroundRect.y % 32 > 15)
			        {
				        if (settings.rowColoringOddEnabled)
					        EditorGUI.DrawRect(backgroundRect, settings.rowOddColor);
			        }
			        else if (settings.rowColoringEvenEnabled)
				        EditorGUI.DrawRect(backgroundRect, settings.rowEvenColor);
			        //GUI.depth = guiDepth;
		        }

		        if (hasParent && settings.guideLinesEnabled)
		        {
			        int extraWidth = hasChildren ? 0 : 12;

			        void Line(Vector3 start, Vector3 end) => Handles.DrawAAPolyLine(1, 2, start, end);

			        Handles.color = settings.guideLinesColor;

			        GUI.BeginClip(lineRect);
			        float basef = lineWidth - marginWidth;
			        Vector3 startingPoint = new Vector3(basef, rect.height / 2);
			        Vector3 middlePoint = new Vector3(basef - extraWidth - 8, rect.height / 2);
			        Line(startingPoint, middlePoint);

			        if (isLastChild)
			        {
				        Vector3 connectionPoint = new Vector3(basef - extraWidth - 8, 0);
				        Line(middlePoint, connectionPoint);
			        }

			        for (int i = 0; i < middleLines.Count; i++)
			        {
				        if (!middleLines[i]) continue;
				        float x = lineRect.x + 14 * i;
				        Vector3 topConnection = new Vector3(x, 0);
				        Vector3 bottomConnection = new Vector3(x, rect.height);
				        Line(topConnection, bottomConnection);
			        }

			        GUI.EndClip();
		        }
	        }

	        if (settings.GetIconsEnabled())
	        {
		        Rect iconRect = rect;
		        iconRect.x += settings.guiXOffset;
		        iconRect.width -= settings.guiXOffset;
		        iconRect.x = rect.xMax - 32 + settings.guiXOffset;
		        iconRect.width = 18;
		        //iconRect = DrawIcon(typeof(GameObject), iconRect);
		        float iconsAreaWidth = rect.width - 32 + settings.guiXOffset - go.name.Length * 6.1f;

		        bool CanDrawIcon(out bool drawBackground)
		        {
			        bool dotsOnly = iconsAreaWidth < 36;
			        bool overlapping = iconsAreaWidth < 18;
			        bool drawIcon = settings.alwaysShowIcons || (!dotsOnly && !overlapping);
			        drawBackground = drawIcon && settings.iconBackgroundColorEnabled && (overlapping || !settings.iconBackgroundOverlapOnly);
			        
			        if (!drawIcon && dotsOnly)
			        {
				        GUI.Label(iconRect, "...", EditorStyles.centeredGreyMiniLabel);
				        iconsAreaWidth -= 18;
				        return false;
			        }
			        
			        iconsAreaWidth -= 18;
			        return drawIcon;

		        }

		        if (settings.showGameObjectIcon && CanDrawIcon(out bool withBg))
			        iconRect = DrawIconToggle(iconRect, go, withBg);

		        bool isFirstComponent = true;
		        foreach (var c in go.GetComponents<Component>())
		        {
			        if (c != null)
			        {
				        if (!isFirstComponent && !settings.showNonBehaviourIcons)
					        if (!(c is Behaviour) && !(c is Renderer) && !(c is Collider))
						        continue;
			        
				        if (isFirstComponent)
				        {
					        isFirstComponent = false;
					        if (!settings.showTransformIcon) 
						        continue;
				        }

			        

				        if (!isFirstComponent && settings.hiddenIconTypes.Any(ss => ss.value == c.GetType().Name)) continue;
			        }

			        if (CanDrawIcon(out bool withBg2))
			        {
				        Rect nextRect = iconRect;
				        iconRect = DrawIconToggle(iconRect, c, withBg2);
				        Event e = Event.current;
				        if (settings.enableContextClick && e.type == EventType.MouseDown && e.button == 1 && nextRect.Contains(e.mousePosition))
				        {
					        var method = typeof(EditorUtility).GetMethod("DisplayObjectContextMenu", BindingFlags.Static | BindingFlags.NonPublic, null, new Type[] {typeof(Rect), typeof(Object[]), typeof(int)}, null);
					        method.Invoke(null, new object[] {nextRect, new Object[] {c == null ? c as MonoBehaviour : c}, 0});
					        e.Use();
				        }
			        }
		        }
	        }

        }

        private static void DisposeOfColorScopes()
        {
	        colorScope3?.Dispose();
	        colorScope2?.Dispose();
	        colorScope?.Dispose();
        }
        #endregion

        #region Drawing Helpers
        
        internal static void MakeRectLinkCursor(Rect rect = default)
        {
	        if (Event.current.type == EventType.Repaint)
	        {
		        if (rect == default) rect = GUILayoutUtility.GetLastRect();
		        EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);
	        }
        }

        private static bool Foldout(GUIContent label, ref bool b)
        {
	        return b = EditorGUILayout.Foldout(b, label, true);
        }

        private static bool Foldout(string label, ref bool b) => Foldout(new GUIContent(label), ref b);

        private static bool DrawFoldoutTitle(string label, bool foldout, SavedBool enabled)
        {
	        using (new GUILayout.HorizontalScope())
	        {
		        var r = EditorGUILayout.GetControlRect(false, 24, Styles.bigTitle, GUILayout.ExpandWidth(true));
		        GUI.Label(r, label, EditorStyles.whiteLargeLabel);

		        if (enabled != null)
		        {
			        enabled.DrawToggle("Enabled", "Disabled", null, pastelGreenColor, pastelRedColor, GUILayout.ExpandWidth(false));
			        MakeRectLinkCursor();
		        }

		        if (LeftClicked(r)) foldout = !foldout;
		        MakeRectLinkCursor(r);
	        }

	        return foldout;
        }
        private static void DrawColorSetting(string label, SavedColor color, SavedBool toggle = null)
        {
	        using (new GUILayout.HorizontalScope())
	        {
		        if (toggle != null)
		        {
			        GUIContent toggleTooltip = toggle ? new GUIContent("","Enabled") : new GUIContent("","Disabled");
			        toggle.DrawToggle(toggleTooltip, null, EditorStyles.radioButton, pastelGreenColor, Color.grey, GUILayout.Width(18), GUILayout.Height(18));
			        var r = GUILayoutUtility.GetLastRect();
			        EditorGUIUtility.AddCursorRect(r, MouseCursor.Link);
		        } else using (new EditorGUI.DisabledScope(true))
			        GUILayout.Toggle(true, " ", EditorStyles.radioButton, GUILayout.Width(18), GUILayout.Height(18));
		        
		        EditorGUILayout.PrefixLabel(label);
		        color.DrawField(GUIContent.none);
	        }
        }

        private static Rect DrawIconToggle(Rect rect, GameObject go, bool withBackground)
        {
	        bool newState = !go.activeSelf;
	        bool leftClicked = LeftClicked(rect);
	        if (leftClicked || MouseDraggedOver(rect, go))
	        {
		        if (leftClicked) StartDragToggling(newState);
		        Undo.RecordObject(go, "[H+] Toggle GameObject");
		        go.SetActive(dragToggleNewState);
		        EditorUtility.SetDirty(go);
		        dragToggledObjects.Add(go);
	        }

	        return DrawIcon(rect, go, newState, withBackground);
        }

        private static Rect DrawIconToggle(Rect rect, Component c, bool withBackgroun)
        {
	        bool newState = !IsComponentEnabled(c);
	        if (!IsComponentToggleable(c)) return DrawIcon(rect, c, newState, withBackgroun);
	        
	        bool leftClicked = LeftClicked(rect);
	        if (leftClicked || MouseDraggedOver(rect, c))
	        {
		        if (leftClicked) StartDragToggling(newState);
		        Undo.RecordObject(c, "[H+] Toggle Component");
		        SetComponentEnabled(c, dragToggleNewState);
		        EditorUtility.SetDirty(c);
		        dragToggledObjects.Add(c);
	        }

	        return DrawIcon(rect, c, newState, withBackgroun);
        }
        private static Rect DrawIcon(Rect rect, Component c, bool faded, bool withBackground) => DrawIcon(GetIcon(c), rect, faded, withBackground);

        private static Rect DrawIcon(Rect rect, GameObject go, bool faded, bool withBackground)
        {
	        GUIContent goContent = gameObjectContent;
	        if (settings.useCustomGameObjectIcon && GetGameObjectIconMethod != null)
	        {
		        Texture2D icon = GetGameObjectIconMethod.Invoke(null, new object[] { go }) as Texture2D;
		        if (icon != null) goContent = new GUIContent(goContent){image = icon};
	        }
	        return DrawIcon(goContent, rect, faded, withBackground);
        }
        
        private static Rect DrawIcon(GUIContent content, Rect rect, bool faded, bool withBackground)
        {
	        using (new ColoredScope(ColoredScope.ColoringType.All, faded, settings.iconFadedTintColor, settings.iconTintColor))
	        {
		        if (withBackground) EditorGUI.DrawRect(rect, settings.iconBackgroundColor);
		        GUI.Label(rect, content);
		        if (settings.linkCursorOnHover)
			        MakeRectLinkCursor(rect);
	        }
            rect.x -= 18;
            return rect;
        }

        private static GUIContent GetIcon(Component c)
        {
            if (c == null) return missingScriptContent;
            Type type = c.GetType();
            if (customIconCache.TryGetValue(type.Name, out var contentIcon)) return contentIcon;
            if (iconCache.TryGetValue(type, out contentIcon)) return contentIcon;
            
            
            Texture2D icon = AssetPreview.GetMiniThumbnail(c);
            if (!icon || defaultTextures.Any(t => icon == t))
            {
                defaultContent.tooltip = type.Name;
                return defaultContent;
            }

	        contentIcon = new GUIContent(icon, type.Name);
            iconCache.Add(type, contentIcon);
            return contentIcon;
        }
        
        internal static Texture2D GenerateColorTexture(float r, float g, float b, float a = 1)
        {
	        if (r > 1) r /= 255;
	        if (g > 1) g /= 255;
	        if (b > 1) b /= 255;
	        if (a > 1) a /= 255;

	        return GenerateColorTexture(new Color(r, g, b, a));
        }

        private static Texture2D _temporaryTexture;
        
        internal static Texture2D GenerateColorTexture(Color color)
        {
	        if (_temporaryTexture == null)
	        {
		        _temporaryTexture = new Texture2D(1, 1);
		        _temporaryTexture.anisoLevel = 0;
		        _temporaryTexture.filterMode = FilterMode.Point;
	        }
	        
	        _temporaryTexture.SetPixel(0, 0, color);
	        _temporaryTexture.Apply();
	        return _temporaryTexture;
        }
        #endregion
        
        #region Functional Helpers
        private static bool IsComponentToggleable(Component c) => c is Behaviour || c is Renderer || c is Collider;

        private static bool IsComponentEnabled(Component c)
        {
	        if (!IsComponentToggleable(c)) return true;
	        dynamic d = c;
	        return d.enabled;
        }

        private static void SetComponentEnabled(Component c, bool enabled)
        {
	        if (!IsComponentToggleable(c)) return;
	        dynamic d = c;
	        d.enabled = enabled;
        }

        private static int GetDepth(GameObject go) => GetDepth(go.transform);
        private static int GetDepth(Transform t)
        {
	        int depth = 0;
	        while (t.parent != null)
	        {
		        depth++;
		        t = t.parent;
	        }

	        return depth;
        }
        private static bool LeftClicked(Rect rect)
        {
            var e = Event.current;
            bool clicked = e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition);
            if (clicked) e.Use();
            return clicked;
        }
        private static bool RightClicked(Rect rect)
        {
            var e = Event.current;
            bool clicked = e.type == EventType.MouseDown && e.button == 1 && rect.Contains(e.mousePosition);
            if (clicked) e.Use();
            return clicked;
        }

        private static bool MouseDraggedOver(Rect rect, Object o)
        {
	        var e = Event.current;
	        return GUIUtility.hotControl == DRAG_TOGGLE_HOT_CONTROL_ID && e.type != EventType.Layout && rect.Contains(e.mousePosition) && !dragToggledObjects.Contains(o);
        }

        private static void StartDragToggling(bool toggleToState)
        {
	        dragToggledObjects.Clear();
	        dragToggleNewState = toggleToState;
	        if (settings.enableDragToggle) 
		        GUIUtility.hotControl = DRAG_TOGGLE_HOT_CONTROL_ID;
        }
        #endregion

        #region Initialization


        [InitializeOnLoadMethod]
        private static void InitializeGUI()
        {
            InitializeAll();
            EditorApplication.hierarchyWindowItemOnGUI -= OnHierarchyItemGUI;
            EditorApplication.hierarchyWindowItemOnGUI = OnHierarchyItemGUI + EditorApplication.hierarchyWindowItemOnGUI;
        }
        
        private static void InitializeAll()
        {
	        iconCache.Clear();
            InitializeIconFolderPath();
            InitializeCustomIcons();
            InitializeSpecialIcons();
        }

        private static void InitializeIconFolderPath()
        {
            iconFolderPath = string.Empty;
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(assembly);
            if (packageInfo != null)
            {
                var packagePath = packageInfo.assetPath;
                iconFolderPath = $"{packagePath}/{PACKAGE_ICON_FOLDER_PATH}";
                
                if (!AssetDatabase.IsValidFolder(iconFolderPath))
                {
                    CustomLog($"Custom Icon folder couldn't be found in {iconFolderPath}. Custom Icons are disabled.");
                    iconFolderPath = string.Empty;
                }
            } else CustomLog("Couldn't get package info for HierarchyPlus. Custom Icons are disabled. Is the script in Packages?", CustomLogType.Warning);
        }
        
        private static void InitializeSpecialIcons()
        {
	        GetGameObjectIconMethod = typeof(EditorGUIUtility).GetMethod("GetIconForObject", BindingFlags.NonPublic | BindingFlags.Static );
	        if (GetGameObjectIconMethod == null)
		        GetGameObjectIconMethod = typeof(EditorGUIUtility).GetMethod("GetIconForObject", BindingFlags.Public | BindingFlags.Static );
	        
	        defaultTextures[0] = EditorGUIUtility.IconContent("cs Script Icon")?.image as Texture2D;
	        defaultTextures[1] = EditorGUIUtility.IconContent("d_cs Script Icon")?.image as Texture2D;
	        defaultTextures[2] = EditorGUIUtility.IconContent("dll Script Icon")?.image as Texture2D;

	        if (!customIconCache.TryGetValue("GameObject", out gameObjectContent))
                gameObjectContent = new GUIContent(AssetPreview.GetMiniTypeThumbnail(typeof(GameObject)));
	        gameObjectContent.tooltip = "GameObject";
            
            if (!customIconCache.TryGetValue(DEFAULT_ICON_NAME, out defaultContent))
                defaultContent = new GUIContent(AssetPreview.GetMiniTypeThumbnail(typeof(MonoScript)));
            
            string missingTooltip = "Missing Script";
            if (customIconCache.TryGetValue(MISSING_SCRIPT_ICON_NAME, out var value))
                missingScriptContent = new GUIContent(value){tooltip = missingTooltip};
            else missingScriptContent = new GUIContent(defaultContent){tooltip = missingTooltip};
        }
        private static void InitializeCustomIcons()
        {            
            customIconCache.Clear();
            if (string.IsNullOrWhiteSpace(iconFolderPath)) return;
            var paths = Directory.GetFiles(iconFolderPath, "*", SearchOption.AllDirectories).Where(p => !p.EndsWith(".meta"));
            foreach (var p in paths)
            {
	            Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
                if (icon != null) customIconCache.Add(icon.name, new GUIContent(icon, icon.name));
            }
            
        }
        #endregion

        #region Logging
        internal static Color pastelBlueColor = new Color(0.5f, 0.8f, 1);
        internal static Color pastelGreenColor = new Color(0.56f, 0.94f, 0.47f);
        internal static Color pastelRedColor = new Color(1, 0.25f, 0.25f);
        internal static Color pastelYellowColor = new Color(0.99f, 0.95f, 0, 6f);
        internal static Color linkColor = new Color(0.3f, 0.7f, 1);

        internal static bool CustomLog(string message, CustomLogType type = CustomLogType.Regular, bool condition = true)
        {
            if (condition)
            {
                Color finalColor = type == CustomLogType.Regular ? pastelGreenColor : type == CustomLogType.Warning ? pastelYellowColor : pastelRedColor;
                string fullMessage = $"<color=#{ColorUtility.ToHtmlStringRGB(finalColor)}>[{PRODUCT_NAME}]</color> {message.Replace("\\n", "\n")}";
                switch (type)
                {
                    case CustomLogType.Regular:
                        Debug.Log(fullMessage); break;
                    case CustomLogType.Warning:
                        Debug.LogWarning(fullMessage); break;
                    case CustomLogType.Error:
                        Debug.LogError(fullMessage); break;
                }

            }
            return condition;
        }
        internal enum CustomLogType
        {
            Regular,
            Warning,
            Error
        }
        #endregion
    }
}
