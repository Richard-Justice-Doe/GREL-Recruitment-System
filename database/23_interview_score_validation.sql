START TRANSACTION;
ALTER TABLE `Scorecards` ADD `ReturnReason` text NULL;

ALTER TABLE `Scorecards` ADD `ReturnedAt` datetime(6) NULL;

ALTER TABLE `Scorecards` ADD `ReturnedByUserId` varchar(255) NULL;

ALTER TABLE `Interviews` ADD `ValidatedAt` datetime(6) NULL;

ALTER TABLE `Interviews` ADD `ValidatedByUserId` varchar(255) NULL;

ALTER TABLE `Interviews` ADD `ValidationNote` text NULL;

ALTER TABLE `InterviewPanelMembers` ADD `Attendance` varchar(20) NOT NULL DEFAULT 'NotRecorded';

ALTER TABLE `InterviewPanelMembers` ADD `AttendanceNote` varchar(1000) NULL;

ALTER TABLE `InterviewPanelMembers` ADD `AttendanceRecordedAt` datetime(6) NULL;

ALTER TABLE `InterviewPanelMembers` ADD `AttendanceRecordedByUserId` varchar(255) NULL;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20261109132559_InterviewScoreValidation', '10.0.14');

COMMIT;

