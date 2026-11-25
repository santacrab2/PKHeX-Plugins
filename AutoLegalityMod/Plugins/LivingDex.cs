using System;
using System.IO;
using System.Windows.Forms;
using AutoModPlugins.Properties;
using PKHeX.Core;
using PKHeX.Core.AutoMod;
using System.Collections.Generic;
using Microsoft.VisualBasic.Devices;
using System.Threading.Tasks;
using AutoModPlugins.GUI;
using System.Threading;
using System.Reflection.Metadata;

namespace AutoModPlugins;

public class LivingDex : AutoModPlugin
{
    public override string Name => "Generate Living Dex";
    public override int Priority => 1;

    protected override void AddPluginControl(ToolStripDropDownItem modmenu)
    {
        var ctrl = new ToolStripMenuItem(Name)
        {
            Image = WinFormsUtil.GetIconForTheme(Resources.livingdex, Application.IsDarkModeEnabled),
            ShortcutKeys = Keys.Alt | Keys.E,
        };
        ctrl.Click += GenLivingDex;
        ctrl.Name = "Menu_LivingDex";
        modmenu.DropDownItems.Add(ctrl);
    }
    public static CancellationTokenSource? cts;
    private async void GenLivingDex(object? sender, EventArgs e)
    {
        var prompt = WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Generate a Living Dex?");
        if (prompt != DialogResult.Yes)
            return;
        bool egg = new Keyboard().AltKeyDown;
        var sav = SaveFileEditor.SAV;
        var t = new ALMStatusBar("Living Dex", sav.MaxSpeciesID);
        t.Show();
        cts = new();
        // After showing the form, start a polling loop
        _ = Task.Run(() => PollingLoop(t));

        var dex = await Task.Run(() => egg ? sav.GenerateLivingEggDex(sav.Personal) : sav.GenerateLivingDex(sav.Personal), cts.Token);
        if (cts.IsCancellationRequested)
            return;

        List<PKM> extra = [];
        t.closedbyuser = false;
        t.Close();
        cts.Cancel();
        prompt = WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Overwrite any existing Pokémon in your boxes?");
        int generated = IngestToBoxes(sav, dex, extra, prompt == DialogResult.Yes);
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
        Span<byte> PokeData = stackalloc byte[sav.SIZE_PARTY];
        foreach (var f in extra)
        {
            f.WriteDecryptedDataParty(PokeData);
            File.WriteAllBytes($"{ofd.SelectedPath}/{f.FileName}", PokeData);
        }
    }

    private static void PollingLoop(ALMStatusBar t)
    {
        int lastCount = -1;
        while (!t.IsDisposed)
        {
            if (ModLogic.TrackingCount != lastCount)
            {
                lastCount = ModLogic.TrackingCount;
                t.Count = lastCount;
            }
        }
    }
    private static int IngestToBoxes(SaveFile sav, IEnumerable<PKM> list, IList<PKM> extra, bool overwrite, int slot = 0)
    {
        int generated = 0;
        foreach (var pk in list)
        {
            generated++;
            if (TryAdd(sav, extra, pk, overwrite, ref slot))
                continue;
            do
            {
                slot++;
            }
            while (!TryAdd(sav, extra, pk,overwrite, ref slot));
        }
        return generated;
    }

    private static bool TryAdd(SaveFile sav, IList<PKM> extra, PKM pk, bool overwrite, ref int slot)
    {
        if (slot >= sav.SlotCount)
        {
            extra.Add(pk);
            return true;
        }
        if (!overwrite && sav.NextOpenBoxSlot(slot - 1) != slot)
            return false;
        if (!sav.IsBoxSlotOverwriteProtected(slot))
        {
            sav.SetBoxSlotAtIndex(pk, slot++);
            return true;
        }
        return false;
    }
}
