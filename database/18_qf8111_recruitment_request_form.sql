START TRANSACTION;
ALTER TABLE `Vacancies` ADD `RequisitionNumber` varchar(40) NULL;

ALTER TABLE `RecruitmentRequests` ADD `CertificateRequired` varchar(500) NULL;

ALTER TABLE `RecruitmentRequests` ADD `DrivingLicenceClass` varchar(50) NULL;

ALTER TABLE `RecruitmentRequests` ADD `DrivingLicenceRequired` tinyint(1) NOT NULL DEFAULT FALSE;

ALTER TABLE `RecruitmentRequests` ADD `EducationLevel` varchar(30) NULL;

ALTER TABLE `RecruitmentRequests` ADD `MinYearsExperience` int NULL;

ALTER TABLE `RecruitmentRequests` ADD `PostDescription` text NULL;

ALTER TABLE `RecruitmentRequests` ADD `ProfessionalExperience` text NULL;

ALTER TABLE `RecruitmentRequests` ADD `ProposedStartDate` date NULL;

ALTER TABLE `RecruitmentRequests` ADD `PurposeOther` varchar(200) NULL;

ALTER TABLE `RecruitmentRequests` ADD `QualificationCategory` varchar(200) NULL;

ALTER TABLE `RecruitmentRequests` ADD `Remarks` text NULL;

ALTER TABLE `RecruitmentRequests` ADD `RequestDate` date NULL;

ALTER TABLE `RecruitmentRequests` ADD `StaffCategory` varchar(20) NULL;

ALTER TABLE `RecruitmentRequests` ADD `TrainingLevel` varchar(200) NULL;

CREATE UNIQUE INDEX `IX_Vacancies_RequisitionNumber` ON `Vacancies` (`RequisitionNumber`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20261105193905_Qf8111RecruitmentRequestForm', '10.0.14');

ALTER TABLE `RecruitmentRequests` ADD `DrivingLicence` varchar(20) NOT NULL DEFAULT 'NotNeeded';

UPDATE RecruitmentRequests SET DrivingLicence = 'Mandatory' WHERE DrivingLicenceRequired = 1;

ALTER TABLE `RecruitmentRequests` DROP COLUMN `DrivingLicenceRequired`;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20261106071649_DrivingLicenceRequirement', '10.0.14');

COMMIT;

