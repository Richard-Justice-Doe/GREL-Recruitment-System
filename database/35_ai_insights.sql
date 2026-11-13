START TRANSACTION;
CREATE TABLE `AiInsights` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `Feature` varchar(40) NOT NULL,
    `RecordType` varchar(50) NOT NULL,
    `RecordId` int NOT NULL,
    `Content` text NOT NULL,
    `PromptVersion` varchar(20) NOT NULL,
    `GeneratedByUserId` varchar(450) NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `CreatedBy` varchar(256) NULL,
    `UpdatedAt` datetime(6) NULL,
    `UpdatedBy` varchar(256) NULL,
    `IsDeleted` tinyint(1) NOT NULL,
    `DeletedAt` datetime(6) NULL,
    `DeletedBy` varchar(256) NULL,
    PRIMARY KEY (`Id`)
);

CREATE INDEX `IX_AiInsights_IsDeleted` ON `AiInsights` (`IsDeleted`);

CREATE INDEX `IX_AiInsights_RecordType_RecordId_Feature` ON `AiInsights` (`RecordType`, `RecordId`, `Feature`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20261113230839_AiInsights', '10.0.14');

COMMIT;

