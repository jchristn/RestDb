namespace RestDb.Test.Shared;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using RestDb.McpServer.Classes;

/// <summary>
/// Deterministic tool definitions registered through <see cref="RestMcpToolRegistrar"/> so MCP transport
/// tests exercise the production registration path without requiring a live RestDb server.
/// </summary>
internal static class McpTestTools
{
    public const string EchoToolName = "restdb_test_echo";
    public const string FailToolName = "restdb_test_fail";
    public const string FailureMessage = "Simulated downstream failure.";
    public const string DownstreamOkToolName = "restdb_test_downstream_ok";
    public const string DownstreamNotFoundToolName = "restdb_test_downstream_not_found";

    public static List<RestMcpToolDefinition> Build()
    {
        return new List<RestMcpToolDefinition>
        {
            new RestMcpToolDefinition(
                EchoToolName,
                "Echoes the supplied message.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        message = new { type = "string", description = "Message to echo." }
                    },
                    required = new[] { "message" }
                },
                (arguments, token) =>
                {
                    string? message = null;
                    if (arguments.HasValue
                        && arguments.Value.ValueKind == JsonValueKind.Object
                        && arguments.Value.TryGetProperty("message", out JsonElement value))
                    {
                        message = value.GetString();
                    }

                    return Task.FromResult<object>(new { echoed = message });
                }),
            new RestMcpToolDefinition(
                FailToolName,
                "Always fails.",
                new
                {
                    type = "object",
                    properties = new { }
                },
                (arguments, token) => throw new InvalidOperationException(FailureMessage)),
            new RestMcpToolDefinition(
                DownstreamOkToolName,
                "Returns a successful downstream RestDb response.",
                EmptySchema(),
                (arguments, token) => Task.FromResult<object>(new RestMcpResponse
                {
                    Success = true,
                    StatusCode = 200,
                    ReasonPhrase = "OK",
                    Body = "downstream-ok"
                })),
            new RestMcpToolDefinition(
                DownstreamNotFoundToolName,
                "Returns a failed (404) downstream RestDb response.",
                EmptySchema(),
                (arguments, token) => Task.FromResult<object>(new RestMcpResponse
                {
                    Success = false,
                    StatusCode = 404,
                    ReasonPhrase = "Not Found",
                    Body = "downstream-not-found"
                }))
        };
    }

    public static List<string> Names()
    {
        List<string> names = new List<string>();
        foreach (RestMcpToolDefinition tool in Build())
        {
            names.Add(tool.Name);
        }

        return names;
    }

    private static object EmptySchema()
    {
        return new
        {
            type = "object",
            properties = new { }
        };
    }
}
