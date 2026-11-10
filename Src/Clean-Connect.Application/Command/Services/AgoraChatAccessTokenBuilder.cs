using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Clean_Connect.Application.Command.Services
{
    internal static class AgoraChatAccessTokenBuilder
    {
        private const string Version = "007";
        private const ushort ServiceTypeChat = 5;
        private const ushort PrivilegeChatUser = 1;
        private const ushort PrivilegeChatApp = 2;
        private static readonly Regex AgoraCredentialPattern = new("^[0-9a-fA-F]{32}$", RegexOptions.Compiled);

        public static string BuildUserToken(string appId, string appCertificate, string userId, uint ttlSeconds)
        {
            return BuildToken(appId, appCertificate, userId, PrivilegeChatUser, ttlSeconds);
        }

        public static string BuildAppToken(string appId, string appCertificate, uint ttlSeconds)
        {
            return BuildToken(appId, appCertificate, string.Empty, PrivilegeChatApp, ttlSeconds);
        }

        private static string BuildToken(
            string appId,
            string appCertificate,
            string chatUserId,
            ushort privilege,
            uint ttlSeconds)
        {
            if (!AgoraCredentialPattern.IsMatch(appId) || !AgoraCredentialPattern.IsMatch(appCertificate))
            {
                throw new InvalidOperationException("Agora Chat App ID and App Certificate must be configured as 32-character hexadecimal values.");
            }

            var issueTimestamp = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var salt = GenerateSalt();
            var signing = HmacSha256(BitConverter.GetBytes(issueTimestamp), Encoding.UTF8.GetBytes(appCertificate));
            signing = HmacSha256(BitConverter.GetBytes(salt), signing);

            var signingInfo = new TokenByteWriter()
                .WriteBytes(Encoding.UTF8.GetBytes(appId))
                .WriteUInt32(issueTimestamp)
                .WriteUInt32(ttlSeconds)
                .WriteUInt32(salt)
                .WriteUInt16(1)
                .WriteUInt16(ServiceTypeChat)
                .WriteUInt16(1)
                .WriteUInt16(privilege)
                .WriteUInt32(ttlSeconds)
                .WriteBytes(Encoding.UTF8.GetBytes(chatUserId))
                .ToArray();

            var signature = HmacSha256(signing, signingInfo);
            var tokenContent = new TokenByteWriter()
                .WriteBytes(signature)
                .WriteRaw(signingInfo)
                .ToArray();

            return Version + Convert.ToBase64String(Compress(tokenContent));
        }

        private static uint GenerateSalt()
        {
            Span<byte> bytes = stackalloc byte[4];
            RandomNumberGenerator.Fill(bytes);
            return BitConverter.ToUInt32(bytes);
        }

        private static byte[] HmacSha256(byte[] key, byte[] message)
        {
            using var hmac = new HMACSHA256(key);
            return hmac.ComputeHash(message);
        }

        private static byte[] Compress(byte[] data)
        {
            using var output = new MemoryStream();
            using (var zlib = new ZLibStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
            {
                zlib.Write(data, 0, data.Length);
            }

            return output.ToArray();
        }

        private sealed class TokenByteWriter
        {
            private readonly MemoryStream _stream = new();

            public TokenByteWriter WriteUInt16(ushort value)
            {
                _stream.WriteByte((byte)(value & 0xff));
                _stream.WriteByte((byte)((value >> 8) & 0xff));
                return this;
            }

            public TokenByteWriter WriteUInt32(uint value)
            {
                _stream.WriteByte((byte)(value & 0xff));
                _stream.WriteByte((byte)((value >> 8) & 0xff));
                _stream.WriteByte((byte)((value >> 16) & 0xff));
                _stream.WriteByte((byte)((value >> 24) & 0xff));
                return this;
            }

            public TokenByteWriter WriteBytes(byte[] value)
            {
                if (value.Length > ushort.MaxValue)
                {
                    throw new InvalidOperationException("Agora token field is too large.");
                }

                WriteUInt16((ushort)value.Length);
                _stream.Write(value, 0, value.Length);
                return this;
            }

            public TokenByteWriter WriteRaw(byte[] value)
            {
                _stream.Write(value, 0, value.Length);
                return this;
            }

            public byte[] ToArray() => _stream.ToArray();
        }
    }
}
