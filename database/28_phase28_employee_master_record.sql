START TRANSACTION;
CREATE TABLE `Grades` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `Code` varchar(20) NOT NULL,
    `Name` varchar(100) NOT NULL,
    `SortOrder` int NOT NULL,
    `MinMonthlySalary` decimal(18,2) NULL,
    `MaxMonthlySalary` decimal(18,2) NULL,
    `IsActive` tinyint(1) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `CreatedBy` varchar(256) NULL,
    `UpdatedAt` datetime(6) NULL,
    `UpdatedBy` varchar(256) NULL,
    `IsDeleted` tinyint(1) NOT NULL,
    `DeletedAt` datetime(6) NULL,
    `DeletedBy` varchar(256) NULL,
    PRIMARY KEY (`Id`)
);

CREATE TABLE `Employees` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `EmployeeNumber` varchar(30) NOT NULL,
    `UserId` varchar(255) NOT NULL,
    `OnboardingRecordId` int NULL,
    `PositionTitle` varchar(150) NOT NULL,
    `DepartmentId` int NOT NULL,
    `SiteId` int NOT NULL,
    `GradeId` int NULL,
    `ReportsToUserId` varchar(255) NULL,
    `EmploymentType` varchar(30) NOT NULL,
    `ContractEndDate` date NULL,
    `HireDate` date NOT NULL,
    `ConfirmationDate` date NULL,
    `Status` varchar(20) NOT NULL,
    `StatusChangedAt` datetime(6) NOT NULL,
    `Version` char(36) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `CreatedBy` varchar(256) NULL,
    `UpdatedAt` datetime(6) NULL,
    `UpdatedBy` varchar(256) NULL,
    `IsDeleted` tinyint(1) NOT NULL,
    `DeletedAt` datetime(6) NULL,
    `DeletedBy` varchar(256) NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Employees_Departments_DepartmentId` FOREIGN KEY (`DepartmentId`) REFERENCES `Departments` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_Employees_Grades_GradeId` FOREIGN KEY (`GradeId`) REFERENCES `Grades` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_Employees_Onboardings_OnboardingRecordId` FOREIGN KEY (`OnboardingRecordId`) REFERENCES `Onboardings` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_Employees_Sites_SiteId` FOREIGN KEY (`SiteId`) REFERENCES `Sites` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_Employees_Users_ReportsToUserId` FOREIGN KEY (`ReportsToUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_Employees_Users_UserId` FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
);

CREATE TABLE `EmployeeAssignments` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `EmployeeId` int NOT NULL,
    `EffectiveFrom` date NOT NULL,
    `EffectiveTo` date NULL,
    `PositionTitle` varchar(150) NOT NULL,
    `DepartmentId` int NOT NULL,
    `SiteId` int NOT NULL,
    `GradeId` int NULL,
    `ReportsToUserId` varchar(255) NULL,
    `EmploymentType` varchar(30) NOT NULL,
    `ContractEndDate` date NULL,
    `Source` varchar(20) NOT NULL,
    `SourceId` int NULL,
    `Note` varchar(500) NULL,
    `RecordedByUserId` varchar(255) NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `CreatedBy` varchar(256) NULL,
    `UpdatedAt` datetime(6) NULL,
    `UpdatedBy` varchar(256) NULL,
    `IsDeleted` tinyint(1) NOT NULL,
    `DeletedAt` datetime(6) NULL,
    `DeletedBy` varchar(256) NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_EmployeeAssignments_Departments_DepartmentId` FOREIGN KEY (`DepartmentId`) REFERENCES `Departments` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_EmployeeAssignments_Employees_EmployeeId` FOREIGN KEY (`EmployeeId`) REFERENCES `Employees` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_EmployeeAssignments_Grades_GradeId` FOREIGN KEY (`GradeId`) REFERENCES `Grades` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_EmployeeAssignments_Sites_SiteId` FOREIGN KEY (`SiteId`) REFERENCES `Sites` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_EmployeeAssignments_Users_ReportsToUserId` FOREIGN KEY (`ReportsToUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
);

CREATE INDEX `IX_EmployeeAssignments_DepartmentId` ON `EmployeeAssignments` (`DepartmentId`);

CREATE INDEX `IX_EmployeeAssignments_EmployeeId_EffectiveFrom` ON `EmployeeAssignments` (`EmployeeId`, `EffectiveFrom`);

CREATE INDEX `IX_EmployeeAssignments_GradeId` ON `EmployeeAssignments` (`GradeId`);

CREATE INDEX `IX_EmployeeAssignments_IsDeleted` ON `EmployeeAssignments` (`IsDeleted`);

CREATE INDEX `IX_EmployeeAssignments_ReportsToUserId` ON `EmployeeAssignments` (`ReportsToUserId`);

CREATE INDEX `IX_EmployeeAssignments_SiteId` ON `EmployeeAssignments` (`SiteId`);

CREATE INDEX `IX_Employees_DepartmentId_Status` ON `Employees` (`DepartmentId`, `Status`);

CREATE UNIQUE INDEX `IX_Employees_EmployeeNumber` ON `Employees` (`EmployeeNumber`);

CREATE INDEX `IX_Employees_GradeId` ON `Employees` (`GradeId`);

CREATE INDEX `IX_Employees_IsDeleted` ON `Employees` (`IsDeleted`);

CREATE INDEX `IX_Employees_OnboardingRecordId` ON `Employees` (`OnboardingRecordId`);

CREATE INDEX `IX_Employees_ReportsToUserId` ON `Employees` (`ReportsToUserId`);

CREATE INDEX `IX_Employees_SiteId` ON `Employees` (`SiteId`);

CREATE UNIQUE INDEX `IX_Employees_UserId` ON `Employees` (`UserId`);

CREATE UNIQUE INDEX `IX_Grades_Code` ON `Grades` (`Code`);

CREATE INDEX `IX_Grades_IsDeleted` ON `Grades` (`IsDeleted`);

CREATE UNIQUE INDEX `IX_Grades_Name` ON `Grades` (`Name`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20261111131122_EmployeeMasterRecord', '10.0.14');

COMMIT;

