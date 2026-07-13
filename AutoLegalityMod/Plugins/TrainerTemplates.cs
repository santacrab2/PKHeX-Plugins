using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using AutoModPlugins.Properties;
using PKHeX.Core;
using PKHeX.Core.AutoMod;
namespace AutoModPlugins;

public class TrainerTemplates : AutoModPlugin
{
    public override string Name => "Generate Trainer Templates";
    public override int Priority => 2;
    private static readonly string ProcessPath = Environment.ProcessPath ?? string.Empty;
    private static readonly string TrainerPath = Path.Combine(Path.GetDirectoryName(ProcessPath) ?? string.Empty, "trainers");

    protected override void AddPluginControl(ToolStripDropDownItem modmenu)
    {
        var ctrl = new ToolStripMenuItem(Name) { Image = WinFormsUtil.GetIconForTheme(Resources.settings, Application.IsDarkModeEnabled) };
        ctrl.Click += CreateTrainerTemplates;
        ctrl.Name = "Menu_TrainerTemplates";
        modmenu.DropDownItems.Add(ctrl);
    }
  private void CreateTrainerTemplates(object? sender, EventArgs e)
  {
      if (!Directory.Exists(TrainerPath))
          Directory.CreateDirectory(TrainerPath);

      var preservesettings = ParseSettings.Settings.Handler.CheckActiveHandler;
      ParseSettings.Settings.Handler.CheckActiveHandler = false;

      for (int i = 1; i < 52; i++)
      {
          if (!Enum.IsDefined(typeof(GameVersion), (byte)i))
              continue;
          if (i == 34)
              continue;
          var gen = ((GameVersion)i).Generation == 0 ? 1 : ((GameVersion)i).Generation;
          var TID = (ushort)Random.Shared.Next(ushort.MaxValue);
          var SID = (ushort)Random.Shared.Next(ushort.MaxValue);
          while (TrainerIDVerifier.IsOTIDSuspicious(TID, SID))
          {
              TID = (ushort)Random.Shared.Next(ushort.MaxValue);
              SID = (ushort)Random.Shared.Next(ushort.MaxValue);
          }
          var text = $"Pikachu\nOT: {TrainerSettings.DefaultOT}\nTID: {TID}\nSID: {SID}\n.Version={(GameVersion)i}";
          var set = new RegenTemplate(new ShowdownSet(text), (byte)gen);

          var temp = BlankSaveFile.Get((GameVersion)i, TrainerSettings.DefaultOT);
          var result = temp.GetLegalFromSet(set);

          if (result.Created == null)
              continue;

          byte[] data = new byte[result.Created.SIZE_PARTY];
          result.Created.WriteEncryptedDataParty(data);
          File.WriteAllBytes(Path.Combine(TrainerPath, result.Created.FileName), data);
      }

      ParseSettings.Settings.Handler.CheckActiveHandler = preservesettings;

      var page = new TaskDialogPage
      {
          Text = $"The randomized templates were created in {TrainerPath}, edit them to match your preferences."
      };
      var ok = new TaskDialogButton("OK");
      var gotofolder = new TaskDialogButton("Open Folder");
      page.AllowCancel = true;
      page.Buttons.Add(ok);
      page.Buttons.Add(gotofolder);
      gotofolder.Click += (_, _) =>
      {
          Process.Start(new ProcessStartInfo
          {
              FileName = TrainerPath,
              UseShellExecute = true
          });
      };
      TaskDialog.ShowDialog(page);
  }

}
