CREATE TABLE IF NOT EXISTS "user" (
    "id" TEXT NULL,
    "name" TEXT NULL,
    "passwordhash" TEXT NULL,
    "createat" DATETIME NULL,
    "role" TEXT NULL,
    "isactive" INTEGER NULL,
    "email" TEXT NULL,
    "mustchangepassword" INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS "refreshtoken" (
    "token" TEXT NOT NULL,
    "userid" TEXT NOT NULL,
    "expires" TEXT NOT NULL,
    "createdat" TEXT NOT NULL,
    "isrevoked" INTEGER NOT NULL DEFAULT 0,
    "replacedbyttoken" TEXT NULL
);

CREATE TABLE IF NOT EXISTS "humpinstance" (
    "ID" TEXT NULL,
    "Name" TEXT NULL,
    "Owner" TEXT NULL,
    "CreatedDate" DATETIME NULL,
    "IsActive" INTEGER NULL
);

CREATE TABLE IF NOT EXISTS "slopeline" (
    "ID" TEXT NULL,
    "Name" TEXT NULL,
    "InstanceID" TEXT NULL
);

CREATE TABLE IF NOT EXISTS "position" (
    "ID" TEXT NULL,
    "X" REAL NULL,
    "Height" REAL NULL,
    "InstanceID" TEXT NULL,
    "SlopeLineID" TEXT NULL
);

CREATE TABLE IF NOT EXISTS "positionsegment" (
    "ID" TEXT NOT NULL,
    "StartPositionID" TEXT NOT NULL,
    "EndPositionID" TEXT NOT NULL,
    "Length" REAL NOT NULL,
    "CurveDegree" REAL NOT NULL,
    "LocationParam" INTEGER NOT NULL,
    "CurveDirection" TEXT NOT NULL DEFAULT '',
    "InstanceID" TEXT NULL,
    "SlopeLineID" TEXT NULL
);

CREATE TABLE IF NOT EXISTS "switch" (
    "BindingPositionID" TEXT NULL,
    "BindingPositionSegmentID" TEXT NULL,
    "Type" TEXT NULL,
    "Direction" TEXT NULL,
    "Side" TEXT NULL,
    "ID" TEXT NULL,
    "CurveDegree" REAL NULL,
    "InstanceID" TEXT NULL,
    "SlopeLineID" TEXT NULL
);

CREATE TABLE IF NOT EXISTS "retarder" (
    "BindingPositionSegmentID" TEXT NULL,
    "Numbers" TEXT NULL,
    "ID" TEXT NULL,
    "InstanceID" TEXT NULL,
    "SlopeLineID" TEXT NULL
);

CREATE TABLE IF NOT EXISTS "wagonconcept" (
    "TypeName" TEXT NULL,
    "Length" REAL NULL,
    "NetMass" REAL NULL,
    "LoadingMass" REAL NULL,
    "WindwardArea" REAL NULL,
    "AxleNumber" INTEGER NULL,
    "Label" TEXT NULL,
    "InstanceID" TEXT NULL,
    "g" REAL NULL
);

CREATE TABLE IF NOT EXISTS "operationcondition" (
    "InstanceID" TEXT NULL,
    "ID" TEXT NULL,
    "WagonVelocityOnTop" REAL NULL,
    "WagonVelocityOnSlope" REAL NULL,
    "WagonVelocityOnYard" REAL NULL,
    "WindVelocity" REAL NULL,
    "IsHeadWind" INTEGER NULL,
    "AirDensity" REAL NULL,
    "Temperature" REAL NULL,
    "Name" TEXT NULL
);

CREATE TABLE IF NOT EXISTS "humpscheme" (
    "InstanceID" TEXT NULL,
    "ID" TEXT NULL,
    "Name" TEXT NULL
);

CREATE TABLE IF NOT EXISTS "vposition" (
    "ID" TEXT NULL,
    "X" REAL NULL,
    "Height" REAL NULL,
    "InstanceID" TEXT NULL,
    "HumpSchemeID" TEXT NULL
);

CREATE TABLE IF NOT EXISTS "vpositionsegment" (
    "ID" TEXT NOT NULL,
    "StartPositionID" TEXT NOT NULL,
    "EndPositionID" TEXT NOT NULL,
    "Length" REAL NOT NULL,
    "Gradient" REAL NOT NULL,
    "Height" REAL NULL,
    "InstanceID" TEXT NULL,
    "HumpSchemeID" TEXT NULL
);

CREATE TABLE IF NOT EXISTS "humpcalculation" (
    "InstanceID" TEXT NULL,
    "HumpSchemeID" TEXT NULL,
    "ID" TEXT NULL,
    "WagonType" TEXT NULL,
    "OperationConditionID" TEXT NULL,
    "SlopeLineID" TEXT NULL
);

CREATE TABLE IF NOT EXISTS "humpcalculationdata" (
    "InstanceID" TEXT NULL,
    "HumpSchemeID" TEXT NULL,
    "HumpCalculationID" TEXT NULL,
    "X" REAL NULL,
    "GravityEnergyHeight" REAL NULL,
    "ResistanceEnergyHeight" REAL NULL,
    "KineticEnergyHeight" REAL NULL,
    "BreakingEnergyHeight" REAL NULL,
    "InitTotalEnergyHeight" REAL NULL
);

CREATE TABLE IF NOT EXISTS "retarderstatus" (
    "InstanceID" TEXT NULL,
    "RetarderID" TEXT NULL,
    "IsActivated" INTEGER NULL,
    "Output" REAL NULL,
    "TotalEnergyHeight" REAL NULL,
    "HumpCalculationID" TEXT NULL
);

CREATE TABLE IF NOT EXISTS "headwaycheckscheme" (
    "InstanceID" TEXT NULL,
    "ID" TEXT NULL,
    "Name" TEXT NULL,
    "HumpSchemeID" TEXT NULL,
    "WagonVelocityOnTop" REAL NULL,
    "SlopeLineID" TEXT NULL
);

CREATE TABLE IF NOT EXISTS "headwaycheckwagon" (
    "InstanceID" TEXT NULL,
    "HeadwayCheckID" TEXT NULL,
    "Sequence" INTEGER NULL,
    "HumpCalculationID" TEXT NULL
);

CREATE TABLE IF NOT EXISTS "headwaycheckdata" (
    "InstanceID" TEXT NULL,
    "HeadwayCheckID" TEXT NULL,
    "Sequence" INTEGER NULL,
    "X" REAL NULL,
    "Velocity" REAL NULL,
    "TimeSpan" REAL NULL
);

CREATE TABLE IF NOT EXISTS "headwaycheckresult" (
    "InstanceID" TEXT NULL,
    "HeadwayCheckID" TEXT NULL,
    "EquipmentType" TEXT NULL,
    "EquipmentID" TEXT NULL,
    "Headway" REAL NULL
);
