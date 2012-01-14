﻿using System;

namespace PcapDotNet.Packets.Dns
{
    /// <summary>
    /// RFC 3658.
    /// <pre>
    /// 0 Or more of:
    /// +-----+---------+-----------+-------------+
    /// | bit | 0-15    | 16-23     | 24-31       |
    /// +-----+---------+-----------+-------------+
    /// | 0   | key tag | algorithm | Digest type |
    /// +-----+---------+-----------+-------------+
    /// | 32  | digest                            |
    /// | ... |                                   |
    /// +-----+-----------------------------------+
    /// </pre>
    /// </summary>
    [DnsTypeRegistration(Type = DnsType.Ds)]
    [DnsTypeRegistration(Type = DnsType.Cds)]
    [DnsTypeRegistration(Type = DnsType.Ta)]
    [DnsTypeRegistration(Type = DnsType.Dlv)]
    public sealed class DnsResourceDataDelegationSigner : DnsResourceDataSimple, IEquatable<DnsResourceDataDelegationSigner>
    {
        public static class Offset
        {
            public const int KeyTag = 0;
            public const int Algorithm = KeyTag + sizeof(ushort);
            public const int DigestType = Algorithm + sizeof(byte);
            public const int Digest = DigestType + sizeof(byte);
        }

        public const int ConstPartLength = Offset.Digest;

        public DnsResourceDataDelegationSigner(ushort keyTag, DnsAlgorithm algorithm, DnsDigestType digestType, DataSegment digest)
        {
            KeyTag = keyTag;
            Algorithm = algorithm;
            DigestType = digestType;
            int maxDigestLength;
            switch (DigestType)
            {
                case DnsDigestType.Sha1:
                    maxDigestLength = 20;
                    break;

                case DnsDigestType.Sha256:
                    maxDigestLength = 32;
                    break;

                default:
                    maxDigestLength = int.MaxValue;
                    break;
            }
            Digest = digest.SubSegment(0, Math.Min(digest.Length, maxDigestLength));
            ExtraDigest = digest.SubSegment(Digest.Length, digest.Length - Digest.Length);
        }

        /// <summary>
        /// Lists the key tag of the DNSKEY RR referred to by the DS record.
        /// The Key Tag used by the DS RR is identical to the Key Tag used by RRSIG RRs.
        /// Calculated as specified in RFC 2535.
        /// </summary>
        public ushort KeyTag { get; private set; }

        /// <summary>
        /// Algorithm must be allowed to sign DNS data.
        /// </summary>
        public DnsAlgorithm Algorithm { get; private set; }

        /// <summary>
        /// An identifier for the digest algorithm used.
        /// </summary>
        public DnsDigestType DigestType { get; private set; }

        /// <summary>
        /// Calculated over the canonical name of the delegated domain name followed by the whole RDATA of the KEY record (all four fields).
        /// digest = hash(canonical FQDN on KEY RR | KEY_RR_rdata)
        /// KEY_RR_rdata = Flags | Protocol | Algorithm | Public Key
        /// The size of the digest may vary depending on the digest type.
        /// </summary>
        public DataSegment Digest { get; private set; }

        /// <summary>
        /// The extra digest bytes after number of bytes according to the digest type.
        /// </summary>
        public DataSegment ExtraDigest { get; private set; }

        public bool Equals(DnsResourceDataDelegationSigner other)
        {
            return other != null &&
                   KeyTag.Equals(other.KeyTag) &&
                   Algorithm.Equals(other.Algorithm) &&
                   DigestType.Equals(other.DigestType) &&
                   Digest.Equals(other.Digest);
        }

        public override bool Equals(DnsResourceData other)
        {
            return Equals(other as DnsResourceDataDelegationSigner);
        }

        internal DnsResourceDataDelegationSigner()
            : this(0, DnsAlgorithm.None, DnsDigestType.Sha1, DataSegment.Empty)
        {
        }

        internal override int GetLength()
        {
            return ConstPartLength + Digest.Length;
        }

        internal override void WriteDataSimple(byte[] buffer, int offset)
        {
            buffer.Write(offset + Offset.KeyTag, KeyTag, Endianity.Big);
            buffer.Write(offset + Offset.Algorithm, (byte)Algorithm);
            buffer.Write(offset + Offset.DigestType, (byte)DigestType);
            Digest.Write(buffer, offset + Offset.Digest);
        }

        internal override DnsResourceData CreateInstance(DataSegment data)
        {
            ushort keyTag = data.ReadUShort(Offset.KeyTag, Endianity.Big);
            DnsAlgorithm algorithm = (DnsAlgorithm)data[Offset.Algorithm];
            DnsDigestType digestType = (DnsDigestType)data[Offset.DigestType];
            DataSegment digest = data.SubSegment(Offset.Digest, data.Length - ConstPartLength);

            return new DnsResourceDataDelegationSigner(keyTag, algorithm, digestType, digest);
        }
    }
}