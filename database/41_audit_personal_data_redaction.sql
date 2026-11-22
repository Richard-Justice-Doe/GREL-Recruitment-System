START TRANSACTION;
UPDATE `AuditLogs` SET `OldValues` = JSON_REPLACE(`OldValues`, '$.Qualification', '(personal data)', '$.SsnitNumber', '(personal data)', '$.DateOfBirth', '(personal data)', '$.ResidentialAddress', '(personal data)', '$.HomeTown', '(personal data)', '$.Region', '(personal data)', '$.Tribe', '(personal data)', '$.Religion', '(personal data)', '$.Email', '(personal data)', '$.MobileNumber', '(personal data)', '$.HasDisability', '(personal data)', '$.DisabilityDetails', '(personal data)', '$.VisibleMarks', '(personal data)', '$.NextOfKinName', '(personal data)', '$.NextOfKinTelephone', '(personal data)', '$.NextOfKinRelationship', '(personal data)', '$.NextOfKinDateOfBirth', '(personal data)', '$.NextOfKinAddress', '(personal data)', '$.CreatedAt', '(personal data)', '$.CreatedBy', '(personal data)', '$.UpdatedAt', '(personal data)', '$.UpdatedBy', '(personal data)', '$.DeletedAt', '(personal data)', '$.DeletedBy', '(personal data)') WHERE `EntityName` = 'EmployeePersonalRecord' AND `OldValues` IS NOT NULL AND JSON_VALID(`OldValues`);

UPDATE `AuditLogs` SET `NewValues` = JSON_REPLACE(`NewValues`, '$.Qualification', '(personal data)', '$.SsnitNumber', '(personal data)', '$.DateOfBirth', '(personal data)', '$.ResidentialAddress', '(personal data)', '$.HomeTown', '(personal data)', '$.Region', '(personal data)', '$.Tribe', '(personal data)', '$.Religion', '(personal data)', '$.Email', '(personal data)', '$.MobileNumber', '(personal data)', '$.HasDisability', '(personal data)', '$.DisabilityDetails', '(personal data)', '$.VisibleMarks', '(personal data)', '$.NextOfKinName', '(personal data)', '$.NextOfKinTelephone', '(personal data)', '$.NextOfKinRelationship', '(personal data)', '$.NextOfKinDateOfBirth', '(personal data)', '$.NextOfKinAddress', '(personal data)', '$.CreatedAt', '(personal data)', '$.CreatedBy', '(personal data)', '$.UpdatedAt', '(personal data)', '$.UpdatedBy', '(personal data)', '$.DeletedAt', '(personal data)', '$.DeletedBy', '(personal data)') WHERE `EntityName` = 'EmployeePersonalRecord' AND `NewValues` IS NOT NULL AND JSON_VALID(`NewValues`);

UPDATE `AuditLogs` SET `OldValues` = JSON_REPLACE(`OldValues`, '$.BankName', '(personal data)', '$.Branch', '(personal data)', '$.AccountNumber', '(personal data)') WHERE `EntityName` = 'EmployeeBankAccount' AND `OldValues` IS NOT NULL AND JSON_VALID(`OldValues`);

UPDATE `AuditLogs` SET `NewValues` = JSON_REPLACE(`NewValues`, '$.BankName', '(personal data)', '$.Branch', '(personal data)', '$.AccountNumber', '(personal data)') WHERE `EntityName` = 'EmployeeBankAccount' AND `NewValues` IS NOT NULL AND JSON_VALID(`NewValues`);

UPDATE `AuditLogs` SET `OldValues` = JSON_REPLACE(`OldValues`, '$.FullName', '(personal data)', '$.Occupation', '(personal data)', '$.DateOfBirth', '(personal data)', '$.Address', '(personal data)', '$.Telephone', '(personal data)') WHERE `EntityName` = 'EmployeeSpouse' AND `OldValues` IS NOT NULL AND JSON_VALID(`OldValues`);

UPDATE `AuditLogs` SET `NewValues` = JSON_REPLACE(`NewValues`, '$.FullName', '(personal data)', '$.Occupation', '(personal data)', '$.DateOfBirth', '(personal data)', '$.Address', '(personal data)', '$.Telephone', '(personal data)') WHERE `EntityName` = 'EmployeeSpouse' AND `NewValues` IS NOT NULL AND JSON_VALID(`NewValues`);

UPDATE `AuditLogs` SET `OldValues` = JSON_REPLACE(`OldValues`, '$.FullName', '(personal data)', '$.Gender', '(personal data)', '$.DateOfBirth', '(personal data)', '$.PlaceOfBirth', '(personal data)', '$.CurrentResidence', '(personal data)') WHERE `EntityName` = 'EmployeeChild' AND `OldValues` IS NOT NULL AND JSON_VALID(`OldValues`);

