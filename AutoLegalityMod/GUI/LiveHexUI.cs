using AutoModPlugins.GUI;
using PKHeX.Core;
using PKHeX.Core.Injection;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
namespace AutoModPlugins;

public partial class LiveHeXUI : Form, ISlotViewer<PictureBox>
{
    private ISaveFileProvider SAV { get; }
    private static GameVersion SAV_Version = GameVersion.Any;

    public int ViewIndex => BoxSelect?.SelectedIndex ?? 0;
    public IList<PictureBox> SlotPictureBoxes => throw new InvalidOperationException();
    SaveFile ISlotViewer<PictureBox>.SAV => throw new InvalidOperationException();
    public void ApplyNewFilter(Func<PKM, bool>? filter, bool reload = true) {}
    private LiveHeXController Remote;
    private readonly SaveDataEditor<PictureBox> x;
    private readonly PluginSettings _settings;
    private IPKMView Editor;
    private InjectorCommunicationType CurrentInjectionType;
    private bool initializing = true;
    private readonly ComboBox? BoxSelect; // this is just us holding a reference; disposal is done by its parent

    public LiveHeXUI(ISaveFileProvider sav, IPKMView editor, PluginSettings settings)
    {
        Editor = editor;
        SAV = sav;
        if (SAV.SAV.Version != GameVersion.Invalid)
            SAV_Version = sav.SAV.Version;

        _settings = settings;
        CurrentInjectionType = _settings.USBBotBasePreferred ? InjectorCommunicationType.USB : InjectorCommunicationType.SocketNetwork;
        Remote = new LiveHeXController(sav, editor, CurrentInjectionType);

        InitializeComponent();
        connectionMode.SelectedIndex = settings.USBBotBasePreferred ? 1 : 0;
        this.TranslateInterface(WinFormsTranslator.CurrentLanguage);
        TB_IP.Text = _settings.LatestIP;

        // Default Wi-Fi ports for loaded save, 6000 for Switch, 8000 for 3DS
        var default_port = RamOffsets.IsSwitchTitle(sav.SAV) ? 6000 : 8000;

        if (_settings.USBBotBasePreferred)
        {
            // Only use the saved port if using USB-Botbase
            if (int.TryParse(_settings.LatestPort, out int port))
                default_port = port;
            // Allow editing of the port field.
            TB_Port.ReadOnly = false;
        }
        TB_Port.Text = default_port.ToString();
        SetInjectionTypeView();

        // add an event to the editor
        // ReSharper disable once SuspiciousTypeConversion.Global
        BoxSelect = ((Control)sav).Controls.Find("CB_BoxSelect", true).FirstOrDefault() as ComboBox;
        if (BoxSelect is not null)
        {
            BoxSelect.SelectedIndexChanged += ChangeBox;
            FormClosing += (_, _) => BoxSelect.SelectedIndexChanged -= ChangeBox;
        }

        var type = sav.GetType();
        var fields = type.GetTypeInfo().DeclaredFields;
        var test = fields.First(z => z.Name == "EditEnv");
        x = (SaveDataEditor<PictureBox>)(test.GetValue(sav) ?? new Exception("Error with LiveHeXUI init."));
        x.Slots.Publisher.Subscribe(this);

        CenterToParent();
        initializing = false;
    }

    public ISlotInfo GetSlotData(PictureBox view) => throw new InvalidOperationException();

    public int GetViewIndex(ISlotInfo slot) => -1;

    public void NotifySlotOld(ISlotInfo previous) { }

    public void NotifySlotChanged(ISlotInfo slot, SlotTouchType type, PKM pkm)
    {
        if (!checkBox2.Checked || !Remote.Bot.Connected)
            return;

        if (slot is not SlotInfoBox SIB)
            return;

        if (!type.IsContentChange())
            return;
        SAV.SAV.AdaptToSaveFile(pkm);
        var size = RamOffsets.WriteBoxData(Remote.Bot.Version) ? SAV.SAV.SIZE_STORED : SAV.SAV.SIZE_PARTY;
        if (Remote.Bot.Version >= LiveHeXVersion.ZA_v101 && Remote.Bot.Version <= LiveHeXVersion.ZA_v202)
            size = 345;
        Span<byte> PokemonData = stackalloc byte[size];
        if (RamOffsets.WriteBoxData(Remote.Bot.Version))
            pkm.WriteEncryptedDataStored(PokemonData);
        else
            pkm.WriteEncryptedDataParty(PokemonData);
        if (Remote.Bot.Version >= LiveHeXVersion.ZA_v101 && Remote.Bot.Version <= LiveHeXVersion.ZA_v202)
            PokemonData[344] = (byte)(pkm.Species == 0 ? PokemonData[344] : 1);
        Remote.Bot.SendSlot(PokemonData, SIB.Box, SIB.Slot);
    }

