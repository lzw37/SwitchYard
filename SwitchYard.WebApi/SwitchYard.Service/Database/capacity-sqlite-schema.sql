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
