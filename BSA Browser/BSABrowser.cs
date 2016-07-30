﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Management.Automation;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using BSA_Browser.Classes;
using BSA_Browser.Controls;
using BSA_Browser.Extensions;
using BSA_Browser.Properties;
using SharpBSABA2;

namespace BSA_Browser
{
    public enum ArchiveFileSortOrder
    {
        FolderName,
        FileName,
        FileSize,
        Offset,
        FileType
    }

    public partial class BSABrowser : Form
    {
        string _untouchedTitle;
        OpenFolderDialog _openFolderDialog = new OpenFolderDialog();
        ColumnHeader[] _extraColumns;
        List<ArchiveEntry> _files = new List<ArchiveEntry>();
        ArchiveFileSorter _filesSorter = new ArchiveFileSorter();
        Timer _searchDelayTimer;

        public BSABrowser()
        {
            InitializeComponent();

            // Show application version in title
            this.Text += $" ({Program.GetVersion()})";

            // Store title so it can be restored later,
            // for example when showing the extraction progress in title
            _untouchedTitle = this.Text;

            lvFiles.ContextMenu = contextMenu1;

            if (Settings.Default.UpdateSettings)
            {
                Settings.Default.Upgrade();
                Settings.Default.UpdateSettings = false;
                Settings.Default.Save();
            }

            // Restore last path for OpenFolderDialog
            if (!string.IsNullOrEmpty(Settings.Default.LastUnpackPath))
                _openFolderDialog.InitialFolder = Settings.Default.LastUnpackPath;

            // Load Recent Files list
            if (Settings.Default.RecentFiles != null)
            {
                foreach (string item in Settings.Default.RecentFiles)
                    AddToRecentFiles(item);
            }

            // Load Quick Extract Paths
            if (Settings.Default.QuickExtractPaths == null)
                Settings.Default.QuickExtractPaths = new QuickExtractPaths();

            this.LoadQuickExtractPaths();

            // Set lvFiles sorter
            ArchiveFileSorter.SetSorter(Settings.Default.SortType, Settings.Default.SortDesc);

            // Toggle columns based on setting
            this.UpdateColumns();

            // Enable visual styles
            tvFolders.EnableVisualStyles();
            tvFolders.EnableAutoScroll();

            lvFiles.EnableVisualStyles();
            lvFiles.EnableVisualStylesSelection();
            lvFiles.HideFocusRectangle();

            // Set TextBox cue
            txtSearch.SetCue("Search term...");
        }

        public BSABrowser(string[] args)
            : this()
        {
            foreach (string file in args)
                OpenArchive(file, true);
        }

        private void BSABrowser_Load(object sender, EventArgs e)
        {
            // Initialize WindowStates if null
            if (Settings.Default.WindowStates == null)
            {
                Settings.Default.WindowStates = new WindowStates();
            }

            // Add this form if it doesn't exists
            if (!Settings.Default.WindowStates.Contains(this.Name))
            {
                Settings.Default.WindowStates.Add(this.Name);
            }

            // Restore window state
            Settings.Default.WindowStates[this.Name].RestoreForm(this);

            // Restore sorting preferences
            cmbSortOrder.SelectedIndex = (int)Settings.Default.SortType;
            cbDesc.Checked = Settings.Default.SortDesc;

            // Restore Regex preference
            cbRegex.Checked = Settings.Default.SearchUseRegex;

            // Show ! in main menu if update is available
            this.ShowUpdateNotification();
        }

        private void BSABrowser_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (tvFolders.GetNodeCount(false) > 0)
                CloseArchives();

            SaveRecentFiles();

            Settings.Default.WindowStates[this.Name].SaveForm(this);
            Settings.Default.LastUnpackPath = _openFolderDialog.Folder;
            Settings.Default.Save();
        }

        private void btnOpen_Click(object sender, EventArgs e)
        {
            if (OpenArchiveDialog.ShowDialog() == DialogResult.OK)
                this.OpenArchives(true, OpenArchiveDialog.FileNames);
        }

        private void btnExtract_Click(object sender, EventArgs e)
        {
            if (lvFiles.SelectedIndices.Count == 0)
                return;

            if (_openFolderDialog.ShowDialog(this) == DialogResult.OK)
            {
                var files = new List<ArchiveEntry>();

                foreach (int index in lvFiles.SelectedIndices)
                    files.Add(_files[index]);

                bool useFolderPath = Settings.Default.ExtractMaintainFolderStructure;

                ExtractFiles(_openFolderDialog.Folder, useFolderPath, true, files.ToArray());
            }
        }

