using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Cryptography.ECDSA;

namespace HiveAPI.CS
{
    public class CBase58 : Cryptography.ECDSA.Base58
    {
        public static byte[] DecodePrivateWif(string data)
        {
            if (data.All(Hexdigits.Contains))
                return Hex.HexToBytes(data);

            switch (data[0])
            {
                case '5':
                case '6':
                    return Base58CheckDecode(data);
                case 'K':
                case 'L':
                    return CutLastBytes(Base58CheckDecode(data), 1);
                default:
                    throw new NotImplementedException();
            }
        }

        public static string EncodePrivateWif(byte[] source)
        {
            return Base58CheckEncode(0x80, source);
        }

        public static byte[] DecodePublicWif(string publicKey, string prefix)
        {
            if (!publicKey.StartsWith(prefix))
                return Array.Empty<byte>();

            var buf = publicKey.Remove(0, prefix.Length);
            var s = Decode(buf);

            var checksum = BitConverter.ToInt32(s, s.Length - CheckSumSizeInBytes);
            var dec = RemoveCheckSum(s);
            var hash = Ripemd160Manager.GetHash(dec);
            var newChecksum = BitConverter.ToInt32(hash, 0);

            if (checksum != newChecksum)
                throw new ArithmeticException(nameof(checksum));

            return dec;
        }

        public static string EncodePublicWif(byte[] publicKey, string prefix)
        {
            var checksum = Ripemd160Manager.GetHash(publicKey);
            var s = AddLastBytes(publicKey, CheckSumSizeInBytes);
            Array.Copy(checksum, 0, s, s.Length - CheckSumSizeInBytes, CheckSumSizeInBytes);
            var pubdata = Encode(s);
            return prefix + pubdata;
        }

        public static byte[] Base58CheckDecode(string data)
        {
            var s = Decode(data);
            var dec = CutLastBytes(s, CheckSumSizeInBytes);

            var checksum = DoubleHash(dec);
            for (var i = 0; i < CheckSumSizeInBytes; i++)
            {
                if (checksum[i] != s[s.Length - CheckSumSizeInBytes + i])
                    throw new ArithmeticException("Invalide data");
            }

            return CutFirstBytes(dec, 1);
        }

        public static string Base58CheckEncode(byte version, byte[] data)
        {
            var s = AddFirstBytes(data, 1);
            s[0] = version;
            var checksum = DoubleHash(s);
            s = AddLastBytes(s, CheckSumSizeInBytes);
            Array.Copy(checksum, 0, s, s.Length - CheckSumSizeInBytes, CheckSumSizeInBytes);
            return Encode(s);
        }

        public static string GetSubWif(string name, string password, string role)
        {
            var seed = name + role + password;
            seed = Regex.Replace(seed, @"\s+", " ");
            var brainKey = Encoding.ASCII.GetBytes(seed);
            var hashSha256 = Sha256Manager.GetHash(brainKey);
            return EncodePrivateWif(hashSha256);
        }
    }
}
