namespace QueryLens.Core.Fingerprinting
{
    public interface IQueryFingerprinter
    {
        QueryFingerprint Fingerprint(string sql);
    }
}
