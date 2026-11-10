START TRANSACTION;
DROP INDEX IX_Vacancies_Status ON Vacancies;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20261110090638_AuditDropRedundantVacancyStatusIndex', '10.0.14');

COMMIT;

