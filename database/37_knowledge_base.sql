START TRANSACTION;
CREATE TABLE `KnowledgeDocuments` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `Title` varchar(200) NOT NULL,
    `Owner` varchar(150) NOT NULL,
    `Version` varchar(30) NOT NULL,
    `EffectiveDate` date NOT NULL,
    `Audience` varchar(30) NOT NULL,
    `IsActive` tinyint(1) NOT NULL,
    `Summary` varchar(500) NULL,
    `AttachmentId` int NOT NULL,
    `FileName` varchar(200) NOT NULL,
    `ReadProblem` varchar(300) NULL,
    `SectionCount` int NOT NULL,
    `CharacterCount` int NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `CreatedBy` varchar(256) NULL,
    `UpdatedAt` datetime(6) NULL,
    `UpdatedBy` varchar(256) NULL,
    `IsDeleted` tinyint(1) NOT NULL,
    `DeletedAt` datetime(6) NULL,
    `DeletedBy` varchar(256) NULL,
    PRIMARY KEY (`Id`)
);

CREATE TABLE `KnowledgeSections` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `KnowledgeDocumentId` int NOT NULL,
    `SortOrder` int NOT NULL,
    `Heading` varchar(300) NOT NULL,
    `Text` text NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `CreatedBy` varchar(256) NULL,
    `UpdatedAt` datetime(6) NULL,
    `UpdatedBy` varchar(256) NULL,
    `IsDeleted` tinyint(1) NOT NULL,
    `DeletedAt` datetime(6) NULL,
    `DeletedBy` varchar(256) NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_KnowledgeSections_KnowledgeDocuments_KnowledgeDocumentId` FOREIGN KEY (`KnowledgeDocumentId`) REFERENCES `KnowledgeDocuments` (`Id`) ON DELETE CASCADE
);

CREATE INDEX `IX_KnowledgeDocuments_IsActive` ON `KnowledgeDocuments` (`IsActive`);

CREATE INDEX `IX_KnowledgeDocuments_IsDeleted` ON `KnowledgeDocuments` (`IsDeleted`);

CREATE INDEX `IX_KnowledgeSections_IsDeleted` ON `KnowledgeSections` (`IsDeleted`);

CREATE INDEX `IX_KnowledgeSections_KnowledgeDocumentId_SortOrder` ON `KnowledgeSections` (`KnowledgeDocumentId`, `SortOrder`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20261114154253_KnowledgeBase', '10.0.14');

COMMIT;

