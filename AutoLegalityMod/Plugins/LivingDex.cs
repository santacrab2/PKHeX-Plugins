using System;
using System.IO;
using System.Windows.Forms;
using AutoModPlugins.Properties;
using PKHeX.Core;
using PKHeX.Core.AutoMod;
using System.Collections.Generic;
using Microsoft.VisualBasic.Devices;

namespace AutoModPlugins;

public class LivingDex : AutoModPlugin
{
    public override string Name => "Generate Living Dex";
    public override int Priority => 1;

    protected override void AddPluginControl(ToolStripDropDownItem modmenu)
    {
        var ctrl = new ToolStripMenuItem(Name) 
        { 
            Image = Resources.livingdex,
            ShortcutKeys = Keys.Alt | Keys.E,
        };
        ctrl.Click += GenLivingDex;
        ctrl.Name = "Menu_LivingDex";
        modmenu.DropDownItems.Add(ctrl);
    }

    private void GenLivingDex(object? sender, EventArgs e)
    {
        var prompt = WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Generate a Living Dex?");
        if (prompt != DialogResult.Yes)
            return;
        bool egg = false;
        if (new Keyboard().AltKeyDown)
            egg = true;
        var sav = SaveFileEditor.SAV;
        var dex = egg?sav.GenerateLivingEggDex(sav.Personal):sav.GenerateLivingDex(sav.Personal);
        List<PKM> extra = [];
        int generated = IngestToBoxes(sav, dex, extra);
        System.Diagnostics.Debug.WriteLine($"Generated Living Dex with {generated} entries.");
        SaveFileEditor.ReloadSlots();
        if (extra.Count == 0)
            return;

        prompt = WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "This Living Dex does not fit in all boxes. Save the extra to a folder?");
        if (prompt != DialogResult.Yes)
            return;

        using var ofd = new FolderBrowserDialog();
        if (ofd.ShowDialog() != DialogResult.OK)
            return;

        foreach (var f in extra)
            File.WriteAllBytes($"{ofd.SelectedPath}/{f.FileName}", f.DecryptedPartyData);
    }

    private static int IngestToBoxes(SaveFile sav, IEnumerable<PKM> list, IList<PKM> extra, int slot = 0)
    {
        int generated = 0;
        foreach (var pk in list)
        {
            generated++;
            if (TryAdd(sav, extra, pk, ref slot))
                continue;
            while (true)
            {
                slot++;
                if (TryAdd(sav, extra, pk, ref slot))
                    break;
            }
        }
        return generated;
    }

    private static bool TryAdd(SaveFile sav, IList<PKM> extra, PKM pk, ref int slot)
    {
        if (slot >= sav.SlotCount)
        {
            extra.Add(pk);
            return true;
        }
        if (!sav.IsBoxSlotOverwriteProtected(slot))
        {
            sav.SetBoxSlotAtIndex(pk, slot++);
            return true;
        }
        return false;
    }
}