using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using KartRider.Common.Security;
using KartRider.IO.Packet;

namespace KartRider.Common.Data
{
    public class PINFile
    {
        public class PINHeader
        {
            public byte Unk1 { get; set; }

            public ushort LocaleID { get; set; }

            public ushort Unk2 { get; set; }

            public byte LocaleType { get; set; }

            public byte Unk3 { get; set; }

            public ushort MinorVersion { get; set; }

            public byte Unk4 { get; set; }

            public byte Unk5 { get; set; }

            public byte LoginType { get; set; }

            public string AESKey { get; set; }

            public string URL { get; set; }

            public string PatchURL { get; set; }

            public PINHeader()
            {
                AESKey = (URL = (PatchURL = string.Empty));
            }
        }

        public class IPEndPoint
        {
            public string IP { get; set; }

            public ushort Port { get; set; }

            public void Read(InPacket p)
            {
                IP = $"{p.ReadByte()}.{p.ReadByte()}.{p.ReadByte()}.{p.ReadByte()}";
                Port = p.ReadUShort();
            }

            public void Write(OutPacket p)
            {
                IPAddress ip = IPAddress.Parse(IP);
                p.WriteEndPoint(ip, Port);
            }

            public override string ToString()
            {
                return IP + ":" + Port;
            }
        }

        public class AuthMethod
        {
            public byte Index { get; set; }

            public string Name { get; set; }

            public List<IPEndPoint> LoginServers { get; set; }

            public BmlObject AccountConfig { get; set; }

            public BmlObject ExtraConfig { get; set; }

            public AuthMethod()
            {
                Name = string.Empty;
                LoginServers = new List<IPEndPoint>();
            }
        }

        private const uint RTTI_PINOBJECT = 284099454u;

        public PINHeader Header { get; set; }

        public List<AuthMethod> AuthMethods { get; set; }

        public List<BmlObject> BmlObjects { get; set; }

        public int NullsBeforeBmlObjects { get; private set; }

        public BmlObject StorageConfig { get; set; }

        public BmlObject ExtraConfig { get; set; }

        public KREncodedBlock.EncodeFlag EncodingFlags { get; private set; }

        public uint? KartCryptoKey { get; private set; }

        public PINFile()
        {
            Header = new PINHeader();
            AuthMethods = new List<AuthMethod>();
            BmlObjects = new List<BmlObject>();
            EncodingFlags = (KREncodedBlock.EncodeFlag)3;
            KartCryptoKey = 862557747u;
        }

        public PINFile(string path)
            : this()
        {
            if (!File.Exists(path))
            {
                throw new Exception("Unable to locate PIN file.");
            }

            byte[] input = File.ReadAllBytes(path);
            InPacket envelope = new InPacket(input);
            byte[] encoded = envelope.ReadBytes(envelope.ReadInt());
            CaptureEncoding(encoded);
            Read(new InPacket(KREncodedBlock.Decode(encoded)));
        }

        public byte[] GetEncryptedData()
        {
            return Encrypt(this);
        }

        private void CaptureEncoding(byte[] encoded)
        {
            if (encoded.Length < 6 || encoded[0] != 83)
                return;

            EncodingFlags = (KREncodedBlock.EncodeFlag)encoded[1];
            if ((EncodingFlags & KREncodedBlock.EncodeFlag.KartCrypto) != 0 && encoded.Length >= 10)
                KartCryptoKey = BitConverter.ToUInt32(encoded, 6);
            else
                KartCryptoKey = null;
        }

        private static byte[] Encrypt(PINFile pinFile)
        {
            OutPacket outPacket = new OutPacket();
            pinFile.Write(outPacket);
            OutPacket outPacket2 = new OutPacket();
            byte[] array = KREncodedBlock.Encode(outPacket.ToArray(), pinFile.EncodingFlags, pinFile.KartCryptoKey);
            outPacket2.WriteInt(array.Length);
            outPacket2.WriteBytes(array);
            return outPacket2.ToArray();
        }

