﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using DereTore.Application.Toolchain.Properties;

namespace DereTore.Application.Toolchain {
    public partial class FMain : Form {

        public FMain() {
            InitializeComponent();
            RegisterEventHandlers();
        }

        ~FMain() {
            UnregisterEventHandlers();
        }

        private void UnregisterEventHandlers() {
            Load -= FMain_Load;
            txtSourceWaveFile.DragDrop -= TxtSourceWaveFile_DragDrop;
            txtSourceWaveFile.DragEnter -= TxtSourceWaveFile_DragEnter;
            txtSourceWaveFile.QueryContinueDrag -= TxtSourceWaveFile_QueryContinueDrag;
            btnBrowseSourceWaveFile.Click -= BtnBrowseSourceWaveFile_Click;
            btnBrowseSaveLocation.Click -= BtnBrowseSaveLocation_Click;
            btnGo.Click -= BtnGo_Click;
        }

        private void RegisterEventHandlers() {
            Load += FMain_Load;
            txtSourceWaveFile.DragDrop += TxtSourceWaveFile_DragDrop;
            txtSourceWaveFile.DragEnter += TxtSourceWaveFile_DragEnter;
            txtSourceWaveFile.QueryContinueDrag += TxtSourceWaveFile_QueryContinueDrag;
            btnBrowseSourceWaveFile.Click += BtnBrowseSourceWaveFile_Click;
            btnBrowseSaveLocation.Click += BtnBrowseSaveLocation_Click;
            btnGo.Click += BtnGo_Click;
        }

        private void BtnBrowseSaveLocation_Click(object sender, EventArgs e) {
            var result = saveFileDialog.ShowDialog();
            if (result != DialogResult.Cancel && saveFileDialog.FileName.Length > 0) {
                txtSaveLocation.Text = saveFileDialog.FileName;
            }
        }

        private void BtnGo_Click(object sender, EventArgs e) {
            if (!CheckBeforeGo()) {
                return;
            }
            btnGo.Enabled = false;
            var thread = new Thread(Go);
            thread.IsBackground = true;
            thread.Start();
        }

        private void TxtSourceWaveFile_DragEnter(object sender, DragEventArgs e) {
            e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private void BtnBrowseSourceWaveFile_Click(object sender, EventArgs e) {
            openFileDialog.FileName = string.Empty;
            var result = openFileDialog.ShowDialog();
            if (result != DialogResult.Cancel && openFileDialog.FileName.Length > 0) {
                txtSourceWaveFile.Text = openFileDialog.FileName;
            }
        }

        private void TxtSourceWaveFile_QueryContinueDrag(object sender, QueryContinueDragEventArgs e) {
            e.Action = DragAction.Continue;
        }

        private void TxtSourceWaveFile_DragDrop(object sender, DragEventArgs e) {
            var dataObject = e.Data as DataObject;
            if (dataObject == null || !dataObject.ContainsFileDropList()) {
                e.Effect = DragDropEffects.None;
                return;
            }
            var fileList = dataObject.GetFileDropList();
            if (fileList.Count > 1) {
                e.Effect = DragDropEffects.None;
                LogError("Please drag a single file.");
                return;
            }
            var fileName = fileList[0];
            if (!fileName.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)) {
                e.Effect = DragDropEffects.None;
                LogError($"The file '{fileName}' is not a wave file.");
                return;
            }
            e.Effect = DragDropEffects.Copy;
            txtSourceWaveFile.Text = fileName;
        }

        private void FMain_Load(object sender, EventArgs e) {
            if (CheckEnvironment()) {
                InitializeControls();
            }
        }

        private void Go() {
            string temp1, temp2;
            int code;
            var startInfo = new ProcessStartInfo();
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;

            Log("Encoding HCA...");
            startInfo.FileName = "hcaenc.exe";
            temp1 = Path.GetTempFileName();
            Log($"Target: {temp1}");
            var waveFileName = txtSourceWaveFile.Text;
            startInfo.Arguments = GetArgsString(SanitizeString(waveFileName), SanitizeString(temp1));
            using (var proc = Process.Start(startInfo)) {
                proc.WaitForExit();
                code = proc.ExitCode;
            }
            if (code != 0) {
                LogError($"hcaenc exited with code {code}.");
                if (File.Exists(temp1)) {
                    File.Delete(temp1);
                }
                EnableGoButton();
                return;
            }
            Log("Encoding finished.");

            Log("Converting HCA...");
            var key1 = txtKey1.Text;
            var key2 = txtKey2.Text;
            if (key1.Length > 0 && key2.Length > 0) {
                startInfo.FileName = "hcacc.exe";
                temp2 = Path.GetTempFileName();
                Log($"Target: {temp2}");
                startInfo.Arguments = GetArgsString(SanitizeString(temp1), SanitizeString(temp2), "-ot", "56", "-o1", key1, "-o2", key2);
                using (var proc = Process.Start(startInfo)) {
                    proc.WaitForExit();
                    code = proc.ExitCode;
                }
                if (code != 0) {
                    LogError($"hcacc exited with code {code}.");
                    if (File.Exists(temp1)) {
                        File.Delete(temp1);
                    }
                    if (File.Exists(temp2)) {
                        File.Delete(temp2);
                    }
                    EnableGoButton();
                    return;
                }
                Log("Conversion finished.");
            } else {
                temp2 = temp1;
                Log("Unnecessary.");
            }

            Log("Packing ACB...");
            startInfo.FileName = "AcbMaker.exe";
            var acbFileName = txtSaveLocation.Text;
            Log($"Target: {acbFileName}");
            var songName = txtSongName.Text;
            startInfo.Arguments = GetArgsString(SanitizeString(temp2), SanitizeString(acbFileName), "-n", songName);
            using (var proc = Process.Start(startInfo)) {
                proc.WaitForExit();
                code = proc.ExitCode;
            }
            if (code != 0) {
                LogError($"AcbMaker exited with code {code}.");
                if (File.Exists(temp1)) {
                    File.Delete(temp1);
                }
                if (File.Exists(temp2)) {
                    File.Delete(temp2);
                }
                EnableGoButton();
                return;
            }
            Log("ACB packing finished.");
            if (File.Exists(temp1)) {
                File.Delete(temp1);
            }
            if (File.Exists(temp2)) {
                File.Delete(temp2);
            }
            EnableGoButton();
        }

