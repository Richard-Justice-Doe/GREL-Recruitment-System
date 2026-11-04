START TRANSACTION;
CREATE TABLE `Vacancies` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `VacancyNumber` varchar(40) NOT NULL,
    `RecruitmentRequestId` int NOT NULL,
    `JobTitle` varchar(150) NOT NULL,
    `DepartmentId` int NOT NULL,
    `UnitId` int NULL,
    `SiteId` int NOT NULL,
    `ReportingManagerUserId` varchar(255) NULL,
    `EmploymentType` varchar(30) NOT NULL,
    `NumberOfPositions` int NOT NULL,
    `VacancyType` varchar(30) NOT NULL,
    `JobDescription` longtext NOT NULL,
    `MinimumEducation` varchar(30) NOT NULL,
    `FieldOfStudy` varchar(200) NULL,
    `QualificationNotes` varchar(1000) NULL,
    `MinYearsExperience` int NOT NULL,
    `ExperienceNotes` varchar(1000) NULL,
    `SalaryMin` decimal(18,2) NULL,
    `SalaryMax` decimal(18,2) NULL,
    `ShowSalary` tinyint(1) NOT NULL,
    `ClosingDate` date NOT NULL,
    `RecruiterUserId` varchar(255) NOT NULL,
    `Status` varchar(30) NOT NULL,
    `StatusChangedAt` datetime(6) NOT NULL,
    `PublishedAt` datetime(6) NULL,
    `ClosedAt` datetime(6) NULL,
    `Version` char(36) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `CreatedBy` varchar(256) NULL,
    `UpdatedAt` datetime(6) NULL,
    `UpdatedBy` varchar(256) NULL,
    `IsDeleted` tinyint(1) NOT NULL,
    `DeletedAt` datetime(6) NULL,
    `DeletedBy` varchar(256) NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Vacancies_Departments_DepartmentId` FOREIGN KEY (`DepartmentId`) REFERENCES `Departments` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_Vacancies_RecruitmentRequests_RecruitmentRequestId` FOREIGN KEY (`RecruitmentRequestId`) REFERENCES `RecruitmentRequests` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_Vacancies_Sites_SiteId` FOREIGN KEY (`SiteId`) REFERENCES `Sites` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_Vacancies_Units_UnitId` FOREIGN KEY (`UnitId`) REFERENCES `Units` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_Vacancies_Users_RecruiterUserId` FOREIGN KEY (`RecruiterUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_Vacancies_Users_ReportingManagerUserId` FOREIGN KEY (`ReportingManagerUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
);

CREATE TABLE `VacancyActions` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `VacancyId` int NOT NULL,
    `Action` varchar(30) NOT NULL,
    `FromStatus` varchar(30) NOT NULL,
    `ToStatus` varchar(30) NOT NULL,
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
    CONSTRAINT `FK_VacancyActions_Users_ActorUserId` FOREIGN KEY (`ActorUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_VacancyActions_Vacancies_VacancyId` FOREIGN KEY (`VacancyId`) REFERENCES `Vacancies` (`Id`) ON DELETE RESTRICT
);

CREATE TABLE `VacancyRequirements` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `VacancyId` int NOT NULL,
    `Type` varchar(20) NOT NULL,
    `Name` varchar(100) NOT NULL,
    `IsEssential` tinyint(1) NOT NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_VacancyRequirements_Vacancies_VacancyId` FOREIGN KEY (`VacancyId`) REFERENCES `Vacancies` (`Id`) ON DELETE CASCADE
);

CREATE INDEX `IX_Vacancies_DepartmentId_Status` ON `Vacancies` (`DepartmentId`, `Status`);

CREATE INDEX `IX_Vacancies_IsDeleted` ON `Vacancies` (`IsDeleted`);

CREATE INDEX `IX_Vacancies_RecruiterUserId` ON `Vacancies` (`RecruiterUserId`);

CREATE UNIQUE INDEX `IX_Vacancies_RecruitmentRequestId` ON `Vacancies` (`RecruitmentRequestId`);

CREATE INDEX `IX_Vacancies_ReportingManagerUserId` ON `Vacancies` (`ReportingManagerUserId`);

CREATE INDEX `IX_Vacancies_SiteId` ON `Vacancies` (`SiteId`);

CREATE INDEX `IX_Vacancies_Status` ON `Vacancies` (`Status`);

CREATE INDEX `IX_Vacancies_Status_ClosingDate` ON `Vacancies` (`Status`, `ClosingDate`);

CREATE INDEX `IX_Vacancies_UnitId` ON `Vacancies` (`UnitId`);

CREATE UNIQUE INDEX `IX_Vacancies_VacancyNumber` ON `Vacancies` (`VacancyNumber`);

CREATE INDEX `IX_VacancyActions_ActorUserId` ON `VacancyActions` (`ActorUserId`);

CREATE INDEX `IX_VacancyActions_IsDeleted` ON `VacancyActions` (`IsDeleted`);

CREATE INDEX `IX_VacancyActions_VacancyId_ActionAt` ON `VacancyActions` (`VacancyId`, `ActionAt`);

CREATE INDEX `IX_VacancyRequirements_Name` ON `VacancyRequirements` (`Name`);

CREATE INDEX `IX_VacancyRequirements_VacancyId_Type` ON `VacancyRequirements` (`VacancyId`, `Type`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20261104060753_Phase03_Vacancies', '10.0.14');

COMMIT;

