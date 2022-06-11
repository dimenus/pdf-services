using System;
using System.Data;
using Dapper;
using PdfServices.Service.Utils;

namespace PdfServices.Service.Models;

public class PdfInputChunkModel
{
    public enum ChunkStatus
    {
        AwaitingPayload,
        StoredLocally
    }

    public ref struct ChunkInfo
    {
        public long ChunkIndex { get; init; }
        public long FileSizeInBytes { get; init; }
    }

    private const string CREATE_QUERY_TEXT =
        "INSERT INTO PdfInputChunk (ChunkIndex, PdfInputId, FileSizeInBytes, StatusId) VALUES (@ChunkIndex, @PdfInputId, @FileSize, @StatusId)";

    private const string UPDATE_STATUS_QUERY_TEXT =
        "UPDATE PdfInputChunk set StatusID = @StatusId WHERE ChunkIndex = @ChunkIndex AND PdfInputId = @PdfInputId";

    public int ChunkId { get; init; }

    public int PdfInputId { get; init; }

    public long FileSizeInBytes { get; init; }

    public ChunkStatus Status { get; init; }
    
    public static void Create(SqliteDbContext dbContext, long pdfInputId, ChunkInfo chunkInfo)
    {
        var sql_conn = dbContext.GetConnection();

        var sql_cmd_create = sql_conn.CreateCommand();
        sql_cmd_create.CommandText = CREATE_QUERY_TEXT;
        
        sql_cmd_create.AddParameterWithValue("@ChunkIndex", chunkInfo.ChunkIndex);
        sql_cmd_create.AddParameterWithValue("@PdfInputId", pdfInputId);
        sql_cmd_create.AddParameterWithValue("@FileSize", chunkInfo.FileSizeInBytes);
        sql_cmd_create.AddParameterWithValue("@StatusId", (int)ChunkStatus.AwaitingPayload);

        if (sql_cmd_create.ExecuteNonQuery() != 1) {
            throw new Exception($"Failed to insert ModelData with ChunkIndex '{chunkInfo.ChunkIndex}'");
        }
    }

    public static PdfInputChunkModel? Get(SqliteDbContext dbContext, long pdfInputId, long chunkIndex)
    {
        var sql_conn = dbContext.GetConnection();
        return sql_conn.QuerySingleOrDefault<PdfInputChunkModel?>(
            "SELECT * FROM PdfInputChunk WHERE ChunkIndex = @ChunkIndex AND PdfInputId = @PdfInputId",
            new {ChunkIndex = chunkIndex, PdfInputId = pdfInputId});
    }

    public static void UpdateStatus(SqliteDbContext dbContext, long pdfInputId, long chunkIndex, ChunkStatus chunkStatus)
    {
        var sql_conn = dbContext.GetConnection();
        
        var sql_cmd_create = sql_conn.CreateCommand();
        sql_cmd_create.CommandText = UPDATE_STATUS_QUERY_TEXT;
        
        sql_cmd_create.AddParameterWithValue("@StatusId", (int)chunkStatus);
        sql_cmd_create.AddParameterWithValue("@ChunkIndex", chunkIndex);
        sql_cmd_create.AddParameterWithValue("@PdfInputId", pdfInputId);
        Console.WriteLine($"Updating PdfInputID: {pdfInputId}, chunkIndex: {chunkIndex}");
        if (sql_cmd_create.ExecuteNonQuery() != 1) {
            throw new Exception(
                $"Failed to update  PdfInput '{pdfInputId}', chunkIndex ({chunkIndex}) with status '{chunkStatus}'");
        }
    }
}