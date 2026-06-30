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

CREATE TABLE IF NOT EXISTS `curve` (
    `InstanceID` VARCHAR(50) NULL,
    `StationSchemeID` VARCHAR(50) NULL,
    `ID` VARCHAR(50) NULL,
    `VertexNodeID` VARCHAR(50) NULL,
    `TangentLinkID1` VARCHAR(50) NULL,
    `TangentLinkID2` VARCHAR(50) NULL,
    `Radius` DOUBLE NULL,
    `Angle` DOUBLE NULL,
    `TangentDistance` DOUBLE NULL,
    `StartX` DOUBLE NULL,
    `StartY` DOUBLE NULL,
    `EndX` DOUBLE NULL,
    `EndY` DOUBLE NULL,
    `CenterX` DOUBLE NULL,
    `CenterY` DOUBLE NULL,
    `LargeArcFlag` TINYINT NULL,
    `SweepFlag` TINYINT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
