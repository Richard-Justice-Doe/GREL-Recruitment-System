START TRANSACTION;
ALTER TABLE `Onboardings` ADD `CorrectionMessage` text NULL;

ALTER TABLE `Onboardings` ADD `CorrectionRequestedAt` datetime(6) NULL;

ALTER TABLE `Onboardings` ADD `DocumentsDueDate` date NULL;

ALTER TABLE `Onboardings` ADD `DocumentsDueDateExtended` tinyint(1) NOT NULL DEFAULT FALSE;

ALTER TABLE `Onboardings` ADD `IsExpatriate` tinyint(1) NOT NULL DEFAULT FALSE;

ALTER TABLE `EmployeeDocuments` ADD `IsApplicable` tinyint(1) NOT NULL DEFAULT TRUE;

ALTER TABLE `EmployeeDocuments` ADD `RejectionCount` int NOT NULL DEFAULT 0;

ALTER TABLE `EmployeeDocuments` ADD `RejectionReasonCode` varchar(20) NULL;

ALTER TABLE `EmployeeDocuments` ADD `ReviewStartedAt` datetime(6) NULL;

ALTER TABLE `EmployeeDocuments` ADD `ReviewStartedByUserId` varchar(255) NULL;

ALTER TABLE `EmployeeDocuments` ADD `Version` char(36) NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

CREATE TABLE `EmployeeDocumentEvents` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `EmployeeDocumentId` int NOT NULL,
    `OnboardingRecordId` int NOT NULL,
    `Action` varchar(30) NOT NULL,
    `FromStatus` varchar(20) NOT NULL,
    `ToStatus` varchar(20) NOT NULL,
    `AttachmentId` int NULL,
    `FileName` varchar(200) NULL,
    `RejectionReason` varchar(20) NULL,
    `Comments` text NULL,
    `ActorUserId` varchar(255) NOT NULL,
    `ActorName` varchar(200) NOT NULL,
    `IpAddress` varchar(64) NULL,
    `ActionAt` datetime(6) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `CreatedBy` varchar(256) NULL,
    `UpdatedAt` datetime(6) NULL,
    `UpdatedBy` varchar(256) NULL,
    `IsDeleted` tinyint(1) NOT NULL,
    `DeletedAt` datetime(6) NULL,
    `DeletedBy` varchar(256) NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_EmployeeDocumentEvents_EmployeeDocuments_EmployeeDocumentId` FOREIGN KEY (`EmployeeDocumentId`) REFERENCES `EmployeeDocuments` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_EmployeeDocumentEvents_Onboardings_OnboardingRecordId` FOREIGN KEY (`OnboardingRecordId`) REFERENCES `Onboardings` (`Id`) ON DELETE RESTRICT
);

