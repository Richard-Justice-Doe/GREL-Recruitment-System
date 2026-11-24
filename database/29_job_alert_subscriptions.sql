START TRANSACTION;
CREATE TABLE `JobAlertSubscriptions` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `UserId` varchar(255) NOT NULL,
    `Keywords` varchar(300) NULL,
    `IsActive` tinyint(1) NOT NULL,
    `LastSentAt` datetime(6) NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `CreatedBy` varchar(256) NULL,
    `UpdatedAt` datetime(6) NULL,
    `UpdatedBy` varchar(256) NULL,
    `IsDeleted` tinyint(1) NOT NULL,
    `DeletedAt` datetime(6) NULL,
    `DeletedBy` varchar(256) NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_JobAlertSubscriptions_Users_UserId` FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
);

CREATE INDEX `IX_JobAlertSubscriptions_IsActive` ON `JobAlertSubscriptions` (`IsActive`);

CREATE INDEX `IX_JobAlertSubscriptions_IsDeleted` ON `JobAlertSubscriptions` (`IsDeleted`);

CREATE UNIQUE INDEX `IX_JobAlertSubscriptions_UserId` ON `JobAlertSubscriptions` (`UserId`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20261112205618_JobAlertSubscriptions', '10.0.14');

COMMIT;

