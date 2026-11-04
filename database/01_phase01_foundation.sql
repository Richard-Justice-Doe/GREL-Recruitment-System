CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
    `MigrationId` varchar(150) NOT NULL,
    `ProductVersion` varchar(32) NOT NULL,
    PRIMARY KEY (`MigrationId`)
);

START TRANSACTION;
CREATE TABLE `AuditLogs` (
    `Id` bigint NOT NULL AUTO_INCREMENT,
    `Timestamp` datetime(6) NOT NULL,
    `UserId` varchar(255) NULL,
    `UserName` varchar(256) NULL,
    `Action` varchar(50) NOT NULL,
    `EntityName` varchar(100) NULL,
    `EntityId` varchar(255) NULL,
    `OldValues` longtext NULL,
    `NewValues` longtext NULL,
    `IpAddress` varchar(45) NULL,
    PRIMARY KEY (`Id`)
);

CREATE TABLE `Roles` (
    `Id` varchar(255) NOT NULL,
    `Name` varchar(256) NULL,
    `NormalizedName` varchar(256) NULL,
    `ConcurrencyStamp` longtext NULL,
    PRIMARY KEY (`Id`)
);

CREATE TABLE `Sites` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `Name` varchar(150) NOT NULL,
    `Code` varchar(20) NOT NULL,
    `Location` varchar(300) NULL,
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

CREATE TABLE `RoleClaims` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `RoleId` varchar(255) NOT NULL,
    `ClaimType` longtext NULL,
    `ClaimValue` longtext NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_RoleClaims_Roles_RoleId` FOREIGN KEY (`RoleId`) REFERENCES `Roles` (`Id`) ON DELETE CASCADE
);

CREATE TABLE `Departments` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `Name` varchar(150) NOT NULL,
    `Code` varchar(20) NOT NULL,
    `Description` varchar(1000) NULL,
    `HeadOfDepartmentUserId` varchar(255) NULL,
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

CREATE TABLE `Units` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `DepartmentId` int NOT NULL,
    `Name` varchar(150) NOT NULL,
    `Code` varchar(20) NOT NULL,
    `Description` varchar(1000) NULL,
    `IsActive` tinyint(1) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `CreatedBy` varchar(256) NULL,
    `UpdatedAt` datetime(6) NULL,
    `UpdatedBy` varchar(256) NULL,
    `IsDeleted` tinyint(1) NOT NULL,
    `DeletedAt` datetime(6) NULL,
    `DeletedBy` varchar(256) NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Units_Departments_DepartmentId` FOREIGN KEY (`DepartmentId`) REFERENCES `Departments` (`Id`) ON DELETE RESTRICT
);

CREATE TABLE `Users` (
    `Id` varchar(255) NOT NULL,
    `FullName` varchar(150) NOT NULL,
    `StaffNumber` varchar(30) NULL,
    `DepartmentId` int NULL,
    `SiteId` int NULL,
    `IsCandidate` tinyint(1) NOT NULL,
    `IsActive` tinyint(1) NOT NULL,
    `MustChangePassword` tinyint(1) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `LastSignInAt` datetime(6) NULL,
    `UserName` varchar(256) NULL,
    `NormalizedUserName` varchar(256) NULL,
    `Email` varchar(256) NULL,
    `NormalizedEmail` varchar(256) NULL,
    `EmailConfirmed` tinyint(1) NOT NULL,
    `PasswordHash` longtext NULL,
    `SecurityStamp` longtext NULL,
    `ConcurrencyStamp` longtext NULL,
    `PhoneNumber` longtext NULL,
    `PhoneNumberConfirmed` tinyint(1) NOT NULL,
    `TwoFactorEnabled` tinyint(1) NOT NULL,
    `LockoutEnd` datetime NULL,
    `LockoutEnabled` tinyint(1) NOT NULL,
    `AccessFailedCount` int NOT NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Users_Departments_DepartmentId` FOREIGN KEY (`DepartmentId`) REFERENCES `Departments` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_Users_Sites_SiteId` FOREIGN KEY (`SiteId`) REFERENCES `Sites` (`Id`) ON DELETE RESTRICT
);

