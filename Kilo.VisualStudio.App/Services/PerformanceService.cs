using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Kilo.VisualStudio.App.Services
{
    public class PerformanceService
    {
        private readonly PerformanceMetrics _metrics = new PerformanceMetrics();
        private readonly object _lock = new object();

        public event EventHandler<PerformanceWarning>? PerformanceWarning;

        public PerformanceMetrics GetMetrics() => _metrics;

        public IDisposable StartMeasure(string operation)
        {
            return new MeasureScope(this, operation);
        }

        public async Task<T> MeasureAsync<T>(string operation, Func<Task<T>> action)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                return await action();
            }
            finally
            {
                RecordOperation(operation, sw.ElapsedMilliseconds);
            }
        }

        public void RecordOperation(string operation, long durationMs)
        {
            lock (_lock)
            {
                if (!_metrics.OperationTimes.ContainsKey(operation))
                {
                    _metrics.OperationTimes[operation] = new RollingAverage(20);
                }

                _metrics.OperationTimes[operation].Add(durationMs);
                _metrics.TotalOperations++;

                if (durationMs > GetThreshold(operation))
                {
                    PerformanceWarning?.Invoke(this, new PerformanceWarning
                    {
                        Operation = operation,
                        DurationMs = durationMs,
                        Threshold = GetThreshold(operation),
                        Timestamp = DateTimeOffset.UtcNow
                    });
                }
            }
        }

        public void RecordMemory(long bytes)
        {
            lock (_lock)
            {
                _metrics.CurrentMemoryBytes = bytes;
                if (bytes > _metrics.PeakMemoryBytes)
                {
                    _metrics.PeakMemoryBytes = bytes;
                }
            }
        }

        public void RecordThreadPoolUsage(int activeThreads, int availableThreads)
        {
            lock (_lock)
            {
                _metrics.ActiveThreads = activeThreads;
                _metrics.AvailableThreads = availableThreads;
            }
        }

        public string GetPerformanceReport()
        {
            lock (_lock)
            {
                var report = "=== Performance Report ===\n\n";
                report += $"Total Operations: {_metrics.TotalOperations}\n";
                report += $"Current Memory: {_metrics.CurrentMemoryBytes / 1024 / 1024} MB\n";
                report += $"Peak Memory: {_metrics.PeakMemoryBytes / 1024 / 1024} MB\n";
                report += $"Active Threads: {_metrics.ActiveThreads}\n\n";

                report += "Operation Times:\n";
                foreach (var kvp in _metrics.OperationTimes)
                {
                    var avg = kvp.Value.GetAverage();
                    report += $"  {kvp.Key}: {avg:F2}ms (avg)\n";
                }

                return report;
            }
        }

        private long GetThreshold(string operation)
        {
            return operation.ToLower() switch
            {
                "search" => 5000,
                "index" => 30000,
                "request" => 10000,
                "render" => 100,
                _ => 5000
            };
        }

        private class MeasureScope : IDisposable
        {
            private readonly PerformanceService _service;
            private readonly string _operation;
            private readonly Stopwatch _sw;

            public MeasureScope(PerformanceService service, string operation)
            {
                _service = service;
                _operation = operation;
                _sw = Stopwatch.StartNew();
            }

            public void Dispose()
            {
                _sw.Stop();
                _service.RecordOperation(_operation, _sw.ElapsedMilliseconds);
            }
        }
    }

    public class PerformanceMetrics
    {
        public long TotalOperations { get; set; }
        public long CurrentMemoryBytes { get; set; }
        public long PeakMemoryBytes { get; set; }
        public int ActiveThreads { get; set; }
        public int AvailableThreads { get; set; }
        public System.Collections.Generic.Dictionary<string, RollingAverage> OperationTimes { get; set; } = new System.Collections.Generic.Dictionary<string, RollingAverage>();
    }

    public class PerformanceWarning
    {
        public string Operation { get; set; } = string.Empty;
        public long DurationMs { get; set; }
        public long Threshold { get; set; }
        public DateTimeOffset Timestamp { get; set; }
    }

    public class RollingAverage
    {
        private readonly int _windowSize;
        private readonly System.Collections.Generic.Queue<double> _values = new System.Collections.Generic.Queue<double>();

        public RollingAverage(int windowSize)
        {
            _windowSize = windowSize;
        }

        public void Add(double value)
        {
            if (_values.Count >= _windowSize)
            {
                _values.Dequeue();
            }
            _values.Enqueue(value);
        }

        public double GetAverage()
        {
            if (_values.Count == 0) return 0;
            double sum = 0;
            foreach (var v in _values) sum += v;
            return sum / _values.Count;
        }
    }
}