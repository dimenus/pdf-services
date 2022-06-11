using System.Collections.Generic;
using System.Text;

namespace PdfServices.Service.Utils;

/// <summary>
///     This is a very light abstraction over raw query text intended for cases where you may or may not have existing
///     where clauses.
///     It performs no validation, it's expected that the Sqlite driver / library will return errors if your syntax is
///     invalid
/// </summary>
public class SqliteQueryBuilder
{
    public enum WhereConnector
    {
        And,
        Or
    }

    private readonly StringBuilder _sbQuery;

    private bool _haveWhere;


    public SqliteQueryBuilder(int reserveCap = 2048)
    {
        _sbQuery = new StringBuilder(reserveCap);
    }

    public void AddSelect(string str)
    {
        _sbQuery.Append(" SELECT ");
        _sbQuery.Append(str);
    }

    public void AddFrom(string str)
    {
        _sbQuery.Append(" FROM ");
        _sbQuery.Append(str);
    }

    public void AddWhere(string str, WhereConnector connector = WhereConnector.And)
    {
        if (_haveWhere) {
            _sbQuery.Append($" {connector.ToString().ToUpperInvariant()}");
        } else {
            _haveWhere = true;
            _sbQuery.Append(" WHERE ");
        }

        _sbQuery.Append('(');
        _sbQuery.Append(str);
        _sbQuery.Append(')');
    }

    public void AddGroupedWhere(string firstClause, List<WhereSubgroup> subGroups,
        WhereConnector firstConnector = WhereConnector.And)
    {
        if (_haveWhere) {
            _sbQuery.Append($" {firstConnector.ToString().ToUpperInvariant()} ");
        } else {
            _haveWhere = true;
            _sbQuery.Append(" WHERE ");
        }

        _sbQuery.Append('(');
        _sbQuery.Append('(');
        _sbQuery.Append(firstClause);
        _sbQuery.Append(')');
        foreach (var item in subGroups.ToArray()[1..]) {
            _sbQuery.Append($" {item.Connector.ToString().ToUpper()} ");
            _sbQuery.Append('(');
            _sbQuery.Append(item.QueryText);
            _sbQuery.Append(')');
        }

        _sbQuery.Append(')');
    }

    public string ToSqlString()
    {
        return _sbQuery.ToString();
    }

    public struct WhereSubgroup
    {
        public WhereConnector Connector { get; init; }
        public string QueryText { get; init; }
    }
}