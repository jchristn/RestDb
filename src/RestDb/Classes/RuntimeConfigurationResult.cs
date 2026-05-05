namespace RestDb
{
    /// <summary>
    /// Result of a configuration or context update/reload operation.
    /// </summary>
    public class RuntimeConfigurationResult
    {
        /// <summary>
        /// Success indicator.
        /// </summary>
        public bool Success { get; set; } = true;

        /// <summary>
        /// Human-readable message.
        /// </summary>
        public string Message { get; set; } = null;

        /// <summary>
        /// Whether a process restart is required for all settings to take effect.
        /// </summary>
        public bool RestartRequired { get; set; } = false;
    }
}