    private void SetTrainerData(SaveFile sav)
    {
        // Check and set trainerdata based on ISaveBlock interfaces
        Span<byte> dest;

        Func<PokeSysBotMini, byte[]?> tdata;
        switch (sav)
        {
            case ISaveBlock8SWSH s8:
                dest = s8.MyStatus.Data;
                tdata = LPBasic.GetTrainerData;
                break;

            case ISaveBlock7Main s7:
                dest = s7.MyStatus.Data;
                tdata = LPBasic.GetTrainerData;
                break;

            case ISaveBlock6Main s6:
                dest = s6.Status.Data;
                tdata = LPBasic.GetTrainerData;
                break;

            case SAV7b slgpe:
                dest = slgpe.Blocks.Status.Data;
                tdata = LPLGPE.GetTrainerData;
                break;

            case SAV8BS sbdsp:
                dest = sbdsp.MyStatus.Data;
                tdata = LPBDSP.GetTrainerData;
                break;

            case SAV8LA sbla:
                dest = sbla.MyStatus.Data;
                tdata = LPPointer.GetTrainerDataLA;
                break;

            case SAV9SV s9sv:
                dest = s9sv.MyStatus.Data;
                tdata = LPPointer.GetTrainerDataSV;
                break;
            case SAV9ZA s9za:
                dest = s9za.MyStatus.Data;
                tdata = LPPointer.GetTrainerDataZA;
                break;

            default:
                dest = [];
                tdata = LPBasic.GetTrainerData;
                break;
        }

        if (dest.Length == 0)
            return;

        var data = tdata(Remote.Bot);
        if (data is null || data.Length == 0)
            return;
        data.CopyTo(dest);
    }

    private void ChangeBox(object? sender, EventArgs e)
    {
        if (CB_ReadBox.Checked && Remote.Bot.Connected)
            Remote.ChangeBox(ViewIndex);
    }

    private void B_Connect_Click(object sender, EventArgs e)
    {
        try
        {
            Text = "LiveHeXUI";
            var communicator = RamOffsets.GetCommunicator(SAV.SAV, CurrentInjectionType);
            communicator.IP = TB_IP.Text;
            communicator.Port = int.Parse(TB_Port.Text);
            communicator.Connect();

            var (validation, msg, lv) = (LiveHeXValidation.None, "", LiveHeXVersion.Unknown);
            string gameVer = "0",
                gameName = "";

            var versions = RamOffsets.GetValidVersions(SAV.SAV).Reverse().ToArray();
            if (communicator is not ICommunicatorNX nx)
            {
                if (!communicator.Connected)
                {
                    const string errorstr = "Unable to Connect. Click the \"Wiki\" button to troubleshoot.";

                    var error = WinFormsUtil.ALMErrorBasic(errorstr);
                    error.ShowDialog();

                    var res = error.DialogResult;
                    if (res != DialogResult.Retry)
                        return;

                    var info = new ProcessStartInfo
                    {
                        FileName = "https://github.com/santacrab2/PKHeX-Plugins/wiki/Installing-LiveHeX",
                        UseShellExecute = true,
                    };
                    Process.Start(info);
                    return;
                }
                (validation, msg, lv) = Connect_NTR(communicator, versions);
            }
            else
            {
                (validation, msg, lv) = Connect_Switch(nx, versions, ref gameVer, ref gameName);
            }

            var currVer = lv is LiveHeXVersion.Unknown ? RamOffsets.GetValidVersions(SAV.SAV).Reverse().ToArray()[0] : lv;
            bool validated = ConnectionValidated(Remote.Bot, gameVer, currVer, validation, msg);
            if (!validated && !_settings.EnableDevMode)
                return;

            Text = $"Detected: {gameName} ({gameVer})";
            if (_settings.EnableDevMode && lv is LiveHeXVersion.Unknown)
                Text += " [Forced DevMode]";

            if (Remote.Bot.com is IPokeBlocks && lv is not LiveHeXVersion.Unknown)
            {
                var cblist = GetSortedBlockList(currVer).ToArray();
                if (cblist.Length > 0)
                {
                    groupBox5.Enabled = true;
                    CB_BlockName.Items.AddRange(cblist);
                    CB_BlockName.SelectedIndex = 0;
                }
            }

            if (Remote.Bot.com is ICommunicatorNX)
                groupBox4.Enabled = groupBox6.Enabled = true;

            if (lv is not LiveHeXVersion.Unknown)
            {
                // Set Trainer Data
                SetTrainerData(SAV.SAV);
                // Load current box
                Remote.ReadBox(SAV.CurrentBox);
            }
        }
        // Console might be disconnected...
        catch (Exception ex)
        {
            var errorstr = $"{ex.Message}\n\n" + "Click the \"Wiki\" button to troubleshoot.";

            var error = WinFormsUtil.ALMErrorBasic(errorstr);
            error.ShowDialog();

            var res = error.DialogResult;
            if (res != DialogResult.Retry)
                return;

            var info = new ProcessStartInfo
            {
                FileName = "https://github.com/santacrab2/PKHeX-Plugins/wiki/FAQ-and-Troubleshooting#troubleshooting",
                UseShellExecute = true,
            };
            Process.Start(info);
            return;
        }

        B_Connect.Enabled = B_Connect.Visible = TB_IP.Enabled = TB_Port.Enabled = connectionMode.Enabled = false;
        B_Disconnect.Enabled = B_Disconnect.Visible = GB_Boxes.Enabled = groupBox3.Enabled = true;
    }

