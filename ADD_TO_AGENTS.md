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
dotnet run --project src\RestDb.McpServer\RestDb.McpServer.csproj -- install --api-key default --yes
```

Preview without writing files:

```powershell
dotnet run --project src\RestDb.McpServer\RestDb.McpServer.csproj -- install --api-key default --dry-run
```

By default, the installer targets these user-level files:

- Claude Code: `~/.claude.json`
- Codex: `~/.codex/config.toml`
- Gemini CLI: `~/.gemini/settings.json`
- Cursor: `~/.cursor/mcp.json`

If you supply `--api-key`, the installer writes both:

- `Authorization: Bearer <api-key>`
- the configured API key header, usually `x-api-key: <api-key>`

That keeps the installed definitions compatible with both RestDb auth styles.

## Defaults In This Repo

If you started the Docker stack from this repo with the default ports, the relevant values are:

- RestDb API: `http://localhost:8000`
- RestDb MCP HTTP endpoint: `http://localhost:8010/mcp`
- Sample API key: `default`

If your ports or key are different, replace them in the examples below.

## Recommended Authentication Header

RestDb supports both:

- `Authorization: Bearer <api-key>`
- `x-api-key: <api-key>`

For cross-client consistency, the examples below use:

```text
Authorization: Bearer default
```

That is usually the easiest way to configure an authenticated HTTP MCP server across multiple agent clients.

## Important Note About Localhost

All examples below assume the agent client is running on the same machine as RestDb, so `localhost` works.

If the client runs somewhere else, replace:

```text
http://localhost:8010/mcp
```

with a host name or IP address that the client can actually reach.

## Claude Code

Claude Code's MCP docs explicitly support remote HTTP MCP servers and custom headers.

### Add It

```powershell
claude mcp add --scope user --transport http restdb http://localhost:8010/mcp --header "Authorization: Bearer default"
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

The docs page used here does not show a protected-header example, so the header block below is the practical config to use for a RestDb MCP endpoint that requires an API key.

### Add The Server URL

```powershell
codex mcp add restdb --url http://localhost:8010/mcp
```

### Add The Header In `~/.codex/config.toml`

```toml
[mcp_servers.restdb]
url = "http://localhost:8010/mcp"

[mcp_servers.restdb.headers]
Authorization = "Bearer default"
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

Gemini CLI's MCP docs explicitly support:

- HTTP MCP via `httpUrl`
- custom `headers`
- CLI add via `gemini mcp add --transport http`

Gemini CLI stores user settings in:

- `~/.gemini/settings.json`

and project settings in:

- `.gemini/settings.json`

### Add It With The CLI

```powershell
gemini mcp add --transport http --header "Authorization: Bearer default" restdb http://localhost:8010/mcp
```

### Or Add It Directly In `~/.gemini/settings.json`

```json
{
  "mcpServers": {
    "restdb": {
      "httpUrl": "http://localhost:8010/mcp",
      "headers": {
        "Authorization": "Bearer default"
      },
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

Cursor's MCP docs explicitly support:

- streamable HTTP MCP servers
- a `headers` object for authentication
- shared MCP configuration between the editor and `cursor-agent`

### Add It In `mcp.json`

The OpenAI Docs MCP page shows Cursor reading from `~/.cursor/mcp.json` on macOS/Linux. On Windows, the practical home-directory equivalent is typically `%USERPROFILE%\.cursor\mcp.json`.

Use:

```json
{
  "mcpServers": {
    "restdb": {
      "url": "http://localhost:8010/mcp",
      "headers": {
        "Authorization": "Bearer default"
      }
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

## Using `x-api-key` Instead

If you prefer the explicit RestDb header instead of bearer auth, replace:

```text
Authorization: Bearer default
```

with:

```text
x-api-key: default
```

Examples:

### Claude Code

```powershell
claude mcp add --scope user --transport http restdb http://localhost:8010/mcp --header "x-api-key: default"
```

### Gemini CLI

```json
{
  "mcpServers": {
    "restdb": {
      "httpUrl": "http://localhost:8010/mcp",
      "headers": {
        "x-api-key": "default"
      }
    }
  }
}
```

### Cursor

```json
{
  "mcpServers": {
    "restdb": {
      "url": "http://localhost:8010/mcp",
      "headers": {
        "x-api-key": "default"
      }
    }
  }
}
```

### Codex

```toml
[mcp_servers.restdb]
url = "http://localhost:8010/mcp"

[mcp_servers.restdb.headers]
x-api-key = "default"
```

## Troubleshooting

If a client does not connect:

1. Confirm the MCP service is running:

```powershell
curl.exe http://localhost:8010/
```

2. Confirm the RestDb API itself is healthy:

```powershell
curl.exe -H "Authorization: Bearer default" http://localhost:8000/_databases
```

3. Confirm you used the MCP endpoint, not the REST endpoint:

- Correct: `http://localhost:8010/mcp`
- Incorrect: `http://localhost:8000`

4. If the client is not local, do not use `localhost`.

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
