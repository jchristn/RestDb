namespace RestDb
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using RestDb.Classes;
    using SyslogLogging;
    using WatsonWebserver;

    partial class RestDbServer
    {
        private static readonly object RuntimeStateLock = new object();
        private static readonly string SettingsFilename = "./restdb.json";
        private static readonly string ContextFilename = "./context.json";

        private static ContextDocument _ContextDocument;
        private static DateTime _SettingsLastLoadedUtc;
        private static DateTime _ContextLastLoadedUtc;

        private static void InitializeRuntimeState()
        {
            if (!File.Exists(SettingsFilename)) new Setup();
            if (!File.Exists(ContextFilename)) new ContextDocument().ToFile(ContextFilename);

            Settings settings = Settings.FromFile(SettingsFilename) ?? new Settings();
            ContextDocument contextDocument = ContextDocument.FromFile(ContextFilename) ?? new ContextDocument();

            lock (RuntimeStateLock)
            {
                ApplySettingsInternal(settings);
                ApplyContextDocumentInternal(contextDocument);
                _SettingsLastLoadedUtc = DateTime.UtcNow;
                _ContextLastLoadedUtc = DateTime.UtcNow;
            }
        }

        private static Settings GetSettingsSnapshot()
        {
            lock (RuntimeStateLock)
            {
                return SerializationHelper.CopyObject<Settings>(_Settings) ?? new Settings();
            }
        }

        private static ContextDocument GetContextDocumentSnapshot()
        {
            lock (RuntimeStateLock)
            {
                ContextDocument ret = SerializationHelper.CopyObject<ContextDocument>(_ContextDocument) ?? new ContextDocument();
                ret.Normalize();
                EnsureContextCoverage(ret);
                return ret;
            }
        }

        private static RuntimeConfigurationResult ReloadSettingsFromDisk()
        {
            Settings settings = Settings.FromFile(SettingsFilename) ?? new Settings();
            RuntimeConfigurationResult result = ApplySettings(settings, false);
            result.Message = result.RestartRequired
                ? "Settings reloaded. Listener binding changes require a process restart."
                : "Settings reloaded.";
            return result;
        }

        private static RuntimeConfigurationResult UpdateSettings(Settings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            RuntimeConfigurationResult result = ApplySettings(settings, true);
            result.Message = result.RestartRequired
                ? "Settings updated. Listener binding changes require a process restart."
                : "Settings updated.";
            return result;
        }

        private static RuntimeConfigurationResult ReloadContextFromDisk()
        {
            ContextDocument contextDocument = ContextDocument.FromFile(ContextFilename) ?? new ContextDocument();

            lock (RuntimeStateLock)
            {
                ApplyContextDocumentInternal(contextDocument);
                _ContextLastLoadedUtc = DateTime.UtcNow;
            }

            return new RuntimeConfigurationResult
            {
                Success = true,
                Message = "Context reloaded."
            };
        }

        private static RuntimeConfigurationResult UpdateContextDocument(ContextDocument contextDocument)
        {
            if (contextDocument == null) throw new ArgumentNullException(nameof(contextDocument));

            lock (RuntimeStateLock)
            {
                ContextDocument clone = SerializationHelper.CopyObject<ContextDocument>(contextDocument) ?? new ContextDocument();
                clone.Normalize();
                EnsureContextCoverage(clone);
                clone.ToFile(ContextFilename);
                ApplyContextDocumentInternal(clone);
                _ContextLastLoadedUtc = DateTime.UtcNow;
            }

            return new RuntimeConfigurationResult
            {
                Success = true,
                Message = "Context updated."
            };
        }

        private static RuntimeConfigurationResult UpdateDatabaseContext(string databaseName, DatabaseContextPayload payload)
        {
            if (String.IsNullOrWhiteSpace(databaseName)) throw new ArgumentNullException(nameof(databaseName));
            if (payload == null) throw new ArgumentNullException(nameof(payload));

            lock (RuntimeStateLock)
            {
                ContextDocument clone = SerializationHelper.CopyObject<ContextDocument>(_ContextDocument) ?? new ContextDocument();
                clone.Normalize();

                DatabaseContextEntry entry = clone.GetDatabaseContext(databaseName, true);
                entry.Context = payload.Context;
                entry.Tables = payload.Tables != null
                    ? new Dictionary<string, string>(payload.Tables, StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                entry.Normalize();

                EnsureContextCoverage(clone);
                clone.ToFile(ContextFilename);
                ApplyContextDocumentInternal(clone);
                _ContextLastLoadedUtc = DateTime.UtcNow;
            }

            return new RuntimeConfigurationResult
            {
                Success = true,
                Message = "Database context updated."
            };
        }

        private static RuntimeConfigurationResult UpdateTableContext(string databaseName, string tableName, string contextValue)
        {
            if (String.IsNullOrWhiteSpace(databaseName)) throw new ArgumentNullException(nameof(databaseName));
            if (String.IsNullOrWhiteSpace(tableName)) throw new ArgumentNullException(nameof(tableName));

            lock (RuntimeStateLock)
            {
                ContextDocument clone = SerializationHelper.CopyObject<ContextDocument>(_ContextDocument) ?? new ContextDocument();
                clone.Normalize();

                DatabaseContextEntry entry = clone.GetDatabaseContext(databaseName, true);
                entry.Normalize();
                entry.Tables[tableName] = contextValue;

                EnsureContextCoverage(clone);
                clone.ToFile(ContextFilename);
                ApplyContextDocumentInternal(clone);
                _ContextLastLoadedUtc = DateTime.UtcNow;
            }

            return new RuntimeConfigurationResult
            {
                Success = true,
                Message = "Table context updated."
            };
        }

        private static async Task<DatabaseContextPayload> BuildDatabaseContextPayloadAsync(string databaseName)
        {
            if (String.IsNullOrWhiteSpace(databaseName)) throw new ArgumentNullException(nameof(databaseName));

            ContextDocument contextSnapshot;
            lock (RuntimeStateLock)
            {
                contextSnapshot = SerializationHelper.CopyObject<ContextDocument>(_ContextDocument) ?? new ContextDocument();
            }

            contextSnapshot.Normalize();
            DatabaseContextEntry entry = contextSnapshot.GetDatabaseContext(databaseName, false) ?? new DatabaseContextEntry();
            entry.Normalize();

            Dictionary<string, string> tables = new Dictionary<string, string>(entry.Tables, StringComparer.OrdinalIgnoreCase);
            List<string> tableNames = await _Databases.GetTableNamesAsync(databaseName).ConfigureAwait(false) ?? new List<string>();

            foreach (string tableName in tableNames)
            {
                if (!tables.ContainsKey(tableName))
                {
                    tables[tableName] = null;
                }
            }

            return new DatabaseContextPayload
            {
                Database = databaseName,
                Context = entry.Context,
                Tables = tables
            };
        }

        private static async Task<TableContextPayload> BuildTableContextPayloadAsync(string databaseName, string tableName)
        {
            if (String.IsNullOrWhiteSpace(databaseName)) throw new ArgumentNullException(nameof(databaseName));
            if (String.IsNullOrWhiteSpace(tableName)) throw new ArgumentNullException(nameof(tableName));

            Table table = await _Databases.GetTableByNameAsync(databaseName, tableName).ConfigureAwait(false);
            if (table == null) return null;

            string contextValue = null;

            lock (RuntimeStateLock)
            {
                ContextDocument contextSnapshot = SerializationHelper.CopyObject<ContextDocument>(_ContextDocument) ?? new ContextDocument();
                contextSnapshot.Normalize();

                DatabaseContextEntry entry = contextSnapshot.GetDatabaseContext(databaseName, false);
                if (entry != null)
                {
                    entry.Normalize();
                    entry.Tables.TryGetValue(table.Name, out contextValue);
                }
            }

            return new TableContextPayload
            {
                Database = databaseName,
                Table = table.Name,
                Context = contextValue
            };
        }

        private static void ApplyOperationHeaders(HttpContext http, RuntimeConfigurationResult result)
        {
            if (http == null || http.Response == null || result == null) return;

            http.Response.Headers["x-restart-required"] = result.RestartRequired ? "true" : "false";

            if (!String.IsNullOrWhiteSpace(result.Message))
            {
                http.Response.Headers["x-operation-message"] = result.Message;
            }
        }

        private static RuntimeConfigurationResult ApplySettings(Settings settings, bool persistToDisk)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            lock (RuntimeStateLock)
            {
                Settings clone = SerializationHelper.CopyObject<Settings>(settings) ?? new Settings();
                bool restartRequired = ListenerBindingChanged(_Settings?.Server, clone.Server);

                LoggingModule logging = CreateLoggingModule(clone);
                DatabaseManager databases = new DatabaseManager(clone, logging);
                AuthManager auth = new AuthManager(clone, logging);

                if (persistToDisk)
                {
                    clone.ToFile(SettingsFilename);
                }

                DatabaseManager priorDatabases = _Databases;
                _Settings = clone;
                _Logging = logging;
                _Databases = databases;
                _Auth = auth;
                _SettingsLastLoadedUtc = DateTime.UtcNow;

                EnsureContextCoverage(_ContextDocument);

                priorDatabases?.Dispose();

                return new RuntimeConfigurationResult
                {
                    Success = true,
                    RestartRequired = restartRequired
                };
            }
        }

        private static void ApplySettingsInternal(Settings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            Settings clone = SerializationHelper.CopyObject<Settings>(settings) ?? new Settings();
            LoggingModule logging = CreateLoggingModule(clone);
            DatabaseManager databases = new DatabaseManager(clone, logging);
            AuthManager auth = new AuthManager(clone, logging);

            DatabaseManager priorDatabases = _Databases;

            _Settings = clone;
            _Logging = logging;
            _Databases = databases;
            _Auth = auth;

            EnsureContextCoverage(_ContextDocument);

            priorDatabases?.Dispose();
        }

        private static void ApplyContextDocumentInternal(ContextDocument contextDocument)
        {
            ContextDocument clone = SerializationHelper.CopyObject<ContextDocument>(contextDocument) ?? new ContextDocument();
            clone.Normalize();
            EnsureContextCoverage(clone);
            _ContextDocument = clone;
        }

        private static LoggingModule CreateLoggingModule(Settings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            LoggingModule logging = new LoggingModule(
                settings.Logging.ServerIp,
                settings.Logging.ServerPort,
                settings.Logging.ConsoleLogging);

            logging.Settings.MinimumSeverity = (Severity)settings.Logging.MinimumLevel;
            return logging;
        }

        private static bool ListenerBindingChanged(ServerSettings currentSettings, ServerSettings updatedSettings)
        {
            if (currentSettings == null || updatedSettings == null) return false;

            if (!String.Equals(currentSettings.ListenerHostname, updatedSettings.ListenerHostname, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (currentSettings.ListenerPort != updatedSettings.ListenerPort)
            {
                return true;
            }

            if (currentSettings.Ssl != updatedSettings.Ssl)
            {
                return true;
            }

            return false;
        }

        private static void EnsureContextCoverage(ContextDocument contextDocument)
        {
            if (contextDocument == null) return;

            contextDocument.Normalize();

            if (_Settings?.Databases == null) return;

            foreach (Database database in _Settings.Databases)
            {
                if (String.IsNullOrWhiteSpace(database?.Name)) continue;

                DatabaseContextEntry entry = contextDocument.GetDatabaseContext(database.Name, true);
                entry.Normalize();

                if (database.Tables != null)
                {
                    foreach (Table table in database.Tables)
                    {
                        if (String.IsNullOrWhiteSpace(table?.Name)) continue;
                        if (!entry.Tables.ContainsKey(table.Name)) entry.Tables[table.Name] = null;
                    }
                }

                if (database.TableNames != null)
                {
                    foreach (string tableName in database.TableNames)
                    {
                        if (String.IsNullOrWhiteSpace(tableName)) continue;
                        if (!entry.Tables.ContainsKey(tableName)) entry.Tables[tableName] = null;
                    }
                }
            }
        }
    }
}