    private (LiveHeXValidation, string, LiveHeXVersion) Connect_NTR(ICommunicator com, LiveHeXVersion[] versions)
    {
        foreach (var version in versions)
        {
            Remote.Bot = new PokeSysBotMini(version, com);
            var data = Remote.Bot.ReadSlot(0, 0);
            var pkm = SAV.SAV.GetDecryptedPKM(data.ToArray());
            bool valid = pkm.Species <= pkm.MaxSpeciesID && pkm.ChecksumValid &&
                         pkm is { Species: 0, EncryptionConstant: 0 } or { Species: not 0, Language: not (int)LanguageID.None and not (int)LanguageID.UNUSED_6 };
            if (valid)
                return (LiveHeXValidation.None, "", version);
        }

        var saveName = GameInfo.GetVersionName(SAV.SAV.Version);
        var msg = "Could not find a compatible game version while establishing an NTR connection.\n" + $"Save file loaded: Pokémon {saveName}";
        return (LiveHeXValidation.GameVersion, msg, LiveHeXVersion.Unknown);
    }

    private (LiveHeXValidation, string, LiveHeXVersion) Connect_Switch(ICommunicatorNX nx, LiveHeXVersion[] versions, ref string gameVer, ref string gameName)
    {
        var botbaseVer = nx.GetBotbaseVersion();
        var version = Version.TryParse(botbaseVer, out var v) ? v : null;
        if ((version < InjectionBase.BotbaseVersion || version is null) && !_settings.EnableDevMode)
        {
            var msg = $"Incompatible {(nx.Protocol is InjectorCommunicationType.SocketNetwork ? "sys-botbase" : "usb-botbase")} version.\n"
                      + $"Expected version {InjectionBase.BotbaseVersion} or greater, and current version is {version}.\n\n"
                      + "Please download and install the latest version by clicking the \"Update\" button.";

            return (LiveHeXValidation.Botbase, msg, LiveHeXVersion.Unknown);
        }

        var titleID = nx.GetTitleID();
        gameName = nx.GetGameInfo("name");
        gameVer = nx.GetGameInfo("version").Trim();

        var compatible = InjectionBase.SaveCompatibleWithTitle(SAV.SAV, titleID);
        var lv = compatible ? InjectionBase.GetVersionFromTitle(titleID, gameVer) : LiveHeXVersion.Unknown;
        if (lv >= LiveHeXVersion.FRLG_E_v100)
            gameName = "FireRed/LeafGreen";
        if (!compatible && !_settings.EnableDevMode)
        {
            var saveName = GameInfo.GetVersionName(SAV_Version);
            var msg = $"Detected game: {gameName} ({gameVer})\n"
                      + $"Save file loaded: Pokémon {saveName}\n\n"
                      + "Have you selected the correct blank save in PKHeX?";

            if (lv is not LiveHeXVersion.Unknown)
                gameVer = lv.ToString();

            return (LiveHeXValidation.BlankSAV, msg, LiveHeXVersion.Unknown);
        }

        if (lv is LiveHeXVersion.Unknown && !_settings.EnableDevMode)
        {
            var msg = $"Unsupported version for {gameName}\n\n"
                      + $"Latest supported version is {versions[0]}.\n"
                      + $"Earliest supported version is {versions[^1]}.\n"
                      + $"Detected version is {gameVer}.";
            return (LiveHeXValidation.GameVersion, msg, lv);
        }

        var connect_ver = lv is LiveHeXVersion.Unknown ? RamOffsets.GetValidVersions(SAV.SAV).Reverse().ToArray()[0] : lv;
        Remote.Bot = new PokeSysBotMini(connect_ver, nx);
        if (lv is LiveHeXVersion.Unknown && _settings.EnableDevMode)
            return (LiveHeXValidation.None, "", lv);
        var data = Remote.Bot.ReadSlot(0, 0);
        if (lv >= LiveHeXVersion.ZA_v101 && lv <= LiveHeXVersion.ZA_v202)
            data = data[..344]; //shrink pa9 to incorrect size so debug build doesn't crash
        if (_settings.EnableDevMode && data.Length < SAV.SAV.SIZE_BOXSLOT)
            data = new byte[SAV.SAV.SIZE_BOXSLOT];
        var pkm = SAV.SAV.GetDecryptedPKM(data.ToArray());
        bool valid = pkm.Species <= pkm.MaxSpeciesID && pkm.ChecksumValid &&
                     pkm is { Species: 0, EncryptionConstant: 0 }
                         or { Species: not 0, Language: not (int)LanguageID.None and not (int)LanguageID.UNUSED_6 };
        return !_settings.EnableDevMode && !valid && InjectionBase.CheckRAMShift(Remote.Bot, out string err) ? (LiveHeXValidation.RAMShift, err, lv) : !valid ? (LiveHeXValidation.GameVersion, "Invalid data found.", LiveHeXVersion.Unknown) : (LiveHeXValidation.None, "", lv);
    }

