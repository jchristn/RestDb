namespace RestDb.McpServer.Classes
{
    using System;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class RestMcpToolDefinition
    {
        public string Name { get; }

        public string Description { get; }

        public object InputSchema { get; }

        public Func<JsonElement?, CancellationToken, Task<object>> Handler { get; }

        public RestMcpToolDefinition(
            string name,
            string description,
            object inputSchema,
            Func<JsonElement?, CancellationToken, Task<object>> handler)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Description = description ?? throw new ArgumentNullException(nameof(description));
            InputSchema = inputSchema ?? throw new ArgumentNullException(nameof(inputSchema));
            Handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }
    }
}