UPDATE `AuditLogs` SET `NewValues` = JSON_REPLACE(`NewValues`, '$.FullName', '(personal data)', '$.Gender', '(personal data)', '$.DateOfBirth', '(personal data)', '$.PlaceOfBirth', '(personal data)', '$.CurrentResidence', '(personal data)') WHERE `EntityName` = 'EmployeeChild' AND `NewValues` IS NOT NULL AND JSON_VALID(`NewValues`);

UPDATE `AuditLogs` SET `OldValues` = JSON_REPLACE(`OldValues`, '$.FullName', '(personal data)', '$.Relationship', '(personal data)', '$.Age', '(personal data)') WHERE `EntityName` = 'EmployeeDependant' AND `OldValues` IS NOT NULL AND JSON_VALID(`OldValues`);

UPDATE `AuditLogs` SET `NewValues` = JSON_REPLACE(`NewValues`, '$.FullName', '(personal data)', '$.Relationship', '(personal data)', '$.Age', '(personal data)') WHERE `EntityName` = 'EmployeeDependant' AND `NewValues` IS NOT NULL AND JSON_VALID(`NewValues`);

UPDATE `AuditLogs` SET `OldValues` = JSON_REPLACE(`OldValues`, '$.Name', '(personal data)', '$.Relationship', '(personal data)', '$.Telephone1', '(personal data)', '$.Telephone2', '(personal data)') WHERE `EntityName` = 'EmployeeEmergencyContact' AND `OldValues` IS NOT NULL AND JSON_VALID(`OldValues`);

UPDATE `AuditLogs` SET `NewValues` = JSON_REPLACE(`NewValues`, '$.Name', '(personal data)', '$.Relationship', '(personal data)', '$.Telephone1', '(personal data)', '$.Telephone2', '(personal data)') WHERE `EntityName` = 'EmployeeEmergencyContact' AND `NewValues` IS NOT NULL AND JSON_VALID(`NewValues`);

UPDATE `AuditLogs` SET `OldValues` = JSON_REPLACE(`OldValues`, '$.Name', '(personal data)', '$.Relationship', '(personal data)', '$.DateOfBirth', '(personal data)', '$.Percentage', '(personal data)') WHERE `EntityName` = 'EmployeeBeneficiary' AND `OldValues` IS NOT NULL AND JSON_VALID(`OldValues`);

UPDATE `AuditLogs` SET `NewValues` = JSON_REPLACE(`NewValues`, '$.Name', '(personal data)', '$.Relationship', '(personal data)', '$.DateOfBirth', '(personal data)', '$.Percentage', '(personal data)') WHERE `EntityName` = 'EmployeeBeneficiary' AND `NewValues` IS NOT NULL AND JSON_VALID(`NewValues`);

UPDATE `AuditLogs` SET `OldValues` = JSON_REPLACE(`OldValues`, '$.DeclarationText', '(personal data)', '$.SignatureName', '(personal data)', '$.IpAddress', '(personal data)', '$.Snapshot', '(personal data)', '$.CreatedAt', '(personal data)', '$.CreatedBy', '(personal data)', '$.UpdatedAt', '(personal data)', '$.UpdatedBy', '(personal data)', '$.DeletedAt', '(personal data)', '$.DeletedBy', '(personal data)') WHERE `EntityName` = 'EmployeeDeclaration' AND `OldValues` IS NOT NULL AND JSON_VALID(`OldValues`);

UPDATE `AuditLogs` SET `NewValues` = JSON_REPLACE(`NewValues`, '$.DeclarationText', '(personal data)', '$.SignatureName', '(personal data)', '$.IpAddress', '(personal data)', '$.Snapshot', '(personal data)', '$.CreatedAt', '(personal data)', '$.CreatedBy', '(personal data)', '$.UpdatedAt', '(personal data)', '$.UpdatedBy', '(personal data)', '$.DeletedAt', '(personal data)', '$.DeletedBy', '(personal data)') WHERE `EntityName` = 'EmployeeDeclaration' AND `NewValues` IS NOT NULL AND JSON_VALID(`NewValues`);

UPDATE `AuditLogs` SET `OldValues` = JSON_REPLACE(`OldValues`, '$.DateOfBirth', '(personal data)') WHERE `EntityName` = 'Employee' AND `OldValues` IS NOT NULL AND JSON_VALID(`OldValues`);

UPDATE `AuditLogs` SET `NewValues` = JSON_REPLACE(`NewValues`, '$.DateOfBirth', '(personal data)') WHERE `EntityName` = 'Employee' AND `NewValues` IS NOT NULL AND JSON_VALID(`NewValues`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20261122201735_AuditPersonalDataRedaction', '10.0.14');

COMMIT;

