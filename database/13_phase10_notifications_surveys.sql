START TRANSACTION;
CREATE TABLE `Notifications` (
    `Id` bigint NOT NULL AUTO_INCREMENT,
    `UserId` varchar(255) NOT NULL,
    `Title` varchar(200) NOT NULL,
    `Body` varchar(500) NULL,
    `Link` varchar(500) NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `ReadAt` datetime(6) NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Notifications_Users_UserId` FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`) ON DELETE CASCADE
);

CREATE TABLE `SentReminders` (
    `Id` bigint NOT NULL AUTO_INCREMENT,
    `Key` varchar(200) NOT NULL,
    `SentAt` datetime(6) NOT NULL,
    PRIMARY KEY (`Id`)
);

CREATE TABLE `SurveyInvitations` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `Type` varchar(30) NOT NULL,
    `UserId` varchar(255) NOT NULL,
    `JobApplicationId` int NOT NULL,
    `OnboardingRecordId` int NULL,
    `VacancyId` int NOT NULL,
    `DivisionId` int NOT NULL,
    `DepartmentId` int NOT NULL,
    `RequestingHodUserId` varchar(255) NOT NULL,
    `Outcome` varchar(20) NOT NULL,
    `SentAt` datetime(6) NOT NULL,
    `ExpiresOn` date NOT NULL,
    `CompletedAt` datetime(6) NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `CreatedBy` varchar(256) NULL,
    `UpdatedAt` datetime(6) NULL,
    `UpdatedBy` varchar(256) NULL,
    `IsDeleted` tinyint(1) NOT NULL,
    `DeletedAt` datetime(6) NULL,
    `DeletedBy` varchar(256) NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_SurveyInvitations_Departments_DepartmentId` FOREIGN KEY (`DepartmentId`) REFERENCES `Departments` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_SurveyInvitations_Divisions_DivisionId` FOREIGN KEY (`DivisionId`) REFERENCES `Divisions` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_SurveyInvitations_JobApplications_JobApplicationId` FOREIGN KEY (`JobApplicationId`) REFERENCES `JobApplications` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_SurveyInvitations_Onboardings_OnboardingRecordId` FOREIGN KEY (`OnboardingRecordId`) REFERENCES `Onboardings` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_SurveyInvitations_Users_RequestingHodUserId` FOREIGN KEY (`RequestingHodUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_SurveyInvitations_Users_UserId` FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_SurveyInvitations_Vacancies_VacancyId` FOREIGN KEY (`VacancyId`) REFERENCES `Vacancies` (`Id`) ON DELETE RESTRICT
);

CREATE TABLE `SurveyAnswers` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `SurveyInvitationId` int NOT NULL,
    `QuestionKey` varchar(30) NOT NULL,
    `Rating` int NULL,
    `Text` text NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `CreatedBy` varchar(256) NULL,
    `UpdatedAt` datetime(6) NULL,
    `UpdatedBy` varchar(256) NULL,
    `IsDeleted` tinyint(1) NOT NULL,
    `DeletedAt` datetime(6) NULL,
    `DeletedBy` varchar(256) NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_SurveyAnswers_SurveyInvitations_SurveyInvitationId` FOREIGN KEY (`SurveyInvitationId`) REFERENCES `SurveyInvitations` (`Id`) ON DELETE RESTRICT
);

CREATE INDEX `IX_Notifications_UserId_Id` ON `Notifications` (`UserId`, `Id`);

CREATE INDEX `IX_Notifications_UserId_ReadAt` ON `Notifications` (`UserId`, `ReadAt`);

CREATE UNIQUE INDEX `IX_SentReminders_Key` ON `SentReminders` (`Key`);

CREATE INDEX `IX_SurveyAnswers_IsDeleted` ON `SurveyAnswers` (`IsDeleted`);

CREATE UNIQUE INDEX `IX_SurveyAnswers_SurveyInvitationId_QuestionKey` ON `SurveyAnswers` (`SurveyInvitationId`, `QuestionKey`);

CREATE INDEX `IX_SurveyInvitations_DepartmentId` ON `SurveyInvitations` (`DepartmentId`);

CREATE INDEX `IX_SurveyInvitations_DivisionId` ON `SurveyInvitations` (`DivisionId`);

CREATE INDEX `IX_SurveyInvitations_IsDeleted` ON `SurveyInvitations` (`IsDeleted`);

CREATE UNIQUE INDEX `IX_SurveyInvitations_JobApplicationId_Type` ON `SurveyInvitations` (`JobApplicationId`, `Type`);

CREATE INDEX `IX_SurveyInvitations_OnboardingRecordId` ON `SurveyInvitations` (`OnboardingRecordId`);

CREATE INDEX `IX_SurveyInvitations_RequestingHodUserId` ON `SurveyInvitations` (`RequestingHodUserId`);

CREATE INDEX `IX_SurveyInvitations_Type_DivisionId_DepartmentId` ON `SurveyInvitations` (`Type`, `DivisionId`, `DepartmentId`);

CREATE INDEX `IX_SurveyInvitations_UserId_CompletedAt` ON `SurveyInvitations` (`UserId`, `CompletedAt`);

CREATE INDEX `IX_SurveyInvitations_VacancyId` ON `SurveyInvitations` (`VacancyId`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20261104190146_Phase10_NotificationsAndSurveys', '10.0.14');

COMMIT;

