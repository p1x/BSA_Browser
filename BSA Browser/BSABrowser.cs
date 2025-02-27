﻿using BSA_Browser.Classes;
using BSA_Browser.Dialogs;
using BSA_Browser.Enums;
using BSA_Browser.Extensions;
using BSA_Browser.Properties;
using BSA_Browser.Sorting;
using BSA_Browser.Tools;
using SharpBSABA2;
using SharpBSABA2.BA2Util;
using SharpBSABA2.Enums;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using DirectXTex;

namespace BSA_Browser
{
    public partial class BSABrowser : Form
    {
        private const string UpdateMarker = "(!) ";

        int _limitSearchId = LimitedAction.GenerateId();
        OpenFolderDialog _openFolderDialog = new OpenFolderDialog();
        ArchiveFileSorter _filesSorter = new ArchiveFileSorter();
        TreeNodeSorter _nodeSorter = new TreeNodeSorter();
        CompareForm _compareForm;
        string[] _args;
        bool _pauseFiltering = false;

        /// <summary>
        /// Gets the selected <see cref="ArchiveNode"/>.
        /// </summary>
        private ArchiveNode SelectedArchiveNode => tvFolders.SelectedNode?.GetRootNode() as ArchiveNode;

        /// <summary>
        /// Gets list of <see cref="ArchiveEntry"/> currently visible.
        /// </summary>
        private List<ArchiveEntry> VisibleFiles { get; set; } = new List<ArchiveEntry>();

        public BSABrowser()
        {
            InitializeComponent();

            // Fix SSL issue with checking for updates, on Windows 7 at least
            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            // Set it here otherwise DPI scaling will not work correctly, for some reason
            this.Menu = mainMenu1;

            // Show application version in title
            this.Text += $" ({Program.Version})";

            var archiveNode = new ArchiveNode("All", archive: null)
            {
                SubFiles = new ArchiveEntry[0],
                ImageIndex = 4,
                SelectedImageIndex = 4
            };
            archiveNode.Loaded = true;
            tvFolders.Nodes.Add(archiveNode);
            tvFolders.ContextMenu = foldersContextMenu;

            lvFiles.ContextMenu = contextMenu1;

            // Restore last path for OpenFolderDialog
            if (!string.IsNullOrEmpty(Settings.Default.LastUnpackPath))
                _openFolderDialog.InitialFolder = Settings.Default.LastUnpackPath;

            // Load Recent Files list
            if (Settings.Default.RecentFiles != null)
            {
                foreach (string item in Settings.Default.RecentFiles)
                    this.AddToRecentFiles(item);
            }

            // Load Quick Extract Paths
            Settings.Default.QuickExtractPaths = Settings.Default.QuickExtractPaths ?? new QuickExtractPaths();

            this.LoadQuickExtractPaths();

            // Set lvFiles sorter
            ArchiveFileSorter.SetSorter(Settings.Default.SortType, Settings.Default.SortDesc);

            // Enable visual styles
            tvFolders.EnableVisualStyles();
            lvFiles.EnableVisualStyles();

            // Set TextBox cue
            txtSearch.SetCue("Search term...");

#if DEBUG
            this.SetupDebugTools();
#endif

            filesImageList.Images.Add("NoAssoc", SystemIcons.FilesNoAssoc);

            foldersImageList.Images.Add(new System.Drawing.Bitmap(16, 16));
            foldersImageList.Images.Add(SystemIcons.FolderSmall);
            foldersImageList.Images.Add(SystemIcons.Files);
            foldersImageList.Images.Add((System.Drawing.Icon)this.Icon.Clone());
            foldersImageList.Images.Add(Resources.all);
            foldersImageList.Images.Add(Resources.unloaded);

            if (Settings.Default.Icons.HasFlag(Enums.Icons.FileList))
            {
                lvFiles.SmallImageList = filesImageList;
            }

            if (Settings.Default.Icons.HasFlag(Enums.Icons.FolderTree))
            {
                tvFolders.ImageList = foldersImageList;
            }
        }

        #region Debug Tools
#if DEBUG
        /// <summary>
        /// Returns <see cref="Archive"/> with the given file path from <see cref="tvFolders"/>.
        /// </summary>
        private Archive FindArchive(string fullPath)
        {
            return tvFolders.Nodes
                .OfType<ArchiveNode>()
                .Skip(1)
                .First(x => x.Archive.FullPath.ToLower() == fullPath.ToLower()).Archive;
        }

        private void SetupDebugTools()
        {
            lvFiles.Columns.Add("Extra", 200);

            var debugMenuItem = mainMenu1.MenuItems.Add("DEBUG");

            debugMenuItem.MenuItems.Add("Average opening speed of archive", OpeningSpeedAverage_Click);
            debugMenuItem.MenuItems.Add("Average extraction speed of selected file", ExtractionSpeedAverage_Click);
            debugMenuItem.MenuItems.Add("Average extraction speed of files", ExtractionSpeedAverageFiles_Click);
            debugMenuItem.MenuItems.Add("Average extraction speed of files (multi-threaded)", ExtractionSpeedAverageFilesMultiThreaded_Click);
            debugMenuItem.MenuItems.Add("Check if all textures formats are supported", CheckTextureFormats_Click);
            debugMenuItem.MenuItems.Add("Show ProgressForm", ShowProgressForm_Click);
            debugMenuItem.MenuItems.Add("Benchmark DoSearch", BenchmarkDoSearch_Click);
        }

        private void OpeningSpeedAverage_Click(object sender, EventArgs e)
        {
            if (OpenArchiveDialog.ShowDialog(this) != DialogResult.OK)
                return;

            var skipped = 0;
            var path = OpenArchiveDialog.FileNames[0];

            var sw = new Stopwatch();
            var count = 0;
            var results = new List<long>();

            while (count < 40)
            {
                sw.Restart();

                try
                {
                    var extension = Path.GetExtension(path);
                    var encoding = Encoding.GetEncoding(Settings.Default.EncodingCodePage);

                    switch (extension.ToLower())
                    {
                        case ".bsa":
                        case ".dat":
                            if (SharpBSABA2.BSAUtil.BSA.IsSupportedVersion(path) == false)
                            {
                                goto default;
                            }

                            new SharpBSABA2.BSAUtil.BSA(path, encoding, Settings.Default.RetrieveRealSize);
                            break;
                        case ".ba2":
                            new SharpBSABA2.BA2Util.BA2(path, encoding, Settings.Default.RetrieveRealSize);
                            break;
                        default:
                            skipped++;
                            break;
                    }
                }
                catch
                {
                    skipped++;
                }

                sw.Stop();
                results.Add(sw.ElapsedMilliseconds);
                Console.WriteLine(count + " - " + sw.ElapsedMilliseconds + "ms");
                count++;
            }

            Console.WriteLine($"Average: {results.Sum() / results.Count}ms. {skipped} skipped.");
        }

        private void ExtractionSpeedAverage_Click(object sender, EventArgs e)
        {
            if (lvFiles.SelectedIndices.Count == 0)
                return;

            if (lvFiles.SelectedIndices.Count > 1)
            {
                MessageBox.Show(this, "Can only test one file at a time", "Error");
                return;
            }

            var fe = VisibleFiles[lvFiles.SelectedIndices[0]];
            Stopwatch sw = new Stopwatch();
            int count = 0;
            List<long> results = new List<long>();

            while (count < 1000)
            {
                sw.Restart();
                using (var ms = fe.GetDataStream())
                {
                    sw.Stop();
                    results.Add(sw.ElapsedMilliseconds);
                    count++;
                }
            }

            Console.WriteLine($"Average: {results.Sum() / results.Count}ms");
        }

