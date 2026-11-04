START TRANSACTION;
CREATE TABLE `Interviews` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `JobApplicationId` int NOT NULL,
    `VacancyId` int NOT NULL,
    `DivisionId` int NOT NULL,
    `DepartmentId` int NOT NULL,
    `RequestingHodUserId` varchar(255) NOT NULL,
    `Round` int NOT NULL,
    `Type` varchar(20) NOT NULL,
    `Mode` varchar(20) NOT NULL,
    `StartsAt` datetime(6) NOT NULL,
    `DurationMinutes` int NOT NULL,
    `Location` varchar(500) NOT NULL,
    `CandidateInstructions` text NULL,
    `Status` varchar(20) NOT NULL,
    `StatusReason` text NULL,
    `ScheduledByUserId` varchar(255) NOT NULL,
    `Version` char(36) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `CreatedBy` varchar(256) NULL,
    `UpdatedAt` datetime(6) NULL,
    `UpdatedBy` varchar(256) NULL,
    `IsDeleted` tinyint(1) NOT NULL,
    `DeletedAt` datetime(6) NULL,
    `DeletedBy` varchar(256) NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Interviews_Departments_DepartmentId` FOREIGN KEY (`DepartmentId`) REFERENCES `Departments` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_Interviews_Divisions_DivisionId` FOREIGN KEY (`DivisionId`) REFERENCES `Divisions` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_Interviews_JobApplications_JobApplicationId` FOREIGN KEY (`JobApplicationId`) REFERENCES `JobApplications` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_Interviews_Users_RequestingHodUserId` FOREIGN KEY (`RequestingHodUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_Interviews_Users_ScheduledByUserId` FOREIGN KEY (`ScheduledByUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_Interviews_Vacancies_VacancyId` FOREIGN KEY (`VacancyId`) REFERENCES `Vacancies` (`Id`) ON DELETE RESTRICT
);

CREATE TABLE `InterviewCriteria` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `InterviewId` int NOT NULL,
    `Name` varchar(150) NOT NULL,
    `Weight` int NOT NULL,
    `SortOrder` int NOT NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_InterviewCriteria_Interviews_InterviewId` FOREIGN KEY (`InterviewId`) REFERENCES `Interviews` (`Id`) ON DELETE CASCADE
);

CREATE TABLE `InterviewPanelMembers` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `InterviewId` int NOT NULL,
    `UserId` varchar(255) NOT NULL,
    `IsChair` tinyint(1) NOT NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_InterviewPanelMembers_Interviews_InterviewId` FOREIGN KEY (`InterviewId`) REFERENCES `Interviews` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_InterviewPanelMembers_Users_UserId` FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
);

CREATE TABLE `Scorecards` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `InterviewId` int NOT NULL,
    `PanelUserId` varchar(255) NOT NULL,
    `Recommendation` varchar(30) NOT NULL,
    `Strengths` text NULL,
    `Concerns` text NULL,
    `SubmittedAt` datetime(6) NOT NULL,
    `WeightedScore` decimal(5,1) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `CreatedBy` varchar(256) NULL,
    `UpdatedAt` datetime(6) NULL,
    `UpdatedBy` varchar(256) NULL,
    `IsDeleted` tinyint(1) NOT NULL,
    `DeletedAt` datetime(6) NULL,
    `DeletedBy` varchar(256) NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Scorecards_Interviews_InterviewId` FOREIGN KEY (`InterviewId`) REFERENCES `Interviews` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_Scorecards_Users_PanelUserId` FOREIGN KEY (`PanelUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
);

CREATE TABLE `ScorecardRatings` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `ScorecardId` int NOT NULL,
    `CriterionId` int NOT NULL,
    `Score` int NOT NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_ScorecardRatings_InterviewCriteria_CriterionId` FOREIGN KEY (`CriterionId`) REFERENCES `InterviewCriteria` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_ScorecardRatings_Scorecards_ScorecardId` FOREIGN KEY (`ScorecardId`) REFERENCES `Scorecards` (`Id`) ON DELETE CASCADE
);

CREATE INDEX `IX_InterviewCriteria_InterviewId` ON `InterviewCriteria` (`InterviewId`);

CREATE UNIQUE INDEX `IX_InterviewPanelMembers_InterviewId_UserId` ON `InterviewPanelMembers` (`InterviewId`, `UserId`);

CREATE INDEX `IX_InterviewPanelMembers_UserId` ON `InterviewPanelMembers` (`UserId`);

CREATE INDEX `IX_Interviews_DepartmentId_Status` ON `Interviews` (`DepartmentId`, `Status`);

CREATE INDEX `IX_Interviews_DivisionId_Status` ON `Interviews` (`DivisionId`, `Status`);

CREATE INDEX `IX_Interviews_IsDeleted` ON `Interviews` (`IsDeleted`);

CREATE UNIQUE INDEX `IX_Interviews_JobApplicationId_Round` ON `Interviews` (`JobApplicationId`, `Round`);

CREATE INDEX `IX_Interviews_RequestingHodUserId` ON `Interviews` (`RequestingHodUserId`);

CREATE INDEX `IX_Interviews_ScheduledByUserId` ON `Interviews` (`ScheduledByUserId`);

CREATE INDEX `IX_Interviews_Status_StartsAt` ON `Interviews` (`Status`, `StartsAt`);

CREATE INDEX `IX_Interviews_VacancyId_Status` ON `Interviews` (`VacancyId`, `Status`);

CREATE INDEX `IX_ScorecardRatings_CriterionId` ON `ScorecardRatings` (`CriterionId`);

CREATE UNIQUE INDEX `IX_ScorecardRatings_ScorecardId_CriterionId` ON `ScorecardRatings` (`ScorecardId`, `CriterionId`);

CREATE UNIQUE INDEX `IX_Scorecards_InterviewId_PanelUserId` ON `Scorecards` (`InterviewId`, `PanelUserId`);

CREATE INDEX `IX_Scorecards_IsDeleted` ON `Scorecards` (`IsDeleted`);

CREATE INDEX `IX_Scorecards_PanelUserId` ON `Scorecards` (`PanelUserId`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20261104154613_Phase07_Interviews', '10.0.14');

COMMIT;

