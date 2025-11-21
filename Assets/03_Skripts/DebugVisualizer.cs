using Assets.Skripts;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class DebugVisualizer : MonoBehaviour
{
    [Header("GUI Settings")]
    [SerializeField, Tooltip("Toggle to enable/disable debug HUD overlay.")]
    private bool showDebugHud = true;

    [SerializeField, Tooltip("Screen position for the HUD block.")]
    private Vector2 hudPosition = new Vector2(20f, 20f);

    [SerializeField, Tooltip("Width of the debug panel.")]
    private float panelWidth = 260f;

    [SerializeField, Tooltip("Base color of the GUI background.")]
    private Color panelColor = new Color(0f, 0f, 0f, 0.5f);


    private GUIStyle labelStyle;
    private MLAgentController mLAgentController;
    private MovementController movementController;




    private void Start()
    {
        mLAgentController = GetComponent<MLAgentController>();
        movementController = GetComponent<MovementController>();
    }

    private void OnGUI()
    {
        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                normal = { textColor = Color.white }
            };
        }
        if (!showDebugHud)
        {
            return;
        }


        StringBuilder builder = new StringBuilder();

        if (mLAgentController != null)
        {
            builder.AppendLine("##  ML Agent Steuerung ##"); // Haupttitel
            AppendDebugInfo(builder, mLAgentController.GetDebugInformations());
        }

        if (movementController != null)
        {
            builder.AppendLine("\n##  Bewegungs-Controller ##"); // Haupttitel
            AppendDebugInfo(builder, movementController.GetDebugInformations());
        }


        float height = labelStyle.CalcHeight(new GUIContent(builder.ToString()), panelWidth);
        Vector2 size = new Vector2(panelWidth, height + 22f);

        Rect rect = new Rect(hudPosition, size);
        Color oldColor = GUI.color;
        GUI.color = panelColor;
        GUI.Box(rect, GUIContent.none);
        GUI.color = oldColor;
        GUI.Label(new Rect(rect.x + 10f, rect.y + 6f, rect.width - 20f, rect.height - 12f), builder.ToString(), labelStyle);
    }

    private void AppendDebugInfo(StringBuilder builder, Dictionary<string, string> debugInfo)
    {
        // Iteriere durch alle Einträge und füge sie dem Builder hinzu
        foreach (var kvp in debugInfo)
        {
            if (kvp.Key.StartsWith("---"))
            {
                // Hervorhebung für Sektionen wie "--- GABEL ---"
                builder.AppendLine($"\n**{kvp.Key.Replace("-", "").Trim()}**");
            }
            else
            {
                // Standard-Debug-Eintrag
                builder.AppendLine($"{kvp.Key}: **{kvp.Value}**");
            }
        }
    }

}