    private void B_Disconnect_Click(object sender, EventArgs e)
    {
        if (!Remote.Bot.com.Connected)
            return;

        try
        {
            B_Connect.Enabled = B_Connect.Visible = TB_IP.Enabled = TB_Port.Enabled = connectionMode.Enabled = true;
            B_Disconnect.Enabled =
                B_Disconnect.Visible =
                    GB_Boxes.Enabled =
                            groupBox3.Enabled =
                                false;
            CB_BlockName.Items.Clear();
            Text = "LiveHeXUI";

            if (Remote.Bot.com is ICommunicatorNX)
                groupBox4.Enabled = groupBox6.Enabled = groupBox5.Enabled = false;

            Remote.Bot.com.Disconnect();
        }
        catch (Exception ex)
        {
            var error = WinFormsUtil.ALMErrorBasic(ex.Message);
            error.ShowDialog();
        }
    }

    private void LiveHeXUI_FormClosing(object sender, FormClosingEventArgs e)
    {
        if (Remote.Bot.Connected)
            Remote.Bot.com.Disconnect();

        x.Slots.Publisher.Unsubscribe(this);
        _settings.LatestIP = TB_IP.Text;
        // Only save the port if using USB-Botbase
        if (_settings.USBBotBasePreferred)
            _settings.LatestPort = TB_Port.Text;
        _settings.Save();
    }

    private void B_ReadCurrent_Click(object sender, EventArgs e) => Remote.ReadBox(SAV.CurrentBox);

    private void B_WriteCurrent_Click(object sender, EventArgs e) => Remote.WriteBox(SAV.CurrentBox);

    private void B_ReadOffset_Click(object sender, EventArgs e)
    {
        bool readPointer = (ModifierKeys & Keys.Control) == Keys.Control;
        var txt = RamOffset.Text;
        var method = RWMethod.Heap;
        if (RB_Main.Checked)
            method = RWMethod.Main;
        if (RB_Absolute.Checked)
            method = RWMethod.Absolute;
        var offset = readPointer && Remote.Bot.com is ICommunicatorNX nx ? nx.GetPointerAddress(TB_Pointer.Text, method == RWMethod.Heap) : Util.GetHexValue64(txt);
        if ((offset.ToString("X16") != txt.ToUpper().PadLeft(16, '0') && !readPointer) || offset == InjectionUtil.INVALID_PTR)
        {
            WinFormsUtil.Alert("Specified offset is not a valid hex string.");
            return;
        }
        try
        {
            var result = Remote.ReadOffset(offset, method);
            if (!result)
                WinFormsUtil.Alert("No valid data is located at the specified offset.");
        }
        catch (Exception ex)
        {
            WinFormsUtil.Error("Unable to load data from the specified offset.", ex.Message);
        }
    }

    private RWMethod GetRWMethod()
    {
        if (RB_Main.Checked)
            return RWMethod.Main;

        return RB_Absolute.Checked ? RWMethod.Absolute : RWMethod.Heap;
    }

    private void B_ReadRAM_Click(object sender, EventArgs e)
    {
        var txt = RamOffset.Text;
        var offset = Util.GetHexValue64(txt);
        var valid = int.TryParse(RamSize.Text, out int size);
        if (offset.ToString("X16") != txt.ToUpper().PadLeft(16, '0') || !valid)
        {
            WinFormsUtil.Alert("Make sure that the RAM offset is a hex string and the size is a valid integer.");
            return;
        }

        try
        {
            Span<byte> result;
            if (Remote.Bot.com is not ICommunicatorNX cnx)
            {
                result = Remote.ReadRAM(offset, size);
            }
            else if (RB_Main.Checked)
            {
                result = cnx.ReadBytesMain(offset, size);
            }
            else if (RB_Absolute.Checked)
            {
                result = cnx.ReadBytesAbsolute(offset, size);
            }
            else
            {
                result = Remote.ReadRAM(offset, size);
            }

            bool blockview = (ModifierKeys & Keys.Control) == Keys.Control;
            PKM? pkm = null;
            if (blockview)
            {
                pkm = SAV.SAV.GetDecryptedPKM(result.ToArray());
                if (!pkm.ChecksumValid)
                {
                    blockview = false;
                }
            }

            using var form = new SimpleHexEditor(result.ToArray(), Remote.Bot, offset, GetRWMethod(),refreshrate: _settings.refreshRate);
            var loadgrid = blockview && ReflectUtil.GetPropertiesCanWritePublicDeclared(pkm!.GetType()).Count() > 1;
            if (loadgrid)
            {
                form.PG_BlockView.Visible = true;
                form.PG_BlockView.SelectedObject = pkm;
            }

            var res = form.ShowDialog();
            _settings.refreshRate = form.RefreshRate;
            if (res != DialogResult.OK)
            {
                return;
            }

            if (loadgrid)
            {
                PKM pk = pkm!;
                var PokemonSize = RamOffsets.WriteBoxData(Remote.Bot.Version) ? SAV.SAV.SIZE_STORED : SAV.SAV.SIZE_PARTY;
                Span<byte> pkmbytes = stackalloc byte[Remote.Bot.SlotSize];
                if (RamOffsets.WriteBoxData(Remote.Bot.Version))
                    pk.WriteEncryptedDataStored(pkmbytes);
                else
                    pk.WriteEncryptedDataParty(pkmbytes);
                if (pkmbytes.Length == Remote.Bot.SlotSize)
                {
                    form.Bytes = pkmbytes.ToArray();
                }
                else
                {
                    form.Bytes = result.ToArray();
                    WinFormsUtil.Error("Size mismatch. Please report this issue on the Discord server.");
                }
            }

            var modifiedRAM = form.Bytes;
            if (Remote.Bot.com is not ICommunicatorNX nx)
            {
                Remote.WriteRAM(offset, modifiedRAM);
            }
            else if (RB_Main.Checked)
            {
                nx.WriteBytesMain(modifiedRAM, offset);
            }
            else if (RB_Absolute.Checked)
            {
                nx.WriteBytesAbsolute(modifiedRAM, offset);
            }
            else
            {
                Remote.WriteRAM(offset, modifiedRAM);
            }

            Debug.WriteLine("RAM Modified");
        }
        catch (Exception ex)
        {
            WinFormsUtil.Error("Unable to load data from the specified offset.", ex.Message);
        }
    }