CREATE TABLE `UserClaims` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `UserId` varchar(255) NOT NULL,
    `ClaimType` longtext NULL,
    `ClaimValue` longtext NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_UserClaims_Users_UserId` FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`) ON DELETE CASCADE
);

CREATE TABLE `UserLogins` (
    `LoginProvider` varchar(128) NOT NULL,
    `ProviderKey` varchar(128) NOT NULL,
    `ProviderDisplayName` longtext NULL,
    `UserId` varchar(255) NOT NULL,
    PRIMARY KEY (`LoginProvider`, `ProviderKey`),
    CONSTRAINT `FK_UserLogins_Users_UserId` FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`) ON DELETE CASCADE
);

CREATE TABLE `UserRoles` (
    `UserId` varchar(255) NOT NULL,
    `RoleId` varchar(255) NOT NULL,
    PRIMARY KEY (`UserId`, `RoleId`),
    CONSTRAINT `FK_UserRoles_Roles_RoleId` FOREIGN KEY (`RoleId`) REFERENCES `Roles` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_UserRoles_Users_UserId` FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`) ON DELETE CASCADE
);

CREATE TABLE `UserTokens` (
    `UserId` varchar(255) NOT NULL,
    `LoginProvider` varchar(128) NOT NULL,
    `Name` varchar(128) NOT NULL,
    `Value` longtext NULL,
    PRIMARY KEY (`UserId`, `LoginProvider`, `Name`),
    CONSTRAINT `FK_UserTokens_Users_UserId` FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`) ON DELETE CASCADE
);

INSERT INTO `Departments` (`Id`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `Description`, `HeadOfDepartmentUserId`, `IsActive`, `IsDeleted`, `Name`, `UpdatedAt`, `UpdatedBy`)
VALUES (1, 'HR', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, NULL, NULL, TRUE, FALSE, 'Human Resources', NULL, NULL);
SELECT ROW_COUNT();

INSERT INTO `Departments` (`Id`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `Description`, `HeadOfDepartmentUserId`, `IsActive`, `IsDeleted`, `Name`, `UpdatedAt`, `UpdatedBy`)
VALUES (2, 'IT', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, NULL, NULL, TRUE, FALSE, 'Information Technology', NULL, NULL);
SELECT ROW_COUNT();

INSERT INTO `Departments` (`Id`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `Description`, `HeadOfDepartmentUserId`, `IsActive`, `IsDeleted`, `Name`, `UpdatedAt`, `UpdatedBy`)
VALUES (3, 'FIN', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, NULL, NULL, TRUE, FALSE, 'Finance', NULL, NULL);
SELECT ROW_COUNT();

INSERT INTO `Departments` (`Id`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `Description`, `HeadOfDepartmentUserId`, `IsActive`, `IsDeleted`, `Name`, `UpdatedAt`, `UpdatedBy`)
VALUES (4, 'PROC', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, NULL, NULL, TRUE, FALSE, 'Procurement', NULL, NULL);
SELECT ROW_COUNT();

INSERT INTO `Departments` (`Id`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `Description`, `HeadOfDepartmentUserId`, `IsActive`, `IsDeleted`, `Name`, `UpdatedAt`, `UpdatedBy`)
VALUES (5, 'SCM', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, NULL, NULL, TRUE, FALSE, 'Supply Chain', NULL, NULL);
SELECT ROW_COUNT();

INSERT INTO `Departments` (`Id`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `Description`, `HeadOfDepartmentUserId`, `IsActive`, `IsDeleted`, `Name`, `UpdatedAt`, `UpdatedBy`)
VALUES (6, 'SALES', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, NULL, NULL, TRUE, FALSE, 'Sales and Marketing', NULL, NULL);
SELECT ROW_COUNT();

