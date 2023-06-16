/**
 * This file is part of the NAF-Extension, an editor extension for Unity3d.
 *
 * @link   NAF-URL
 * @author Nevin Foster
 * @since  14.06.23
 */

using UnityEditor;
using UnityEngine;
using Pnak;

namespace PnakEditor
{
	[CustomPropertyDrawer(typeof(AsLabelAttribute))]
	public class AsLabelAttributePropertyDrawer : PropertyDrawer
	{
		private string GetText(SerializedProperty property)
		{
			string format = (attribute as AsLabelAttribute).Format ?? "{0}";
			return string.Format(format, ExpressionEvaluator.GetPropertyValue(property));
		}

		private GUIContent GetContent(SerializedProperty property, out GUIStyle style, ref Vector2 size)
		{
			GUIContent content = new GUIContent(GetText(property));
			style = GetGUIStyle();
			
			size = style.CalcSize(content);
			return content;
		}

		private GUIStyle GetGUIStyle()
		{
			AsLabelAttribute asLabelAttribute = attribute as AsLabelAttribute;

			GUIStyle style = new GUIStyle(GUI.skin.label);
			style.wordWrap = true;

			if (asLabelAttribute.Type.HasFlag(LabelType.Bold))
				style.fontStyle = FontStyle.Bold;

			if (asLabelAttribute.Type.HasFlag(LabelType.Italic))
				style.fontStyle = FontStyle.Italic;

			if (asLabelAttribute.Type.HasFlag(LabelType.Mini))
				style.fontSize = 9;

			if (asLabelAttribute.Type.HasFlag(LabelType.Left))
				style.alignment = TextAnchor.MiddleLeft;
			
			if (asLabelAttribute.Type.HasFlag(LabelType.Center))
				style.alignment = TextAnchor.MiddleCenter;

			if (asLabelAttribute.Type.HasFlag(LabelType.Right))
				style.alignment = TextAnchor.MiddleRight;

			return style;
		}

		public override float GetPropertyHeight(SerializedProperty property,
												GUIContent label)
		{
			AsLabelAttribute asLabelAttribute = attribute as AsLabelAttribute;

			Vector2 size = Vector2.zero;
			size.x = EditorGUIUtility.currentViewWidth;
			GetContent(property, out GUIStyle style, ref size);
			return size.y;
		}

		public override void OnGUI(Rect position,
								SerializedProperty property,
								GUIContent label)
		{
			AsLabelAttribute asLabelAttribute = attribute as AsLabelAttribute;

			Vector2 size = position.width * Vector2.one;
			GUIContent content = GetContent(property, out GUIStyle style, ref size);
			EditorGUI.LabelField(position, content, style);
		}
	}
}