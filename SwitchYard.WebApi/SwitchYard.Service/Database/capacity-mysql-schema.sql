CREATE TABLE IF NOT EXISTS `capacityinstance` (
    `ID` VARCHAR(50) NULL,
    `Name` VARCHAR(100) NULL,
    `Owner` VARCHAR(50) NULL,
    `CreatedDate` DATETIME NULL,
    `IsActive` TINYINT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS `annotation` (
    `InstanceID` VARCHAR(50) NULL,
    `StationSchemeID` VARCHAR(50) NULL,
    `ID` VARCHAR(50) NULL,
    `Text` TEXT NULL,
    `X` DOUBLE NULL,
    `Y` DOUBLE NULL,
    `FontFamily` VARCHAR(100) NULL,
    `FontSize` DOUBLE NULL,
    `FontWeight` VARCHAR(20) NULL,
    `FontStyle` VARCHAR(20) NULL,
    `Angle` DOUBLE NULL,
    `TextColor` VARCHAR(30) NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
