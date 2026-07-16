using KartRider_PacketName;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;

namespace KartRider.Common.Network
{
    /// <summary>
    /// Records complete decoded packet payloads without allowing trace failures to
    /// interfere with the network path.
    /// </summary>
    public static class PacketTrace
    {
        private static readonly object SyncRoot = new object();

        private static StreamWriter _writer;
        private static string _tracePath = "";
        private static bool _processExitHooked;
        private static bool _failureReported;
        private static long _sequence;
        private static DateTime _nextStartAttemptUtc = DateTime.MinValue;

        public static string TracePath
        {
            get
            {
                lock (SyncRoot)
                {
                    return _tracePath;
                }
            }
        }

        public static void Start(string baseDirectory = null)
        {
            string startedPath = "";
            lock (SyncRoot)
            {
                if (_writer == null)
                {
                    _nextStartAttemptUtc = DateTime.MinValue;
                }
                if (EnsureStartedLocked(baseDirectory))
                {
                    startedPath = _tracePath;
                }
            }

            if (!string.IsNullOrEmpty(startedPath))
            {
                SafeConsoleWrite($"[PACKET TRACE] Full RX/TX log: {startedPath}");
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
            try
            {
                byte[] payload = packet ?? Array.Empty<byte>();
                bool hasHash = hashOffset >= 0 && payload.Length >= hashOffset + sizeof(uint);
                uint hash = hasHash ? BitConverter.ToUInt32(payload, hashOffset) : 0;
                string packetName = hasHash
                    ? Enum.GetName(typeof(PacketName), (PacketName)hash) ?? "UNKNOWN"
                    : "MALFORMED";
                string hashText = hasHash
                    ? $"0x{hash:X8}"
                    : "-";
                string timestamp = DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture);
                string summary;

                lock (SyncRoot)
                {
                    long sequence = ++_sequence;
                    summary =
                        $"{timestamp} | PACKET | seq={sequence:D8} | transport={Normalize(transport)}" +
                        $" | dir={Normalize(direction)} | local={Normalize(localEndPoint?.ToString())}" +
                        $" | remote={Normalize(remoteEndPoint?.ToString())} | id={Normalize(identity)}" +
                        $" | len={payload.Length} | hash={hashText} | name={packetName}" +
                        $" | details={Normalize(details)}";

                    if (EnsureStartedLocked(null))
                    {
                        try
                        {
                            _writer.WriteLine(summary);
                            _writer.WriteLine($"HEX  | {ToHex(payload)}");
                            if (wirePacket != null)
                            {
                                _writer.WriteLine($"WIRE | {ToHex(wirePacket)}");
                            }
                            _writer.Flush();
                        }
                        catch (Exception ex)
                        {
                            DisableWriterLocked(ex);
                        }
                    }
                }

                SafeConsoleWrite(summary);
                SafeConsoleWrite($"HEX  | {ToHex(payload)}");
                if (wirePacket != null)
                {
                    SafeConsoleWrite($"WIRE | {ToHex(wirePacket)}");
                }

            }
            catch (Exception ex)
            {
                ReportFailureOnce(ex);
            }
        }

        public static void LogEvent(
            string transport,
            string eventName,
            EndPoint localEndPoint,
            EndPoint remoteEndPoint,
            string identity,
            string details)
        {
            try
            {
                string timestamp = DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture);
                string line;

                lock (SyncRoot)
                {
                    long sequence = ++_sequence;
                    line =
                        $"{timestamp} | EVENT | seq={sequence:D8} | transport={Normalize(transport)}" +
                        $" | event={Normalize(eventName)} | local={Normalize(localEndPoint?.ToString())}" +
                        $" | remote={Normalize(remoteEndPoint?.ToString())} | id={Normalize(identity)}" +
                        $" | details={Normalize(details)}";

                    if (EnsureStartedLocked(null))
                    {
                        try
                        {
                            _writer.WriteLine(line);
                            _writer.Flush();
                        }
                        catch (Exception ex)
                        {
                            DisableWriterLocked(ex);
                        }
                    }
                }

                SafeConsoleWrite(line);

            }
            catch (Exception ex)
            {
                ReportFailureOnce(ex);
            }
        }

        private static bool EnsureStartedLocked(string baseDirectory)
        {
            if (_writer != null)
            {
                return true;
            }

            DateTime now = DateTime.UtcNow;
            if (now < _nextStartAttemptUtc)
            {
                return false;
            }

            _nextStartAttemptUtc = now.AddSeconds(30);
            try
            {
                string root = string.IsNullOrWhiteSpace(baseDirectory)
                    ? AppDomain.CurrentDomain.BaseDirectory
                    : Path.GetFullPath(baseDirectory);
                string logDirectory = Path.Combine(root, "logs");
                Directory.CreateDirectory(logDirectory);

                int processId = Process.GetCurrentProcess().Id;
                string stem = $"packet-trace_{DateTime.Now:yyyyMMdd_HHmmss_fff}_{processId}";
                FileStream stream = null;
                string selectedPath = "";
                for (int attempt = 0; attempt < 100; attempt++)
                {
                    selectedPath = Path.Combine(
                        logDirectory,
                        attempt == 0 ? stem + ".log" : $"{stem}_{attempt}.log");
                    try
                    {
                        stream = new FileStream(
                            selectedPath,
                            FileMode.CreateNew,
                            FileAccess.Write,
                            FileShare.ReadWrite,
                            4096,
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

                _tracePath = selectedPath;
                _writer = new StreamWriter(stream, new UTF8Encoding(false), 4096)
                {
                    AutoFlush = true
                };
                _writer.WriteLine("# KartRider server full packet trace");
                _writer.WriteLine($"# started={DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture)} pid={processId}");
                _writer.WriteLine("# HEX is the complete decoded logical packet; WIRE is the complete datagram/read buffer when available.");
                _writer.WriteLine("# WARNING: authentication fields and other sensitive packet contents are recorded verbatim.");
                _writer.Flush();

                if (!_processExitHooked)
                {
                    _processExitHooked = true;
                    AppDomain.CurrentDomain.ProcessExit += delegate { Close(); };
                }

                return true;
            }
            catch (Exception ex)
            {
                ReportFailureOnce(ex);
                return false;
            }
        }

        private static void Close()
        {
            lock (SyncRoot)
            {
                try
                {
                    _writer?.Flush();
                    _writer?.Dispose();
                }
                catch
                {
                }
                finally
                {
                    _writer = null;
                }
            }
        }

        private static void DisableWriterLocked(Exception ex)
        {
            try
            {
                _writer?.Dispose();
            }
            catch
            {
            }
            finally
            {
                _writer = null;
                _nextStartAttemptUtc = DateTime.UtcNow.AddSeconds(30);
            }

            ReportFailureOnce(ex);
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

        private static string ToHex(byte[] value)
        {
            return value == null || value.Length == 0
                ? ""
                : BitConverter.ToString(value).Replace("-", " ");
        }

        private static void ReportFailureOnce(Exception ex)
        {
            if (_failureReported)
            {
                return;
            }

            _failureReported = true;
            try
            {
                Console.Error.WriteLine($"[PACKET TRACE ERROR] {ex}");
            }
            catch
            {
            }
        }

        private static void SafeConsoleWrite(string message)
        {
            try
            {
                Console.WriteLine(message);
            }
            catch
            {
            }
        }
    }
}
