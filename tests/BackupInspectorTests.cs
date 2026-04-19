using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SsmsRestoreDrop.Services;

namespace SsmsRestoreDrop.Tests
{
    /// <summary>
    /// Unit tests for BackupInspector.
    ///
    /// Integration tests (ReadHeader / ReadFileList) are skipped if no SQL Server
    /// connection is configured via the SSMS_TEST_CONN environment variable, so
    /// the test suite can run cleanly in CI without a SQL instance.
    ///
    /// To run integration tests locally:
    ///   set SSMS_TEST_CONN=Server=.\SQLEXPRESS;Integrated Security=true
    ///   set SSMS_TEST_BAK=C:\path\to\sample.bak
    /// </summary>
    [TestClass]
    public class BackupInspectorTests
    {
        private static readonly string? TestConn = Environment.GetEnvironmentVariable("SSMS_TEST_CONN");
        private static readonly string? TestBak  = Environment.GetEnvironmentVariable("SSMS_TEST_BAK");

        // ── BuildDefaultRelocation — pure logic, no SQL needed ───────────────
        [TestMethod]
        public void BuildDefaultRelocation_MapsFilesToCorrectPaths()
        {
            var inspector = new BackupInspector("Server=.;Integrated Security=true");

            var files = new[]
            {
                new BackupFileInfo { LogicalName = "MyDb",     PhysicalName = @"C:\old\MyDb.mdf",     FileType = "D" },
                new BackupFileInfo { LogicalName = "MyDb_log", PhysicalName = @"C:\old\MyDb_log.ldf", FileType = "L" }
            };

            var result = inspector.BuildDefaultRelocation(files, @"D:\Data", @"E:\Log");

            Assert.AreEqual(2, result.Count);

            var data = result.Single(r => r.FileType == "D");
            Assert.AreEqual("MyDb",             data.LogicalName);
            Assert.AreEqual(@"D:\Data\MyDb.mdf", data.PhysicalPath);

            var log = result.Single(r => r.FileType == "L");
            Assert.AreEqual("MyDb_log",             log.LogicalName);
            Assert.AreEqual(@"E:\Log\MyDb_log.ldf",  log.PhysicalPath);
        }

        [TestMethod]
        public void BuildDefaultRelocation_HandlesUncPaths()
        {
            var inspector = new BackupInspector("Server=.;Integrated Security=true");

            var files = new[]
            {
                new BackupFileInfo
                {
                    LogicalName  = "DB1",
                    PhysicalName = @"\\server\share\DB1.mdf",
                    FileType     = "D"
                }
            };

            var result = inspector.BuildDefaultRelocation(files, @"\\targetserver\data", @"\\targetserver\log");
            Assert.AreEqual(@"\\targetserver\data\DB1.mdf", result[0].PhysicalPath);
        }

        // ── RestoreRunner.GenerateScript — pure logic, no SQL needed ─────────
        [TestMethod]
        public void GenerateScript_ContainsExpectedClauses()
        {
            var opts = new RestoreOptions
            {
                ConnectionString  = "Server=.;Integrated Security=true",
                TargetDatabase    = "TestDB",
                BackupSetPosition = 1,
                Replace           = true,
                NoRecovery        = false
            };
            opts.BackupFiles.Add(@"C:\backups\TestDB.bak");
            opts.RelocateFiles.Add(new RelocateFileEntry
            {
                LogicalName  = "TestDB",
                PhysicalPath = @"D:\Data\TestDB.mdf",
                FileType     = "D"
            });

            var runner = new RestoreRunner(opts);
            var script = runner.GenerateScript();

            StringAssert.Contains(script, "RESTORE DATABASE [TestDB]");
            StringAssert.Contains(script, @"DISK = N'C:\backups\TestDB.bak'");
            StringAssert.Contains(script, "FILE = 1");
            StringAssert.Contains(script, "REPLACE");
            StringAssert.Contains(script, "RECOVERY");                // not NORECOVERY
            StringAssert.Contains(script, @"MOVE N'TestDB' TO N'D:\Data\TestDB.mdf'");
        }

        [TestMethod]
        public void GenerateScript_NoRecovery_ContainsNorecovery()
        {
            var opts = new RestoreOptions
            {
                ConnectionString = "Server=.;Integrated Security=true",
                TargetDatabase   = "LogDB",
                NoRecovery       = true
            };
            opts.BackupFiles.Add(@"C:\backups\LogDB.trn");

            var script = new RestoreRunner(opts).GenerateScript();
            StringAssert.Contains(script, "NORECOVERY");
        }

        [TestMethod]
        public void GenerateScript_EscapesSingleQuotesInPaths()
        {
            var opts = new RestoreOptions
            {
                ConnectionString = "Server=.;Integrated Security=true",
                TargetDatabase   = "DB"
            };
            opts.BackupFiles.Add(@"C:\back'ups\DB.bak");

            var script = new RestoreRunner(opts).GenerateScript();
            StringAssert.Contains(script, @"C:\back''ups\DB.bak");
        }

        // ── Integration tests ─────────────────────────────────────────────────
        [TestMethod]
        [TestCategory("Integration")]
        public void ReadHeader_ReturnsAtLeastOneSet()
        {
            if (TestConn == null || TestBak == null)
            {
                Assert.Inconclusive("Set SSMS_TEST_CONN and SSMS_TEST_BAK to run integration tests.");
                return;
            }

            var inspector = new BackupInspector(TestConn);
            var headers   = inspector.ReadHeader(new[] { TestBak });

            Assert.IsTrue(headers.Count > 0, "Expected at least one backup set in header.");
            Assert.IsFalse(string.IsNullOrEmpty(headers[0].DatabaseName));
        }

        [TestMethod]
        [TestCategory("Integration")]
        public void ReadFileList_ReturnsAtLeastOneFile()
        {
            if (TestConn == null || TestBak == null)
            {
                Assert.Inconclusive("Set SSMS_TEST_CONN and SSMS_TEST_BAK to run integration tests.");
                return;
            }

            var inspector = new BackupInspector(TestConn);
            var files     = inspector.ReadFileList(new[] { TestBak }, position: 1);

            Assert.IsTrue(files.Count > 0);
            Assert.IsFalse(string.IsNullOrEmpty(files[0].LogicalName));
        }

        [TestMethod]
        [TestCategory("Integration")]
        public void GetDefaultPaths_ReturnsNonEmptyPaths()
        {
            if (TestConn == null)
            {
                Assert.Inconclusive("Set SSMS_TEST_CONN to run integration tests.");
                return;
            }

            var inspector         = new BackupInspector(TestConn);
            var (dataPath, logPath) = inspector.GetDefaultPaths();

            Assert.IsFalse(string.IsNullOrEmpty(dataPath), "Data path should not be empty");
            Assert.IsFalse(string.IsNullOrEmpty(logPath),  "Log path should not be empty");
        }
    }
}
