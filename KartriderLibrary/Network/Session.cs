using KartLibrary.IO;
using KartRider.Common.Security;
using KartRider.Common.Utilities;
using KartRider.IO.Packet;
using KartRider_PacketName;
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;

namespace KartRider.Common.Network
{
    public abstract class Session
    {
        private readonly System.Net.Sockets.Socket _socket;

        public event Action<Session> OnDisconnected;

        private const int DEFAULT_SIZE = 65536;

        private byte[] mBuffer = new byte[65536];

        private byte[] mSharedBuffer = new byte[65536];

        private int mCursor = 0;

        public int mDisconnected = 0;

        private LockFreeQueue<ByteArraySegment> mSendSegments = new LockFreeQueue<ByteArraySegment>();

        private int mSending = 0;

        private readonly object mSendLock = new object();

        private int mInitialHandshakeQueued = 0;

        private string _label = "";

        public string Label
        {
            get
            {
                return this._label;
            }
        }

        public uint RIV
        {
            get;
            set;
        }

        public uint SIV
        {
            get;
            set;
        }

        public string Nickname
        {
            get;
            set;
        }

        private SocketAsyncEventArgs mReadEventArgs
        {
            get;
            set;
        }

        private SocketAsyncEventArgs mWriteEventArgs
        {
            get;
            set;
        }

        public System.Net.Sockets.Socket Socket
        {
            get
            {
                return this._socket;
            }
        }

        public Session(System.Net.Sockets.Socket socket)
        {
            this._socket = socket;
            this.mWriteEventArgs = new SocketAsyncEventArgs()
            {
                DisconnectReuseSocket = false
            };
            this.mWriteEventArgs.Completed += new EventHandler<SocketAsyncEventArgs>((object s, SocketAsyncEventArgs a) => this.EndSend(a));
            this.WaitForData();
        }

        public void Append(byte[] pBuffer)
        {
            this.Append(pBuffer, 0, (int)pBuffer.Length);
        }

        public void Append(byte[] pBuffer, int pStart, int pLength)
        {
            try
            {
                if ((int)this.mBuffer.Length - this.mCursor < pLength)
                {
                    int length = (int)this.mBuffer.Length * 2;
                    while (length < this.mCursor + pLength)
                    {
                        length *= 2;
                    }
                    Array.Resize<byte>(ref this.mBuffer, length);
                }
                Buffer.BlockCopy(pBuffer, pStart, this.mBuffer, this.mCursor, pLength);
                this.mCursor += pLength;
            }
            catch
            {
            }
        }

        public void BeginReceive()
        {
            if ((this.mDisconnected != 0 ? false : this._socket.Connected))
            {
                try
                {
                    this._socket.BeginReceive(this.mSharedBuffer, 0, 65536, SocketFlags.None, new AsyncCallback(this.EndReceive), this._socket);
                }
                catch
                {
                    this.Disconnect();
                }
            }
            else
            {
                this.Disconnect();
            }
        }

