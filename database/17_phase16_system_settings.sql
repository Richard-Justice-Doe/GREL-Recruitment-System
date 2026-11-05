START TRANSACTION;
CREATE TABLE `EmailTemplates` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `Key` varchar(100) NOT NULL,
    `Subject` varchar(200) NOT NULL,
    `Body` text NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `CreatedBy` varchar(256) NULL,
    `UpdatedAt` datetime(6) NULL,
    `UpdatedBy` varchar(256) NULL,
    `IsDeleted` tinyint(1) NOT NULL,
    `DeletedAt` datetime(6) NULL,
    `DeletedBy` varchar(256) NULL,
    PRIMARY KEY (`Id`)
);

CREATE TABLE `SystemSettings` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `Key` varchar(100) NOT NULL,
    `SettingValue` text NULL,
    `IsSecret` tinyint(1) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `CreatedBy` varchar(256) NULL,
    `UpdatedAt` datetime(6) NULL,
    `UpdatedBy` varchar(256) NULL,
    `IsDeleted` tinyint(1) NOT NULL,
    `DeletedAt` datetime(6) NULL,
    `DeletedBy` varchar(256) NULL,
    PRIMARY KEY (`Id`)
);

CREATE INDEX `IX_EmailTemplates_IsDeleted` ON `EmailTemplates` (`IsDeleted`);

CREATE INDEX `IX_EmailTemplates_Key` ON `EmailTemplates` (`Key`);

CREATE INDEX `IX_SystemSettings_IsDeleted` ON `SystemSettings` (`IsDeleted`);

CREATE INDEX `IX_SystemSettings_Key` ON `SystemSettings` (`Key`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20261105122849_Phase16_SystemSettings', '10.0.14');

COMMIT;

