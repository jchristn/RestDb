namespace RestDb
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using RestDb.Classes;

    /// <summary>
    /// File-backed database and table context metadata.
    /// </summary>
    public class ContextDocument
    {
        /// <summary>
        /// Per-database context values.
        /// </summary>
        public Dictionary<string, DatabaseContextEntry> Databases { get; set; } = new Dictionary<string, DatabaseContextEntry>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Instantiate from file.
        /// </summary>
        /// <param name="filename">Filename.</param>
        /// <returns>Context document.</returns>
        public static ContextDocument FromFile(string filename)
        {
            if (String.IsNullOrEmpty(filename)) throw new ArgumentNullException(nameof(filename));
            if (!File.Exists(filename)) return new ContextDocument();

            ContextDocument ret = SerializationHelper.DeserializeJson<ContextDocument>(File.ReadAllBytes(filename));
            if (ret == null) ret = new ContextDocument();
            ret.Normalize();
            return ret;
        }

        /// <summary>
        /// Write to file.
        /// </summary>
        /// <param name="filename">Filename.</param>
        public void ToFile(string filename)
        {
            if (String.IsNullOrEmpty(filename)) throw new ArgumentNullException(nameof(filename));
            Normalize();
            File.WriteAllBytes(filename, Encoding.UTF8.GetBytes(SerializationHelper.SerializeJson(this, true)));
        }

        /// <summary>
        /// Normalize dictionary instances and case-insensitive lookups.
        /// </summary>
        public void Normalize()
        {
            if (Databases == null)
            {
                Databases = new Dictionary<string, DatabaseContextEntry>(StringComparer.OrdinalIgnoreCase);
                return;
            }

            Databases = Databases
                .Where(kvp => !String.IsNullOrWhiteSpace(kvp.Key))
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp =>
                    {
                        DatabaseContextEntry entry = kvp.Value ?? new DatabaseContextEntry();
                        entry.Normalize();
                        return entry;
                    },
                    StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Retrieve a database context entry.
        /// </summary>
        /// <param name="databaseName">Database name.</param>
        /// <param name="createIfMissing">Whether or not to create the entry if absent.</param>
        /// <returns>Database context entry.</returns>
        public DatabaseContextEntry GetDatabaseContext(string databaseName, bool createIfMissing)
        {
            if (String.IsNullOrEmpty(databaseName)) throw new ArgumentNullException(nameof(databaseName));

            Normalize();

            if (Databases.TryGetValue(databaseName, out DatabaseContextEntry ret)) return ret;
            if (!createIfMissing) return null;

            ret = new DatabaseContextEntry();
            Databases[databaseName] = ret;
            return ret;
        }
    }

    /// <summary>
    /// Context values for a specific database and its tables.
    /// </summary>
    public class DatabaseContextEntry
    {
        /// <summary>
        /// Database-level context text.
        /// </summary>
        public string Context { get; set; } = null;

        /// <summary>
        /// Per-table context values.
        /// </summary>
        public Dictionary<string, string> Tables { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Normalize table dictionary.
        /// </summary>
        public void Normalize()
        {
            if (Tables == null)
            {
                Tables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                return;
            }

            Tables = Tables
                .Where(kvp => !String.IsNullOrWhiteSpace(kvp.Key))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Database context payload.
    /// </summary>
    public class DatabaseContextPayload
    {
        /// <summary>
        /// Database name.
        /// </summary>
        public string Database { get; set; } = null;

        /// <summary>
        /// Database-level context text.
        /// </summary>
        public string Context { get; set; } = null;

        /// <summary>
        /// Per-table context text.
        /// </summary>
        public Dictionary<string, string> Tables { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Table context payload.
    /// </summary>
    public class TableContextPayload
    {
        /// <summary>
        /// Database name.
        /// </summary>
        public string Database { get; set; } = null;

        /// <summary>
        /// Table name.
        /// </summary>
        public string Table { get; set; } = null;

        /// <summary>
        /// Table-level context text.
        /// </summary>
        public string Context { get; set; } = null;
    }

    /// <summary>
    /// Generic context update request.
    /// </summary>
    public class ContextValueUpdateRequest
    {
        /// <summary>
        /// Context value.
        /// </summary>
        public string Context { get; set; } = null;
    }
}
