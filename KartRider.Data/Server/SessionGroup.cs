using System;
using System.Net.Sockets;

namespace KartRider
{
    public class SessionGroup
    {
        public object m_lock = new object();

        public ClientSession Client
        {
            get;
            set;
        }

        public string ClientId { get; internal set; } = string.Empty;

        public long IdentityGeneration { get; internal set; }

        // P5136 opens a new TCP session for every concrete channel. Keep the
        // selected catalog group on the generation that completed migration so
        // room discovery cannot leak rooms from another mode family.
        public byte P5136ChannelGameType { get; internal set; }

        public ushort P5136ChannelId { get; internal set; }

        public int TimeAttackStartTicks = 0;
        public int SendPlaneCount = 6;
        public int TotalSendPlaneCount = 6;
        public byte PlaneCheck1 = 0;

        public static uint LucciMax = 2000000;

        public SessionGroup(Socket clientSocket, Socket serverSocket)
        {
            this.Client = new ClientSession(this, clientSocket);
        }
    }
}
