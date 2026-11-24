START TRANSACTION;
ALTER TABLE `Interviews` ADD `QuorumRequired` tinyint(1) NOT NULL DEFAULT FALSE;

ALTER TABLE `InterviewPanelMembers` ADD `DeclineReason` varchar(500) NULL;

ALTER TABLE `InterviewPanelMembers` ADD `IsHodDelegate` tinyint(1) NOT NULL DEFAULT FALSE;

ALTER TABLE `InterviewPanelMembers` ADD `RespondedAt` datetime(6) NULL;

ALTER TABLE `InterviewPanelMembers` ADD `Response` varchar(20) NOT NULL DEFAULT 'Pending';

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20261112215108_InterviewPanelReplies', '10.0.14');

COMMIT;