        private void ExtractionSpeedAverageFiles_Click(object sender, EventArgs e)
        {
            if (tvFolders.SelectedNode == null)
                return;

            var sw = new Stopwatch();
            int count = 0;
            var results = new List<long>();

            while (count < 50)
            {
                sw.Restart();
                foreach (var file in VisibleFiles)
                    file.GetDataStream();
                sw.Stop();
                results.Add(sw.ElapsedMilliseconds);
                count++;
                Console.WriteLine($"{count} - Average: {results.Sum() / results.Count}ms");
            }

            MessageBox.Show($"Average: {results.Sum() / results.Count}ms");
        }

        private async void ExtractionSpeedAverageFilesMultiThreaded_Click(object sender, EventArgs e)
        {
            if (VisibleFiles.Count == 0)
                return;

            var sw = new Stopwatch();
            int count = 0;
            var results = new List<long>();
            var tasks = new List<Task>();

            var chunks = VisibleFiles.Split(4);

            while (count < 50)
            {
                sw.Restart();

                foreach (var list in chunks)
                {
                    Task task = new Task(files =>
                    {
                        var sharedParams = new Dictionary<string, SharedExtractParams>();
                        foreach (var arc in (files as List<ArchiveEntry>).Select(x => x.Archive.FullPath.ToLower()).Distinct())
                            sharedParams.Add(arc, FindArchive(arc).CreateSharedParams(true, true));

                        foreach (ArchiveEntry file in (List<ArchiveEntry>)files)
                            // file.Extract(@"D:\Testing\test1", true, file.FileName, sharedParams[file.Archive.FullPath.ToLower()]);
                            file.GetDataStream(sharedParams[file.Archive.FullPath.ToLower()]);

                        foreach (var sp in sharedParams)
                            sp.Value.Reader.Close();
                    }, list);

                    tasks.Add(task);
                    task.Start();
                }

                await Task.WhenAll(tasks);
                sw.Stop();
                results.Add(sw.ElapsedMilliseconds);
                count++;
                Console.WriteLine($"{count} - Average: {results.Sum() / results.Count}ms");
                tasks.Clear();
            }

            MessageBox.Show($"Average: {results.Sum() / results.Count}ms");
        }

        private void CheckTextureFormats_Click(object sender, EventArgs e)
        {
            int checkedTextures = 0;
            int unsupported = 0;
            var sw = new Stopwatch();

            sw.Start();

            foreach (var fe in VisibleFiles.OfType<BA2TextureEntry>())
            {
                checkedTextures++;

                if (!fe.IsFormatSupported())
                    unsupported++;
            }

            sw.Stop();
            MessageBox.Show($"Checked {checkedTextures} textures in {sw.ElapsedMilliseconds}ms, {unsupported} unsupported textures.");
        }

        private void ShowProgressForm_Click(object sender, EventArgs e)
        {
            ProgressForm pf = new ProgressForm(10);
            pf.Show(this);

            BackgroundWorker bw = new BackgroundWorker();
            bw.WorkerReportsProgress = true;
            bw.DoWork += delegate (object _, DoWorkEventArgs eventArgs)
            {
                for (int i = 0; i < 10; i++)
                {
                    if (eventArgs.Cancel)
                        break;

                    decimal percentage = (decimal)i / 10 * 100;
                    bw.ReportProgress((int)percentage);
                    System.Threading.Thread.Sleep(1000);
                }
            };
            bw.ProgressChanged += (_, eventArgs) =>
            {
                pf.Progress = eventArgs.ProgressPercentage;
            };
            bw.RunWorkerCompleted += delegate
            {
                pf.BlockClose = false;
                pf.Close();
            };

            pf.Canceled += delegate
            {
                bw.CancelAsync();
            };

            bw.RunWorkerAsync();
        }

        private void BenchmarkDoSearch_Click(object sender, EventArgs e)
        {
            var sw = new Stopwatch();
            var elapsedTimes = new List<long>();

            for (int i = 0; i < 100; i++)
            {
                sw.Start();
                this.DoSearch();
                sw.Restart();
                elapsedTimes.Add(sw.ElapsedMilliseconds);
            }

            MessageBox.Show(this, "Average: " + elapsedTimes.Sum() / 100 + "ms\n" +
                                  "Max: " + elapsedTimes.Max() + "ms\n" +
                                  "Min: " + elapsedTimes.Min() + "ms\n" +
                                  "Total: " + elapsedTimes.Sum() + "ms");
        }
#endif
        #endregion

        public BSABrowser(string[] args)
            : this()
        {
            _args = args;
        }

        private async void BSABrowser_Load(object sender, EventArgs e)
        {
            // Initialize WindowStates if null
            Settings.Default.WindowStates = Settings.Default.WindowStates ?? new WindowStates();

            // Restore window state
            Settings.Default.WindowStates.Restore(this, false);

            // Restore sorting preferences
            lvFiles.SetSortIcon(
                (int)Settings.Default.SortType,
                Settings.Default.SortDesc ? SortOrder.Ascending : SortOrder.Descending);

            // Restore Regex preference
            cbRegex.Checked = Settings.Default.SearchUseRegex;

            OpenArchiveDialog.InitialDirectory = Settings.Default.OpenArchiveDialog;

            if (Settings.Default.CheckForUpdates)
            {
                // Show ! in main menu if update is available
                this.ShowUpdateNotification();
            }

            // Check if we should list archives from last session
            this.AddRememberedArchivesToList();

            if (_args?.Length > 0)
                await this.OpenArchives(true, _args);
        }

        private void BSABrowser_FormClosed(object sender, FormClosedEventArgs e)
        {
            // Signal to close all Forms when MainForm closes
            for (int i = Application.OpenForms.Count; i-- > 0;)
            {
                Form form = Application.OpenForms[i];
                if (form is ProgressForm pf)
                    pf.ForceCancel();
                else
                    form.Close();
            }
        }

        private void BSABrowser_FormClosing(object sender, FormClosingEventArgs e)
        {
            Settings.Default.RememberedArchives.Clear();
            // Store opened archives if setting is enabled, before closing archives
            if (Settings.Default.RememberArchives)
            {
                Settings.Default.RememberedArchives.AddRange(tvFolders.Nodes
                    .Cast<ArchiveNode>()
                    .Skip(1)
                    .Select(x => x.FilePath)
                    .ToArray());
            }

            if (tvFolders.GetNodeCount(false) > 1)
                this.CloseArchives();

            this.SaveRecentFiles();

            Settings.Default.WindowStates.Save(this);
            Settings.Default.LastUnpackPath = _openFolderDialog.Folder;
            Settings.Default.Save();
        }

