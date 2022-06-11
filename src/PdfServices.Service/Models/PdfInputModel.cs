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
        public string Sha256Hash { get; init; }
    }

    private const string CREATE_QUERY_TEXT =
        $"INSERT INTO PdfInput (ExternalId, Sha256Hash, SubmittedDateTime, StatusId, LocalStoragePath) " +
        "VALUES (@ExternalId, @Sha256, @SubmittedDateTime, @StatusId, @LocalStoragePath)";

    private const string SELECT_USING_EXTERNAL_ID = "SELECT * from PdfInput where ExternalId = @ExternalId";

    public long Id { get; init; }
    public string ExternalId { get; init; } = null!;
    public string Sha256Hash { get; init; } = null!;
    public DateTime SubmittedDateTime { get; init; }
    public string LocalStoragePath { get; init; } = null!;
    public InputStatus Status { get; init; } = InputStatus.Created;
#pragma warning restore CS8618

    public static void Create(SqliteDbContext dbContext, InputInfo inputInfo)
    {
        GenericUtils.ValidateSha256(inputInfo.Sha256Hash);
        var sql_conn = dbContext.GetConnection();

        var sql_cmd_create = sql_conn.CreateCommand();
        sql_cmd_create.CommandText = CREATE_QUERY_TEXT;
        
        var storage_path = Path.Combine(Path.GetTempPath(), "kwapi-pdf-services", inputInfo.ExternalId);
        Directory.CreateDirectory(storage_path);

        var pdf_input = new PdfInputModel {
            ExternalId = inputInfo.ExternalId,
            Sha256Hash = inputInfo.Sha256Hash,
            SubmittedDateTime = DateTime.UtcNow,
            LocalStoragePath = storage_path
        };
        
        sql_cmd_create.AddParameterWithValue("@ExternalId", pdf_input.ExternalId);
        sql_cmd_create.AddParameterWithValue("@Sha256", pdf_input.Sha256Hash);
        sql_cmd_create.AddParameterWithValue("@SubmittedDateTime", pdf_input.SubmittedDateTime);
        sql_cmd_create.AddParameterWithValue("@StatusId", (int)pdf_input.Status);
        sql_cmd_create.AddParameterWithValue("@LocalStoragePath", storage_path);

        if (sql_cmd_create.ExecuteNonQuery() != 1) {
            throw new Exception($"Failed to insert ModelData with Hash '{inputInfo.ExternalId}'");
        }
    }

    public static PdfInputModel? GetByExternalId(SqliteDbContext dbContext, string externalId)
    {
        var sql_conn = dbContext.GetConnection();
        return sql_conn.QuerySingleOrDefault<PdfInputModel>(SELECT_USING_EXTERNAL_ID, new { ExternalId = externalId});
    }
}