using System.Linq;
using System.Text;
using UnityEngine;
using TMPro;

public class GenomeDebugUIScript : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text outputText;

    [Header("Refresh")]
    public float refreshSeconds = 0.25f;
    public int maxRows = 25;

    private float nextRefresh;

    void Update()
    {
        if (outputText == null) return;
        if (Time.time < nextRefresh) return;
        nextRefresh = Time.time + refreshSeconds;

        var ants = FindObjectsByType<WorkerAntScript>(FindObjectsSortMode.None)
            .Where(a => a != null)
            .OrderByDescending(a => a.Fitness)
            .Take(maxRows)
            .ToList();

        var sb = new StringBuilder(4096);

        sb.AppendLine($"GENOME TABLE  (top {maxRows})   t={Time.time:0.0}");
        sb.AppendLine(
            "Idx  Name               Fit     HP    ms   turn  pause  rad  wallT  acidAv  acidR"
        );
        sb.AppendLine(
            "---- ------------------ ------- ----- ----- ----- ----- ---- ------ ------- -----"
        );

        if (ants.Count == 0)
        {
            sb.AppendLine("No WorkerAntScript found.");
            outputText.text = sb.ToString();
            return;
        }

        for (int i = 0; i < ants.Count; i++)
        {
            var a = ants[i];
            var g = a.Genome;

            // keep name from breaking table width
            string n = a.name;
            if (n.Length > 18) n = n.Substring(0, 18);

            sb.AppendLine(string.Format(
                "{0,3}  {1,-18}  {2,7:0.00} {3,5:0.0}  {4,4:0.00} {5,5:0.00} {6,5:0.00} {7,4} {8,6:0.00} {9,7:0.00} {10,5}",
                i + 1,
                n,
                a.Fitness,
                a.health,
                g.moveSpeed,
                g.turnChance,
                g.pauseDuration,
                g.searchRadius,
                g.turnChanceTwoBlocks,
                g.avoidAcid,
                g.acidSenseRadius
            ));
        }

        outputText.text = sb.ToString();
    }
}