        private void BSABrowser_Shown(object sender, EventArgs e)
        {
            // Show notification if Settings was reset this session
            if (Program.SettingsReset)
                MessageBox.Show(this,
                    "Settings was reset to default.",
                    "Settings Reset",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void File_DragOver(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
                return;

            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

            e.Effect = files.All(this.IsSupportedFile) ? DragDropEffects.Link : DragDropEffects.Scroll;
        }

        private async void File_DragDrop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
                return;

            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

            if (files.All(x => x.StartsWith(Program.TempPath)))
                return;

            if (files.Any(x => !this.IsSupportedFile(x)))
            {
                MessageBox.Show(this,
                    "One or more files not supported, only opening supported.\n\nUnsupported format(s): " +
                        string.Join(", ", files
                            .Where(x => !this.IsSupportedFile(x))
                            .Select(Path.GetExtension)
                            .Distinct(StringComparer.OrdinalIgnoreCase)),
                    "Unsupported File(s)",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            await this.OpenArchives(true, files.Where(this.IsSupportedFile));
        }

        private async void btnOpen_Click(object sender, EventArgs e)
        {
            if (OpenArchiveDialog.ShowDialog(this) == DialogResult.OK)
                await this.OpenArchives(true, OpenArchiveDialog.FileNames);
        }

        private void btnExtractFiles_Click(object sender, EventArgs e)
        {
            if (this.SelectedArchiveNode == null)
                return;

            this.ExtractFilesTo(false, true, this.VisibleFiles);
        }

        private void btnExtractFolders_Click(object sender, EventArgs e)
        {
            if (this.SelectedArchiveNode == null)
                return;

            this.ExtractFilesTo(true, true, this.VisibleFiles);
        }

        private void lvFiles_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            var type = (ArchiveFileSortOrder)e.Column;

            if (Settings.Default.SortType == type)
            {
                // Reverse order
                Settings.Default.SortDesc = !Settings.Default.SortDesc;
            }
            else
            {
                Settings.Default.SortType = type;
                Settings.Default.SortDesc = true;
            }

            lvFiles.SetSortIcon(e.Column,
                Settings.Default.SortDesc ? SortOrder.Ascending : SortOrder.Descending);

            this.SortList();
        }

        private void lvFiles_DoubleClick(object sender, EventArgs e)
        {
            this.PreviewSelected();
        }

        private async void lvFiles_ItemDrag(object sender, ItemDragEventArgs e)
        {
            if (!(lvFiles.SelectedIndices.Count >= 1))
                return;

            var obj = new DataObject();
            var sc = new StringCollection();
            string dest = Program.CreateTempDirectory();
            var entries = this.GetSelectedEntries().ToList();

            this.UseWaitCursor = true;

            await Task.Run(() =>
            {
                foreach (var entry in entries)
                {
                    entry.Extract(dest, true);
                    sc.Add(Path.Combine(dest, entry.FullPath));
                }
            });

            this.UseWaitCursor = false;

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

        private void lvFiles_RetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
        {
            if (VisibleFiles.Count <= e.ItemIndex)
                return;

            var file = VisibleFiles[e.ItemIndex];
            var fullpath = Path.Combine(file.Folder, file.FileName);

            // Prepend 1-based indexing if there is no name table
            if (!file.Archive.HasNameTable && !file.HadHashTranslated)
                fullpath = $"({file.Index + 1}) {fullpath}";

            var lvi = new ListViewItem(fullpath, GetFileIconIndex(fullpath));

            if (_compareSource == file)
                lvi.BackColor = System.Drawing.Color.LightGreen;

            lvi.SubItems.Add(Common.FormatBytes(file.DisplaySize));
            lvi.SubItems.Add(file.Archive.FileName);
            lvi.ToolTipText = file.GetToolTipText();
            lvi.Tag = file;

#if DEBUG
            if (file is BA2TextureEntry ba2Tex)
                lvi.SubItems.Add(Enum.GetName(typeof(DirectXTexUtility.DXGIFormat), ba2Tex.format));
            else
                lvi.SubItems.Add(string.Empty);
#endif

            e.Item = lvi;
        }

        private void txtSearch_TextChanged(object sender, EventArgs e)
        {
            LimitedAction.RunAfter(_limitSearchId, 500, this.DoSearch);
        }

        private void cbRegex_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.SearchUseRegex = cbRegex.Checked;
            this.DoSearch();
        }

        private async void tvFolders_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            var rootNode = e.Node.GetRootNode() as ArchiveNode;

            if (rootNode.Index == 0)
                return;

            if (!rootNode.Loaded)
            {
                rootNode = await this.OpenArchive(rootNode.FilePath, true);
                rootNode?.Expand();
                e.Cancel = rootNode == null;
                return;
            }

            // Check if node structure has already been built
            if (rootNode.Built)
                return;

            e.Node.Nodes.Clear();
            var nodes = new Dictionary<string, TreeNode>();
            // Initial sorting
            rootNode.Archive.Files.Sort(_filesSorter);
            rootNode.SortingConfig = ArchiveFileSorter.SortingConfig;

            // Keep track of directories with files directly under them
            var directoriesWithFiles = new List<string>();

            // Build the whole node structure
            foreach (var lvi in rootNode.Archive.Files)
            {
                string path = Path.GetDirectoryName(lvi.FullPath);

                if (!directoriesWithFiles.Contains(path.ToLower()))
                    directoriesWithFiles.Add(path.ToLower());

                // Ignore files under root node and already processed nodes
                if (path == string.Empty || nodes.ContainsKey(path.ToLower()))
                    continue;

                string[] dirs = path.Split('\\');

                for (int i = 0; i < dirs.Length; i++)
                {
                    string newpath = string.Join("\\", dirs, 0, i + 1);

                    if (!nodes.ContainsKey(newpath.ToLower()))
                    {
                        var tn = new TreeNode(dirs[i], 1, 1);
                        tn.Tag = newpath;

                        if (i == 0) // Root node
                            e.Node.Nodes.Add(tn);
                        else // Sub nodes
                            nodes[path.ToLower()].Nodes.Add(tn);

                        nodes.Add(newpath.ToLower(), tn);
                    }
                    path = newpath;
                }
            }

            // Insert <Files> nodes under each directory
            foreach (var node in nodes)
            {
                // Only add if there are sub nodes. Node without sub nodes already behaves the same
                if (node.Value.Nodes.Count > 0 && directoriesWithFiles.Contains(node.Key.ToLower()))
                    node.Value.Nodes.Insert(0, new TreeNode("<Files>", 2, 2) { Tag = node.Key });
            }

            if (Settings.Default.SortArchiveDirectories)
            {
                this.SortNodes(rootNode, true);
            }

            rootNode.Built = true;
        }

        private void tvFolders_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (_pauseFiltering || e.Node == null)
                return;

            var rootNode = e.Node.GetRootNode() as ArchiveNode;

            if (rootNode.Loaded == false)
            {
                this.ClearList();
                return;
            }

            // If AllFiles is null, trigger event which will populate it
            if (!rootNode.Built)
                tvFolders_BeforeExpand(null, new TreeViewCancelEventArgs(e.Node, false, TreeViewAction.Unknown));

            // First we collect entries that will be searched
            List<ArchiveEntry> lvis;

            if (rootNode.Index == 0) // 'All' node is selected, handle files in all archives
            {
                lvis = new List<ArchiveEntry>(tvFolders.Nodes
                    .Cast<ArchiveNode>()
                    .Skip(1) // Skip 'All' node
                    .Where(x => x.Loaded)
                    .Select(x => x.Archive.Files.Count)
                    .Sum());

                for (int i = 1; i < tvFolders.Nodes.Count; i++)
                {
                    var archiveNode = (ArchiveNode)tvFolders.Nodes[i];
                    if (archiveNode.Loaded)
                        lvis.AddRange(archiveNode.Archive.Files);
                }
            }
            else if (e.Node.Tag == null) // Root node is selected, handle all files in selected archive
            {
                lvis = new List<ArchiveEntry>(rootNode.Archive.Files);
            }
            else // Handle all files in selected node
            {
                // Ignore casing
                string lowerPath = ((string)e.Node.Tag).ToLower();

                // Only show files under selected node
                lvis = new List<ArchiveEntry>(rootNode.Archive.Files.Count);

                foreach (var lvi in rootNode.Archive.Files)
                {
                    if (e.Node.Text == "<Files>")
                    {
                        string selectedPath = Path.GetDirectoryName(lvi.FullPath.Replace('/', '\\'));

                        // Show files in current node, not including sub nodes
                        if (selectedPath.ToLower() == lowerPath) lvis.Add(lvi);
                    }
                    else
                    {
                        string path = lvi.FullPath.ToLower().Replace('/', '\\');

                        // Show all files under current node, including sub nodes.
                        // Add \ to exclude folders with similar names on same level, for example 'folder' & 'folder2'
                        if (path.StartsWith(lowerPath + '\\')) lvis.Add(lvi);
                    }
                }

                lvis.TrimExcess();
            }

            if (rootNode.Index == 0 || rootNode.SortingConfig != ArchiveFileSorter.SortingConfig)
            {
                lvis.Sort(_filesSorter);
                rootNode.SortingConfig = ArchiveFileSorter.SortingConfig;
            }
            rootNode.SubFiles = lvis.ToArray();

            lvFiles.ScrollToTop();
            this.DoSearch();
        }