INSERT INTO `Departments` (`Id`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `Description`, `HeadOfDepartmentUserId`, `IsActive`, `IsDeleted`, `Name`, `UpdatedAt`, `UpdatedBy`)
VALUES (7, 'PROD', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, NULL, NULL, TRUE, FALSE, 'Production', NULL, NULL);
SELECT ROW_COUNT();

INSERT INTO `Departments` (`Id`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `Description`, `HeadOfDepartmentUserId`, `IsActive`, `IsDeleted`, `Name`, `UpdatedAt`, `UpdatedBy`)
VALUES (8, 'ENG', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, NULL, NULL, TRUE, FALSE, 'Engineering', NULL, NULL);
SELECT ROW_COUNT();

INSERT INTO `Departments` (`Id`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `Description`, `HeadOfDepartmentUserId`, `IsActive`, `IsDeleted`, `Name`, `UpdatedAt`, `UpdatedBy`)
VALUES (9, 'MAINT', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, NULL, NULL, TRUE, FALSE, 'Maintenance', NULL, NULL);
SELECT ROW_COUNT();

INSERT INTO `Departments` (`Id`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `Description`, `HeadOfDepartmentUserId`, `IsActive`, `IsDeleted`, `Name`, `UpdatedAt`, `UpdatedBy`)
VALUES (10, 'HSE', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, NULL, NULL, TRUE, FALSE, 'Health, Safety & Environment (HSE)', NULL, NULL);
SELECT ROW_COUNT();

INSERT INTO `Departments` (`Id`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `Description`, `HeadOfDepartmentUserId`, `IsActive`, `IsDeleted`, `Name`, `UpdatedAt`, `UpdatedBy`)
VALUES (11, 'QA', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, NULL, NULL, TRUE, FALSE, 'Quality Assurance', NULL, NULL);
SELECT ROW_COUNT();

INSERT INTO `Departments` (`Id`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `Description`, `HeadOfDepartmentUserId`, `IsActive`, `IsDeleted`, `Name`, `UpdatedAt`, `UpdatedBy`)
VALUES (12, 'RND', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, NULL, NULL, TRUE, FALSE, 'Research & Development', NULL, NULL);
SELECT ROW_COUNT();

INSERT INTO `Departments` (`Id`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `Description`, `HeadOfDepartmentUserId`, `IsActive`, `IsDeleted`, `Name`, `UpdatedAt`, `UpdatedBy`)
VALUES (13, 'LEGAL', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, NULL, NULL, TRUE, FALSE, 'Legal & Compliance', NULL, NULL);
SELECT ROW_COUNT();

INSERT INTO `Departments` (`Id`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `Description`, `HeadOfDepartmentUserId`, `IsActive`, `IsDeleted`, `Name`, `UpdatedAt`, `UpdatedBy`)
VALUES (14, 'SEC', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, NULL, NULL, TRUE, FALSE, 'Security', NULL, NULL);
SELECT ROW_COUNT();

INSERT INTO `Departments` (`Id`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `Description`, `HeadOfDepartmentUserId`, `IsActive`, `IsDeleted`, `Name`, `UpdatedAt`, `UpdatedBy`)
VALUES (15, 'ADMIN', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, NULL, NULL, TRUE, FALSE, 'Administration', NULL, NULL);
SELECT ROW_COUNT();

INSERT INTO `Departments` (`Id`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `Description`, `HeadOfDepartmentUserId`, `IsActive`, `IsDeleted`, `Name`, `UpdatedAt`, `UpdatedBy`)
VALUES (16, 'CA', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, NULL, NULL, TRUE, FALSE, 'Corporate Affairs', NULL, NULL);
SELECT ROW_COUNT();

INSERT INTO `Departments` (`Id`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `Description`, `HeadOfDepartmentUserId`, `IsActive`, `IsDeleted`, `Name`, `UpdatedAt`, `UpdatedBy`)
VALUES (17, 'TRANS', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, NULL, NULL, TRUE, FALSE, 'Transport & Logistics', NULL, NULL);
SELECT ROW_COUNT();

