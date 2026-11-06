START TRANSACTION;
CREATE TABLE `EmployeePersonalRecords` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `OnboardingRecordId` int NOT NULL,
    `EmployeeUserId` varchar(255) NOT NULL,
    `Qualification` varchar(200) NULL,
    `SsnitNumber` varchar(30) NULL,
    `DateOfBirth` date NULL,
    `ResidentialAddress` varchar(500) NULL,
    `HomeTown` varchar(100) NULL,
    `Region` varchar(20) NULL,
    `Tribe` varchar(100) NULL,
    `Religion` varchar(100) NULL,
    `Email` varchar(256) NULL,
    `MobileNumber` varchar(30) NULL,
    `HasDisability` tinyint(1) NOT NULL,
    `DisabilityDetails` text NULL,
    `VisibleMarks` varchar(500) NULL,
    `NextOfKinName` varchar(200) NULL,
    `NextOfKinTelephone` varchar(30) NULL,
    `NextOfKinRelationship` varchar(100) NULL,
    `NextOfKinDateOfBirth` date NULL,
    `NextOfKinAddress` varchar(500) NULL,
    `DeclaredAt` datetime(6) NULL,
    `CurrentDeclarationId` int NULL,
    `Version` char(36) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `CreatedBy` varchar(256) NULL,
    `UpdatedAt` datetime(6) NULL,
    `UpdatedBy` varchar(256) NULL,
    `IsDeleted` tinyint(1) NOT NULL,
    `DeletedAt` datetime(6) NULL,
    `DeletedBy` varchar(256) NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_EmployeePersonalRecords_Onboardings_OnboardingRecordId` FOREIGN KEY (`OnboardingRecordId`) REFERENCES `Onboardings` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_EmployeePersonalRecords_Users_EmployeeUserId` FOREIGN KEY (`EmployeeUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
);

CREATE TABLE `EmployeeBankAccounts` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `PersonalRecordId` int NOT NULL,
    `BankName` varchar(100) NOT NULL,
    `Branch` varchar(100) NOT NULL,
    `AccountNumber` varchar(40) NOT NULL,
    `IsPrimary` tinyint(1) NOT NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_EmployeeBankAccounts_EmployeePersonalRecords_PersonalRecordId` FOREIGN KEY (`PersonalRecordId`) REFERENCES `EmployeePersonalRecords` (`Id`) ON DELETE CASCADE
);

CREATE TABLE `EmployeeBeneficiaries` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `PersonalRecordId` int NOT NULL,
    `Name` varchar(200) NOT NULL,
    `Relationship` varchar(100) NOT NULL,
    `DateOfBirth` date NULL,
    `Percentage` decimal(5,2) NOT NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_EmployeeBeneficiaries_EmployeePersonalRecords_PersonalRecord~` FOREIGN KEY (`PersonalRecordId`) REFERENCES `EmployeePersonalRecords` (`Id`) ON DELETE CASCADE
);

CREATE TABLE `EmployeeChildren` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `PersonalRecordId` int NOT NULL,
    `FullName` varchar(200) NOT NULL,
    `Gender` varchar(10) NOT NULL,
    `DateOfBirth` date NOT NULL,
    `PlaceOfBirth` varchar(100) NULL,
    `CurrentResidence` varchar(200) NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_EmployeeChildren_EmployeePersonalRecords_PersonalRecordId` FOREIGN KEY (`PersonalRecordId`) REFERENCES `EmployeePersonalRecords` (`Id`) ON DELETE CASCADE
);

CREATE TABLE `EmployeeDeclarations` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `PersonalRecordId` int NOT NULL,
    `EmployeeUserId` varchar(255) NOT NULL,
    `DeclarationText` varchar(500) NOT NULL,
    `SignatureName` varchar(200) NOT NULL,
    `SignedAt` datetime(6) NOT NULL,
    `IpAddress` varchar(64) NULL,
    `Snapshot` text NOT NULL,
    `SnapshotSha256` varchar(64) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `CreatedBy` varchar(256) NULL,
    `UpdatedAt` datetime(6) NULL,
    `UpdatedBy` varchar(256) NULL,
    `IsDeleted` tinyint(1) NOT NULL,
    `DeletedAt` datetime(6) NULL,
    `DeletedBy` varchar(256) NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_EmployeeDeclarations_EmployeePersonalRecords_PersonalRecordId` FOREIGN KEY (`PersonalRecordId`) REFERENCES `EmployeePersonalRecords` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_EmployeeDeclarations_Users_EmployeeUserId` FOREIGN KEY (`EmployeeUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
);

CREATE TABLE `EmployeeDependants` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `PersonalRecordId` int NOT NULL,
    `FullName` varchar(200) NOT NULL,
    `Relationship` varchar(100) NOT NULL,
    `Age` int NOT NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_EmployeeDependants_EmployeePersonalRecords_PersonalRecordId` FOREIGN KEY (`PersonalRecordId`) REFERENCES `EmployeePersonalRecords` (`Id`) ON DELETE CASCADE
);

