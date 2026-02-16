using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class QueenUI : MonoBehaviour
{
    [Header("References")]
    public QueenAntScript queen;
    public EvolutionManagerScript evo;

    [Header("UI")]
    public Slider healthSlider;

    public TMP_Text generationText;
    public TMP_Text countdownText;
    public TMP_Text queenHealthText;
    public TMP_Text blocksPlacedText;
    public TMP_Text averagesText;

    void Awake()
    {
        TryFindQueen();
        TryFindEvo();
    }

    void Update()
    {
        if (queen == null) TryFindQueen();
        if (evo == null) TryFindEvo();

        // ---- Health bar ----
        if (queen != null && healthSlider != null)
        {
            float t = (queen.maxHealth <= 0f) ? 0f : (queen.health / queen.maxHealth);
            healthSlider.value = t;
        }

        // ---- Generation number ----
        if (generationText != null)
        {
            int gen = (evo != null) ? evo.CurrentGeneration : 0;
            generationText.text = $"Generation: {gen}";
        }

        // ---- Countdown ----
        if (countdownText != null)
        {
            float secs = (evo != null) ? evo.SecondsRemaining : 0f;
            countdownText.text = $"Time Left: {secs:0.0}s";
        }

        // ---- Queen health text ----
        if (queenHealthText != null)
        {
            float h = (queen != null) ? queen.health : 0f;
            float mh = (queen != null) ? queen.maxHealth : 0f;

            queenHealthText.text = $"Queen Health: {h:0}/{mh:0}";
        }

        // ---- Blocks placed ----
        if (blocksPlacedText != null)
        {
            int blocks = (queen != null) ? queen.BlocksPlacedThisGeneration : 0;
            blocksPlacedText.text = $"Blocks Placed: {blocks}";
        }
        if (averagesText != null)
        {
            if (evo == null)
                averagesText.text = "Averages: (no evo manager)";
            else
                averagesText.text =
                    "Averages\n" +
                    $"MoveSpeed: {evo.AvgMoveSpeed:0.00}\n" +
                    $"TurnChance: {evo.AvgTurnChance:0.00}\n" +
                    $"Pause: {evo.AvgPauseDuration:0.00}\n" +
                    $"SearchRadius: {evo.AvgSearchRadius:0.00}\n" +
                    $"Turn2Block: {evo.AvgTurnChanceTwoBlocks:0.00}\n" +
                    $"AvoidAcid: {evo.AvgAvoidAcid:0.00}\n" +
                    $"AcidSenseRadius: {evo.AvgAcidSenseRadius:0.00}\n" +
                    $"DigProb: {evo.AvgDigProbability:0.00}";
        }


    }

    private void TryFindQueen()
    {
        var q = GameObject.FindWithTag("Queen");
        if (q == null) return;

        queen = q.GetComponentInChildren<QueenAntScript>();
    }

    private void TryFindEvo()
    {
        evo = FindFirstObjectByType<EvolutionManagerScript>();
    }
}
