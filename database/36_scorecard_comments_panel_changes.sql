START TRANSACTION;
ALTER TABLE `ScorecardRatings` ADD `Comment` varchar(1000) NULL;

CREATE TABLE `InterviewPanelChanges` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `InterviewId` int NOT NULL,
    `Action` varchar(20) NOT NULL,
    `OriginalUserId` varchar(450) NULL,
    `ReplacementUserId` varchar(450) NULL,
    `Reason` varchar(500) NULL,
    `ActorUserId` varchar(450) NOT NULL,
    `ActorName` varchar(256) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `CreatedBy` varchar(256) NULL,
    `UpdatedAt` datetime(6) NULL,
    `UpdatedBy` varchar(256) NULL,
    `IsDeleted` tinyint(1) NOT NULL,
    `DeletedAt` datetime(6) NULL,
    `DeletedBy` varchar(256) NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_InterviewPanelChanges_Interviews_InterviewId` FOREIGN KEY (`InterviewId`) REFERENCES `Interviews` (`Id`) ON DELETE RESTRICT
);

CREATE INDEX `IX_InterviewPanelChanges_InterviewId` ON `InterviewPanelChanges` (`InterviewId`);

CREATE INDEX `IX_InterviewPanelChanges_IsDeleted` ON `InterviewPanelChanges` (`IsDeleted`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20261113233315_ScorecardCommentsAndPanelChanges', '10.0.14');

COMMIT;

