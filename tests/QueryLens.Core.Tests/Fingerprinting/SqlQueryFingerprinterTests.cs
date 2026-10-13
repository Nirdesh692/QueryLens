using QueryLens.Core.Fingerprinting;
using Xunit;

namespace QueryLens.Core.Tests.Fingerprinting
{
    public class SqlQueryFingerprinterTests
    {
        private readonly SqlQueryFingerprinter _fingerprinter = new SqlQueryFingerprinter();

        [Fact]
        public void SameQueryWithDifferentIntegerLiterals_ProducesSameHash()
        {
            var a = _fingerprinter.Fingerprint("SELECT * FROM Users WHERE Id = 10");
            var b = _fingerprinter.Fingerprint("SELECT * FROM Users WHERE Id = 20");

            Assert.Equal(a.Hash, b.Hash);
            Assert.Equal("SELECT * FROM Users WHERE Id = ?", a.NormalizedSql);
        }

        [Fact]
        public void SameQueryWithDifferentDecimalLiterals_ProducesSameHash()
        {
            var a = _fingerprinter.Fingerprint("SELECT * FROM Products WHERE Price > 19.99");
            var b = _fingerprinter.Fingerprint("SELECT * FROM Products WHERE Price > 4.50");

            Assert.Equal(a.Hash, b.Hash);
            Assert.Equal("SELECT * FROM Products WHERE Price > ?", a.NormalizedSql);
        }

        [Fact]
        public void SameQueryWithDifferentStringLiterals_ProducesSameHash()
        {
            var a = _fingerprinter.Fingerprint("SELECT * FROM Users WHERE Name = 'Alice'");
            var b = _fingerprinter.Fingerprint("SELECT * FROM Users WHERE Name = 'Bob'");

            Assert.Equal(a.Hash, b.Hash);
            Assert.Equal("SELECT * FROM Users WHERE Name = ?", a.NormalizedSql);
        }

        [Fact]
        public void StringLiteralWithEscapedQuote_IsTreatedAsOneLiteral()
        {
            var result = _fingerprinter.Fingerprint("SELECT * FROM Users WHERE Name = 'O''Brien'");

            Assert.Equal("SELECT * FROM Users WHERE Name = ?", result.NormalizedSql);
        }

        [Fact]
        public void QuotedGuidLiterals_ProduceSameHash()
        {
            var a = _fingerprinter.Fingerprint("SELECT * FROM Orders WHERE Id = 'B3B7B1B0-1234-4E56-8A9B-1234567890AB'");
            var b = _fingerprinter.Fingerprint("SELECT * FROM Orders WHERE Id = '11111111-2222-3333-4444-555555555555'");

            Assert.Equal(a.Hash, b.Hash);
            Assert.Equal("SELECT * FROM Orders WHERE Id = ?", a.NormalizedSql);
        }

        [Fact]
        public void QuotedDateLiterals_ProduceSameHash()
        {
            var a = _fingerprinter.Fingerprint("SELECT * FROM Orders WHERE CreatedAt > '2024-01-01'");
            var b = _fingerprinter.Fingerprint("SELECT * FROM Orders WHERE CreatedAt > '2025-06-15'");

            Assert.Equal(a.Hash, b.Hash);
        }

        [Fact]
        public void InClauseWithDifferentItemCounts_ProducesSameHash()
        {
            var a = _fingerprinter.Fingerprint("SELECT * FROM Orders WHERE CustomerId IN (1, 2, 3)");
            var b = _fingerprinter.Fingerprint("SELECT * FROM Orders WHERE CustomerId IN (1, 2, 3, 4, 5)");

            Assert.Equal(a.Hash, b.Hash);
            Assert.Equal("SELECT * FROM Orders WHERE CustomerId IN (?)", a.NormalizedSql);
        }

        [Fact]
        public void InClauseWithStringItems_CollapsesToSinglePlaceholder()
        {
            var result = _fingerprinter.Fingerprint("SELECT * FROM Users WHERE Status IN ('Active', 'Pending', 'Suspended')");

            Assert.Equal("SELECT * FROM Users WHERE Status IN (?)", result.NormalizedSql);
        }

        [Fact]
        public void ExtraWhitespaceAndNewlines_DoNotAffectHash()
        {
            var a = _fingerprinter.Fingerprint("SELECT * FROM Users WHERE Id = 10");
            var b = _fingerprinter.Fingerprint("SELECT   *\nFROM   Users\n  WHERE Id =    10");

            Assert.Equal(a.Hash, b.Hash);
            Assert.Equal(a.NormalizedSql, b.NormalizedSql);
        }

        [Fact]
        public void TableAliases_ArePreservedInNormalizedSql()
        {
            var result = _fingerprinter.Fingerprint("SELECT u.Id, u.Name FROM Users u WHERE u.Id = 5");

            Assert.Equal("SELECT u.Id, u.Name FROM Users u WHERE u.Id = ?", result.NormalizedSql);
        }

        [Fact]
        public void ParameterizedSql_IsUnchanged()
        {
            var result = _fingerprinter.Fingerprint("SELECT * FROM Users WHERE Id = @Id");

            Assert.Equal("SELECT * FROM Users WHERE Id = @Id", result.NormalizedSql);
        }

        [Fact]
        public void LineAndBlockComments_AreStripped()
        {
            var sql = "SELECT * FROM Users -- get all users\nWHERE Id = 10 /* filter by id */";

            var result = _fingerprinter.Fingerprint(sql);

            Assert.Equal("SELECT * FROM Users WHERE Id = ?", result.NormalizedSql);
            Assert.DoesNotContain("--", result.NormalizedSql);
            Assert.DoesNotContain("/*", result.NormalizedSql);
        }

        [Fact]
        public void MultilineSql_NormalizesToSingleLine()
        {
            var sql = "SELECT\n    Id,\n    Name\nFROM Users\nWHERE Id = 10";

            var result = _fingerprinter.Fingerprint(sql);

            Assert.Equal("SELECT Id, Name FROM Users WHERE Id = ?", result.NormalizedSql);
        }

        [Fact]
        public void StructurallyDifferentQueries_ProduceDifferentHashes()
        {
            var a = _fingerprinter.Fingerprint("SELECT * FROM Users WHERE Id = 10");
            var b = _fingerprinter.Fingerprint("SELECT * FROM Orders WHERE Id = 10");

            Assert.NotEqual(a.Hash, b.Hash);
        }

        [Fact]
        public void RepresentativeSql_RetainsOriginalLiteralValues()
        {
            var result = _fingerprinter.Fingerprint("SELECT * FROM Users WHERE Id = 10");

            Assert.Equal("SELECT * FROM Users WHERE Id = 10", result.RepresentativeSql);
            Assert.Equal("SELECT * FROM Users WHERE Id = ?", result.NormalizedSql);
        }

        [Fact]
        public void Hash_IsTwelveUppercaseHexCharacters()
        {
            var result = _fingerprinter.Fingerprint("SELECT 1");

            Assert.Matches("^[0-9A-F]{12}$", result.Hash);
        }

        [Fact]
        public void HexLiteral_IsNormalized()
        {
            var a = _fingerprinter.Fingerprint("SELECT * FROM Files WHERE Checksum = 0x1A2B3C");
            var b = _fingerprinter.Fingerprint("SELECT * FROM Files WHERE Checksum = 0xFF00FF");

            Assert.Equal(a.Hash, b.Hash);
        }
    }
}
