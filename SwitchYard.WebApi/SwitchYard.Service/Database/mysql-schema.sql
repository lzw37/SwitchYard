CREATE TABLE IF NOT EXISTS `user` (
    `id` VARCHAR(50) NULL,
    `name` VARCHAR(50) NULL,
    `passwordhash` VARCHAR(255) NULL,
    `createat` DATETIME NULL,
    `role` VARCHAR(50) NULL,
    `isactive` TINYINT NULL,
    `email` VARCHAR(255) NULL,
    `mustchangepassword` TINYINT NOT NULL DEFAULT 0
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS `refreshtoken` (
    `token` VARCHAR(128) NOT NULL,
    `userid` VARCHAR(50) NOT NULL,
    `expires` VARCHAR(40) NOT NULL,
    `createdat` VARCHAR(40) NOT NULL,
    `isrevoked` TINYINT NOT NULL DEFAULT 0,
    `replacedbyttoken` VARCHAR(128) NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS `humpinstance` (
    `ID` VARCHAR(50) NULL,
    `Name` VARCHAR(100) NULL,
    `Owner` VARCHAR(50) NULL,
    `CreatedDate` DATETIME NULL,
    `IsActive` TINYINT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS `slopeline` (
    `ID` VARCHAR(50) NULL,
    `Name` VARCHAR(100) NULL,
    `InstanceID` VARCHAR(50) NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS `position` (
    `ID` VARCHAR(50) NULL,
    `X` DOUBLE NULL,
    `Height` DOUBLE NULL,
    `InstanceID` VARCHAR(50) NULL,
    `SlopeLineID` VARCHAR(50) NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS `positionsegment` (
    `ID` VARCHAR(50) NOT NULL,
    `StartPositionID` VARCHAR(50) NOT NULL,
    `EndPositionID` VARCHAR(50) NOT NULL,
    `Length` DOUBLE NOT NULL,
    `CurveDegree` DOUBLE NOT NULL,
    `LocationParam` INT NOT NULL,
    `CurveDirection` VARCHAR(10) NOT NULL DEFAULT '',
    `InstanceID` VARCHAR(50) NULL,
    `SlopeLineID` VARCHAR(50) NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS `switch` (
    `BindingPositionID` VARCHAR(50) NULL,
    `BindingPositionSegmentID` VARCHAR(50) NULL,
    `Type` VARCHAR(50) NULL,
    `Direction` VARCHAR(50) NULL,
    `Side` VARCHAR(50) NULL,
    `ID` VARCHAR(50) NULL,
    `CurveDegree` DOUBLE NULL,
    `InstanceID` VARCHAR(50) NULL,
    `SlopeLineID` VARCHAR(50) NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS `retarder` (
    `BindingPositionSegmentID` VARCHAR(50) NULL,
    `Numbers` VARCHAR(50) NULL,
    `ID` VARCHAR(50) NULL,
    `InstanceID` VARCHAR(50) NULL,
    `SlopeLineID` VARCHAR(50) NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS `wagonconcept` (
    `TypeName` VARCHAR(50) NULL,
    `Length` DOUBLE NULL,
    `NetMass` DOUBLE NULL,
    `LoadingMass` DOUBLE NULL,
    `WindwardArea` DOUBLE NULL,
    `AxleNumber` INT NULL,
    `Label` VARCHAR(50) NULL,
    `InstanceID` VARCHAR(50) NULL,
    `g` DOUBLE NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS `operationcondition` (
    `InstanceID` VARCHAR(50) NULL,
    `ID` VARCHAR(50) NULL,
    `WagonVelocityOnTop` DOUBLE NULL,
    `WagonVelocityOnSlope` DOUBLE NULL,
    `WagonVelocityOnYard` DOUBLE NULL,
    `WindVelocity` DOUBLE NULL,
    `IsHeadWind` TINYINT NULL,
    `AirDensity` DOUBLE NULL,
    `Temperature` DOUBLE NULL,
    `Name` VARCHAR(100) NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS `humpscheme` (
    `InstanceID` VARCHAR(50) NULL,
    `ID` VARCHAR(50) NULL,
    `Name` VARCHAR(100) NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS `vposition` (
    `ID` VARCHAR(50) NULL,
    `X` DOUBLE NULL,
    `Height` DOUBLE NULL,
    `InstanceID` VARCHAR(50) NULL,
    `HumpSchemeID` VARCHAR(50) NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS `vpositionsegment` (
    `ID` VARCHAR(50) NOT NULL,
    `StartPositionID` VARCHAR(50) NOT NULL,
    `EndPositionID` VARCHAR(50) NOT NULL,
    `Length` DOUBLE NOT NULL,
    `Gradient` DOUBLE NOT NULL,
    `Height` DOUBLE NULL,
    `InstanceID` VARCHAR(50) NULL,
    `HumpSchemeID` VARCHAR(50) NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS `humpcalculation` (
    `InstanceID` VARCHAR(50) NULL,
    `HumpSchemeID` VARCHAR(50) NULL,
    `ID` VARCHAR(50) NULL,
    `WagonType` VARCHAR(50) NULL,
    `OperationConditionID` VARCHAR(50) NULL,
    `SlopeLineID` VARCHAR(50) NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS `humpcalculationdata` (
    `InstanceID` VARCHAR(50) NULL,
    `HumpSchemeID` VARCHAR(50) NULL,
    `HumpCalculationID` VARCHAR(50) NULL,
    `X` DOUBLE NULL,
    `GravityEnergyHeight` DOUBLE NULL,
    `ResistanceEnergyHeight` DOUBLE NULL,
    `KineticEnergyHeight` DOUBLE NULL,
    `BreakingEnergyHeight` DOUBLE NULL,
    `InitTotalEnergyHeight` DOUBLE NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS `retarderstatus` (
    `InstanceID` VARCHAR(50) NULL,
    `RetarderID` VARCHAR(50) NULL,
    `IsActivated` TINYINT NULL,
    `Output` DOUBLE NULL,
    `TotalEnergyHeight` DOUBLE NULL,
    `HumpCalculationID` VARCHAR(50) NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS `headwaycheckscheme` (
    `InstanceID` VARCHAR(50) NULL,
    `ID` VARCHAR(50) NULL,
    `Name` VARCHAR(100) NULL,
    `HumpSchemeID` VARCHAR(50) NULL,
    `WagonVelocityOnTop` DOUBLE NULL,
    `SlopeLineID` VARCHAR(50) NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS `headwaycheckwagon` (
    `InstanceID` VARCHAR(50) NULL,
    `HeadwayCheckID` VARCHAR(50) NULL,
    `Sequence` INT NULL,
    `HumpCalculationID` VARCHAR(50) NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS `headwaycheckdata` (
    `InstanceID` VARCHAR(50) NULL,
    `HeadwayCheckID` VARCHAR(50) NULL,
    `Sequence` INT NULL,
    `X` DOUBLE NULL,
    `Velocity` DOUBLE NULL,
    `TimeSpan` DOUBLE NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS `headwaycheckresult` (
    `InstanceID` VARCHAR(50) NULL,
    `HeadwayCheckID` VARCHAR(50) NULL,
    `EquipmentType` VARCHAR(50) NULL,
    `EquipmentID` VARCHAR(50) NULL,
    `Headway` DOUBLE NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