        private void EnableGoButton() {
            if (InvokeRequired) {
                Invoke(_enableGoDelegate);
            } else {
                btnGo.Enabled = true;
            }
        }

        private void DisableControls() {
            foreach (Control control in Controls) {
                var textBox = control as TextBox;
                if (textBox != null) {
                    if (!textBox.ReadOnly) {
                        textBox.Enabled = false;
                    }
                } else {
                    control.Enabled = false;
                }
            }
        }

        private bool CheckEnvironment() {
            Log("Checking environment...");
            _logDelegate = Log;
            _enableGoDelegate = EnableGoButton;
            var criticalFiles = new[] { "hcaenc.exe", "hcacc.exe", "AcbMaker.exe", "hcaenc_lite.dll" };
            var missingFiles = criticalFiles.Where(s => !File.Exists(s)).ToList();
            if (missingFiles.Count > 0) {
                DisableControls();
                LogError("Missing file(s): " + missingFiles.BuildString());
                return false;
            } else {
                Log("Environment is OK.");
                return true;
            }
        }

        private bool CheckBeforeGo() {
            if (txtSourceWaveFile.TextLength == 0) {
                LogError("Please specify the source file.");
                return false;
            }
            if (txtSaveLocation.TextLength == 0) {
                LogError("Please specify the save location.");
                return false;
            }
            if (txtSongName.TextLength == 0) {
                LogError("Please enter the song name.");
                return false;
            }
            foreach (var c in txtSongName.Text) {
                if (c > 127) {
                    LogError("Unsupported song name. Please make sure the name consists only ASCII characters.");
                    return false;
                }
            }
            var keyRegex = new Regex(@"^[0-9A-Fa-f]{8}$", RegexOptions.CultureInvariant);
            if (txtKey1.TextLength > 0) {
                if (!keyRegex.IsMatch(txtKey1.Text)) {
                    LogError("Format of key #1 is invalid.");
                    return false;
                }
            }
            if (txtKey2.TextLength > 0) {
                if (!keyRegex.IsMatch(txtKey2.Text)) {
                    LogError("Format of key #2 is invalid.");
                    return false;
                }
            }
            return true;
        }

        private void InitializeControls() {
            txtKey1.Text = CgssKey1.ToString("x8");
            txtKey2.Text = CgssKey2.ToString("x8");
            txtSongName.Text = DefaultSongName;
            txtSourceWaveFile.AllowDrop = true;
            openFileDialog.CheckFileExists = true;
            openFileDialog.CheckPathExists = true;
            openFileDialog.Multiselect = false;
            openFileDialog.AutoUpgradeEnabled = true;
            openFileDialog.DereferenceLinks = true;
            openFileDialog.ShowReadOnly = false;
            openFileDialog.ReadOnlyChecked = false;
            saveFileDialog.ValidateNames = true;
            openFileDialog.Filter = Resources.BrowseForWaveFilter;
            saveFileDialog.AutoUpgradeEnabled = true;
            saveFileDialog.CreatePrompt = true;
            saveFileDialog.OverwritePrompt = true;
            saveFileDialog.ValidateNames = true;
            saveFileDialog.Filter = Resources.BrowseForAcbFilter;
        }

        private void Log(string message) {
            if (InvokeRequired) {
                Invoke(_logDelegate, message);
            } else {
                txtLog.AppendText(message);
                txtLog.AppendText(Environment.NewLine);
                txtLog.SelectionStart = txtLog.TextLength;
            }
        }

        private void LogError(string message) {
            Log("[ERROR] " + message);
        }

        private static string GetArgsString(params string[] args) {
            return args.Aggregate((total, next) => total + " " + next);
        }

        private static string SanitizeString(string s) {
            var shouldCoverWithQuotes = false;
            if (s.IndexOf('"') >= 0) {
                s = s.Replace("\"", "\"\"\"");
                shouldCoverWithQuotes = true;
            }
            if (s.IndexOfAny(CommandlineEscapeChars) >= 0) {
                shouldCoverWithQuotes = true;
            }
            if (s.Any(c => c > 127)) {
                shouldCoverWithQuotes = true;
            }
            return shouldCoverWithQuotes ? "\"" + s + "\"" : s;
        }

        private static readonly uint CgssKey1 = 0xF27E3B22;
        private static readonly uint CgssKey2 = 0x00003657;
        private static readonly string DefaultSongName = "song_1001";
        private static readonly char[] CommandlineEscapeChars = { ' ', '&', '%', '#', '@', '!', ',', '~', '+', '=', '(', ')' };
        private Action<string> _logDelegate;
        private Action _enableGoDelegate;

    }
}
