using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Odds;
using MegaCrit.Sts2.Core.Context;

namespace CardRarityPreview.CardRarityPreviewCode;

[ModInitializer(nameof(Initialize))]
public partial class MainFile : Node
{
    public const string ModId = "CardRarityPreview";

    public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } =
        new(ModId, MegaCrit.Sts2.Core.Logging.LogType.Generic);

    public static void Initialize()
    {
        var assembly = Assembly.GetExecutingAssembly();
        Harmony harmony = new(ModId);
        harmony.PatchAll();
    }

    public static CardRarityOdds? GetCardRarityOdds()
    {
        var runState = RunManager.Instance?.DebugOnlyGetState();
        if (runState == null) return null;

        var players = runState.Players;
        if (players == null || players.Count == 0) return null;

        var me = LocalContext.GetMe(players);
        if (me == null) return null;

        return me.PlayerOdds?.CardRarity;
    }

    /// <summary>
    /// Computes the actual probability of each rarity for a given
    /// card-source context (Regular, Elite, Shop, Boss, Uniform).
    ///
    /// Reproduces the exact logic from CardRarityOdds.RollWithoutChangingFutureOdds
    /// without consuming an RNG roll or mutating state.
    /// </summary>
    public static RarityChances? GetChances(CardRarityOddsType type)
    {
        var odds = GetCardRarityOdds();
        if (odds == null) return null;

        float offset = odds.CurrentValue;

        // Base odds per source type, mirroring CardRarityOdds.GetBaseOdds.
        // Values pulled directly from the static properties on CardRarityOdds
        // so ascension scaling (Scarcity) is handled automatically.
        float baseRare, baseUncommon;
        switch (type)
        {
            case CardRarityOddsType.RegularEncounter:
                baseRare = CardRarityOdds.RegularRareOdds;
                baseUncommon = 0.37f;
                break;
            case CardRarityOddsType.EliteEncounter:
                baseRare = CardRarityOdds.EliteRareOdds;
                baseUncommon = 0.40f;
                break;
            case CardRarityOddsType.BossEncounter:
                baseRare = 1f;
                baseUncommon = 0f;
                break;
            case CardRarityOddsType.Shop: 
                baseRare = CardRarityOdds.ShopRareOdds;
                baseUncommon = 0.37f;
                break;
            case CardRarityOddsType.Uniform:
                baseRare = 0.33f;
                baseUncommon = 0.33f;
                break;
            default:
                return null;
        }

        // Game logic:
        //   rareThreshold = baseRare + offset
        //   roll < rareThreshold                 -> Rare
        //   roll < baseUncommon + rareThreshold  -> Uncommon
        //   otherwise                            -> Common
        //
        // Since roll is uniform in [0,1), probabilities are:
        //   P(Rare)     = max(0, baseRare + offset)
        //   P(Uncommon) = baseUncommon (fixed width)
        //   P(Common)   = 1 - P(Rare) - P(Uncommon)
        float rare = System.Math.Max(0f, baseRare + offset);
        float uncommon = baseUncommon;
        float common = System.Math.Max(0f, 1f - rare - uncommon);

        return new RarityChances(common, uncommon, rare);
    }
}

/// <summary>
/// Simple DTO for displaying computed rarity probabilities.
/// </summary>
public readonly struct RarityChances(float common, float uncommon, float rare)
{
    public float Common { get; } = common;
    public float Uncommon { get; } = uncommon;
    public float Rare { get; } = rare;
}