CREATE TABLE `EmployeeEmergencyContacts` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `PersonalRecordId` int NOT NULL,
    `Name` varchar(200) NOT NULL,
    `Relationship` varchar(100) NOT NULL,
    `Telephone1` varchar(30) NOT NULL,
    `Telephone2` varchar(30) NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_EmployeeEmergencyContacts_EmployeePersonalRecords_PersonalRe~` FOREIGN KEY (`PersonalRecordId`) REFERENCES `EmployeePersonalRecords` (`Id`) ON DELETE CASCADE
);

CREATE TABLE `EmployeeLanguages` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `PersonalRecordId` int NOT NULL,
    `Language` varchar(60) NOT NULL,
    `Proficiency` varchar(20) NOT NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_EmployeeLanguages_EmployeePersonalRecords_PersonalRecordId` FOREIGN KEY (`PersonalRecordId`) REFERENCES `EmployeePersonalRecords` (`Id`) ON DELETE CASCADE
);

CREATE TABLE `EmployeeSpouses` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `PersonalRecordId` int NOT NULL,
    `FullName` varchar(200) NOT NULL,
    `Occupation` varchar(100) NULL,
    `DateOfBirth` date NULL,
    `Address` varchar(500) NULL,
    `Telephone` varchar(30) NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_EmployeeSpouses_EmployeePersonalRecords_PersonalRecordId` FOREIGN KEY (`PersonalRecordId`) REFERENCES `EmployeePersonalRecords` (`Id`) ON DELETE CASCADE
);

CREATE INDEX `IX_EmployeeBankAccounts_PersonalRecordId` ON `EmployeeBankAccounts` (`PersonalRecordId`);

CREATE INDEX `IX_EmployeeBeneficiaries_PersonalRecordId` ON `EmployeeBeneficiaries` (`PersonalRecordId`);

CREATE INDEX `IX_EmployeeChildren_PersonalRecordId` ON `EmployeeChildren` (`PersonalRecordId`);

CREATE INDEX `IX_EmployeeDeclarations_EmployeeUserId` ON `EmployeeDeclarations` (`EmployeeUserId`);

CREATE INDEX `IX_EmployeeDeclarations_IsDeleted` ON `EmployeeDeclarations` (`IsDeleted`);

CREATE INDEX `IX_EmployeeDeclarations_PersonalRecordId_SignedAt` ON `EmployeeDeclarations` (`PersonalRecordId`, `SignedAt`);

CREATE INDEX `IX_EmployeeDependants_PersonalRecordId` ON `EmployeeDependants` (`PersonalRecordId`);

CREATE INDEX `IX_EmployeeEmergencyContacts_PersonalRecordId` ON `EmployeeEmergencyContacts` (`PersonalRecordId`);

CREATE INDEX `IX_EmployeeLanguages_PersonalRecordId` ON `EmployeeLanguages` (`PersonalRecordId`);

CREATE INDEX `IX_EmployeePersonalRecords_EmployeeUserId` ON `EmployeePersonalRecords` (`EmployeeUserId`);

CREATE INDEX `IX_EmployeePersonalRecords_IsDeleted` ON `EmployeePersonalRecords` (`IsDeleted`);

CREATE UNIQUE INDEX `IX_EmployeePersonalRecords_OnboardingRecordId` ON `EmployeePersonalRecords` (`OnboardingRecordId`);

CREATE INDEX `IX_EmployeeSpouses_PersonalRecordId` ON `EmployeeSpouses` (`PersonalRecordId`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20261106101407_Phase19_PersonalRecordDeclaration', '10.0.14');

COMMIT;

