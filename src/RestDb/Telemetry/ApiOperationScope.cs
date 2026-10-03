namespace RestDb.Telemetry
{
    using System;
    using System.Diagnostics;

    /// <summary>
    /// Times one API operation: records the operation counter, duration histogram, in-flight gauge, and error counter,
    /// opens an internal span named "api {operation}", and labels Watson's per-request server span with the operation
    /// and route template (RestDb routes everything through Watson's default route, so Watson cannot name the span itself).
    /// Call <see cref="Complete"/> or <see cref="Fail"/>, then dispose. Best-effort: never throws.
    /// Thread safety: an instance belongs to one request; do not share it across threads.
    /// </summary>
    internal sealed class ApiOperationScope : IDisposable
    {
        #region Private-Members

        private readonly string _Operation;
        private readonly string _PreviousOperation;
        private readonly long _StartTimestamp;
        private readonly Activity _Activity;
        private int _StatusCode = 0;
        private string _ErrorType = null;
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Begin an API operation.
        /// </summary>
        /// <param name="operation">Bounded operation name.</param>
        /// <param name="method">HTTP method.</param>
        /// <param name="routeTemplate">Route template. May be null.</param>
        internal ApiOperationScope(string operation, string method, string routeTemplate)
        {
            _Operation = String.IsNullOrEmpty(operation) ? RestDbTelemetryNames.OperationUnknown : operation;
            _StartTimestamp = Stopwatch.GetTimestamp();
            _PreviousOperation = RestDbTelemetry.CurrentOperation;

            try
            {
                RestDbTelemetry.CurrentOperation = _Operation;
                RestDbTelemetry.AdjustActiveOperations(_Operation, 1);

                RestDbTelemetry.TagServerSpan(method, routeTemplate, _Operation);

                _Activity = RestDbTelemetry.Source.StartActivity("api " + _Operation, ActivityKind.Internal);
                if (_Activity != null)
                {
                    _Activity.SetTag(RestDbTelemetryNames.AttributeOperation, _Operation);
                    if (!String.IsNullOrEmpty(routeTemplate)) _Activity.SetTag(RestDbTelemetryNames.AttributeHttpRoute, routeTemplate);
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
        /// Mark the operation complete with the response status code.
        /// </summary>
        /// <param name="statusCode">HTTP status code.</param>
        internal void Complete(int statusCode)
        {
            _StatusCode = statusCode;
        }

        /// <summary>
        /// Mark the operation failed by an exception. The response is a 500.
        /// </summary>
        /// <param name="e">Exception.</param>
        internal void Fail(Exception e)
        {
            _StatusCode = 500;
            _ErrorType = RestDbTelemetry.ErrorType(e);
            RestDbTelemetry.MarkFailed(_Activity, e);
        }

        /// <summary>
        /// Stop the span and record the operation metrics.
        /// </summary>
        public void Dispose()
        {
            if (_Disposed) return;
            _Disposed = true;

            try
            {
                double seconds = Stopwatch.GetElapsedTime(_StartTimestamp).TotalSeconds;
                int statusCode = _StatusCode == 0 ? 200 : _StatusCode;
                RestDbTelemetry.RecordOperation(_Operation, statusCode, _ErrorType, seconds);
                RestDbTelemetry.AdjustActiveOperations(_Operation, -1);

                if (_Activity != null)
                {
                    _Activity.SetTag(RestDbTelemetryNames.AttributeHttpStatusCode, statusCode);
                    if (_Activity.Status == ActivityStatusCode.Unset)
                    {
                        if (statusCode >= 500) _Activity.SetStatus(ActivityStatusCode.Error, statusCode.ToString());
                        else _Activity.SetStatus(ActivityStatusCode.Ok);
                    }

                    _Activity.Dispose();
                }
            }
            catch (Exception)
            {
                // best-effort
            }
            finally
            {
                RestDbTelemetry.CurrentOperation = _PreviousOperation;
            }
        }

        #endregion
    }
}