    private IEnumerable<string> GetSortedBlockList(LiveHeXVersion lv)
    {
        if (Remote.Bot.Injector is LPBasic)
        {
            if (!LPBasic.SCBlocks.TryGetValue(lv, out BlockData[]? value))
            {
                return new List<string>();
            }

            var blks = value.Select(z => z.Display).Distinct().Order();
            return blks;
        }

        if (Remote.Bot.Injector is LPBDSP)
        {
            var save_blocks = LPBDSP.FunctionMap.Keys;
            var custom_blocks = LPBDSP.types.Select(t => t.Name);
            var blks = save_blocks.Concat(custom_blocks).Order();
            return blks;
        }

        if (Remote.Bot.Injector is LPPointer)
        {
            var save_blocks = LPPointer.SCBlocks[lv].Select(z => z.Display).Distinct();
            var blks = save_blocks.Order();
            return blks;
        }
        if (Remote.Bot.Injector is LPFRLG)
        {
            var save_blocks = LPFRLG.SCBlocks[lv].Select(z => z.Display).Where(z=> z != "SecurityKey").Distinct();
            var blks = save_blocks.Order();
            return blks;
        }

        return new List<string>();
    }

    private void SetInjectionTypeView()
    {
        TB_IP.Visible = CurrentInjectionType == InjectorCommunicationType.SocketNetwork;
        L_USBState.Visible = CurrentInjectionType == InjectorCommunicationType.USB;
    }

    public ulong GetPointerAddress(ICommunicatorNX sb)
    {
        var ptr = TB_Pointer.Text.Contains("[key]") ? TB_Pointer.Text.Replace("[key]", "").Trim() : TB_Pointer.Text.Trim();
        var address = sb.GetPointerAddress(ptr, false);
        return address;
    }

    private void B_CopyAddress_Click(object sender, EventArgs e)
    {
        if (Remote.Bot.com is not ICommunicatorNX sb)
            return;

        ulong address = GetPointerAddress(sb);
        if (address == 0)
        {
            WinFormsUtil.Alert("No pointer address.");
            return;
        }



        Clipboard.SetText(address.ToString("X"));

    }

