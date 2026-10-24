using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StateRepositoryMgr.Tests
{
    // The fixture is a reduced, synthetic StateRepository schema that follows the naming
    // conventions RemovalPlanner relies on (_<Table>ID keys, child columns named after the
    // parent table). It is NOT a copy of the real Windows schema.
    [TestClass]
    public class RemovalPlannerTests
    {
        private SQLiteConnection db;

        [TestInitialize]
        public void Setup()
        {
            db = new SQLiteConnection("Data Source=:memory:;Version=3;");
            db.Open();
            Exec(@"
                CREATE TABLE PackageFamily(_PackageFamilyID INTEGER PRIMARY KEY, PackageFamilyName TEXT, Name TEXT);
                CREATE TABLE PackageIdentity(_PackageIdentityID INTEGER PRIMARY KEY, PackageFullName TEXT);
                CREATE TABLE Package(_PackageID INTEGER PRIMARY KEY, PackageFamily INTEGER, PackageFullName TEXT);
                CREATE TABLE ApplicationIdentity(_ApplicationIdentityID INTEGER PRIMARY KEY, ApplicationUserModelId TEXT);
                CREATE TABLE Activation(_ActivationID INTEGER PRIMARY KEY);
                CREATE TABLE Application(_ApplicationID INTEGER PRIMARY KEY, Package INTEGER, ApplicationUserModelId TEXT, Activation INTEGER);
                CREATE TABLE ApplicationExtension(_ApplicationExtensionID INTEGER PRIMARY KEY, Application INTEGER, Activation INTEGER);
                CREATE TABLE PackageExtension(_PackageExtensionID INTEGER PRIMARY KEY, Package INTEGER, Activation INTEGER);
                CREATE TABLE AppService(_AppServiceID INTEGER PRIMARY KEY, Extension INTEGER);
                CREATE TABLE Dependency(_DependencyID INTEGER PRIMARY KEY, DependentPackage INTEGER);
                CREATE TABLE DependencyGraph(_DependencyGraphID INTEGER PRIMARY KEY, DependentPackage INTEGER, SupplierPackage INTEGER);

                -- Family 1 (to be removed)
                INSERT INTO PackageFamily VALUES (1, 'Foo_abc', 'Foo');
                INSERT INTO Package VALUES (10, 1, 'Foo_1.0_x64__abc');
                INSERT INTO ApplicationIdentity VALUES (200, 'Foo_abc!App');
                INSERT INTO Activation VALUES (1000), (1001), (3000);
                INSERT INTO Application VALUES (100, 10, 'Foo_abc!App', 1000);
                INSERT INTO ApplicationExtension VALUES (300, 100, 1001);
                INSERT INTO AppService VALUES (400, 300);
                INSERT INTO PackageExtension VALUES (500, 10, 3000);
                INSERT INTO Dependency VALUES (600, 10);
                INSERT INTO DependencyGraph VALUES (700, 10, 20);

                -- Family 2 (must survive); shares Activation 3000 with family 1
                INSERT INTO PackageFamily VALUES (2, 'Bar_xyz', 'Bar');
                INSERT INTO Package VALUES (20, 2, 'Bar_1.0_x64__xyz');
                INSERT INTO ApplicationIdentity VALUES (210, 'Bar_xyz!App');
                INSERT INTO Activation VALUES (2000);
                INSERT INTO Application VALUES (110, 20, 'Bar_xyz!App', 2000);
                INSERT INTO PackageExtension VALUES (510, 20, 3000);
                INSERT INTO Dependency VALUES (610, 20);");
        }

        [TestCleanup]
        public void Teardown()
        {
            db.Dispose();
        }

        private void Exec(string sql)
        {
            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
            }
        }

        private long Count(string table)
        {
            return RemovalPlanner.ScalarLong(db, "SELECT COUNT(*) FROM " + table);
        }

        private Dictionary<string, HashSet<long>> Plan(long familyId = 1, string name = "Foo_abc")
        {
            return RemovalPlanner.PlanRemoval(db, RemovalPlanner.ReadSchema(db), familyId, name);
        }

        private static void AssertIds(Dictionary<string, HashSet<long>> plan, string table,
            params long[] expected)
        {
            HashSet<long> actual;
            if (!plan.TryGetValue(table, out actual)) actual = new HashSet<long>();
            CollectionAssert.AreEquivalent(expected, actual.ToList(), table);
        }

        // ---- ReadSchema ----------------------------------------------------------------

        [TestMethod]
        public void ReadSchema_FindsConventionalKeys()
        {
            var schema = RemovalPlanner.ReadSchema(db);
            Assert.AreEqual("_PackageID", schema.Keys["Package"]);
            Assert.AreEqual("_PackageFamilyID", schema.Keys["PackageFamily"]);
        }

        [TestMethod]
        public void ReadSchema_FallsBackToPrimaryKeyColumn()
        {
            Exec("CREATE TABLE Odd(Id INTEGER PRIMARY KEY, Name TEXT)");
            Assert.AreEqual("Id", RemovalPlanner.ReadSchema(db).Keys["Odd"]);
        }

        [TestMethod]
        public void ReadSchema_DetectsReferencesByColumnName()
        {
            var refs = RemovalPlanner.ReadSchema(db).References["Package"];
            Assert.IsTrue(refs.Any(r => r.Child == "Application" && r.Column == "Package"));
            Assert.IsTrue(refs.Any(r => r.Child == "PackageExtension" && r.Column == "Package"));
        }

        [TestMethod]
        public void ReadSchema_AddsKnownIrregularReferences()
        {
            var schema = RemovalPlanner.ReadSchema(db);
            var pkg = schema.References["Package"];
            Assert.IsTrue(pkg.Any(r => r.Child == "Dependency" && r.Column == "DependentPackage"));
            Assert.IsTrue(pkg.Any(r => r.Child == "DependencyGraph" && r.Column == "DependentPackage"));
            Assert.IsTrue(pkg.Any(r => r.Child == "DependencyGraph" && r.Column == "SupplierPackage"));
            Assert.IsTrue(schema.References["ApplicationExtension"]
                .Any(r => r.Child == "AppService" && r.Column == "Extension"));
        }

        [TestMethod]
        public void ReadSchema_RejectsUnexpectedSchema()
        {
            Exec("DROP TABLE ApplicationIdentity");
            var ex = Assert.ThrowsException<InvalidOperationException>(
                () => RemovalPlanner.ReadSchema(db));
            StringAssert.Contains(ex.Message, "ApplicationIdentity");
        }

        // ---- PlanRemoval ---------------------------------------------------------------

        [TestMethod]
        public void PlanRemoval_SelectsExactlyTheFamilyGraph()
        {
            var plan = Plan();
            AssertIds(plan, "PackageFamily", 1);
            AssertIds(plan, "Package", 10);
            AssertIds(plan, "Application", 100);
            AssertIds(plan, "ApplicationExtension", 300);
            AssertIds(plan, "AppService", 400);
            AssertIds(plan, "PackageExtension", 500);
            AssertIds(plan, "Dependency", 600);
            AssertIds(plan, "DependencyGraph", 700);
            AssertIds(plan, "ApplicationIdentity", 200);
            AssertIds(plan, "Activation", 1000, 1001);
            Assert.AreEqual(11, plan.Values.Sum(s => s.Count));
        }

        [TestMethod]
        public void PlanRemoval_KeepsActivationSharedWithAnotherFamily()
        {
            Assert.IsFalse(Plan()["Activation"].Contains(3000));
        }

        [TestMethod]
        public void PlanRemoval_RejectsChangedFamilyName()
        {
            var ex = Assert.ThrowsException<InvalidOperationException>(
                () => Plan(1, "Renamed_abc"));
            StringAssert.Contains(ex.Message, "Package family has changed");
        }

        [TestMethod]
        public void PlanRemoval_RejectsUnknownFamilyId()
        {
            Assert.ThrowsException<InvalidOperationException>(() => Plan(99, "Foo_abc"));
        }

        [TestMethod]
        public void PlanRemoval_RejectsApplicationIdentitySharedWithAnotherPackage()
        {
            Exec("INSERT INTO Application VALUES (120, 20, 'Foo_abc!App', NULL)");
            var ex = Assert.ThrowsException<InvalidOperationException>(() => Plan());
            StringAssert.Contains(ex.Message, "also used by another package");
        }

        [TestMethod]
        public void PlanRemoval_RejectsGraphThatReachesAnotherFamily()
        {
            // Package 20 hangs off ApplicationIdentity 200, which belongs to family 1.
            Exec("ALTER TABLE Package ADD COLUMN ApplicationIdentity INTEGER");
            Exec("UPDATE Package SET ApplicationIdentity = 200 WHERE _PackageID = 20");
            var ex = Assert.ThrowsException<InvalidOperationException>(() => Plan());
            StringAssert.Contains(ex.Message, "reaches another package family");
        }

        [TestMethod]
        public void PlanRemoval_HandlesMoreThanOneParameterBatch()
        {
            Exec("INSERT INTO PackageFamily VALUES (3, 'Big_abc', 'Big')");
            using (var tx = db.BeginTransaction())
            {
                for (int i = 0; i < 1000; i++)
                    Exec("INSERT INTO Package VALUES (" + (5000 + i) + ", 3, 'Big_" + i + "')");
                tx.Commit();
            }
            var plan = Plan(3, "Big_abc");
            Assert.AreEqual(1000, plan["Package"].Count);
        }

        // ---- DeleteSelection / CheckNoRemainingReferences ------------------------------

        [TestMethod]
        public void DeleteSelection_RemovesOnlyThePlannedRows()
        {
            var schema = RemovalPlanner.ReadSchema(db);
            var plan = RemovalPlanner.PlanRemoval(db, schema, 1, "Foo_abc");
            RemovalPlanner.DeleteSelection(db, schema, plan);
            RemovalPlanner.CheckNoRemainingReferences(db, schema, plan);

            Assert.AreEqual(0, RemovalPlanner.ScalarLong(db,
                "SELECT COUNT(*) FROM PackageFamily WHERE _PackageFamilyID = 1"));
            Assert.AreEqual(1, Count("PackageFamily"));
            Assert.AreEqual(1, Count("Package"));
            Assert.AreEqual(1, Count("Application"));
            Assert.AreEqual(1, Count("Dependency"));
            Assert.AreEqual(0, Count("DependencyGraph"));
            Assert.AreEqual(1, Count("ApplicationIdentity"));
            Assert.AreEqual(2, Count("Activation"), "2000 and the shared 3000 must remain");
            Assert.AreEqual("ok", RemovalPlanner.Integrity(db));
        }

        [TestMethod]
        public void DeleteSelection_RejectsRowCountMismatch()
        {
            var schema = RemovalPlanner.ReadSchema(db);
            var plan = RemovalPlanner.PlanRemoval(db, schema, 1, "Foo_abc");
            plan["Dependency"].Add(9999);
            var ex = Assert.ThrowsException<InvalidOperationException>(
                () => RemovalPlanner.DeleteSelection(db, schema, plan));
            StringAssert.Contains(ex.Message, "Unexpected DELETE count");
        }

        [TestMethod]
        public void CheckNoRemainingReferences_DetectsDanglingChild()
        {
            var schema = RemovalPlanner.ReadSchema(db);
            var plan = RemovalPlanner.PlanRemoval(db, schema, 1, "Foo_abc");
            RemovalPlanner.DeleteSelection(db, schema, plan);
            Exec("INSERT INTO Dependency VALUES (601, 10)");
            var ex = Assert.ThrowsException<InvalidOperationException>(
                () => RemovalPlanner.CheckNoRemainingReferences(db, schema, plan));
            StringAssert.Contains(ex.Message, "Dependency.DependentPackage");
        }

        [TestMethod]
        public void Q_EscapesEmbeddedQuotes()
        {
            Assert.AreEqual("\"a\"\"b\"", RemovalPlanner.Q("a\"b"));
        }
    }
}