        private void btnExtractAll_Click(object sender, EventArgs e)
        {
            if (tvFolders.SelectedNode == null)
                return;

            if (_openFolderDialog.ShowDialog(this) == DialogResult.OK)
            {
                ExtractFiles(_openFolderDialog.Folder, true, true, GetSelectedArchiveNode().Archive.Files.ToArray());
            }
        }

        private void btnPreview_Click(object sender, EventArgs e)
        {
            if (lvFiles.SelectedIndices.Count == 0)
                return;

            if (lvFiles.SelectedIndices.Count == 1)
            {
                var fe = _files[lvFiles.SelectedIndices[0]];

                switch (Path.GetExtension(fe.LowerPath))
                {
                    /*case ".nif":
                        MessageBox.Show("Viewing of nif's disabled as their format differs from oblivion");
                        return;
                    case ".dds":
                    case ".tga":
                    case ".bmp":
                    case ".jpg":
                        System.Diagnostics.Process.Start("obmm\\NifViewer.exe", fe.LowerName);
                        break;*/
                    case ".lst":
                    case ".txt":
                    case ".xml":
                        string dest = Program.CreateTempDirectory();

                        fe.Extract(dest, false);
                        System.Diagnostics.Process.Start(Path.Combine(dest, fe.FileName));
                        break;
                    default:
                        MessageBox.Show("Filetype not supported.\n" +
                            "Currently only txt or xml files can be previewed", "Error");
                        break;
                }
            }
            else
            {
                MessageBox.Show("Can only preview one file at a time", "Error");
            }
        }

        private void cmbSortOrder_SelectedIndexChanged(object sender, EventArgs e)
        {
            ArchiveFileSorter.SetSorter((ArchiveFileSortOrder)cmbSortOrder.SelectedIndex, cbDesc.Checked);
            lvFiles.BeginUpdate();
            _files.Sort(_filesSorter);
            lvFiles.EndUpdate();
            Settings.Default.SortType = (ArchiveFileSortOrder)cmbSortOrder.SelectedIndex;
        }

        private void cbDesc_CheckedChanged(object sender, EventArgs e)
        {
            ArchiveFileSorter.SetSorter((ArchiveFileSortOrder)cmbSortOrder.SelectedIndex, cbDesc.Checked);
            lvFiles.BeginUpdate();
            _files.Sort(_filesSorter);
            lvFiles.EndUpdate();
            Settings.Default.SortDesc = cbDesc.Checked;
        }

        private void lvFiles_Enter(object sender, EventArgs e)
        {
            lvFiles.HideFocusRectangle();
        }

        private void lvFiles_ItemDrag(object sender, ItemDragEventArgs e)
        {
            if (!(lvFiles.SelectedIndices.Count >= 1))
                return;

            DataObject obj = new DataObject();
            StringCollection sc = new StringCollection();

            foreach (int index in lvFiles.SelectedIndices)
            {
                var fe = _files[index];
                string dest = Program.CreateTempDirectory();

                fe.Extract(dest, false);
                sc.Add(Path.Combine(dest, fe.FileName));
            }

            obj.SetFileDropList(sc);
            lvFiles.DoDragDrop(obj, DragDropEffects.Move);
        }