        private void BeginSend()
        {
            ByteArraySegment next = this.mSendSegments.Next;
            try
            {
                if (next == null)
                {
                    this.mSendSegments.Dequeue();
                }
                else if ((int)next.Buffer.Length >= next.Length)
                {
                    if (!next.Prepared)
                    {
                        byte[] logicalPacket = next.Buffer;
                        bool encrypted = next.Encrypted && this.SIV != 0;
                        byte[] framedPacket = new byte[logicalPacket.Length + (encrypted ? 8 : 4)];
                        if (encrypted)
                        {
                            uint sendIv = this.SIV;
                            uint checksum = KRPacketCrypto.HashEncrypt(
                                logicalPacket,
                                (uint)logicalPacket.Length,
                                sendIv);
                            Buffer.BlockCopy(
                                BitConverter.GetBytes((int)((ulong)sendIv ^ (ulong)(logicalPacket.Length + 4) ^ 4164199944u)),
                                0,
                                framedPacket,
                                0,
                                4);
                            Buffer.BlockCopy(
                                BitConverter.GetBytes(sendIv ^ checksum ^ 3388492432u),
                                0,
                                framedPacket,
                                framedPacket.Length - 4,
                                4);
                            this.SIV += 21446425;
                            if (this.SIV == 0)
                            {
                                this.SIV = 1;
                            }
                        }
                        else
                        {
                            Buffer.BlockCopy(
                                BitConverter.GetBytes(logicalPacket.Length),
                                0,
                                framedPacket,
                                0,
                                4);
                        }

                        Buffer.BlockCopy(logicalPacket, 0, framedPacket, 4, logicalPacket.Length);
                        next.Prepare(framedPacket);
                    }

                    this.mWriteEventArgs.SetBuffer(next.Buffer, next.Start, next.Length);
                    PacketTrace.LogEvent(
                        "TCP",
                        "TX-BEGIN",
                        this.GetLocalEndPoint(),
                        this.GetRemoteEndPoint(),
                        this.Nickname,
                        $"wireLength={next.Length}; encrypted={next.Encrypted}");
                    next = null;
                    try
                    {
                        if (!this.Socket.SendAsync(this.mWriteEventArgs))
                        {
                            this.EndSend(this.mWriteEventArgs);
                        }
                    }
                    catch (ObjectDisposedException objectDisposedException)
                    {
                        PacketTrace.LogEvent(
                            "TCP",
                            "TX-BEGIN-ERROR",
                            this.GetLocalEndPoint(),
                            this.GetRemoteEndPoint(),
                            this.Nickname,
                            objectDisposedException.ToString());
                        Console.WriteLine("[SOCKET ERR] {0}", objectDisposedException.ToString());
                        this.Disconnect();
                    }
                }
                else
                {
                    PacketTrace.LogEvent(
                        "TCP",
                        "TX-DROP",
                        this.GetLocalEndPoint(),
                        this.GetRemoteEndPoint(),
                        this.Nickname,
                        $"bufferLength={next.Buffer.Length}; segmentLength={next.Length}");
                    Console.WriteLine("[SOCKET ERR] Tried to send a packet that has a bufferlength value that is lower than the length: {0} {1}", (int)next.Buffer.Length, next.Length);
                    this.mSendSegments.Dequeue();
                }
            }
            catch (Exception exception)
            {
                PacketTrace.LogEvent(
                    "TCP",
                    "TX-BEGIN-ERROR",
                    this.GetLocalEndPoint(),
                    this.GetRemoteEndPoint(),
                    this.Nickname,
                    exception.ToString());
                Console.WriteLine("[SOCKET ERR] {0}", exception.ToString());
                this.Disconnect();
            }
        }

        public void Disconnect()
        {
            if (Interlocked.CompareExchange(ref this.mDisconnected, 1, 0) == 0)
            {
                PacketTrace.LogEvent(
                    "TCP",
                    "DISCONNECT",
                    this.GetLocalEndPoint(),
                    this.GetRemoteEndPoint(),
                    this.Nickname,
                    "session closed");
                try
                {
                    this.OnDisconnect();
                }
                catch (Exception exception)
                {
                    PacketTrace.LogEvent(
                        "TCP",
                        "DISCONNECT-CLEANUP-ERROR",
                        this.GetLocalEndPoint(),
                        this.GetRemoteEndPoint(),
                        this.Nickname,
                        exception.ToString());
                }

                try
                {
                    this.OnDisconnected?.Invoke(this);
                }
                catch (Exception exception)
                {
                    PacketTrace.LogEvent(
                        "TCP",
                        "DISCONNECT-EVENT-ERROR",
                        this.GetLocalEndPoint(),
                        this.GetRemoteEndPoint(),
                        this.Nickname,
                        exception.ToString());
                }
                finally
                {
                    try
                    {
                        this.Socket.Shutdown(SocketShutdown.Both);
                    }
                    catch
                    {
                    }
                    try
                    {
                        this.Socket.Close();
                    }
                    catch
                    {
                    }
                    try
                    {
                        this.mWriteEventArgs?.Dispose();
                    }
                    catch
                    {
                    }
                    this.mWriteEventArgs = null;
                }
            }
        }

