using System;
using System.IO;
using System.Text;

namespace KartRider.ServerLauncher
{
    internal sealed class UiLogTextWriter : TextWriter
    {
        private readonly object gate = new object();
        private readonly StringBuilder pending = new StringBuilder();
        private readonly Action<string> writeLine;
        private readonly string prefix;

        public UiLogTextWriter(Action<string> writeLine, string prefix = "")
        {
            this.writeLine = writeLine ?? throw new ArgumentNullException(nameof(writeLine));
            this.prefix = prefix;
        }

        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value)
        {
            string completeLine = null;
            lock (gate)
            {
                if (value == '\n')
                {
                    completeLine = pending.ToString();
                    pending.Clear();
                }
                else if (value != '\r')
                {
                    pending.Append(value);
                }
            }

            if (completeLine != null)
            {
                writeLine(prefix + completeLine);
            }
        }

        public override void Write(string value)
        {
            if (value == null)
            {
                return;
            }

            foreach (char character in value)
            {
                Write(character);
            }
        }

        public override void WriteLine(string value)
        {
            Write(value);
            Write('\n');
        }
    }

    internal sealed class TeeTextWriter : TextWriter
    {
        private readonly TextWriter first;
        private readonly TextWriter second;

        public TeeTextWriter(TextWriter first, TextWriter second)
        {
            this.first = first ?? throw new ArgumentNullException(nameof(first));
            this.second = second ?? throw new ArgumentNullException(nameof(second));
        }

        public override Encoding Encoding => first.Encoding;

        public override void Write(char value)
        {
            first.Write(value);
            second.Write(value);
        }

        public override void Write(string value)
        {
            first.Write(value);
            second.Write(value);
        }

        public override void WriteLine(string value)
        {
            first.WriteLine(value);
            second.WriteLine(value);
        }

        public override void Flush()
        {
            first.Flush();
            second.Flush();
        }
    }
}
