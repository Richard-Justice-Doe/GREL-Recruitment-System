START TRANSACTION;
CREATE TABLE `AiCacheEntries` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `Key` varchar(64) NOT NULL,
    `Feature` varchar(40) NOT NULL,
    `Output` text NOT NULL,
    `ExpiresAt` datetime(6) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `CreatedBy` varchar(256) NULL,
    `UpdatedAt` datetime(6) NULL,
    `UpdatedBy` varchar(256) NULL,
    `IsDeleted` tinyint(1) NOT NULL,
    `DeletedAt` datetime(6) NULL,
    `DeletedBy` varchar(256) NULL,
    PRIMARY KEY (`Id`)
);

CREATE TABLE `AiRequestLogs` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `Feature` varchar(40) NOT NULL,
    `UserId` varchar(450) NULL,
    `Provider` varchar(20) NOT NULL,
    `Model` varchar(100) NOT NULL,
    `PromptVersion` varchar(20) NOT NULL,
    `InputTokens` int NOT NULL,
    `OutputTokens` int NOT NULL,
    `CostUsd` decimal(12,6) NOT NULL,
    `DurationMs` int NOT NULL,
    `Outcome` varchar(20) NOT NULL,
    `Error` varchar(300) NULL,
    `RecordType` varchar(50) NULL,
    `RecordId` int NULL,
    `Prompt` text NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `CreatedBy` varchar(256) NULL,
    `UpdatedAt` datetime(6) NULL,
    `UpdatedBy` varchar(256) NULL,
    `IsDeleted` tinyint(1) NOT NULL,
    `DeletedAt` datetime(6) NULL,
    `DeletedBy` varchar(256) NULL,
    PRIMARY KEY (`Id`)
);

CREATE INDEX `IX_AiCacheEntries_ExpiresAt` ON `AiCacheEntries` (`ExpiresAt`);

CREATE INDEX `IX_AiCacheEntries_IsDeleted` ON `AiCacheEntries` (`IsDeleted`);

CREATE UNIQUE INDEX `IX_AiCacheEntries_Key` ON `AiCacheEntries` (`Key`);

CREATE INDEX `IX_AiRequestLogs_CreatedAt` ON `AiRequestLogs` (`CreatedAt`);

CREATE INDEX `IX_AiRequestLogs_IsDeleted` ON `AiRequestLogs` (`IsDeleted`);

CREATE INDEX `IX_AiRequestLogs_UserId_CreatedAt` ON `AiRequestLogs` (`UserId`, `CreatedAt`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20261113160903_AiFoundation', '10.0.14');

COMMIT;

