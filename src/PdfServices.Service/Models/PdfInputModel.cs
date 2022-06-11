using System;
using System.IO;
using Dapper;
using PdfServices.Service.Utils;

namespace PdfServices.Service.Models;

public class PdfInputModel
{
    public enum InputStatus
    {
        Created,
        AwaitingChunks,
        StoredLocally,
    }

    public ref struct InputInfo
    {
        public string ExternalId { get; init; }
        
        public long ExpectedFileSize { get; init; }
        public string Sha256Hash { get; init; }
    }

    private const string CREATE_QUERY_TEXT =
        $"INSERT INTO PdfInput (ExternalId, Sha256Hash, ExpectedFileSize, SubmittedDateTime, StatusId) " +
        "VALUES (@ExternalId, @Sha256, @ExpectedFileSize, @SubmittedDateTime, @StatusId)";

    private const string SELECT_USING_EXTERNAL_ID = "SELECT * from PdfInput where ExternalId = @ExternalId";

    public long Id { get; init; }
    public string ExternalId { get; init; } = null!;
    
    public long ExpectedFileSize { get; init; }
    public string Sha256Hash { get; init; } = null!;
    public DateTime SubmittedDateTime { get; init; }
    public InputStatus Status { get; init; } = InputStatus.Created;
#pragma warning restore CS8618

    public static void Create(SqliteDbContext dbContext, InputInfo inputInfo)
    {
        GenericUtils.ValidateSha256(inputInfo.Sha256Hash);
        var sql_conn = dbContext.GetConnection();

        var sql_cmd_create = sql_conn.CreateCommand();
        sql_cmd_create.CommandText = CREATE_QUERY_TEXT;
        
        sql_cmd_create.AddParameterWithValue("@ExternalId", inputInfo.ExternalId);
        sql_cmd_create.AddParameterWithValue("@Sha256", inputInfo.Sha256Hash);
        sql_cmd_create.AddParameterWithValue("@ExpectedFileSize", inputInfo.ExpectedFileSize);
        sql_cmd_create.AddParameterWithValue("@SubmittedDateTime", DateTime.UtcNow);
        sql_cmd_create.AddParameterWithValue("@StatusId", (int)InputStatus.AwaitingChunks);

        if (sql_cmd_create.ExecuteNonQuery() != 1) {
            throw new Exception($"Failed to insert ModelData with Hash '{inputInfo.ExternalId}'");
        }
        
        
    }

    public static PdfInputModel? GetByExternalId(SqliteDbContext dbContext, string externalId)
    {
        var sql_conn = dbContext.GetConnection();
        return sql_conn.QuerySingleOrDefault<PdfInputModel>(SELECT_USING_EXTERNAL_ID, new { ExternalId = externalId});
    }

    public string GetLocalStoragePath()
    {
        var path = $"{Path.Combine(Path.GetTempPath(), "kwapi-pdf-services", ExternalId)}";
        Directory.CreateDirectory(path);
        return path;
    }
}