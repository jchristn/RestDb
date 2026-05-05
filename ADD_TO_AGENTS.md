# Add RestDb To Agents

This document shows how to add the RestDb MCP server to:

- Claude Code
- Codex
- Gemini CLI
- Cursor

It uses the MCP HTTP endpoint exposed by `RestDb.McpServer`, not the raw RestDb REST API.

## Fastest Option: Use The Built-In Installer

`RestDb.McpServer` can write the MCP definition directly into the supported client config files for:

- Claude Code
- Codex
- Gemini CLI
- Cursor

Example:

```powershell
restdb.mcpserver install --yes
```

From source without installing the executable on your `PATH`:

```powershell
dotnet run --project src\RestDb.McpServer\RestDb.McpServer.csproj -- install --yes
```

Preview without writing files:

```powershell
restdb.mcpserver install --dry-run
```

From source:

```powershell
dotnet run --project src\RestDb.McpServer\RestDb.McpServer.csproj -- install --dry-run
```

By default, the installer targets these user-level files:

- Claude Code: `~/.claude.json`
- Codex: `~/.codex/config.toml`
- Gemini CLI: `~/.gemini/settings.json`
- Cursor: `~/.cursor/mcp.json`

The agent client configs written by `install` are URL-only HTTP MCP definitions. The agent does not need to send the RestDb API key to the MCP server.

`install` does not persist downstream RestDb credentials. Configure those on the `RestDb.McpServer` process itself with `--api-key`, `--bearer-token`, or the `RESTDB_MCP_*` environment variables described below.

## Defaults In This Repo

If you started the Docker stack from this repo with the default ports, the relevant values are:

- RestDb API: `http://localhost:8000`
- RestDb MCP HTTP endpoint: `http://localhost:8010/mcp`
- Sample API key: `default`

If your ports or key are different, replace them in the examples below.

## Where Authentication Happens

RestDb supports both:

- `Authorization: Bearer <api-key>`
- `x-api-key: <api-key>`

Those headers are for the RestDb HTTP API itself.

`RestDb.McpServer` is a proxy. Configure the API key on the MCP server process:

```powershell
restdb.mcpserver --server-url http://localhost:8000 --api-key default
```

or in Docker Compose through:

- `RESTDB_MCP_API_KEY`
- `RESTDB_MCP_API_KEY_HEADER`
- `RESTDB_MCP_BEARER_TOKEN`

The agent client then connects to the MCP HTTP endpoint without extra RestDb auth headers.

## Important Note About Localhost

All examples below assume the agent client is running on the same machine as RestDb, so `localhost` works.

If the client runs somewhere else, replace:

```text
http://localhost:8010/mcp
```

with a host name or IP address that the client can actually reach.

## Claude Code

Claude Code's MCP docs explicitly support remote HTTP MCP servers.

### Add It

```powershell
claude mcp add --scope user --transport http restdb http://localhost:8010/mcp
```

### Verify It

```powershell
claude mcp list
```

### Try It

Ask Claude Code:

```text
List the databases available from the RestDb MCP server.
```

## Codex

OpenAI's Codex docs explicitly show:

- `codex mcp add <name> --url <mcp-url>`
- direct editing of `~/.codex/config.toml`

Codex should point at the RestDb MCP HTTP endpoint directly. The RestDb API key belongs on the `RestDb.McpServer` process, not in the Codex MCP client config.

### Add The Server URL

```powershell
codex mcp add restdb --url http://localhost:8010/mcp
```

### Or Add It Directly In `~/.codex/config.toml`

```toml
[mcp_servers.restdb]
url = "http://localhost:8010/mcp"
```

### Verify It

```powershell
codex mcp list
```

### Try It

Ask Codex:

```text
Use the RestDb MCP server and tell me which databases are available.
```

## Gemini CLI

Gemini CLI's MCP docs explicitly support HTTP MCP via `httpUrl` and CLI add via `gemini mcp add --transport http`.

Gemini CLI stores user settings in:

- `~/.gemini/settings.json`

and project settings in:

- `.gemini/settings.json`

### Add It With The CLI

```powershell
gemini mcp add --transport http restdb http://localhost:8010/mcp
```

### Or Add It Directly In `~/.gemini/settings.json`

```json
{
  "mcpServers": {
    "restdb": {
      "httpUrl": "http://localhost:8010/mcp",
      "timeout": 30000
    }
  }
}
```

### Verify It

```powershell
gemini mcp list
```

### Try It

Ask Gemini CLI:

```text
Use the restdb MCP server and list the databases it exposes.
```

## Cursor

Cursor's MCP docs explicitly support streamable HTTP MCP servers and shared MCP configuration between the editor and `cursor-agent`.

### Add It In `mcp.json`

The OpenAI Docs MCP page shows Cursor reading from `~/.cursor/mcp.json` on macOS/Linux. On Windows, the practical home-directory equivalent is typically `%USERPROFILE%\.cursor\mcp.json`.

Use:

```json
{
  "mcpServers": {
    "restdb": {
      "url": "http://localhost:8010/mcp"
    }
  }
}
```

### Verify It

```powershell
cursor-agent mcp list
```

You can also inspect the available tools:

```powershell
cursor-agent mcp list-tools restdb
```

### Try It

In Cursor Agent mode, ask:

```text
Use the RestDb MCP server and enumerate the databases and tables.
```

## Troubleshooting

If a client does not connect:

1. Confirm the MCP service is running:

```powershell
curl.exe -H "Content-Type: application/json" -d "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"ping\"}" http://localhost:8010/mcp
```

2. Confirm the RestDb API itself is healthy and that the MCP server can authenticate to it:

```powershell
curl.exe -H "Authorization: Bearer default" http://localhost:8000/_databases
```

3. Confirm you used the MCP streamable HTTP endpoint, not the REST endpoint:

- Correct: `http://localhost:8010/mcp`
- Incorrect: `http://localhost:8000`

4. If the client is not local, do not use `localhost`.

5. If you are using `restdb.mcpserver install`, rerun it after updating the binary so the generated agent config picks up the `/mcp` endpoint.

## Sources

Official references used for the client-specific instructions:

- Claude Code MCP docs: https://docs.anthropic.com/en/docs/claude-code/mcp
- OpenAI Docs MCP page, including Codex and Cursor examples: https://platform.openai.com/docs/docs-mcp
- Gemini CLI MCP docs: https://github.com/google-gemini/gemini-cli/blob/main/docs/tools/mcp-server.md
- Gemini CLI settings locations: https://github.com/google-gemini/gemini-cli/blob/main/docs/cli/settings.md
- Cursor MCP docs: https://docs.cursor.com/cli/mcp
- Cursor MCP configuration examples and headers support: https://docs.cursor.com/es/context/mcp

Repo-local references:

- [MCP_API.md](MCP_API.md)
- [README.md](README.md)
