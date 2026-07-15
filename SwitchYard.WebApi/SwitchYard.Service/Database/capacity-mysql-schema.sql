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
    `Description` LONGTEXT NULL,
    `NodeList` LONGTEXT NULL,
    `LinkList` LONGTEXT NULL,
    `SwitchList` LONGTEXT NULL,
    `CellList` LONGTEXT NULL,
    `InterruptCellList` LONGTEXT NULL,
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

CREATE TABLE IF NOT EXISTS `operationplan` (
    `InstanceID` VARCHAR(50) NULL,
    `StationSchemeID` VARCHAR(50) NULL,
    `OperationPlanID` VARCHAR(50) NULL,
    `Name` VARCHAR(100) NULL,
    `Description` VARCHAR(500) NULL,
    `SortOrder` INT NULL,
    `CreatedDate` DATETIME NULL,
    `UpdatedDate` DATETIME NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `traintemplate` (
    `InstanceID` VARCHAR(50) NULL,
    `StationSchemeID` VARCHAR(50) NULL,
    `OperationPlanID` VARCHAR(50) NULL,
    `TrainTemplateID` VARCHAR(50) NULL,
    `Name` VARCHAR(50) NULL,
    `Type` VARCHAR(50) NULL,
    `Number` INT NULL,
    `IsFixedOperation` TINYINT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `movementtemplate` (
    `InstanceID` VARCHAR(50) NULL,
    `StationSchemeID` VARCHAR(50) NULL,
    `OperationPlanID` VARCHAR(50) NULL,
    `TrainTemplateID` VARCHAR(50) NULL,
    `MovementID` VARCHAR(50) NULL,
    `Name` VARCHAR(50) NULL,
    `RouteIDList` LONGTEXT NULL,
    `MinDuration` INT NULL,
    `SortOrder` INT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `train` (
    `InstanceID` VARCHAR(50) NULL,
    `StationSchemeID` VARCHAR(50) NULL,
    `OperationPlanID` VARCHAR(50) NULL,
    `ID` VARCHAR(50) NULL,
    `TrainTemplateID` VARCHAR(50) NULL,
    `TrainNumber` VARCHAR(50) NULL,
    `Name` VARCHAR(50) NULL,
    `TrainType` VARCHAR(20) NULL,
    `IsFixedOperation` TINYINT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `movement` (
    `InstanceID` VARCHAR(50) NULL,
    `StationSchemeID` VARCHAR(50) NULL,
    `OperationPlanID` VARCHAR(50) NULL,
    `TrainID` VARCHAR(50) NULL,
    `TrainTemplateID` VARCHAR(50) NULL,
    `MovementID` VARCHAR(50) NULL,
    `Name` VARCHAR(50) NULL,
    `RouteIDList` LONGTEXT NULL,
    `MinDuration` INT NULL,
    `EarliestStartTime` VARCHAR(50) NULL,
    `LatestEndTime` VARCHAR(50) NULL,
    `Route` VARCHAR(50) NULL,
    `Tag` VARCHAR(50) NULL,
    `SortOrder` INT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `operationbottlenecksummarycategory` (
    `InstanceID` VARCHAR(50) NULL,
    `StationSchemeID` VARCHAR(50) NULL,
    `OperationPlanID` VARCHAR(50) NULL,
    `CategoryID` VARCHAR(50) NULL,
    `Name` VARCHAR(100) NULL,
    `SortOrder` INT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `operationbottlenecksummarycategoryroute` (
    `InstanceID` VARCHAR(50) NULL,
    `StationSchemeID` VARCHAR(50) NULL,
    `OperationPlanID` VARCHAR(50) NULL,
    `CategoryID` VARCHAR(50) NULL,
    `RouteID` VARCHAR(50) NULL,
    `SortOrder` INT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `operationanalysismeta` (
    `InstanceID` VARCHAR(50) NULL,
    `StationSchemeID` VARCHAR(50) NULL,
    `OperationPlanID` VARCHAR(50) NULL,
    `TotalTimeSeconds` INT NULL,
    `UpdatedDate` DATETIME NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `operationanalysiscell` (
    `InstanceID` VARCHAR(50) NULL,
    `StationSchemeID` VARCHAR(50) NULL,
    `OperationPlanID` VARCHAR(50) NULL,
    `CellID` VARCHAR(50) NULL,
    `CellName` VARCHAR(100) NULL,
    `SortOrder` INT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `operationoccupationtimerow` (
    `InstanceID` VARCHAR(50) NULL,
    `StationSchemeID` VARCHAR(50) NULL,
    `OperationPlanID` VARCHAR(50) NULL,
    `RowKey` VARCHAR(100) NULL,
    `RowType` VARCHAR(20) NULL,
    `SequenceText` VARCHAR(50) NULL,
    `RouteID` VARCHAR(50) NULL,
    `RouteName` VARCHAR(200) NULL,
    `OperationCountText` VARCHAR(50) NULL,
    `SortOrder` INT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `operationoccupationtimecell` (
    `InstanceID` VARCHAR(50) NULL,
    `StationSchemeID` VARCHAR(50) NULL,
    `OperationPlanID` VARCHAR(50) NULL,
    `RowKey` VARCHAR(100) NULL,
    `CellID` VARCHAR(50) NULL,
    `CellValue` DOUBLE NULL,
    `InterruptCellValue` DOUBLE NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `operationoccupationtimesubtable` (
    `InstanceID` VARCHAR(50) NULL,
    `StationSchemeID` VARCHAR(50) NULL,
    `OperationPlanID` VARCHAR(50) NULL,
    `SubTableID` VARCHAR(50) NULL,
    `SubTableName` VARCHAR(100) NULL,
    `CellIDList` LONGTEXT NULL,
    `SortOrder` INT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `operationbottleneckanalysisresult` (
    `InstanceID` VARCHAR(50) NULL,
    `StationSchemeID` VARCHAR(50) NULL,
    `OperationPlanID` VARCHAR(50) NULL,
    `RouteID` VARCHAR(50) NULL,
    `RouteName` VARCHAR(200) NULL,
    `OperationCount` INT NULL,
    `BottleneckCellID` VARCHAR(50) NULL,
    `BottleneckCellName` VARCHAR(100) NULL,
    `BottleneckUtilization` DOUBLE NULL,
    `ThroughputCapacity` DOUBLE NULL,
    `SortOrder` INT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `operationthroughputsummaryresult` (
    `InstanceID` VARCHAR(50) NULL,
    `StationSchemeID` VARCHAR(50) NULL,
    `OperationPlanID` VARCHAR(50) NULL,
    `CategoryID` VARCHAR(50) NULL,
    `GroupKey` VARCHAR(50) NULL,
    `GroupText` VARCHAR(100) NULL,
    `RouteCount` INT NULL,
    `OperationCount` INT NULL,
    `CapacityTotal` DOUBLE NULL,
    `CapacityAverage` DOUBLE NULL,
    `SortOrder` INT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `operationthroughputsummaryroute` (
    `InstanceID` VARCHAR(50) NULL,
    `StationSchemeID` VARCHAR(50) NULL,
    `OperationPlanID` VARCHAR(50) NULL,
    `CategoryID` VARCHAR(50) NULL,
    `RouteID` VARCHAR(50) NULL,
    `SortOrder` INT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
