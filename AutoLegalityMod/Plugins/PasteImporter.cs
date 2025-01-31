using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using AutoModPlugins.Properties;
using PKHeX.Core;
using PKHeX.Core.AutoMod;
using PKHeX.Core.Enhancements;

namespace AutoModPlugins;

/// <summary>
/// Main Plugin with clipboard import calls
/// </summary>
public class PasteImporter : AutoModPlugin
{
    // TODO: Check for Auto-Legality Mod Updates
    public override string Name => "Import with Auto-Legality Mod";
    public override int Priority => 0;

    protected override void AddPluginControl(ToolStripDropDownItem modmenu)
    {
        var ctrl = new ToolStripMenuItem(Name)
        {
            Image = Resources.autolegalitymod,
            ShortcutKeys = Keys.Control | Keys.I,
        };
        ctrl.Click += ImportPaste;
        ctrl.Name = "Menu_PasteImporter";
        modmenu.DropDownItems.Add(ctrl);
        ToolStripItem parent = modmenu.OwnerItem?? ctrl;
        var currparent = parent.GetCurrentParent()??throw new Exception("Parent not found");
        var form = (currparent.Parent ?? throw new Exception("Parent not found")).FindForm() ?? throw new Exception("Form not found");
        form.Icon = Resources.icon;
        form.KeyDown += Downkey;
        ShowdownSetLoader.PKMEditor = PKMEditor;
        ShowdownSetLoader.SaveFileEditor = SaveFileEditor;
    }

    private void Downkey(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode is not (Keys.NumPad6 or Keys.D6 or Keys.E) || !e.Control)
            return;

        if (WinFormsUtil.Prompt(MessageBoxButtons.OKCancel, "Generate 6 Random Pokemon?") != DialogResult.OK)
            return;
        PKM[] result = [];
        var sav = SaveFileEditor.SAV;
        if (e.KeyCode is not Keys.E)
        {
            APILegality.RandTypes = _settings.RandomTypes;
            APILegality.RandTypes = _settings.RandomTypes;
            result = sav.GetSixRandomMons();
        }
        else
        {
            var text = GetTextShowdownData();
            if (string.IsNullOrWhiteSpace(text))
                return;
            var made = sav.GenerateEgg(new ShowdownSet(text), out var legalized);
            result.Append(made);
            PKMEditor.PopulateFields(made);
        }
            

        const int slotIndexStart = 0;
        var slot = slotIndexStart - 1;
        foreach (var pk in result)
        {
            slot = sav.NextOpenBoxSlot(slot);
            if (slot == -1)
                break;
            sav.SetBoxSlotAtIndex(pk, slot);
        }
        SaveFileEditor.ReloadSlots();
    }

    private static void ImportPaste(object? sender, EventArgs e)
    {
        // Check for showdown data in clipboard
        var text = GetTextShowdownData();
        if (string.IsNullOrWhiteSpace(text))
            return;

        ShowdownSetLoader.Import(text);
    }

    /// <summary>
    /// Check whether the showdown text is supposed to be loaded via a text file. If so, set the clipboard to its contents.
    /// </summary>
    /// <returns>output boolean that tells if the data provided is valid or not</returns>
    private static string? GetTextShowdownData()
    {
        bool skipClipboardCheck = (Control.ModifierKeys & Keys.Shift) == Keys.Shift;
        if (!skipClipboardCheck && Clipboard.ContainsText())
        {
            var txt = Clipboard.GetText();
            if (ShowdownUtil.IsTextShowdownData(txt))
                return txt;
        }
        if (!WinFormsUtil.OpenSAVPKMDialog(["txt"], out var path))
        {
            WinFormsUtil.Alert("No data provided.");
            return null;
        }

        if (path == null)
        {
            WinFormsUtil.Alert("Path invalid.");
            return null;
        }

        var text = File.ReadAllText(path).TrimEnd();
        if (ShowdownUtil.IsTextShowdownData(text))
        {
            return text;
        }

        WinFormsUtil.Alert("Text file with invalid data provided. Please provide a text file with proper Showdown data");
        return null;
    }

    public static int? GetFormSpecificItem(int game, int species, int form)
    {
        if (game == (int)GameVersion.PLA)
            return null;

        var generation = ((GameVersion)game).GetGeneration();
        return species switch
        {
            (ushort)Species.Arceus => generation != 4 || form < 9 ? SimpleEdits.GetArceusHeldItemFromForm(form) : SimpleEdits.GetArceusHeldItemFromForm(form - 1),
            (ushort)Species.Silvally => SimpleEdits.GetSilvallyHeldItemFromForm(form),
            (ushort)Species.Genesect => SimpleEdits.GetGenesectHeldItemFromForm(form),
            (ushort)Species.Giratina => form == 1 && generation < 9 ? 112 : form == 1 ? 1779 : null, // Griseous Orb
            (ushort)Species.Zacian => form == 1 ? 1103 : null, // Rusted Sword
            (ushort)Species.Zamazenta => form == 1 ? 1104 : null, // Rusted Shield
            _ => null,
        };
    }

    public static bool GetIsFormInvalid(PKM pk, ITrainerInfo tr, byte form)
    {
        var generation = tr.Generation;
        var species = pk.Species;
        switch ((Species)species)
        {
            case Species.Unown when generation == 2 && form >= 26:
                return true;
            case Species.Floette when form == 5:
                return true;
            case Species.Shaymin
                or Species.Furfrou
                or Species.Hoopa when form != 0 && generation <= 6:
                return true;
            case Species.Arceus when generation == 4 && form == 9: // ??? form
                return true;
            case Species.Scatterbug or Species.Spewpa when form == 19:
                return true;
        }
        if (FormInfo.IsBattleOnlyForm(pk.Species, form, generation))
            return true;

        if (form == 0)
            return false;

        return false;
    }
}