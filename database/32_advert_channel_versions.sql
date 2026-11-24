START TRANSACTION;
ALTER TABLE `JobAdverts` ADD `ExternalText` text NULL;

ALTER TABLE `JobAdverts` ADD `InternalNotice` text NULL;

ALTER TABLE `JobAdverts` ADD `LinkedInText` varchar(3000) NULL;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20261113162316_AdvertChannelVersions', '10.0.14');

COMMIT;