        private void lvFiles_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.A)
            {
                lvFiles.SelectAllItems();
            }
        }

        private void lvFiles_SelectedIndexChanged(object sender, EventArgs e)
        {
            lvFiles.HideFocusRectangle();
        }

        private void lvFiles_RetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
        {
            if (_files.Count <= e.ItemIndex)
                return;

            var file = _files[e.ItemIndex];
            var lvi = new ListViewItem(Path.Combine(file.Folder, file.FileName));

            lvi.SubItems.Add(FormatBytes(file.Size));
            lvi.SubItems.Add(file.Offset.ToString());
            lvi.SubItems.Add((file.Compressed ? "Compressed" : "Uncompressed"));
            lvi.Tag = file;
            lvi.ToolTipText =
                $"File size: {FormatBytes(file.Size)}\n" +
                $"File offset: {file.Offset} bytes\n" +
                (file.Compressed ? "Compressed" : "Uncompressed");

            e.Item = lvi;
        }

        private void txtSearch_TextChanged(object sender, EventArgs e)
        {
            if (_searchDelayTimer == null)
            {
                _searchDelayTimer = new Timer();
                _searchDelayTimer.Tick += delegate { Search(); };
                _searchDelayTimer.Interval = 500;
            }

            _searchDelayTimer.Stop();
            _searchDelayTimer.Start();
        }

        private void cbRegex_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.SearchUseRegex = cbRegex.Checked;
            this.Search();
        }

        private void tvFolders_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            if (GetRootNode(e.Node).AllFiles != null)
                return;

            e.Node.Nodes.Clear();
            Dictionary<string, TreeNode> nodes = new Dictionary<string, TreeNode>();
            GetRootNode(e.Node).AllFiles = (ArchiveEntry[])GetRootNode(e.Node).Files.Clone();

            foreach (ArchiveEntry lvi in GetRootNode(e.Node).AllFiles)
            {
                string path = Path.GetDirectoryName(lvi.FullPath);

                if (path == string.Empty || nodes.ContainsKey(path))
                    continue;

                string[] dirs = path.Split('\\');

                for (int i = 0; i < dirs.Length; i++)
                {
                    string newpath = string.Join("\\", dirs, 0, i + 1);

                    if (!nodes.ContainsKey(newpath))
                    {
                        TreeNode tn = new TreeNode(dirs[i]);
                        tn.Tag = newpath;

                        if (i == 0)
                            e.Node.Nodes.Add(tn);
                        else
                            nodes[path].Nodes.Add(tn);

                        nodes.Add(newpath, tn);
                    }
                    path = newpath;
                }
            }

            if (Settings.Default.SortArchiveDirectories)
            {
                this.SortNodes(e.Node);
            }
        }

        private void tvFolders_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (GetRootNode(e.Node).AllFiles == null)
                tvFolders_BeforeExpand(null, new TreeViewCancelEventArgs(e.Node, false, TreeViewAction.Unknown));

            string s = (string)e.Node.Tag;

            if (s == null)
                GetRootNode(e.Node).Files = GetRootNode(e.Node).AllFiles;
            else
            {
                var lvis = new List<ArchiveEntry>(GetRootNode(e.Node).AllFiles.Length);

                foreach (var lvi in GetRootNode(e.Node).AllFiles)
                    if (lvi.FullPath.StartsWith(s)) lvis.Add(lvi);

                GetRootNode(e.Node).Files = lvis.ToArray();
            }
            this.Search();
        }

        #region mainMenu1

        private void openArchiveMenuItem_Click(object sender, EventArgs e)
        {
            if (OpenArchiveDialog.ShowDialog() == DialogResult.OK)
                this.OpenArchives(true, OpenArchiveDialog.FileNames);
        }

        private void closeSelectedArchiveMenuItem_Click(object sender, EventArgs e)
        {
            if (GetSelectedArchiveNode() == null)
                return;

            CloseArchive(GetSelectedArchiveNode());
        }

        private void optionsMenuItem_Click(object sender, EventArgs e)
        {
            using (var of = new OptionsForm())
            {
                if (of.ShowDialog(this) == DialogResult.OK)
                {
                    of.SaveChanges();
                    Settings.Default.Save();

                    // Sync changes to UI
                    this.LoadQuickExtractPaths();
                    this.UpdateColumns();
                }
            }
        }

        private void recentFilesMenuItem_Popup(object sender, EventArgs e)
        {
            if (recentFilesMenuItem.MenuItems.Count > 2)
                emptyListMenuItem.Enabled = true;
            else
                emptyListMenuItem.Enabled = false;
        }

        private void emptyListMenuItem_Click(object sender, EventArgs e)
        {
            for (int i = recentFilesMenuItem.MenuItems.Count - 1; i != 1; i--)
                recentFilesMenuItem.MenuItems.RemoveAt(i);
        }

        private void recentFiles_Click(object sender, EventArgs e)
        {
            MenuItem item = (MenuItem)sender;
            string file = item.Tag.ToString();

            if (!string.IsNullOrEmpty(file) && File.Exists(file))
            {
                OpenArchive(file, true);
            }
            else
            {
                string message = string.Format("\"{1}\" doesn't exist anymore.{0}{0}" +
                    "Do you want to remove it from the recent files list?", Environment.NewLine, item.Tag.ToString());

                if (MessageBox.Show(this, message, "Lost File", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    recentFilesMenuItem.MenuItems.Remove(item);
                }
            }
        }

        private void exitMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void editMenuItem_Popup(object sender, EventArgs e)
        {
            bool hasSelectedItems = lvFiles.SelectedIndices.Count > 0;

            copyPathMenuItem.Enabled = hasSelectedItems;
            copyFolderPathMenuItem.Enabled = hasSelectedItems;
            copyFileNameMenuItem.Enabled = hasSelectedItems;
        }

        private void copyPathMenuItem_Click(object sender, EventArgs e)
        {
            StringBuilder builder = new StringBuilder();

            foreach (int index in lvFiles.SelectedIndices)
            {
                if (!string.IsNullOrEmpty(builder.ToString()))
                    builder.Append(Environment.NewLine);

                builder.Append(_files[index].FullPath);
            }

            Clipboard.SetText(builder.ToString());
        }

        private void copyFolderPathMenuItem_Click(object sender, EventArgs e)
        {
            StringBuilder builder = new StringBuilder();

            foreach (int index in lvFiles.SelectedIndices)
            {
                if (!string.IsNullOrEmpty(builder.ToString()))
                    builder.Append(Environment.NewLine);

                builder.Append(Path.GetDirectoryName(_files[index].FullPath));
            }

            Clipboard.SetText(builder.ToString());
        }

        private void copyFileNameMenuItem_Click(object sender, EventArgs e)
        {
            StringBuilder builder = new StringBuilder();

            foreach (int index in lvFiles.SelectedIndices)
            {
                if (!string.IsNullOrEmpty(builder.ToString()))
                    builder.Append(Environment.NewLine);

                builder.Append(Path.GetFileName(_files[index].FullPath));
            }

            Clipboard.SetText(builder.ToString());
        }

        private void openFolderMenuItem_Click(object sender, EventArgs e)
        {
            var menuItem = sender as MenuItem;
            var path = menuItem.Tag as QuickExtractPath;

            if (!Directory.Exists(path.Path))
            {
                MessageBox.Show(
                    this,
                    $"{path.Name}'s path no longer exists.");
                return;
            }

            System.Diagnostics.Process.Start(path.Path);
        }

        private void helpMenuItem_Popup(object sender, EventArgs e)
        {
            int count = "(!) ".Length;

            // Remove (!) from Text
            if (helpMenuItem.Text.StartsWith("(!) "))
                helpMenuItem.Text = helpMenuItem.Text.Remove(0, count);
        }

        private async void checkForUpdateMenuItem_Click(object sender, EventArgs e)
        {
            int count = "(!) ".Length;

            // Remove (!) from Text
            if (checkForUpdateMenuItem.Text.StartsWith("(!) "))
                checkForUpdateMenuItem.Text = checkForUpdateMenuItem.Text.Remove(0, count);

            try
            {
                if (await this.IsUpdateAvailable())
                {
                    if (MessageBox.Show(this,
                            "Update available!\n\n" + "Do you want to open the BSA Browser NexusMods page?",
                            "Update available",
                            MessageBoxButtons.YesNo) == DialogResult.Yes)
                    {
                        Process.Start(Program.Website);
                    }
                }
                else
                {
                    MessageBox.Show(this, "You have the latest version.");
                }
            }
            catch (Win32Exception)
            {
                MessageBox.Show(this, "Couldn't open the BSA Browser NexusMods page.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Error checking for update.\n\n" + ex.Message);
            }
        }

        private void aboutMenuItem_Click(object sender, EventArgs e)
        {
            using (AboutBox ab = new AboutBox())
            {
                ab.ShowDialog(this);
            }
        }

        #endregion

        #region contextMenu1

        private void contextMenu1_Popup(object sender, EventArgs e)
        {
            bool hasSelectedItems = lvFiles.SelectedIndices.Count > 0;

            quickExtractsMenuItem.Enabled = hasSelectedItems;
            copyPathMenuItem1.Enabled = hasSelectedItems;
            copyFolderPathMenuItem1.Enabled = hasSelectedItems;
            copyFileNameMenuItem1.Enabled = hasSelectedItems;
        }

        private void quickExtractsMenuItem_Click(object sender, EventArgs e)
        {
            if (quickExtractsMenuItem.MenuItems.Count > 0)
                return;

            // Open options with second tab selected
            using (var of = new OptionsForm(1))
            {
                if (of.ShowDialog(this) == DialogResult.OK)
                {
                    of.SaveChanges();
                    Settings.Default.Save();

                    // Sync changes to UI
                    this.LoadQuickExtractPaths();
                    this.UpdateColumns();
                }
            }
        }

        private void quickExtractMenuItem_Click(object sender, EventArgs e)
        {
            var menuItem = sender as MenuItem;
            var path = menuItem.Tag as QuickExtractPath;

            if (!Directory.Exists(path.Path))
            {
                DialogResult result = MessageBox.Show(
                    this,
                    string.Format("{0} path doesn't exists anymore. Do you want to create it?", path.Name),
                    "Quick Extract",
                    MessageBoxButtons.YesNo);

                if (result == DialogResult.No)
                    return;

                Directory.CreateDirectory(path.Path);
            }

            var files = new List<ArchiveEntry>();

            foreach (int index in lvFiles.SelectedIndices)
                files.Add(_files[index]);

            ExtractFiles(path.Path, path.UseFolderPath, true, files.ToArray());
        }

        private void copyPathMenuItem1_Click(object sender, EventArgs e)
        {
            copyPathMenuItem.PerformClick();
        }

        private void copyFolderPathMenuItem1_Click(object sender, EventArgs e)
        {
            copyFolderPathMenuItem.PerformClick();
        }

        private void copyFileNameMenuItem1_Click(object sender, EventArgs e)
        {
            copyFileNameMenuItem.PerformClick();
        }

        #endregion

        /// <summary>
        /// Opens the given archive, adding it to the TreeView and making it browsable.
        /// </summary>
        /// <param name="path">The archive file path.</param>
        /// <param name="addToRecentFiles">True if archive should be added to recent files list.</param>
        public void OpenArchive(string path, bool addToRecentFiles = false)
        {
            // Check if archive is already opened
            foreach (ArchiveNode node in tvFolders.Nodes)
            {
                if (node.Archive.FullPath.ToLower() == path.ToLower())
                {
                    MessageBox.Show(this, "This archive is already opened.");
                    return;
                }
            }

            Archive archive = null;

            try
            {
                string extension = Path.GetExtension(path);

                // ToDo: Read file header to find archive type, not just extension
                switch (extension)
                {
                    case ".bsa":
                    case ".dat":
                        if (SharpBSABA2.BSAUtil.BSA.IsSupportedVersion(path) == false)
                        {
                            if (MessageBox.Show(this,
                                    "This BSA archive has an unknown version number.\n" + "Attempt to open anyway?",
                                    "Warning",
                                    MessageBoxButtons.YesNo) != DialogResult.Yes)
                                return;
                        }

                        archive = new SharpBSABA2.BSAUtil.BSA(path);
                        break;
                    case ".ba2":
                        archive = new SharpBSABA2.BA2Util.BA2(path);
                        break;
                    default:
                        throw new Exception($"Unrecognized archive file type ({extension}).");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return;
            }

            var newNode = new ArchiveNode(Path.GetFileNameWithoutExtension(path), archive);

            var newMenuItem = new MenuItem("Close");
            newMenuItem.Tag = newNode;
            newMenuItem.Click += delegate (object sender, EventArgs e)
            {
                CloseArchive(newNode);
                if (tvFolders.Nodes.Count == 0)
                {
                    lvFiles.BeginUpdate();
                    _files.Clear();
                    lvFiles.VirtualListSize = 0;
                    lvFiles.EndUpdate();
                }
                else
                    this.Search();
            };
            var cm = new ContextMenu(new MenuItem[] { newMenuItem });
            newNode.ContextMenu = cm;
            newNode.Files = archive.Files.ToArray();
            newNode.Nodes.Add("empty");
            tvFolders.Nodes.Add(newNode);

            if (newNode.IsExpanded)
                newNode.Collapse();

            txtSearch.Text = "";
            btnExtract.Enabled = true;
            btnExtractAll.Enabled = true;
            btnPreview.Enabled = true;

            if (addToRecentFiles)
                AddToRecentFiles(path);

            tvFolders.SelectedNode = newNode;
        }

        /// <summary>
        /// Opens all given archives.
        /// </summary>
        /// <param name="addToRecentFiles">True if archives should be added to recent files list.</param>
        /// <param name="paths">Array of archive file paths.</param>
        public void OpenArchives(bool addToRecentFiles, params string[] paths)
        {
            foreach (string path in paths)
                this.OpenArchive(path, addToRecentFiles);
        }

        /// <summary>
        /// Adds the given file to the recent files list. If it already exists in the list, it gets bumped up to the top.
        /// </summary>
        /// <param name="file">The file to add.</param>
        private void AddToRecentFiles(string file)
        {
            if (string.IsNullOrEmpty(file))
                return;

            if (RecentListContains(file))
            {
                var item = RecentListGetItemByString(file);

                if (item == null)
                    return;

                int index = recentFilesMenuItem.MenuItems.IndexOf(item);
                recentFilesMenuItem.MenuItems.Remove(item);
                recentFilesMenuItem.MenuItems.Add(2, item);
            }
            else
            {
                var newItem = new MenuItem(file, new EventHandler(recentFiles_Click));
                newItem.Tag = file;
                recentFilesMenuItem.MenuItems.Add(2, newItem);
            }
        }

        /// <summary>
        /// Closes the given archive, removing it from the TreeView.
        /// </summary>
        /// <param name="archiveNode"></param>
        private void CloseArchive(ArchiveNode archiveNode)
        {
            if (GetSelectedArchiveNode() == archiveNode)
            {
                lvFiles.BeginUpdate();
                _files.Clear();
                lvFiles.VirtualListSize = 0;
                lvFiles.EndUpdate();
            }

            archiveNode.Archive.Close();

            tvFolders.Nodes.Remove(archiveNode);

            if (tvFolders.GetNodeCount(false) == 0)
            {
                btnPreview.Enabled = false;
                btnExtract.Enabled = false;
                btnExtractAll.Enabled = false;
            }
        }

        /// <summary>
        /// Closes all open archives, clearing the TreeView.
        /// </summary>
        private void CloseArchives()
        {
            lvFiles.BeginUpdate();
            _files.Clear();
            lvFiles.VirtualListSize = 0;
            lvFiles.EndUpdate();

            foreach (ArchiveNode node in tvFolders.Nodes)
                node.Archive.Close();

            tvFolders.Nodes.Clear();
        }

        /// <summary>
        /// Extracts the given file(s) to the given path.
        /// </summary>
        /// <param name="folder">The path to extract files to.</param>
        /// <param name="useFolderPath">True to use full folder path for files, false to extract straight to path.</param>
        /// <param name="gui">True to show a progression dialog.</param>
        /// <param name="files">The files in the selected archive to extract.</param>
        private void ExtractFiles(string folder, bool useFolderPath, bool gui, params ArchiveEntry[] files)
        {
            if (gui)
            {
                pf = new ProgressForm("Unpacking archive", false);
                pf.EnableCancel();
                pf.SetProgressRange(files.Length);
                pf.Canceled += delegate { bw.CancelAsync(); };
                pf.Show(this);

                bw = new BackgroundWorker();
                bw.WorkerReportsProgress = true;
                bw.WorkerSupportsCancellation = true;
                bw.DoWork += bw_DoWork;
                bw.ProgressChanged += bw_ProgressChanged;
                bw.RunWorkerCompleted += bw_RunWorkerCompleted;
                bw.RunWorkerAsync(new ExtractFilesArguments()
                {
                    UseFolderPath = useFolderPath,
                    Folder = folder,
                    Files = files
                });
            }
            else
            {
                try
                {
                    var root = GetSelectedArchiveNode();

                    foreach (var fe in files)
                        fe.Extract(folder, useFolderPath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error");
                }
            }
        }

        #region ExtractFiles variables

        BackgroundWorker bw;
        ProgressForm pf;

        private class ExtractFilesArguments
        {
            public bool UseFolderPath { get; set; }
            public string Folder { get; set; }
            public ArchiveEntry[] Files { get; set; }
        }

        private void bw_DoWork(object sender, DoWorkEventArgs e)
        {
            var arguments = e.Argument as ExtractFilesArguments;

            try
            {
                int count = 0;

                foreach (var fe in arguments.Files)
                {
                    if (bw.CancellationPending)
                    {
                        e.Result = false;
                        break;
                    }

                    fe.Extract(arguments.Folder, arguments.UseFolderPath);
                    bw.ReportProgress(count++);
                }
            }
            catch (Exception ex)
            {
                e.Result = ex;
            }
        }

        private void bw_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            pf.UpdateProgress(e.ProgressPercentage);
            this.Text = string.Format("{0}% - {1}", pf.GetProgressPercentage(), _untouchedTitle);
        }

        private void bw_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            pf.Unblock();
            pf.Close();
            pf.Dispose();
            pf = null;

            bw.Dispose();
            bw = null;

            this.Text = _untouchedTitle;

            if (e.Result is bool)
            {
                if (!(bool)e.Result)
                {
                    MessageBox.Show("Operation cancelled", "Message");
                }
            }
            else if (e.Result is Exception)
            {
                MessageBox.Show(((Exception)e.Result).Message, "Error");
            }
        }

        #endregion

        /// <summary>
        /// Formats the given file size to a more readable string.
        /// </summary>
        /// <param name="bytes">The file size to format.</param>
        private string FormatBytes(long bytes)
        {
            const int scale = 1024;
            string[] orders = new string[] { "GB", "MB", "KB", "Bytes" };
            long max = (long)Math.Pow(scale, orders.Length - 1);

            foreach (string order in orders)
            {
                if (bytes > max)
                    return string.Format("{0:##.##} {1}", decimal.Divide(bytes, max), order);

                max /= scale;
            }
            return "0 Bytes";
        }

        /// <summary>
        /// Returns the root node of the given TreeNode.
        /// </summary>
        /// <param name="node">The TreeNode to get root node from.</param>
        private ArchiveNode GetRootNode(TreeNode node)
        {
            TreeNode rootNode = node;
            while (rootNode.Parent != null)
                rootNode = rootNode.Parent;
            return rootNode as ArchiveNode;
        }

        /// <summary>
        /// Returns the selected archive.
        /// </summary>
        private ArchiveNode GetSelectedArchiveNode()
        {
            if (tvFolders.SelectedNode == null)
                return null;

            return GetRootNode(tvFolders.SelectedNode);
        }

        /// <summary>
        /// Returns true if update is available online.
        /// </summary>
        private async Task<bool> IsUpdateAvailable()
        {
            using (var wc = new WebClient())
            {
                var onlineVersion = new Version(await wc.DownloadStringTaskAsync(Program.VersionUrl));
                var localVersion = new Version(Application.ProductVersion);

                return localVersion < onlineVersion;
            }
        }

        /// <summary>
        /// Loads quick extract paths into Quick Extract menu item.
        /// </summary>
        private void LoadQuickExtractPaths()
        {
            openFoldersMenuItem.MenuItems.Clear();
            quickExtractsMenuItem.MenuItems.Clear();

            foreach (QuickExtractPath path in Settings.Default.QuickExtractPaths)
            {
                openFoldersMenuItem.MenuItems.Add(
                    new MenuItem(path.Name, openFolderMenuItem_Click)
                    {
                        Tag = path
                    });
                quickExtractsMenuItem.MenuItems.Add(
                    new MenuItem(path.Name, quickExtractMenuItem_Click)
                    {
                        Tag = path
                    });
            }
        }

        /// <summary>
        /// Returns true if recent files list contains the given file, false otherwise.
        /// </summary>
        /// <param name="file">The file to check.</param>
        private bool RecentListContains(string file)
        {
            foreach (MenuItem item in recentFilesMenuItem.MenuItems)
                if (item.Tag != null && item.Tag.ToString() == file) return true;
            return false;
        }

        /// <summary>
        /// Returns the given file's MenuItem.
        /// </summary>
        /// <param name="file">The file to get MenuItem from.</param>
        private MenuItem RecentListGetItemByString(string file)
        {
            foreach (MenuItem item in recentFilesMenuItem.MenuItems)
                if (item.Tag != null && item.Tag.ToString() == file) return item;

            return null;
        }

        /// <summary>
        /// Saves the recent files list to Settings.
        /// </summary>
        private void SaveRecentFiles()
        {
            if (Settings.Default.RecentFiles == null)
                Settings.Default.RecentFiles = new StringCollection();
            else
                Settings.Default.RecentFiles.Clear();

            for (int i = recentFilesMenuItem.MenuItems.Count - 1; i != 1; i--)
                Settings.Default.RecentFiles.Add(recentFilesMenuItem.MenuItems[i].Tag.ToString());
        }

        /// <summary>
        /// Searches files list, filtering out not-matching files.
        /// </summary>
        private void Search()
        {
            _searchDelayTimer?.Stop();

            if (!(tvFolders.GetNodeCount(false) > 0) || tvFolders.SelectedNode == null)
                return;

            string str = txtSearch.Text;

            txtSearch.ForeColor = System.Drawing.SystemColors.WindowText;

            if (cbRegex.Checked && str.Length > 0)
            {
                Regex regex;

                try
                {
                    regex = new Regex(str, RegexOptions.Compiled | RegexOptions.Singleline);
                }
                catch
                {
                    txtSearch.ForeColor = System.Drawing.Color.Red;
                    return;
                }

                _files.Clear();

                for (int i = 0; i < GetSelectedArchiveNode().Files.Length; i++)
                {
                    var file = GetSelectedArchiveNode().Files[i];

                    if (regex.IsMatch(Path.Combine(file.Folder, file.FileName)))
                        _files.Add(file);
                }
            }
            else
            {
                _files.Clear();

                if (str.Length == 0)
                    _files.AddRange(GetSelectedArchiveNode().Files);
                else
                {
                    // Escape special characters, then unescape wild card characters again
                    str = WildcardPattern.Escape(str).Replace("`*", "*");
                    var pattern = new WildcardPattern($"*{str}*", WildcardOptions.Compiled | WildcardOptions.IgnoreCase);

                    try
                    {
                        for (int i = 0; i < GetSelectedArchiveNode().Files.Length; i++)
                        {
                            var file = GetSelectedArchiveNode().Files[i];

                            if (pattern.IsMatch(Path.Combine(file.Folder, file.FileName)))
                                _files.Add(file);
                        }
                    }
                    catch
                    {
                        txtSearch.ForeColor = System.Drawing.Color.Red;
                        return;
                    }
                }
            }

            _files.Sort(_filesSorter);

            lvFiles.BeginUpdate();
            lvFiles.VirtualListSize = _files.Count;
            lvFiles.Invalidate();
            lvFiles.EndUpdate();

            lFileCount.Text = string.Format("{0:n0} files", _files.Count);
        }

        /// <summary>
        /// Adds (!) to Help & Check for update menu items if there is an update available.
        /// </summary>
        private async void ShowUpdateNotification()
        {
            if (await this.IsUpdateAvailable())
            {
                helpMenuItem.Text = $"(!) {helpMenuItem.Text}";
                checkForUpdateMenuItem.Text = $"(!) {checkForUpdateMenuItem.Text}";
            }
        }

        /// <summary>
        /// Sorts all nodes in given TreeNode.
        /// </summary>
        /// <param name="rootNode">The TreeNode whose children is to be sorted.</param>
        private void SortNodes(TreeNode rootNode)
        {
            foreach (TreeNode node in rootNode.Nodes)
            {
                TreeNode[] nodes = new TreeNode[node.Nodes.Count];

                node.Nodes.CopyTo(nodes, 0);

                Array.Sort<TreeNode>(nodes, new TreeNodeSorter());

                node.Nodes.Clear();
                node.Nodes.AddRange(nodes);

                SortNodes(node);
            }
        }

        /// <summary>
        /// Shows or hides additional columns according to settings.
        /// </summary>
        private void UpdateColumns()
        {
            if (_extraColumns == null)
                _extraColumns = new ColumnHeader[] { columnHeader2, columnHeader3, columnHeader4 };

            if (Settings.Default.MoreColumns)
            {
                if (lvFiles.Columns.Count > 1)
                    return;

                lvFiles.BeginUpdate();
                lvFiles.Columns.AddRange(_extraColumns);
                lvFiles.EndUpdate();
            }
            else
            {
                foreach (ColumnHeader column in _extraColumns)
                    lvFiles.Columns.Remove(column);
            }
        }
    }

    public class ArchiveFileSorter : Comparer<ArchiveEntry>
    {
        internal static ArchiveFileSortOrder order = 0;
        internal static bool desc = true;

        public static void SetSorter(ArchiveFileSortOrder sortOrder, bool sortDesc)
        {
            order = sortOrder;
            desc = sortDesc;
        }

        public override int Compare(ArchiveEntry a, ArchiveEntry b)
        {
            ArchiveEntry fa = a;
            ArchiveEntry fb = b;
            switch (order)
            {
                case ArchiveFileSortOrder.FolderName:
                    return (desc) ? string.Compare(fa.LowerPath, fb.LowerPath) : string.Compare(fb.LowerPath, fa.LowerPath);
                case ArchiveFileSortOrder.FileName:
                    return (desc) ? string.Compare(fa.FileName, fb.FileName) : string.Compare(fb.FileName, fa.FileName);
                case ArchiveFileSortOrder.FileSize:
                    return (desc) ? fa.Size.CompareTo(fb.Size) : fb.Size.CompareTo(fa.Size);
                case ArchiveFileSortOrder.Offset:
                    return (desc) ? fa.Offset.CompareTo(fb.Offset) : fb.Offset.CompareTo(fa.Offset);
                case ArchiveFileSortOrder.FileType:
                    return (desc) ? string.Compare(Path.GetExtension(fa.FileName), Path.GetExtension(fb.FileName)) :
                                    string.Compare(Path.GetExtension(fb.FileName), Path.GetExtension(fa.FileName));
                default:
                    return 0;
            }
        }
    }

    public class TreeNodeSorter : Comparer<TreeNode>
    {
        public override int Compare(TreeNode a, TreeNode b)
        {
            if (a == null)
            {
                return b == null ? 0 : -1;
            }
            else
            {
                return b == null ? 1 : a.Text.CompareTo(b.Text);
            }
        }
    }
}