using HarmonyLib;
using Godot;
using MegaCrit.Sts2.Core.Nodes.TopBar;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Rooms;

namespace CardRarityPreview.CardRarityPreviewCode;

[HarmonyPatch(typeof(NTopBarDeckButton))]
public static class DeckButtonHoverPatch
{
    [HarmonyPatch("OnFocus")]
    [HarmonyPostfix]
    public static void OnFocusPostfix(NTopBarDeckButton __instance)
    {
        try
        {
            NHoverTipSet.Remove(__instance);

            var hoverTips = BuildCardOddsHoverTips();
            if (hoverTips.Count == 0)
                return;

            var tipSet = NHoverTipSet.CreateAndShow(__instance, hoverTips);
            tipSet?.SetGlobalPosition(
                __instance.GlobalPosition +
                new Vector2(__instance.Size.X - tipSet.Size.X, __instance.Size.Y + 20f));
        }
        catch (System.Exception ex)
        {
            MainFile.Logger.Error($"Error in NTopBarDeckButton.OnFocus postfix: {ex.Message}");
        }
    }

    private static List<IHoverTip> BuildCardOddsHoverTips()
    {
        var hoverTips = new List<IHoverTip>();

        // Figure out which reward context we're in based on the current room.
        // The tooltip fires from the top bar, so the current room is the one
        // whose reward will be rolled next (once combat ends).
        var runState = RunManager.Instance?.DebugOnlyGetState();
        var context = DetermineContext(runState);

        var chances = MainFile.GetChances(context);
        if (chances == null)
        {
            hoverTips.Add(new HoverTip(
                new LocString("static_hover_tips", "CARD_ODDS-NO_DATA.title"),
                new LocString("static_hover_tips", "CARD_ODDS-NO_DATA.description")
            ));
            return hoverTips;
        }

        var text = new System.Text.StringBuilder();
        text.AppendLine("[table=2]");
        text.AppendLine($"[cell][color=#cccccc]Common[/color][/cell][cell]{chances.Value.Common * 100f,3:F0}%[/cell]");
        text.AppendLine($"[cell][color=#5599ff]Uncommon[/color][/cell][cell]{chances.Value.Uncommon * 100f,3:F0}%[/cell]");
        text.AppendLine($"[cell][color=#ffcc44]Rare[/color][/cell][cell]{chances.Value.Rare * 100f,3:F0}%[/cell]");
        text.AppendLine("[/table]");
        var desc = new LocString("static_hover_tips", "CARD_ODDS-POOL.description");
        desc.Add("OddsList", text.ToString());

        hoverTips.Add(new HoverTip(
            new LocString("static_hover_tips", "CARD_ODDS-POOL.title"),
            desc
        ));

        return hoverTips;
    }

    /// <summary>
    /// Picks which CardRarityOddsType applies to the current room.
    /// Falls back to RegularEncounter when the room isn't a card-reward source.
    /// </summary>
    private static CardRarityOddsType DetermineContext(RunState? runState)
    {
        if (runState == null) return CardRarityOddsType.RegularEncounter;

        var room = runState.CurrentRoom;
        if (room == null) return CardRarityOddsType.RegularEncounter;

        // CombatRoom covers Monster, Elite and Boss. RoomType tells us which.
        if (room is CombatRoom combatRoom)
        {
            return combatRoom.Encounter.RoomType switch
            {
                RoomType.Elite => CardRarityOddsType.EliteEncounter,
                RoomType.Boss  => CardRarityOddsType.BossEncounter,
                _              => CardRarityOddsType.RegularEncounter,
            };
        }
        if (room.RoomType == RoomType.Shop)
            return CardRarityOddsType.Shop;

        return CardRarityOddsType.RegularEncounter;
    }
}