        private void Read(InPacket p)
        {
            if (p.ReadUInt() != 284099454)
            {
                throw new InvalidDataException("This is not a valid PIN file.");
            }

            Header = new PINHeader();
            Header.Unk1 = p.ReadByte();
            Header.LocaleID = p.ReadUShort();
            Header.Unk2 = p.ReadUShort();
            Header.LocaleType = p.ReadByte();
            Header.Unk3 = p.ReadByte();
            Header.MinorVersion = p.ReadUShort();
            Header.Unk4 = p.ReadByte();
            Header.Unk5 = p.ReadByte();

            if (Header.Unk1 == 1)
            {
                // Early PIN files store one URL and a flat endpoint list. The
                // 2005 client itself promotes this representation to its v2
                // PinObject in memory, using the "Default" authentication
                // method and login type 2.
                Header.LoginType = 2;
                Header.AESKey = string.Empty;
                Header.URL = p.ReadString();
                Header.PatchURL = string.Empty;

                AuthMethod legacyAuthMethod = new AuthMethod
                {
                    Index = 1,
                    Name = "Default"
                };
                for (int endpointCount = p.ReadInt(); endpointCount > 0; endpointCount--)
                {
                    IPEndPoint endpoint = new IPEndPoint();
                    endpoint.Read(p);
                    legacyAuthMethod.LoginServers.Add(endpoint);
                }

                AuthMethods = new List<AuthMethod> { legacyAuthMethod };
                BmlObjects = new List<BmlObject>();
                StorageConfig = null;
                ExtraConfig = null;
                return;
            }

            Header.LoginType = p.ReadByte();
            Header.AESKey = p.ReadString();
            Header.URL = p.ReadString();
            Header.PatchURL = p.ReadString();
            AuthMethods = new List<AuthMethod>();
            for (int num = p.ReadInt(); num > 0; num--)
            {
                AuthMethod authMethod = new AuthMethod
                {
                    Index = p.ReadByte(),
                    Name = p.ReadString(),
                    AccountConfig = ReadBML(p),
                    LoginServers = new List<IPEndPoint>()
                };
                for (int num2 = p.ReadInt(); num2 > 0; num2--)
                {
                    IPEndPoint iPEndPoint = new IPEndPoint();
                    iPEndPoint.Read(p);
                    authMethod.LoginServers.Add(iPEndPoint);
                }

                authMethod.ExtraConfig = ReadBML(p);
                AuthMethods.Add(authMethod);
            }

            BmlObjects = new List<BmlObject>();
            StorageConfig = p.Available > 0 ? ReadBML(p) : null;
            ExtraConfig = p.Available > 0 ? ReadBML(p) : null;
            if (StorageConfig != null)
                BmlObjects.Add(StorageConfig);
            else
                NullsBeforeBmlObjects++;
            if (ExtraConfig != null)
                BmlObjects.Add(ExtraConfig);
        }

        private void Write(OutPacket p)
        {
            p.WriteUInt(284099454u);
            p.WriteByte(Header.Unk1);
            p.WriteUShort(Header.LocaleID);
            p.WriteUShort(Header.Unk2);
            p.WriteByte(Header.LocaleType);
            p.WriteByte(Header.Unk3);
            p.WriteUShort(Header.MinorVersion);
            p.WriteByte(Header.Unk4);
            p.WriteByte(Header.Unk5);

            if (Header.Unk1 == 1)
            {
                p.WriteString(Header.URL);
                int endpointCount = 0;
                foreach (AuthMethod authMethod in AuthMethods)
                    endpointCount += authMethod.LoginServers?.Count ?? 0;
                p.WriteInt(endpointCount);
                foreach (AuthMethod authMethod in AuthMethods)
                {
                    if (authMethod.LoginServers == null)
                        continue;
                    foreach (IPEndPoint loginServer in authMethod.LoginServers)
                        loginServer.Write(p);
                }
                return;
            }

            p.WriteByte(Header.LoginType);
            p.WriteString(Header.AESKey);
            p.WriteString(Header.URL);
            p.WriteString(Header.PatchURL);
            p.WriteInt(AuthMethods.Count);
            foreach (AuthMethod authMethod in AuthMethods)
            {
                p.WriteByte(authMethod.Index);
                p.WriteString(authMethod.Name);
                WriteBML(p, authMethod.AccountConfig);
                p.WriteInt(authMethod.LoginServers.Count);
                foreach (IPEndPoint loginServer in authMethod.LoginServers)
                {
                    loginServer.Write(p);
                }

                WriteBML(p, authMethod.ExtraConfig);
            }

            WriteBML(p, ResolveTopLevelConfig(StorageConfig, "storage"));
            WriteBML(p, ResolveTopLevelConfig(ExtraConfig, "extra"));
        }

        private BmlObject ResolveTopLevelConfig(BmlObject configured, string name)
        {
            if (configured != null)
                return configured;
            if (BmlObjects == null)
                return null;

            foreach (BmlObject bmlObject in BmlObjects)
            {
                if (string.Equals(bmlObject?.Name, name, StringComparison.OrdinalIgnoreCase))
                    return bmlObject;
            }
            return null;
        }

        private BmlObject ReadBML(InPacket p)
        {
            return (!p.ReadBool()) ? null : new BmlObject(p);
        }

        private void WriteBML(OutPacket p, BmlObject bml)
        {
            p.WriteBool(bml != null);
            bml?.Save(p);
        }
    }
}
