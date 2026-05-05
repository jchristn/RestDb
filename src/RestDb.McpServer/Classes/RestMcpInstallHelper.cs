namespace RestDb.McpServer.Classes
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using System.Threading.Tasks;

    internal static class RestMcpInstallHelper
    {
        private const string ServerKey = "restdb";
        private const string CodexManagedBlockStart = "# restdb:mcp:begin";
        private const string CodexManagedBlockEnd = "# restdb:mcp:end";

        private static JsonSerializerOptions JsonOptions { get; } = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        internal static async Task<int> RunInstallAsync(RestMcpServerSettings settings)
        {
            string streamableHttpUrl = BuildStreamableHttpUrl(settings);
            List<InstallTarget> targets = BuildTargets(streamableHttpUrl);
            bool failed = false;

            Console.WriteLine("RestDb MCP installer");
            Console.WriteLine("MCP HTTP endpoint: " + streamableHttpUrl);
            Console.WriteLine();

            if (!String.IsNullOrWhiteSpace(settings.ApiKey) || !String.IsNullOrWhiteSpace(settings.BearerToken))
            {
                Console.WriteLine("Note: install does not persist downstream RestDb credentials.");
                Console.WriteLine("Configure RESTDB_MCP_API_KEY / RESTDB_MCP_BEARER_TOKEN on the RestDb.McpServer process itself.");
                Console.WriteLine();
            }

            if (settings.DryRun)
            {
                Console.WriteLine("[DRY RUN] No files will be modified.");
                Console.WriteLine();
            }

            foreach (InstallTarget target in targets)
            {
                Console.WriteLine(target.ClientName);
                Console.WriteLine(new string('-', target.ClientName.Length));

                bool shouldApply = settings.DryRun
                    || settings.Yes
                    || Confirm("Configure " + target.ClientName + " at " + target.FilePath + "?", true);

                if (!shouldApply)
                {
                    Console.WriteLine("Skipped.");
                    Console.WriteLine();
                    continue;
                }

                try
                {
                    ApplyResult result = await target.ApplyAsync(settings.DryRun).ConfigureAwait(false);
                    Console.WriteLine(result.Message);
                    Console.WriteLine(result.FilePath);

                    if (!String.IsNullOrWhiteSpace(result.Preview))
                    {
                        Console.WriteLine();
                        Console.WriteLine(result.Preview);
                    }
                }
                catch (Exception ex)
                {
                    failed = true;
                    Console.WriteLine("Failed to configure " + target.ClientName + ": " + ex.Message);
                    Console.WriteLine(target.FilePath);
                }

                Console.WriteLine();
            }

            Console.WriteLine("Next steps");
            Console.WriteLine("----------");
            Console.WriteLine("1. Restart Claude Code, Codex, Gemini CLI, and Cursor if they were already running.");
            Console.WriteLine("2. Confirm the RestDb MCP server is reachable at " + streamableHttpUrl + ".");
            Console.WriteLine("3. Re-run `install --dry-run` any time you want to preview the generated client config.");
            return failed ? 1 : 0;
        }

        internal static string BuildStreamableHttpUrl(RestMcpServerSettings settings)
        {
            string host = NormalizeInstallHostname(settings.HttpHostname);
            return "http://" + host + ":" + settings.HttpPort + "/mcp";
        }

        private static List<InstallTarget> BuildTargets(string streamableHttpUrl)
        {
            JsonObject claudeConfig = BuildClaudeConfig(streamableHttpUrl);
            JsonObject geminiConfig = BuildGeminiConfig(streamableHttpUrl);
            JsonObject cursorConfig = BuildCursorConfig(streamableHttpUrl);
            string codexToml = BuildCodexToml(streamableHttpUrl);

            return new List<InstallTarget>
            {
                new InstallTarget(
                    "Claude Code",
                    GetClaudeConfigPath(),
                    dryRun => ApplyJsonTargetAsync(
                        "Claude Code",
                        GetClaudeConfigPath(),
                        claudeConfig,
                        dryRun)),
                new InstallTarget(
                    "Codex",
                    GetCodexConfigPath(),
                    dryRun => ApplyCodexTargetAsync(
                        GetCodexConfigPath(),
                        codexToml,
                        dryRun)),
                new InstallTarget(
                    "Gemini CLI",
                    GetGeminiConfigPath(),
                    dryRun => ApplyJsonTargetAsync(
                        "Gemini CLI",
                        GetGeminiConfigPath(),
                        geminiConfig,
                        dryRun)),
                new InstallTarget(
                    "Cursor",
                    GetCursorConfigPath(),
                    dryRun => ApplyJsonTargetAsync(
                        "Cursor",
                        GetCursorConfigPath(),
                        cursorConfig,
                        dryRun)),
            };
        }

        private static async Task<ApplyResult> ApplyJsonTargetAsync(string clientName, string filePath, JsonObject expectedConfig, bool dryRun)
        {
            JsonObject existingRoot = await ReadOrCreateJsonRootAsync(filePath).ConfigureAwait(false);
            if (existingRoot["mcpServers"] is not JsonObject mcpServers)
            {
                mcpServers = new JsonObject();
                existingRoot["mcpServers"] = mcpServers;
            }

            JsonNode? existingNode = mcpServers[ServerKey];
            bool changed = existingNode == null || !JsonNode.DeepEquals(existingNode, expectedConfig);
            mcpServers[ServerKey] = expectedConfig.DeepClone();
            string output = existingRoot.ToJsonString(JsonOptions);

            if (dryRun)
            {
                return new ApplyResult(clientName, filePath, changed, "Would configure " + clientName + " MCP entry.", output);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            await File.WriteAllTextAsync(filePath, output).ConfigureAwait(false);
            return new ApplyResult(clientName, filePath, changed, changed
                ? "Configured " + clientName + " MCP entry."
                : clientName + " MCP entry already matched the expected configuration.");
        }

        private static async Task<ApplyResult> ApplyCodexTargetAsync(string filePath, string managedBlock, bool dryRun)
        {
            string existing = File.Exists(filePath) ? await File.ReadAllTextAsync(filePath).ConfigureAwait(false) : String.Empty;
            string updated = UpsertCodexConfig(existing, managedBlock);
            bool changed = !String.Equals(existing, updated, StringComparison.Ordinal);

            if (dryRun)
            {
                return new ApplyResult("Codex", filePath, changed, "Would configure Codex MCP entry.", updated);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            await File.WriteAllTextAsync(filePath, updated).ConfigureAwait(false);
            return new ApplyResult("Codex", filePath, changed, changed
                ? "Configured Codex MCP entry."
                : "Codex MCP entry already matched the expected configuration.");
        }

        private static JsonObject BuildClaudeConfig(string mcpUrl)
        {
            return new JsonObject
            {
                ["type"] = "http",
                ["url"] = mcpUrl
            };
        }

        private static JsonObject BuildGeminiConfig(string mcpUrl)
        {
            return new JsonObject
            {
                ["httpUrl"] = mcpUrl,
                ["timeout"] = 30000
            };
        }

        private static JsonObject BuildCursorConfig(string mcpUrl)
        {
            return new JsonObject
            {
                ["url"] = mcpUrl,
                ["transport"] = "http"
            };
        }

        private static string BuildCodexToml(string mcpUrl)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(CodexManagedBlockStart);
            sb.AppendLine("[mcp_servers.restdb]");
            sb.AppendLine("url = " + ToTomlString(mcpUrl));

            sb.AppendLine(CodexManagedBlockEnd);
            return sb.ToString().TrimEnd();
        }

        private static async Task<JsonObject> ReadOrCreateJsonRootAsync(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return new JsonObject();
            }

            string content = await File.ReadAllTextAsync(filePath).ConfigureAwait(false);
            if (String.IsNullOrWhiteSpace(content))
            {
                return new JsonObject();
            }

            JsonNode? node = JsonNode.Parse(content);
            if (node is JsonObject root)
            {
                return root;
            }

            throw new InvalidOperationException("Configuration file '" + filePath + "' does not contain a JSON object.");
        }

        private static string UpsertCodexConfig(string existing, string managedBlock)
        {
            if (String.IsNullOrWhiteSpace(existing))
            {
                return managedBlock + Environment.NewLine;
            }

            int managedStart = existing.IndexOf(CodexManagedBlockStart, StringComparison.Ordinal);
            int managedEnd = existing.IndexOf(CodexManagedBlockEnd, StringComparison.Ordinal);
            if (managedStart >= 0 && managedEnd >= managedStart)
            {
                int afterEnd = managedEnd + CodexManagedBlockEnd.Length;
                string prefix = existing[..managedStart].TrimEnd();
                string suffix = existing[afterEnd..].TrimStart();
                return CombineSections(prefix, managedBlock, suffix);
            }

            if (TryFindExistingCodexSection(existing, out int sectionStart, out int sectionEnd))
            {
                string prefix = existing[..sectionStart].TrimEnd();
                string suffix = existing[sectionEnd..].TrimStart();
                return CombineSections(prefix, managedBlock, suffix);
            }

            return CombineSections(existing.TrimEnd(), managedBlock, String.Empty);
        }

        private static bool TryFindExistingCodexSection(string content, out int startIndex, out int endIndex)
        {
            startIndex = -1;
            endIndex = -1;

            string normalized = content.Replace("\r\n", "\n");
            string[] lines = normalized.Split('\n');
            int characterOffset = 0;
            bool found = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string trimmed = lines[i].Trim();
                if (!found && IsCodexRestDbTableHeader(trimmed))
                {
                    startIndex = characterOffset;
                    found = true;
                }
                else if (found && trimmed.StartsWith("[", StringComparison.Ordinal) && !IsCodexRestDbTableHeader(trimmed))
                {
                    endIndex = characterOffset;
                    break;
                }

                characterOffset += lines[i].Length + 1;
            }

            if (!found)
            {
                return false;
            }

            if (endIndex < 0)
            {
                endIndex = normalized.Length;
            }

            startIndex = TranslateNormalizedIndex(content, startIndex);
            endIndex = TranslateNormalizedIndex(content, endIndex);
            return true;
        }

        private static bool IsCodexRestDbTableHeader(string value)
        {
            return value.Equals("[mcp_servers.restdb]", StringComparison.Ordinal)
                || value.Equals("[mcp_servers.\"restdb\"]", StringComparison.Ordinal)
                || value.Equals("[mcp_servers.restdb.headers]", StringComparison.Ordinal)
                || value.Equals("[mcp_servers.\"restdb\".headers]", StringComparison.Ordinal);
        }

        private static int TranslateNormalizedIndex(string original, int normalizedIndex)
        {
            if (!original.Contains("\r\n", StringComparison.Ordinal))
            {
                return normalizedIndex;
            }

            int originalIndex = 0;
            int seenNormalized = 0;

            while (originalIndex < original.Length && seenNormalized < normalizedIndex)
            {
                if (original[originalIndex] == '\r' && originalIndex + 1 < original.Length && original[originalIndex + 1] == '\n')
                {
                    originalIndex += 2;
                }
                else
                {
                    originalIndex++;
                }

                seenNormalized++;
            }

            return originalIndex;
        }

        private static string CombineSections(string prefix, string middle, string suffix)
        {
            List<string> sections = new List<string>();
            if (!String.IsNullOrWhiteSpace(prefix))
            {
                sections.Add(prefix);
            }

            if (!String.IsNullOrWhiteSpace(middle))
            {
                sections.Add(middle);
            }

            if (!String.IsNullOrWhiteSpace(suffix))
            {
                sections.Add(suffix);
            }

            if (sections.Count < 1)
            {
                return String.Empty;
            }

            return String.Join(Environment.NewLine + Environment.NewLine, sections) + Environment.NewLine;
        }

        private static string NormalizeInstallHostname(string hostname)
        {
            if (String.IsNullOrWhiteSpace(hostname))
            {
                return "localhost";
            }

            string trimmed = hostname.Trim();
            if (trimmed == "+"
                || trimmed == "*"
                || trimmed == "0.0.0.0"
                || trimmed == "::"
                || trimmed == "[::]")
            {
                return "localhost";
            }

            return trimmed;
        }

        private static bool Confirm(string prompt, bool defaultValue)
        {
            Console.Write(prompt + (defaultValue ? " [Y/n] " : " [y/N] "));
            string? response = Console.ReadLine();
            if (String.IsNullOrWhiteSpace(response))
            {
                return defaultValue;
            }

            string normalized = response.Trim().ToLowerInvariant();
            if (normalized == "y" || normalized == "yes")
            {
                return true;
            }

            if (normalized == "n" || normalized == "no")
            {
                return false;
            }

            return defaultValue;
        }

        private static string ToTomlString(string value)
        {
            string escaped = value
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("\"", "\\\"", StringComparison.Ordinal);
            return "\"" + escaped + "\"";
        }

        private static string GetClaudeConfigPath()
        {
            return Path.Combine(GetInstallHomeDirectory(), ".claude.json");
        }

        private static string GetCodexConfigPath()
        {
            return Path.Combine(GetInstallHomeDirectory(), ".codex", "config.toml");
        }

        private static string GetGeminiConfigPath()
        {
            return Path.Combine(GetInstallHomeDirectory(), ".gemini", "settings.json");
        }

        private static string GetCursorConfigPath()
        {
            return Path.Combine(GetInstallHomeDirectory(), ".cursor", "mcp.json");
        }

        private static string GetInstallHomeDirectory()
        {
            string? overrideDirectory = Environment.GetEnvironmentVariable("RESTDB_MCP_INSTALL_HOME");
            if (!String.IsNullOrWhiteSpace(overrideDirectory))
            {
                return overrideDirectory;
            }

            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        private sealed record InstallTarget(string ClientName, string FilePath, Func<bool, Task<ApplyResult>> ApplyAsync);

        private sealed record ApplyResult(string ClientName, string FilePath, bool Changed, string Message, string? Preview = null);
    }
}
