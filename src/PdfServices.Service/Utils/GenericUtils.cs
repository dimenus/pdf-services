using System;
using Microsoft.Data.Sqlite;

namespace PdfServices.Service.Utils;

internal static class GenericUtils
{
    public static void ValidateSha256(string hash)
    {
#if DEBUG
        if (hash.Length != 64) throw new ArgumentException($"{nameof(hash)} must be base16 encoded sha256");
#endif
    }

    public static void AddParameterWithValue(this SqliteCommand sqlCommand, string paramName, object? value)
    {
        sqlCommand.Parameters.Add(new SqliteParameter {
            ParameterName = paramName,
            Value = value
        });
    }
}