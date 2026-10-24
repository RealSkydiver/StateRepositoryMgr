using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;

namespace StateRepositoryMgr
{
    internal sealed class Reference
    {
        public string Child;
        public string Column;
        public Reference(string child, string column)
        {
            Child = child;
            Column = column;
        }
    }

    internal sealed class DbSchema
    {
        public readonly Dictionary<string, string> Keys =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, HashSet<string>> Columns =
            new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, List<Reference>> References =
            new Dictionary<string, List<Reference>>(StringComparer.OrdinalIgnoreCase);
    }

    // Pure SQLite logic for removing a package family from an offline working copy.
    // Kept free of WinForms so it can be unit-tested.
    internal static class RemovalPlanner
    {
        internal static string Q(string name)
        {
            return "\"" + name.Replace("\"", "\"\"") + "\"";
        }

        internal static long ScalarLong(SQLiteConnection db, string sql, params object[] args)
        {
            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = sql;
                for (int i = 0; i < args.Length; i++)
                    cmd.Parameters.AddWithValue("@p" + i, args[i]);
                return Convert.ToInt64(cmd.ExecuteScalar());
            }
        }

        internal static string Integrity(SQLiteConnection db)
        {
            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = "PRAGMA integrity_check";
                return Convert.ToString(cmd.ExecuteScalar());
            }
        }

        internal static void RequireIntegrity(SQLiteConnection db)
        {
            string result = Integrity(db);
            if (result != "ok")
                throw new InvalidOperationException(
                    "SQLite integrity_check failed: " + result);
        }

        private static void AddReference(DbSchema schema, string parent,
            string child, string column)
        {
            if (!schema.Keys.ContainsKey(parent) || !schema.Keys.ContainsKey(child) ||
                !schema.Columns[child].Contains(column)) return;
            var list = schema.References[parent];
            if (!list.Any(r => r.Child.Equals(child, StringComparison.OrdinalIgnoreCase) &&
                               r.Column.Equals(column, StringComparison.OrdinalIgnoreCase)))
                list.Add(new Reference(child, column));
        }

        internal static DbSchema ReadSchema(SQLiteConnection db)
        {
            var schema = new DbSchema();
            var tableNames = new List<string>();
            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = "SELECT name FROM sqlite_master " +
                                  "WHERE type='table' AND name NOT LIKE 'sqlite_%'";
                using (var reader = cmd.ExecuteReader())
                    while (reader.Read()) tableNames.Add(reader.GetString(0));
            }
            foreach (string table in tableNames)
            {
                var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                string primary = null, conventional = "_" + table + "ID";
                using (var cmd = db.CreateCommand())
                {
                    cmd.CommandText = "PRAGMA table_info(" + Q(table) + ")";
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string column = reader.GetString(1);
                            columns.Add(column);
                            if (column.Equals(conventional, StringComparison.OrdinalIgnoreCase))
                                primary = column;
                            else if (primary == null && Convert.ToInt32(reader[5]) > 0)
                                primary = column;
                        }
                    }
                }
                schema.Columns.Add(table, columns);
                if (primary != null)
                {
                    schema.Keys.Add(table, primary);
                    schema.References.Add(table, new List<Reference>());
                }
            }
            foreach (string parent in schema.Keys.Keys)
                foreach (string child in schema.Keys.Keys)
                    AddReference(schema, parent, child, parent);

            // StateRepository references whose column names differ from their parent table.
            foreach (string child in new[] { "Dependency", "DependencyGraph", "WowDependencyGraph" })
                AddReference(schema, "Package", child, "DependentPackage");
            foreach (string child in new[] { "DependencyGraph", "WowDependencyGraph" })
                AddReference(schema, "Package", child, "SupplierPackage");
            AddReference(schema, "Bundle", "OptionalBundle", "MainBundle");

            foreach (string child in new[] {
                "AppExecutionAlias", "AppExtension", "AppExtensionHost",
                "AppService", "AppUriHandler", "AppUriHandlerGroup",
                "ApplicationBackgroundTask", "FileTypeAssociation", "Protocol" })
                AddReference(schema, "ApplicationExtension", child, "Extension");

            foreach (string required in new[] {
                "PackageFamily", "Package", "PackageIdentity", "Application",
                "ApplicationExtension", "PackageExtension", "ApplicationIdentity" })
                if (!schema.Keys.ContainsKey(required))
                    throw new InvalidOperationException(
                        "Unexpected StateRepository schema: missing " + required);
            return schema;
        }

        private static HashSet<long> GetIds(SQLiteConnection db, string table,
            string keyColumn, string whereColumn, IEnumerable<long> values)
        {
            var found = new HashSet<long>();
            var ids = values.ToList();
            for (int offset = 0; offset < ids.Count; offset += 400)
            {
                var batch = ids.Skip(offset).Take(400).ToList();
                string placeholders = string.Join(",", Enumerable.Range(0, batch.Count)
                    .Select(i => "@p" + i));
                using (var cmd = db.CreateCommand())
                {
                    cmd.CommandText = "SELECT " + Q(keyColumn) + " FROM " + Q(table) +
                                      " WHERE " + Q(whereColumn) + " IN (" + placeholders + ")";
                    for (int i = 0; i < batch.Count; i++)
                        cmd.Parameters.AddWithValue("@p" + i, batch[i]);
                    using (var reader = cmd.ExecuteReader())
                        while (reader.Read())
                            if (!reader.IsDBNull(0)) found.Add(Convert.ToInt64(reader[0]));
                }
            }
            return found;
        }

        private static HashSet<long> SetFor(
            Dictionary<string, HashSet<long>> selected, string table)
        {
            HashSet<long> set;
            if (!selected.TryGetValue(table, out set))
            {
                set = new HashSet<long>();
                selected[table] = set;
            }
            return set;
        }

        private static void ExpandSelection(SQLiteConnection db, DbSchema schema,
            Dictionary<string, HashSet<long>> selected)
        {
            bool changed;
            do
            {
                changed = false;
                foreach (string parent in schema.Keys.Keys)
                {
                    HashSet<long> roots;
                    if (!selected.TryGetValue(parent, out roots) || roots.Count == 0)
                        continue;
                    foreach (Reference reference in schema.References[parent])
                    {
                        var hits = GetIds(db, reference.Child,
                            schema.Keys[reference.Child], reference.Column, roots);
                        var dest = SetFor(selected, reference.Child);
                        int count = dest.Count;
                        dest.UnionWith(hits);
                        if (dest.Count != count) changed = true;
                    }
                }
            } while (changed);
        }

        private static HashSet<long> GetMatchingApplicationIdentityIds(
            SQLiteConnection db, HashSet<long> applicationIds)
        {
            var result = new HashSet<long>();
            if (applicationIds.Count == 0) return result;
            var modelIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (long id in applicationIds)
            {
                using (var cmd = db.CreateCommand())
                {
                    cmd.CommandText = "SELECT ApplicationUserModelId FROM Application " +
                                      "WHERE _ApplicationID=@id";
                    cmd.Parameters.AddWithValue("@id", id);
                    var obj = cmd.ExecuteScalar();
                    if (obj != null && obj != DBNull.Value)
                        modelIds.Add(Convert.ToString(obj));
                }
            }
            foreach (string modelId in modelIds)
            {
                using (var cmd = db.CreateCommand())
                {
                    cmd.CommandText = "SELECT _ApplicationID FROM Application " +
                                      "WHERE ApplicationUserModelId=@model";
                    cmd.Parameters.AddWithValue("@model", modelId);
                    using (var reader = cmd.ExecuteReader())
                        while (reader.Read())
                            if (!applicationIds.Contains(Convert.ToInt64(reader[0])))
                                throw new InvalidOperationException(
                                    "Application identity is also used by another package: " + modelId);
                }
                using (var cmd = db.CreateCommand())
                {
                    cmd.CommandText = "SELECT _ApplicationIdentityID FROM ApplicationIdentity " +
                                      "WHERE ApplicationUserModelId=@model";
                    cmd.Parameters.AddWithValue("@model", modelId);
                    using (var reader = cmd.ExecuteReader())
                        while (reader.Read()) result.Add(Convert.ToInt64(reader[0]));
                }
            }
            return result;
        }

        private static void AddExclusiveActivations(SQLiteConnection db,
            Dictionary<string, HashSet<long>> selected)
        {
            var candidates = new HashSet<long>();
            foreach (string table in new[] { "Application", "ApplicationExtension", "PackageExtension" })
            {
                HashSet<long> ids;
                if (!selected.TryGetValue(table, out ids) || ids.Count == 0) continue;
                string key = "_" + table + "ID";
                if (table == "Application" || table == "ApplicationExtension" || table == "PackageExtension")
                    foreach (long id in ids)
                    {
                        using (var cmd = db.CreateCommand())
                        {
                            cmd.CommandText = "SELECT Activation FROM " + Q(table) +
                                              " WHERE " + Q(key) + "=@id";
                            cmd.Parameters.AddWithValue("@id", id);
                            object value = cmd.ExecuteScalar();
                            if (value != null && value != DBNull.Value &&
                                Convert.ToInt64(value) > 0)
                                candidates.Add(Convert.ToInt64(value));
                        }
                    }
            }
            foreach (long activation in candidates)
            {
                bool shared = false;
                foreach (string table in new[] { "Application", "ApplicationExtension", "PackageExtension" })
                {
                    using (var cmd = db.CreateCommand())
                    {
                        cmd.CommandText = "SELECT " + Q("_" + table + "ID") +
                                          " FROM " + Q(table) + " WHERE Activation=@id";
                        cmd.Parameters.AddWithValue("@id", activation);
                        HashSet<long> allowed = SetFor(selected, table);
                        using (var reader = cmd.ExecuteReader())
                            while (reader.Read())
                                if (!allowed.Contains(Convert.ToInt64(reader[0]))) shared = true;
                    }
                }
                if (!shared) SetFor(selected, "Activation").Add(activation);
            }
        }

        internal static Dictionary<string, HashSet<long>> PlanRemoval(
            SQLiteConnection db, DbSchema schema, long familyId, string familyName)
        {
            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = "SELECT PackageFamilyName FROM PackageFamily " +
                                  "WHERE _PackageFamilyID=@id";
                cmd.Parameters.AddWithValue("@id", familyId);
                object actual = cmd.ExecuteScalar();
                if (actual == null || actual == DBNull.Value ||
                    !string.Equals(Convert.ToString(actual), familyName, StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        "Package family has changed. Search again.");
            }

            var selected = new Dictionary<string, HashSet<long>>(StringComparer.OrdinalIgnoreCase);
            SetFor(selected, "PackageFamily").Add(familyId);
            var packageIds = GetIds(db, "Package", "_PackageID",
                "PackageFamily", new[] { familyId });
            SetFor(selected, "Package").UnionWith(packageIds);
            ExpandSelection(db, schema, selected);

            var identities = GetMatchingApplicationIdentityIds(db,
                SetFor(selected, "Application"));
            SetFor(selected, "ApplicationIdentity").UnionWith(identities);
            ExpandSelection(db, schema, selected);

            AddExclusiveActivations(db, selected);
            ExpandSelection(db, schema, selected);

            if (!SetFor(selected, "PackageFamily").SetEquals(new[] { familyId }) ||
                !SetFor(selected, "Package").SetEquals(packageIds))
                throw new InvalidOperationException(
                    "Removal graph reaches another package family. Operation cancelled.");

            return selected;
        }

        internal static void DeleteSelection(SQLiteConnection db, DbSchema schema,
            Dictionary<string, HashSet<long>> selected)
        {
            var order = selected.Keys.Where(t => t != "Package" && t != "PackageFamily")
                .OrderBy(t => t).Concat(new[] { "Package", "PackageFamily" });
            foreach (string table in order)
            {
                HashSet<long> ids;
                if (!selected.TryGetValue(table, out ids) || ids.Count == 0) continue;
                var values = ids.ToList();
                for (int offset = 0; offset < values.Count; offset += 400)
                {
                    var batch = values.Skip(offset).Take(400).ToList();
                    string placeholders = string.Join(",", Enumerable.Range(0, batch.Count)
                        .Select(i => "@p" + i));
                    using (var cmd = db.CreateCommand())
                    {
                        cmd.CommandText = "DELETE FROM " + Q(table) + " WHERE " +
                                          Q(schema.Keys[table]) + " IN (" + placeholders + ")";
                        for (int i = 0; i < batch.Count; i++)
                            cmd.Parameters.AddWithValue("@p" + i, batch[i]);
                        int affected = cmd.ExecuteNonQuery();
                        if (affected != batch.Count)
                            throw new InvalidOperationException("Unexpected DELETE count in " + table);
                    }
                }
            }
        }

        internal static void CheckNoRemainingReferences(SQLiteConnection db, DbSchema schema,
            Dictionary<string, HashSet<long>> selected)
        {
            foreach (string parent in schema.Keys.Keys)
            {
                HashSet<long> removed;
                if (!selected.TryGetValue(parent, out removed) || removed.Count == 0) continue;
                foreach (Reference r in schema.References[parent])
                {
                    var left = GetIds(db, r.Child, schema.Keys[r.Child], r.Column, removed);
                    if (left.Count != 0)
                        throw new InvalidOperationException(
                            "Dangling reference: " + r.Child + "." + r.Column +
                            " -> " + parent);
                }
            }
        }
    }
}
