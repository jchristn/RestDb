namespace RestDb.Telemetry
{
    using System;
    using System.Diagnostics;

    /// <summary>
    /// Times one API workflow stage: records the per-stage counter and duration histogram, labeled with the current
    /// operation, and opens a child span named "stage:{stage}". Call <see cref="Complete"/> or <see cref="Fail"/>, then
    /// dispose; a scope disposed without either is recorded as an error. Best-effort: never throws.
    /// Thread safety: an instance belongs to one async flow; do not share it across threads.
    /// </summary>
    internal sealed class StageScope : IDisposable
    {
        #region Private-Members

        private readonly string _Stage;
        private readonly string _Operation;
        private readonly long _StartTimestamp;
        private readonly Activity _Activity;
        private bool _Succeeded = false;
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Begin a stage.
        /// </summary>
        /// <param name="stage">Bounded stage name.</param>
        internal StageScope(string stage)
        {
            _Stage = String.IsNullOrEmpty(stage) ? "unknown" : stage;
            _Operation = RestDbTelemetry.CurrentOperation;
            _StartTimestamp = Stopwatch.GetTimestamp();

            try
            {
                _Activity = RestDbTelemetry.Source.StartActivity("stage:" + _Stage, ActivityKind.Internal);
                if (_Activity != null)
                {
                    _Activity.SetTag(RestDbTelemetryNames.AttributeStage, _Stage);
                    if (_Operation != null) _Activity.SetTag(RestDbTelemetryNames.AttributeOperation, _Operation);
                }
            }
            catch (Exception)
            {
                // best-effort
            }
        }

        #endregion

        #region Internal-Methods

        /// <summary>
        /// Mark the stage successful.
        /// </summary>
        internal void Complete()
        {
            _Succeeded = true;
        }

        /// <summary>
        /// Mark the stage failed by an exception.
        /// </summary>
        /// <param name="e">Exception.</param>
        internal void Fail(Exception e)
        {
            _Succeeded = false;
            RestDbTelemetry.MarkFailed(_Activity, e);
        }

        /// <summary>
        /// Stop the span and record the stage metrics.
        /// </summary>
        public void Dispose()
        {
            if (_Disposed) return;
            _Disposed = true;

            try
            {
                double seconds = Stopwatch.GetElapsedTime(_StartTimestamp).TotalSeconds;
                string outcome = _Succeeded ? RestDbTelemetryNames.OutcomeSuccess : RestDbTelemetryNames.OutcomeError;
                RestDbTelemetry.RecordStage(_Operation, _Stage, outcome, seconds);

                if (_Activity != null)
                {
                    if (_Activity.Status == ActivityStatusCode.Unset)
                    {
                        if (_Succeeded) _Activity.SetStatus(ActivityStatusCode.Ok);
                        else _Activity.SetStatus(ActivityStatusCode.Error);
                    }

                    _Activity.Dispose();
                }
            }
            catch (Exception)
            {
                // best-effort
            }
        }

        #endregion
    }
}
