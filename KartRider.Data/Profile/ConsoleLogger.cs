using System;
using System.IO;
using System.Text;

namespace LoggerLibrary
{
    public sealed class CachedConsoleWriter : TextWriter
    {
        private const int MaximumCacheCharacters = 1_000_000;
        private const int RetainedCacheCharacters = 500_000;

        public static CachedConsoleWriter cachedWriter;

        private readonly TextWriter originalOut;
        private readonly StringBuilder cache = new StringBuilder();
        private readonly object syncRoot = new object();

        public CachedConsoleWriter(TextWriter originalOut)
        {
            this.originalOut = originalOut ?? throw new ArgumentNullException(nameof(originalOut));
        }

        public string Cache
        {
            get
            {
                lock (syncRoot)
                {
                    return cache.ToString();
                }
            }
        }

        public override Encoding Encoding => originalOut.Encoding;

        public void ClearCache()
        {
            lock (syncRoot)
            {
                cache.Clear();
            }
        }

        public override void Write(char value)
        {
            lock (syncRoot)
            {
                originalOut.Write(value);
                cache.Append(value);
                TrimCacheIfNeeded();
            }
        }

        public override void Write(string value)
        {
            lock (syncRoot)
            {
                originalOut.Write(value);
                cache.Append(value);
                TrimCacheIfNeeded();
            }
        }

        public override void WriteLine(string value)
        {
            lock (syncRoot)
            {
                originalOut.WriteLine(value);
                cache.AppendLine(value);
                TrimCacheIfNeeded();
            }
        }

        public override void Flush()
        {
            lock (syncRoot)
            {
                originalOut.Flush();
            }
        }

        public static void SaveToFile()
        {
            try
            {
                if (cachedWriter == null)
                {
                    return;
                }

                string fileName = $"{DateTime.Now:yyyyMMddHHmmss}.log";
                File.WriteAllText(fileName, cachedWriter.Cache);
                Console.WriteLine($"콘솔 로그 저장: {fileName}");
            }
            catch (Exception exception)
            {
                Console.WriteLine($"콘솔 로그 저장 실패: {exception.Message}");
            }
        }

        private void TrimCacheIfNeeded()
        {
            if (cache.Length <= MaximumCacheCharacters)
                return;

            cache.Remove(0, cache.Length - RetainedCacheCharacters);
        }
    }
}
