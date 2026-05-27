CREATE TABLE [dbo].[NotificationOutbox] (
    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
    [SourceApplication] nvarchar(100) NOT NULL,
    [NotificationType] nvarchar(150) NOT NULL,
    [IdempotencyKey] nvarchar(500) NOT NULL,
    [PayloadJson] nvarchar(max) NOT NULL,
    [Status] nvarchar(50) NOT NULL,
    [QueueMessageId] nvarchar(200) NULL,
    [AttemptCount] int NOT NULL DEFAULT 0,
    [LastError] nvarchar(max) NULL,
    [CreatedUtc] datetime2 NOT NULL,
    [UpdatedUtc] datetime2 NOT NULL,
    [ProcessedUtc] datetime2 NULL,
    [NextAttemptUtc] datetime2 NULL,
    [LockedUntilUtc] datetime2 NULL
);

CREATE UNIQUE INDEX [IX_NotificationOutbox_IdempotencyKey] ON [dbo].[NotificationOutbox] ([IdempotencyKey]);

CREATE INDEX [IX_NotificationOutbox_Status_CreatedUtc] ON [dbo].[NotificationOutbox] ([Status], [CreatedUtc]);
CREATE INDEX [IX_NotificationOutbox_Status_NextAttemptUtc] ON [dbo].[NotificationOutbox] ([Status], [NextAttemptUtc]);
CREATE INDEX [IX_NotificationOutbox_Status_LockedUntilUtc] ON [dbo].[NotificationOutbox] ([Status], [LockedUntilUtc]);