        private void EndReceive(IAsyncResult ar)
        {
            if (this.mDisconnected == 0)
            {
                try
                {
                    int num = 0;
                    try
                    {
                        num = this._socket.EndReceive(ar);
                        if (num == 0)
                        {
                            // 读取到0字节，说明客户端主动断开
                            this.Disconnect();
                            return;
                        }
                    }
                    catch
                    {
                        this.Disconnect();
                        return;
                    }
                    if (num > 0)
                    {
                        this.Append(this.mSharedBuffer, 0, num);
                        while (true)
                        {
                            if (this.mCursor >= 4)
                            {
                                uint num1 = BitConverter.ToUInt32(this.mBuffer, 0);
                                if (this.RIV != 0)
                                {
                                    num1 = this.RIV ^ num1 ^ 4164199944;
                                }
                                if ((ulong)this.mCursor >= (ulong)(num1 + 4))
                                {
                                    byte[] wireFrame = new byte[num1 + 4];
                                    Buffer.BlockCopy(this.mBuffer, 0, wireFrame, 0, wireFrame.Length);
                                    byte[] numArray = new byte[num1 - 4];
                                    Buffer.BlockCopy(this.mBuffer, 4, numArray, 0, (int)(num1 - 4));
                                    uint receiveIv = this.RIV;
                                    bool checksumValid = true;
                                    if (this.RIV != 0)
                                    {
                                        uint receivedChecksum = BitConverter.ToUInt32(this.mBuffer, (int)num1);
                                        uint calculatedChecksum = KRPacketCrypto.HashDecrypt(numArray, num1 - 4, this.RIV);
                                        checksumValid =
                                            (this.RIV ^ receivedChecksum ^ 3388492432) == calculatedChecksum;
                                        if (!checksumValid)
                                        {
                                            Console.WriteLine("Different checksum while decrypting");
                                        }
                                        this.RIV += 21446425;
                                        if (this.RIV == 0)
                                        {
                                            this.RIV = 1;
                                        }
                                    }
                                    this.mCursor = (int)(this.mCursor - (num1 + 4));
                                    if (this.mCursor > 0)
                                    {
                                        Buffer.BlockCopy(this.mBuffer, (int)(num1 + 4), this.mBuffer, 0, this.mCursor);
                                    }
                                    PacketTrace.LogPacket(
                                        "TCP",
                                        "RX",
                                        this.GetLocalEndPoint(),
                                        this.GetRemoteEndPoint(),
                                        this.Nickname,
                                        numArray,
                                        0,
                                        $"frameLength={wireFrame.Length}; encrypted={receiveIv != 0}; iv=0x{receiveIv:X8}; checksum={(checksumValid ? "OK" : "BAD")}",
                                        wireFrame);
                                    if (receiveIv != 0 && !checksumValid)
                                    {
                                        PacketTrace.LogEvent(
                                            "TCP",
                                            "RX-DROP",
                                            this.GetLocalEndPoint(),
                                            this.GetRemoteEndPoint(),
                                            this.Nickname,
                                            "invalid encrypted frame checksum");
                                        this.Disconnect();
                                        return;
                                    }
                                    if (this.mDisconnected == 0)
                                    {
                                        if (!ClientManager.IsRegistered(this))
                                        {
                                            PacketTrace.LogEvent(
                                                "TCP",
                                                "RX-DROP",
                                                this.GetLocalEndPoint(),
                                                this.GetRemoteEndPoint(),
                                                this.Nickname,
                                                "session is not registered in ClientManager");
                                            this.Disconnect();
                                            return;
                                        }
                                        using (InPacket inPacket = new InPacket(numArray))
                                        {
                                            this.OnPacket(inPacket);
                                        }
                                    }
                                    else
                                    {
                                        return;
                                    }
                                }
                                else
                                {
                                    break;
                                }
                            }
                            else
                            {
                                break;
                            }
                        }
                        this.BeginReceive();
                    }
                    else
                    {
                        this.Disconnect();
                    }
                }
                catch (Exception exception)
                {
                    PacketTrace.LogEvent(
                        "TCP",
                        "RX-ERROR",
                        this.GetLocalEndPoint(),
                        this.GetRemoteEndPoint(),
                        this.Nickname,
                        exception.ToString());
                    Console.WriteLine(exception.ToString());
                    this.Disconnect();
                }
            }
        }

