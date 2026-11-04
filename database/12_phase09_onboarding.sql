START TRANSACTION;
CREATE TABLE `Onboardings` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `Number` varchar(40) NOT NULL,
    `JobApplicationId` int NOT NULL,
    `OfferId` int NOT NULL,
    `VacancyId` int NOT NULL,
    `DivisionId` int NOT NULL,
    `DepartmentId` int NOT NULL,
    `RequestingHodUserId` varchar(255) NOT NULL,
    `SiteId` int NOT NULL,
    `EmployeeUserId` varchar(255) NOT NULL,
    `EmployeeName` varchar(200) NOT NULL,
    `JobTitle` varchar(150) NOT NULL,
    `MonthlySalary` decimal(18,2) NOT NULL,
    `EmployeeNumber` varchar(30) NULL,
    `StartDate` date NOT NULL,
    `ReportTime` time NOT NULL,
    `ReportLocation` varchar(300) NOT NULL,
    `ContactPersonUserId` varchar(255) NULL,
    `FirstDayNotes` text NULL,
    `LetterNumber` varchar(40) NULL,
    `LetterTerms` text NULL,
    `LetterIssuedAt` datetime(6) NULL,
    `LetterIssuedByUserId` varchar(255) NULL,
    `LetterAcknowledgedAt` datetime(6) NULL,
    `LetterAcknowledgedName` varchar(200) NULL,
    `LetterAcknowledgedIp` varchar(64) NULL,
    `Status` varchar(20) NOT NULL,
    `StatusChangedAt` datetime(6) NOT NULL,
    `CreatedByUserId` varchar(255) NOT NULL,
    `CancelReason` text NULL,
    `Version` char(36) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `CreatedBy` varchar(256) NULL,
    `UpdatedAt` datetime(6) NULL,
    `UpdatedBy` varchar(256) NULL,
    `IsDeleted` tinyint(1) NOT NULL,
    `DeletedAt` datetime(6) NULL,
    `DeletedBy` varchar(256) NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Onboardings_Departments_DepartmentId` FOREIGN KEY (`DepartmentId`) REFERENCES `Departments` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_Onboardings_Divisions_DivisionId` FOREIGN KEY (`DivisionId`) REFERENCES `Divisions` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_Onboardings_JobApplications_JobApplicationId` FOREIGN KEY (`JobApplicationId`) REFERENCES `JobApplications` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_Onboardings_Offers_OfferId` FOREIGN KEY (`OfferId`) REFERENCES `Offers` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_Onboardings_Sites_SiteId` FOREIGN KEY (`SiteId`) REFERENCES `Sites` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_Onboardings_Users_ContactPersonUserId` FOREIGN KEY (`ContactPersonUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_Onboardings_Users_CreatedByUserId` FOREIGN KEY (`CreatedByUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_Onboardings_Users_EmployeeUserId` FOREIGN KEY (`EmployeeUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_Onboardings_Users_LetterIssuedByUserId` FOREIGN KEY (`LetterIssuedByUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_Onboardings_Users_RequestingHodUserId` FOREIGN KEY (`RequestingHodUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_Onboardings_Vacancies_VacancyId` FOREIGN KEY (`VacancyId`) REFERENCES `Vacancies` (`Id`) ON DELETE RESTRICT
);

CREATE TABLE `OnboardingTasks` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `OnboardingRecordId` int NOT NULL,
    `Team` varchar(30) NOT NULL,
    `Title` varchar(300) NOT NULL,
    `DueDate` date NULL,
    `AssignedToUserId` varchar(255) NULL,
    `Status` varchar(20) NOT NULL,
    `Notes` text NULL,
    `CompletedAt` datetime(6) NULL,
    `CompletedByUserId` varchar(255) NULL,
    `SortOrder` int NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `CreatedBy` varchar(256) NULL,
    `UpdatedAt` datetime(6) NULL,
    `UpdatedBy` varchar(256) NULL,
    `IsDeleted` tinyint(1) NOT NULL,
    `DeletedAt` datetime(6) NULL,
    `DeletedBy` varchar(256) NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_OnboardingTasks_Onboardings_OnboardingRecordId` FOREIGN KEY (`OnboardingRecordId`) REFERENCES `Onboardings` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_OnboardingTasks_Users_AssignedToUserId` FOREIGN KEY (`AssignedToUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_OnboardingTasks_Users_CompletedByUserId` FOREIGN KEY (`CompletedByUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
);

CREATE INDEX `IX_Onboardings_ContactPersonUserId` ON `Onboardings` (`ContactPersonUserId`);

CREATE INDEX `IX_Onboardings_CreatedByUserId` ON `Onboardings` (`CreatedByUserId`);

CREATE INDEX `IX_Onboardings_DepartmentId_Status` ON `Onboardings` (`DepartmentId`, `Status`);

CREATE INDEX `IX_Onboardings_DivisionId_Status` ON `Onboardings` (`DivisionId`, `Status`);

CREATE INDEX `IX_Onboardings_EmployeeUserId` ON `Onboardings` (`EmployeeUserId`);

CREATE INDEX `IX_Onboardings_IsDeleted` ON `Onboardings` (`IsDeleted`);

CREATE UNIQUE INDEX `IX_Onboardings_JobApplicationId` ON `Onboardings` (`JobApplicationId`);

CREATE INDEX `IX_Onboardings_LetterIssuedByUserId` ON `Onboardings` (`LetterIssuedByUserId`);

CREATE UNIQUE INDEX `IX_Onboardings_LetterNumber` ON `Onboardings` (`LetterNumber`);

CREATE UNIQUE INDEX `IX_Onboardings_Number` ON `Onboardings` (`Number`);

CREATE UNIQUE INDEX `IX_Onboardings_OfferId` ON `Onboardings` (`OfferId`);

CREATE INDEX `IX_Onboardings_RequestingHodUserId` ON `Onboardings` (`RequestingHodUserId`);

CREATE INDEX `IX_Onboardings_SiteId` ON `Onboardings` (`SiteId`);

CREATE INDEX `IX_Onboardings_Status_StartDate` ON `Onboardings` (`Status`, `StartDate`);

CREATE INDEX `IX_Onboardings_VacancyId` ON `Onboardings` (`VacancyId`);

CREATE INDEX `IX_OnboardingTasks_AssignedToUserId_Status` ON `OnboardingTasks` (`AssignedToUserId`, `Status`);

CREATE INDEX `IX_OnboardingTasks_CompletedByUserId` ON `OnboardingTasks` (`CompletedByUserId`);

CREATE INDEX `IX_OnboardingTasks_IsDeleted` ON `OnboardingTasks` (`IsDeleted`);

CREATE INDEX `IX_OnboardingTasks_OnboardingRecordId_SortOrder` ON `OnboardingTasks` (`OnboardingRecordId`, `SortOrder`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20261104180859_Phase09_Onboarding', '10.0.14');

COMMIT;

