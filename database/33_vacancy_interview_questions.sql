START TRANSACTION;
CREATE TABLE `VacancyInterviewQuestions` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `VacancyId` int NOT NULL,
    `Category` varchar(20) NOT NULL,
    `Question` varchar(1000) NOT NULL,
    `Guidance` varchar(1000) NULL,
    `SortOrder` int NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `CreatedBy` varchar(256) NULL,
    `UpdatedAt` datetime(6) NULL,
    `UpdatedBy` varchar(256) NULL,
    `IsDeleted` tinyint(1) NOT NULL,
    `DeletedAt` datetime(6) NULL,
    `DeletedBy` varchar(256) NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_VacancyInterviewQuestions_Vacancies_VacancyId` FOREIGN KEY (`VacancyId`) REFERENCES `Vacancies` (`Id`) ON DELETE RESTRICT
);

CREATE INDEX `IX_VacancyInterviewQuestions_IsDeleted` ON `VacancyInterviewQuestions` (`IsDeleted`);

CREATE INDEX `IX_VacancyInterviewQuestions_VacancyId` ON `VacancyInterviewQuestions` (`VacancyId`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20261113163311_VacancyInterviewQuestions', '10.0.14');

COMMIT;

