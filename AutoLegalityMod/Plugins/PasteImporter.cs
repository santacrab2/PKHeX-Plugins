using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using AutoModPlugins.Properties;
using PKHeX.Core;
using PKHeX.Core.AutoMod;
using PKHeX.Core.Enhancements;
using PKHeX.Core.Injection;

namespace AutoModPlugins
{
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
            ToolStripItem parent = modmenu.OwnerItem;
            var form = (parent.GetCurrentParent().Parent ?? throw new Exception("Parent not found")).FindForm();
            if (form is not null)
            {
                form.Icon = Resources.icon;
            }

            ShowdownSetLoader.PKMEditor = PKMEditor;
            ShowdownSetLoader.SaveFileEditor = SaveFileEditor;
        }

        private void ImportPaste(object? sender, EventArgs e)
        {
            // Check for showdown data in clipboard
            var text = GetTextShowdownData();
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            ShowdownSetLoader.Import(text!);
        }

        /// <summary>
        /// Check whether the showdown text is supposed to be loaded via a text file. If so, set the clipboard to its contents.
        /// </summary>
        /// <returns>output boolean that tells if the data provided is valid or not</returns>
        private string? GetTextShowdownData()
        {
            bool skipClipboardCheck = (Control.ModifierKeys & Keys.Shift) == Keys.Shift;
            if (!skipClipboardCheck && Clipboard.ContainsText())
            {
                var txt = Clipboard.GetText();
                if (ShowdownUtil.IsTextShowdownData(txt))
                    return txt;
            }

            if (!WinFormsUtil.OpenSAVPKMDialog(new[] { "txt" }, out var path))
            {
                WinFormsUtil.Alert("No data provided.");
                var sixrando=WinFormsUtil.Prompt(MessageBoxButtons.OKCancel, "Generate 6 Random Pokemon?");
                if (sixrando == DialogResult.OK)
                    return GetSixRandomMons();
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
        public string? GetSixRandomMons()
        {
            string showdowntext = string.Empty;
            int i = 0;
            do
            {
                var rng = new Random();
                var spec = rng.Next(SaveFileEditor.SAV.MaxSpeciesID);
                var rough = EntityBlank.GetBlank(SaveFileEditor.SAV);
                rough.Species = (ushort)spec;
                rough.Gender = rough.GetSaneGender();
                if (!SaveFileEditor.SAV.Personal.IsSpeciesInGame(rough.Species))
                    continue;
                var formnumb = SaveFileEditor.SAV.Personal[rough.Species].FormCount;
                if(formnumb == 1)
                    formnumb = (byte)FormConverter.GetFormList(rough.Species, GameInfo.Strings.types, GameInfo.Strings.forms, GameInfo.GenderSymbolUnicode, SaveFileEditor.SAV.Context).Length;
                do
                {
                    if (formnumb == 0) break;
                    rough.Form = (byte)rng.Next(formnumb);
                }
                while (!SaveFileEditor.SAV.Personal.IsPresentInGame(rough.Species, rough.Form) || FormInfo.IsLordForm(rough.Species, rough.Form, SaveFileEditor.SAV.Context) || FormInfo.IsBattleOnlyForm(rough.Species, rough.Form, SaveFileEditor.SAV.Generation) || FormInfo.IsFusedForm(rough.Species, rough.Form, SaveFileEditor.SAV.Generation) || (FormInfo.IsTotemForm(rough.Species, rough.Form) && SaveFileEditor.SAV.Context is not EntityContext.Gen7));
                if (rough.Species is ((ushort)Species.Meowstic) or ((ushort)Species.Indeedee))
                {
                    rough.Gender = rough.Form;
                    rough.Form = (byte)rough.Gender;
                }
                var item = GetFormSpecificItem((int)SaveFileEditor.SAV.Version, rough.Species, rough.Form);
                if (item is not null)
                    rough.HeldItem = (int)item;

                if (rough.Species == (ushort)Species.Keldeo && rough.Form == 1)
                    rough.Move1 = (ushort)Move.SecretSword;

                if (GetIsFormInvalid(rough, SaveFileEditor.SAV, rough.Form))
                    continue;
                try
                {
                    var goodset = new SmogonSetList(rough);
                    if (goodset.Valid && goodset.Sets.Count != 0)
                    {
                        showdowntext += goodset.Sets[0].Text;
                        showdowntext += "\n\n";
                        i++;
                        continue;
                    }
                }
                catch (Exception ex) { }

                showdowntext += new ShowdownSet(rough).Text.Split('\r')[0];
                showdowntext += "\nLevel: 100\n";
                Span<int> ivs = stackalloc int[6];
                EffortValues.SetMax(ivs, rough);
                showdowntext += $"EVs: {ivs[0]} HP / {ivs[1]} Atk / {ivs[2]} Def / {ivs[3]} SpA / {ivs[4]} SpD / {ivs[5]} Spe\n";
                var m = new ushort[4];
                rough.GetMoveSet(m, true);
                showdowntext += $"- {GameInfo.MoveDataSource.First(z => z.Value == m[0]).Text}\n- {GameInfo.MoveDataSource.First(z => z.Value == m[1]).Text}\n- {GameInfo.MoveDataSource.First(z => z.Value == m[2]).Text}\n- {GameInfo.MoveDataSource.First(z => z.Value == m[3]).Text}";
                showdowntext += "\n\n";
                i++;

            } while (i < 6);
            return showdowntext;
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
                _ => null
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
}
