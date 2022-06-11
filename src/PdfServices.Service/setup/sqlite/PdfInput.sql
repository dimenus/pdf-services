create table PdfInput
(
    Id                integer  not null
        constraint PdfInput_pk
            primary key autoincrement,
    ExternalId        text(36) not null,
    Sha256Hash        text(64) not null,
    SubmittedDateTime datetime not null,
    StatusId          integer  not null,
    LocalStoragePath  text(512)
);

create unique index PdfInput_PdfInputId_uindex
    on PdfInput (Id);

