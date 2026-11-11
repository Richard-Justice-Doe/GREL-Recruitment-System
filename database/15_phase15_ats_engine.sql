START TRANSACTION;
ALTER TABLE `JobApplications` ADD `AtsClassification` varchar(30) NULL;

ALTER TABLE `JobApplications` ADD `AtsDecision` varchar(20) NOT NULL DEFAULT 'None';

ALTER TABLE `JobApplications` ADD `AtsDecisionAt` datetime(6) NULL;

ALTER TABLE `JobApplications` ADD `AtsDecisionByUserId` varchar(255) NULL;

ALTER TABLE `JobApplications` ADD `AtsDecisionNote` text NULL;

ALTER TABLE `JobApplications` ADD `AtsRejectionReason` text NULL;

ALTER TABLE `JobApplications` ADD `CvParsed` mediumtext NULL;

ALTER TABLE `JobApplications` ADD `CvText` mediumtext NULL;

CREATE TABLE `AtsSettings` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `EducationWeight` int NOT NULL,
    `ExperienceWeight` int NOT NULL,
    `SkillsWeight` int NOT NULL,
    `CertificationWeight` int NOT NULL,
    `HighlyRecommendedFrom` int NOT NULL,
    `RecommendedFrom` int NOT NULL,
    `ReviewRequiredFrom` int NOT NULL,
    `LowMatchFrom` int NOT NULL,
    `RejectOnKnockout` tinyint(1) NOT NULL,
    `RejectBelowLowMatch` tinyint(1) NOT NULL,
    `RejectBelowMinimumEducation` tinyint(1) NOT NULL,
    `RejectBelowMinimumExperience` tinyint(1) NOT NULL,
    `RejectMissingRequiredCertification` tinyint(1) NOT NULL,
    `RejectMissingRequiredSkill` tinyint(1) NOT NULL,
    `UseAiReader` tinyint(1) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `CreatedBy` varchar(256) NULL,
    `UpdatedAt` datetime(6) NULL,
    `UpdatedBy` varchar(256) NULL,
    `IsDeleted` tinyint(1) NOT NULL,
    `DeletedAt` datetime(6) NULL,
    `DeletedBy` varchar(256) NULL,
    PRIMARY KEY (`Id`)
);

CREATE TABLE `JobApplicationKnockoutAnswers` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `JobApplicationId` int NOT NULL,
    `Question` varchar(300) NOT NULL,
    `Answer` tinyint(1) NOT NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_JobApplicationKnockoutAnswers_JobApplications_JobApplication~` FOREIGN KEY (`JobApplicationId`) REFERENCES `JobApplications` (`Id`) ON DELETE RESTRICT
);

CREATE TABLE `VacancyKnockoutQuestions` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `VacancyId` int NOT NULL,
    `Text` varchar(300) NOT NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_VacancyKnockoutQuestions_Vacancies_VacancyId` FOREIGN KEY (`VacancyId`) REFERENCES `Vacancies` (`Id`) ON DELETE CASCADE
);

CREATE INDEX `IX_JobApplications_AtsDecisionByUserId` ON `JobApplications` (`AtsDecisionByUserId`);

CREATE INDEX `IX_JobApplications_VacancyId_AtsDecision` ON `JobApplications` (`VacancyId`, `AtsDecision`);

CREATE INDEX `IX_AtsSettings_IsDeleted` ON `AtsSettings` (`IsDeleted`);

CREATE INDEX `IX_JobApplicationKnockoutAnswers_JobApplicationId` ON `JobApplicationKnockoutAnswers` (`JobApplicationId`);

CREATE INDEX `IX_VacancyKnockoutQuestions_VacancyId` ON `VacancyKnockoutQuestions` (`VacancyId`);

ALTER TABLE `JobApplications` ADD CONSTRAINT `FK_JobApplications_Users_AtsDecisionByUserId` FOREIGN KEY (`AtsDecisionByUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20261105094620_Phase15_AtsEngine', '10.0.14');

COMMIT;

