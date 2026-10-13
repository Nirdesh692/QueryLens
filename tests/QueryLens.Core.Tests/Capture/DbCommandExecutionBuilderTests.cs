using System;
using System.Data;
using QueryLens.Core.Capture;
using QueryLens.Core.Tests.TestHelpers;
using Xunit;

namespace QueryLens.Core.Tests.Capture
{
    public class DbCommandExecutionBuilderTests
    {
        [Fact]
        public void Build_CapturesBasicFieldsFromCommandAndConnection()
        {
            var connection = new FakeDbConnection("OrdersDb", "sql-primary");
            var command = new FakeDbCommand
            {
                CommandText = "SELECT * FROM Orders WHERE CustomerId = @CustomerId",
                Connection = connection,
                CommandType = CommandType.Text
            };

            var execution = DbCommandExecutionBuilder.Build(
                command, TimeSpan.FromMilliseconds(42), "req-1", captureParameters: false,
                provider: "EF6", orm: "EF6", framework: ".NETFramework");

            Assert.Equal("SELECT * FROM Orders WHERE CustomerId = @CustomerId", execution.Sql);
            Assert.Equal(TimeSpan.FromMilliseconds(42), execution.Duration);
            Assert.Equal("Text", execution.CommandType);
            Assert.Equal("OrdersDb", execution.Database);
            Assert.Equal("sql-primary", execution.Server);
            Assert.Equal("req-1", execution.RequestId);
            Assert.Equal("EF6", execution.Provider);
            Assert.Equal("EF6", execution.Orm);
            Assert.Equal(".NETFramework", execution.Framework);
        }

        [Fact]
        public void Build_TagsProviderAndOrmIndependently()
        {
            var command = new FakeDbCommand { CommandText = "SELECT 1" };

            var execution = DbCommandExecutionBuilder.Build(
                command, TimeSpan.Zero, null, captureParameters: false,
                provider: "EFCore", orm: "EFCore", framework: ".NET");

            Assert.Equal("EFCore", execution.Provider);
            Assert.Equal("EFCore", execution.Orm);
            Assert.Equal(".NET", execution.Framework);
        }

        [Fact]
        public void Build_WhenParametersNotRequested_LeavesParametersNull()
        {
            var command = new FakeDbCommand { CommandText = "SELECT 1" };

            var execution = DbCommandExecutionBuilder.Build(command, TimeSpan.Zero, null, false, "AdoNet", "AdoNet");

            Assert.Null(execution.Parameters);
        }

        [Fact]
        public void Build_WhenParametersRequestedButNonePresent_ReturnsEmptyNotNull()
        {
            var command = new FakeDbCommand { CommandText = "SELECT 1" };

            var execution = DbCommandExecutionBuilder.Build(command, TimeSpan.Zero, null, true, "AdoNet", "AdoNet");

            Assert.NotNull(execution.Parameters);
            Assert.Empty(execution.Parameters);
        }

        [Fact]
        public void Build_WhenParametersRequested_CapturesMetadataOnlyNeverValues()
        {
            var command = new FakeDbCommand { CommandText = "SELECT * FROM Users WHERE Id = @Id AND Name = @Name" };

            var idParam = command.CreateParameter();
            idParam.ParameterName = "@Id";
            idParam.DbType = DbType.Int32;
            idParam.Size = 4;
            idParam.Value = 12345;
            command.Parameters.Add(idParam);

            var nameParam = command.CreateParameter();
            nameParam.ParameterName = "@Name";
            nameParam.DbType = DbType.String;
            nameParam.Size = 100;
            nameParam.Value = "Alice";
            command.Parameters.Add(nameParam);

            var execution = DbCommandExecutionBuilder.Build(command, TimeSpan.FromMilliseconds(5), null, true, "EF6", "EF6");

            Assert.Equal(2, execution.Parameters.Count);
            Assert.Equal("@Id", execution.Parameters[0].Name);
            Assert.Equal("Int32", execution.Parameters[0].DbType);
            Assert.Equal(4, execution.Parameters[0].Size);
            Assert.Equal("@Name", execution.Parameters[1].Name);
            Assert.Equal("String", execution.Parameters[1].DbType);

            // QueryParameter has no Value property at all - this is a compile-time
            // guarantee, not a runtime one.
        }

        [Fact]
        public void Build_WithNoConnection_DoesNotThrowAndLeavesDatabaseServerNull()
        {
            var command = new FakeDbCommand { CommandText = "SELECT 1" };

            var execution = DbCommandExecutionBuilder.Build(command, TimeSpan.Zero, null, false, "AdoNet", "AdoNet");

            Assert.Null(execution.Database);
            Assert.Null(execution.Server);
        }

        [Fact]
        public void Build_WithNullCommand_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                DbCommandExecutionBuilder.Build(null, TimeSpan.Zero, null, false, "AdoNet", "AdoNet"));
        }
    }
}
