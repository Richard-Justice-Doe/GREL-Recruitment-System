START TRANSACTION;
CREATE TABLE `ProbationRecords` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `Number` varchar(40) NOT NULL,
    `EmployeeUserId` varchar(255) NOT NULL,
    `EmployeeName` varchar(200) NOT NULL,
    `StaffNumber` varchar(30) NULL,
    `JobTitle` varchar(150) NOT NULL,
    `DepartmentId` int NOT NULL,
    `OnboardingRecordId` int NOT NULL,
    `StartDate` date NOT NULL,
    `EndDate` date NOT NULL,
    `OriginalEndDate` date NOT NULL,
    `Status` varchar(30) NOT NULL,
    `StatusChangedAt` datetime(6) NOT NULL,
    `HodUserId` varchar(255) NULL,
    `HrManagerUserId` varchar(255) NULL,
    `InitialMeetingDueDate` date NOT NULL,
    `InitialMeetingDate` date NULL,
    `InitialMeetingNotes` text NULL,
    `EmployeeSignedAt` datetime(6) NULL,
    `EmployeeSignatureName` varchar(200) NULL,
    `HodSignedAt` datetime(6) NULL,
    `HodSignatureName` varchar(200) NULL,
    `ExtendedMonths` int NOT NULL,
    `ExtensionReason` text NULL,
    `ExtensionImprovementPlan` text NULL,
    `Version` char(36) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `CreatedBy` varchar(256) NULL,
    `UpdatedAt` datetime(6) NULL,
    `UpdatedBy` varchar(256) NULL,
    `IsDeleted` tinyint(1) NOT NULL,
    `DeletedAt` datetime(6) NULL,
    `DeletedBy` varchar(256) NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_ProbationRecords_Departments_DepartmentId` FOREIGN KEY (`DepartmentId`) REFERENCES `Departments` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_ProbationRecords_Onboardings_OnboardingRecordId` FOREIGN KEY (`OnboardingRecordId`) REFERENCES `Onboardings` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_ProbationRecords_Users_EmployeeUserId` FOREIGN KEY (`EmployeeUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_ProbationRecords_Users_HodUserId` FOREIGN KEY (`HodUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_ProbationRecords_Users_HrManagerUserId` FOREIGN KEY (`HrManagerUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
);

CREATE TABLE `ConfirmationRecords` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `EmployeeUserId` varchar(255) NOT NULL,
    `ProbationRecordId` int NOT NULL,
    `LetterNumber` varchar(40) NOT NULL,
    `ConfirmationDate` date NOT NULL,
    `ApprovedByUserId` varchar(255) NOT NULL,
    `PersonnelFileDocumentId` int NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `CreatedBy` varchar(256) NULL,
    `UpdatedAt` datetime(6) NULL,
    `UpdatedBy` varchar(256) NULL,
    `IsDeleted` tinyint(1) NOT NULL,
    `DeletedAt` datetime(6) NULL,
    `DeletedBy` varchar(256) NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_ConfirmationRecords_PersonnelFileDocuments_PersonnelFileDocu~` FOREIGN KEY (`PersonnelFileDocumentId`) REFERENCES `PersonnelFileDocuments` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_ConfirmationRecords_ProbationRecords_ProbationRecordId` FOREIGN KEY (`ProbationRecordId`) REFERENCES `ProbationRecords` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_ConfirmationRecords_Users_ApprovedByUserId` FOREIGN KEY (`ApprovedByUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_ConfirmationRecords_Users_EmployeeUserId` FOREIGN KEY (`EmployeeUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
);

CREATE TABLE `ProbationActions` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `ProbationRecordId` int NOT NULL,
    `Action` varchar(40) NOT NULL,
    `FromStatus` varchar(30) NOT NULL,
    `ToStatus` varchar(30) NOT NULL,
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
    CONSTRAINT `FK_ProbationActions_ProbationRecords_ProbationRecordId` FOREIGN KEY (`ProbationRecordId`) REFERENCES `ProbationRecords` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_ProbationActions_Users_ActorUserId` FOREIGN KEY (`ActorUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
);