    private void B_EditPointerData_Click(object sender, EventArgs e)
    {
        if (Remote.Bot.com is not ICommunicatorNX sb)
            return;

        ulong address;
        int size;
        uint keyval = 0;

        var blk_key = TB_Pointer.Text.Contains("[key]");
        if (!blk_key)
        {
            address = GetPointerAddress(sb);
            if (address == 0)
            {
                WinFormsUtil.Alert("No pointer address.");
                return;
            }

            var valid = int.TryParse(RamSize.Text, out size);
            if (!valid)
            {
                WinFormsUtil.Alert("Make sure that the size is a valid integer.");
                return;
            }
        }
        else
        {
            var key = TB_Pointer.Text.Replace("[key]", "").Trim();
            if (!uint.TryParse(key, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out keyval))
            {
                WinFormsUtil.Alert("Key must be in a hexadecimal format.");
                return;
            }

            if (keyval == 0)
            {
                WinFormsUtil.Alert("Make sure that the provided key is valid.");
                return;
            }

            try
            {
                (address, size) = ReadKey(Remote.Bot, keyval);
            }
            catch (Exception ex)
            {
                WinFormsUtil.Alert(ex.Message);
                return;
            }
        }

        try
        {
            var header = 0;
            Span<byte> result = sb.ReadBytesAbsolute(address, size);
            if (blk_key)
            {
                bool typeView = (ModifierKeys & Keys.Alt) == Keys.Alt;
                var block = SCBlock.ReadFromOffset(result, keyval, ref header);

                if (block.Type.IsBoolean())
                {
                    WinFormsUtil.Alert($"SCBlock is set to {block.Type}.");
                    return;
                }

                if (typeView)
                {
                    WinFormsUtil.Alert($"Block type is {block.Type}.");
                }

                result = block.Data;
            }

            bool blockview = (ModifierKeys & Keys.Control) == Keys.Control;
            PKM? pkm = null;
            if (blockview)
            {
                pkm = SAV.SAV.GetDecryptedPKM(result.ToArray());
                if (!pkm.ChecksumValid)
                {
                    blockview = false;
                }
            }

            using (var form = new SimpleHexEditor(result.ToArray(), Remote.Bot, address, RWMethod.Absolute, blk_key, keyval, header,_settings.refreshRate))
            {
                var loadgrid = blockview && ReflectUtil.GetPropertiesCanWritePublicDeclared(pkm!.GetType()).Count() > 1;
                if (loadgrid)
                {
                    form.PG_BlockView.Visible = true;
                    form.PG_BlockView.SelectedObject = pkm;
                }

                var res = form.ShowDialog();
                _settings.refreshRate = form.RefreshRate;
                if (res == DialogResult.OK)
                {
                    if (loadgrid)
                    {
                        PKM pk = pkm!;
                        var PokemonSize = RamOffsets.WriteBoxData(Remote.Bot.Version) ? SAV.SAV.SIZE_STORED : SAV.SAV.SIZE_PARTY;
                        Span<byte> pkmbytes = stackalloc byte[Remote.Bot.SlotSize];
                        if (RamOffsets.WriteBoxData(Remote.Bot.Version))
                            pk.WriteEncryptedDataStored(pkmbytes);
                        else
                            pk.WriteEncryptedDataParty(pkmbytes);
                        if (pkmbytes.Length == Remote.Bot.SlotSize)
                        {
                            form.Bytes = pkmbytes.ToArray();
                        }
                        else
                        {
                            form.Bytes = result.ToArray();
                            WinFormsUtil.Error("Size mismatch. Please report this issue on the discord server.");
                        }
                    }

                    var modifiedRAM = form.Bytes;
                    sb.WriteBytesAbsolute(modifiedRAM, address);
                }
            }

            Debug.WriteLine("RAM Modified");
        }
        catch (Exception ex)
        {
            WinFormsUtil.Error("Unable to load data from the specified offset.", ex.Message);
        }
    }

    private void B_ReadPointer_Click(object sender, EventArgs e)
    {
        if (Remote.Bot.com is not ICommunicatorNX sb)
            return;

        ulong address = GetPointerAddress(sb);
        if (address == 0)
        {
            WinFormsUtil.Alert("No pointer address.");
            return;
        }

        var size = Remote.Bot.SlotSize;
        var data = sb.ReadBytesAbsolute(address, size);
        if (data.All(b=>b==0))
        {
            WinFormsUtil.Alert("No valid data is located at the specified offset.");
            return;
        }
        var pkm = SAV.SAV.GetDecryptedPKM(data);

        // Since data might not actually exist at the user-specified offset, double check that the pkm data is valid.
        if (pkm.ChecksumValid)
        {
            Remote.Editor.PopulateFields(pkm);
        }
    }

