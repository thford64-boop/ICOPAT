using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace IconPatcher
{
    public class MainForm : Form
    {
        // ── Controls ─────────────────────────────────────────────────────────
        private Panel       pnlTop;
        private Label       lblTitle;
        private Label       lblSub;
        private Panel       pnlBody;
        private Panel       pnlLeft;
        private Panel       pnlRight;
        private Panel       pnlExeDrop;
        private Label       lblExeDrop;
        private ListBox     lstExes;
        private Button      btnAddExe;
        private Button      btnRemoveExe;
        private Panel       pnlIcoDrop;
        private Label       lblIcoDrop;
        private PictureBox  picIcon;
        private Label       lblIconName;
        private Button      btnPickIco;
        private Panel       pnlBottom;
        private Button      btnPatch;
        private Button      btnClear;
        private RichTextBox txtLog;
        private ProgressBar prog;
        private Label       lblStatus;

        // ── State ─────────────────────────────────────────────────────────────
        private List<string> exePaths  = new List<string>();
        private string       icoPath   = null;

        // ── Colors ───────────────────────────────────────────────────────────
        private static readonly Color C_BG       = Color.FromArgb(18,  18,  22);
        private static readonly Color C_SURFACE  = Color.FromArgb(28,  28,  34);
        private static readonly Color C_BORDER   = Color.FromArgb(55,  55,  65);
        private static readonly Color C_ACCENT   = Color.FromArgb(100, 180, 255);
        private static readonly Color C_TEXT     = Color.FromArgb(220, 220, 230);
        private static readonly Color C_MUTED    = Color.FromArgb(120, 120, 140);
        private static readonly Color C_OK       = Color.FromArgb(100, 220, 130);
        private static readonly Color C_ERR      = Color.FromArgb(255, 100, 100);
        private static readonly Color C_WARN     = Color.FromArgb(255, 195, 80);
        private static readonly Color C_BTN      = Color.FromArgb(40,  40,  50);
        private static readonly Color C_BTNHOV   = Color.FromArgb(55,  55,  70);
        private static readonly Color C_PATCH    = Color.FromArgb(60,  130, 220);
        private static readonly Color C_PATCHHOV = Color.FromArgb(80,  155, 255);

        public MainForm()
        {
            BuildUI();
            SetupDragDrop();
            Log("Ready. Drop EXE/SCR files and an ICO/PNG image, then click Patch.", C_MUTED);
        }

        // ── UI construction ───────────────────────────────────────────────────

        private void BuildUI()
        {
            SuspendLayout();

            Text            = "EXE Icon Patcher";
            MinimumSize     = new Size(780, 580);
            Size            = new Size(860, 640);
            StartPosition   = FormStartPosition.CenterScreen;
            BackColor       = C_BG;
            ForeColor       = C_TEXT;
            Font            = new Font("Segoe UI", 9f);
            FormBorderStyle = FormBorderStyle.Sizable;

            // ── Header ────────────────────────────────────────────────────────
            pnlTop = Panel("pnlTop", 0, 0, ClientSize.Width, 64, C_SURFACE, AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right);
            pnlTop.Paint += DrawBottomBorder;

            lblTitle = Lbl("EXE  ICON  PATCHER", 20, 10, 400, 28, C_ACCENT, new Font("Segoe UI", 15f, FontStyle.Bold));
            lblSub   = Lbl("Drop files below — ICO, PNG, JPG, BMP all supported. Multi-file batch patching.", 20, 38, 700, 18, C_MUTED);

            pnlTop.Controls.Add(lblTitle);
            pnlTop.Controls.Add(lblSub);
            Controls.Add(pnlTop);

            // ── Body (two columns) ────────────────────────────────────────────
            pnlBody = Panel("pnlBody", 0, 64, ClientSize.Width, ClientSize.Height - 64 - 160,
                            C_BG, AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right);
            pnlBody.Padding = new Padding(14);
            Controls.Add(pnlBody);

            // Left column — EXE list
            pnlLeft = Panel("pnlLeft", 14, 14, 380, pnlBody.Height - 28, C_BG,
                             AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left);
            pnlBody.Controls.Add(pnlLeft);

            var lblExeHead = Lbl("TARGET FILES  (EXE · SCR · DLL · any PE)", 0, 0, 380, 20, C_MUTED);
            lblExeHead.Font = new Font("Segoe UI", 7.5f, FontStyle.Bold);
            pnlLeft.Controls.Add(lblExeHead);

            pnlExeDrop = Panel("pnlExeDrop", 0, 24, 380, 80, C_SURFACE, AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right);
            pnlExeDrop.Paint   += DrawDashedBorder;
            pnlExeDrop.Cursor   = Cursors.Hand;
            pnlExeDrop.Click   += (s, e) => BrowseExes();
            pnlLeft.Controls.Add(pnlExeDrop);

            lblExeDrop = Lbl("Drop EXE / SCR files here  —  or click to browse", 0, 0, 380, 80, C_MUTED);
            lblExeDrop.TextAlign = ContentAlignment.MiddleCenter;
            lblExeDrop.Cursor    = Cursors.Hand;
            lblExeDrop.Click    += (s, e) => BrowseExes();
            pnlExeDrop.Controls.Add(lblExeDrop);

            lstExes = new ListBox
            {
                Name          = "lstExes",
                Left          = 0,
                Top           = 112,
                Width         = 380,
                BackColor     = C_SURFACE,
                ForeColor     = C_TEXT,
                BorderStyle   = BorderStyle.None,
                SelectionMode = SelectionMode.MultiExtended,
                Anchor        = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            lstExes.Height = pnlLeft.Height - 150;
            pnlLeft.Controls.Add(lstExes);

            var btnRow = Panel("btnRow", 0, 108, 380, 28, C_BG, AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right);
            pnlLeft.Controls.Add(btnRow);

            btnAddExe    = SmallBtn("+ Add", 0, 0);
            btnRemoveExe = SmallBtn("− Remove", 60, 0);
            btnAddExe.Click    += (s, e) => BrowseExes();
            btnRemoveExe.Click += (s, e) => RemoveSelected();
            btnRow.Controls.Add(btnAddExe);
            btnRow.Controls.Add(btnRemoveExe);

            // Right column — Icon picker
            pnlRight = Panel("pnlRight", 408, 14, 0, pnlBody.Height - 28, C_BG,
                              AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right);
            pnlRight.Left   = 408;
            pnlRight.Width  = pnlBody.Width - 422;
            pnlBody.Controls.Add(pnlRight);

            var lblIcoHead = Lbl("ICON FILE  (ICO · PNG · JPG · BMP · SVG)", 0, 0, 360, 20, C_MUTED);
            lblIcoHead.Font = new Font("Segoe UI", 7.5f, FontStyle.Bold);
            pnlRight.Controls.Add(lblIcoHead);

            pnlIcoDrop = Panel("pnlIcoDrop", 0, 24, pnlRight.Width, 160, C_SURFACE,
                                AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right);
            pnlIcoDrop.Paint  += DrawDashedBorder;
            pnlIcoDrop.Cursor  = Cursors.Hand;
            pnlIcoDrop.Click  += (s, e) => BrowseIco();
            pnlRight.Controls.Add(pnlIcoDrop);

            picIcon = new PictureBox
            {
                Name        = "picIcon",
                Left        = 0, Top = 0,
                Width       = pnlIcoDrop.Width,
                Height      = 160,
                SizeMode    = PictureBoxSizeMode.Zoom,
                BackColor   = Color.Transparent,
                Cursor      = Cursors.Hand,
                Anchor      = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            picIcon.Click += (s, e) => BrowseIco();
            pnlIcoDrop.Controls.Add(picIcon);

            lblIcoDrop = Lbl("Drop ICO / PNG / JPG / BMP here  —  or click to browse", 0, 0, pnlIcoDrop.Width, 160, C_MUTED);
            lblIcoDrop.TextAlign = ContentAlignment.MiddleCenter;
            lblIcoDrop.Cursor    = Cursors.Hand;
            lblIcoDrop.Click    += (s, e) => BrowseIco();
            pnlIcoDrop.Controls.Add(lblIcoDrop);

            lblIconName = Lbl("No icon selected.", 0, 192, pnlRight.Width, 20, C_MUTED);
            pnlRight.Controls.Add(lblIconName);

            btnPickIco = new Button
            {
                Text      = "Browse for icon…",
                Left      = 0, Top = 218,
                Width     = 160, Height = 30,
                FlatStyle = FlatStyle.Flat,
                BackColor = C_BTN,
                ForeColor = C_TEXT,
                Cursor    = Cursors.Hand
            };
            btnPickIco.FlatAppearance.BorderColor = C_BORDER;
            btnPickIco.Click += (s, e) => BrowseIco();
            pnlRight.Controls.Add(btnPickIco);

            // ── Bottom panel ───────────────────────────────────────────────────
            pnlBottom = Panel("pnlBottom", 0, ClientSize.Height - 160, ClientSize.Width, 160, C_SURFACE,
                               AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right);
            pnlBottom.Paint += DrawTopBorder;
            Controls.Add(pnlBottom);

            txtLog = new RichTextBox
            {
                Left       = 14, Top = 10,
                Width      = ClientSize.Width - 200,
                Height     = 100,
                BackColor  = C_BG,
                ForeColor  = C_MUTED,
                BorderStyle= BorderStyle.None,
                ReadOnly   = true,
                Font       = new Font("Cascadia Mono", 8f),
                Anchor     = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            pnlBottom.Controls.Add(txtLog);

            prog = new ProgressBar
            {
                Left   = 14, Top = 118,
                Width  = ClientSize.Width - 200,
                Height = 4,
                Style  = ProgressBarStyle.Continuous,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            pnlBottom.Controls.Add(prog);

            lblStatus = Lbl("Idle", 14, 126, 400, 18, C_MUTED);
            pnlBottom.Controls.Add(lblStatus);

            // Right-side action buttons
            btnPatch = BigBtn("PATCH  ALL", ClientSize.Width - 180, 14, C_PATCH, C_PATCHHOV);
            btnClear = BigBtn("Clear list",   ClientSize.Width - 180, 74, C_BTN, C_BTNHOV);
            btnPatch.Click += (s, e) => DoPatch();
            btnClear.Click += (s, e) => ClearAll();
            pnlBottom.Controls.Add(btnPatch);
            pnlBottom.Controls.Add(btnClear);

            ResumeLayout();
        }

        // ── Drag-and-drop ─────────────────────────────────────────────────────

        private void SetupDragDrop()
        {
            void AllowDrop(Control c, bool exe)
            {
                c.AllowDrop = true;
                c.DragEnter += (s, e) => {
                    if (e.Data.GetDataPresent(DataFormats.FileDrop))
                        e.Effect = DragDropEffects.Copy;
                };
                c.DragDrop += (s, e) => {
                    var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                    if (exe) foreach (var f in files) AddExe(f);
                    else if (files.Length > 0) SetIco(files[0]);
                };
                if (exe)
                {
                    // Also allow ico drops on exe zone
                    c.DragDrop += (s, e) => {
                        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                        foreach (var f in files)
                        {
                            var ext = Path.GetExtension(f).ToLower();
                            if (ext == ".ico" || ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp")
                                SetIco(f);
                        }
                    };
                }
            }

            AllowDrop(pnlExeDrop,  true);
            AllowDrop(lblExeDrop,  true);
            AllowDrop(lstExes,     true);
            AllowDrop(pnlIcoDrop,  false);
            AllowDrop(lblIcoDrop,  false);
            AllowDrop(picIcon,     false);

            // Also allow drops on the main form
            this.AllowDrop = true;
            DragEnter += (s, e) => { if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; };
            DragDrop  += (s, e) => {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                foreach (var f in files)
                {
                    var ext = Path.GetExtension(f).ToLower();
                    if (ext == ".ico" || ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp")
                        SetIco(f);
                    else
                        AddExe(f);
                }
            };
        }

        // ── Logic ─────────────────────────────────────────────────────────────

        private void AddExe(string path)
        {
            if (!File.Exists(path)) return;
            if (exePaths.Contains(path))
            {
                Log($"Already in list: {Path.GetFileName(path)}", C_WARN);
                return;
            }
            exePaths.Add(path);
            lstExes.Items.Add(Path.GetFileName(path));
            Log($"Added: {Path.GetFileName(path)}  ({ToKb(new FileInfo(path).Length)})", C_TEXT);
        }

        private void SetIco(string path)
        {
            if (!File.Exists(path)) return;
            icoPath     = path;
            lblIcoDrop.Visible  = false;
            lblIconName.Text    = $"{Path.GetFileName(path)}  ({ToKb(new FileInfo(path).Length)})";
            lblIconName.ForeColor = C_OK;

            try
            {
                picIcon.Image = Image.FromFile(path);
            }
            catch
            {
                picIcon.Image = null;
                lblIcoDrop.Visible = true;
                lblIcoDrop.Text = Path.GetFileName(path) + "  (preview N/A)";
            }

            Log($"Icon set: {Path.GetFileName(path)}", C_ACCENT);
        }

        private void BrowseExes()
        {
            using var dlg = new OpenFileDialog
            {
                Title     = "Select target files",
                Filter    = "Executable files|*.exe;*.scr;*.dll;*.com|All files|*.*",
                Multiselect = true
            };
            if (dlg.ShowDialog() == DialogResult.OK)
                foreach (var f in dlg.FileNames) AddExe(f);
        }

        private void BrowseIco()
        {
            using var dlg = new OpenFileDialog
            {
                Title  = "Select icon or image",
                Filter = "Icon / Image|*.ico;*.png;*.jpg;*.jpeg;*.bmp|All files|*.*"
            };
            if (dlg.ShowDialog() == DialogResult.OK)
                SetIco(dlg.FileName);
        }

        private void RemoveSelected()
        {
            // Remove in reverse index order to keep indices stable
            var selected = new List<int>();
            foreach (int i in lstExes.SelectedIndices) selected.Add(i);
            selected.Sort();
            selected.Reverse();
            foreach (int i in selected)
            {
                Log($"Removed: {exePaths[i]}", C_MUTED);
                exePaths.RemoveAt(i);
                lstExes.Items.RemoveAt(i);
            }
        }

        private void ClearAll()
        {
            exePaths.Clear();
            lstExes.Items.Clear();
            icoPath = null;
            picIcon.Image = null;
            lblIcoDrop.Visible = true;
            lblIcoDrop.Text    = "Drop ICO / PNG / JPG / BMP here  —  or click to browse";
            lblIconName.Text   = "No icon selected.";
            lblIconName.ForeColor = C_MUTED;
            txtLog.Clear();
            prog.Value = 0;
            lblStatus.Text = "Idle";
            Log("Cleared.", C_MUTED);
        }

        private async void DoPatch()
        {
            if (exePaths.Count == 0) { Warn("No target files added."); return; }
            if (icoPath == null)     { Warn("No icon file selected."); return; }

            btnPatch.Enabled = false;
            prog.Maximum = exePaths.Count;
            prog.Value   = 0;

            int ok = 0, fail = 0;

            for (int i = 0; i < exePaths.Count; i++)
            {
                string exe = exePaths[i];
                string name = Path.GetFileName(exe);
                lblStatus.Text = $"Patching {i + 1} / {exePaths.Count}:  {name}";
                Application.DoEvents();

                // Back up the file first
                string backup = exe + ".bak";
                try { File.Copy(exe, backup, true); }
                catch { /* backup failed — continue anyway */ }

                string err = IconEngine.PatchIcon(exe, icoPath);

                if (err == null)
                {
                    Log($"✓  {name}", C_OK);
                    ok++;
                }
                else
                {
                    Log($"✗  {name}  —  {err}", C_ERR);
                    // Restore backup if we made one
                    try { if (File.Exists(backup)) File.Copy(backup, exe, true); } catch { }
                    fail++;
                }

                // Clean up backup on success
                try { if (err == null && File.Exists(backup)) File.Delete(backup); } catch { }

                prog.Value = i + 1;
                await System.Threading.Tasks.Task.Delay(30);
            }

            lblStatus.Text = $"Done — {ok} succeeded, {fail} failed.";
            btnPatch.Enabled = true;

            if (fail == 0)
                Log($"\nAll {ok} file(s) patched successfully!", C_OK);
            else
                Log($"\n{ok} succeeded, {fail} failed. Failed files restored from .bak backups.", C_WARN);
        }

        private void Warn(string msg)
        {
            Log("⚠  " + msg, C_WARN);
            lblStatus.Text = msg;
        }

        private void Log(string msg, Color col)
        {
            txtLog.SelectionStart  = txtLog.TextLength;
            txtLog.SelectionLength = 0;
            txtLog.SelectionColor  = col;
            txtLog.AppendText(msg + "\n");
            txtLog.ScrollToCaret();
        }

        // ── Helper builders ───────────────────────────────────────────────────

        private Panel Panel(string name, int x, int y, int w, int h, Color bg, AnchorStyles anchor)
        {
            return new Panel
            {
                Name      = name,
                Left      = x, Top = y,
                Width     = w, Height = h,
                BackColor = bg,
                Anchor    = anchor
            };
        }

        private Label Lbl(string text, int x, int y, int w, int h, Color col, Font font = null)
        {
            var l = new Label
            {
                Text      = text,
                Left      = x, Top = y,
                Width     = w, Height = h,
                ForeColor = col,
                BackColor = Color.Transparent,
                Font      = font ?? this.Font
            };
            return l;
        }

        private Button SmallBtn(string text, int x, int y)
        {
            var b = new Button
            {
                Text      = text,
                Left      = x, Top = y,
                Width     = 55, Height = 24,
                FlatStyle = FlatStyle.Flat,
                BackColor = C_BTN,
                ForeColor = C_TEXT,
                Font      = new Font("Segoe UI", 8f),
                Cursor    = Cursors.Hand
            };
            b.FlatAppearance.BorderColor = C_BORDER;
            b.MouseEnter += (s, e) => b.BackColor = C_BTNHOV;
            b.MouseLeave += (s, e) => b.BackColor = C_BTN;
            return b;
        }

        private Button BigBtn(string text, int x, int y, Color col, Color hov)
        {
            var b = new Button
            {
                Text      = text,
                Left      = x, Top = y,
                Width     = 160, Height = 46,
                FlatStyle = FlatStyle.Flat,
                BackColor = col,
                ForeColor = Color.White,
                Font      = new Font("Segoe UI", 10f, FontStyle.Bold),
                Cursor    = Cursors.Hand,
                Anchor    = AnchorStyles.Top | AnchorStyles.Right
            };
            b.FlatAppearance.BorderSize  = 0;
            b.MouseEnter += (s, e) => b.BackColor = hov;
            b.MouseLeave += (s, e) => b.BackColor = col;
            return b;
        }

        private void DrawDashedBorder(object sender, System.Windows.Forms.PaintEventArgs e)
        {
            var c = (Control)sender;
            using var pen = new Pen(C_BORDER, 1) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
            e.Graphics.DrawRectangle(pen, 0, 0, c.Width - 1, c.Height - 1);
        }

        private void DrawBottomBorder(object sender, System.Windows.Forms.PaintEventArgs e)
        {
            var c = (Control)sender;
            using var pen = new Pen(C_BORDER, 1);
            e.Graphics.DrawLine(pen, 0, c.Height - 1, c.Width, c.Height - 1);
        }

        private void DrawTopBorder(object sender, System.Windows.Forms.PaintEventArgs e)
        {
            using var pen = new Pen(C_BORDER, 1);
            e.Graphics.DrawLine(pen, 0, 0, ((Control)sender).Width, 0);
        }

        private static string ToKb(long bytes) => $"{bytes / 1024.0:F1} KB";

        
    }
}
