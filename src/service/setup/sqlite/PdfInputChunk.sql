create table PdfInputChunk
(
    ChunkIndex      integer,
    PdfInputId      integer not null
        references PdfInput,
    FileSizeInBytes integer not null,
    StatusId        integer not null
);

create index PdfInputChunk_ChunkIndex_PdfInputId_index
    on PdfInputChunk (ChunkIndex, PdfInputId);