INSERT INTO `Departments` (`Id`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `Description`, `HeadOfDepartmentUserId`, `IsActive`, `IsDeleted`, `Name`, `UpdatedAt`, `UpdatedBy`)
VALUES (18, 'WH', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, NULL, NULL, TRUE, FALSE, 'Warehouse', NULL, NULL);
SELECT ROW_COUNT();

INSERT INTO `Departments` (`Id`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `Description`, `HeadOfDepartmentUserId`, `IsActive`, `IsDeleted`, `Name`, `UpdatedAt`, `UpdatedBy`)
VALUES (19, 'PROJ', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, NULL, NULL, TRUE, FALSE, 'Projects', NULL, NULL);
SELECT ROW_COUNT();

INSERT INTO `Departments` (`Id`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `Description`, `HeadOfDepartmentUserId`, `IsActive`, `IsDeleted`, `Name`, `UpdatedAt`, `UpdatedBy`)
VALUES (20, 'IA', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, NULL, NULL, TRUE, FALSE, 'Internal Audit', NULL, NULL);
SELECT ROW_COUNT();

INSERT INTO `Departments` (`Id`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `Description`, `HeadOfDepartmentUserId`, `IsActive`, `IsDeleted`, `Name`, `UpdatedAt`, `UpdatedBy`)
VALUES (21, 'MED', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, NULL, NULL, TRUE, FALSE, 'Medical Services', NULL, NULL);
SELECT ROW_COUNT();

INSERT INTO `Departments` (`Id`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `Description`, `HeadOfDepartmentUserId`, `IsActive`, `IsDeleted`, `Name`, `UpdatedAt`, `UpdatedBy`)
VALUES (22, 'CR', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, NULL, NULL, TRUE, FALSE, 'Community Relations', NULL, NULL);
SELECT ROW_COUNT();

INSERT INTO `Departments` (`Id`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `Description`, `HeadOfDepartmentUserId`, `IsActive`, `IsDeleted`, `Name`, `UpdatedAt`, `UpdatedBy`)
VALUES (23, 'SUS', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, NULL, NULL, TRUE, FALSE, 'Sustainability', NULL, NULL);
SELECT ROW_COUNT();

INSERT INTO `Departments` (`Id`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `Description`, `HeadOfDepartmentUserId`, `IsActive`, `IsDeleted`, `Name`, `UpdatedAt`, `UpdatedBy`)
VALUES (24, 'BD', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, NULL, NULL, TRUE, FALSE, 'Business Development', NULL, NULL);
SELECT ROW_COUNT();


INSERT INTO `Roles` (`Id`, `ConcurrencyStamp`, `Name`, `NormalizedName`)
VALUES ('candidate', 'candidate', 'Candidate', 'CANDIDATE');
SELECT ROW_COUNT();

INSERT INTO `Roles` (`Id`, `ConcurrencyStamp`, `Name`, `NormalizedName`)
VALUES ('employee', 'employee', 'Employee', 'EMPLOYEE');
SELECT ROW_COUNT();

INSERT INTO `Roles` (`Id`, `ConcurrencyStamp`, `Name`, `NormalizedName`)
VALUES ('head-of-department', 'head-of-department', 'Head of Department', 'HEAD OF DEPARTMENT');
SELECT ROW_COUNT();

INSERT INTO `Roles` (`Id`, `ConcurrencyStamp`, `Name`, `NormalizedName`)
VALUES ('hr-administrator', 'hr-administrator', 'HR Administrator', 'HR ADMINISTRATOR');
SELECT ROW_COUNT();

INSERT INTO `Roles` (`Id`, `ConcurrencyStamp`, `Name`, `NormalizedName`)
VALUES ('hr-manager', 'hr-manager', 'HR Manager', 'HR MANAGER');
SELECT ROW_COUNT();

INSERT INTO `Roles` (`Id`, `ConcurrencyStamp`, `Name`, `NormalizedName`)
VALUES ('interview-panel-member', 'interview-panel-member', 'Interview Panel Member', 'INTERVIEW PANEL MEMBER');
SELECT ROW_COUNT();