        private void EndSend(SocketAsyncEventArgs pArguments)
        {
            if (this.mDisconnected == 0)
            {
                try
                {
                    PacketTrace.LogEvent(
                        "TCP",
                        pArguments.BytesTransferred > 0 && pArguments.SocketError == SocketError.Success
                            ? "TX-COMPLETE"
                            : "TX-FAILED",
                        this.GetLocalEndPoint(),
                        this.GetRemoteEndPoint(),
                        this.Nickname,
                        $"bytesTransferred={pArguments.BytesTransferred}; socketError={pArguments.SocketError}");
                    if (pArguments.BytesTransferred > 0 &&
                        pArguments.SocketError == SocketError.Success)
                    {
                        bool sendNext;
                        lock (this.mSendLock)
                        {
                            ByteArraySegment current = this.mSendSegments.Next;
                            if (current == null)
                            {
                                throw new InvalidOperationException(
                                    "The send queue completed without an active segment.");
                            }

                            if (current.Advance(pArguments.BytesTransferred))
                            {
                                this.mSendSegments.Dequeue();
                            }

                            sendNext = this.mSendSegments.Next != null;
                            if (!sendNext)
                            {
                                // Enqueue and this transition use the same lock,
                                // so a producer cannot miss the pump hand-off.
                                this.mSending = 0;
                            }
                        }

                        if (sendNext)
                        {
                            this.BeginSend();
                        }
                    }
                    else
                    {
                        if (pArguments.SocketError != SocketError.Success)
                        {
                            Console.WriteLine("Send Error: {0}", pArguments.SocketError);
                        }
                        Console.WriteLine("Disconnected session 1 {0}", pArguments.SocketError.ToString());
                        this.Disconnect();
                    }
                }
                catch
                {
                    this.Disconnect();
                }
            }
        }

        public string GetRemoteAddress()
        {
            string str;
            try
            {
                str = ((IPEndPoint)this._socket.RemoteEndPoint).Address.ToString();
            }
            catch
            {
                str = "";
            }
            return str;
        }

        public IPEndPoint GetRemoteEndPoint()
        {
            IPEndPoint remoteEndPoint;
            try
            {
                remoteEndPoint = (IPEndPoint)this._socket.RemoteEndPoint;
            }
            catch
            {
                remoteEndPoint = new IPEndPoint((long)0, 0);
            }
            return remoteEndPoint;
        }

        public IPEndPoint GetLocalEndPoint()
        {
            IPEndPoint localEndPoint;
            try
            {
                localEndPoint = (IPEndPoint)this._socket.LocalEndPoint;
            }
            catch
            {
                localEndPoint = new IPEndPoint((long)0, 0);
            }
            return localEndPoint;
        }

        public abstract void OnDisconnect();

        public abstract void OnPacket(InPacket inPacket);

        public void Send(OutPacket pPacket)
        {
            try
            {
                byte[] packet = pPacket.ToArray();
                bool startSend = false;
                string dropReason = null;
                lock (this.mSendLock)
                {
                    if (this.mDisconnected != 0 || this._socket == null || !this._socket.Connected)
                    {
                        dropReason = "disconnected";
                    }
                    else if (this.mInitialHandshakeQueued == 0)
                    {
                        // An unauthenticated socket must never receive a global
                        // broadcast before PcFirstMessage.
                        dropReason = "initial-handshake-pending";
                    }
                    else
                    {
                        this.mSendSegments.Enqueue(new ByteArraySegment(packet, true));
                        if (this.mSending == 0)
                        {
                            this.mSending = 1;
                            startSend = true;
                        }
                    }
                }

                if (dropReason == null)
                {
                    PacketTrace.LogPacket(
                        "TCP",
                        "TX",
                        this.GetLocalEndPoint(),
                        this.GetRemoteEndPoint(),
                        this.Nickname,
                        packet,
                        0,
                        "stage=QUEUE");
                    if (startSend)
                        this.BeginSend();
                }
                else
                {
                    PacketTrace.LogPacket(
                        "TCP",
                        "TX",
                        this.GetLocalEndPoint(),
                        this.GetRemoteEndPoint(),
                        this.Nickname,
                        packet,
                        0,
                        $"stage=DROP; reason={dropReason}");
                }
            }
            catch (ObjectDisposedException objectDisposedException)
            {
                PacketTrace.LogEvent(
                    "TCP",
                    "TX-ERROR",
                    this.GetLocalEndPoint(),
                    this.GetRemoteEndPoint(),
                    this.Nickname,
                    objectDisposedException.ToString());
                Console.WriteLine("[SOCKET ERR] {0}", objectDisposedException.ToString());
                this.Disconnect();
            }
            catch (Exception exception)
            {
                PacketTrace.LogEvent(
                    "TCP",
                    "TX-ERROR",
                    this.GetLocalEndPoint(),
                    this.GetRemoteEndPoint(),
                    this.Nickname,
                    exception.ToString());
                Console.WriteLine("Disconnected session 11 {0}", exception.ToString());
                this.Disconnect();
            }
        }

