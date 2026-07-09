CREATE TABLE IF NOT EXISTS `capacityinstance` (
    `ID` VARCHAR(50) NULL,
    `Name` VARCHAR(100) NULL,
    `Owner` VARCHAR(50) NULL,
    `CreatedDate` DATETIME NULL,
    `IsActive` TINYINT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS `stationscheme` (
    `InstanceID` VARCHAR(50) NULL,
    `ID` VARCHAR(50) NULL,
    `Name` VARCHAR(100) NULL,
    `DisplayStyles` TEXT NULL,
    `GridSettings` TEXT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS `stationrouteend` (
    `InstanceID` VARCHAR(50) NULL,
    `StationSchemeID` VARCHAR(50) NULL,
    `ID` VARCHAR(50) NULL,
    `BindingNodeID` VARCHAR(50) NULL,
    `Type` VARCHAR(50) NULL,
    `SegmentTag` VARCHAR(50) NULL,
    `SidingTag` VARCHAR(50) NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `stationroute` (
    `InstanceID` VARCHAR(50) NULL,
    `StationSchemeID` VARCHAR(50) NULL,
    `ID` VARCHAR(50) NULL,
    `Type` VARCHAR(50) NULL,
    `NodeList` LONGTEXT NULL,
    `LinkList` LONGTEXT NULL,
    `SwitchList` LONGTEXT NULL,
    `CellList` LONGTEXT NULL,
    `SignalList` LONGTEXT NULL,
    `AllowanceTags` LONGTEXT NULL,
    `ForbiddenTags` LONGTEXT NULL,
    `StartNodeID` VARCHAR(50) NULL,
    `EndNodeID` VARCHAR(50) NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `cell` (
    `InstanceID` VARCHAR(50) NULL,
    `StationSchemeID` VARCHAR(50) NULL,
    `ID` VARCHAR(50) NULL,
    `LinkIDList` VARCHAR(255) NULL,
    `Name` VARCHAR(100) NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

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
    `BindingNodeID` VARCHAR(50) NULL,
    `BindingLink1ID` VARCHAR(50) NULL,
    `BindingLink2ID` VARCHAR(50) NULL,
    `Radius` INT NULL,
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
