START TRANSACTION;
CREATE TABLE `CandidateProfiles` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `UserId` varchar(255) NOT NULL,
    `Phone` varchar(30) NULL,
    `Location` varchar(150) NULL,
    `HighestEducation` varchar(30) NOT NULL,
    `FieldOfStudy` varchar(200) NULL,
    `YearsExperience` int NOT NULL,
    `CurrentEmployer` varchar(150) NULL,
    `CurrentPosition` varchar(150) NULL,
    `Skills` varchar(3000) NULL,
    `Certifications` varchar(3000) NULL,
    `LinkedInUrl` varchar(300) NULL,
    `Summary` varchar(2000) NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `CreatedBy` varchar(256) NULL,
    `UpdatedAt` datetime(6) NULL,
    `UpdatedBy` varchar(256) NULL,
    `IsDeleted` tinyint(1) NOT NULL,
    `DeletedAt` datetime(6) NULL,
    `DeletedBy` varchar(256) NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_CandidateProfiles_Users_UserId` FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
);

CREATE TABLE `JobApplications` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `ApplicationNumber` varchar(40) NOT NULL,
    `VacancyId` int NOT NULL,
    `JobAdvertId` int NULL,
    `ApplicantUserId` varchar(255) NOT NULL,
    `IsInternal` tinyint(1) NOT NULL,
    `Source` varchar(30) NOT NULL,
    `DivisionId` int NOT NULL,
    `DepartmentId` int NOT NULL,
    `RequestingHodUserId` varchar(255) NOT NULL,
    `FullName` varchar(150) NOT NULL,
    `Email` varchar(256) NOT NULL,
    `Phone` varchar(30) NOT NULL,
    `Location` varchar(150) NULL,
    `HighestEducation` varchar(30) NOT NULL,
    `FieldOfStudy` varchar(200) NULL,
    `YearsExperience` int NOT NULL,
    `CurrentEmployer` varchar(150) NULL,
    `CurrentPosition` varchar(150) NULL,
    `Skills` varchar(3000) NULL,
    `Certifications` varchar(3000) NULL,
    `LinkedInUrl` varchar(300) NULL,
    `CoverLetter` varchar(4000) NULL,
    `Status` varchar(30) NOT NULL,
    `SubmittedAt` datetime(6) NOT NULL,
    `StatusChangedAt` datetime(6) NOT NULL,
    `OutcomeReason` varchar(2000) NULL,
    `Version` char(36) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `CreatedBy` varchar(256) NULL,
    `UpdatedAt` datetime(6) NULL,
    `UpdatedBy` varchar(256) NULL,
    `IsDeleted` tinyint(1) NOT NULL,
    `DeletedAt` datetime(6) NULL,
    `DeletedBy` varchar(256) NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_JobApplications_Departments_DepartmentId` FOREIGN KEY (`DepartmentId`) REFERENCES `Departments` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_JobApplications_Divisions_DivisionId` FOREIGN KEY (`DivisionId`) REFERENCES `Divisions` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_JobApplications_JobAdverts_JobAdvertId` FOREIGN KEY (`JobAdvertId`) REFERENCES `JobAdverts` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_JobApplications_Users_ApplicantUserId` FOREIGN KEY (`ApplicantUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_JobApplications_Users_RequestingHodUserId` FOREIGN KEY (`RequestingHodUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_JobApplications_Vacancies_VacancyId` FOREIGN KEY (`VacancyId`) REFERENCES `Vacancies` (`Id`) ON DELETE RESTRICT
);

CREATE TABLE `JobApplicationActions` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `JobApplicationId` int NOT NULL,
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
    CONSTRAINT `FK_JobApplicationActions_JobApplications_JobApplicationId` FOREIGN KEY (`JobApplicationId`) REFERENCES `JobApplications` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_JobApplicationActions_Users_ActorUserId` FOREIGN KEY (`ActorUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
);

CREATE INDEX `IX_CandidateProfiles_IsDeleted` ON `CandidateProfiles` (`IsDeleted`);

CREATE UNIQUE INDEX `IX_CandidateProfiles_UserId` ON `CandidateProfiles` (`UserId`);

CREATE INDEX `IX_JobApplicationActions_ActorUserId` ON `JobApplicationActions` (`ActorUserId`);

CREATE INDEX `IX_JobApplicationActions_IsDeleted` ON `JobApplicationActions` (`IsDeleted`);

CREATE INDEX `IX_JobApplicationActions_JobApplicationId_ActionAt` ON `JobApplicationActions` (`JobApplicationId`, `ActionAt`);

CREATE INDEX `IX_JobApplications_ApplicantUserId` ON `JobApplications` (`ApplicantUserId`);

CREATE UNIQUE INDEX `IX_JobApplications_ApplicationNumber` ON `JobApplications` (`ApplicationNumber`);

CREATE INDEX `IX_JobApplications_DepartmentId_Status` ON `JobApplications` (`DepartmentId`, `Status`);

CREATE INDEX `IX_JobApplications_DivisionId_Status` ON `JobApplications` (`DivisionId`, `Status`);

CREATE INDEX `IX_JobApplications_IsDeleted` ON `JobApplications` (`IsDeleted`);

CREATE INDEX `IX_JobApplications_JobAdvertId` ON `JobApplications` (`JobAdvertId`);

CREATE INDEX `IX_JobApplications_RequestingHodUserId` ON `JobApplications` (`RequestingHodUserId`);

CREATE INDEX `IX_JobApplications_SubmittedAt` ON `JobApplications` (`SubmittedAt`);

CREATE UNIQUE INDEX `IX_JobApplications_VacancyId_ApplicantUserId` ON `JobApplications` (`VacancyId`, `ApplicantUserId`);

CREATE INDEX `IX_JobApplications_VacancyId_Status` ON `JobApplications` (`VacancyId`, `Status`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20261104142539_Phase05_Applications', '10.0.14');

COMMIT;