        private void OpenArchiveDialog_FileOk(object sender, CancelEventArgs e)
        {
            OpenArchiveDialog.InitialDirectory = Path.GetDirectoryName(OpenArchiveDialog.FileName);
            Settings.Default.OpenArchiveDialog = OpenArchiveDialog.InitialDirectory;
        }

        #region mainMenu1

        private void fileMenuItem_Popup(object sender, EventArgs e)
        {
            closeSelectedArchiveMenuItem.Enabled = this.SelectedArchiveNode?.Index > 0;
            closeAllArchivesMenuItem.Enabled = tvFolders.Nodes.Count > 1;
            extractArchivesMenuItem.Enabled = tvFolders.Nodes.Cast<ArchiveNode>().Skip(1).Any(x => x.Loaded);
        }

        private async void openArchiveMenuItem_Click(object sender, EventArgs e)
        {
            if (OpenArchiveDialog.ShowDialog(this) == DialogResult.OK)
                await this.OpenArchives(true, OpenArchiveDialog.FileNames);
        }

        private void closeSelectedArchiveMenuItem_Click(object sender, EventArgs e)
        {
            if (this.SelectedArchiveNode == null || this.SelectedArchiveNode.Index == 0)
                return;

            this.CloseArchive(SelectedArchiveNode);
        }

        private void closeAllArchivesMenuItem_Click(object sender, EventArgs e)
        {
            this.CloseArchives();
        }

        private void extractArchivesMenuItem_Click(object sender, EventArgs e)
        {
            var archives = tvFolders.Nodes
                .Cast<ArchiveNode>()
                .Skip(1)
                .Where(x => x.Loaded)
                .Select(x => x.Archive);
            var dialog = ExtractArchivesDialog.ShowDialog(this, archives);

            if (dialog.DialogResult != DialogResult.OK)
                return;

            var files = dialog.Selected
                .Select(x => x.Files)
                .SelectMany(x => x);

            this.ExtractFilesTo(true, true, files);
        }

        private void optionsMenuItem_Click(object sender, EventArgs e)
        {
            this.ShowOptions();
        }

        private void recentFilesMenuItem_Popup(object sender, EventArgs e)
        {
            emptyListMenuItem.Enabled = recentFilesMenuItem.MenuItems.Count > 2;
        }

        private void emptyListMenuItem_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show(this,
                                "Are you sure?",
                                "Confirmation",
                                MessageBoxButtons.YesNo,
                                MessageBoxIcon.Warning) == DialogResult.No)
                return;

