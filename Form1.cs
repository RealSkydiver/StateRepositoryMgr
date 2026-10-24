using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SQLite;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Windows.Forms;
using static StateRepositoryMgr.RemovalPlanner;

namespace StateRepositoryMgr
{
    public partial class Form1 : Form
    {
        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int index);

        private const int SM_CLEANBOOT = 67;
        private const string RepositoryFile = "StateRepository-Machine.srd";
        private string selectedDatabasePath;

        // Avoid loading native SQLite components when Visual Studio creates the form.
        private static bool sqliteFunctionsRegistered;
        private static readonly object sqliteRegistrationLock = new object();

        private static void EnsureSQLiteFunctionsRegistered()
        {
            lock (sqliteRegistrationLock)
            {
                if (sqliteFunctionsRegistered) return;
                SQLiteFunction.RegisterFunction(typeof(NoSrJournalFunction));
                SQLiteFunction.RegisterFunction(typeof(NoSrTriggersFunction));
                SQLiteFunction.RegisterFunction(typeof(SrOptionsFunction));
                SQLiteFunction.RegisterFunction(typeof(SrWorkIdFunction));
                SQLiteFunction.RegisterFunction(typeof(SrNowFunction));
                sqliteFunctionsRegistered = true;
            }
        }

        public Form1()
        {
            InitializeComponent();

            lvFiles.View = View.Details;
            lvFiles.FullRowSelect = true;
            lvFiles.GridLines = true;
            lvFiles.Columns.Clear();
            lvFiles.Columns.Add("File", 310);
            lvFiles.Columns.Add("Size", 100);
            lvFiles.Columns.Add("Status", 120);

            lvPackages.View = View.Details;
            lvPackages.FullRowSelect = true;
            lvPackages.GridLines = true;
            lvPackages.MultiSelect = false;
            lvPackages.Columns.Clear();
            lvPackages.Columns.Add("Package family", 180);
            lvPackages.Columns.Add("Versions", 60);
            lvPackages.Columns.Add("Package names", 450);

            txtLog.ReadOnly = true;
            txtEditedPath.ReadOnly = true;
            btnReplace.Enabled = false;

            // All Click events are assigned exclusively in Form1.Designer.cs.
            btnRestore.Enabled = true;
            btnValidate.Enabled = false;

            // The designer must not query services or Windows system files.
            if (LicenseManager.UsageMode != LicenseUsageMode.Designtime)
                RefreshStatus();
        }

        private void RefreshStatus()
        {
            int mode = GetSystemMetrics(SM_CLEANBOOT);
            lblBootMode.Text = mode == 0 ? "Normal" :
                               mode == 1 ? "Safe Mode" :
                               mode == 2 ? "Safe Mode (Network)" : "Unknown";
            lblBootMode.ForeColor = mode == 0 ? Color.DarkGreen : Color.DarkOrange;

            try
            {
                using (var sc = new ServiceController("StateRepository"))
                {
                    sc.Refresh();
                    lblServiceStatus.Text = sc.Status.ToString();
                    lblServiceStatus.ForeColor =
                        sc.Status == ServiceControllerStatus.Running
                        ? Color.DarkGreen : Color.DarkRed;
                }
            }
            catch (Exception ex)
            {
                lblServiceStatus.Text = "Unknown";
                lblServiceStatus.ForeColor = Color.DarkRed;
                Log("Service status error: " + ex.Message);
            }
            RefreshFileList();
        }

        private void RefreshFileList()
        {
            lvFiles.Items.Clear();
            foreach (string suffix in new[] { "", "-wal", "-shm" })
            {
                string name = RepositoryFile + suffix;
                string path = Path.Combine(txtLivePath.Text, name);
                var item = new ListViewItem(name);
                try
                {
                    if (File.Exists(path))
                    {
                        var info = new FileInfo(path);
                        item.SubItems.Add(info.Length.ToString("N0") + " bytes");
                        item.SubItems.Add("Present");
                        item.ForeColor = Color.DarkGreen;
                    }
                    else
                    {
                        item.SubItems.Add("-");
                        item.SubItems.Add("Missing");
                        item.ForeColor = Color.DarkGray;
                    }
                }
                catch (Exception ex)
                {
                    item.SubItems.Add("-");
                    item.SubItems.Add("Error");
                    item.ForeColor = Color.DarkRed;
                    Log("File status error: " + ex.Message);
                }
                lvFiles.Items.Add(item);
            }
        }

        private void Log(string message)
        {
            txtLog.AppendText(DateTime.Now.ToString("HH:mm:ss") +
                              "  " + message + Environment.NewLine);
        }

