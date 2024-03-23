using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace DreadScripts.HierarchyPlus
{
	[Serializable]
	internal class RegexComparison
	{
		internal readonly GUIContent fontIcon = new GUIContent(EditorGUIUtility.IconContent("Font Icon")) {tooltip = "Case Sensitive"};
		internal readonly GUIContent regexIcon = new GUIContent(EditorGUIUtility.IconContent("d_PreTexR@2x")) {tooltip = "Regex Mode"};

		[SerializeField] internal ComparisonType _comparisonType = ComparisonType.Contains;
		[SerializeField] internal bool caseSensitive;
		[SerializeField] internal string comparisonPattern;

		internal ComparisonType comparisonType
		{
			get => _comparisonType;
			set
			{
				if (_comparisonType == value) return;
				if (value == ComparisonType.Regex)
					comparisonPattern = GetFinalPattern();

				_comparisonType = value;
			}
		}

		internal enum ComparisonType
		{
			Contains = 0,
			StartsWith = 1 << 0,
			EndsWith = 1 << 1,
			EqualsTo = 3,
			Regex = 4,
		}

		internal bool IsMatch(string input) => !string.IsNullOrEmpty(comparisonPattern) && Regex.IsMatch(input, GetFinalPattern());

		internal bool[] IsMatch(IEnumerable<string> inputs)
		{
			var enumerable = inputs as string[] ?? inputs.ToArray();
			bool[] results = new bool[enumerable.Length];
			if (string.IsNullOrEmpty(comparisonPattern)) return results;

			string pattern = GetFinalPattern();
			for (int i = 0; i < enumerable.Length; i++)
				results[i] = Regex.IsMatch(enumerable[i], pattern);

			return results;
		}

		internal string GetFinalPattern()
		{
			if (comparisonType == ComparisonType.Regex) return comparisonPattern;

			StringBuilder finalPattern = new StringBuilder();
			if (((int) comparisonType & 1) > 0) finalPattern.Append('^');
			if (!caseSensitive) finalPattern.Append("(?i)");
			finalPattern.Append(Regex.Escape(comparisonPattern));
			if (((int) comparisonType & 2) > 0) finalPattern.Append('$');
			return finalPattern.ToString();
		}

		internal bool isValid => !string.IsNullOrEmpty(comparisonPattern) && IsValidRegex(GetFinalPattern());

		internal static bool IsValidRegex(string pattern)
		{
			if (string.IsNullOrEmpty(pattern)) return false;

			try
			{
				Regex.Match(string.Empty, pattern);
			}
			catch
			{
				return false;
			}

			return true;
		}

		internal void ExitRegexComparison()
		{
			bool starts = comparisonPattern.StartsWith("^");
			bool ends = comparisonPattern.EndsWith("$");
			if (starts && ends) _comparisonType = ComparisonType.EqualsTo;
			else if (starts) _comparisonType = ComparisonType.StartsWith;
			else if (ends) _comparisonType = ComparisonType.EndsWith;
			else _comparisonType = ComparisonType.Contains;
			if (starts) comparisonPattern = comparisonPattern.Substring(1);
			if (ends) comparisonPattern = comparisonPattern.Substring(0, comparisonPattern.Length - 1);

			var temp = comparisonPattern;
			comparisonPattern = comparisonPattern.Replace("(?i)", "");
			if (temp == comparisonPattern)
				caseSensitive = true;
		}
	}
}
