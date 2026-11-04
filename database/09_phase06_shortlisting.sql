START TRANSACTION;
ALTER TABLE `JobApplications` MODIFY `Skills` text NULL;

ALTER TABLE `JobApplications` MODIFY `OutcomeReason` text NULL;

ALTER TABLE `JobApplications` MODIFY `CoverLetter` text NULL;

ALTER TABLE `JobApplications` MODIFY `Certifications` text NULL;

ALTER TABLE `JobApplications` ADD `HodNote` text NULL;

ALTER TABLE `JobApplications` ADD `HodRecommendation` varchar(20) NOT NULL DEFAULT 'None';

ALTER TABLE `JobApplications` ADD `HodRecommendedAt` datetime(6) NULL;

ALTER TABLE `JobApplications` ADD `HodRecommendedByUserId` varchar(255) NULL;

ALTER TABLE `JobApplications` ADD `MeetsMinimum` tinyint(1) NULL;

ALTER TABLE `JobApplications` ADD `RecruiterNote` text NULL;

ALTER TABLE `JobApplications` ADD `RecruiterRecommendation` varchar(20) NOT NULL DEFAULT 'None';

ALTER TABLE `JobApplications` ADD `RecruiterRecommendedAt` datetime(6) NULL;

ALTER TABLE `JobApplications` ADD `RecruiterRecommendedByUserId` varchar(255) NULL;

ALTER TABLE `JobApplications` ADD `ScreenedAt` datetime(6) NULL;

ALTER TABLE `JobApplications` ADD `ScreeningDetails` text NULL;

ALTER TABLE `JobApplications` ADD `ScreeningScore` int NULL;

ALTER TABLE `JobApplications` ADD `ShortlistedAt` datetime(6) NULL;

CREATE INDEX `IX_JobApplications_HodRecommendedByUserId` ON `JobApplications` (`HodRecommendedByUserId`);

CREATE INDEX `IX_JobApplications_RecruiterRecommendedByUserId` ON `JobApplications` (`RecruiterRecommendedByUserId`);

CREATE INDEX `IX_JobApplications_VacancyId_ScreeningScore` ON `JobApplications` (`VacancyId`, `ScreeningScore`);

ALTER TABLE `JobApplications` ADD CONSTRAINT `FK_JobApplications_Users_HodRecommendedByUserId` FOREIGN KEY (`HodRecommendedByUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT;

ALTER TABLE `JobApplications` ADD CONSTRAINT `FK_JobApplications_Users_RecruiterRecommendedByUserId` FOREIGN KEY (`RecruiterRecommendedByUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20261104151518_Phase06_Shortlisting', '10.0.14');

COMMIT;