        /// <summary>
        /// Queues the protocol's first server frame as plaintext while making
        /// the negotiated IV visible to the receive path before the frame can
        /// reach the client. This closes the fast-response race where the
        /// client's first encrypted request could otherwise be parsed with IV 0.
        /// </summary>
        public void SendInitialHandshake(OutPacket packet, uint iv)
        {
            if (packet == null)
            {
                throw new ArgumentNullException(nameof(packet));
            }
            if (iv == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(iv));
            }
            try
            {
                byte[] payload = packet.ToArray();
                bool startSend = false;
                lock (this.mSendLock)
                {
                    if (this.mInitialHandshakeQueued != 0)
                    {
                        throw new InvalidOperationException("The initial handshake was already queued.");
                    }
                    if (this.mDisconnected != 0 || this._socket == null || !this._socket.Connected)
                    {
                        throw new InvalidOperationException("The session disconnected before its initial handshake.");
                    }

                    // Set IVs and enqueue the explicitly plaintext first frame
                    // in one admission section. Normal Send calls cannot slip a
                    // frame ahead of it.
                    this.RIV = iv;
                    this.SIV = iv;
                    this.mInitialHandshakeQueued = 1;
                    this.mSendSegments.Enqueue(new ByteArraySegment(payload, false));
                    if (this.mSending == 0)
                    {
                        this.mSending = 1;
                        startSend = true;
                    }
                }

                PacketTrace.LogPacket(
                    "TCP",
                    "TX",
                    this.GetLocalEndPoint(),
                    this.GetRemoteEndPoint(),
                    this.Nickname,
                    payload,
                    0,
                    "stage=QUEUE; initial=true; encrypted=false");
                if (startSend)
                    this.BeginSend();
            }
            catch
            {
                this.Disconnect();
                throw;
            }
        }

        public void SendRaw(byte[] pBuffer)
        {
            try
            {
                bool startSend = false;
                string dropReason = null;
                lock (this.mSendLock)
                {
                    if (this.mDisconnected != 0 || this._socket == null || !this._socket.Connected)
                    {
                        dropReason = "disconnected";
                    }
                    else if (this.mInitialHandshakeQueued == 0)
                    {
                        dropReason = "initial-handshake-pending";
                    }
                    else
                    {
                        this.mSendSegments.Enqueue(new ByteArraySegment(pBuffer, false));
                        if (this.mSending == 0)
                        {
                            this.mSending = 1;
                            startSend = true;
                        }
                    }
                }

                if (dropReason == null)
                {
                    PacketTrace.LogPacket(
                        "TCP",
                        "TX",
                        this.GetLocalEndPoint(),
                        this.GetRemoteEndPoint(),
                        this.Nickname,
                        pBuffer,
                        0,
                        "stage=QUEUE; raw=true");
                    if (startSend)
                        this.BeginSend();
                }
                else
                {
                    PacketTrace.LogPacket(
                        "TCP",
                        "TX",
                        this.GetLocalEndPoint(),
                        this.GetRemoteEndPoint(),
                        this.Nickname,
                        pBuffer,
                        0,
                        $"stage=DROP; reason={dropReason}; raw=true");
                }
            }
            catch (ObjectDisposedException objectDisposedException)
            {
                PacketTrace.LogEvent(
                    "TCP",
                    "TX-ERROR",
                    this.GetLocalEndPoint(),
                    this.GetRemoteEndPoint(),
                    this.Nickname,
                    objectDisposedException.ToString());
                Console.WriteLine("[SOCKET ERR] {0}", objectDisposedException.ToString());
                this.Disconnect();
            }
            catch (Exception exception)
            {
                PacketTrace.LogEvent(
                    "TCP",
                    "TX-ERROR",
                    this.GetLocalEndPoint(),
                    this.GetRemoteEndPoint(),
                    this.Nickname,
                    exception.ToString());
                Console.WriteLine("Disconnected session 12 {0}", exception.ToString());
                this.Disconnect();
            }
        }

        public void WaitForData()
        {
            this.BeginReceive();
        }
    }
}