INSERT INTO `Roles` (`Id`, `ConcurrencyStamp`, `Name`, `NormalizedName`)
VALUES ('managing-director', 'managing-director', 'Managing Director', 'MANAGING DIRECTOR');
SELECT ROW_COUNT();

INSERT INTO `Roles` (`Id`, `ConcurrencyStamp`, `Name`, `NormalizedName`)
VALUES ('recruitment-officer', 'recruitment-officer', 'Recruitment Officer', 'RECRUITMENT OFFICER');
SELECT ROW_COUNT();


INSERT INTO `Sites` (`Id`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `IsActive`, `IsDeleted`, `Location`, `Name`, `UpdatedAt`, `UpdatedBy`)
VALUES (1, 'HQ', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, TRUE, FALSE, NULL, 'Head Office', NULL, NULL);
SELECT ROW_COUNT();


CREATE INDEX `IX_AuditLogs_Action` ON `AuditLogs` (`Action`);

CREATE INDEX `IX_AuditLogs_EntityName_EntityId` ON `AuditLogs` (`EntityName`, `EntityId`);

CREATE INDEX `IX_AuditLogs_Timestamp` ON `AuditLogs` (`Timestamp`);

CREATE INDEX `IX_AuditLogs_UserName` ON `AuditLogs` (`UserName`);

CREATE UNIQUE INDEX `IX_Departments_Code` ON `Departments` (`Code`);

CREATE INDEX `IX_Departments_HeadOfDepartmentUserId` ON `Departments` (`HeadOfDepartmentUserId`);

CREATE INDEX `IX_Departments_IsDeleted` ON `Departments` (`IsDeleted`);

CREATE UNIQUE INDEX `IX_Departments_Name` ON `Departments` (`Name`);

CREATE INDEX `IX_RoleClaims_RoleId` ON `RoleClaims` (`RoleId`);

CREATE UNIQUE INDEX `RoleNameIndex` ON `Roles` (`NormalizedName`);

CREATE UNIQUE INDEX `IX_Sites_Code` ON `Sites` (`Code`);

CREATE INDEX `IX_Sites_IsDeleted` ON `Sites` (`IsDeleted`);

CREATE UNIQUE INDEX `IX_Sites_Name` ON `Sites` (`Name`);

CREATE UNIQUE INDEX `IX_Units_DepartmentId_Code` ON `Units` (`DepartmentId`, `Code`);

CREATE UNIQUE INDEX `IX_Units_DepartmentId_Name` ON `Units` (`DepartmentId`, `Name`);

CREATE INDEX `IX_Units_IsDeleted` ON `Units` (`IsDeleted`);

CREATE INDEX `IX_UserClaims_UserId` ON `UserClaims` (`UserId`);

CREATE INDEX `IX_UserLogins_UserId` ON `UserLogins` (`UserId`);

CREATE INDEX `IX_UserRoles_RoleId` ON `UserRoles` (`RoleId`);

CREATE INDEX `EmailIndex` ON `Users` (`NormalizedEmail`);

CREATE INDEX `IX_Users_DepartmentId` ON `Users` (`DepartmentId`);

CREATE INDEX `IX_Users_IsCandidate` ON `Users` (`IsCandidate`);

CREATE INDEX `IX_Users_SiteId` ON `Users` (`SiteId`);

CREATE UNIQUE INDEX `IX_Users_StaffNumber` ON `Users` (`StaffNumber`);

CREATE UNIQUE INDEX `UserNameIndex` ON `Users` (`NormalizedUserName`);

ALTER TABLE `Departments` ADD CONSTRAINT `FK_Departments_Users_HeadOfDepartmentUserId` FOREIGN KEY (`HeadOfDepartmentUserId`) REFERENCES `Users` (`Id`) ON DELETE SET NULL;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20261104045826_Phase01_Foundation', '10.0.14');

COMMIT;

