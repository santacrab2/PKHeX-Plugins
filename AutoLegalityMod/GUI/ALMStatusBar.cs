using System;
using System.Windows.Forms;

namespace AutoModPlugins.GUI;

public partial class ALMStatusBar : Form
{
    private int _count;
    private int _maxTasks;
    public bool closedbyuser = true;
    public int Count
    {
        get => _count;
        set
        {
            _count = value;
            pb_status.Value = Math.Min(_count, pb_status.Maximum);
            L_status.Text = $"{_count}/{_maxTasks} completed";
        }
    }

    public ALMStatusBar(string title, int amountOftasks)
    {
        InitializeComponent();
        this.Text = title;
        this.FormClosing += ALMStatusBar_FormClosing;
        _maxTasks = amountOftasks;
        pb_status.Maximum = amountOftasks;
        Count = 0;
    }

    private void ALMStatusBar_FormClosing(object? sender, EventArgs e)
    {
        if (e is FormClosingEventArgs fcea && closedbyuser)
        {
            var prompt = WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Are you sure you want to cancel the Living Dex generation?");
            if (prompt != DialogResult.Yes)
            {
                fcea.Cancel = true;
                return;
            }
            LivingDex.cts?.Cancel();

        }
    }
}