        private static string FileSha256(string path)
        {
            using (var sha = SHA256.Create())
            using (var input = new FileStream(path, FileMode.Open,
                FileAccess.Read, FileShare.Read))
                return BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "");
        }

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            RefreshStatus();
            Log("System status refreshed.");
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.SelectedPath = txtWorkPath.Text;
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    txtWorkPath.Text = dlg.SelectedPath;
                    selectedDatabasePath = null;
                    lvPackages.Items.Clear();
                    Log("Working directory: " + dlg.SelectedPath);
                }
            }
        }

        private void btnMsConfig_Click(object sender, EventArgs e)
        {
            try
            {
                Process.Start(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                    @"System32\msconfig.exe"));
            }
            catch (Exception ex)
            {
                Log("Cannot start msconfig: " + ex.Message);
                MessageBox.Show(this, ex.Message, "msconfig",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnStopSrvc_Click(object sender, EventArgs e)
        {
            if (GetSystemMetrics(SM_CLEANBOOT) == 0)
            {
                MessageBox.Show(this, "Stopping StateRepository is only allowed in Safe Mode.",
                    "Stop Service", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            try
            {
                using (var sc = new ServiceController("StateRepository"))
                {
                    sc.Refresh();
                    if (sc.Status == ServiceControllerStatus.Stopped)
                    {
                        Log("StateRepository service is already stopped.");
                        return;
                    }
                    if (sc.Status != ServiceControllerStatus.StopPending)
                    {
                        if ((sc.Status != ServiceControllerStatus.Running &&
                             sc.Status != ServiceControllerStatus.Paused) || !sc.CanStop)
                            throw new InvalidOperationException(
                                "Cannot stop service in state: " + sc.Status);
                        Log("Stopping StateRepository service...");
                        sc.Stop();
                    }
                    sc.WaitForStatus(ServiceControllerStatus.Stopped,
                        TimeSpan.FromSeconds(15));
                    sc.Refresh();
                    if (sc.Status != ServiceControllerStatus.Stopped)
                        throw new InvalidOperationException(
                            "Service did not reach Stopped state.");
                    Log("StateRepository service stopped.");
                }
            }
            catch (Exception ex)
            {
                Log("Stop service failed: " + ex.Message);
                MessageBox.Show(this, ex.Message, "Stop Service",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { RefreshStatus(); }
        }

        private void btnBackup_Click(object sender, EventArgs e)
        {
            if (GetSystemMetrics(SM_CLEANBOOT) == 0)
            {
                MessageBox.Show(this, "Backup is only available in Safe Mode.",
                    "Backup", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                using (var sc = new ServiceController("StateRepository"))
                {
                    sc.Refresh();
                    if (sc.Status != ServiceControllerStatus.Stopped)
                        throw new InvalidOperationException(
                            "StateRepository service must be stopped. Current status: " + sc.Status);
                }

                string sourceDir = Path.GetFullPath(txtLivePath.Text.Trim());
                string workDir = Path.GetFullPath(txtWorkPath.Text.Trim());
                EnsureOutsideLive(workDir);

                string backupDir = Path.Combine(workDir,
                    "Backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"));
                string[] files = { RepositoryFile, RepositoryFile + "-wal", RepositoryFile + "-shm" };
                var streams = new Dictionary<string, FileStream>();
                bool success = false;
                try
                {
                    foreach (string file in files)
                    {
                        string path = Path.Combine(sourceDir, file);
                        if (!File.Exists(path)) continue;
                        streams.Add(file, new FileStream(path, FileMode.Open,
                            FileAccess.Read, FileShare.None));
                    }
                    if (!streams.ContainsKey(RepositoryFile))
                        throw new FileNotFoundException("Main repository database is missing.");

                    Directory.CreateDirectory(backupDir);
                    foreach (string file in files)
                    {
                        FileStream source;
                        if (!streams.TryGetValue(file, out source)) continue;
                        string destination = Path.Combine(backupDir, file);
                        source.Position = 0;
                        using (var target = new FileStream(destination, FileMode.CreateNew,
                            FileAccess.Write, FileShare.None))
                        {
                            source.CopyTo(target);
                            target.Flush(true);
                        }
                        if (source.Length != new FileInfo(destination).Length)
                            throw new IOException("File size mismatch: " + file);
                        Log("Backed up: " + file + " (" +
                            source.Length.ToString("N0") + " bytes)");
                    }
                    using (var sc = new ServiceController("StateRepository"))
                    {
                        sc.Refresh();
                        if (sc.Status != ServiceControllerStatus.Stopped)
                            throw new InvalidOperationException(
                                "StateRepository service state changed during backup.");
                    }
                    success = true;
                }
                finally
                {
                    foreach (FileStream stream in streams.Values) stream.Dispose();
                    if (!success && Directory.Exists(backupDir))
                    {
                        try { Directory.Delete(backupDir, true); }
                        catch (Exception ex) { Log("Incomplete backup cleanup failed: " + ex.Message); }
                    }
                }

                selectedDatabasePath = null;
                lvPackages.Items.Clear();
                Log("Backup completed: " + backupDir);
                MessageBox.Show(this, "Backup completed.\n\n" + backupDir +
                    "\n\nDatabase integrity has not yet been validated.",
                    "Backup", MessageBoxButtons.OK, MessageBoxIcon.Information);
                RefreshStatus();
            }
            catch (Exception ex)
            {
                Log("Backup failed: " + ex.Message);
                MessageBox.Show(this, ex.Message, "Backup failed",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void EnsureOutsideLive(string path)
        {
            string live = Path.GetFullPath(txtLivePath.Text.Trim()).TrimEnd('\\');
            string full = Path.GetFullPath(path).TrimEnd('\\');
            if (full.Equals(live, StringComparison.OrdinalIgnoreCase) ||
                full.StartsWith(live + "\\", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "Working directory must be outside the live repository directory.");
        }

        private string FindLatestBackup()
        {
            string workDir = Path.GetFullPath(txtWorkPath.Text.Trim());
            EnsureOutsideLive(workDir);
            if (!Directory.Exists(workDir))
                throw new DirectoryNotFoundException("Working directory not found: " + workDir);

            foreach (string dir in Directory.GetDirectories(workDir, "Backup_*",
                         SearchOption.TopDirectoryOnly).OrderByDescending(Path.GetFileName))
            {
                string path = Path.Combine(dir, RepositoryFile);
                if (File.Exists(path)) return path;
            }
            throw new FileNotFoundException(
                "No StateRepository backup found in the working directory.");
        }

        private static SQLiteConnection OpenDatabase(string path, bool readOnly)
        {
            EnsureSQLiteFunctionsRegistered();
            var builder = new SQLiteConnectionStringBuilder
            {
                DataSource = path,
                Version = 3,
                ReadOnly = readOnly,
                FailIfMissing = true,
                Pooling = false
            };
            var connection = new SQLiteConnection(builder.ConnectionString);
            connection.Open();
            return connection;
        }

        private void btnSearchPackage_Click(object sender, EventArgs e)
        {
            lvPackages.Items.Clear();
            selectedDatabasePath = null;
            string search = txtSearchPackage.Text.Trim();
            if (search.Length == 0)
            {
                MessageBox.Show(this, "Please enter a package search term.",
                    "Search", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try
            {
                string dbPath = FindLatestBackup();
                using (var db = OpenDatabase(dbPath, true))
                using (var cmd = db.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT f._PackageFamilyID, f.PackageFamilyName,
                               COUNT(p._PackageID), GROUP_CONCAT(p.PackageFullName, ' | ')
                        FROM PackageFamily f
                        LEFT JOIN Package p ON p.PackageFamily = f._PackageFamilyID
                        WHERE f.PackageFamilyName LIKE @search
                           OR f.Name LIKE @search
                           OR EXISTS (SELECT 1 FROM Package p2
                                      WHERE p2.PackageFamily = f._PackageFamilyID
                                      AND p2.PackageFullName LIKE @search)
                        GROUP BY f._PackageFamilyID, f.PackageFamilyName
                        ORDER BY f.PackageFamilyName";
                    cmd.Parameters.AddWithValue("@search", "%" + search + "%");
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var item = new ListViewItem(reader.GetString(1));
                            item.SubItems.Add(reader.GetInt64(2).ToString());
                            item.SubItems.Add(reader.IsDBNull(3) ? "" : reader.GetString(3));
                            item.Tag = reader.GetInt64(0);
                            lvPackages.Items.Add(item);
                        }
                    }
                }
                selectedDatabasePath = dbPath;
                Log("Package search: " + search);
                Log("Database: " + dbPath);
                Log("Found " + lvPackages.Items.Count + " package families.");
            }
            catch (Exception ex)
            {
                Log("Package search failed: " + ex.Message);
                MessageBox.Show(this, ex.Message, "Package search failed",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnRemovePackage_Click(object sender, EventArgs e)
        {
            if (lvPackages.SelectedItems.Count != 1)
            {
                MessageBox.Show(this, "Please select exactly one package family.",
                    "Remove Package", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (string.IsNullOrEmpty(selectedDatabasePath) ||
                !File.Exists(selectedDatabasePath))
            {
                MessageBox.Show(this, "Source backup is missing. Search again.",
                    "Remove Package", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var item = lvPackages.SelectedItems[0];
            long familyId = Convert.ToInt64(item.Tag);
            string familyName = item.Text;
            if (MessageBox.Show(this,
                    "Remove the ENTIRE package family from a NEW working copy?\n\n" +
                    familyName + "\n\nAll versions will be removed.\n" +
                    "The original backup and live database will not be changed.",
                    "Confirm removal", MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2)
                != DialogResult.Yes) return;

            string outputDir = null;
            bool complete = false;
            try
            {
                string workDir = Path.GetFullPath(txtWorkPath.Text.Trim());
                EnsureOutsideLive(workDir);
                EnsureOutsideLive(selectedDatabasePath);
                outputDir = Path.Combine(workDir,
                    "Edited_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"));
                Directory.CreateDirectory(outputDir);

                string sourceDir = Path.GetDirectoryName(selectedDatabasePath);
                string destDb = Path.Combine(outputDir, RepositoryFile);
                foreach (string suffix in new[] { "", "-wal", "-shm" })
                {
                    string name = RepositoryFile + suffix;
                    string source = Path.Combine(sourceDir, name);
                    if (File.Exists(source))
                        File.Copy(source, Path.Combine(outputDir, name), false);
                }
                if (!File.Exists(destDb))
                    throw new FileNotFoundException("Working copy has no .srd file.");

                int removedCount;
                using (var db = OpenDatabase(destDb, false))
                {
                    RequireIntegrity(db);
                    DbSchema schema = ReadSchema(db);
                    var selected = PlanRemoval(db, schema, familyId, familyName);
                    removedCount = selected.Values.Sum(x => x.Count);
                    Log("Removal plan for " + familyName + ": " +
                        removedCount + " rows in " +
                        selected.Count(x => x.Value.Count != 0) + " tables.");
                    if (removedCount <= 1)
                        throw new InvalidOperationException(
                            "No related package entries found. Removal cancelled.");

                    using (var cmd = db.CreateCommand())
                    {
                        cmd.CommandText = "BEGIN IMMEDIATE";
                        cmd.ExecuteNonQuery();
                    }
                    bool committed = false;
                    try
                    {
                        DeleteSelection(db, schema, selected);
                        CheckNoRemainingReferences(db, schema, selected);
                        RequireIntegrity(db);
                        using (var cmd = db.CreateCommand())
                        {
                            cmd.CommandText = "COMMIT";
                            cmd.ExecuteNonQuery();
                        }
                        committed = true;
                    }
                    finally
                    {
                        if (!committed)
                        {
                            using (var cmd = db.CreateCommand())
                            {
                                cmd.CommandText = "ROLLBACK";
                                cmd.ExecuteNonQuery();
                            }
                        }
                    }
                    RequireIntegrity(db);
                    using (var cmd = db.CreateCommand())
                    {
                        cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE)";
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read() && Convert.ToInt32(reader[0]) != 0)
                                throw new InvalidOperationException("WAL checkpoint is busy.");
                        }
                    }
                }
                using (var db = OpenDatabase(destDb, true))
                {
                    RequireIntegrity(db);
                    if (ScalarLong(db,
                        "SELECT COUNT(*) FROM PackageFamily WHERE _PackageFamilyID=@p0", familyId) != 0)
                        throw new InvalidOperationException("Target family still exists.");
                }

                // Only a self-contained .srd is eligible for Replace.
                // A nonempty WAL may contain transactions not present in the main file.
                string editedWal = destDb + "-wal";
                if (File.Exists(editedWal) && new FileInfo(editedWal).Length != 0)
                    throw new InvalidOperationException(
                        "Edited database still has a nonempty WAL. Removal output rejected.");
                if (File.Exists(editedWal)) File.Delete(editedWal);
                string editedShm = destDb + "-shm";
                if (File.Exists(editedShm)) File.Delete(editedShm);
                VerifyDatabaseSet(destDb, false);
                txtEditedPath.Text = Path.GetFullPath(outputDir);
                btnReplace.Enabled = true;

                complete = true;
                Log("Removed " + familyName + ": " + removedCount + " rows.");
                Log("Edited database: " + destDb);
                Log("SQLite integrity_check: ok");
                MessageBox.Show(this,
                    "Removal completed on WORKING COPY.\n\n" +
                    "Package family: " + familyName + "\n" +
                    "Deleted rows: " + removedCount + "\n" +
                    "Integrity: ok\n\n" + destDb + "\n\n" +
                    "The live repository has NOT been changed.",
                    "Removal completed", MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Log("Removal failed: " + ex.Message);
                MessageBox.Show(this, ex.Message, "Removal failed",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (!complete && outputDir != null && Directory.Exists(outputDir))
                {
                    try { Directory.Delete(outputDir, true); }
                    catch (Exception ex) { Log("Cleanup failed: " + ex.Message); }
                }
            }
        }

        // Offline repository swap -------------------------------------------------
        // Both operations leave the original backup/edited copy untouched.
        // A recovery snapshot is written before any live file is renamed.

        private static readonly string[] RepositorySuffixes = { "", "-wal", "-shm" };

        private static string FileFor(string directory, string suffix)
        {
            return Path.Combine(directory, RepositoryFile + suffix);
        }

        private static string UniqueOperationId()
        {
            return DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + "_" +
                   Guid.NewGuid().ToString("N").Substring(0, 8);
        }

        private string GetLiveDirectory()
        {
            string expected = Path.GetFullPath(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                @"Microsoft\Windows\AppRepository")).TrimEnd('\\');
            string actual = Path.GetFullPath(txtLivePath.Text.Trim()).TrimEnd('\\');
            if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "Live path does not match the Windows AppRepository directory.\n" +
                    "Expected: " + expected + "\nCurrent: " + actual);
            return actual;
        }

        private static bool IsInside(string path, string parent)
        {
            string a = Path.GetFullPath(path).TrimEnd('\\');
            string b = Path.GetFullPath(parent).TrimEnd('\\');
            return a.Equals(b, StringComparison.OrdinalIgnoreCase) ||
                   a.StartsWith(b + "\\", StringComparison.OrdinalIgnoreCase);
        }

        private void EnsureOfflineAndStopped()
        {
            if (GetSystemMetrics(SM_CLEANBOOT) == 0)
                throw new InvalidOperationException(
                    "Replace/Restore is only permitted in Windows Safe Mode.");
            using (var sc = new ServiceController("StateRepository"))
            {
                sc.Refresh();
                if (sc.Status != ServiceControllerStatus.Stopped)
                    throw new InvalidOperationException(
                        "StateRepository must be Stopped. Current state: " + sc.Status);
            }
        }

        private static void CheckExclusiveRepositoryAccess(string liveDir)
        {
            // Check that no process currently has an incompatible handle.
            // The service status is checked again immediately before renaming.
            var handles = new List<FileStream>();
            try
            {
                foreach (string suffix in RepositorySuffixes)
                {
                    string path = FileFor(liveDir, suffix);
                    if (File.Exists(path))
                        handles.Add(new FileStream(path, FileMode.Open,
                            FileAccess.Read, FileShare.None));
                }
                if (!File.Exists(FileFor(liveDir, "")))
                    throw new FileNotFoundException("Live StateRepository .srd is missing.");
            }
            finally
            {
                foreach (var handle in handles) handle.Dispose();
            }
        }

        private static void CopyFileWithSizeCheck(string source, string destination)
        {
            using (var input = new FileStream(source, FileMode.Open,
                FileAccess.Read, FileShare.Read))
            using (var output = new FileStream(destination, FileMode.CreateNew,
                FileAccess.Write, FileShare.None))
            {
                input.CopyTo(output);
                output.Flush(true);
                if (input.Length != output.Length)
                    throw new IOException("Copy size mismatch for " + Path.GetFileName(source));
            }
        }

        private string CreateRecoverySnapshot(string liveDir, string workDir,
            string prefix)
        {
            // Open all three before copying to obtain a consistent offline set.
            var streams = new Dictionary<string, FileStream>();
            string snapshot = Path.Combine(workDir, prefix + "_" + UniqueOperationId());
            bool completed = false;
            try
            {
                foreach (string suffix in RepositorySuffixes)
                {
                    string path = FileFor(liveDir, suffix);
                    if (File.Exists(path))
                        streams.Add(suffix, new FileStream(path, FileMode.Open,
                            FileAccess.Read, FileShare.None));
                }
                if (!streams.ContainsKey(""))
                    throw new FileNotFoundException("Live StateRepository .srd is missing.");

                Directory.CreateDirectory(snapshot);
                foreach (string suffix in RepositorySuffixes)
                {
                    FileStream source;
                    if (!streams.TryGetValue(suffix, out source)) continue;
                    source.Position = 0;
                    string targetPath = FileFor(snapshot, suffix);
                    using (var output = new FileStream(targetPath, FileMode.CreateNew,
                        FileAccess.Write, FileShare.None))
                    {
                        source.CopyTo(output);
                        output.Flush(true);
                        if (source.Length != output.Length)
                            throw new IOException("Recovery snapshot copy size mismatch: " + suffix);
                    }
                    Log("Recovery saved: " + Path.GetFileName(targetPath) +
                        " (" + source.Length.ToString("N0") + " bytes)");
                }
                EnsureOfflineAndStopped();
                completed = true;
                return snapshot;
            }
            finally
            {
                foreach (var file in streams.Values) file.Dispose();
                if (!completed && Directory.Exists(snapshot))
                {
                    try { Directory.Delete(snapshot, true); }
                    catch (Exception ex) { Log("Incomplete snapshot remains: " + ex.Message); }
                }
            }
        }

        private void VerifyDatabaseSet(string sourceMain, bool includeSidecars)
        {
            // Validate a separate disposable directory, never the supplied source.
            string workDir = Path.GetFullPath(txtWorkPath.Text.Trim());
            EnsureOutsideLive(workDir);
            Directory.CreateDirectory(workDir);
            string verifyDir = Path.Combine(workDir, "Verify_" + UniqueOperationId());
            Directory.CreateDirectory(verifyDir);
            try
            {
                string sourceDir = Path.GetDirectoryName(sourceMain);
                foreach (string suffix in RepositorySuffixes)
                {
                    if (suffix != "" && !includeSidecars) continue;
                    string file = FileFor(sourceDir, suffix);
                    if (!File.Exists(file))
                    {
                        if (suffix == "")
                            throw new FileNotFoundException("Source database missing: " + file);
                        continue;
                    }
                    CopyFileWithSizeCheck(file, FileFor(verifyDir, suffix));
                }
                using (var db = OpenDatabase(FileFor(verifyDir, ""), true))
                    RequireIntegrity(db);
            }
            finally
            {
                try { Directory.Delete(verifyDir, true); }
                catch (Exception ex) { Log("Verification cleanup failed: " + ex.Message); }
            }
        }

        private static void StagePermissionsFromLive(string liveMain, string stagedMain)
        {
            // Preserve the security descriptor and attributes expected by Windows.
            // Copy the DACL; copying the protected Windows file owner can
            // require privileges that even an elevated administrator lacks.
            FileSecurity security = File.GetAccessControl(
                liveMain, AccessControlSections.Access);
            File.SetAccessControl(stagedMain, security);
            FileAttributes attributes = File.GetAttributes(liveMain) &
                (FileAttributes.ReadOnly | FileAttributes.Hidden |
                 FileAttributes.System | FileAttributes.Archive);
            File.SetAttributes(stagedMain,
                attributes == 0 ? FileAttributes.Normal : attributes);
        }

        private void ApplyRepositorySet(string sourceDir, bool restoreSidecars,
            string operationName, string expectedMainHash = null)
        {
            // Caller has already chosen the source and verified its SQLite integrity.
            EnsureOfflineAndStopped();
            string liveDir = GetLiveDirectory();
            string workDir = Path.GetFullPath(txtWorkPath.Text.Trim());
            EnsureOutsideLive(workDir);
            EnsureOutsideLive(sourceDir);
            if (!IsInside(sourceDir, workDir))
                throw new InvalidOperationException(
                    "Source must be inside the configured working directory.");
            if (!Directory.Exists(liveDir))
                throw new DirectoryNotFoundException("AppRepository directory not found.");

            CheckExclusiveRepositoryAccess(liveDir);

            // Durable recovery snapshot before touching the live directory.
            string recovery = CreateRecoverySnapshot(liveDir, workDir,
                operationName == "Restore" ? "PreRestore" : "Rollback");
            Log("Recovery snapshot: " + recovery);

            string id = UniqueOperationId();
            var staged = new Dictionary<string, string>();
            var held = new Dictionary<string, string>();
            var installed = new List<string>();
            bool finished = false;
            bool rollbackComplete = true;
            try
            {
                // Stage the ENTIRE intended replacement set inside AppRepository,
                // without touching any canonical names yet.
                foreach (string suffix in RepositorySuffixes)
                {
                    if (suffix != "" && !restoreSidecars) continue;
                    string from = FileFor(sourceDir, suffix);
                    if (!File.Exists(from))
                    {
                        if (suffix == "")
                            throw new FileNotFoundException("Replacement .srd missing: " + from);
                        continue;
                    }
                    string destination = FileFor(liveDir, suffix) + ".stage_" + id;
                    staged.Add(suffix, destination);
                    CopyFileWithSizeCheck(from, destination);
                }

                // StagePermissionsFromLive(FileFor(liveDir, ""), staged[""]);
                if (expectedMainHash != null &&
                    !string.Equals(FileSha256(staged[""]), expectedMainHash,
                        StringComparison.OrdinalIgnoreCase))
                    throw new IOException("Staged edited repository does not match prepared checksum.");
                EnsureOfflineAndStopped();
                CheckExclusiveRepositoryAccess(liveDir);

                // Move journals FIRST; old journals must never be paired with the
                // new database. Hold original files in the same directory so
                // renames remain on the same volume.
                foreach (string suffix in new[] { "-shm", "-wal", "" })
                {
                    string current = FileFor(liveDir, suffix);
                    if (!File.Exists(current)) continue;
                    string hold = current + ".hold_" + id;
                    File.Move(current, hold);
                    held.Add(suffix, hold);
                }

                // Install the main DB first, then only the journals if RESTORING
                // an exact previous repository snapshot.
                foreach (string suffix in RepositorySuffixes)
                {
                    string stage;
                    if (!staged.TryGetValue(suffix, out stage)) continue;
                    File.Move(stage, FileFor(liveDir, suffix));
                    installed.Add(suffix);
                }

                EnsureOfflineAndStopped();
                if (!File.Exists(FileFor(liveDir, "")))
                    throw new IOException("Installed repository .srd is missing.");
                if (!restoreSidecars &&
                    (File.Exists(FileFor(liveDir, "-wal")) ||
                     File.Exists(FileFor(liveDir, "-shm"))))
                    throw new IOException("Old journal files are still at the live path.");

                finished = true;
                Log(operationName + " completed successfully.");
            }
            catch (Exception originalError)
            {
                Log(operationName + " failed: " + originalError.Message);

                // Recover from the still-present originals (.hold_*). Move
                // newly installed files aside instead of destroying them.
                foreach (string suffix in installed.AsEnumerable().Reverse())
                {
                    try
                    {
                        string current = FileFor(liveDir, suffix);
                        if (File.Exists(current))
                            File.Move(current, current + ".failed_" + id);
                    }
                    catch (Exception ex)
                    {
                        rollbackComplete = false;
                        Log("Cannot move failed replacement aside: " + ex.Message);
                    }
                }
                foreach (string suffix in new[] { "", "-wal", "-shm" })
                {
                    string oldPath;
                    if (!held.TryGetValue(suffix, out oldPath)) continue;
                    try
                    {
                        string destination = FileFor(liveDir, suffix);
                        if (File.Exists(destination))
                            throw new IOException("Original destination is occupied: " + destination);
                        File.Move(oldPath, destination);
                    }
                    catch (Exception ex)
                    {
                        rollbackComplete = false;
                        Log("AUTOMATIC ROLLBACK FAILED: " + ex.Message);
                    }
                }

                string details = operationName + " failed:\n" + originalError.Message +
                    "\n\nRecovery snapshot:\n" + recovery +
                    "\n\nAutomatic rollback: " +
                    (rollbackComplete ? "completed" : "INCOMPLETE - DO NOT REBOOT");
                throw new InvalidOperationException(details, originalError);
            }
            finally
            {
                // Never clean up originals held in AppRepository after an error.
                if (finished)
                {
                    foreach (string hold in held.Values)
                    {
                        try { if (File.Exists(hold)) File.Delete(hold); }
                        catch (Exception ex)
                        {
                            Log("Original .hold file retained: " + hold + " (" + ex.Message + ")");
                        }
                    }
                }
                foreach (string stage in staged.Values)
                {
                    try { if (File.Exists(stage)) File.Delete(stage); }
                    catch (Exception ex) { Log("Staging file retained: " + stage + " (" + ex.Message + ")"); }
                }
                if (finished)
                    Log("Recovery snapshot retained at: " + recovery);
                RefreshStatus();
            }
        }

        private string LatestDirectory(string prefix)
        {
            string root = Path.GetFullPath(txtWorkPath.Text.Trim());
            if (!Directory.Exists(root)) return null;
            return Directory.GetDirectories(root, prefix + "_*", SearchOption.TopDirectoryOnly)
                .Where(d => File.Exists(FileFor(d, "")))
                .OrderByDescending(Path.GetFileName)
                .FirstOrDefault();
        }

        private void btnReplace_Click(object sender, EventArgs e)
        {
            // The visible read-only path is the sole source for Replace.
            // A failed attempt must not discard it or disable retry.
            string entered = txtEditedPath.Text.Trim();
            if (entered.Length == 0)
            {
                MessageBox.Show(this,
                    "No edited repository available. Run Remove Package first.",
                    "Replace Repository", MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            try
            {
                string dir = Path.GetFullPath(entered);
                string main = FileFor(dir, "");
                if (!Path.GetFileName(dir).StartsWith("Edited_",
                    StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Unexpected edited directory name.");
                EnsureOutsideLive(dir);
                if (!IsInside(dir, txtWorkPath.Text.Trim()))
                    throw new InvalidOperationException(
                        "Edited directory must be inside the working directory.");
                if (!File.Exists(main) || File.Exists(main + "-wal") ||
                    File.Exists(main + "-shm"))
                    throw new InvalidOperationException(
                        "Edited database is missing or still has WAL/SHM files.");

                EnsureOfflineAndStopped();
                VerifyDatabaseSet(main, false);

                if (MessageBox.Show(this,
                    "Replace the LIVE Windows StateRepository?\n\n" +
                    "Edited directory:\n" + dir + "\n\n" +
                    "A complete Rollback_* snapshot will be saved first.\n" +
                    "Old live WAL/SHM files will NOT be reused.\n\n" +
                    "An incompatible repository can prevent Windows from starting.\n" +
                    "Continue?",
                    "Replace Repository", MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2)
                    != DialogResult.Yes) return;

                // The checksum is checked during staging, but is not stored
                // as UI/application state. txtEditedPath is the source.
                ApplyRepositorySet(dir, false, "Replace", FileSha256(main));
                btnReplace.Enabled = false; // Success only; path stays visible.
                Log("Replaced StateRepository with: " + main);
                MessageBox.Show(this,
                    "Repository replaced.\n\n" +
                    "Keep the Rollback_* snapshot until Windows has been tested.",
                    "Replace Repository", MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                // Do NOT clear txtEditedPath or disable the button after errors.
                Log("Replace Repository error: " + ex.Message);
                MessageBox.Show(this, ex.Message, "Replace Repository failed",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // This button is intentionally disabled; validation is part of Remove and Replace.
        // Click event remains connected exclusively by the WinForms Designer.
        private void btnValidate_Click(object sender, EventArgs e)
        {
        }


        private void btnRestore_Click(object sender, EventArgs e)
        {
            try
            {
                EnsureOfflineAndStopped();
                string suggested = LatestDirectory("Rollback") ?? LatestDirectory("Backup");
                string chosen;
                using (var dialog = new FolderBrowserDialog())
                {
                    dialog.Description = "Select a Backup_*, Rollback_* or PreRestore_* repository folder";
                    dialog.SelectedPath = suggested ?? txtWorkPath.Text.Trim();
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    chosen = Path.GetFullPath(dialog.SelectedPath);
                }

                string name = Path.GetFileName(chosen.TrimEnd('\\'));
                if (!(name.StartsWith("Backup_", StringComparison.OrdinalIgnoreCase) ||
                      name.StartsWith("Rollback_", StringComparison.OrdinalIgnoreCase) ||
                      name.StartsWith("PreRestore_", StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException(
                        "Select a folder named Backup_*, Rollback_* or PreRestore_*.");
                if (!IsInside(chosen, txtWorkPath.Text.Trim()))
                    throw new InvalidOperationException(
                        "Backup folder must be inside the working directory.");
                EnsureOutsideLive(chosen);

                string restoreMain = FileFor(chosen, "");
                if (!File.Exists(restoreMain))
                    throw new FileNotFoundException("Backup .srd is missing: " + restoreMain);

                VerifyDatabaseSet(restoreMain, true);
                Log("Restore snapshot validated: " + chosen);

                string setDescription = string.Join("\n", RepositorySuffixes
                    .Select(s => Path.GetFileName(FileFor(chosen, s)) +
                        (File.Exists(FileFor(chosen, s)) ? "  present" : "  absent")));
                if (MessageBox.Show(this,
                    "Restore this EXACT repository snapshot?\n\n" +
                    chosen + "\n\n" + setDescription + "\n\n" +
                    "Current live files will first be saved in PreRestore_*.\n" +
                    "Existing live WAL/SHM files will not be reused.\n\n" +
                    "Proceed?", "Restore Backup", MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                    return;

                ApplyRepositorySet(chosen, true, "Restore");
                Log("Restored repository snapshot: " + chosen);
                MessageBox.Show(this,
                    "Repository restored.\n\nThe previously active files are saved in PreRestore_*.",
                    "Restore Backup", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Log("Restore Backup error: " + ex.Message);
                MessageBox.Show(this, ex.Message, "Restore Backup failed",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    // Windows StateRepository triggers refer to functions not supplied by original SQLite.
    // Suppress journaling/cascade side effects in the OFFLINE WORKING COPY only.
    [SQLiteFunction(Name = "is_srjournal_enabled", Arguments = 0, FuncType = FunctionType.Scalar)]
    public sealed class NoSrJournalFunction : SQLiteFunction
    {
        public override object Invoke(object[] args) { return 0; }
    }

    [SQLiteFunction(Name = "is_triggers_enabled", Arguments = 0, FuncType = FunctionType.Scalar)]
    public sealed class NoSrTriggersFunction : SQLiteFunction
    {
        public override object Invoke(object[] args) { return 0; }
    }

    [SQLiteFunction(Name = "sroptions", Arguments = 0, FuncType = FunctionType.Scalar)]
    public sealed class SrOptionsFunction : SQLiteFunction
    {
        public override object Invoke(object[] args) { return 0; }
    }

    [SQLiteFunction(Name = "workid", Arguments = 0, FuncType = FunctionType.Scalar)]
    public sealed class SrWorkIdFunction : SQLiteFunction
    {
        public override object Invoke(object[] args) { return 0; }
    }

    [SQLiteFunction(Name = "now", Arguments = 0, FuncType = FunctionType.Scalar)]
    public sealed class SrNowFunction : SQLiteFunction
    {
        public override object Invoke(object[] args)
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }
}
