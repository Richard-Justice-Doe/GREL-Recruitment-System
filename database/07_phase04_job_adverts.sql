START TRANSACTION;
CREATE TABLE `JobAdverts` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `AdvertNumber` varchar(40) NOT NULL,
    `VacancyId` int NOT NULL,
    `DivisionId` int NOT NULL,
    `DepartmentId` int NOT NULL,
    `RequestingHodUserId` varchar(255) NOT NULL,
    `Title` varchar(200) NOT NULL,
    `Body` longtext NOT NULL,
    `HowToApply` varchar(2000) NOT NULL,
    `StartDate` date NOT NULL,
    `EndDate` date NOT NULL,
    `Status` varchar(20) NOT NULL,
    `StatusChangedAt` datetime(6) NOT NULL,
    `PublishedAt` datetime(6) NULL,
    `ClosedAt` datetime(6) NULL,
    `CreatedByUserId` varchar(255) NOT NULL,
    `ClonedFromAdvertId` int NULL,
    `Version` char(36) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `CreatedBy` varchar(256) NULL,
    `UpdatedAt` datetime(6) NULL,
    `UpdatedBy` varchar(256) NULL,
    `IsDeleted` tinyint(1) NOT NULL,
    `DeletedAt` datetime(6) NULL,
    `DeletedBy` varchar(256) NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_JobAdverts_Departments_DepartmentId` FOREIGN KEY (`DepartmentId`) REFERENCES `Departments` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_JobAdverts_Divisions_DivisionId` FOREIGN KEY (`DivisionId`) REFERENCES `Divisions` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_JobAdverts_JobAdverts_ClonedFromAdvertId` FOREIGN KEY (`ClonedFromAdvertId`) REFERENCES `JobAdverts` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_JobAdverts_Users_CreatedByUserId` FOREIGN KEY (`CreatedByUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_JobAdverts_Users_RequestingHodUserId` FOREIGN KEY (`RequestingHodUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_JobAdverts_Vacancies_VacancyId` FOREIGN KEY (`VacancyId`) REFERENCES `Vacancies` (`Id`) ON DELETE RESTRICT
);

CREATE TABLE `AdvertChannels` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `JobAdvertId` int NOT NULL,
    `Channel` varchar(30) NOT NULL,
    `Status` varchar(20) NOT NULL,
    `Reference` varchar(500) NULL,
    `PostedAt` datetime(6) NULL,
    `PostedByName` varchar(150) NULL,
    `RemovedAt` datetime(6) NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_AdvertChannels_JobAdverts_JobAdvertId` FOREIGN KEY (`JobAdvertId`) REFERENCES `JobAdverts` (`Id`) ON DELETE CASCADE
);

CREATE TABLE `JobAdvertActions` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `JobAdvertId` int NOT NULL,
    `Action` varchar(30) NOT NULL,
    `FromStatus` varchar(20) NOT NULL,
    `ToStatus` varchar(20) NOT NULL,
    `ActorUserId` varchar(255) NULL,
    `ActorName` varchar(150) NOT NULL,
    `Comments` varchar(2000) NULL,
    `ActionAt` datetime(6) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `CreatedBy` varchar(256) NULL,
    `UpdatedAt` datetime(6) NULL,
    `UpdatedBy` varchar(256) NULL,
    `IsDeleted` tinyint(1) NOT NULL,
    `DeletedAt` datetime(6) NULL,
    `DeletedBy` varchar(256) NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_JobAdvertActions_JobAdverts_JobAdvertId` FOREIGN KEY (`JobAdvertId`) REFERENCES `JobAdverts` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_JobAdvertActions_Users_ActorUserId` FOREIGN KEY (`ActorUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
);

CREATE UNIQUE INDEX `IX_AdvertChannels_JobAdvertId_Channel` ON `AdvertChannels` (`JobAdvertId`, `Channel`);

CREATE INDEX `IX_JobAdvertActions_ActorUserId` ON `JobAdvertActions` (`ActorUserId`);

CREATE INDEX `IX_JobAdvertActions_IsDeleted` ON `JobAdvertActions` (`IsDeleted`);

CREATE INDEX `IX_JobAdvertActions_JobAdvertId_ActionAt` ON `JobAdvertActions` (`JobAdvertId`, `ActionAt`);

CREATE UNIQUE INDEX `IX_JobAdverts_AdvertNumber` ON `JobAdverts` (`AdvertNumber`);

CREATE INDEX `IX_JobAdverts_ClonedFromAdvertId` ON `JobAdverts` (`ClonedFromAdvertId`);

CREATE INDEX `IX_JobAdverts_CreatedByUserId` ON `JobAdverts` (`CreatedByUserId`);

CREATE INDEX `IX_JobAdverts_DepartmentId_Status` ON `JobAdverts` (`DepartmentId`, `Status`);

CREATE INDEX `IX_JobAdverts_DivisionId_Status` ON `JobAdverts` (`DivisionId`, `Status`);

CREATE INDEX `IX_JobAdverts_IsDeleted` ON `JobAdverts` (`IsDeleted`);

CREATE INDEX `IX_JobAdverts_RequestingHodUserId` ON `JobAdverts` (`RequestingHodUserId`);

CREATE INDEX `IX_JobAdverts_Status_EndDate` ON `JobAdverts` (`Status`, `EndDate`);

CREATE INDEX `IX_JobAdverts_Status_StartDate` ON `JobAdverts` (`Status`, `StartDate`);

CREATE INDEX `IX_JobAdverts_VacancyId_Status` ON `JobAdverts` (`VacancyId`, `Status`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20261104124907_Phase04_JobAdverts', '10.0.14');

COMMIT;