CREATE TABLE `OnboardingDocumentTypes` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `Code` varchar(40) NOT NULL,
    `Name` varchar(150) NOT NULL,
    `Description` varchar(500) NULL,
    `Group` varchar(20) NOT NULL,
    `Requirement` varchar(20) NOT NULL,
    `AllowsWordFiles` tinyint(1) NOT NULL,
    `MinFiles` int NOT NULL,
    `PersonnelFileCategoryId` int NOT NULL,
    `SortOrder` int NOT NULL,
    `IsActive` tinyint(1) NOT NULL,
    `IsLegacy` tinyint(1) NOT NULL,
    `Version` char(36) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `CreatedBy` varchar(256) NULL,
    `UpdatedAt` datetime(6) NULL,
    `UpdatedBy` varchar(256) NULL,
    `IsDeleted` tinyint(1) NOT NULL,
    `DeletedAt` datetime(6) NULL,
    `DeletedBy` varchar(256) NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_OnboardingDocumentTypes_PersonnelFileCategories_PersonnelFil~` FOREIGN KEY (`PersonnelFileCategoryId`) REFERENCES `PersonnelFileCategories` (`Id`) ON DELETE RESTRICT
);

INSERT INTO `OnboardingDocumentTypes` (`Id`, `AllowsWordFiles`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `Description`, `Group`, `IsActive`, `IsDeleted`, `IsLegacy`, `MinFiles`, `Name`, `PersonnelFileCategoryId`, `Requirement`, `SortOrder`, `UpdatedAt`, `UpdatedBy`, `Version`)
VALUES (1, TRUE, 'ApplicationLetter', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, 'The letter you sent when applying.', 'Personal', TRUE, FALSE, FALSE, 1, 'Application letter', 1, 'Required', 10, NULL, NULL, '00000000-0000-0000-0027-000000000001');
SELECT ROW_COUNT();

INSERT INTO `OnboardingDocumentTypes` (`Id`, `AllowsWordFiles`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `Description`, `Group`, `IsActive`, `IsDeleted`, `IsLegacy`, `MinFiles`, `Name`, `PersonnelFileCategoryId`, `Requirement`, `SortOrder`, `UpdatedAt`, `UpdatedBy`, `Version`)
VALUES (2, TRUE, 'Cv', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, 'Your current CV.', 'Personal', TRUE, FALSE, FALSE, 1, 'Updated CV', 1, 'Required', 20, NULL, NULL, '00000000-0000-0000-0027-000000000002');
SELECT ROW_COUNT();

INSERT INTO `OnboardingDocumentTypes` (`Id`, `AllowsWordFiles`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `Description`, `Group`, `IsActive`, `IsDeleted`, `IsLegacy`, `MinFiles`, `Name`, `PersonnelFileCategoryId`, `Requirement`, `SortOrder`, `UpdatedAt`, `UpdatedBy`, `Version`)
VALUES (3, FALSE, 'PassportPicture', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, 'A recent passport-size photograph with a plain background.', 'Personal', TRUE, FALSE, FALSE, 1, 'Passport-size photograph', 3, 'Required', 30, NULL, NULL, '00000000-0000-0000-0027-000000000003');
SELECT ROW_COUNT();

INSERT INTO `OnboardingDocumentTypes` (`Id`, `AllowsWordFiles`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `Description`, `Group`, `IsActive`, `IsDeleted`, `IsLegacy`, `MinFiles`, `Name`, `PersonnelFileCategoryId`, `Requirement`, `SortOrder`, `UpdatedAt`, `UpdatedBy`, `Version`)
VALUES (4, FALSE, 'GhanaCard', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, 'Both sides of your Ghana Card.', 'Personal', TRUE, FALSE, FALSE, 1, 'Ghana Card / National ID', 3, 'GhanaianOnly', 40, NULL, NULL, '00000000-0000-0000-0027-000000000004');
SELECT ROW_COUNT();

INSERT INTO `OnboardingDocumentTypes` (`Id`, `AllowsWordFiles`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `Description`, `Group`, `IsActive`, `IsDeleted`, `IsLegacy`, `MinFiles`, `Name`, `PersonnelFileCategoryId`, `Requirement`, `SortOrder`, `UpdatedAt`, `UpdatedBy`, `Version`)
VALUES (5, FALSE, 'SsnitCard', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, 'Your SSNIT card or biometric registration slip.', 'Personal', TRUE, FALSE, FALSE, 1, 'SSNIT card', 3, 'Required', 50, NULL, NULL, '00000000-0000-0000-0027-000000000005');
SELECT ROW_COUNT();

INSERT INTO `OnboardingDocumentTypes` (`Id`, `AllowsWordFiles`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `Description`, `Group`, `IsActive`, `IsDeleted`, `IsLegacy`, `MinFiles`, `Name`, `PersonnelFileCategoryId`, `Requirement`, `SortOrder`, `UpdatedAt`, `UpdatedBy`, `Version`)
VALUES (6, FALSE, 'BirthCertificate', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, 'Your birth certificate.', 'Personal', TRUE, FALSE, FALSE, 1, 'Birth certificate', 3, 'Required', 60, NULL, NULL, '00000000-0000-0000-0027-000000000006');
SELECT ROW_COUNT();

INSERT INTO `OnboardingDocumentTypes` (`Id`, `AllowsWordFiles`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `Description`, `Group`, `IsActive`, `IsDeleted`, `IsLegacy`, `MinFiles`, `Name`, `PersonnelFileCategoryId`, `Requirement`, `SortOrder`, `UpdatedAt`, `UpdatedBy`, `Version`)
VALUES (7, FALSE, 'BeceCertificate', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, 'Basic Education Certificate Examination results.', 'Education', TRUE, FALSE, FALSE, 1, 'BECE certificate', 2, 'ChosenPerHire', 70, NULL, NULL, '00000000-0000-0000-0027-000000000007');
SELECT ROW_COUNT();

INSERT INTO `OnboardingDocumentTypes` (`Id`, `AllowsWordFiles`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `Description`, `Group`, `IsActive`, `IsDeleted`, `IsLegacy`, `MinFiles`, `Name`, `PersonnelFileCategoryId`, `Requirement`, `SortOrder`, `UpdatedAt`, `UpdatedBy`, `Version`)
VALUES (8, FALSE, 'WassceCertificate', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, 'West African Senior School Certificate Examination results.', 'Education', TRUE, FALSE, FALSE, 1, 'WASSCE / SSSCE certificate', 2, 'ChosenPerHire', 80, NULL, NULL, '00000000-0000-0000-0027-000000000008');
SELECT ROW_COUNT();

INSERT INTO `OnboardingDocumentTypes` (`Id`, `AllowsWordFiles`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `Description`, `Group`, `IsActive`, `IsDeleted`, `IsLegacy`, `MinFiles`, `Name`, `PersonnelFileCategoryId`, `Requirement`, `SortOrder`, `UpdatedAt`, `UpdatedBy`, `Version`)
VALUES (9, FALSE, 'DiplomaCertificate', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, 'Diploma certificate (and transcript, if you have it).', 'Education', TRUE, FALSE, FALSE, 1, 'Diploma certificate', 2, 'ChosenPerHire', 90, NULL, NULL, '00000000-0000-0000-0027-000000000009');
SELECT ROW_COUNT();

INSERT INTO `OnboardingDocumentTypes` (`Id`, `AllowsWordFiles`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `Description`, `Group`, `IsActive`, `IsDeleted`, `IsLegacy`, `MinFiles`, `Name`, `PersonnelFileCategoryId`, `Requirement`, `SortOrder`, `UpdatedAt`, `UpdatedBy`, `Version`)
VALUES (10, FALSE, 'HndCertificate', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, 'Higher National Diploma certificate.', 'Education', TRUE, FALSE, FALSE, 1, 'HND certificate', 2, 'ChosenPerHire', 100, NULL, NULL, '00000000-0000-0000-0027-000000000010');
SELECT ROW_COUNT();

INSERT INTO `OnboardingDocumentTypes` (`Id`, `AllowsWordFiles`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `Description`, `Group`, `IsActive`, `IsDeleted`, `IsLegacy`, `MinFiles`, `Name`, `PersonnelFileCategoryId`, `Requirement`, `SortOrder`, `UpdatedAt`, `UpdatedBy`, `Version`)
VALUES (11, FALSE, 'DegreeCertificate', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, 'Bachelor''s degree certificate (and transcript, if you have it).', 'Education', TRUE, FALSE, FALSE, 1, 'Degree certificate', 2, 'ChosenPerHire', 110, NULL, NULL, '00000000-0000-0000-0027-000000000011');
SELECT ROW_COUNT();

INSERT INTO `OnboardingDocumentTypes` (`Id`, `AllowsWordFiles`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `Description`, `Group`, `IsActive`, `IsDeleted`, `IsLegacy`, `MinFiles`, `Name`, `PersonnelFileCategoryId`, `Requirement`, `SortOrder`, `UpdatedAt`, `UpdatedBy`, `Version`)
VALUES (12, FALSE, 'MastersCertificate', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, 'Master''s degree certificate.', 'Education', TRUE, FALSE, FALSE, 1, 'Master''s certificate', 2, 'ChosenPerHire', 120, NULL, NULL, '00000000-0000-0000-0027-000000000012');
SELECT ROW_COUNT();

INSERT INTO `OnboardingDocumentTypes` (`Id`, `AllowsWordFiles`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `Description`, `Group`, `IsActive`, `IsDeleted`, `IsLegacy`, `MinFiles`, `Name`, `PersonnelFileCategoryId`, `Requirement`, `SortOrder`, `UpdatedAt`, `UpdatedBy`, `Version`)
VALUES (13, FALSE, 'ProfessionalCertificates', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, 'Professional or trade certificates (one file each).', 'Education', TRUE, FALSE, FALSE, 1, 'Professional certifications', 2, 'ChosenPerHire', 130, NULL, NULL, '00000000-0000-0000-0027-000000000013');
SELECT ROW_COUNT();

INSERT INTO `OnboardingDocumentTypes` (`Id`, `AllowsWordFiles`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `Description`, `Group`, `IsActive`, `IsDeleted`, `IsLegacy`, `MinFiles`, `Name`, `PersonnelFileCategoryId`, `Requirement`, `SortOrder`, `UpdatedAt`, `UpdatedBy`, `Version`)
VALUES (14, TRUE, 'RecommendationLetters', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, 'Two recommendation letters (one file each).', 'Employment', TRUE, FALSE, FALSE, 2, 'Recommendation letters', 1, 'Required', 140, NULL, NULL, '00000000-0000-0000-0027-000000000014');
SELECT ROW_COUNT();

INSERT INTO `OnboardingDocumentTypes` (`Id`, `AllowsWordFiles`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `Description`, `Group`, `IsActive`, `IsDeleted`, `IsLegacy`, `MinFiles`, `Name`, `PersonnelFileCategoryId`, `Requirement`, `SortOrder`, `UpdatedAt`, `UpdatedBy`, `Version`)
VALUES (15, FALSE, 'PoliceClearance', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, 'Police clearance report from the Ghana Police Service.', 'Employment', TRUE, FALSE, FALSE, 1, 'Police clearance report', 4, 'Required', 150, NULL, NULL, '00000000-0000-0000-0027-000000000015');
SELECT ROW_COUNT();

INSERT INTO `OnboardingDocumentTypes` (`Id`, `AllowsWordFiles`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `Description`, `Group`, `IsActive`, `IsDeleted`, `IsLegacy`, `MinFiles`, `Name`, `PersonnelFileCategoryId`, `Requirement`, `SortOrder`, `UpdatedAt`, `UpdatedBy`, `Version`)
VALUES (16, FALSE, 'MedicalReport', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, 'Medical examination report from a recognised hospital.', 'Employment', TRUE, FALSE, FALSE, 1, 'Medical examination report', 4, 'Required', 160, NULL, NULL, '00000000-0000-0000-0027-000000000016');
SELECT ROW_COUNT();

INSERT INTO `OnboardingDocumentTypes` (`Id`, `AllowsWordFiles`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `Description`, `Group`, `IsActive`, `IsDeleted`, `IsLegacy`, `MinFiles`, `Name`, `PersonnelFileCategoryId`, `Requirement`, `SortOrder`, `UpdatedAt`, `UpdatedBy`, `Version`)
VALUES (17, FALSE, 'BankConfirmationLetter', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, 'A letter from your bank confirming your account name and number.', 'Banking', TRUE, FALSE, FALSE, 1, 'Bank confirmation letter', 4, 'Required', 170, NULL, NULL, '00000000-0000-0000-0027-000000000017');
SELECT ROW_COUNT();

INSERT INTO `OnboardingDocumentTypes` (`Id`, `AllowsWordFiles`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `Description`, `Group`, `IsActive`, `IsDeleted`, `IsLegacy`, `MinFiles`, `Name`, `PersonnelFileCategoryId`, `Requirement`, `SortOrder`, `UpdatedAt`, `UpdatedBy`, `Version`)
VALUES (18, FALSE, 'MarriageCertificate', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, 'If you are married.', 'Family', TRUE, FALSE, FALSE, 1, 'Marriage certificate', 2, 'Optional', 180, NULL, NULL, '00000000-0000-0000-0027-000000000018');
SELECT ROW_COUNT();

INSERT INTO `OnboardingDocumentTypes` (`Id`, `AllowsWordFiles`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `Description`, `Group`, `IsActive`, `IsDeleted`, `IsLegacy`, `MinFiles`, `Name`, `PersonnelFileCategoryId`, `Requirement`, `SortOrder`, `UpdatedAt`, `UpdatedBy`, `Version`)
VALUES (19, FALSE, 'ChildBirthCertificates', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, 'One file per child, if you have children.', 'Family', TRUE, FALSE, FALSE, 1, 'Children''s birth certificates', 2, 'Optional', 190, NULL, NULL, '00000000-0000-0000-0027-000000000019');
SELECT ROW_COUNT();

INSERT INTO `OnboardingDocumentTypes` (`Id`, `AllowsWordFiles`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `Description`, `Group`, `IsActive`, `IsDeleted`, `IsLegacy`, `MinFiles`, `Name`, `PersonnelFileCategoryId`, `Requirement`, `SortOrder`, `UpdatedAt`, `UpdatedBy`, `Version`)
VALUES (20, FALSE, 'DependantPhotographs', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, 'Passport-size photographs of your dependants, if any.', 'Family', TRUE, FALSE, FALSE, 1, 'Dependants'' passport photographs', 2, 'Optional', 200, NULL, NULL, '00000000-0000-0000-0027-000000000020');
SELECT ROW_COUNT();

INSERT INTO `OnboardingDocumentTypes` (`Id`, `AllowsWordFiles`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `Description`, `Group`, `IsActive`, `IsDeleted`, `IsLegacy`, `MinFiles`, `Name`, `PersonnelFileCategoryId`, `Requirement`, `SortOrder`, `UpdatedAt`, `UpdatedBy`, `Version`)
VALUES (21, FALSE, 'PassportBiodataPage', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, 'The photo page of your passport.', 'Expatriate', TRUE, FALSE, FALSE, 1, 'Passport biodata page', 3, 'ExpatriateOnly', 210, NULL, NULL, '00000000-0000-0000-0027-000000000021');
SELECT ROW_COUNT();

INSERT INTO `OnboardingDocumentTypes` (`Id`, `AllowsWordFiles`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `Description`, `Group`, `IsActive`, `IsDeleted`, `IsLegacy`, `MinFiles`, `Name`, `PersonnelFileCategoryId`, `Requirement`, `SortOrder`, `UpdatedAt`, `UpdatedBy`, `Version`)
VALUES (22, FALSE, 'NonCitizenCard', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, 'Your Ghana non-citizen identification card.', 'Expatriate', TRUE, FALSE, FALSE, 1, 'Non-citizen card', 3, 'ExpatriateOnly', 220, NULL, NULL, '00000000-0000-0000-0027-000000000022');
SELECT ROW_COUNT();

INSERT INTO `OnboardingDocumentTypes` (`Id`, `AllowsWordFiles`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `Description`, `Group`, `IsActive`, `IsDeleted`, `IsLegacy`, `MinFiles`, `Name`, `PersonnelFileCategoryId`, `Requirement`, `SortOrder`, `UpdatedAt`, `UpdatedBy`, `Version`)
VALUES (23, FALSE, 'WorkPermit', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, 'Your Ghana Immigration Service work permit.', 'Expatriate', TRUE, FALSE, FALSE, 1, 'Work permit', 4, 'ExpatriateOnly', 230, NULL, NULL, '00000000-0000-0000-0027-000000000023');
SELECT ROW_COUNT();

INSERT INTO `OnboardingDocumentTypes` (`Id`, `AllowsWordFiles`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `Description`, `Group`, `IsActive`, `IsDeleted`, `IsLegacy`, `MinFiles`, `Name`, `PersonnelFileCategoryId`, `Requirement`, `SortOrder`, `UpdatedAt`, `UpdatedBy`, `Version`)
VALUES (24, FALSE, 'ResidencePermit', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, 'Your residence permit.', 'Expatriate', TRUE, FALSE, FALSE, 1, 'Residence permit', 4, 'ExpatriateOnly', 240, NULL, NULL, '00000000-0000-0000-0027-000000000024');
SELECT ROW_COUNT();

INSERT INTO `OnboardingDocumentTypes` (`Id`, `AllowsWordFiles`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `Description`, `Group`, `IsActive`, `IsDeleted`, `IsLegacy`, `MinFiles`, `Name`, `PersonnelFileCategoryId`, `Requirement`, `SortOrder`, `UpdatedAt`, `UpdatedBy`, `Version`)
VALUES (25, FALSE, 'SchoolCertificates', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, 'Kept for onboardings started before 10 November 2026.', 'Education', FALSE, FALSE, TRUE, 1, 'School certificates (old checklist)', 2, 'Required', 250, NULL, NULL, '00000000-0000-0000-0027-000000000025');
SELECT ROW_COUNT();


CREATE INDEX `IX_EmployeeDocumentEvents_EmployeeDocumentId_ActionAt` ON `EmployeeDocumentEvents` (`EmployeeDocumentId`, `ActionAt`);

CREATE INDEX `IX_EmployeeDocumentEvents_IsDeleted` ON `EmployeeDocumentEvents` (`IsDeleted`);

CREATE INDEX `IX_EmployeeDocumentEvents_OnboardingRecordId` ON `EmployeeDocumentEvents` (`OnboardingRecordId`);

CREATE UNIQUE INDEX `IX_OnboardingDocumentTypes_Code` ON `OnboardingDocumentTypes` (`Code`);

CREATE INDEX `IX_OnboardingDocumentTypes_IsDeleted` ON `OnboardingDocumentTypes` (`IsDeleted`);

CREATE INDEX `IX_OnboardingDocumentTypes_PersonnelFileCategoryId` ON `OnboardingDocumentTypes` (`PersonnelFileCategoryId`);

UPDATE `Onboardings` SET `Status` = 'AwaitingActivation', `StatusChangedAt` = UTC_TIMESTAMP() WHERE `Status` IN ('AwaitingHrApproval', 'AwaitingDepartmentalInduction');

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20261110071832_Phase27_OnboardingDocumentPortal', '10.0.14');

COMMIT;

