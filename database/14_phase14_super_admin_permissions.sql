START TRANSACTION;
ALTER TABLE `RoleClaims` MODIFY `ClaimValue` varchar(300) NULL;

ALTER TABLE `RoleClaims` MODIFY `ClaimType` varchar(100) NULL;

CREATE TABLE `KnownPermissions` (
    `Name` varchar(100) NOT NULL,
    `AddedAt` datetime(6) NOT NULL,
    PRIMARY KEY (`Name`)
);

INSERT INTO `Roles` (`Id`, `ConcurrencyStamp`, `Name`, `NormalizedName`)
VALUES ('super-admin', 'super-admin', 'Super Admin', 'SUPER ADMIN');
SELECT ROW_COUNT();


CREATE UNIQUE INDEX `IX_RoleClaims_RoleId_ClaimType_ClaimValue` ON `RoleClaims` (`RoleId`, `ClaimType`, `ClaimValue`);

DROP INDEX IX_RoleClaims_RoleId ON RoleClaims;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20261105083349_Phase14_SuperAdminAndPermissions', '10.0.14');

COMMIT;

