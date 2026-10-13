using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace QueryLens.SqlServer.Tests.TestHelpers
{
    public sealed class FakeDbParameterCollection : DbParameterCollection
    {
        private readonly List<DbParameter> _items = new List<DbParameter>();
        public override int Count => _items.Count;
        public override object SyncRoot => this;
        public override int Add(object value) { _items.Add((DbParameter)value); return _items.Count - 1; }
        public override void AddRange(Array values) { foreach (var v in values) Add(v); }
        public override void Clear() => _items.Clear();
        public override bool Contains(object value) => _items.Contains((DbParameter)value);
        public override bool Contains(string value) => _items.Any(p => p.ParameterName == value);
        public override void CopyTo(Array array, int index) => ((ICollection)_items).CopyTo(array, index);
        public override IEnumerator GetEnumerator() => _items.GetEnumerator();
        public override int IndexOf(object value) => _items.IndexOf((DbParameter)value);
        public override int IndexOf(string parameterName) => _items.FindIndex(p => p.ParameterName == parameterName);
        public override void Insert(int index, object value) => _items.Insert(index, (DbParameter)value);
        public override void Remove(object value) => _items.Remove((DbParameter)value);
        public override void RemoveAt(int index) => _items.RemoveAt(index);
        public override void RemoveAt(string parameterName) => _items.RemoveAt(IndexOf(parameterName));
        protected override DbParameter GetParameter(int index) => _items[index];
        protected override DbParameter GetParameter(string parameterName) => _items[IndexOf(parameterName)];
        protected override void SetParameter(int index, DbParameter value) => _items[index] = value;
        protected override void SetParameter(string parameterName, DbParameter value) => _items[IndexOf(parameterName)] = value;

        public IReadOnlyList<DbParameter> Items => _items;
    }

    public sealed class FakeDbParameter : DbParameter
    {
        public override DbType DbType { get; set; }
        public override ParameterDirection Direction { get; set; } = ParameterDirection.Input;
        public override bool IsNullable { get; set; }
        public override string ParameterName { get; set; }
        public override int Size { get; set; }
        public override string SourceColumn { get; set; }
        public override bool SourceColumnNullMapping { get; set; }
        public override object Value { get; set; }
        public override void ResetDbType() { }
    }

    /// <summary>
    /// A DbDataReader backed by an in-memory table (column names + row values),
    /// so tests can simulate exactly the shape a real query would return
    /// without touching a database.
    /// </summary>
    public sealed class FakeDbDataReader : DbDataReader
    {
        private readonly List<string> _columns;
        private readonly List<object[]> _rows;
        private int _currentRow = -1;

        public FakeDbDataReader(IEnumerable<string> columns, IEnumerable<object[]> rows)
        {
            _columns = columns.ToList();
            _rows = rows.ToList();
        }

        public static FakeDbDataReader Empty(IEnumerable<string> columns) => new FakeDbDataReader(columns, Enumerable.Empty<object[]>());

        private bool _isClosed;

        public override int Depth => 0;
        public override int FieldCount => _columns.Count;
        public override bool HasRows => _rows.Count > 0;
        public override bool IsClosed => _isClosed;
        public override int RecordsAffected => -1;

        public override object this[int ordinal] => GetValue(ordinal);
        public override object this[string name] => GetValue(GetOrdinal(name));

        public override int GetOrdinal(string name)
        {
            var index = _columns.IndexOf(name);
            if (index < 0)
            {
                throw new IndexOutOfRangeException($"No column named '{name}'.");
            }

            return index;
        }

        public override bool IsDBNull(int ordinal)
        {
            var value = _rows[_currentRow][ordinal];
            return value == null || value == DBNull.Value;
        }

        public override object GetValue(int ordinal) => _rows[_currentRow][ordinal];
        public override long GetInt64(int ordinal) => Convert.ToInt64(_rows[_currentRow][ordinal]);
        public override int GetInt32(int ordinal) => Convert.ToInt32(_rows[_currentRow][ordinal]);
        public override string GetString(int ordinal) => (string)_rows[_currentRow][ordinal];
        public override double GetDouble(int ordinal) => Convert.ToDouble(_rows[_currentRow][ordinal]);
        public override DateTime GetDateTime(int ordinal) => Convert.ToDateTime(_rows[_currentRow][ordinal]);
        public override bool GetBoolean(int ordinal) => Convert.ToBoolean(_rows[_currentRow][ordinal]);

        public override bool Read()
        {
            _currentRow++;
            return _currentRow < _rows.Count;
        }

        public override Task<bool> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(Read());

        public override bool NextResult() => false;
        public override void Close() => _isClosed = true;
        public override IEnumerator GetEnumerator() => _rows.GetEnumerator();

        public override string GetName(int ordinal) => _columns[ordinal];
        public override int GetValues(object[] values)
        {
            var count = Math.Min(values.Length, _columns.Count);
            for (var i = 0; i < count; i++)
            {
                values[i] = _rows[_currentRow][i];
            }
            return count;
        }

        public override byte GetByte(int ordinal) => throw new NotSupportedException();
        public override long GetBytes(int ordinal, long dataOffset, byte[] buffer, int bufferOffset, int length) => throw new NotSupportedException();
        public override char GetChar(int ordinal) => throw new NotSupportedException();
        public override long GetChars(int ordinal, long dataOffset, char[] buffer, int bufferOffset, int length) => throw new NotSupportedException();
        public override string GetDataTypeName(int ordinal) => throw new NotSupportedException();
        public override decimal GetDecimal(int ordinal) => throw new NotSupportedException();
        public override float GetFloat(int ordinal) => throw new NotSupportedException();
        public override Guid GetGuid(int ordinal) => throw new NotSupportedException();
        public override short GetInt16(int ordinal) => throw new NotSupportedException();
        public override Type GetFieldType(int ordinal) => throw new NotSupportedException();
    }

    /// <summary>Command whose ExecuteScalar/ExecuteReader results are set by the owning connection - lets one test configure "what the next query returns" without caring what SQL text was sent.</summary>
    public sealed class FakeDbCommand : DbCommand
    {
        private readonly FakeDbParameterCollection _parameters = new FakeDbParameterCollection();
        private readonly FakeDbConnection _owner;

        public FakeDbCommand(FakeDbConnection owner)
        {
            _owner = owner;
            CommandType = CommandType.Text;
        }

        public override string CommandText { get; set; }
        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; }
        public override bool DesignTimeVisible { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }
        protected override DbConnection DbConnection { get; set; }
        protected override DbParameterCollection DbParameterCollection => _parameters;
        protected override DbTransaction DbTransaction { get; set; }

        public override void Cancel() { }
        protected override DbParameter CreateDbParameter() => new FakeDbParameter();
        public override void Prepare() { }

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => _owner.NextReaderResult;

        protected override Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken) =>
            Task.FromResult((DbDataReader)_owner.NextReaderResult);

        public override int ExecuteNonQuery() => 0;
        public override object ExecuteScalar() => _owner.NextScalarResult;
        public override Task<object> ExecuteScalarAsync(CancellationToken cancellationToken) => Task.FromResult(_owner.NextScalarResult);
    }

    public sealed class FakeDbConnection : DbConnection
    {
        public object NextScalarResult { get; set; }
        public FakeDbDataReader NextReaderResult { get; set; }

        public override string ConnectionString { get; set; }
        public override string Database => "FakeDb";
        public override string DataSource => "fake-server";
        public override string ServerVersion => "1.0";
        public override ConnectionState State => ConnectionState.Open;

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();
        public override void ChangeDatabase(string databaseName) { }
        public override void Close() { }
        protected override DbCommand CreateDbCommand() => new FakeDbCommand(this) { Connection = this };
        public override void Open() { }
    }
}
