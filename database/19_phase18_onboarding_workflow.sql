START TRANSACTION;
ALTER TABLE `Onboardings` MODIFY `Status` varchar(40) NOT NULL;

ALTER TABLE `Onboardings` ADD `CompletedAt` datetime(6) NULL;

ALTER TABLE `Onboardings` ADD `ExpectedCompletionDate` date NULL;

ALTER TABLE `Onboardings` ADD `Notes` text NULL;

CREATE TABLE `EmployeeDocuments` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `OnboardingRecordId` int NOT NULL,
    `EmployeeUserId` varchar(255) NOT NULL,
    `DocumentType` varchar(40) NOT NULL,
    `IsRequired` tinyint(1) NOT NULL,
    `Status` varchar(20) NOT NULL,
    `SubmittedAt` datetime(6) NULL,
    `NotApplicableReason` varchar(500) NULL,
    `VerifiedAt` datetime(6) NULL,
    `VerifiedByUserId` varchar(255) NULL,
    `RejectionReason` varchar(1000) NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `CreatedBy` varchar(256) NULL,
    `UpdatedAt` datetime(6) NULL,
    `UpdatedBy` varchar(256) NULL,
    `IsDeleted` tinyint(1) NOT NULL,
    `DeletedAt` datetime(6) NULL,
    `DeletedBy` varchar(256) NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_EmployeeDocuments_Onboardings_OnboardingRecordId` FOREIGN KEY (`OnboardingRecordId`) REFERENCES `Onboardings` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_EmployeeDocuments_Users_EmployeeUserId` FOREIGN KEY (`EmployeeUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_EmployeeDocuments_Users_VerifiedByUserId` FOREIGN KEY (`VerifiedByUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
);

CREATE TABLE `OnboardingActions` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `OnboardingRecordId` int NOT NULL,
    `Action` varchar(40) NOT NULL,
    `FromStatus` varchar(40) NOT NULL,
    `ToStatus` varchar(40) NOT NULL,
    `ActorUserId` varchar(255) NOT NULL,
    `ActorName` varchar(200) NOT NULL,
    `Comments` text NULL,
    `ActionAt` datetime(6) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `CreatedBy` varchar(256) NULL,
    `UpdatedAt` datetime(6) NULL,
    `UpdatedBy` varchar(256) NULL,
    `IsDeleted` tinyint(1) NOT NULL,
    `DeletedAt` datetime(6) NULL,
    `DeletedBy` varchar(256) NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_OnboardingActions_Onboardings_OnboardingRecordId` FOREIGN KEY (`OnboardingRecordId`) REFERENCES `Onboardings` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_OnboardingActions_Users_ActorUserId` FOREIGN KEY (`ActorUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
);

CREATE INDEX `IX_EmployeeDocuments_EmployeeUserId` ON `EmployeeDocuments` (`EmployeeUserId`);

CREATE INDEX `IX_EmployeeDocuments_IsDeleted` ON `EmployeeDocuments` (`IsDeleted`);

CREATE UNIQUE INDEX `IX_EmployeeDocuments_OnboardingRecordId_DocumentType` ON `EmployeeDocuments` (`OnboardingRecordId`, `DocumentType`);

CREATE INDEX `IX_EmployeeDocuments_Status` ON `EmployeeDocuments` (`Status`);

CREATE INDEX `IX_EmployeeDocuments_VerifiedByUserId` ON `EmployeeDocuments` (`VerifiedByUserId`);

CREATE INDEX `IX_OnboardingActions_ActorUserId` ON `OnboardingActions` (`ActorUserId`);

CREATE INDEX `IX_OnboardingActions_IsDeleted` ON `OnboardingActions` (`IsDeleted`);

CREATE INDEX `IX_OnboardingActions_OnboardingRecordId_ActionAt` ON `OnboardingActions` (`OnboardingRecordId`, `ActionAt`);

UPDATE Onboardings SET Status = CASE WHEN LetterAcknowledgedAt IS NOT NULL THEN 'AwaitingDepartmentalInduction' WHEN LetterIssuedAt IS NOT NULL THEN 'AwaitingEmployeeSubmission' ELSE 'Draft' END WHERE Status = 'InProgress';

UPDATE Onboardings SET CompletedAt = StatusChangedAt WHERE Status = 'Completed';

UPDATE Onboardings SET ExpectedCompletionDate = DATE_ADD(StartDate, INTERVAL 30 DAY);

INSERT INTO EmployeeDocuments (OnboardingRecordId, EmployeeUserId, DocumentType, IsRequired, Status, CreatedAt, CreatedBy, IsDeleted) SELECT o.Id, o.EmployeeUserId, t.DocumentType, t.IsRequired, 'Pending', UTC_TIMESTAMP(6), 'system', 0 FROM Onboardings o CROSS JOIN (SELECT 'ApplicationLetter' AS DocumentType, 1 AS IsRequired UNION ALL SELECT 'Cv', 1 UNION ALL SELECT 'PassportPicture', 1 UNION ALL SELECT 'RecommendationLetters', 1 UNION ALL SELECT 'MedicalReport', 1 UNION ALL SELECT 'PoliceClearance', 1 UNION ALL SELECT 'SchoolCertificates', 1 UNION ALL SELECT 'BirthCertificate', 1 UNION ALL SELECT 'GhanaCard', 1 UNION ALL SELECT 'SsnitCard', 1 UNION ALL SELECT 'MarriageCertificate', 0 UNION ALL SELECT 'ChildBirthCertificates', 0 UNION ALL SELECT 'DependantPhotographs', 0 UNION ALL SELECT 'BankConfirmationLetter', 1 UNION ALL SELECT 'PassportBiodataPage', 0 UNION ALL SELECT 'NonCitizenCard', 0) t WHERE o.IsDeleted = 0 AND o.Status IN ('Draft', 'AwaitingEmployeeSubmission');

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20261106094747_Phase18_OnboardingWorkflow', '10.0.14');

COMMIT;