    private void B_EditBlock_Click(object sender, EventArgs e)
    {
        var txt = CB_BlockName.Text;
        var version = Remote.Bot.Version;
        if (version >= LiveHeXVersion.FRLG_E_v100)
        {
            ReadBlock(Remote.Bot, SAV.SAV, "SecurityKey", out _);
        }
        var valid = ReadBlock(Remote.Bot, SAV.SAV, txt, out var data);
        if (!valid || data is null)
        {
            WinFormsUtil.Error("Invalid Entry");
            return;
        }

        var objects = new List<object>();
        for (var i = 0; i < data.Count; i++)
        {
            var obj = data[i];
            valid = TryGetObjectInSave(version, SAV.SAV, txt, i, obj, out var blk);
            if (!valid || blk == null)
            {
                WinFormsUtil.Error("Error fetching Block");
                return;
            }
            objects.Add(blk);
        }
        var sb = objects[0];

        var write = false;
        if (txt.IsSpecialBlock(Remote.Bot, out var v) && v is not null)
        {
            // ReSharper disable once SuspiciousTypeConversion.Global
            var cc = (ContainerControl)SAV;

            // Set sender
            var s = txt switch
            {
                "Raid" => cc.Controls.Find("B_Raids", true)[0],
                "RaidKitakami" => cc.Controls.Find("B_RaidsDLC1", true)[0],
                "RaidBlueberry" => cc.Controls.Find("B_RaidsDLC2", true)[0],
                "RaidArmor" => cc.Controls.Find("B_RaidsDLC1", true)[0],
                "RaidCrown" => cc.Controls.Find("B_RaidsDLC2", true)[0],
                _ => sender,
            };

            // Invoke function
            cc.GetType().GetMethod(v, BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(cc, [s, e]);

            bool dataChanged = false;
            for (var i = 0; i < objects.Count; i++)
            {
                if (objects[i] is not SCBlock scb)
                {
                    dataChanged = true;
                }
                else if (!scb.Data.SequenceEqual(data[i]))
                {
                    dataChanged = true;
                }

                if (dataChanged)
                {
                    break;
                }
            }

            if (dataChanged)
            {
                var confirm = WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Apply changes to this block?");
                write = confirm == DialogResult.Yes;
            }
        }
        else if (sb is SCBlock or IDataIndirect or ICustomBlock)
        {
            // Must be single block output
            using var form = new SimpleHexEditor(data[0], refreshrate: _settings.refreshRate);
            if (sb is IDataIndirect or ICustomBlock)
            {
                var props = ReflectUtil.GetPropertiesCanWritePublicDeclared(sb.GetType());
                if (props.Count() > 1 && ModifierKeys != Keys.Control)
                {
                    form.PG_BlockView.Visible = true;
                    form.PG_BlockView.SelectedObject = sb;
                }
            }
            else
            {
                var o = SCBlockMetadata.GetEditableBlockObject((SCBlock)sb);
                if (o is not null)
                {
                    form.PG_BlockView.Visible = true;
                    form.PG_BlockView.SelectedObject = o;
                }
            }
            var res = form.ShowDialog();
            _settings.refreshRate = form.RefreshRate;
            write = res == DialogResult.OK;
        }
        else if (sb is not null)
        {
            using var form = new SimpleHexEditor(data[0],refreshrate: _settings.refreshRate);

            form.PG_BlockView.Visible = true;
            form.PG_BlockView.SelectedObject = SAV.SAV;

            var res = form.ShowDialog();
            _settings.refreshRate = form.RefreshRate;
            write = res == DialogResult.OK;
        }
        if (!write)
            return;

        if (Remote.Bot.Injector is LPBDSP && sb is not null)
        {
            Remote.Bot.Injector.WriteBlockFromString(Remote.Bot, txt, GetBlockDataRaw(sb, data[0]), sb);
        }
        else
        {
            Remote.Bot.Injector.WriteBlocksFromSAV(Remote.Bot, txt, SAV.SAV);
        }
    }

    private static byte[] GetBlockDataRaw(object sb, byte[] data) =>
        sb switch
        {
            SCBlock sc => sc.Data.ToArray(),
            IDataIndirect sv => sv.Data.ToArray(),
            _ => data,
        };

    private static bool ReadBlock(PokeSysBotMini bot, SaveFile sav, string display, out List<byte[]>? data)
    {
        return bot.Injector.ReadBlockFromString(bot, sav, display, out data);
    }

    private static (ulong Offset, int Length) ReadKey(PokeSysBotMini bot, uint keyval)
    {
        var version = bot.Version;
        string sbptr = LPPointer.GetSaveBlockPointer(version);

        if (sbptr.Length == 0)
            throw new Exception($"Pointer is not documented for searching block keys in {version}.");

        if (bot.com is not ICommunicatorNX nx)
            throw new Exception("Remote connection type is unable to read data from absolute offsets.");

        var ofs = bot.SearchSaveKey(sbptr, keyval);
        if (ofs == 0)
            throw new Exception($"Unable to find block key 0x{keyval:X8}");

        var dt = nx.ReadBytesAbsolute(ofs + 8, 8);
        ofs = BitConverter.ToUInt64(dt);

        var headerAfterKey = nx.ReadBytesAbsolute(ofs, 6);
        int size = SCBlock.GetTotalLength(headerAfterKey, keyval);
        return (ofs, size);
    }

    private bool TryGetObjectInSave(LiveHeXVersion version, SaveFile sav, string display, int index, byte[]? customdata, out object? sb)
    {
        sb = null;
        if (Remote.Bot.Injector is LPBDSP)
        {
            var prop = sav.GetType().GetProperty(display);
            if (prop is not null)
            {
                sb = prop.GetValue(sav);
            }
            else
            {
                sb = Activator.CreateInstance(LPBDSP.types.First(t => t.Name == display), customdata);
            }
        }
        else
        {
            BlockData[] subblocks = Remote.Bot.Injector switch
            {
                LPBasic => [.. LPBasic.SCBlocks[version].Where(z => z.Display == display)],
                LPPointer => [.. LPPointer.SCBlocks[version].Where(z => z.Display == display)],
                LPFRLG => [.. LPFRLG.SCBlocks[version].Where(z => z.Display == display)],
                _ => [],
            };

            if (subblocks.Length == 0)
                return false;
            if (Remote.Bot.Injector is LPFRLG)
            {
                var prop = sav.GetType().GetProperties().Where(p=> p.Name == subblocks[0].Name) ?? throw new Exception($"{display} not found");
                if (subblocks[0].Name == "LargeBlock")
                    sb = ((SAV3)sav).LargeBlock;
                else
                    sb = ((SAV3)sav).SmallBlock;
                return sb is not null;
            }
            // Check for SCBlocks or SaveBlocks based on name. (SCBlocks will invoke the hex editor, SaveBlocks will invoke a property grid
            var props = sav.GetType().GetProperty("Blocks");
            if (props is null)
                return false;

            var allblocks = props.GetValue(sav) ?? throw new Exception("Blocks not present.");
            var blockprop = allblocks.GetType().GetProperty(subblocks.ToArray()[index].Name);
            if (allblocks is SCBlockAccessor scba && blockprop is null)
            {
                sb = scba.GetBlock(subblocks.ToArray()[index].SCBKey);
            }
            else
            {
                sb = blockprop?.GetValue(allblocks);
            }
        }

        return sb is not null;
    }

    private bool ConnectionValidated(PokeSysBotMini psb, string gameVer, LiveHeXVersion version, LiveHeXValidation validation, string msg)
    {
        if (psb.com is not ICommunicatorNX nx)
        {
            if (msg != "")
            {
                var error = WinFormsUtil.ALMErrorBasic(msg);
                error.ShowDialog();

                var res = error.DialogResult;
                if (res == DialogResult.Retry)
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "https://github.com/santacrab2/PKHeX-Plugins/wiki/FAQ-and-Troubleshooting#troubleshooting",
                        UseShellExecute = true,
                    });
                }

                return false;
            }
            return true;
        }

        var url = validation switch
        {
            LiveHeXValidation.Botbase when nx.Protocol is InjectorCommunicationType.SocketNetwork => "https://github.com/olliz0r/sys-botbase/releases/latest",
            LiveHeXValidation.Botbase when nx.Protocol is InjectorCommunicationType.USB => "https://github.com/Koi-3088/usb-botbase/releases/latest",
            LiveHeXValidation.BlankSAV => "https://github.com/santacrab2/PKHeX-Plugins/wiki/FAQ-and-Troubleshooting#pkhex-plugins-is-telling-me-that-the-detected-game-does-not-match-the-current-save-file-the-top-of-the-window-says-forced-for-the-game-version",
            LiveHeXValidation.GameVersion => "https://github.com/santacrab2/PKHeX-Plugins/wiki/FAQ-and-Troubleshooting#pkhex-plugins-is-telling-me-that-the-detected-game-does-not-match-the-current-save-file-the-top-of-the-window-says-forced-for-the-game-version",
            LiveHeXValidation.RAMShift => "https://github.com/santacrab2/PKHeX-Plugins/wiki/FAQ-and-Troubleshooting#pkhex-plugins-is-telling-me-that-a-possible-ram-shift-is-detected",
            _ => "https://github.com/santacrab2/PKHeX-Plugins/wiki/FAQ-and-Troubleshooting#troubleshooting",
        };

        switch (validation)
        {
            case LiveHeXValidation.Botbase:
                {
                    var error = WinFormsUtil.ALMErrorBasic(msg, true);
                    error.ShowDialog();

                    var res = error.DialogResult;
                    if (res == DialogResult.Retry)
                    {
                        Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
                    }

                    return false;
                }

            case LiveHeXValidation.BlankSAV or LiveHeXValidation.GameVersion:
                {
                    Remote.Bot = new PokeSysBotMini(version, nx);
                    Text += $" SAV/Version (Detected: {gameVer} | Forced: {version})";

                    var error = WinFormsUtil.ALMErrorBasic(msg);
                    error.ShowDialog();

                    var res = error.DialogResult;
                    if (res == DialogResult.Retry)
                    {
                        Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
                    }

                    return false;
                }

            case LiveHeXValidation.RAMShift:
                {
                    Text += $" Possible RAM Shift | Detected: {version}";
                    var error = WinFormsUtil.ALMErrorBasic(msg);
                    error.ShowDialog();

                    var res = error.DialogResult;
                    if (res == DialogResult.Retry)
                    {
                        Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
                    }
                    return false;
                }
        }

        return true;
    }

    private void ConnectionMode_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (initializing)
            return;
        _settings.USBBotBasePreferred = connectionMode.SelectedIndex == 1;
        CurrentInjectionType = _settings.USBBotBasePreferred ? InjectorCommunicationType.USB : InjectorCommunicationType.SocketNetwork;
        Remote = new LiveHeXController(SAV, Editor, CurrentInjectionType);
        // Default Wi-Fi ports for loaded save, 6000 for Switch, 8000 for 3DS
        var default_port = RamOffsets.IsSwitchTitle(SAV.SAV) ? 6000 : 8000;
        TB_Port.ReadOnly = true;
        if (_settings.USBBotBasePreferred)
        {
            // Only use the saved port if using USB-Botbase
            if (int.TryParse(_settings.LatestPort, out int port))
                default_port = port;
            // Allow editing of the port field.
            TB_Port.ReadOnly = false;
        }
        TB_Port.Text = default_port.ToString();
        SetInjectionTypeView();
    }
}

internal class HexTextBox : TextBox
{
    private const int WM_PASTE = 0x0302;

    protected override void WndProc(ref Message m)
    {
        Debug.WriteLine(m.Msg);
        if (m.Msg == WM_PASTE)
        {
            var text = Clipboard.GetText();
            if (text.StartsWith("0x"))
            {
                text = text[2..].Trim();
                Clipboard.SetText(text);
            }
        }

        base.WndProc(ref m);
    }
}


