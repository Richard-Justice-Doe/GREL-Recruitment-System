START TRANSACTION;
CREATE TABLE `BackgroundChecks` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `JobApplicationId` int NOT NULL,
    `Type` varchar(30) NOT NULL,
    `Subject` varchar(300) NOT NULL,
    `Status` varchar(20) NOT NULL,
    `Notes` text NULL,
    `CompletedAt` datetime(6) NULL,
    `RecordedByUserId` varchar(255) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `CreatedBy` varchar(256) NULL,
    `UpdatedAt` datetime(6) NULL,
    `UpdatedBy` varchar(256) NULL,
    `IsDeleted` tinyint(1) NOT NULL,
    `DeletedAt` datetime(6) NULL,
    `DeletedBy` varchar(256) NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_BackgroundChecks_JobApplications_JobApplicationId` FOREIGN KEY (`JobApplicationId`) REFERENCES `JobApplications` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_BackgroundChecks_Users_RecordedByUserId` FOREIGN KEY (`RecordedByUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
);

CREATE TABLE `HiringRecommendations` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `Number` varchar(40) NOT NULL,
    `JobApplicationId` int NOT NULL,
    `VacancyId` int NOT NULL,
    `DivisionId` int NOT NULL,
    `DepartmentId` int NOT NULL,
    `RequestingHodUserId` varchar(255) NOT NULL,
    `ProposedSalary` decimal(18,2) NOT NULL,
    `ProposedStartDate` date NOT NULL,
    `Justification` text NOT NULL,
    `CreatedByUserId` varchar(255) NOT NULL,
    `Status` varchar(20) NOT NULL,
    `HodDecision` varchar(10) NOT NULL,
    `HodComment` text NULL,
    `HodUserId` varchar(255) NULL,
    `HodDecidedAt` datetime(6) NULL,
    `HrDecision` varchar(10) NOT NULL,
    `HrComment` text NULL,
    `HrUserId` varchar(255) NULL,
    `HrDecidedAt` datetime(6) NULL,
    `MdDecision` varchar(10) NOT NULL,
    `MdComment` text NULL,
    `MdUserId` varchar(255) NULL,
    `MdDecidedAt` datetime(6) NULL,
    `Version` char(36) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `CreatedBy` varchar(256) NULL,
    `UpdatedAt` datetime(6) NULL,
    `UpdatedBy` varchar(256) NULL,
    `IsDeleted` tinyint(1) NOT NULL,
    `DeletedAt` datetime(6) NULL,
    `DeletedBy` varchar(256) NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_HiringRecommendations_Departments_DepartmentId` FOREIGN KEY (`DepartmentId`) REFERENCES `Departments` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_HiringRecommendations_Divisions_DivisionId` FOREIGN KEY (`DivisionId`) REFERENCES `Divisions` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_HiringRecommendations_JobApplications_JobApplicationId` FOREIGN KEY (`JobApplicationId`) REFERENCES `JobApplications` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_HiringRecommendations_Users_CreatedByUserId` FOREIGN KEY (`CreatedByUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_HiringRecommendations_Users_HodUserId` FOREIGN KEY (`HodUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_HiringRecommendations_Users_HrUserId` FOREIGN KEY (`HrUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_HiringRecommendations_Users_MdUserId` FOREIGN KEY (`MdUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_HiringRecommendations_Users_RequestingHodUserId` FOREIGN KEY (`RequestingHodUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_HiringRecommendations_Vacancies_VacancyId` FOREIGN KEY (`VacancyId`) REFERENCES `Vacancies` (`Id`) ON DELETE RESTRICT
);

CREATE TABLE `Offers` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `Number` varchar(40) NOT NULL,
    `JobApplicationId` int NOT NULL,
    `HiringRecommendationId` int NOT NULL,
    `VacancyId` int NOT NULL,
    `DivisionId` int NOT NULL,
    `DepartmentId` int NOT NULL,
    `RequestingHodUserId` varchar(255) NOT NULL,
    `JobTitle` varchar(150) NOT NULL,
    `MonthlySalary` decimal(18,2) NOT NULL,
    `StartDate` date NOT NULL,
    `ExpiresOn` date NOT NULL,
    `Terms` text NULL,
    `Status` varchar(20) NOT NULL,
    `SentAt` datetime(6) NOT NULL,
    `SentByUserId` varchar(255) NOT NULL,
    `RespondedAt` datetime(6) NULL,
    `Reason` text NULL,
    `Version` char(36) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `CreatedBy` varchar(256) NULL,
    `UpdatedAt` datetime(6) NULL,
    `UpdatedBy` varchar(256) NULL,
    `IsDeleted` tinyint(1) NOT NULL,
    `DeletedAt` datetime(6) NULL,
    `DeletedBy` varchar(256) NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Offers_Departments_DepartmentId` FOREIGN KEY (`DepartmentId`) REFERENCES `Departments` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_Offers_Divisions_DivisionId` FOREIGN KEY (`DivisionId`) REFERENCES `Divisions` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_Offers_HiringRecommendations_HiringRecommendationId` FOREIGN KEY (`HiringRecommendationId`) REFERENCES `HiringRecommendations` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_Offers_JobApplications_JobApplicationId` FOREIGN KEY (`JobApplicationId`) REFERENCES `JobApplications` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_Offers_Users_RequestingHodUserId` FOREIGN KEY (`RequestingHodUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_Offers_Users_SentByUserId` FOREIGN KEY (`SentByUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_Offers_Vacancies_VacancyId` FOREIGN KEY (`VacancyId`) REFERENCES `Vacancies` (`Id`) ON DELETE RESTRICT
);

CREATE INDEX `IX_BackgroundChecks_IsDeleted` ON `BackgroundChecks` (`IsDeleted`);

CREATE INDEX `IX_BackgroundChecks_JobApplicationId` ON `BackgroundChecks` (`JobApplicationId`);

CREATE INDEX `IX_BackgroundChecks_RecordedByUserId` ON `BackgroundChecks` (`RecordedByUserId`);

CREATE INDEX `IX_HiringRecommendations_CreatedByUserId` ON `HiringRecommendations` (`CreatedByUserId`);

CREATE INDEX `IX_HiringRecommendations_DepartmentId` ON `HiringRecommendations` (`DepartmentId`);

CREATE INDEX `IX_HiringRecommendations_DivisionId_Status` ON `HiringRecommendations` (`DivisionId`, `Status`);

CREATE INDEX `IX_HiringRecommendations_HodUserId` ON `HiringRecommendations` (`HodUserId`);

CREATE INDEX `IX_HiringRecommendations_HrUserId` ON `HiringRecommendations` (`HrUserId`);

CREATE INDEX `IX_HiringRecommendations_IsDeleted` ON `HiringRecommendations` (`IsDeleted`);

CREATE INDEX `IX_HiringRecommendations_JobApplicationId_Status` ON `HiringRecommendations` (`JobApplicationId`, `Status`);

CREATE INDEX `IX_HiringRecommendations_MdUserId` ON `HiringRecommendations` (`MdUserId`);

CREATE UNIQUE INDEX `IX_HiringRecommendations_Number` ON `HiringRecommendations` (`Number`);

CREATE INDEX `IX_HiringRecommendations_RequestingHodUserId` ON `HiringRecommendations` (`RequestingHodUserId`);

CREATE INDEX `IX_HiringRecommendations_Status_DepartmentId` ON `HiringRecommendations` (`Status`, `DepartmentId`);

CREATE INDEX `IX_HiringRecommendations_VacancyId` ON `HiringRecommendations` (`VacancyId`);

CREATE INDEX `IX_Offers_DepartmentId` ON `Offers` (`DepartmentId`);

CREATE INDEX `IX_Offers_DivisionId_Status` ON `Offers` (`DivisionId`, `Status`);

CREATE INDEX `IX_Offers_HiringRecommendationId` ON `Offers` (`HiringRecommendationId`);

CREATE INDEX `IX_Offers_IsDeleted` ON `Offers` (`IsDeleted`);

CREATE INDEX `IX_Offers_JobApplicationId` ON `Offers` (`JobApplicationId`);

CREATE UNIQUE INDEX `IX_Offers_Number` ON `Offers` (`Number`);

CREATE INDEX `IX_Offers_RequestingHodUserId` ON `Offers` (`RequestingHodUserId`);

CREATE INDEX `IX_Offers_SentByUserId` ON `Offers` (`SentByUserId`);

CREATE INDEX `IX_Offers_Status_ExpiresOn` ON `Offers` (`Status`, `ExpiresOn`);

CREATE INDEX `IX_Offers_VacancyId_Status` ON `Offers` (`VacancyId`, `Status`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20261104165751_Phase08_Hiring', '10.0.14');

COMMIT;

