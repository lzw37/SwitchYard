CREATE TABLE IF NOT EXISTS "capacityinstance" (
    "ID" TEXT NULL,
    "Name" TEXT NULL,
    "Owner" TEXT NULL,
    "CreatedDate" DATETIME NULL,
    "IsActive" INTEGER NULL
);

CREATE TABLE IF NOT EXISTS "stationscheme" (
    "InstanceID" TEXT NULL,
    "ID" TEXT NULL,
    "Name" TEXT NULL,
    "DisplayStyles" TEXT NULL,
    "GridSettings" TEXT NULL
);

CREATE TABLE IF NOT EXISTS "stationrouteend" (
    "InstanceID" TEXT NULL,
    "StationSchemeID" TEXT NULL,
    "ID" TEXT NULL,
    "BindingNodeID" TEXT NULL,
    "Type" TEXT NULL,
    "SegmentTag" TEXT NULL,
    "SidingTag" TEXT NULL
);

CREATE TABLE IF NOT EXISTS "stationroute" (
    "InstanceID" TEXT NULL,
    "StationSchemeID" TEXT NULL,
    "ID" TEXT NULL,
    "Type" TEXT NULL,
    "Description" TEXT NULL,
    "NodeList" TEXT NULL,
    "LinkList" TEXT NULL,
    "SwitchList" TEXT NULL,
    "CellList" TEXT NULL,
    "SignalList" TEXT NULL,
    "AllowanceTags" TEXT NULL,
    "ForbiddenTags" TEXT NULL,
    "StartNodeID" TEXT NULL,
    "EndNodeID" TEXT NULL
);

CREATE TABLE IF NOT EXISTS "cell" (
    "InstanceID" TEXT NULL,
    "StationSchemeID" TEXT NULL,
    "ID" TEXT NULL,
    "LinkIDList" TEXT NULL,
    "Name" TEXT NULL
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

CREATE TABLE IF NOT EXISTS "traintemplate" (
    "InstanceID" TEXT NULL,
    "StationSchemeID" TEXT NULL,
    "TrainTemplateID" TEXT NULL,
    "Name" TEXT NULL,
    "Type" TEXT NULL,
    "Number" INTEGER NULL
);

CREATE TABLE IF NOT EXISTS "movementtemplate" (
    "InstanceID" TEXT NULL,
    "StationSchemeID" TEXT NULL,
    "TrainTemplateID" TEXT NULL,
    "MovementID" TEXT NULL,
    "Name" TEXT NULL,
    "RouteIDList" TEXT NULL,
    "MinDuration" INTEGER NULL
);

CREATE TABLE IF NOT EXISTS "train" (
    "InstanceID" TEXT NULL,
    "StationSchemeID" TEXT NULL,
    "ID" TEXT NULL,
    "TrainTemplateID" TEXT NULL,
    "TrainNumber" TEXT NULL,
    "Name" TEXT NULL,
    "TrainType" TEXT NULL
);

CREATE TABLE IF NOT EXISTS "movement" (
    "InstanceID" TEXT NULL,
    "StationSchemeID" TEXT NULL,
    "TrainID" TEXT NULL,
    "TrainTemplateID" TEXT NULL,
    "MovementID" TEXT NULL,
    "Name" TEXT NULL,
    "RouteIDList" TEXT NULL,
    "MinDuration" INTEGER NULL,
    "EarliestStartTime" TEXT NULL,
    "LatestEndTime" TEXT NULL,
    "Route" TEXT NULL,
    "Tag" TEXT NULL
);

CREATE TABLE IF NOT EXISTS "operationbottlenecksummarycategory" (
    "InstanceID" TEXT NULL,
    "StationSchemeID" TEXT NULL,
    "CategoryID" TEXT NULL,
    "Name" TEXT NULL,
    "RouteIDList" TEXT NULL,
    "SortOrder" INTEGER NULL
);
