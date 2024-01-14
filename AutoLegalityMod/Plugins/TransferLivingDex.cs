using AutoModPlugins.Properties;
using PKHeX.Core.AutoMod;
using PKHeX.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AutoModPlugins
{
    public class TransferLivingDex : AutoModPlugin
    {
        public override string Name => "Transfer Living Dex";

        public override int Priority => 1;

        protected override void AddPluginControl(ToolStripDropDownItem modmenu)
        {
            var ctrl = new ToolStripMenuItem(Name) { Image = Resources.livingdex };
            ctrl.Click += GenTLivingDex;
            ctrl.Name = "Menu_TransferDex";
            modmenu.DropDownItems.Add(ctrl);
        }
        private void GenTLivingDex(object? sender, EventArgs e)
        {
            var prompt = WinFormsUtil.Prompt(MessageBoxButtons.YesNo, $"Generate a Transfer Dex for {_settings.TransferVersion}?");
            if (prompt != DialogResult.Yes)
            {
                return;
            }

            var sav = SaveFileEditor.SAV;
            Span<PKM> pkms = sav.GenerateTLivingDex().ToArray();
            Span<PKM> bd = sav.BoxData.ToArray();
            if (pkms.Length > bd.Length)
            {
                pkms = pkms[..bd.Length];
            }

            pkms.CopyTo(bd);
            sav.BoxData = bd.ToArray();
            SaveFileEditor.ReloadSlots();

            System.Diagnostics.Debug.WriteLine($"Generated Living Dex with {pkms.Length} entries.");
        }
    }
}