CREATE TABLE `ProbationAssessments` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `ProbationRecordId` int NOT NULL,
    `Kind` varchar(10) NOT NULL,
    `AssessorUserId` varchar(255) NOT NULL,
    `AssessedAt` datetime(6) NOT NULL,
    `ApplicationOfSkills` varchar(20) NOT NULL,
    `QualityOfWork` varchar(20) NOT NULL,
    `Productivity` varchar(20) NOT NULL,
    `Cooperation` varchar(20) NOT NULL,
    `Dependability` varchar(20) NOT NULL,
    `AbilityToSupervise` varchar(20) NULL,
    `Comments` text NOT NULL,
    `ImprovementPlan` text NULL,
    `Outcome` varchar(20) NULL,
    `ExtensionMonths` int NULL,
    `ExtensionReason` text NULL,
    `HrApproved` tinyint(1) NULL,
    `HrComments` text NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `CreatedBy` varchar(256) NULL,
    `UpdatedAt` datetime(6) NULL,
    `UpdatedBy` varchar(256) NULL,
    `IsDeleted` tinyint(1) NOT NULL,
    `DeletedAt` datetime(6) NULL,
    `DeletedBy` varchar(256) NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_ProbationAssessments_ProbationRecords_ProbationRecordId` FOREIGN KEY (`ProbationRecordId`) REFERENCES `ProbationRecords` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_ProbationAssessments_Users_AssessorUserId` FOREIGN KEY (`AssessorUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
);

CREATE TABLE `ProbationDevelopmentPlans` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `ProbationRecordId` int NOT NULL,
    `TrainingNeed` varchar(500) NULL,
    `DevelopmentNeed` varchar(500) NULL,
    `Timeline` varchar(200) NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_ProbationDevelopmentPlans_ProbationRecords_ProbationRecordId` FOREIGN KEY (`ProbationRecordId`) REFERENCES `ProbationRecords` (`Id`) ON DELETE CASCADE
);

CREATE TABLE `ProbationObjectives` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `ProbationRecordId` int NOT NULL,
    `Objective` varchar(500) NOT NULL,
    `SuccessIndicators` varchar(1000) NOT NULL,
    `DueDate` date NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_ProbationObjectives_ProbationRecords_ProbationRecordId` FOREIGN KEY (`ProbationRecordId`) REFERENCES `ProbationRecords` (`Id`) ON DELETE CASCADE
);

CREATE INDEX `IX_ConfirmationRecords_ApprovedByUserId` ON `ConfirmationRecords` (`ApprovedByUserId`);

CREATE INDEX `IX_ConfirmationRecords_EmployeeUserId` ON `ConfirmationRecords` (`EmployeeUserId`);

CREATE INDEX `IX_ConfirmationRecords_IsDeleted` ON `ConfirmationRecords` (`IsDeleted`);

CREATE UNIQUE INDEX `IX_ConfirmationRecords_LetterNumber` ON `ConfirmationRecords` (`LetterNumber`);

CREATE INDEX `IX_ConfirmationRecords_PersonnelFileDocumentId` ON `ConfirmationRecords` (`PersonnelFileDocumentId`);

CREATE UNIQUE INDEX `IX_ConfirmationRecords_ProbationRecordId` ON `ConfirmationRecords` (`ProbationRecordId`);

CREATE INDEX `IX_ProbationActions_ActorUserId` ON `ProbationActions` (`ActorUserId`);

CREATE INDEX `IX_ProbationActions_IsDeleted` ON `ProbationActions` (`IsDeleted`);

CREATE INDEX `IX_ProbationActions_ProbationRecordId_ActionAt` ON `ProbationActions` (`ProbationRecordId`, `ActionAt`);

CREATE INDEX `IX_ProbationAssessments_AssessorUserId` ON `ProbationAssessments` (`AssessorUserId`);

CREATE INDEX `IX_ProbationAssessments_IsDeleted` ON `ProbationAssessments` (`IsDeleted`);

CREATE INDEX `IX_ProbationAssessments_ProbationRecordId_Kind` ON `ProbationAssessments` (`ProbationRecordId`, `Kind`);

CREATE INDEX `IX_ProbationDevelopmentPlans_ProbationRecordId` ON `ProbationDevelopmentPlans` (`ProbationRecordId`);

CREATE INDEX `IX_ProbationObjectives_ProbationRecordId` ON `ProbationObjectives` (`ProbationRecordId`);

CREATE INDEX `IX_ProbationRecords_DepartmentId_Status` ON `ProbationRecords` (`DepartmentId`, `Status`);

CREATE INDEX `IX_ProbationRecords_EmployeeUserId` ON `ProbationRecords` (`EmployeeUserId`);

CREATE INDEX `IX_ProbationRecords_HodUserId` ON `ProbationRecords` (`HodUserId`);

CREATE INDEX `IX_ProbationRecords_HrManagerUserId` ON `ProbationRecords` (`HrManagerUserId`);

CREATE INDEX `IX_ProbationRecords_IsDeleted` ON `ProbationRecords` (`IsDeleted`);

CREATE UNIQUE INDEX `IX_ProbationRecords_Number` ON `ProbationRecords` (`Number`);

CREATE UNIQUE INDEX `IX_ProbationRecords_OnboardingRecordId` ON `ProbationRecords` (`OnboardingRecordId`);

CREATE INDEX `IX_ProbationRecords_Status_EndDate` ON `ProbationRecords` (`Status`, `EndDate`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20261106161059_Phase21_ProbationAndConfirmation', '10.0.14');

COMMIT;

