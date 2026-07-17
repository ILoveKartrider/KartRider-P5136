using KartRider_PacketName;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace KartRider.Common.Network
{
    /// <summary>
    /// Records packet details without performing file or Console/UI I/O on a
    /// network callback thread. Detail and UI queues are independently bounded.
    /// </summary>
    public static class PacketTrace
    {
        private const int DetailQueueCapacity = 2048;
        private const int UiQueueCapacity = 4096;
        private static readonly object LifecycleSync = new object();

        private static TraceWriterWorker _worker;
        private static string _tracePath = "";
        private static bool _processExitHooked;
        private static string _logDirectory = Path.GetFullPath(
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs"));

        public static bool Enabled => Volatile.Read(ref _worker) != null;

        public static string TracePath
        {
            get
            {
                lock (LifecycleSync)
                {
                    return _tracePath;
                }
            }
        }

        public static void Configure(bool enabled, string logDirectory)
        {
            lock (LifecycleSync)
            {
                StopWorkerLocked();

                _logDirectory = string.IsNullOrWhiteSpace(logDirectory)
                    ? Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs"))
                    : Path.GetFullPath(logDirectory);
                _tracePath = "";

                if (!enabled)
                {
                    return;
                }

                try
                {
                    TraceWriterWorker worker = TraceWriterWorker.Start(
                        _logDirectory,
                        OnWorkerFailure,
                        DetailQueueCapacity,
                        UiQueueCapacity);
                    _tracePath = worker.Path;
                    Volatile.Write(ref _worker, worker);
                    worker.TryEnqueueUiMessage(
                        $"[PACKET TRACE] Full RX/TX log: {worker.Path}");

                    if (!_processExitHooked)
                    {
                        AppDomain.CurrentDomain.ProcessExit += delegate { Stop(); };
                        _processExitHooked = true;
                    }
                }
                catch (Exception ex)
                {
                    QueueConsoleError($"[PACKET TRACE ERROR] Could not start tracing: {ex}");
                }
            }
        }

        public static void Start(string logDirectory = null)
        {
            Configure(true, logDirectory);
        }

        public static void Stop()
        {
            lock (LifecycleSync)
            {
                StopWorkerLocked();
            }
        }

        public static void LogPacket(
            string transport,
            string direction,
            EndPoint localEndPoint,
            EndPoint remoteEndPoint,
            string identity,
            byte[] packet,
            int hashOffset = 0,
            string details = null,
            byte[] wirePacket = null)
        {
            TraceWriterWorker worker = Volatile.Read(ref _worker);
            if (worker == null)
            {
                return;
            }

            worker.LogPacket(
                transport,
                direction,
                localEndPoint,
                remoteEndPoint,
                identity,
                packet,
                hashOffset,
                details,
                wirePacket);
        }

        public static void LogEvent(
            string transport,
            string eventName,
            EndPoint localEndPoint,
            EndPoint remoteEndPoint,
            string identity,
            string details)
        {
            TraceWriterWorker worker = Volatile.Read(ref _worker);
            if (worker == null)
            {
                return;
            }

            worker.LogEvent(
                transport,
                eventName,
                localEndPoint,
                remoteEndPoint,
                identity,
                details,
                publishToUi: true);
        }

        public static void LogDetailEvent(
            string transport,
            string eventName,
            EndPoint localEndPoint,
            EndPoint remoteEndPoint,
            string identity,
            string details)
        {
            TraceWriterWorker worker = Volatile.Read(ref _worker);
            if (worker == null)
            {
                return;
            }

            worker.LogEvent(
                transport,
                eventName,
                localEndPoint,
                remoteEndPoint,
                identity,
                details,
                publishToUi: false);
        }

        private static void StopWorkerLocked()
        {
            TraceWriterWorker worker = Interlocked.Exchange(ref _worker, null);
            if (worker == null)
            {
                return;
            }

            TraceStopResult result = worker.StopAndDrain();
            if (!result.WriterStopped || !result.UiStopped || !result.AdmissionsDrained)
            {
                QueueConsoleError(
                    $"[PACKET TRACE] Bounded shutdown reached its limit for {worker.Path}; " +
                    $"admissionsDrained={result.AdmissionsDrained}, " +
                    $"writerStopped={result.WriterStopped}, uiStopped={result.UiStopped}. " +
                    "The background thread may finish the existing file later.");
            }
        }

        private static void OnWorkerFailure(TraceWriterWorker worker, Exception exception)
        {
            Interlocked.CompareExchange(ref _worker, null, worker);
            QueueConsoleError(
                $"[PACKET TRACE ERROR] Writer disabled for {worker.Path}: {exception}");
        }

        private static void QueueConsoleError(string message)
        {
            try
            {
                ThreadPool.QueueUserWorkItem(
                    static state => SafeConsoleError((string)state),
                    message);
            }
            catch
            {
            }
        }

        private static void SafeConsoleError(string message)
        {
            try
            {
                Console.Error.WriteLine(message);
            }
            catch
            {
            }
        }

        private static void SafeConsoleWriteBatch(string messages)
        {
            try
            {
                Console.Write(messages);
            }
            catch
            {
            }
        }

        private static byte[] Snapshot(byte[] value)
        {
            if (value == null || value.Length == 0)
            {
                return Array.Empty<byte>();
            }

            byte[] snapshot = new byte[value.Length];
            Buffer.BlockCopy(value, 0, snapshot, 0, value.Length);
            return snapshot;
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "-";
            }

            return value
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("|", "/");
        }

        internal static PacketTraceDiagnostics GetDiagnosticsForTesting()
        {
            TraceWriterWorker worker = Volatile.Read(ref _worker);
            return worker == null ? default : worker.GetDiagnostics();
        }

        internal static bool PauseDetailWriterForTesting(int timeoutMilliseconds)
        {
            TraceWriterWorker worker = Volatile.Read(ref _worker);
            return worker != null && worker.PauseDetailWriterForTesting(timeoutMilliseconds);
        }

        internal static void ResumeDetailWriterForTesting()
        {
            Volatile.Read(ref _worker)?.ResumeDetailWriterForTesting();
        }

        internal static void InjectWriterFailureForTesting()
        {
            Volatile.Read(ref _worker)?.InjectWriterFailureForTesting();
        }

        internal readonly struct PacketTraceDiagnostics
        {
            public PacketTraceDiagnostics(
                long attempted,
                long enqueued,
                long written,
                long detailDropped,
                long uiDropped,
                long packetSnapshots,
                long shutdownRejected)
            {
                Attempted = attempted;
                Enqueued = enqueued;
                Written = written;
                DetailDropped = detailDropped;
                UiDropped = uiDropped;
                PacketSnapshots = packetSnapshots;
                ShutdownRejected = shutdownRejected;
            }

            public long Attempted { get; }
            public long Enqueued { get; }
            public long Written { get; }
            public long DetailDropped { get; }
            public long UiDropped { get; }
            public long PacketSnapshots { get; }
            public long ShutdownRejected { get; }
        }

        private enum ReservationResult
        {
            Disabled,
            Dropped,
            Reserved
        }

        private enum TraceSummaryKind
        {
            Packet,
            Event,
            Message
        }

        private sealed class TraceSummary
        {
            private string _cachedLine;

            private TraceSummary(TraceSummaryKind kind)
            {
                Kind = kind;
            }

            public TraceSummaryKind Kind { get; }
            public DateTimeOffset Timestamp { get; private set; }
            public long Sequence { get; set; }
            public string Transport { get; private set; }
            public string Action { get; private set; }
            public string LocalEndPoint { get; private set; }
            public string RemoteEndPoint { get; private set; }
            public string Identity { get; private set; }
            public string Details { get; private set; }
            public int PayloadLength { get; private set; }
            public bool HasHash { get; private set; }
            public uint Hash { get; private set; }
            public string Message { get; private set; }

            public static TraceSummary Packet(
                DateTimeOffset timestamp,
                string transport,
                string direction,
                string localEndPoint,
                string remoteEndPoint,
                string identity,
                string details,
                int payloadLength,
                bool hasHash,
                uint hash)
            {
                return new TraceSummary(TraceSummaryKind.Packet)
                {
                    Timestamp = timestamp,
                    Transport = transport,
                    Action = direction,
                    LocalEndPoint = localEndPoint,
                    RemoteEndPoint = remoteEndPoint,
                    Identity = identity,
                    Details = details,
                    PayloadLength = payloadLength,
                    HasHash = hasHash,
                    Hash = hash
                };
            }

            public static TraceSummary Event(
                DateTimeOffset timestamp,
                string transport,
                string eventName,
                string localEndPoint,
                string remoteEndPoint,
                string identity,
                string details)
            {
                return new TraceSummary(TraceSummaryKind.Event)
                {
                    Timestamp = timestamp,
                    Transport = transport,
                    Action = eventName,
                    LocalEndPoint = localEndPoint,
                    RemoteEndPoint = remoteEndPoint,
                    Identity = identity,
                    Details = details
                };
            }

            public static TraceSummary UiMessage(string message)
            {
                return new TraceSummary(TraceSummaryKind.Message)
                {
                    Message = message ?? ""
                };
            }

            public string GetLine()
            {
                string cached = Volatile.Read(ref _cachedLine);
                if (cached != null)
                {
                    return cached;
                }

                string line;
                if (Kind == TraceSummaryKind.Message)
                {
                    line = Message;
                }
                else if (Kind == TraceSummaryKind.Event)
                {
                    line =
                        $"{Timestamp.ToString("O", CultureInfo.InvariantCulture)}" +
                        $" | EVENT | seq={Sequence:D8} | transport={Normalize(Transport)}" +
                        $" | event={Normalize(Action)} | local={Normalize(LocalEndPoint)}" +
                        $" | remote={Normalize(RemoteEndPoint)} | id={Normalize(Identity)}" +
                        $" | details={Normalize(Details)}";
                }
                else
                {
                    string packetName = HasHash
                        ? Enum.GetName(typeof(PacketName), (PacketName)Hash) ?? "UNKNOWN"
                        : "MALFORMED";
                    string hashText = HasHash ? $"0x{Hash:X8}" : "-";
                    line =
                        $"{Timestamp.ToString("O", CultureInfo.InvariantCulture)}" +
                        $" | PACKET | seq={Sequence:D8} | transport={Normalize(Transport)}" +
                        $" | dir={Normalize(Action)} | local={Normalize(LocalEndPoint)}" +
                        $" | remote={Normalize(RemoteEndPoint)} | id={Normalize(Identity)}" +
                        $" | len={PayloadLength} | hash={hashText} | name={packetName}" +
                        $" | details={Normalize(Details)}";
                }

                Interlocked.CompareExchange(ref _cachedLine, line, null);
                return _cachedLine;
            }
        }

        private sealed class TraceRecord
        {
            public TraceRecord(TraceSummary summary, byte[] payload, byte[] wirePacket)
            {
                Summary = summary;
                Payload = payload;
                WirePacket = wirePacket;
            }

            public TraceSummary Summary { get; }
            public byte[] Payload { get; }
            public byte[] WirePacket { get; }
            public bool IsPacket => Payload != null;
        }

        private readonly struct TraceStopResult
        {
            public TraceStopResult(
                bool admissionsDrained,
                bool writerStopped,
                bool uiStopped)
            {
                AdmissionsDrained = admissionsDrained;
                WriterStopped = writerStopped;
                UiStopped = uiStopped;
            }

            public bool AdmissionsDrained { get; }
            public bool WriterStopped { get; }
            public bool UiStopped { get; }
        }

        private sealed class TraceWriterWorker
        {
            private const int FlushRecordInterval = 256;
            private const int UiBatchSize = 64;
            private const int AdmissionDrainTimeoutMilliseconds = 500;
            private const int WriterDrainTimeoutMilliseconds = 4000;
            private const int UiDrainTimeoutMilliseconds = 500;
            private static readonly TimeSpan FlushTimeInterval = TimeSpan.FromSeconds(1);
            private static readonly char[] HexDigits = "0123456789ABCDEF".ToCharArray();

            private readonly object _admissionSync = new object();
            private readonly BlockingCollection<TraceRecord> _detailQueue;
            private readonly BlockingCollection<TraceSummary> _uiQueue;
            private readonly SemaphoreSlim _detailSlots;
            private readonly ManualResetEventSlim _admissionsDrained =
                new ManualResetEventSlim(true);
            private readonly StreamWriter _writer;
            private readonly Thread _writerThread;
            private readonly Thread _uiThread;
            private readonly Action<TraceWriterWorker, Exception> _failureReporter;
            private readonly char[] _hexBuffer = new char[8190];

            private readonly ManualResetEventSlim _testWriterGate =
                new ManualResetEventSlim(true);
            private readonly ManualResetEventSlim _testWriterPaused =
                new ManualResetEventSlim(false);

            private long _sequence;
            private long _attempted;
            private long _enqueued;
            private long _written;
            private long _detailDropped;
            private long _fileReportedDropped;
            private long _uiDropped;
            private long _uiReportedDropped;
            private long _uiReportedDetailDropped;
            private long _packetSnapshots;
            private long _shutdownRejected;
            private int _activeAdmissions;
            private int _accepting = 1;
            private int _forceCompleting;
            private int _queuesCompleted;
            private int _stopped;
            private int _faultReported;
            private int _producerFailureReported;
            private int _testPauseRequested;
            private int _testFailureRequested;

            private TraceWriterWorker(
                string path,
                StreamWriter writer,
                Action<TraceWriterWorker, Exception> failureReporter,
                int detailQueueCapacity,
                int uiQueueCapacity)
            {
                Path = path;
                _writer = writer;
                _failureReporter = failureReporter;
                _detailQueue = new BlockingCollection<TraceRecord>(
                    new ConcurrentQueue<TraceRecord>(),
                    detailQueueCapacity);
                _uiQueue = new BlockingCollection<TraceSummary>(
                    new ConcurrentQueue<TraceSummary>(),
                    uiQueueCapacity);
                _detailSlots = new SemaphoreSlim(
                    detailQueueCapacity,
                    detailQueueCapacity);
                _writerThread = new Thread(WriteLoop)
                {
                    IsBackground = true,
                    Name = "KartRider Packet Trace Writer"
                };
                _uiThread = new Thread(UiLoop)
                {
                    IsBackground = true,
                    Name = "KartRider Packet Trace UI Summary"
                };
            }

            public string Path { get; }

            public static TraceWriterWorker Start(
                string logDirectory,
                Action<TraceWriterWorker, Exception> failureReporter,
                int detailQueueCapacity,
                int uiQueueCapacity)
            {
                Directory.CreateDirectory(logDirectory);

                int processId = Process.GetCurrentProcess().Id;
                string stem = $"packet-trace_{DateTime.Now:yyyyMMdd_HHmmss_fff}_{processId}";
                FileStream stream = null;
                string selectedPath = "";
                for (int attempt = 0; attempt < 100; attempt++)
                {
                    selectedPath = System.IO.Path.Combine(
                        logDirectory,
                        attempt == 0 ? stem + ".log" : $"{stem}_{attempt}.log");
                    try
                    {
                        stream = new FileStream(
                            selectedPath,
                            FileMode.CreateNew,
                            FileAccess.Write,
                            FileShare.Read,
                            64 * 1024,
                            FileOptions.SequentialScan);
                        break;
                    }
                    catch (IOException)
                    {
                    }
                }

                if (stream == null)
                {
                    throw new IOException("Could not allocate a unique packet trace file.");
                }

                StreamWriter writer = null;
                try
                {
                    writer = new StreamWriter(stream, new UTF8Encoding(false), 64 * 1024)
                    {
                        AutoFlush = false
                    };
                    writer.WriteLine("# KartRider server full packet trace");
                    writer.WriteLine(
                        $"# started={DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture)} " +
                        $"pid={processId} detailQueueCapacity={detailQueueCapacity} " +
                        $"uiQueueCapacity={uiQueueCapacity}");
                    writer.WriteLine(
                        "# HEX is the complete decoded logical packet; WIRE is the complete datagram/read buffer when available.");
                    writer.WriteLine(
                        "# Packet details and UI summaries use separate bounded background queues.");
                    writer.WriteLine(
                        "# WARNING: authentication fields and other sensitive packet contents are recorded verbatim.");
                    writer.Flush();

                    TraceWriterWorker worker = new TraceWriterWorker(
                        selectedPath,
                        writer,
                        failureReporter,
                        detailQueueCapacity,
                        uiQueueCapacity);
                    worker._uiThread.Start();
                    worker._writerThread.Start();
                    return worker;
                }
                catch
                {
                    try
                    {
                        writer?.Dispose();
                    }
                    finally
                    {
                        if (writer == null)
                        {
                            stream.Dispose();
                        }
                    }
                    throw;
                }
            }

            public void LogPacket(
                string transport,
                string direction,
                EndPoint localEndPoint,
                EndPoint remoteEndPoint,
                string identity,
                byte[] packet,
                int hashOffset,
                string details,
                byte[] wirePacket)
            {
                TraceSummary summary = null;
                bool reservationActive = false;
                try
                {
                    byte[] payload = packet ?? Array.Empty<byte>();
                    bool hasHash = hashOffset >= 0 &&
                                   payload.Length >= hashOffset + sizeof(uint);
                    uint hash = hasHash ? BitConverter.ToUInt32(payload, hashOffset) : 0;
                    summary = TraceSummary.Packet(
                        DateTimeOffset.Now,
                        transport,
                        direction,
                        localEndPoint?.ToString(),
                        remoteEndPoint?.ToString(),
                        identity,
                        details,
                        payload.Length,
                        hasHash,
                        hash);

                    ReservationResult reservation = ReserveOrDrop(summary);
                    if (reservation != ReservationResult.Reserved)
                    {
                        return;
                    }

                    reservationActive = true;
                    byte[] payloadSnapshot = Snapshot(payload);
                    byte[] wireSnapshot;
                    if (wirePacket == null)
                    {
                        wireSnapshot = null;
                    }
                    else if (ReferenceEquals(payload, wirePacket))
                    {
                        wireSnapshot = payloadSnapshot;
                    }
                    else
                    {
                        wireSnapshot = Snapshot(wirePacket);
                    }
                    Interlocked.Increment(ref _packetSnapshots);

                    CommitReservation(
                        summary,
                        new TraceRecord(summary, payloadSnapshot, wireSnapshot));
                    reservationActive = false;
                }
                catch (Exception ex)
                {
                    if (reservationActive)
                    {
                        AbortReservation(summary);
                    }
                    ReportProducerFailure(ex);
                }
            }

            public void LogEvent(
                string transport,
                string eventName,
                EndPoint localEndPoint,
                EndPoint remoteEndPoint,
                string identity,
                string details,
                bool publishToUi)
            {
                TraceSummary summary = null;
                bool reservationActive = false;
                try
                {
                    summary = TraceSummary.Event(
                        DateTimeOffset.Now,
                        transport,
                        eventName,
                        localEndPoint?.ToString(),
                        remoteEndPoint?.ToString(),
                        identity,
                        details);
                    ReservationResult reservation = ReserveOrDrop(summary, publishToUi);
                    if (reservation != ReservationResult.Reserved)
                    {
                        return;
                    }

                    reservationActive = true;
                    CommitReservation(
                        summary,
                        new TraceRecord(summary, null, null),
                        publishToUi);
                    reservationActive = false;
                }
                catch (Exception ex)
                {
                    if (reservationActive)
                    {
                        AbortReservation(summary, publishToUi);
                    }
                    ReportProducerFailure(ex);
                }
            }

            public bool TryEnqueueUiMessage(string message)
            {
                lock (_admissionSync)
                {
                    if (_queuesCompleted != 0)
                    {
                        return false;
                    }
                    return TryEnqueueUiLocked(TraceSummary.UiMessage(message));
                }
            }

            public TraceStopResult StopAndDrain()
            {
                if (Interlocked.Exchange(ref _stopped, 1) != 0)
                {
                    return new TraceStopResult(true, !_writerThread.IsAlive, !_uiThread.IsAlive);
                }

                lock (_admissionSync)
                {
                    _accepting = 0;
                }

                bool admissionsDrained = _admissionsDrained.Wait(
                    AdmissionDrainTimeoutMilliseconds);
                lock (_admissionSync)
                {
                    if (_activeAdmissions != 0)
                    {
                        _forceCompleting = 1;
                        _shutdownRejected += _activeAdmissions;
                        admissionsDrained = false;
                    }
                    CompleteQueuesLocked();
                }

                ResumeDetailWriterForTesting();

                bool writerStopped = Thread.CurrentThread == _writerThread ||
                                     _writerThread.Join(WriterDrainTimeoutMilliseconds);
                bool uiStopped = Thread.CurrentThread == _uiThread ||
                                 _uiThread.Join(UiDrainTimeoutMilliseconds);
                return new TraceStopResult(admissionsDrained, writerStopped, uiStopped);
            }

            public PacketTraceDiagnostics GetDiagnostics()
            {
                return new PacketTraceDiagnostics(
                    Interlocked.Read(ref _attempted),
                    Interlocked.Read(ref _enqueued),
                    Interlocked.Read(ref _written),
                    Interlocked.Read(ref _detailDropped),
                    Interlocked.Read(ref _uiDropped),
                    Interlocked.Read(ref _packetSnapshots),
                    Interlocked.Read(ref _shutdownRejected));
            }

            public bool PauseDetailWriterForTesting(int timeoutMilliseconds)
            {
                Volatile.Write(ref _testPauseRequested, 1);
                _testWriterPaused.Reset();
                _testWriterGate.Reset();
                return _testWriterPaused.Wait(timeoutMilliseconds);
            }

            public void ResumeDetailWriterForTesting()
            {
                Volatile.Write(ref _testPauseRequested, 0);
                _testWriterGate.Set();
            }

            public void InjectWriterFailureForTesting()
            {
                Volatile.Write(ref _testFailureRequested, 1);
                ResumeDetailWriterForTesting();
            }

            private ReservationResult ReserveOrDrop(
                TraceSummary summary,
                bool publishToUi = true)
            {
                lock (_admissionSync)
                {
                    if (_accepting == 0 || _forceCompleting != 0)
                    {
                        return ReservationResult.Disabled;
                    }

                    _attempted++;
                    if (!_detailSlots.Wait(0))
                    {
                        summary.Sequence = ++_sequence;
                        _detailDropped++;
                        if (publishToUi)
                        {
                            TryEnqueueUiLocked(summary);
                        }
                        return ReservationResult.Dropped;
                    }

                    if (_activeAdmissions == 0)
                    {
                        _admissionsDrained.Reset();
                    }
                    _activeAdmissions++;
                    return ReservationResult.Reserved;
                }
            }

            private void CommitReservation(
                TraceSummary summary,
                TraceRecord record,
                bool publishToUi = true)
            {
                lock (_admissionSync)
                {
                    if (_forceCompleting != 0 || _queuesCompleted != 0)
                    {
                        _detailSlots.Release();
                        FinishAdmissionLocked();
                        return;
                    }

                    summary.Sequence = ++_sequence;
                    if (_detailQueue.TryAdd(record))
                    {
                        _enqueued++;
                    }
                    else
                    {
                        _detailSlots.Release();
                        _detailDropped++;
                    }
                    if (publishToUi)
                    {
                        TryEnqueueUiLocked(summary);
                    }
                    FinishAdmissionLocked();
                }
            }

            private void AbortReservation(
                TraceSummary summary,
                bool publishToUi = true)
            {
                lock (_admissionSync)
                {
                    _detailSlots.Release();
                    if (_forceCompleting == 0 && _queuesCompleted == 0 && summary != null)
                    {
                        summary.Sequence = ++_sequence;
                        _detailDropped++;
                        if (publishToUi)
                        {
                            TryEnqueueUiLocked(summary);
                        }
                    }
                    FinishAdmissionLocked();
                }
            }

            private void FinishAdmissionLocked()
            {
                if (_activeAdmissions > 0)
                {
                    _activeAdmissions--;
                    if (_activeAdmissions == 0)
                    {
                        _admissionsDrained.Set();
                    }
                }
            }

            private bool TryEnqueueUiLocked(TraceSummary summary)
            {
                if (_queuesCompleted != 0)
                {
                    return false;
                }

                try
                {
                    if (_uiQueue.TryAdd(summary))
                    {
                        return true;
                    }
                }
                catch (InvalidOperationException)
                {
                    return false;
                }

                _uiDropped++;
                return false;
            }

            private void CompleteQueuesLocked()
            {
                if (_queuesCompleted != 0)
                {
                    return;
                }

                _queuesCompleted = 1;
                try
                {
                    _detailQueue.CompleteAdding();
                }
                catch (InvalidOperationException)
                {
                }

                try
                {
                    _uiQueue.CompleteAdding();
                }
                catch (InvalidOperationException)
                {
                }
            }

            private void WriteLoop()
            {
                Stopwatch flushTimer = Stopwatch.StartNew();
                int recordsSinceFlush = 0;

                try
                {
                    while (!_detailQueue.IsCompleted)
                    {
                        WaitForTestWriterGate();
                        if (Interlocked.Exchange(ref _testFailureRequested, 0) != 0)
                        {
                            throw new IOException("Injected packet trace writer failure.");
                        }

                        if (_detailQueue.TryTake(out TraceRecord record, 100))
                        {
                            _detailSlots.Release();
                            WriteDroppedNoticeIfNeeded();
                            WriteRecord(record);
                            Interlocked.Increment(ref _written);
                            recordsSinceFlush++;
                        }

                        if (recordsSinceFlush >= FlushRecordInterval ||
                            (recordsSinceFlush > 0 && flushTimer.Elapsed >= FlushTimeInterval))
                        {
                            _writer.Flush();
                            recordsSinceFlush = 0;
                            flushTimer.Restart();
                        }
                    }

                    WriteDroppedNoticeIfNeeded();
                    PacketTraceDiagnostics diagnostics = GetDiagnostics();
                    _writer.WriteLine(
                        $"# stopped={DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture)}" +
                        $" attempted={diagnostics.Attempted}" +
                        $" enqueued={diagnostics.Enqueued}" +
                        $" written={diagnostics.Written}" +
                        $" dropped={diagnostics.DetailDropped}" +
                        $" shutdownRejected={diagnostics.ShutdownRejected}" +
                        $" uiDropped={diagnostics.UiDropped}" +
                        $" packetSnapshots={diagnostics.PacketSnapshots}");
                    _writer.Flush();
                }
                catch (Exception ex)
                {
                    HandleWriterFailure(ex);
                }
                finally
                {
                    try
                    {
                        _writer.Dispose();
                    }
                    catch (Exception ex)
                    {
                        HandleWriterFailure(ex);
                    }

                    try
                    {
                        _detailQueue.Dispose();
                    }
                    catch
                    {
                    }
                }
            }

            private void UiLoop()
            {
                List<TraceSummary> batch = new List<TraceSummary>(UiBatchSize);
                StringBuilder output = new StringBuilder(16 * 1024);
                try
                {
                    while (!_uiQueue.IsCompleted)
                    {
                        batch.Clear();
                        if (_uiQueue.TryTake(out TraceSummary first, 100))
                        {
                            batch.Add(first);
                            while (batch.Count < UiBatchSize &&
                                   _uiQueue.TryTake(out TraceSummary next))
                            {
                                batch.Add(next);
                            }
                        }

                        FlushUiBatch(batch, output);
                    }

                    batch.Clear();
                    while (batch.Count < UiBatchSize &&
                           _uiQueue.TryTake(out TraceSummary remaining))
                    {
                        batch.Add(remaining);
                    }
                    FlushUiBatch(batch, output, forceDropNotice: true);
                }
                catch (Exception ex)
                {
                    QueueConsoleError(
                        $"[PACKET TRACE UI ERROR] Summary worker stopped for {Path}: {ex}");
                }
                finally
                {
                    try
                    {
                        _uiQueue.Dispose();
                    }
                    catch
                    {
                    }
                }
            }

            private void FlushUiBatch(
                List<TraceSummary> batch,
                StringBuilder output,
                bool forceDropNotice = false)
            {
                output.Clear();
                foreach (TraceSummary summary in batch)
                {
                    output.AppendLine(summary.GetLine());
                }

                long detailDropped = Interlocked.Read(ref _detailDropped);
                if (detailDropped > _uiReportedDetailDropped &&
                    (forceDropNotice || batch.Count > 0))
                {
                    output.Append("[PACKET TRACE] Detail queue full; dropped ")
                        .Append(detailDropped - _uiReportedDetailDropped)
                        .Append(" record(s), total=")
                        .Append(detailDropped)
                        .AppendLine(". UI summaries continued when capacity allowed.");
                    _uiReportedDetailDropped = detailDropped;
                }

                long uiDropped = Interlocked.Read(ref _uiDropped);
                if (uiDropped > _uiReportedDropped &&
                    (forceDropNotice || batch.Count > 0))
                {
                    output.Append("[PACKET TRACE] UI summary queue full; omitted ")
                        .Append(uiDropped - _uiReportedDropped)
                        .Append(" summary line(s), total=")
                        .Append(uiDropped)
                        .AppendLine(".");
                    _uiReportedDropped = uiDropped;
                }

                if (output.Length != 0)
                {
                    SafeConsoleWriteBatch(output.ToString());
                }
            }

            private void WriteRecord(TraceRecord record)
            {
                _writer.WriteLine(record.Summary.GetLine());
                if (!record.IsPacket)
                {
                    return;
                }

                WriteHexLine("HEX  | ", record.Payload);
                if (record.WirePacket != null)
                {
                    WriteHexLine("WIRE | ", record.WirePacket);
                }
            }

            private void WriteHexLine(string prefix, byte[] value)
            {
                _writer.Write(prefix);
                if (value == null || value.Length == 0)
                {
                    _writer.WriteLine();
                    return;
                }

                int bufferedCharacters = 0;
                for (int index = 0; index < value.Length; index++)
                {
                    if (index != 0)
                    {
                        _hexBuffer[bufferedCharacters++] = ' ';
                    }

                    byte current = value[index];
                    _hexBuffer[bufferedCharacters++] = HexDigits[current >> 4];
                    _hexBuffer[bufferedCharacters++] = HexDigits[current & 0x0F];

                    if (bufferedCharacters > _hexBuffer.Length - 3)
                    {
                        _writer.Write(_hexBuffer, 0, bufferedCharacters);
                        bufferedCharacters = 0;
                    }
                }

                if (bufferedCharacters != 0)
                {
                    _writer.Write(_hexBuffer, 0, bufferedCharacters);
                }
                _writer.WriteLine();
            }

            private void WriteDroppedNoticeIfNeeded()
            {
                long totalDropped = Interlocked.Read(ref _detailDropped);
                if (totalDropped <= _fileReportedDropped)
                {
                    return;
                }

                long delta = totalDropped - _fileReportedDropped;
                _writer.WriteLine(
                    $"{DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture)}" +
                    $" | TRACE | event=DROPPED | count={delta} | total={totalDropped}" +
                    " | details=detail queue full or record snapshot failed; network callbacks were not blocked on file I/O");
                _fileReportedDropped = totalDropped;
            }

            private void WaitForTestWriterGate()
            {
                if (Volatile.Read(ref _testPauseRequested) == 0)
                {
                    return;
                }

                _testWriterPaused.Set();
                _testWriterGate.Wait();
                _testWriterPaused.Reset();
            }

            private void HandleWriterFailure(Exception exception)
            {
                if (Interlocked.CompareExchange(ref _faultReported, 1, 0) != 0)
                {
                    return;
                }

                lock (_admissionSync)
                {
                    _accepting = 0;
                    if (_forceCompleting == 0 && _activeAdmissions != 0)
                    {
                        _shutdownRejected += _activeAdmissions;
                    }
                    _forceCompleting = 1;
                    CompleteQueuesLocked();
                }
                ResumeDetailWriterForTesting();
                _failureReporter(this, exception);
            }

            private void ReportProducerFailure(Exception exception)
            {
                if (Interlocked.CompareExchange(ref _producerFailureReported, 1, 0) != 0)
                {
                    return;
                }

                TryEnqueueUiMessage(
                    $"[PACKET TRACE ERROR] Producer-side trace snapshot failed: {exception}");
            }
        }
    }
}
