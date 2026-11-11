START TRANSACTION;
ALTER TABLE `JobAdverts` MODIFY `Status` varchar(30) NOT NULL;

ALTER TABLE `JobAdverts` ADD `HrReviewedAt` datetime(6) NULL;

ALTER TABLE `JobAdverts` ADD `HrReviewedByUserId` varchar(255) NULL;

ALTER TABLE `JobAdverts` ADD `ItApprovedAt` datetime(6) NULL;

ALTER TABLE `JobAdverts` ADD `ItApprovedByUserId` varchar(255) NULL;

ALTER TABLE `JobAdverts` ADD `ReviewComment` varchar(1000) NULL;

ALTER TABLE `JobAdverts` ADD `SubmittedAt` datetime(6) NULL;

ALTER TABLE `JobAdverts` ADD `SubmittedByUserId` varchar(255) NULL;

ALTER TABLE `JobAdvertActions` MODIFY `ToStatus` varchar(30) NOT NULL;

ALTER TABLE `JobAdvertActions` MODIFY `FromStatus` varchar(30) NOT NULL;

INSERT INTO `Roles` (`Id`, `ConcurrencyStamp`, `Name`, `NormalizedName`)
VALUES ('it-manager', 'it-manager', 'IT Manager', 'IT MANAGER');
SELECT ROW_COUNT();


INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20261111105459_ItManagerAdvertApproval', '10.0.14');

COMMIT;

