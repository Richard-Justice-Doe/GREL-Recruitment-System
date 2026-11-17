START TRANSACTION;
ALTER TABLE `Employees` ADD `CelebrationsOptOut` tinyint(1) NOT NULL DEFAULT FALSE;

CREATE TABLE `CelebrationMessages` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `EmployeeId` int NOT NULL,
    `Kind` varchar(20) NOT NULL,
    `Year` int NOT NULL,
    `AuthorUserId` varchar(450) NOT NULL,
    `AuthorName` varchar(256) NOT NULL,
    `Text` varchar(280) NOT NULL,
    `IsHidden` tinyint(1) NOT NULL,
    `HiddenBy` varchar(256) NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `CreatedBy` varchar(256) NULL,
    `UpdatedAt` datetime(6) NULL,
    `UpdatedBy` varchar(256) NULL,
    `IsDeleted` tinyint(1) NOT NULL,
    `DeletedAt` datetime(6) NULL,
    `DeletedBy` varchar(256) NULL,
    PRIMARY KEY (`Id`)
);

CREATE INDEX `IX_CelebrationMessages_EmployeeId_Kind_Year` ON `CelebrationMessages` (`EmployeeId`, `Kind`, `Year`);

CREATE INDEX `IX_CelebrationMessages_IsDeleted` ON `CelebrationMessages` (`IsDeleted`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20261117094110_Celebrations', '10.0.14');

COMMIT;