            for (int i = recentFilesMenuItem.MenuItems.Count - 1; i != 1; i--)
                recentFilesMenuItem.MenuItems.RemoveAt(i);
        }

        private async void recentFiles_Click(object sender, EventArgs e)
        {
            var item = sender as MenuItem;
            string file = item.Tag.ToString();

            if (!string.IsNullOrEmpty(file) && File.Exists(file))
            {
                await this.OpenArchive(file, true);
            }
            else
            {
                if (MessageBox.Show(this,
                        $"\"{file}\" doesn't exist anymore.\n\n" + "Do you want to remove it from the recent files list?",
                        "Lost File",
                        MessageBoxButtons.YesNo) == DialogResult.Yes)
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

            copyMenuItem.Enabled = hasSelectedItems;
        }

        private void copyPathMenuItem_Click(object sender, EventArgs e)
        {
            var builder = new StringBuilder();

            foreach (int index in lvFiles.SelectedIndices)
            {
                if (!string.IsNullOrEmpty(builder.ToString()))
                    builder.AppendLine();

                builder.Append(VisibleFiles[index].FullPath);
            }

            Clipboard.SetText(builder.ToString());
        }

        private void copyFolderPathMenuItem_Click(object sender, EventArgs e)
        {
            var builder = new StringBuilder();

            foreach (int index in lvFiles.SelectedIndices)
            {
                if (!string.IsNullOrEmpty(builder.ToString()))
                    builder.AppendLine();

                builder.Append(Path.GetDirectoryName(VisibleFiles[index].FullPath));
            }

            Clipboard.SetText(builder.ToString());
        }

        private void copyFileNameMenuItem_Click(object sender, EventArgs e)
        {
            var builder = new StringBuilder();

            foreach (int index in lvFiles.SelectedIndices)
            {
                if (!string.IsNullOrEmpty(builder.ToString()))
                    builder.AppendLine();

                builder.Append(Path.GetFileName(VisibleFiles[index].FullPath));
            }

            Clipboard.SetText(builder.ToString());
        }

        private void selectAllMenuItem_Click(object sender, EventArgs e)
        {
            lvFiles.SelectAllItems();
        }

        private void compareArchivesMenuItem_Click(object sender, EventArgs e)
        {
            var archives = tvFolders.Nodes
                .Cast<ArchiveNode>()
                .Skip(1)
                .Where(x => x.Loaded)
                .Select(x => x.Archive);

            if (_compareForm == null || _compareForm.IsDisposed)
                _compareForm = new CompareForm(archives);

            _compareForm.Show(this);
        }

        private void openFoldersMenuItem_Click(object sender, EventArgs e)
        {
            if (openFoldersMenuItem.MenuItems.Count == 0)
                this.ShowOptions(OptionsForm.QuickExtractIndex);
        }

        private void openFolderMenuItem_Click(object sender, EventArgs e)
        {
            var menuItem = sender as MenuItem;
            var path = menuItem.Tag as QuickExtractPath;

            if (!Directory.Exists(path.Path))
            {
                MessageBox.Show(this, $"{path.Name}'s path no longer exists.");
                return;
            }

            Process.Start(path.Path);
        }

        private void helpMenuItem_Popup(object sender, EventArgs e)
        {
            // Remove UpdateMarker from Text
            if (helpMenuItem.Text.StartsWith(UpdateMarker))
                helpMenuItem.Text = helpMenuItem.Text.Remove(0, UpdateMarker.Length);
        }

        private async void checkForUpdateMenuItem_Click(object sender, EventArgs e)
        {
            // Remove UpdateMarker from Text
            if (checkForUpdateMenuItem.Text.StartsWith(UpdateMarker))
                checkForUpdateMenuItem.Text = checkForUpdateMenuItem.Text.Remove(0, UpdateMarker.Length);

            try
            {
                if (await this.IsUpdateAvailable())
                {
                    if (MessageBox.Show(this,
                            "Update available!\n\n" + "Do you want to open the BSA Browser NexusMods page?",
                            "Update available",
                            MessageBoxButtons.YesNo) == DialogResult.Yes)
                    {
                        Process.Start(Program.SkyrimSENexus);
                    }
                }
                else
                {
                    MessageBox.Show(this, "You have the latest version.",
                        "Update",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
            catch (Win32Exception)
            {
                MessageBox.Show(this, "Couldn't open the Nexus Mods page.",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    "Error checking for update.\n\nException: " + ex.Message,
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void githubMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start(Program.GitHub);
        }

        private void fallout4NexusPageMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start(Program.Fallout4Nexus);
        }

        private void skyrimSENexusPageMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start(Program.SkyrimSENexus);
        }

        private void discordMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start(Program.Discord);
        }

        private void aboutMenuItem_Click(object sender, EventArgs e)
        {
            using (var ab = new AboutBox())
            {
                ab.ShowDialog(this);
            }
        }

        #endregion

        #region contextMenu1

        private void contextMenu1_Popup(object sender, EventArgs e)
        {
            bool hasSelectedItems = lvFiles.SelectedIndices.Count > 0;

            extractMenuItem.Enabled = hasSelectedItems;
            extractFoldersMenuItem.Enabled = hasSelectedItems;
            previewMenuItem.Enabled = hasSelectedItems;

            quickExtractsMenuItem.Enabled = hasSelectedItems;
            copyMenuItem1.Enabled = hasSelectedItems;

            compareMenuItem.Enabled = hasSelectedItems;

            if (_compareSource != null)
            {
                compareMenuItem.Text = "Compare to " + _compareSource.FileName;
                compareCancelMenuItem.Visible = true;
            }
            else
            {
                compareMenuItem.Text = "Compare...";
                compareCancelMenuItem.Visible = false;
            }
        }

        private void extractMenuItem_Click(object sender, EventArgs e)
        {
            if (lvFiles.SelectedIndices.Count == 0)
                return;

            this.ExtractFilesTo(false, true, this.GetSelectedEntries());
        }

        private void extractFoldersMenuItem_Click(object sender, EventArgs e)
        {
            if (lvFiles.SelectedIndices.Count == 0)
                return;

            this.ExtractFilesTo(true, true, this.GetSelectedEntries());
        }

        private void previewMenuItem_Click(object sender, EventArgs e)
        {
            this.PreviewSelected();
        }

        private void quickExtractsMenuItem_Click(object sender, EventArgs e)
        {
            if (quickExtractsMenuItem.MenuItems.Count > 0)
                return;

            // Open options with second tab selected
            using (var of = new OptionsForm(OptionsForm.QuickExtractIndex))
            {
                if (of.ShowDialog(this) == DialogResult.OK)
                {
                    of.SaveChanges();
                    Settings.Default.Save();

                    // Sync changes to UI
                    this.LoadQuickExtractPaths();
                }
            }
        }

        private void quickExtractMenuItem_Click(object sender, EventArgs e)
        {
            var menuItem = sender as MenuItem;
            var path = menuItem.Tag as QuickExtractPath;

            if (!Directory.Exists(path.Path))
            {
                DialogResult result = MessageBox.Show(this,
                    string.Format("{0} path doesn't exists anymore. Do you want to create it?", path.Name),
                    "Quick Extract",
                    MessageBoxButtons.YesNo);

                if (result == DialogResult.No)
                    return;

                Directory.CreateDirectory(path.Path);
            }

            var files = this.GetSelectedEntries().ToList();

            Common.ExtractFiles(this, path.Path, path.UseFolderPath, true, files, titleProgress: true);
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

        private void selectAllMenuItem1_Click(object sender, EventArgs e)
        {
            lvFiles.SelectAllItems();
        }

        ArchiveEntry _compareSource;
        Tools.CompareEntryWindow _compareEntryWindow;

        private void compareMenuItem_Click(object sender, EventArgs e)
        {
            if (_compareEntryWindow == null)
            {
                _compareEntryWindow = new Tools.CompareEntryWindow();
                _compareEntryWindow.Disposed += delegate
                {
                    compareCancelMenuItem.PerformClick();
                };
            }

            if (_compareSource == null)
            {
                _compareSource = VisibleFiles[lvFiles.SelectedIndices[0]];
                _compareEntryWindow.SetSource(_compareSource);
            }
            else
            {
                _compareEntryWindow.SetEntries(this.GetSelectedEntries());
            }

            if (!_compareEntryWindow.Visible)
                _compareEntryWindow.Show();
        }

        private void compareCancelMenuItem_Click(object sender, EventArgs e)
        {
            int index = VisibleFiles.IndexOf(_compareSource);
            _compareSource = null;

            if (index > -1)
                lvFiles.RedrawItems(index, index, false);

            _compareEntryWindow.Close();
            _compareEntryWindow = null;
        }

        #endregion

        #region archiveContextMenu

        private void archiveContextMenu_Popup(object sender, EventArgs e)
        {
            TreeNode selectedNode = tvFolders.GetNodeAt(tvFolders.PointToClient(Cursor.Position));

            // Ignore if the 'All' node is selected
            if (selectedNode?.Index == 0)
                selectedNode = null;

            // Disable these when there are no selected node
            extractAllFilesMenuItem.Enabled
                = extractAllFoldersMenuItem.Enabled
                = reloadMenuItem.Enabled
                = openArchiveMnuItem.Enabled
                = closeMenuItem.Enabled
                = selectedNode != null;

            archiveContextMenu.Tag = selectedNode;
        }

        private void extractAllFilesMenuItem_Click(object sender, EventArgs e)
        {
            this.ExtractFilesTo(false, true, (archiveContextMenu.Tag as ArchiveNode).Archive.Files);
        }

        private void extractAllFoldersMenuItem_Click(object sender, EventArgs e)
        {
            this.ExtractFilesTo(true, true, (archiveContextMenu.Tag as ArchiveNode).Archive.Files);
        }

        private async void reloadMenuItem_Click(object sender, EventArgs e)
        {
            var archiveNode = archiveContextMenu.Tag as ArchiveNode;
            var index = archiveNode.Index;
            var path = archiveNode.Archive.FullPath;

            // Save expanded nodes
            tvFolders.BeginUpdate();
            var expansionState = archiveNode.Nodes.GetExpansionState();
            var isExpanded = archiveNode.IsExpanded;

            _pauseFiltering = true;
            this.CloseArchive(archiveNode);
            _pauseFiltering = false;
            var newNode = await this.OpenArchive(path, false, index);

            // Restore expanded nodes
            newNode.Nodes.SetExpansionState(expansionState);
            if (isExpanded) newNode.Expand();
            tvFolders.EndUpdate();

            this.DoSearch();
        }

        private void openContainingFolderMenuItem_Click(object sender, EventArgs e)
        {
            Common.OpenContainingDirectory(this,
                (archiveContextMenu.Tag as ArchiveNode).Archive.FullPath);
        }

        private void closeMenuItem_Click(object sender, EventArgs e)
        {
            this.CloseArchive((ArchiveNode)archiveContextMenu.Tag);

            if (tvFolders.Nodes.Count == 1)
                this.ClearList();
            else
                this.DoSearch();
        }

        #endregion

        #region unloadedArchiveContextMenu

        private void unloadedArchiveContextMenu_Popup(object sender, EventArgs e)
        {
            var selectedNode = tvFolders.GetNodeAt(tvFolders.PointToClient(Cursor.Position));

            unloadedOpenContainingFolderMenuItem.Enabled
                = unloadedLoadMenuItem.Enabled
                = unloadedRemoveMenuItem.Enabled = selectedNode != null;

            unloadedArchiveContextMenu.Tag = selectedNode;
        }

        private void unloadedOpenContainingFolderMenuItem_Click(object sender, EventArgs e)
        {
            Common.OpenContainingDirectory(this,
                (unloadedArchiveContextMenu.Tag as ArchiveNode).FilePath);
        }

        private async void unloadedLoadMenuItem_Click(object sender, EventArgs e)
        {
            await this.OpenArchive((unloadedArchiveContextMenu.Tag as ArchiveNode).FilePath, true);
        }

        private void unloadedRemoveMenuItem_Click(object sender, EventArgs e)
        {
            this.CloseArchive((ArchiveNode)unloadedArchiveContextMenu.Tag);

            if (tvFolders.Nodes.Count == 1)
                this.ClearList();
            else
                this.DoSearch();
        }

        #endregion

        #region foldersContextMenu

        private void removeLoadedMenuItem_Click(object sender, EventArgs e)
        {
            this.CloseArchives(true);
        }

        private void removeUnloadedMenuItem_Click(object sender, EventArgs e)
        {
            for (int i = tvFolders.Nodes.Count - 1; i > 0; i--)
            {
                if (!(tvFolders.Nodes[i] as ArchiveNode).Loaded)
                    this.CloseArchive((ArchiveNode)tvFolders.Nodes[i]);
            }

            if (tvFolders.Nodes.Count == 1)
                this.ClearList();
            else
                this.DoSearch();
        }

        #endregion

        /// <summary>
        /// Opens the given archive, adding it to the <see cref="tvFolders"/> and making it browsable, then returns containing <see cref="ArchiveNode"/>.
        /// </summary>
        /// <param name="path">The archive file path.</param>
        /// <param name="addToRecentFiles">True if archive should be added to recent files list.</param>
        /// <param name="index">Where to insert new node. Must be equal or more than 1, any other value defaults to last index.</param>
        public async Task<ArchiveNode> OpenArchive(string path,
                                                   bool addToRecentFiles = false,
                                                   int index = -1,
                                                   CancellationToken? cancellationToken = null)
        {
            if (!File.Exists(path))
            {
                MessageBox.Show(this, "File not found. Make sure it exists then try again.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }

            // Check if archive is already opened
            if (this.TryIndexOfArchive(path, out int archiveIndex))
            {
                var archiveNode = (ArchiveNode)tvFolders.Nodes[archiveIndex];
                if (!archiveNode.Loaded)
                {
                    // If 'index' is not set, set it to 'archiveIndex'
                    if (index == -1)
                        index = archiveIndex;
                    tvFolders.Nodes.Remove(archiveNode);
                }
                else
                {
                    tvFolders.SelectedNode = tvFolders.Nodes[archiveIndex];
                    return null;
                }
            }

            cancellationToken?.ThrowIfCancellationRequested();

            var archive = await Task.Run(() => Common.OpenArchive(path, this));

            // Return null if above returns null, errors are handled in the method
            if (archive == null)
                return null;

            var text = Path.GetFileNameWithoutExtension(path) + this.DetectGame(path);
            var newNode = new ArchiveNode(text, archive)
            {
                ContextMenu = archiveContextMenu,
                ImageIndex = 3,
                SelectedImageIndex = 3,
                SubFiles = archive.Files.ToArray(),
                ToolTipText = $"Path: {path}\nSize: {Common.FormatBytes(archive.FileSize)}"
            };
            newNode.Nodes.Add("empty");

            // Last chance before UI changes are made
            cancellationToken?.ThrowIfCancellationRequested();

            if (index < 1)
                tvFolders.Nodes.Add(newNode);
            else
                tvFolders.Nodes.Insert(index, newNode);

            if (newNode.IsExpanded)
                newNode.Collapse();

            btnExtractAllFolders.Enabled = true;
            btnExtractAll.Enabled = true;

            if (addToRecentFiles)
                this.AddToRecentFiles(path);

            tvFolders.SelectedNode = newNode;

            _compareForm?.AddArchive(archive);

            return newNode;
        }

        /// <summary>
        /// Opens given archives and returns <see cref="List{T}"/> of <see cref="ArchiveNode"/>.
        /// </summary>
        /// <param name="addToRecentFiles">True if archives should be added to recent files list.</param>
        /// <param name="paths">Sequence of archive file paths.</param>
        public async Task<List<ArchiveNode>> OpenArchives(bool addToRecentFiles, IEnumerable<string> paths)
        {
            return await this.OpenArchives(addToRecentFiles, new List<string>(paths));
        }

        /// <summary>
        /// Opens given archives and returns <see cref="List{T}"/> of <see cref="ArchiveNode"/>.
        /// </summary>
        /// <param name="addToRecentFiles">True if archives should be added to recent files list.</param>
        /// <param name="paths">List of archive file paths.</param>
        public async Task<List<ArchiveNode>> OpenArchives(bool addToRecentFiles, List<string> paths)
        {
            // Create ProgressForm is there are more than 3 paths
            var pf = paths.Count <= 3 ? null : new ProgressForm(paths.Count)
            {
                Header = "Opening archives...",
                Footer = string.Empty,
                Cancelable = true,
                Owner = this
            };

            using (var cts = new CancellationTokenSource())
            {
                try
                {
                    if (pf != null)
                    {
                        pf.Canceled += delegate { cts.Cancel(); };
                        pf.Show(this);
                    }

                    // Disable filtering during load so that filtering isn't triggered after each archive
                    var nodes = new List<ArchiveNode>();

                    _pauseFiltering = true;

                    // Open each path as archive
                    for (int i = 0; i < paths.Count; i++)
                    {
                        if (pf != null)
                        {
                            pf.Progress = i + 1;
                            pf.Description = Path.GetFileName(paths[i]);
                            pf.Footer = $"({pf.Progress}/{paths.Count})";
                        }

                        var a = await this.OpenArchive(paths[i], addToRecentFiles, cancellationToken: cts.Token);

                        // Check if 'a' is null, indicates it's already opened
                        if (a != null)
                            nodes.Add(a);
                    }

                    return nodes;
                }
                catch (OperationCanceledException)
                {
                    Debug.WriteLine("Canceled opening archive(s).");
                    return null;
                }
                finally
                {
                    _pauseFiltering = false;

                    if (pf != null)
                    {
                        pf.Progress = paths.Count;
                        pf.BlockClose = false;
                        pf.Close();
                    }

                    // Manually trigger filtering
                    tvFolders_AfterSelect(this, new TreeViewEventArgs(tvFolders.SelectedNode));
                }
            }
        }

        /// <summary>
        /// Adds archives to tree view as unloaded.
        /// </summary>
        private void AddRememberedArchivesToList()
        {
            // Make sure 'RememberedArchives' is not null
            Settings.Default.RememberedArchives = Settings.Default.RememberedArchives ?? new StringCollection();

            if (!Settings.Default.RememberArchives)
                return;

            foreach (var archivePath in Settings.Default.RememberedArchives)
            {
                if (string.IsNullOrEmpty(archivePath))
                    continue;

                var filename = Path.GetFileNameWithoutExtension(archivePath);
                var newNode = new ArchiveNode(
                    $"{filename}{this.DetectGame(archivePath)}",
                    archivePath);

                newNode.ForeColor = System.Drawing.SystemColors.GrayText;
                newNode.ToolTipText = $"Path: {archivePath}\nSize: Unloaded";
                newNode.ImageIndex = newNode.SelectedImageIndex = 5;
                newNode.ContextMenu = unloadedArchiveContextMenu;
                newNode.Nodes.Add("empty");

                if (newNode.IsExpanded)
                    newNode.Collapse();

                tvFolders.Nodes.Add(newNode);
            }
        }

        /// <summary>
        /// Adds the given file to the recent files list. If it already exists in the list, it gets bumped up to the top.
        /// </summary>
        /// <param name="file">The file to add.</param>
        private void AddToRecentFiles(string file)
        {
            if (string.IsNullOrEmpty(file))
                return;

            if (this.RecentListContains(file))
            {
                var item = this.RecentListGetItemByString(file);

                if (item == null)
                    return;

                recentFilesMenuItem.MenuItems.Remove(item);
                recentFilesMenuItem.MenuItems.Add(2, item);
            }
            else
            {
                var item = new MenuItem(file, recentFiles_Click);
                item.Tag = file;
                recentFilesMenuItem.MenuItems.Add(2, item);
            }

            // Add 2 to include the permanent menu items
            while (recentFilesMenuItem.MenuItems.Count > (Settings.Default.RecentFiles_MaxFiles + 2))
            {
                recentFilesMenuItem.MenuItems.RemoveAt(recentFilesMenuItem.MenuItems.Count - 1);
            }
        }

        /// <summary>
        /// Clears the virtual <see cref="ListView"/>.
        /// </summary>
        private void ClearList()
        {
            lvFiles.BeginUpdate();
            VisibleFiles.Clear();
            lvFiles.VirtualListSize = 0;
            lvFiles.EndUpdate();
        }

        /// <summary>
        /// Closes the given archive, removing it from the <see cref="TreeView"/>.
        /// </summary>
        /// <param name="archiveNode"></param>
        private void CloseArchive(ArchiveNode archiveNode)
        {
            if (SelectedArchiveNode == archiveNode)
                this.ClearList();

            if (archiveNode.Loaded)
            {
                archiveNode.Archive.Close();
                _compareForm?.RemoveArchive(archiveNode.Archive);
            }

            tvFolders.Nodes.Remove(archiveNode);

            GC.Collect();

            if (tvFolders.GetNodeCount(false) == 1)
            {
                btnExtractAllFolders.Enabled = false;
                btnExtractAll.Enabled = false;
            }
        }

        /// <summary>
        /// Closes all open archives, clearing the <see cref="TreeView"/>.
        /// </summary>
        private void CloseArchives(bool loadedOnly = false)
        {
            if (!loadedOnly || this.SelectedArchiveNode.Loaded)
                this.ClearList();

            _pauseFiltering = true;
            for (int i = tvFolders.Nodes.Count - 1; i > 0; i--)
            {
                var node = (ArchiveNode)tvFolders.Nodes[i];

                if (loadedOnly && !node.Loaded)
                    continue;

                if (node.Loaded)
                {
                    node.Archive.Close();
                    _compareForm?.RemoveArchive(node.Archive);
                }

                tvFolders.Nodes.RemoveAt(i);
            }
            _pauseFiltering = false;

            GC.Collect();

            // Disable buttons
            if (tvFolders.GetNodeCount(false) == 1)
            {
                btnExtractAllFolders.Enabled = false;
                btnExtractAll.Enabled = false;
            }
        }

        /// <summary>
        /// Returns <see cref="string"/> with game name if given path is a Fallout 3 or Fallout New Vegas file
        /// since these two games share a lot of file names.
        /// </summary>
        private string DetectGame(string path)
        {
            // path is not a original Fallout file, no additional identifier required
            if (!Path.GetFileName(path).ToLower().StartsWith("fallout -"))
                return string.Empty;

            var f3 = new Regex(@"^.*(Fallout|F)\s{0,1}(3).*$", RegexOptions.IgnoreCase);
            var fnv = new Regex(@"^.*(Fallout|F)\s{0,1}(NV|New\s{0,1}Vegas).*$", RegexOptions.IgnoreCase);

            if (f3.IsMatch(path))
                return " (F3)";
            else if (fnv.IsMatch(path))
                return " (NV)";

            return string.Empty;
        }

        /// <summary>
        /// Searches file list, filtering out not-matching files.
        /// </summary>
        private void DoSearch()
        {
            LimitedAction.Stop(_limitSearchId);

            if (tvFolders.SelectedNode == null)
                return;

            if (SelectedArchiveNode.Loaded == false)
                return;

            // Reset text color
            txtSearch.ForeColor = System.Drawing.SystemColors.WindowText;
            VisibleFiles.Clear();

            try
            {
                if (txtSearch.Text.Length == 0)
                {
                    VisibleFiles.AddRange(this.SelectedArchiveNode.SubFiles);
                }
                else if (cbRegex.Checked)
                {
                    VisibleFiles.AddRange(this.DoSearchRegex(txtSearch.Text));
                }
                else
                {
                    VisibleFiles.AddRange(this.DoSearchSimple(txtSearch.Text));
                }
            }
            catch
            {
                // Set text color to red to indicate an error with the search pattern
                txtSearch.ForeColor = System.Drawing.Color.Red;
                return;
            }

            // Refresh list items
            lvFiles.BeginUpdate();
            lvFiles.VirtualListSize = VisibleFiles.Count;
            lvFiles.Invalidate();
            lvFiles.EndUpdate();

            lFileCount.Text = string.Format("{0:n0} files", VisibleFiles.Count);
        }

        /// <summary>
        /// Searches file list using regex.
        /// </summary>
        /// <param name="searchString">Regex expression to match.</param>
        private IEnumerable<ArchiveEntry> DoSearchRegex(string searchString)
        {
            var regex = new Regex(searchString, RegexOptions.Compiled | RegexOptions.Singleline);
            foreach (var entry in this.SelectedArchiveNode.SubFiles)
            {
                if (regex.IsMatch(entry.FullPath))
                    yield return entry;
            }
        }

        /// <summary>
        /// Searches file list using simple pattern match.
        /// </summary>
        /// <param name="searchString">Pattern to match.</param>
        private IEnumerable<ArchiveEntry> DoSearchSimple(string searchString)
        {
            // Escape special characters, then unescape wild card characters again
            searchString = WildcardPattern.Escape(searchString).Replace("`*", "*");
            var pattern = new WildcardPattern($"*{searchString}*", WildcardOptions.Compiled | WildcardOptions.IgnoreCase);
            foreach (var entry in this.SelectedArchiveNode.SubFiles)
            {
                if (pattern.IsMatch(entry.FullPath))
                    yield return entry;
            }
        }

        /// <summary>
        /// Opens <see cref="OpenFolderDialog"/> to select where to extract file(s).
        /// </summary>
        /// <param name="useFolderPath">True to use full folder path for files, false to extract straight to path.</param>
        /// <param name="gui">True to show a progression dialog.</param>
        /// <param name="files">The files in the selected archive to extract.</param>
        private void ExtractFilesTo(bool useFolderPath, bool gui, IEnumerable<ArchiveEntry> files)
        {
            var archive = this.SelectedArchiveNode.Archive?.FullPath;

            _openFolderDialog.DefaultFolder = Path.GetDirectoryName(archive);

            if (_openFolderDialog.ShowDialog(this) == DialogResult.OK)
            {
                Common.ExtractFiles(this,
                    _openFolderDialog.Folder,
                    useFolderPath,
                    gui,
                    files.ToList(),
                    titleProgress: true);
            }
        }

        /// <summary>
        /// Returns index for file icon in <see cref="filesImageList"/>.
        /// </summary>
        /// <param name="filepath"></param>
        /// <returns></returns>
        private int GetFileIconIndex(string filepath)
        {
            if (lvFiles.SmallImageList == null)
                return -1;

            string ext = Path.GetExtension(filepath).TrimStart('.');

            // Return icon for no association if no ext
            if (string.IsNullOrEmpty(ext))
                return 0;

            // Add missing icons
            if (!filesImageList.Images.ContainsKey(ext))
                filesImageList.Images.Add(ext, SystemIcons.GetFileIcon(filepath));

            return filesImageList.Images.IndexOfKey(ext);
        }

        /// <summary>
        /// Returns selected <see cref="ArchiveEntry"/> in list view.
        /// </summary>
        /// <returns></returns>
        private IEnumerable<ArchiveEntry> GetSelectedEntries()
        {
            foreach (int index in lvFiles.SelectedIndices)
                yield return this.VisibleFiles[index];
        }

        /// <summary>
        /// Returns true if file is supported by this program. False otherwise.
        /// </summary>
        private bool IsSupportedFile(string file)
        {
            switch (Path.GetExtension(file).ToLower())
            {
                case ".bsa":
                case ".ba2":
                case ".dat":
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Returns true if update is available online.
        /// </summary>
        private async Task<bool> IsUpdateAvailable()
        {
            using (var wc = new WebClient())
            {
                // Add tick count to disable caching.
                var onlineVersion = new Version(await wc.DownloadStringTaskAsync(Program.VersionUrl + $"?nocache={Environment.TickCount}"));
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
        /// Shows <see cref="OptionsForm"/>, optionally at specified tab page.
        /// </summary>
        /// <param name="tabPage"></param>
        private void ShowOptions(int tabPage = 0)
        {
            bool replaceGNFExt = Settings.Default.ReplaceGNFExt;

            using (var of = new OptionsForm(tabPage))
            {
                if (of.ShowDialog(this) == DialogResult.OK)
                {
                    of.SaveChanges();
                    Settings.Default.Save();

                    // Sync changes to UI
                    this.LoadQuickExtractPaths();

                    this.RefreshIcons();

                    // Add 2 to include the permanent menu items
                    while (recentFilesMenuItem.MenuItems.Count > (Settings.Default.RecentFiles_MaxFiles + 2))
                    {
                        recentFilesMenuItem.MenuItems.RemoveAt(recentFilesMenuItem.MenuItems.Count - 1);
                    }

                    // Sync changes to archives already opened
                    for (int i = 1; i < tvFolders.Nodes.Count; i++)
                    {
                        var archiveNode = (ArchiveNode)tvFolders.Nodes[i];
                        if (archiveNode.Loaded)
                            archiveNode.Archive.MatchLastWriteTime = Settings.Default.MatchLastWriteTime;
                    }

                    if (Settings.Default.ReplaceGNFExt != replaceGNFExt)
                    {
                        lvFiles.BeginUpdate();
                        foreach (var archive in tvFolders.Nodes
                                                .Cast<ArchiveNode>()
                                                .Skip(1)
                                                .Where(x => x.Loaded && x.Archive.Type == ArchiveTypes.BA2_GNMF))
                        {
                            Common.ReplaceGNFExtensions(archive.SubFiles.OfType<BA2GNFEntry>(), Settings.Default.ReplaceGNFExt);
                        }
                        lvFiles.EndUpdate();
                    }
                }
            }
        }

        /// <summary>
        /// Previews selected file in default program or built-in tool if supported.
        /// </summary>
        private void PreviewSelected()
        {
            if (lvFiles.SelectedIndices.Count == 0)
                return;

            if (lvFiles.SelectedIndices.Count > 1)
            {
                MessageBox.Show(this, "Can only preview one file at a time", "Error");
                return;
            }

            Common.PreviewTexture(this, VisibleFiles[lvFiles.SelectedIndices[0]]);
        }

        /// <summary>
        /// Returns true if recent files list contains the given file, false otherwise.
        /// </summary>
        /// <param name="file">The file to check.</param>
        private bool RecentListContains(string file)
        {
            if (file == null)
                throw new ArgumentNullException(nameof(file));

            return recentFilesMenuItem.MenuItems
                .Cast<MenuItem>()
                .Any(x => file.ToLower() == x.Tag?.ToString().ToLower());
        }

        /// <summary>
        /// Returns the given file's <see cref="MenuItem"/>.
        /// </summary>
        /// <param name="file">The file to get <see cref="MenuItem"/> from.</param>
        private MenuItem RecentListGetItemByString(string file)
        {
            if (file == null)
                throw new ArgumentNullException(nameof(file));

            return recentFilesMenuItem.MenuItems
                .Cast<MenuItem>()
                .First(x => file.ToLower() == x.Tag?.ToString().ToLower());
        }

        /// <summary>
        /// Refreshes icons in tree view and list view.
        /// </summary>
        private void RefreshIcons()
        {
            bool requiresReloadFiles = (!Settings.Default.Icons.HasFlag(Icons.FileList) && lvFiles.SmallImageList != null) ||
                                       (Settings.Default.Icons == Icons.None && lvFiles.SmallImageList != null);
            bool requiresReloadFolders = (!Settings.Default.Icons.HasFlag(Icons.FolderTree) && tvFolders.ImageList != null) ||
                                         (Settings.Default.Icons == Icons.None && tvFolders.ImageList != null);

            if (requiresReloadFiles)
            {
                lvFiles.SmallImageList = null;
                // Need to call this to fix icon spacing persisting after disabling
                lvFiles.GetType().GetMethod(
                    "RecreateHandle",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(lvFiles, null);
                lvFiles.EnableVisualStyles();
            }

            if (requiresReloadFolders)
            {
                tvFolders.ImageList = null;
            }

            if (Settings.Default.Icons.HasFlag(Icons.FileList))
            {
                lvFiles.SmallImageList = filesImageList;
            }

            if (Settings.Default.Icons.HasFlag(Icons.FolderTree))
            {
                tvFolders.ImageList = foldersImageList;
            }
        }

        /// <summary>
        /// Saves the recent files list to <see cref="Settings"/>.
        /// </summary>
        private void SaveRecentFiles()
        {
            if (Settings.Default.RecentFiles == null)
                Settings.Default.RecentFiles = new StringCollection();
            else
                Settings.Default.RecentFiles.Clear();

            // Go bottom-to-top, stopping before clear list and separator items
            for (int i = recentFilesMenuItem.MenuItems.Count - 1; i != 1; i--)
                Settings.Default.RecentFiles.Add(recentFilesMenuItem.MenuItems[i].Tag.ToString());
        }

        /// <summary>
        /// Adds update marker (UpdateMarker constant) to Help and Check for update menu items if there is an update available.
        /// </summary>
        private async void ShowUpdateNotification()
        {
            try
            {
                if (await this.IsUpdateAvailable())
                {
                    helpMenuItem.Text = UpdateMarker + helpMenuItem.Text;
                    checkForUpdateMenuItem.Text = UpdateMarker + checkForUpdateMenuItem.Text;
                }
            }
            catch
            {
                // Do nothing
            }
        }

        /// <summary>
        /// Sorts all items in list according to user selection.
        /// </summary>
        private void SortList()
        {
            ArchiveFileSorter.SetSorter(Settings.Default.SortType, Settings.Default.SortDesc);
            lvFiles.BeginUpdate();

            // Sort the archive so it only needs to be done once
            this.SelectedArchiveNode?.Archive?.Files.Sort(_filesSorter);
            if (this.SelectedArchiveNode != null)
            {
                this.SelectedArchiveNode.SortingConfig = ArchiveFileSorter.SortingConfig;
            }

            // Repopulate 'SelectedArchiveNode.Files' with sorted list by triggering this event
            if (this.SelectedArchiveNode != null)
                tvFolders_AfterSelect(null, new TreeViewEventArgs(tvFolders.SelectedNode, TreeViewAction.Unknown));

            lvFiles.EndUpdate();
        }

        /// <summary>
        /// Sorts all nodes in given <see cref="TreeNode"/>.
        /// </summary>
        /// <param name="rootNode">The <see cref="TreeNode"/> whose children is to be sorted.</param>
        private void SortNodes(TreeNode rootNode, bool sortRoot)
        {
            if (sortRoot)
            {
                var nodes = new TreeNode[rootNode.Nodes.Count];
                rootNode.Nodes.CopyTo(nodes, 0);
                Array.Sort(nodes, _nodeSorter);
                rootNode.Nodes.Clear();
                rootNode.Nodes.AddRange(nodes);
            }

            foreach (TreeNode node in rootNode.Nodes)
            {
                var nodes = new TreeNode[node.Nodes.Count];

                node.Nodes.CopyTo(nodes, 0);

                Array.Sort(nodes, _nodeSorter);

                node.Nodes.Clear();
                node.Nodes.AddRange(nodes);

                this.SortNodes(node, false);
            }
        }

        /// <summary>
        /// Returns true if <paramref name="path"/> is open as a <see cref="ArchiveNode"/>. Parameter <paramref name="index"/> gets set to index of archive, or -1 if not found.
        /// </summary>
        private bool TryIndexOfArchive(string path, out int index)
        {
            // Start at 1 to skip 'All' node
            for (int i = 1; i < tvFolders.Nodes.Count; i++)
            {
                var node = (ArchiveNode)tvFolders.Nodes[i];
                if (node.FilePath.ToLower() == path.ToLower())
                {
                    index = i;
                    return true;
                }
            }
            index = -1;
            return false;
        }
    }
}