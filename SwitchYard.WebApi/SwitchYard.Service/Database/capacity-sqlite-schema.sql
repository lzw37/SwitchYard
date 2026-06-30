CREATE TABLE IF NOT EXISTS "capacityinstance" (
    "ID" TEXT NULL,
    "Name" TEXT NULL,
    "Owner" TEXT NULL,
    "CreatedDate" DATETIME NULL,
    "IsActive" INTEGER NULL
);

CREATE TABLE IF NOT EXISTS "annotation" (
    "InstanceID" TEXT NULL,
    "StationSchemeID" TEXT NULL,
    "ID" TEXT NULL,
    "Text" TEXT NULL,
    "X" REAL NULL,
    "Y" REAL NULL,
    "FontFamily" TEXT NULL,
    "FontSize" REAL NULL,
    "FontWeight" TEXT NULL,
    "FontStyle" TEXT NULL,
    "Angle" REAL NULL,
    "TextColor" TEXT NULL
);

CREATE TABLE IF NOT EXISTS "curve" (
    "InstanceID" TEXT NULL,
    "StationSchemeID" TEXT NULL,
    "ID" TEXT NULL,
    "BindingNodeID" TEXT NULL,
    "BindingLink1ID" TEXT NULL,
    "BindingLink2ID" TEXT NULL,
    "Radius" INTEGER NULL,
    "Angle" REAL NULL,
    "TangentDistance" REAL NULL,
    "StartX" REAL NULL,
    "StartY" REAL NULL,
    "EndX" REAL NULL,
    "EndY" REAL NULL,
    "CenterX" REAL NULL,
    "CenterY" REAL NULL,
    "LargeArcFlag" INTEGER NULL,
    "SweepFlag" INTEGER NULL
);
